using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// 统一管控游戏常驻 HUD、实体、面板的显隐。
    /// 进入大地图、地图编辑器等全屏/特殊模式时调用 HideGameUi()，
    /// 退出时调用 ShowGameUi() 按快照恢复。
    /// </summary>
    [GlobalClass]
    public partial class GameUiVisibilityManager : Node
    {
        public static GameUiVisibilityManager Instance { get; private set; }

        // 常驻 HUD 分组。新增常驻 HUD 时，只需在这里追加分组名。
        private static readonly string[] HudGroups = new[]
        {
            "function_bar",
            "skill_bar",
            "buff_bar",
            "minimap_hud",
        };

        // 快照
        private readonly Dictionary<Node, bool> _hudSnapshot = new();
        private readonly List<DraggablePanel> _visiblePanels = new();
        private bool _wasPlayerVisible;
        private bool _wasPatrolOverlayEnabled;

        public override void _Ready()
        {
            Instance = this;
        }

        public override void _ExitTree()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// 隐藏游戏 UI 与实体，并保存快照用于后续恢复。
        /// </summary>
        public void HideGameUi()
        {
            if (_hudSnapshot.Count > 0 || _visiblePanels.Count > 0)
            {
                GD.PushWarning("[GameUiVisibilityManager] HideGameUi called while already hidden; clearing old snapshot.");
                _hudSnapshot.Clear();
                _visiblePanels.Clear();
            }

            // 1. 隐藏常驻 HUD
            foreach (var group in HudGroups)
            {
                var node = GetTree()?.GetFirstNodeInGroup(group);
                if (node is CanvasItem ci)
                    HideCanvasItem(ci);
                else if (node is CanvasLayer cl)
                    HideCanvasLayer(cl);
            }

            // 2. 隐藏巡逻覆盖层
            var patrolOverlay = GetTree()?.GetFirstNodeInGroup("monster_patrol_overlay") as MonsterPatrolOverlay;
            if (patrolOverlay != null && IsInstanceValid(patrolOverlay))
            {
                _wasPatrolOverlayEnabled = patrolOverlay.OverlayEnabled;
                patrolOverlay.SetOverlayEnabled(false);
            }

            // 3. 隐藏玩家
            var player = GetTree()?.GetFirstNodeInGroup("player") as Node2D;
            if (player != null && IsInstanceValid(player))
            {
                _wasPlayerVisible = player.Visible;
                player.Visible = false;
            }

            // 4. 隐藏各类实体管理器下的对象
            var monsterMgr = GetTree()?.GetFirstNodeInGroup("monster_manager") as MonsterManager;
            monsterMgr?.SetAllMonstersVisible(false);

            var npcMgr = GetTree()?.GetFirstNodeInGroup("npc_manager") as NpcManager;
            npcMgr?.SetAllNpcsVisible(false);
            npcMgr?.CloseInteractMenu();

            var chestMgr = GetTree()?.GetFirstNodeInGroup("chest_manager") as ChestManager;
            chestMgr?.SetAllChestsVisible(false);

            var dropMgr = GetTree()?.GetFirstNodeInGroup("drop_manager") as DropManager;
            dropMgr?.SetAllDropsVisible(false);

            var decMgr = GetTree()?.GetFirstNodeInGroup("map_decoration_manager") as MapDecorationManager;
            decMgr?.SetAllDecorationsVisible(false);

            // 5. 隐藏所有 DraggablePanel
            var panelMgr = PanelManager.Instance;
            if (panelMgr != null)
            {
                var uiCanvas = panelMgr.GetParent() as CanvasLayer;
                if (uiCanvas != null)
                {
                    foreach (var child in uiCanvas.GetChildren())
                    {
                        if (child is DraggablePanel dp && dp.Visible)
                        {
                            _visiblePanels.Add(dp);
                            dp.Visible = false;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 按快照恢复游戏 UI 与实体。
        /// </summary>
        public void ShowGameUi()
        {
            // 1. 恢复常驻 HUD
            foreach (var kvp in _hudSnapshot)
            {
                if (kvp.Key != null && IsInstanceValid(kvp.Key))
                {
                    if (kvp.Key is CanvasItem ci)
                        ci.Visible = kvp.Value;
                    else if (kvp.Key is CanvasLayer cl)
                        cl.Visible = kvp.Value;
                }
            }
            _hudSnapshot.Clear();

            // 2. 恢复巡逻覆盖层
            var patrolOverlay = GetTree()?.GetFirstNodeInGroup("monster_patrol_overlay") as MonsterPatrolOverlay;
            if (patrolOverlay != null && IsInstanceValid(patrolOverlay))
                patrolOverlay.SetOverlayEnabled(_wasPatrolOverlayEnabled);

            // 3. 恢复玩家
            var player = GetTree()?.GetFirstNodeInGroup("player") as Node2D;
            if (player != null && IsInstanceValid(player))
                player.Visible = _wasPlayerVisible;

            // 4. 恢复各类实体
            var monsterMgr = GetTree()?.GetFirstNodeInGroup("monster_manager") as MonsterManager;
            monsterMgr?.SetAllMonstersVisible(true);

            var npcMgr = GetTree()?.GetFirstNodeInGroup("npc_manager") as NpcManager;
            npcMgr?.SetAllNpcsVisible(true);

            var chestMgr = GetTree()?.GetFirstNodeInGroup("chest_manager") as ChestManager;
            chestMgr?.SetAllChestsVisible(true);

            var dropMgr = GetTree()?.GetFirstNodeInGroup("drop_manager") as DropManager;
            dropMgr?.SetAllDropsVisible(true);

            var decMgr = GetTree()?.GetFirstNodeInGroup("map_decoration_manager") as MapDecorationManager;
            decMgr?.SetAllDecorationsVisible(true);

            // 5. 恢复面板
            foreach (var panel in _visiblePanels)
            {
                if (panel != null && IsInstanceValid(panel))
                    panel.Visible = true;
            }
            _visiblePanels.Clear();
        }

        private void HideCanvasItem(CanvasItem item)
        {
            if (item == null || !IsInstanceValid(item) || !item.Visible)
                return;
            _hudSnapshot[item] = item.Visible;
            item.Visible = false;
        }

        /// <summary>
        /// 隐藏 CanvasLayer 节点（CanvasLayer 继承自 Node 而非 CanvasItem，需要单独处理）。
        /// </summary>
        private void HideCanvasLayer(CanvasLayer layer)
        {
            if (layer == null || !IsInstanceValid(layer) || !layer.Visible)
                return;
            _hudSnapshot[layer] = layer.Visible;
            layer.Visible = false;
        }
    }
}
