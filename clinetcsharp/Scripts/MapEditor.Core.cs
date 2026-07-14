using Godot;
using System.Collections.Generic;
using System.Linq;

namespace ClinetCSharp
{
    public partial class MapEditor : Node2D
    {
        private void EnterEditMode()
        {
            GD.Print("[MapEditor] 进入编辑模式 — 冻结游戏状态，重新加载地图数据");

            // 1. 保存游戏状态快照
            _wasPlayerVisible = Player?.Visible ?? false;

            var patrolOverlay = GetTree().GetFirstNodeInGroup("monster_patrol_overlay") as MonsterPatrolOverlay;
            _wasMonsterPatrolOverlayVisible = patrolOverlay?.OverlayEnabled ?? false;

            // 2. 暂停游戏树（冻结所有 _Process/_PhysicsProcess），
            //    并通过 UIInputPolicy 保证编辑器相关节点仍可处理输入
            UIInputPolicy.Instance?.RegisterUiNode(this);
            if (Camera is Node camNode)
                UIInputPolicy.Instance?.RegisterUiNode(camNode);
            UIInputPolicy.Instance?.PauseGame();

            // 3. 隐藏玩家、所有怪物实例、所有NPC实例、巡逻覆盖层、宝箱、掉落物
            if (Player != null)
                Player.Visible = false;

            var monsterMgr = GetTree().GetFirstNodeInGroup("monster_manager") as MonsterManager;
            monsterMgr?.SetAllMonstersVisible(false);

            var npcMgr = GetTree().GetFirstNodeInGroup("npc_manager") as NpcManager;
            npcMgr?.SetAllNpcsVisible(false);
            npcMgr?.CloseInteractMenu();

            patrolOverlay?.SetOverlayEnabled(false);

            var chestMgr = GetTree().GetFirstNodeInGroup("chest_manager") as ChestManager;
            chestMgr?.SetAllChestsVisible(false);

            var dropMgr = GetTree().GetFirstNodeInGroup("drop_manager") as DropManager;
            dropMgr?.SetAllDropsVisible(false);

            var decMgr = GetTree().GetFirstNodeInGroup("map_decoration_manager") as MapDecorationManager;
            decMgr?.SetAllDecorationsVisible(false);

            // 4. 隐藏所有与地图编辑器无关的 UI（功能按钮栏、技能栏、小地图、所有面板）
            var funcBar = GetTree()?.GetFirstNodeInGroup("function_bar") as CanvasItem;
            if (funcBar != null)
            {
                _wasFunctionBarVisible = funcBar.Visible;
                funcBar.Visible = false;
            }

            var skillBar = GetTree()?.GetFirstNodeInGroup("skill_bar") as CanvasItem;
            if (skillBar != null)
            {
                _wasSkillBarVisible = skillBar.Visible;
                skillBar.Visible = false;
            }

            var minimap = GetTree().GetFirstNodeInGroup("minimap_hud") as CanvasLayer;
            if (minimap != null)
            {
                _wasMinimapVisible = minimap.Visible;
                minimap.Visible = false;
            }

            _wasVisiblePanels.Clear();
            var panelMgr = PanelManager.Instance;
            if (panelMgr != null)
            {
                // 先快照当前可见的面板
                var uiCanvas = panelMgr.GetParent() as CanvasLayer;
                if (uiCanvas != null)
                {
                    foreach (var child in uiCanvas.GetChildren())
                    {
                        if (child is DraggablePanel dp && dp.Visible)
                            _wasVisiblePanels.Add(dp);
                    }
                }
                // 然后全部隐藏
                panelMgr.HideAll();
            }

            // ===== 核心变更：创建独立编辑 GridManager，彻底隔离编辑副作用 =====
            if (_gameGridManager != null)
            {
                // 防御性清理：防止上一次编辑模式异常退出导致旧实例残留
                if (_editGridManager != null && IsInstanceValid(_editGridManager))
                {
                    GD.Print("[MapEditor] 清理残留的编辑 GridManager");
                    _editGridManager.QueueFree();
                    _editGridManager = null;
                }

                // 隐藏游戏 GridManager（只读，编辑期间不被修改）
                _gameGridManager.Visible = false;

                // 创建编辑用 GridManager，完全复制渲染参数
                _editGridManager = new GridManager();
                _editGridManager.Name = "EditGridManager";
                _editGridManager.ZIndex = _gameGridManager.ZIndex;
                _editGridManager.ZAsRelative = _gameGridManager.ZAsRelative;

                // 复制地图和渲染参数（MapBounds 由 GridData 推导，无需复制）
                _editGridManager.GridSize = _gameGridManager.GridSize;
                _editGridManager.CurrentMapName = _gameGridManager.CurrentMapName;
                _editGridManager.LineColor = _gameGridManager.LineColor;
                _editGridManager.LineWidth = _gameGridManager.LineWidth;
                _editGridManager.AutoLineWidth = _gameGridManager.AutoLineWidth;
                _editGridManager.LineWidthScale = _gameGridManager.LineWidthScale;
                _editGridManager.MinScreenLineWidth = _gameGridManager.MinScreenLineWidth;
                _editGridManager.MaxScreenLineWidth = _gameGridManager.MaxScreenLineWidth;
                _editGridManager.GridAntiAliasSoftness = _gameGridManager.GridAntiAliasSoftness;
                _editGridManager.AdaptiveCalibrationEnabled = _gameGridManager.AdaptiveCalibrationEnabled;
                _editGridManager.RefZoomA = _gameGridManager.RefZoomA;
                _editGridManager.RefWidthA = _gameGridManager.RefWidthA;
                _editGridManager.RefZoomB = _gameGridManager.RefZoomB;
                _editGridManager.RefWidthB = _gameGridManager.RefWidthB;
                _editGridManager.ResponsiveMode = _gameGridManager.ResponsiveMode;
                _editGridManager.VisibleGridsX = _gameGridManager.VisibleGridsX;
                _editGridManager.MinGridSize = _gameGridManager.MinGridSize;
                _editGridManager.MaxGridSize = _gameGridManager.MaxGridSize;
                _editGridManager.ShowOutsideMapGray = _gameGridManager.ShowOutsideMapGray;
                _editGridManager.OutsideMapColor = _gameGridManager.OutsideMapColor;
                _editGridManager.ShowGridCoords = _gameGridManager.ShowGridCoords;
                _editGridManager.ShowCellUids = _gameGridManager.ShowCellUids;
                _editGridManager.ShowTerrainLabels = true; // 编辑模式默认显示地形标签

                // 编辑模式标志（这是编辑地图独有的，游戏 GridManager 不再被修改）
                _editGridManager.IsEditMode = true;

                // 添加到场景树（放在原 GridManager 的父节点下，保持坐标一致）
                _gameGridManager.GetParent()?.AddChild(_editGridManager);
                _editGridManager.GlobalPosition = _gameGridManager.GlobalPosition;

                // 编辑用 GridManager 需在暂停时继续运行 _Process 以响应 zoom/线宽变化
                UIInputPolicy.Instance?.RegisterUiNode(_editGridManager);

                // 显式加载地图数据（防御性：确保 _Ready() 加载成功，若失败则再次尝试）
                var loaded = _editGridManager.LoadMap(_editGridManager.CurrentMapName);
                GD.Print($"[MapEditor] 创建独立编辑 GridManager: {_editGridManager.CurrentMapName} {_editGridManager.MapWidth}x{_editGridManager.MapHeight}, LoadMap={loaded}");

                // 创建编辑期装饰摆件管理器，与游戏运行时管理器隔离
                _editDecorationManager = new MapDecorationManager();
                _editDecorationManager.Name = "EditDecorationManager";
                _editDecorationManager.AddToGroup("edit_decoration_manager");
                _editDecorationManager.SpawnEditable = true;
                _editDecorationManager.GridSize = _editGridManager.GridSize;
                _editGridManager.AddChild(_editDecorationManager);
                _editDecorationManager.SpawnDecorations(_editGridManager.GridData);

                // 编辑期装饰管理器也需在暂停时继续处理
                UIInputPolicy.Instance?.RegisterUiNode(_editDecorationManager);

                // 订阅摆件拖动请求
                MapDecoration.DecorationDragRequested += OnDecorationDragRequested;
            }

            // 5. 清空编辑历史
            SelectedCells.Clear();
            IsSelecting = false;
            _undoStack.Clear();
            _redoStack.Clear();
            _selectionHistory.Clear();

            QueueRedraw();
            ShowEditorUi();

            // 刷新地图列表（必须在 ShowEditorUi 之后，确保面板已创建）
            RefreshMapList();
        }

        private void ExitEditMode()
        {
            GD.Print("[MapEditor] 退出编辑模式 — 保存地图，恢复游戏状态");

            // ===== 核心变更：保存编辑结果并销毁独立编辑 GridManager =====
            if (_editGridManager != null)
            {
                var mapName = _editGridManager.CurrentMapName;
                var err = _editGridManager.SaveCurrentMap();
                if (err == Error.Ok)
                {
                    GD.Print($"[MapEditor] 地图 '{mapName}' 保存成功");
                }
                else
                {
                    GD.PushError($"[MapEditor] 地图 '{mapName}' 保存失败: {err}");
                    ShowToast($"保存失败: {err}", Colors.Red);
                }

                // 取消订阅并清理编辑期装饰管理器
                MapDecoration.DecorationDragRequested -= OnDecorationDragRequested;
                EndDrag(true); // 强制取消未完成的拖拽
                if (_editDecorationManager != null && IsInstanceValid(_editDecorationManager))
                {
                    UIInputPolicy.Instance?.UnregisterUiNode(_editDecorationManager);
                    _editDecorationManager.QueueFree();
                    _editDecorationManager = null;
                }

                UIInputPolicy.Instance?.UnregisterUiNode(_editGridManager);
                _editGridManager.QueueFree();
                _editGridManager = null;
            }

            // 1. 恢复游戏树运行，并恢复 UIInputPolicy 管理的 ProcessMode
            UIInputPolicy.Instance?.ResumeGame();
            UIInputPolicy.Instance?.UnregisterUiNode(this);
            if (Camera is Node camNode)
                UIInputPolicy.Instance?.UnregisterUiNode(camNode);

            // 2. 恢复玩家、所有怪物实例、所有NPC实例、巡逻覆盖层显示
            if (Player != null)
                Player.Visible = _wasPlayerVisible;

            var monsterMgr = GetTree().GetFirstNodeInGroup("monster_manager") as MonsterManager;
            monsterMgr?.SetAllMonstersVisible(true);

            var npcMgr = GetTree().GetFirstNodeInGroup("npc_manager") as NpcManager;
            npcMgr?.SetAllNpcsVisible(true);

            var patrolOverlay = GetTree().GetFirstNodeInGroup("monster_patrol_overlay") as MonsterPatrolOverlay;
            patrolOverlay?.SetOverlayEnabled(_wasMonsterPatrolOverlayVisible);

            var chestMgr = GetTree().GetFirstNodeInGroup("chest_manager") as ChestManager;
            chestMgr?.SetAllChestsVisible(true);

            var dropMgr = GetTree().GetFirstNodeInGroup("drop_manager") as DropManager;
            dropMgr?.SetAllDropsVisible(true);

            var decMgr = GetTree().GetFirstNodeInGroup("map_decoration_manager") as MapDecorationManager;
            decMgr?.SetAllDecorationsVisible(true);

            // 恢复所有与地图编辑器无关的 UI
            var funcBar = GetTree()?.GetFirstNodeInGroup("function_bar") as CanvasItem;
            if (funcBar != null)
                funcBar.Visible = _wasFunctionBarVisible;

            var skillBar = GetTree()?.GetFirstNodeInGroup("skill_bar") as CanvasItem;
            if (skillBar != null)
                skillBar.Visible = _wasSkillBarVisible;

            var minimap = GetTree().GetFirstNodeInGroup("minimap_hud") as CanvasLayer;
            if (minimap != null)
                minimap.Visible = _wasMinimapVisible;

            foreach (var panel in _wasVisiblePanels)
            {
                if (panel != null && IsInstanceValid(panel))
                    panel.Visible = true;
            }
            _wasVisiblePanels.Clear();

            // 3. 显示游戏 GridManager 并重新加载最新地图数据（反映编辑保存的结果）
            if (_gameGridManager != null)
            {
                _gameGridManager.Visible = true;
                _gameGridManager.LoadMap(_gameGridManager.CurrentMapName);
                GD.Print($"[MapEditor] 游戏 GridManager 重新加载地图: {_gameGridManager.CurrentMapName}");
            }

            // 4. 清除选中的格子
            SelectedCells.Clear();
            IsSelecting = false;
            QueueRedraw();

            HideEditorUi();
        }

        private async System.Threading.Tasks.Task RunAutoTestScreenshot()
        {
            // 1. 等待编辑器完全加载
            GD.Print("[MapEditor] Auto-test: waiting 3s for editor to fully load...");
            await ToSignal(GetTree().CreateTimer(3.0f, true), "timeout");

            var camera = GetTree().GetFirstNodeInGroup("camera") as Camera2D;
            var viewport = GetViewport();

            // 2. 在多个关键 zoom 级别分别截图，覆盖所有边界情况
            float[] testZooms = new float[] { 1.0f, 0.7f, 0.5f, 0.4f, 0.3f, 0.2f };
            foreach (var zoom in testZooms)
            {
                if (camera != null)
                {
                    camera.Zoom = new Vector2(zoom, zoom);
                    GD.Print($"[MapEditor] Auto-test: set zoom={zoom:F2}");
                }

                // 等待画面稳定（GridManager 检测到 zoom 变化后会 QueueRedraw）
                await ToSignal(GetTree().CreateTimer(1.0f, true), "timeout");

                var img = viewport.GetTexture().GetImage();
                var path = ProjectSettings.GlobalizePath($"user://auto_test_grid_zoom_{zoom:F2}.png");
                var dir = System.IO.Path.GetDirectoryName(path);
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);
                img.SavePng(path);
                GD.Print($"[MapEditor] Auto-test: screenshot saved to {path}");
            }

            await ToSignal(GetTree().CreateTimer(0.5f, true), "timeout");
            GD.Print("[MapEditor] Auto-test: quitting...");
            GetTree().Quit();
        }

        /// <summary>
        /// 建筑图鉴列表项：显示颜色块 + 建筑名称，ButtonDown 时触发从面板拖拽。
        /// </summary>
        private partial class BuildingListItem : Button
        {
            private readonly MapEditor _editor;
            private readonly int _decorationTypeId;
            private readonly DecorationConfig _cfg;
            public int DecorationTypeId => _decorationTypeId;
            public DecorationConfig Config => _cfg;

            public BuildingListItem(MapEditor editor, DecorationConfig cfg)
            {
                _editor = editor;
                _cfg = cfg;
                _decorationTypeId = cfg.Id;
                CustomMinimumSize = new Vector2(0, 36);
                MouseFilter = MouseFilterEnum.Stop;
                ClipText = true;
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                AddThemeFontSizeOverride("font_size", 12);
                AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
                SetDecorationStyle(_cfg);
                ButtonDown += () => _editor.StartPaletteDrag(_decorationTypeId);
                TooltipText = _cfg.DisplayName;
            }

            private void SetDecorationStyle(DecorationConfig cfg)
            {
                var iconColor = cfg.Color;
                var normal = new StyleBoxFlat
                {
                    BgColor = new Color(0.15f, 0.15f, 0.15f, 0.8f),
                    BorderColor = new Color(0.3f, 0.3f, 0.3f),
                    BorderWidthBottom = 1,
                    BorderWidthLeft = 1,
                    BorderWidthRight = 1,
                    BorderWidthTop = 1,
                };
                var hover = new StyleBoxFlat
                {
                    BgColor = new Color(0.25f, 0.25f, 0.25f, 0.9f),
                    BorderColor = new Color(0.5f, 0.5f, 0.5f),
                    BorderWidthBottom = 1,
                    BorderWidthLeft = 1,
                    BorderWidthRight = 1,
                    BorderWidthTop = 1,
                };
                var pressed = new StyleBoxFlat
                {
                    BgColor = new Color(0.35f, 0.35f, 0.35f, 0.95f),
                    BorderColor = Colors.White,
                    BorderWidthBottom = 1,
                    BorderWidthLeft = 1,
                    BorderWidthRight = 1,
                    BorderWidthTop = 1,
                };
                AddThemeStyleboxOverride("normal", normal);
                AddThemeStyleboxOverride("hover", hover);
                AddThemeStyleboxOverride("pressed", pressed);

                // 用文本前缀加一个彩色标记来示意建筑颜色
                Text = $"  {cfg.DisplayName}";
            }

            public override void _Draw()
            {
                base._Draw();
                var iconRect = new Rect2(new Vector2(6, 8), new Vector2(20, 20));
                DrawRect(iconRect, _cfg.Color, true);
                DrawRect(iconRect, _cfg.BorderColor, false, 1.5f);
            }
        }
    }
}
