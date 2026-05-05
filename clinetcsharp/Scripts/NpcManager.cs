using Godot;
using System.Collections.Generic;
using System.Linq;

namespace ClinetCSharp
{
    /// <summary>
    /// NPC 交互选项定义
    /// </summary>
    public class NpcInteractOption
    {
        public string Label { get; set; } = "";
        public string DialogText { get; set; } = "";  // 对话内容（空=无对话）
        public bool ShowChangeJob { get; set; } = false; // 是否打开转职面板
        public bool TriggerCombat { get; set; } = false; // 是否触发战斗
    }

    /// <summary>
    /// NPC 管理器 - 管理地图上所有 NPC 实体
    /// </summary>
    public partial class NpcManager : Node
    {
        /// <summary>
        /// NPC 类型配置表 — Key = NpcType，定义每种 NPC 的交互选项和对话
        /// 新增 NPC 类型只需在此表添加条目，无需改交互逻辑
        /// </summary>
        private static readonly Dictionary<NpcType, NpcInteractOption[]> NpcInteractConfig = new()
        {
            [NpcType.JobMaster] = new[]
            {
                new NpcInteractOption { Label = "对话", DialogText = "我可以帮你改变职业，选择你想要的方向吧！" },
                new NpcInteractOption { Label = "转职", ShowChangeJob = true },
            },
            [NpcType.Combatant] = new[]
            {
                new NpcInteractOption { Label = "对话", DialogText = "哼，你有胆量挑战我吗？" },
                new NpcInteractOption { Label = "挑战", TriggerCombat = true },
            },
        };

        /// <summary>
        /// 获取 NPC 类型的默认对话（没有交互选项时显示）
        /// </summary>
        private static string GetDefaultDialog(NpcType npcType) => npcType switch
        {
            _ => "你好，旅行者！",
        };
        private List<Npc> _npcs = new();
        private Dictionary<Vector2I, Npc> _npcByPos = new();
        private int _gridSize = 111;
        private NetworkManager _network;

        public static NpcManager Instance;

        // 多配置样式系统 — Key = 配置ID（NpcType）
        // [Obsolete] 已被 EntityProfileManager 接管。保留仅用于旧代码兼容和迁移。
        public static readonly Dictionary<int, EntityStyleConfig> StyleConfigs = new();
        private static readonly HashSet<int> _missingConfigWarned = new();

        public static EntityStyleConfig GetStyleConfig(int id)
        {
            if (StyleConfigs.TryGetValue(id, out var cfg))
                return cfg;
            if (!_missingConfigWarned.Contains(id))
            {
                GD.PrintErr($"[NpcManager] No StyleConfig for NpcType={id}! Available: [{string.Join(", ", StyleConfigs.Keys)}]. Using default. Fix: create config for this ID in debug panel.");
                _missingConfigWarned.Add(id);
            }
            return EntityStyleConfig.CreateNpcDefault();
        }

        public static EntityStyleConfig GetOrCreateStyleConfig(int id)
        {
            if (StyleConfigs.TryGetValue(id, out var cfg))
                return cfg;
            GD.PrintErr($"[NpcManager] No StyleConfig for NpcType={id}! Available: [{string.Join(", ", StyleConfigs.Keys)}]. Create it in the debug panel first.");
            if (StyleConfigs.Count > 0)
                return StyleConfigs.Values.First();
            StyleConfigs[1] = EntityStyleConfig.CreateNpcDefault();
            return StyleConfigs[1];
        }

        public override void _Ready()
        {
            Instance = this;
            AddToGroup("npc_manager");

            var tree = GetTree();
            if (tree != null)
            {
                foreach (var child in tree.Root.GetChildren())
                {
                    if (child is NetworkManager nm)
                    {
                        _network = nm;
                        break;
                    }
                }
                if (_network == null)
                    _network = tree.Root.GetNodeOrNull<NetworkManager>("NetworkManager");
                if (_network != null)
                {
                    _network.NpcInteractNotify += OnNpcInteractNotify;
                    _network.NpcCombatResponse += OnNpcCombatResponse;
                    _network.CombatStateNotify += OnCombatStateNotify;
                    _network.CombatEndNotify += OnCombatEndNotify;
                }
            }

            // 提前加载配置，确保默认值在 SpawnNpcs 之前就绪
            LoadDefaultStyleConfig();

            GD.Print("[NpcManager] _Ready");
        }

        public override void _ExitTree()
        {
            if (_network != null)
            {
                _network.NpcInteractNotify -= OnNpcInteractNotify;
            }
            Instance = null;
        }

        /// <summary>
        /// 从配置文件加载样式配置，确保 SpawnNpcs 时使用已保存的值
        /// </summary>
        private void LoadDefaultStyleConfig()
        {
            var config = new ConfigFile();
            if (config.Load("user://debug_panel_config.cfg") != Error.Ok)
            {
                StyleConfigs[1] = EntityStyleConfig.CreateNpcDefault();
                return;
            }

            // 读取所有 [npc_*] sections
            bool hasAny = false;
            foreach (var sec in config.GetSections())
            {
                if (!sec.StartsWith("npc_")) continue;
                var idStr = sec.Substring("npc_".Length);
                if (!int.TryParse(idStr, out int id)) continue;

                var cfg = EntityStyleConfig.CreateNpcDefault();
                MonsterManager.LoadStyleConfigFromSection(config, sec, cfg);
                // NPC 专用字段
                cfg.InteractMenuOffsetAX = (float)(double)config.GetValue(sec, "interact_menu_offset_ax", cfg.InteractMenuOffsetAX);
                cfg.InteractMenuOffsetAY = (float)(double)config.GetValue(sec, "interact_menu_offset_ay", cfg.InteractMenuOffsetAY);
                cfg.InteractMenuOffsetBX = (float)(double)config.GetValue(sec, "interact_menu_offset_bx", cfg.InteractMenuOffsetBX);
                cfg.InteractMenuOffsetBY = (float)(double)config.GetValue(sec, "interact_menu_offset_by", cfg.InteractMenuOffsetBY);
                StyleConfigs[id] = cfg;
                hasAny = true;
            }

            // 迁移旧 [npc] section
            if (!hasAny && config.HasSection("npc"))
            {
                var cfg = EntityStyleConfig.CreateNpcDefault();
                MonsterManager.LoadStyleConfigFromSection(config, "npc", cfg);
                StyleConfigs[1] = cfg;
                hasAny = true;
            }

            if (!hasAny)
                StyleConfigs[1] = EntityStyleConfig.CreateNpcDefault();

            GD.Print($"[NpcManager] Loaded {StyleConfigs.Count} style configs");
        }

        public void SetGridSize(int size)
        {
            _gridSize = size;
            foreach (var n in _npcs)
                n.SetGridSize(size);
        }

        public void SpawnNpcs(List<Game.NpcInfo> npcData, int gridSize)
        {
            foreach (var n in _npcs)
                n.QueueFree();
            _npcs.Clear();
            _npcByPos.Clear();

            _gridSize = gridSize;
            if (npcData == null) return;

            foreach (var n in npcData)
            {
                // 确保每个 NpcType 都有对应的 StyleConfig
                int ntype = (int)n.NpcType;
                if (!StyleConfigs.ContainsKey(ntype))
                {
                    GD.PrintErr($"[NpcManager] No StyleConfig for NpcType={ntype} ({n.NpcName})! Available: [{string.Join(", ", StyleConfigs.Keys)}]");
                }

                var npc = new Npc();
                npc.Setup(n.NpcInstanceId, n.NpcName, n.NpcType, n.X, n.Y, gridSize);
                ApplyDefaultStyle(npc);
                npc.ProfileId = 3;
                AddChild(npc);
                // 创建后立即应用 Profile
                var pm = EntityProfileManager.Instance;
                if (pm != null) pm.ApplyProfile(npc, 3);
                _npcs.Add(npc);
                _npcByPos[new Vector2I(n.X, n.Y)] = npc;

                GD.Print($"[NpcManager] Spawned NPC {n.NpcInstanceId}({n.NpcName}) at ({n.X},{n.Y})");
            }
        }

        public void ApplyDefaultStyle(Npc npc)
        {
            if (npc == null) return;
            var cfg = GetStyleConfig(npc.UiConfigId);
            npc.ApplyStyle(cfg);
        }

        public void ApplyStyleToAll()
        {
            foreach (var n in _npcs)
                ApplyDefaultStyle(n);
        }

        public Npc GetNpcAt(Vector2I gridPos)
        {
            return _npcByPos.TryGetValue(gridPos, out var npc) ? npc : null;
        }

        /// <summary>
        /// 获取邻格的 NPC（上下左右四方向）
        /// </summary>
        public Npc GetAdjacentNpc(Vector2I gridPos)
        {
            var dirs = new Vector2I[] { new(0, -1), new(0, 1), new(-1, 0), new(1, 0) };
            foreach (var d in dirs)
            {
                var npc = GetNpcAt(gridPos + d);
                if (npc != null) return npc;
            }
            return null;
        }

        public bool IsBlockedByNpc(Vector2I gridPos)
        {
            return _npcByPos.ContainsKey(gridPos);
        }

        /// <summary>
        /// 收到 NPC 交互通知 — 在 NPC 旁弹出交互菜单
        /// </summary>
        private void OnNpcInteractNotify(Game.NpcInteractNotify notify)
        {
            GD.Print($"[NpcManager] NpcInteractNotify: name={notify.NpcName} type={notify.NpcType}");

            // 找到 NPC 实体，在其旁显示交互菜单
            var npc = _npcs.Find(n => n.InstanceId == notify.NpcInstanceId);
            if (npc == null) return;

            var player = GetTree()?.GetFirstNodeInGroup("player") as Player;
            var playerPos = player?.GridPos ?? npc.GridPos;
            ShowInteractMenu(npc, notify.NpcType, playerPos);
        }

        private VBoxContainer _interactMenu;
        private Npc _interactMenuNpc;
        private Vector2I _interactMenuPlayerPos;

        public void ShowInteractMenu(Npc npc, int npcType, Vector2I playerGridPos)
        {
            // 清除旧菜单
            CloseInteractMenu();

            _interactMenu = new VBoxContainer();
            _interactMenu.Name = "NpcInteractMenu";
            _interactMenuNpc = npc;
            _interactMenuPlayerPos = playerGridPos;

            // 菜单样式
            var panel = new PanelContainer();
            panel.Name = "NpcInteractPanel";

            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(0.1f, 0.1f, 0.2f, 0.9f);
            styleBox.BorderColor = new Color(0.3f, 0.5f, 0.9f);
            styleBox.BorderWidthTop = 2;
            styleBox.BorderWidthBottom = 2;
            styleBox.BorderWidthLeft = 2;
            styleBox.BorderWidthRight = 2;
            styleBox.CornerRadiusTopLeft = 4;
            styleBox.CornerRadiusTopRight = 4;
            styleBox.CornerRadiusBottomLeft = 4;
            styleBox.CornerRadiusBottomRight = 4;
            panel.AddThemeStyleboxOverride("panel", styleBox);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 4);

            // NPC 名称
            var nameLabel = new Label();
            nameLabel.Text = npc.NpcName;
            nameLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.7f, 1.0f));
            nameLabel.AddThemeFontSizeOverride("font_size", 14);
            vbox.AddChild(nameLabel);

            // 从配置表读取交互选项
            var npcTypeEnum = (NpcType)npcType;
            if (NpcInteractConfig.TryGetValue(npcTypeEnum, out var options) && options.Length > 0)
            {
                foreach (var opt in options)
                {
                    var btn = new Button();
                    btn.Text = opt.Label;
                    btn.AddThemeFontSizeOverride("font_size", 13);
                    btn.Pressed += () =>
                    {
                        if (!string.IsNullOrEmpty(opt.DialogText))
                            ShowNpcDialog(npc, opt.DialogText);
                        if (opt.ShowChangeJob)
                            ShowChangeJobPanel();
                        if (opt.TriggerCombat)
                            TriggerNpcCombat(npc);
                        CloseInteractMenu();
                    };
                    vbox.AddChild(btn);
                }
            }
            else
            {
                // 无配置的 NPC，只显示默认对话
                var dialogBtn = new Button();
                dialogBtn.Text = "对话";
                dialogBtn.AddThemeFontSizeOverride("font_size", 13);
                dialogBtn.Pressed += () =>
                {
                    ShowNpcDialog(npc, GetDefaultDialog(npcTypeEnum));
                    CloseInteractMenu();
                };
                vbox.AddChild(dialogBtn);
            }

            panel.AddChild(vbox);
            _interactMenu.AddChild(panel);

            // 添加到 NPC 子节点（世界空间），菜单随 NPC 和相机自然移动
            npc.AddChild(_interactMenu);
            // 玩家在 NPC 右侧 → 用B位置（左侧），否则用A位置（右侧）
            var cfg = GetStyleConfig(npc.NpcType);
            bool useB = playerGridPos.X > npc.GridX;
            float offsetX = useB ? cfg.InteractMenuOffsetBX : cfg.InteractMenuOffsetAX;
            float offsetY = useB ? cfg.InteractMenuOffsetBY : cfg.InteractMenuOffsetAY;
            _interactMenu.Position = new Vector2(offsetX, offsetY);
        }

        public void CloseInteractMenu()
        {
            if (_interactMenu != null && IsInstanceValid(_interactMenu))
            {
                _interactMenu.QueueFree();
                _interactMenu = null;
            }
            _interactMenuNpc = null;
        }

        /// <summary>
        /// 刷新已显示的交互面板位置（调试面板调节偏移时调用）
        /// </summary>
        public void RefreshInteractMenuPosition()
        {
            if (_interactMenu == null || !IsInstanceValid(_interactMenu) || _interactMenuNpc == null) return;
            var cfg = GetStyleConfig(_interactMenuNpc.NpcType);
            bool useB = _interactMenuPlayerPos.X > _interactMenuNpc.GridX;
            float offsetX = useB ? cfg.InteractMenuOffsetBX : cfg.InteractMenuOffsetAX;
            float offsetY = useB ? cfg.InteractMenuOffsetBY : cfg.InteractMenuOffsetAY;
            _interactMenu.Position = new Vector2(offsetX, offsetY);
        }

        private void ShowNpcDialog(Npc npc, string dialogText)
        {
            var dialog = new AcceptDialog();
            dialog.Title = npc.NpcName;
            dialog.DialogText = string.IsNullOrEmpty(dialogText) ? GetDefaultDialog((NpcType)npc.NpcType) : dialogText;
            dialog.OkButtonText = "关闭";

            var canvas = GetTree().Root.GetNode("Main").GetNodeOrNull<CanvasLayer>("UICanvas");
            if (canvas != null)
                canvas.AddChild(dialog);
        }

        private ChangeJobPanel _changeJobPanel;

        private void ShowChangeJobPanel()
        {
            if (_changeJobPanel != null && IsInstanceValid(_changeJobPanel))
            {
                _changeJobPanel.Visible = true;
                return;
            }

            _changeJobPanel = new ChangeJobPanel();
            var canvas = GetTree().Root.GetNode("Main").GetNodeOrNull<CanvasLayer>("UICanvas");
            if (canvas != null)
            {
                canvas.AddChild(_changeJobPanel);
                _changeJobPanel.Visible = true;
            }
        }

        /// <summary>
        /// 触发 NPC 战斗 — 显示血条，发送挑战请求到服务端
        /// </summary>
        private void TriggerNpcCombat(Npc npc)
        {
            // 客户端：NPC 进入战斗状态，显示血条
            npc.SetHealthBarVisible(true);
            npc.SetMpBarVisible(true);
            npc.SetHealthBarFillPercent(1.0f);
            npc.SetMpBarFillPercent(1.0f);

            // 发送 NpcCombatRequest 到服务端
            var req = new Game.NpcCombatRequest
            {
                NpcInstanceId = npc.InstanceId,
            };
            _network.SendPacket(Protocol.MessageId.GameNpcCombatReq, req);
            GD.Print($"[NpcManager] TriggerNpcCombat: npc={npc.NpcName} instanceId={npc.InstanceId}");
        }

        private void OnNpcCombatResponse(Game.NpcCombatResponse rsp)
        {
            if (rsp.Code != Common.ErrorCode.Success)
            {
                // 服务端拒绝挑战，恢复 NPC 非战斗状态
                var npc = _npcs.FirstOrDefault(n => n.InstanceId == rsp.NpcInstanceId);
                if (npc != null)
                {
                    npc.SetHealthBarVisible(false);
                    npc.SetMpBarVisible(false);
                }
                GD.PrintErr($"[NpcManager] NpcCombat rejected: code={rsp.Code} npc={rsp.NpcInstanceId}");
            }
            else
            {
                GD.Print($"[NpcManager] NpcCombat accepted: npc={rsp.NpcInstanceId}");
            }
        }

        private void OnCombatStateNotify(Game.CombatStateNotify notify)
        {
            // 同步战斗中的 NPC HP/MP
            foreach (var unit in notify.Units)
            {
                if (unit.IsPlayer) continue; // 只处理非玩家（怪物/NPC）
                var npc = _npcs.FirstOrDefault(n => n.InstanceId == (ulong)unit.EntityId);
                if (npc != null)
                {
                    npc.SetHealthBarVisible(true);
                    npc.SetMpBarVisible(true);
                    npc.SetHealthBarFillPercent(unit.MaxHp > 0 ? (float)unit.Hp / unit.MaxHp : 0f);
                    npc.SetMpBarFillPercent(unit.MaxMp > 0 ? (float)unit.Mp / unit.MaxMp : 0f);
                }
            }
        }

        private void OnCombatEndNotify(Game.CombatEndNotify notify)
        {
            // 战斗结束，NPC 恢复非战斗状态
            foreach (var npc in _npcs)
            {
                if (npc.HealthBarVisible)
                {
                    npc.SetHealthBarVisible(false);
                    npc.SetMpBarVisible(false);
                }
            }
        }

    }
}
