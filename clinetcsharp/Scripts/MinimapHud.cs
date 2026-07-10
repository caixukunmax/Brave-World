using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 右上角常驻地图小窗 HUD。
    /// 支持折叠/展开，点击小窗打开大地图面板。
    /// </summary>
    public partial class MinimapHud : CanvasLayer
    {
        [Export] public float OffsetX { get; set; } = 16.0f;
        [Export] public float OffsetY { get; set; } = 16.0f;
        [Export] public Vector2 DefaultSize { get; set; } = new Vector2(240, 180);
        [Export] public Vector2 ExpandButtonSize { get; set; } = new Vector2(32, 32);

        private PanelContainer _mainPanel;
        private MapViewControl _mapView;
        private Label _titleLabel;
        private Button _collapseButton;
        private Button _expandButton;

        private GridManager _gridManager;
        private string _lastMapName;
        private bool _isCollapsed;

        public override void _Ready()
        {
            Layer = 75;
            AddToGroup("minimap_hud");

            BuildUi();
            ApplyLayout();
        }

        public override void _Process(double delta)
        {
            var gridMgr = GetGridManager();
            if (gridMgr == null) return;

            if (gridMgr.CurrentMapName != _lastMapName)
            {
                _lastMapName = gridMgr.CurrentMapName;
                if (_titleLabel != null)
                    _titleLabel.Text = _lastMapName;
            }
        }

        public void ToggleCollapsed()
        {
            _isCollapsed = !_isCollapsed;
            UpdateCollapsedState();
        }

        public void SetCollapsed(bool collapsed)
        {
            _isCollapsed = collapsed;
            UpdateCollapsedState();
        }

        private void UpdateCollapsedState()
        {
            if (_mainPanel != null)
                _mainPanel.Visible = !_isCollapsed;
            if (_expandButton != null)
                _expandButton.Visible = _isCollapsed;
        }

        private void BuildUi()
        {
            // 展开按钮：小地图折叠后显示在右上角
            _expandButton = new Button
            {
                Text = "地",
                FocusMode = Control.FocusModeEnum.None,
                Visible = false,
            };
            _expandButton.Pressed += () => SetCollapsed(false);
            StyleExpandButton(_expandButton);
            AddChild(_expandButton);

            // 主面板：暗色半透明背景，右上角锚定
            _mainPanel = new PanelContainer();
            _mainPanel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
            _mainPanel.CustomMinimumSize = DefaultSize;
            _mainPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0.05f, 0.05f, 0.05f, 0.85f),
                BorderColor = new Color(0.35f, 0.35f, 0.35f, 0.8f),
                BorderWidthBottom = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                BorderWidthTop = 1,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4,
            });
            _mainPanel.GuiInput += OnMainPanelGuiInput;
            AddChild(_mainPanel);

            var vbox = new VBoxContainer();
            vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            vbox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            _mainPanel.AddChild(vbox);

            // 标题栏：地图名 + 折叠按钮
            var titleBar = new HBoxContainer();
            titleBar.CustomMinimumSize = new Vector2(0, 24);
            vbox.AddChild(titleBar);

            _titleLabel = new Label
            {
                Text = "地图",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            _titleLabel.AddThemeFontSizeOverride("font_size", 12);
            _titleLabel.AddThemeColorOverride("font_color", UiStyles.TextColor);
            titleBar.AddChild(_titleLabel);

            _collapseButton = new Button
            {
                Text = "−",
                FocusMode = Control.FocusModeEnum.None,
                CustomMinimumSize = new Vector2(24, 24),
            };
            _collapseButton.Pressed += () => SetCollapsed(true);
            StyleTitleButton(_collapseButton);
            titleBar.AddChild(_collapseButton);

            // 地图视图
            _mapView = new MapViewControl
            {
                ShowCameraFrame = true,
                MaxTextureLongSide = 256,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            vbox.AddChild(_mapView);
        }

        private void StyleTitleButton(Button btn)
        {
            btn.AddThemeFontSizeOverride("font_size", 14);
            btn.AddThemeColorOverride("font_color", UiStyles.TextColor);
            btn.AddThemeColorOverride("font_hover_color", Colors.White);
            btn.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0) });
            btn.AddThemeStyleboxOverride("hover", new StyleBoxFlat
            {
                BgColor = new Color(0.35f, 0.35f, 0.35f, 0.7f),
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3,
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3,
            });
            btn.AddThemeStyleboxOverride("pressed", new StyleBoxFlat
            {
                BgColor = new Color(0.2f, 0.5f, 0.2f, 0.75f),
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3,
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3,
            });
        }

        private void StyleExpandButton(Button btn)
        {
            btn.AddThemeFontSizeOverride("font_size", 12);
            btn.AddThemeColorOverride("font_color", UiStyles.TextColor);
            btn.AddThemeStyleboxOverride("normal", new StyleBoxFlat
            {
                BgColor = new Color(0.05f, 0.05f, 0.05f, 0.85f),
                BorderColor = new Color(0.35f, 0.35f, 0.35f, 0.8f),
                BorderWidthBottom = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                BorderWidthTop = 1,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4,
            });
            btn.AddThemeStyleboxOverride("hover", new StyleBoxFlat
            {
                BgColor = new Color(0.25f, 0.25f, 0.25f, 0.9f),
                BorderColor = new Color(0.5f, 0.5f, 0.5f, 0.9f),
                BorderWidthBottom = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                BorderWidthTop = 1,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4,
            });
            btn.AddThemeStyleboxOverride("pressed", new StyleBoxFlat
            {
                BgColor = new Color(0.2f, 0.5f, 0.2f, 0.75f),
                BorderColor = new Color(0.35f, 0.35f, 0.35f, 0.8f),
                BorderWidthBottom = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                BorderWidthTop = 1,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4,
            });
        }

        private void ApplyLayout()
        {
            if (_mainPanel != null)
            {
                _mainPanel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
                _mainPanel.CustomMinimumSize = DefaultSize;
                _mainPanel.OffsetLeft = -DefaultSize.X - OffsetX;
                _mainPanel.OffsetTop = OffsetY;
                _mainPanel.OffsetRight = -OffsetX;
                _mainPanel.OffsetBottom = OffsetY + DefaultSize.Y;
            }

            if (_expandButton != null)
            {
                _expandButton.SetAnchorsPreset(Control.LayoutPreset.TopRight);
                _expandButton.CustomMinimumSize = ExpandButtonSize;
                _expandButton.OffsetLeft = -ExpandButtonSize.X - OffsetX;
                _expandButton.OffsetTop = OffsetY;
                _expandButton.OffsetRight = -OffsetX;
                _expandButton.OffsetBottom = OffsetY + ExpandButtonSize.Y;
            }
        }

        private void OnMainPanelGuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouseBtn && mouseBtn.Pressed && mouseBtn.ButtonIndex == MouseButton.Left)
            {
                OpenBigMap();
            }
        }

        private void OpenBigMap()
        {
            BigMapPanel.Instance?.Open();
        }

        private GridManager GetGridManager()
        {
            if (_gridManager != null && IsInstanceValid(_gridManager)) return _gridManager;
            _gridManager = GetTree()?.GetFirstNodeInGroup("grid_manager") as GridManager;
            return _gridManager;
        }
    }
}
