using Godot;
using Protocol;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// 角色属性面板 — 查看/设置角色战斗属性
    /// 继承 DraggablePanel（拖拽/resize/最小化/关闭）
    /// 设置通过 GM 命令 setattr 发送，服务器广播 FullRoleInfo 回来刷新
    /// </summary>
    public partial class CharacterPanel : DraggablePanel
    {
        private NetworkManager _network;
        private Node2D _player;
        private VBoxContainer _content;
        private readonly Dictionary<uint, SpinBox> _spinBoxes = new();

        // 属性 key 对应 Luban common.EAttr 枚举值，需与 tables/defines/common.xml 保持同步
        // EAttr: HP=1, MAX_HP=2, MP=3, MAX_MP=4, AGILITY=5, PATK=6, MATK=7, PDEF=8, MDEF=9, MOVE_SPEED=10
        private static readonly (uint key, string label, string gmName)[] AttrDefs =
        {
            (1, "HP", "hp"),
            (2, "MaxHP", "max_hp"),
            (3, "MP", "mp"),
            (4, "MaxMP", "max_mp"),
            (5, "敏捷", "agility"),
            (6, "物攻", "patk"),
            (7, "魔攻", "matk"),
            (8, "物防", "pdef"),
            (9, "魔防", "mdef"),
            (10, "移速(ms)", "move_speed"),
        };

        protected override void OnPanelReady()
        {
            _content = GetNodeOrNull<VBoxContainer>("VBoxContainer/Content");
            if (_content == null) return;

            // 面板样式
            AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0, 0, 0, 0.85f),
                BorderColor = new Color(0.2f, 0.2f, 0.2f),
                BorderWidthBottom = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                BorderWidthTop = 1,
            });

            var titleBar = GetNodeOrNull<PanelContainer>("VBoxContainer/TitleBar");
            if (titleBar != null)
                titleBar.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.1f, 0.1f, 0.1f, 0.9f) });

            // 创建属性行
            foreach (var (key, label, gmName) in AttrDefs)
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 8);

                var nameLabel = new Label
                {
                    Text = label,
                    CustomMinimumSize = new Vector2(60, 0),
                };
                row.AddChild(nameLabel);

                var spinBox = new SpinBox
                {
                    MinValue = 0,
                    MaxValue = 99999,
                    Step = 1,
                    CustomMinimumSize = new Vector2(100, 0),
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                };
                row.AddChild(spinBox);
                _spinBoxes[key] = spinBox;

                var applyBtn = new Button
                {
                    Text = "应用",
                    CustomMinimumSize = new Vector2(50, 0),
                };
                applyBtn.Pressed += () => OnApplyAttr(gmName, (int)spinBox.Value);
                row.AddChild(applyBtn);

                _content.AddChild(row);
            }

            // 全部应用按钮
            var bottomRow = new HBoxContainer();
            bottomRow.AddThemeConstantOverride("separation", 8);

            var applyAllBtn = new Button
            {
                Text = "全部应用",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            applyAllBtn.Pressed += OnApplyAll;
            bottomRow.AddChild(applyAllBtn);

            var refreshBtn = new Button { Text = "刷新" };
            refreshBtn.Pressed += RefreshFromPlayer;
            bottomRow.AddChild(refreshBtn);

            _content.AddChild(bottomRow);

            // 获取 NetworkManager 和 Player
            _network = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            _player = GetTree()?.GetFirstNodeInGroup("player") as Node2D;

            // 订阅属性更新信号
            if (_network != null)
                _network.RoleAttrUpdated += OnRoleAttrUpdated;
        }

        public override void _ExitTree()
        {
            if (_network != null)
                _network.RoleAttrUpdated -= OnRoleAttrUpdated;
            base._ExitTree();
        }

        protected internal override void NotifyFocusGained()
        {
            RefreshFromPlayer();
        }

        private void RefreshFromPlayer()
        {
            var player = GetTree()?.GetFirstNodeInGroup("player") as Player;
            if (player == null) return;
            if (player.CombatAttrs.Count == 0) return;

            foreach (var (key, _, _) in AttrDefs)
            {
                if (_spinBoxes.TryGetValue(key, out var spinBox))
                {
                    if (player.CombatAttrs.TryGetValue(key, out var value))
                        spinBox.Value = value;
                }
            }
        }

        private void OnApplyAttr(string gmName, int value)
        {
            if (_network == null) return;
            var req = new Game.GmCommandRequest
            {
                Command = $"setattr,{gmName},{value}",
            };
            _network.SendPacket(MessageId.GameGmReq, req);
            GD.Print($"[CharacterPanel] GM setattr {gmName}={value}");
        }

        private void OnApplyAll()
        {
            if (_network == null) return;
            foreach (var (key, label, gmName) in AttrDefs)
            {
                if (_spinBoxes.TryGetValue(key, out var spinBox))
                {
                    var req = new Game.GmCommandRequest
                    {
                        Command = $"setattr,{gmName},{(int)spinBox.Value}",
                    };
                    _network.SendPacket(MessageId.GameGmReq, req);
                }
            }
            GD.Print("[CharacterPanel] GM setattr all applied");
        }

        private void OnRoleAttrUpdated(Game.FullRoleInfo roleInfo)
        {
            RefreshFromPlayer();
        }
    }
}
