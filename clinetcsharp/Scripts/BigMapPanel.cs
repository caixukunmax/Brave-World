using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// 大地图全屏覆盖层。
    /// 打开时接管主相机并拉远视角，显示与地图编辑器同款的网格渲染；
    /// 关闭时恢复游戏相机与 UI。
    /// </summary>
    public partial class BigMapPanel : CanvasLayer
    {
        public static BigMapPanel Instance { get; private set; }

        [Export] public float DefaultZoom { get; set; } = 0.3f;
        [Export] public float MinZoom { get; set; } = 0.1f;
        [Export] public float MaxZoom { get; set; } = 1.0f;
        [Export] public float ZoomStep { get; set; } = 0.05f;

        private const Key ToggleKey = Key.M;

        private PanelContainer _toolbar;
        private Label _titleLabel;
        private HSlider _zoomSlider;
        private Button _closeButton;
        private Control _markerOverlay;

        [Export] public Color PlayerMarkerColor { get; set; } = new Color(0.2f, 1.0f, 0.2f);
        [Export] public Color MonsterMarkerColor { get; set; } = new Color(1.0f, 0.25f, 0.25f);
        [Export] public Color NpcMarkerColor { get; set; } = new Color(0.25f, 0.6f, 1.0f);
        [Export] public float MarkerRadius { get; set; } = 6.0f;

        private CameraController _camera;
        private GridManager _gridManager;
        private Player _player;

        // 打开大地图前的状态快照
        private bool _wasCameraEditorMode;
        private Vector2 _savedCameraPosition;
        private Vector2 _savedCameraZoom;
        private bool _savedCameraReturn;
        private Node2D _savedCameraTarget;
        private float _savedCameraMinZoom;
        private float _savedCameraMaxZoom;
        private bool _isOpen;

        // 被隐藏对象的快照
        private readonly List<CanvasItem> _hiddenItems = new();
        private readonly List<DraggablePanel> _hiddenPanels = new();

        public override void _Ready()
        {
            Instance = this;
            Layer = 110;
            Visible = false;
            AddToGroup("big_map_panel");

            BuildUi();
        }

        public override void _ExitTree()
        {
            if (Instance == this)
                Instance = null;
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is not InputEventKey key || !key.Pressed || key.Echo)
                return;

            bool isToggle = key.Keycode == ToggleKey;
            bool isClose = key.Keycode == Key.Escape && _isOpen;
            if (!isToggle && !isClose)
                return;

            // 地图编辑模式下不响应，避免冲突
            if (isToggle && _camera != null && _camera.IsEditorMode && !_isOpen)
                return;

            Toggle();
            GetViewport()?.SetInputAsHandled();
        }

        public override void _Process(double delta)
        {
            if (_isOpen && _markerOverlay != null)
                _markerOverlay.QueueRedraw();
        }

        public void Toggle()
        {
            if (_isOpen)
                Close();
            else
                Open();
        }

        public void Open()
        {
            if (_isOpen) return;

            ResolveNodes();
            if (_camera == null || _gridManager == null)
            {
                GD.PushError("[BigMapPanel] 无法打开大地图：缺少 Camera 或 GridManager");
                return;
            }

            // 如果正在地图编辑模式，拒绝打开
            if (_camera.IsEditorMode)
            {
                GD.Print("[BigMapPanel] 地图编辑模式中，暂不打开大地图");
                return;
            }

            _isOpen = true;

            SaveState();
            HideGameUiAndEntities();

            // 进入相机编辑模式
            _camera.IsEditorMode = true;
            _camera.SetReturning(false);
            _camera.MinZoom = MinZoom;
            _camera.MaxZoom = MaxZoom;

            // 设置视角
            Vector2 targetPos = _player != null ? _player.Position : _camera.Position;
            _camera.Position = targetPos;
            _camera.Zoom = new Vector2(DefaultZoom, DefaultZoom);

            SyncSliderToCamera();

            Visible = true;

            // 暂停游戏，但保证相机、本覆盖层、网格管理器仍能处理
            UIInputPolicy.Instance?.RegisterUiNode(this);
            UIInputPolicy.Instance?.RegisterUiNode(_camera);
            UIInputPolicy.Instance?.RegisterUiNode(_gridManager);
            UIInputPolicy.Instance?.PauseGame();

            GD.Print($"[BigMapPanel] 打开大地图 zoom={DefaultZoom} pos={targetPos}");
        }

        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;

            UIInputPolicy.Instance?.ResumeGame();
            UIInputPolicy.Instance?.UnregisterUiNode(_gridManager);
            UIInputPolicy.Instance?.UnregisterUiNode(_camera);
            UIInputPolicy.Instance?.UnregisterUiNode(this);

            RestoreState();
            ShowGameUiAndEntities();

            Visible = false;

            GD.Print("[BigMapPanel] 关闭大地图");
        }

        #region UI Construction

        private void BuildUi()
        {
            // 全屏标记覆盖层：在工具栏下方绘制玩家/怪物/NPC 位置标记
            _markerOverlay = new Control();
            _markerOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _markerOverlay.MouseFilter = Control.MouseFilterEnum.Ignore;
            _markerOverlay.Draw += DrawMarkers;
            AddChild(_markerOverlay);

            _toolbar = new PanelContainer();
            _toolbar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
            _toolbar.CustomMinimumSize = new Vector2(0, 48);
            _toolbar.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0.05f, 0.05f, 0.05f, 0.85f),
                BorderColor = new Color(0.35f, 0.35f, 0.35f, 0.8f),
                BorderWidthBottom = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                BorderWidthTop = 1,
            });
            AddChild(_toolbar);

            var hbox = new HBoxContainer();
            hbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            hbox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            _toolbar.AddChild(hbox);

            _titleLabel = new Label
            {
                Text = "大地图",
                VerticalAlignment = VerticalAlignment.Center,
            };
            _titleLabel.AddThemeFontSizeOverride("font_size", 16);
            _titleLabel.AddThemeColorOverride("font_color", UiStyles.TextColor);
            _titleLabel.CustomMinimumSize = new Vector2(80, 0);
            hbox.AddChild(_titleLabel);

            var zoomLabel = new Label
            {
                Text = "缩放",
                VerticalAlignment = VerticalAlignment.Center,
            };
            zoomLabel.AddThemeColorOverride("font_color", UiStyles.TextColor);
            hbox.AddChild(zoomLabel);

            _zoomSlider = new HSlider
            {
                MinValue = MinZoom,
                MaxValue = MaxZoom,
                Step = ZoomStep,
                Value = DefaultZoom,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(200, 0),
            };
            _zoomSlider.ValueChanged += OnZoomSliderChanged;
            hbox.AddChild(_zoomSlider);

            var spacer = new Control();
            spacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            hbox.AddChild(spacer);

            _closeButton = new Button
            {
                Text = "关闭",
                FocusMode = Control.FocusModeEnum.None,
            };
            _closeButton.Pressed += Close;
            hbox.AddChild(_closeButton);
        }

        private void OnZoomSliderChanged(double value)
        {
            if (_camera == null) return;
            float z = (float)value;
            _camera.Zoom = new Vector2(z, z);
        }

        private void SyncSliderToCamera()
        {
            if (_camera == null || _zoomSlider == null) return;
            _zoomSlider.Value = _camera.Zoom.X;
        }

        #endregion

        #region State Save / Restore

        private void SaveState()
        {
            _wasCameraEditorMode = _camera.IsEditorMode;
            _savedCameraPosition = _camera.Position;
            _savedCameraZoom = _camera.Zoom;
            _savedCameraReturn = _camera.IsReturning;
            _savedCameraTarget = _camera.Target;
            _savedCameraMinZoom = _camera.MinZoom;
            _savedCameraMaxZoom = _camera.MaxZoom;

            if (_titleLabel != null)
                _titleLabel.Text = string.IsNullOrEmpty(_gridManager.CurrentMapName)
                    ? "大地图"
                    : $"大地图 - {_gridManager.CurrentMapName}";
        }

        private void RestoreState()
        {
            _camera.IsEditorMode = _wasCameraEditorMode;
            _camera.Position = _savedCameraPosition;
            _camera.Zoom = _savedCameraZoom;
            _camera.Target = _savedCameraTarget;
            _camera.MinZoom = _savedCameraMinZoom;
            _camera.MaxZoom = _savedCameraMaxZoom;

            // 如果退出前在跟随玩家，恢复回归状态
            if (_savedCameraTarget != null)
            {
                _camera.SetReturning(_savedCameraReturn);
            }
        }

        #endregion

        #region Hide / Show Game World

        private void HideGameUiAndEntities()
        {
            _hiddenItems.Clear();
            _hiddenPanels.Clear();

            HideItem(GetTree()?.GetFirstNodeInGroup("function_bar") as CanvasItem);
            HideItem(GetTree()?.GetFirstNodeInGroup("skill_bar") as CanvasItem);
            HideItem(GetTree()?.GetFirstNodeInGroup("buff_bar") as CanvasItem);
            HideItem(GetTree()?.GetFirstNodeInGroup("minimap_hud") as CanvasItem);

            var patrolOverlay = GetTree()?.GetFirstNodeInGroup("monster_patrol_overlay") as CanvasItem;
            HideItem(patrolOverlay);

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

            if (_player != null)
                _player.Visible = false;

            // 隐藏所有面板
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
                            _hiddenPanels.Add(dp);
                            dp.Visible = false;
                        }
                    }
                }
            }
        }

        private void ShowGameUiAndEntities()
        {
            foreach (var item in _hiddenItems)
            {
                if (item != null && IsInstanceValid(item))
                    item.Visible = true;
            }
            _hiddenItems.Clear();

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

            if (_player != null)
                _player.Visible = true;

            foreach (var panel in _hiddenPanels)
            {
                if (panel != null && IsInstanceValid(panel))
                    panel.Visible = true;
            }
            _hiddenPanels.Clear();
        }

        private void HideItem(CanvasItem item)
        {
            if (item == null || !IsInstanceValid(item) || !item.Visible) return;
            _hiddenItems.Add(item);
            item.Visible = false;
        }

        #endregion

        #region Marker Drawing

        private void DrawMarkers()
        {
            var viewport = GetViewport();
            if (viewport == null || _camera == null) return;

            Transform2D canvasTransform = viewport.GetCanvasTransform();

            // 玩家
            if (_player != null && IsInstanceValid(_player))
                DrawMarkerAt(canvasTransform * _player.GlobalPosition, PlayerMarkerColor, MarkerRadius);

            // 怪物
            var monsterMgr = GetTree()?.GetFirstNodeInGroup("monster_manager") as MonsterManager;
            if (monsterMgr != null)
            {
                foreach (var monster in monsterMgr.GetMonsters())
                {
                    if (monster == null || !IsInstanceValid(monster)) continue;
                    DrawMarkerAt(canvasTransform * monster.GlobalPosition, MonsterMarkerColor, MarkerRadius);
                }
            }

            // NPC
            var npcMgr = NpcManager.Instance ?? GetTree()?.GetFirstNodeInGroup("npc_manager") as NpcManager;
            if (npcMgr != null)
            {
                foreach (var npc in npcMgr.GetNpcs())
                {
                    if (npc == null || !IsInstanceValid(npc)) continue;
                    DrawMarkerAt(canvasTransform * npc.GlobalPosition, NpcMarkerColor, MarkerRadius);
                }
            }
        }

        private void DrawMarkerAt(Vector2 screenPos, Color color, float radius)
        {
            // 只绘制在屏幕内的标记
            var viewportRect = _markerOverlay.GetViewportRect();
            if (!viewportRect.HasPoint(screenPos))
                return;

            _markerOverlay.DrawCircle(screenPos, radius, color);
            _markerOverlay.DrawArc(screenPos, radius + 1.5f, 0.0f, Mathf.Tau, 16, new Color(0, 0, 0, 0.5f), 1.5f);
        }

        #endregion

        #region Node Resolution

        private void ResolveNodes()
        {
            if (_camera == null || !IsInstanceValid(_camera))
                _camera = GetTree()?.GetFirstNodeInGroup("camera") as CameraController;

            if (_gridManager == null || !IsInstanceValid(_gridManager))
                _gridManager = GetTree()?.GetFirstNodeInGroup("grid_manager") as GridManager;

            if (_player == null || !IsInstanceValid(_player))
                _player = GetTree()?.GetFirstNodeInGroup("player") as Player;
        }

        #endregion
    }
}
