using Godot;
using Godot.Collections;

namespace ClinetCSharp
{
    /// <summary>
    /// 单位列表面板 — 继承 DraggablePanel，按 F3 开关。
    /// 显示当前地图上的所有单位（玩家、宝箱、怪物）。
    /// </summary>
    public partial class EntityListPanel : DraggablePanel
    {
        private VBoxContainer _contentBox;

        protected override void OnPanelReady()
        {
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

            _contentBox = GetNodeOrNull<VBoxContainer>("VBoxContainer/Content/ContentBox");

            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm != null)
                nm.MapInfoReceived += OnMapInfoReceived;

            SetToggleKey(Key.F3);
        }

        public override void _ExitTree()
        {
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm != null)
                nm.MapInfoReceived -= OnMapInfoReceived;
            base._ExitTree();
        }

        protected internal override void NotifyFocusGained()
        {
            RefreshList();
        }

        private void OnMapInfoReceived()
        {
            if (Visible)
                RefreshList();
        }

        private void RefreshList()
        {
            if (_contentBox == null) return;

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
            _contentBox.AddChild(MakeHeader("玩家"));
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
            _contentBox.AddChild(MakeHeader("宝箱"));
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
            _contentBox.AddChild(MakeHeader("怪物"));
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
            var label = new Label { Text = text };
            label.AddThemeFontSizeOverride("font_size", 12);
            label.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 1));
            return label;
        }
    }
}
