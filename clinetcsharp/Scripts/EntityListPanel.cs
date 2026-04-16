using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 单位列表面板 - 显示当前地图上的所有单位（玩家、宝箱、怪物）
    /// 按 M 键开关
    /// </summary>
    public partial class EntityListPanel : CanvasLayer
    {
        private Panel _panel;
        private VBoxContainer _contentBox;
        private Button _toggleBtn;
        private bool _isVisible = false;

        public override void _Ready()
        {
            BuildUI();

            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm != null)
            {
                nm.MapInfoReceived += OnMapInfoReceived;
            }
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.F3)
            {
                Toggle();
                GetViewport().SetInputAsHandled();
            }
        }

        private void BuildUI()
        {
            // 开关按钮
            _toggleBtn = new Button
            {
                Text = "单位列表 (F3)",
                Position = new Vector2(10, 10),
                CustomMinimumSize = new Vector2(100, 36),
            };
            _toggleBtn.Pressed += Toggle;
            AddChild(_toggleBtn);

            // 主面板
            _panel = new Panel();
            _panel.Position = new Vector2(10, 52);
            _panel.Size = new Vector2(280, 400);
            _panel.Visible = false;

            var scroll = new ScrollContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            };
            scroll.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            scroll.OffsetLeft = 8;
            scroll.OffsetTop = 8;
            scroll.OffsetRight = -8;
            scroll.OffsetBottom = -8;

            _contentBox = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            };

            scroll.AddChild(_contentBox);
            _panel.AddChild(scroll);
            AddChild(_panel);
        }

        private void Toggle()
        {
            _isVisible = !_isVisible;
            _panel.Visible = _isVisible;
            if (_isVisible)
                RefreshList();
        }

        private void OnMapInfoReceived()
        {
            if (_isVisible)
                RefreshList();
        }

        private void RefreshList()
        {
            foreach (var child in _contentBox.GetChildren())
                child.QueueFree();

            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            var player = GetTree()?.GetFirstNodeInGroup("player") as Player;

            // 标题
            var title = new Label
            {
                Text = "═══ 地图单位列表 ═══",
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            title.AddThemeFontSizeOverride("font_size", 14);
            _contentBox.AddChild(title);

            // 地图名
            var mapLabel = new Label
            {
                Text = $"地图: {nm?.CurrentMapName ?? "-"}",
            };
            mapLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.9f, 1));
            _contentBox.AddChild(mapLabel);

            _contentBox.AddChild(new HSeparator());

            // 玩家
            var playerHeader = MakeHeader("👤 玩家");
            _contentBox.AddChild(playerHeader);

            if (player != null && nm?.CachedRoleInfo != null)
            {
                var playerInfo = new Label
                {
                    Text = $"  {nm.CachedRoleInfo.RoleName} (Lv.{nm.CachedRoleInfo.Level}) ({player.GridPos.X},{player.GridPos.Y})",
                };
                _contentBox.AddChild(playerInfo);
            }
            else
            {
                _contentBox.AddChild(new Label { Text = "  加载中..." });
            }

            _contentBox.AddChild(new HSeparator());

            // 宝箱
            var chestHeader = MakeHeader("📦 宝箱");
            _contentBox.AddChild(chestHeader);

            if (nm != null && nm.Chests.Count > 0)
            {
                foreach (var entry in nm.Chests)
                {
                    var dict = entry.AsGodotDictionary();
                    int cid = dict["chest_id"].AsInt32();
                    int cx = dict["x"].AsInt32();
                    int cy = dict["y"].AsInt32();
                    bool opened = dict["opened"].AsBool();
                    var chestLabel = new Label
                    {
                        Text = $"  #{cid} ({cx},{cy}) {(opened ? "[已开]" : "[未开]")}",
                    };
                    if (opened)
                        chestLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
                    else
                        chestLabel.AddThemeColorOverride("font_color", new Color(1, 0.85f, 0.4f));
                    _contentBox.AddChild(chestLabel);
                }
            }
            else
            {
                _contentBox.AddChild(new Label { Text = "  无" });
            }

            _contentBox.AddChild(new HSeparator());

            // 怪物
            var monsterHeader = MakeHeader("👹 怪物");
            _contentBox.AddChild(monsterHeader);

            if (nm != null && nm.Monsters.Count > 0)
            {
                foreach (var entry in nm.Monsters)
                {
                    var dict = entry.AsGodotDictionary();
                    string mname = dict["name"].AsString();
                    int mx = dict["x"].AsInt32();
                    int my = dict["y"].AsInt32();
                    int level = dict["level"].AsInt32();
                    var monsterLabel = new Label
                    {
                        Text = $"  {mname} (Lv.{level}) ({mx},{my})",
                    };
                    monsterLabel.AddThemeColorOverride("font_color", new Color(1, 0.5f, 0.5f));
                    _contentBox.AddChild(monsterLabel);
                }
            }
            else
            {
                _contentBox.AddChild(new Label { Text = "  无" });
            }
        }

        private static Label MakeHeader(string text)
        {
            var label = new Label
            {
                Text = text,
            };
            label.AddThemeFontSizeOverride("font_size", 12);
            label.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 1));
            return label;
        }
    }
}
