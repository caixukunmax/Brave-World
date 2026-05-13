using Godot;
using Protocol;

namespace ClinetCSharp
{
    /// <summary>
    /// 单位列表面板 — 继承 DraggablePanel，按 F3 开关。
    /// 显示当前地图上的所有单位（玩家、宝箱、怪物）。
    /// </summary>
    public partial class EntityListPanel : DraggablePanel
    {
        private VBoxContainer _contentBox;
        private NetworkManager _network;

        protected override void OnPanelInitialized()
        {
            _contentBox = GetNodeOrNull<VBoxContainer>("VBoxContainer/Content/ContentBox");

            _network = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (_network != null)
                _network.MapInfoReceived += OnMapInfoReceived;

            SetToggleKey(Key.F3);
        }

        public override void _ExitTree()
        {
            if (_network != null)
                _network.MapInfoReceived -= OnMapInfoReceived;
            base._ExitTree();
        }

        protected internal override void NotifyFocusGained()
        {
            RefreshList();
        }

        private void OnMapInfoReceived(Game.MapInfoSyncNotify notify)
        {
            if (Visible)
                RefreshList();
        }

        private void RefreshList()
        {
            if (_contentBox == null) return;

            foreach (var child in _contentBox.GetChildren())
                child.QueueFree();

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
                Text = $"地图: {_network?.CurrentMapName ?? "-"}",
            };
            mapLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.9f, 1));
            _contentBox.AddChild(mapLabel);

            _contentBox.AddChild(new HSeparator());

            // 玩家
            _contentBox.AddChild(MakeHeader("玩家"));
            if (player != null && _network?.CachedRoleInfo != null)
            {
                var playerInfo = new Label
                {
                    Text = $"  {_network.CachedRoleInfo.RoleName} (Lv.{_network.CachedRoleInfo.Level}) ({player.GridPos.X},{player.GridPos.Y})",
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
            if (_network != null && _network.Chests.Count > 0)
            {
                foreach (var c in _network.Chests)
                {
                    var chestLabel = new Label
                    {
                        Text = $"  #{c.ChestId} ({c.X},{c.Y}) {(c.Opened ? "[已开]" : "[未开]")}",
                    };
                    if (c.Opened)
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
            var monsterMgr = GetTree()?.GetFirstNodeInGroup("monster_manager") as MonsterManager;
            if (monsterMgr != null && monsterMgr.GetMonsters().Count > 0)
            {
                foreach (var m in monsterMgr.GetMonsters())
                {
                    string posText = m.PendingGridPos.HasValue
                        ? $"({m.GridX},{m.GridY}) -> ({m.PendingGridPos.Value.X},{m.PendingGridPos.Value.Y})"
                        : $"({m.GridX},{m.GridY})";
                    var monsterLabel = new Label
                    {
                        Text = $"  {m.MonsterName} (Lv.{m.Level}) {posText}",
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
