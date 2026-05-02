using Godot;
using System.Collections.Generic;
using System.Linq;
using Protocol;

namespace ClinetCSharp
{
    /// <summary>
    /// 鎬墿绠＄悊鍣?- 绠＄悊鍦板浘涓婃墍鏈夋€墿瀹炰綋
    /// 鎸傝浇鍒?Main 鍦烘櫙
    /// </summary>
    public partial class MonsterManager : Node
    {
        private List<Monster> _monsters = new();
        private HashSet<Vector2I> _monsterPositions = new();
        private int _gridSize = 111;
        private NetworkManager _network;
        private readonly Dictionary<ulong, Game.CombatStateNotify.Types.CombatUnit> _combatUnits = new();

        // 多配置样式系统：Key = 配置ID（MonsterId）
        public readonly Dictionary<int, EntityStyleConfig> StyleConfigs = new();

        public EntityStyleConfig GetStyleConfig(int id)
        {
            if (StyleConfigs.TryGetValue(id, out var cfg))
                return cfg;
            // 回退到任意已有配置
            if (StyleConfigs.Count > 0)
                return StyleConfigs.Values.First();
            // 绌哄瓧鍏告椂鑷姩鍒涘缓榛樿
            StyleConfigs[1] = EntityStyleConfig.CreateMonsterDefault();
            return StyleConfigs[1];
        }

        public EntityStyleConfig GetOrCreateStyleConfig(int id)
        {
            if (StyleConfigs.TryGetValue(id, out var cfg))
                return cfg;
            cfg = GetStyleConfig(0).Clone();
            StyleConfigs[id] = cfg;
            return cfg;
        }

        public override void _Ready()
        {
            AddToGroup("monster_manager");

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
                    _network.CombatStateNotify += OnCombatStateNotify;
                    _network.MonsterMoveCancelNotify += OnMonsterMoveCancel;
                }
            }

            // 鎻愬墠鍔犺浇閰嶇疆锛岀‘淇?DefaultVisualSizeScale 绛夐粯璁ゅ€煎湪 SpawnMonsters 涔嬪墠灏辩华
            LoadDefaultStyleConfig();

            GD.Print("[MonsterManager] _Ready");
        }

        public override void _ExitTree()
        {
            if (_network != null)
            {
                _network.CombatStateNotify -= OnCombatStateNotify;
                _network.MonsterMoveCancelNotify -= OnMonsterMoveCancel;
            }
        }

        /// <summary>
        /// 浠庨厤缃枃浠跺姞杞芥牱寮忛厤缃紝纭繚 SpawnMonsters 鏃朵娇鐢ㄥ凡淇濆瓨鐨勫€?        /// </summary>
        private void LoadDefaultStyleConfig()
        {
            var config = new ConfigFile();
            if (config.Load("user://debug_panel_config.cfg") != Error.Ok)
            {
                // 没有配置文件，创建默认配置
                StyleConfigs[1] = EntityStyleConfig.CreateMonsterDefault();
                return;
            }

            // 璇诲彇鎵€鏈?[monster_*] sections
            bool hasAny = false;
            foreach (var sec in config.GetSections())
            {
                if (!sec.StartsWith("monster_")) continue;
                var idStr = sec.Substring("monster_".Length);
                if (!int.TryParse(idStr, out int id)) continue;

                var cfg = EntityStyleConfig.CreateMonsterDefault();
                LoadStyleConfigFromSection(config, sec, cfg);
                StyleConfigs[id] = cfg;
                hasAny = true;
            }

            // 杩佺Щ鏃?[monster] section
            if (!hasAny && config.HasSection("monster"))
            {
                var cfg = EntityStyleConfig.CreateMonsterDefault();
                LoadStyleConfigFromSection(config, "monster", cfg);
                StyleConfigs[1] = cfg;
                hasAny = true;
            }

            if (!hasAny)
                StyleConfigs[1] = EntityStyleConfig.CreateMonsterDefault();

            GD.Print($"[MonsterManager] Loaded {StyleConfigs.Count} style configs");
        }

        public static void LoadStyleConfigFromSection(ConfigFile config, string section, EntityStyleConfig cfg)
        {
            cfg.VisualSizeScale = (float)(double)config.GetValue(section, "visual_size_scale", cfg.VisualSizeScale);
            cfg.BorderWidthScale = (float)(double)config.GetValue(section, "border_width_scale", cfg.BorderWidthScale);
            cfg.CornerRadius = (float)(double)config.GetValue(section, "corner_radius", cfg.CornerRadius);
            cfg.BgOpacity = (float)(double)config.GetValue(section, "bg_opacity", cfg.BgOpacity);
            cfg.FontSize = (int)(double)config.GetValue(section, "font_size", cfg.FontSize);

            float bcR = (float)(double)config.GetValue(section, "border_color_r", cfg.BorderColor.R);
            float bcG = (float)(double)config.GetValue(section, "border_color_g", cfg.BorderColor.G);
            float bcB = (float)(double)config.GetValue(section, "border_color_b", cfg.BorderColor.B);
            cfg.BorderColor = new Color(bcR, bcG, bcB);

            float bgcR = (float)(double)config.GetValue(section, "bg_color_r", cfg.BgColor.R);
            float bgcG = (float)(double)config.GetValue(section, "bg_color_g", cfg.BgColor.G);
            float bgcB = (float)(double)config.GetValue(section, "bg_color_b", cfg.BgColor.B);
            cfg.BgColor = new Color(bgcR, bgcG, bgcB);

            float tcR = (float)(double)config.GetValue(section, "text_color_r", cfg.TextColor.R);
            float tcG = (float)(double)config.GetValue(section, "text_color_g", cfg.TextColor.G);
            float tcB = (float)(double)config.GetValue(section, "text_color_b", cfg.TextColor.B);
            cfg.TextColor = new Color(tcR, tcG, tcB);

            for (int i = 0; i < 4; i++)
            {
                cfg.LabelTexts[i] = (string)config.GetValue(section, $"label_text_{i}", cfg.LabelTexts[i] ?? "");
                cfg.LabelFontSizes[i] = (int)(double)config.GetValue(section, $"label_font_size_{i}", 0);
                cfg.LabelXOffsets[i] = (float)(double)config.GetValue(section, $"label_x_offset_{i}", 0);
                cfg.LabelCenterX[i] = (bool)config.GetValue(section, $"label_center_x_{i}", true);
                cfg.LabelYOffsets[i] = (float)(double)config.GetValue(section, $"label_y_offset_{i}", 0);
            }

            cfg.HpBarLengthScale = (float)(double)config.GetValue(section, "hp_bar_length_scale", cfg.HpBarLengthScale);
            cfg.HpBarHeightScale = (float)(double)config.GetValue(section, "hp_bar_height_scale", cfg.HpBarHeightScale);
            cfg.HpBarFillPercent = (float)(double)config.GetValue(section, "hp_bar_fill_percent", cfg.HpBarFillPercent);
            cfg.HpBarVisible = (bool)config.GetValue(section, "hp_bar_visible", cfg.HpBarVisible);
            cfg.HpBarCenterX = (bool)config.GetValue(section, "hp_bar_center_x", RoleControlCenterXResolver.ResolveInitialCenterX(null, cfg.HpBarOffsetX));
            cfg.HpBarOffsetX = (float)(double)config.GetValue(section, "hp_bar_offset_x", cfg.HpBarOffsetX);
            cfg.HpBarOffsetY = (float)(double)config.GetValue(section, "hp_bar_offset_y", cfg.HpBarOffsetY);
            float hpR = (float)(double)config.GetValue(section, "hp_bar_color_r", cfg.HpBarColor.R);
            float hpG = (float)(double)config.GetValue(section, "hp_bar_color_g", cfg.HpBarColor.G);
            float hpB = (float)(double)config.GetValue(section, "hp_bar_color_b", cfg.HpBarColor.B);
            cfg.HpBarColor = new Color(hpR, hpG, hpB);

            cfg.MpBarLengthScale = (float)(double)config.GetValue(section, "mp_bar_length_scale", cfg.MpBarLengthScale);
            cfg.MpBarHeightScale = (float)(double)config.GetValue(section, "mp_bar_height_scale", cfg.MpBarHeightScale);
            cfg.MpBarFillPercent = (float)(double)config.GetValue(section, "mp_bar_fill_percent", cfg.MpBarFillPercent);
            cfg.MpBarVisible = (bool)config.GetValue(section, "mp_bar_visible", cfg.MpBarVisible);
            cfg.MpBarCenterX = (bool)config.GetValue(section, "mp_bar_center_x", RoleControlCenterXResolver.ResolveInitialCenterX(null, cfg.MpBarOffsetX));
            cfg.MpBarOffsetX = (float)(double)config.GetValue(section, "mp_bar_offset_x", cfg.MpBarOffsetX);
            cfg.MpBarOffsetY = (float)(double)config.GetValue(section, "mp_bar_offset_y", cfg.MpBarOffsetY);
            float mpR = (float)(double)config.GetValue(section, "mp_bar_color_r", cfg.MpBarColor.R);
            float mpG = (float)(double)config.GetValue(section, "mp_bar_color_g", cfg.MpBarColor.G);
            float mpB = (float)(double)config.GetValue(section, "mp_bar_color_b", cfg.MpBarColor.B);
            cfg.MpBarColor = new Color(mpR, mpG, mpB);

            // InteractMenuOffset (NPC 专用)
            cfg.InteractMenuOffsetAX = (float)(double)config.GetValue(section, "interact_menu_offset_ax", cfg.InteractMenuOffsetAX);
            cfg.InteractMenuOffsetAY = (float)(double)config.GetValue(section, "interact_menu_offset_ay", cfg.InteractMenuOffsetAY);
            cfg.InteractMenuOffsetBX = (float)(double)config.GetValue(section, "interact_menu_offset_bx", cfg.InteractMenuOffsetBX);
            cfg.InteractMenuOffsetBY = (float)(double)config.GetValue(section, "interact_menu_offset_by", cfg.InteractMenuOffsetBY);
        }

        public void SetGridSize(int size)
        {
            _gridSize = size;
            foreach (var m in _monsters)
                m.SetGridSize(size);
        }

        public void SpawnMonsters(List<Game.MonsterInfo> monsterData, int gridSize)
        {
            foreach (var m in _monsters)
                m.QueueFree();
            _monsters.Clear();
            _monsterPositions.Clear();

            _gridSize = gridSize;
            if (monsterData == null) return;

            foreach (var m in monsterData)
            {
                var monster = new Monster();
                monster.Setup(
                    m.InstanceId,
                    m.MonsterId,
                    m.X,
                    m.Y,
                    m.Name,
                    m.Level,
                    gridSize,
                    m.Attrs
                );
                ApplyDefaultStyle(monster);
                AddChild(monster);
                _monsters.Add(monster);
                _monsterPositions.Add(new Vector2I(m.X, m.Y));

                GD.Print($"[MonsterManager] Spawned monster {monster.InstanceId}({monster.MonsterName}) at ({monster.GridX},{monster.GridY})");
            }
        }

        public void ApplyDefaultStyle(Monster monster)
        {
            if (monster == null) return;
            var cfg = GetStyleConfig((int)monster.MonsterId);
            monster.ApplyStyle(cfg);
        }

        public void ApplyStyleToAll()
        {
            foreach (var m in _monsters)
                ApplyDefaultStyle(m);
        }

        public void SetActionBarTextYOffsetAll(float offset)
        {
            foreach (var m in _monsters)
                m.SetActionBarTextYOffset(offset);
        }

        public void SetActionBarProgressHeightAll(float height)
        {
            foreach (var m in _monsters)
                m.SetActionBarProgressHeight(height);
        }

        public System.Collections.Generic.IReadOnlyList<Monster> GetMonsters() => _monsters;

        public void SetHealthBarVisibleAll(bool visible) { foreach (var m in _monsters) m.SetHealthBarVisible(visible); SyncStyleConfigFromEntity(); }
        public void SetHealthBarLengthScaleAll(float scale) { foreach (var m in _monsters) m.SetHealthBarLengthScale(scale); SyncStyleConfigFromEntity(); }
        public void SetHealthBarHeightScaleAll(float scale) { foreach (var m in _monsters) m.SetHealthBarHeightScale(scale); SyncStyleConfigFromEntity(); }
        public void SetHealthBarFillPercentAll(float percent) { foreach (var m in _monsters) m.SetHealthBarFillPercent(percent); SyncStyleConfigFromEntity(); }
        public void SetHealthBarColorAll(Color color) { foreach (var m in _monsters) m.SetHealthBarColor(color); SyncStyleConfigFromEntity(); }
        public void SetHealthBarOffsetAll(Vector2 offset) { foreach (var m in _monsters) m.SetHealthBarOffset(offset); SyncStyleConfigFromEntity(); }

        public void SetMpBarVisibleAll(bool visible) { foreach (var m in _monsters) m.SetMpBarVisible(visible); SyncStyleConfigFromEntity(); }
        public void SetMpBarLengthScaleAll(float scale) { foreach (var m in _monsters) m.SetMpBarLengthScale(scale); SyncStyleConfigFromEntity(); }
        public void SetMpBarHeightScaleAll(float scale) { foreach (var m in _monsters) m.SetMpBarHeightScale(scale); SyncStyleConfigFromEntity(); }
        public void SetMpBarFillPercentAll(float percent) { foreach (var m in _monsters) m.SetMpBarFillPercent(percent); SyncStyleConfigFromEntity(); }
        public void SetMpBarColorAll(Color color) { foreach (var m in _monsters) m.SetMpBarColor(color); SyncStyleConfigFromEntity(); }
        public void SetMpBarOffsetAll(Vector2 offset) { foreach (var m in _monsters) m.SetMpBarOffset(offset); SyncStyleConfigFromEntity(); }

        /// <summary>
        /// 灏嗗綋鍓嶆€墿瀹炰綋鐨勮瑙夊睘鎬у悓姝ュ洖鎵€鏈?StyleConfig
        /// </summary>
        private void SyncStyleConfigFromEntity()
        {
            if (_monsters.Count == 0) return;
            var first = _monsters[0];
            foreach (var kv in StyleConfigs)
            {
                kv.Value.SyncFromEntity(first);
            }
        }

        public Monster GetMonsterAt(Vector2I gridPos)
        {
            if (!_monsterPositions.Contains(gridPos))
                return null;
            foreach (var m in _monsters)
            {
                if (m.GridX == gridPos.X && m.GridY == gridPos.Y)
                    return m;
            }
            return null;
        }

        public void OnMonsterMove(uint instanceId, Vector2I from, Vector2I to, string state, int durationMs)
        {
            var m = _monsters.Find(x => x.InstanceId == instanceId);
            if (m == null) return;
            _monsterPositions.Remove(from);
            _monsterPositions.Add(to);
            m.CurrentState = state;
            float durationSec = durationMs > 0 ? durationMs / 1000.0f : 0.15f;
            m.MoveTo(to, durationSec);
        }

        public bool IsBlockedByMonster(Vector2I gridPos)
        {
            return _monsterPositions.Contains(gridPos);
        }

        private void OnCombatStateNotify(Game.CombatStateNotify notify)
        {
            if (notify.Units.Count == 0)
            {
                foreach (var m in _monsters)
                {
                    m.CastingSkill = "";
                    m.CastProgress = 0;
                    m.HealthBarFillPercent = 1.0f;
                    m.MpBarFillPercent = 1.0f;
                    m.QueueRedraw();
                }
                return;
            }

            var combatMonsters = _combatUnits;
            combatMonsters.Clear();
            foreach (var unit in notify.Units)
            {
                if (!unit.IsPlayer)
                    combatMonsters[unit.EntityId] = unit;
            }

            foreach (var m in _monsters)
            {
                if (combatMonsters.TryGetValue(m.InstanceId, out var unit))
                {
                    m.CastingSkill = unit.CastingSkill;
                    m.CastProgress = unit.CastProgress;
                    if (unit.MaxHp > 0)
                        m.HealthBarFillPercent = (float)unit.Hp / unit.MaxHp;
                    if (unit.MaxMp > 0)
                        m.MpBarFillPercent = (float)unit.Mp / unit.MaxMp;
                }
                else
                {
                    m.CastingSkill = "";
                    m.CastProgress = 0;
                    m.HealthBarFillPercent = 1.0f;
                    m.MpBarFillPercent = 1.0f;
                }
                m.QueueRedraw();
            }
        }

        public void OnMonsterMoveCancel(Game.MonsterMoveCancelNotify notify)
        {
            var m = _monsters.Find(x => x.InstanceId == notify.InstanceId);
            if (m == null) return;

            var rollbackPos = new Vector2I(notify.RollbackX, notify.RollbackY);
            _monsterPositions.Remove(new Vector2I(m.GridX, m.GridY));
            _monsterPositions.Add(rollbackPos);

            // Keep the rollback smooth while the monster is already moving.
            if (m.IsMoving)
                m.PlayBounceBack(rollbackPos);
            else
                m.RollbackTo(rollbackPos);

            GD.Print($"[MonsterManager] Monster {m.InstanceId}({m.MonsterName}) move cancelled, rolled back to ({rollbackPos.X},{rollbackPos.Y})");
        }
    }
}
