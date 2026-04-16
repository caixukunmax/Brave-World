using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// 调试面板 - 主文件
    /// 功能：游戏调试和配置界面
    /// 结构：按功能分组，支持折叠展开
    ///
    /// 拆分文件：
    ///   DebugPanel.Nodes.cs     — 节点初始化、分区折叠、信号连接
    ///   DebugPanel.Handlers.cs  — 所有事件处理方法
    ///   DebugPanel.DynamicUI.cs — 动态创建控件、预设 UI
    ///   DebugPanel.Config.cs    — 配置保存/加载/预设操作
    ///   DebugPanel.Undo.cs      — 撤销系统
    /// </summary>
    public partial class DebugPanel : CanvasLayer
    {
        #region Constants
        private const int CONFIG_VERSION = 1;
        private const string CONFIG_PATH = "user://debug_panel_config.cfg";
        private const string PRESET_PATH = "user://debug_panel_presets.cfg";
        private const int MAX_HISTORY_STEPS = 20;

        public static readonly Color[] COLOR_PRESETS = new Color[]
        {
            Colors.Black,
            Colors.White,
            new Color(1, 0, 0, 1),
            new Color(0, 1, 0, 1),
            new Color(0, 0, 1, 1),
            new Color(1, 1, 0, 1),
        };

        private static readonly string[] EASE_TYPE_NAMES = new string[]
        {
            "线性", "平滑", "缓出", "缓入", "缓入缓出"
        };

        public static readonly string[] FONT_LIST = new string[]
        {
            "res://assets/fonts/SourceHanSansCN-Bold.otf",
            "res://assets/fonts/Alibaba-PuHuiTi-Bold.otf",
            "res://assets/fonts/LXGW WenKai TC-Bold.ttf"
        };

        private static readonly string[] FONT_NAMES = new string[]
        {
            "思源黑体", "阿里巴巴普惠体", "霞鹜文楷"
        };
        #endregion

        #region Node References - Main Controls
        private Panel _panel;
        private Button _toggleButton;
        private ScrollContainer _scrollContainer;
        private TabContainer _tabContainer;
        #endregion

        #region Node References - Map Tab - Basic Settings
        private HSlider _gridSizeSlider;
        private Label _gridSizeValue;
        private HSlider _zoomSlider;
        private Label _zoomValue;
        #endregion

        #region Node References - Map Tab - Grid Line Settings
        private HSlider _gridLineWidthSlider;
        private Label _gridLineWidthValue;
        private HSlider _gridLineBrightnessSlider;
        private Label _gridLineBrightnessValue;
        private CheckButton _gridCoordsCheck;
        #endregion

        #region Node References - Map Tab - Camera Settings
        private HSlider _cameraReturnDelaySlider;
        private Label _cameraReturnDelayValue;
        private HSlider _cameraReturnSpeedSlider;
        private Label _cameraReturnSpeedValue;
        private OptionButton _cameraEaseTypeOption;
        private HSlider _cameraEasePowerSlider;
        private Label _cameraEasePowerValue;
        #endregion

        #region Node References - Map Tab - Debug Toggles
        private CheckButton _debugInfoCheck;
        private CheckButton _cameraDebugCheck;
        #endregion

        #region Node References - Player Tab - Appearance
        private HSlider _playerSizeSlider;
        private Label _playerSizeValue;
        private HSlider _playerSizeScaleSlider;
        private Label _playerSizeScaleValue;
        private HSlider _borderWidthSlider;
        private Label _borderWidthValue;
        private HSlider _borderWidthScaleSlider;
        private Label _borderWidthScaleValue;
        private HSlider _cornerRadiusSlider;
        private Label _cornerRadiusValue;
        private HSlider _bgOpacitySlider;
        private Label _bgOpacityValue;
        #endregion

        #region Node References - Player Tab - Text Style
        private OptionButton _fontOption;
        private Button _loadFontBtn;
        private HSlider _fontSizeSlider;
        private Label _fontSizeValue;
        private HSlider _lineSpacingSlider;
        private Label _lineSpacingValue;
        private HSlider _letterSpacingSlider;
        private Label _letterSpacingValue;
        private Button _alignLeftBtn;
        private Button _alignCenterBtn;
        private Button _alignRightBtn;
        #endregion

        #region Node References - Player Tab - Visual Effects
        private List<Button> _lineColorButtons = new List<Button>();
        private CheckButton _boldCheck;
        private CheckButton _italicCheck;
        private CheckButton _shadowCheck;
        #endregion

        #region Node References - Player Tab - Label Controls (4 independent)
        private const int LabelCount = 4;
        private CheckButton[] _labelVisibleChecks = new CheckButton[LabelCount];
        private LineEdit[] _labelNameEdits = new LineEdit[LabelCount];
        private LineEdit[] _labelTextEdits = new LineEdit[LabelCount];
        private HSlider[] _labelFontSizeSliders = new HSlider[LabelCount];
        private Label[] _labelFontSizeValues = new Label[LabelCount];
        private Button[] _labelColorButtons = new Button[LabelCount];
        private HSlider[] _labelOffsetXSliders = new HSlider[LabelCount];
        private HSlider[] _labelOffsetYSliders = new HSlider[LabelCount];
        private Label[] _labelOffsetXValues = new Label[LabelCount];
        private Label[] _labelOffsetYValues = new Label[LabelCount];
        private Button[] _labelResetButtons = new Button[LabelCount];
        private CheckButton _labelAutoCenterXCheck;
        #endregion

        #region Dynamic Created Controls
        private CheckButton _freeLookCheck;
        private HSlider _lineWidthScaleSlider;
        private Label _lineWidthScaleValue;
        private SpinBox _refZoomASpin;
        private SpinBox _refWidthASpin;
        private SpinBox _refZoomBSpin;
        private SpinBox _refWidthBSpin;
        private CheckButton _applyCalibrationBtn;
        private CheckButton _responsiveCheck;
        private SpinBox _visibleGridsXSpin;
        private CheckButton _fontAutoSizeCheck;
        #endregion

        #region Preset System Controls
        private OptionButton _presetOption;
        private Button _savePresetBtn;
        private Button _deletePresetBtn;
        private LineEdit _presetNameEdit;
        #endregion

        #region Node References - Health Bar Controls
        private CheckButton _healthBarVisibleCheck;
        private HSlider _healthBarLengthSlider;
        private Label _healthBarLengthValue;
        private HSlider _healthBarLengthScaleSlider;
        private Label _healthBarLengthScaleValue;
        private HSlider _healthBarHeightSlider;
        private Label _healthBarHeightValue;
        private HSlider _healthBarHeightScaleSlider;
        private Label _healthBarHeightScaleValue;
        private HSlider _healthBarFillSlider;
        private Label _healthBarFillValue;
        private Button _healthBarColorBtn;
        private HSlider _healthBarOffsetXSlider;
        private HSlider _healthBarOffsetYSlider;
        private Label _healthBarOffsetXValue;
        private Label _healthBarOffsetYValue;
        #endregion

        #region Node References - Cast Bar Controls
        private CheckButton _castBarVisibleCheck;
        private HSlider _castBarLengthSlider;
        private Label _castBarLengthValue;
        private HSlider _castBarHeightSlider;
        private Label _castBarHeightValue;
        private HSlider _castBarFillSlider;
        private Label _castBarFillValue;
        private Button _castBarColorBtn;
        private HSlider _castBarOffsetXSlider;
        private HSlider _castBarOffsetYSlider;
        private Label _castBarOffsetXValue;
        private Label _castBarOffsetYValue;
        #endregion

        #region Node References - Level Badge Controls
        private CheckButton _levelBadgeVisibleCheck;
        private HSlider _levelBadgeFontSizeSlider;
        private Label _levelBadgeFontSizeValue;
        private Button _levelBadgeTextColorBtn;
        private LineEdit _levelBadgeTextEdit;
        private HSlider _levelBadgeOffsetXSlider;
        private HSlider _levelBadgeOffsetYSlider;
        private Label _levelBadgeOffsetXValue;
        private Label _levelBadgeOffsetYValue;
        #endregion

        #region Editor Key Config Controls
        private OptionButton _editorDragButtonOption;
        private CheckButton _editorSelectModCheck;
        #endregion

        #region Monster Tab Controls
        private HSlider _monsterSizeSlider;
        private Label _monsterSizeValue;
        private HSlider _monsterSizeScaleSlider;
        private Label _monsterSizeScaleValue;
        private HSlider _monsterBorderWidthSlider;
        private Label _monsterBorderWidthValue;
        private HSlider _monsterBorderWidthScaleSlider;
        private Label _monsterBorderWidthScaleValue;
        private HSlider _monsterCornerRadiusSlider;
        private Label _monsterCornerRadiusValue;
        private HSlider _monsterBgOpacitySlider;
        private Label _monsterBgOpacityValue;
        private HSlider _monsterFontSizeSlider;
        private Label _monsterFontSizeValue;

        private ColorPickerButton _monsterBorderColorPicker;
        private ColorPickerButton _monsterBgColorPicker;
        private ColorPickerButton _monsterTextColorPicker;
        private LineEdit[] _monsterLabelEdits = new LineEdit[4];
        private HSlider[] _monsterLabelFontSizeSliders = new HSlider[4];
        private Label[] _monsterLabelFontSizeValues = new Label[4];
        private HSlider[] _monsterLabelXOffsetSliders = new HSlider[4];
        private Label[] _monsterLabelXOffsetValues = new Label[4];
        private HSlider[] _monsterLabelYOffsetSliders = new HSlider[4];
        private Label[] _monsterLabelYOffsetValues = new Label[4];
        private CheckButton[] _monsterLabelCenterXChecks = new CheckButton[4];
        #endregion

        #region Other References
        private FileDialog _fontFileDialog;
        #endregion

        #region Undo System State
        private bool _isRestoring = false;
        #endregion

        #region State Variables
        private Node2D _gridManager;
        private Node2D _player;
        private Camera2D _camera;
        private bool _isPanelVisible = false;
        private bool _isPanelFocused = false;
        private bool _calibrationEnabled = false;
        private List<Godot.Collections.Dictionary> _configHistory = new List<Godot.Collections.Dictionary>();
        private int _historyIndex = -1;
        private bool _isZoomSliderDragging = false;
        #endregion

        #region Godot Lifecycle Methods
        public override async void _Ready()
        {
            GD.Print("[DebugPanel] _Ready() starting...");
            AddToGroup("debug_panel");

            InitializeNodeReferences();

            GD.Print($"[DebugPanel] Nodes initialized: _toggleButton={_toggleButton != null}, _panel={_panel != null}");

            if (_toggleButton == null)
                GD.Print("[DebugPanel] Button not found in scene, will create in SetupPanel");

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            _gridManager = GetTree().GetFirstNodeInGroup("grid_manager") as Node2D;
            _player = GetTree().GetFirstNodeInGroup("player") as Node2D;
            _camera = GetTree().GetFirstNodeInGroup("camera") as Camera2D;

            SetupPanel();
            SetupSliders();

            CreateMapDebugUI();
            CreatePlayerDebugUI();
            CreateMonsterDebugUI();

            SetupFontOptions();
            SetupEaseOptions();

            CreatePresetUI();
            SetupLineColorButtons();
            ConnectSignals();

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            _player = GetTree().GetFirstNodeInGroup("player") as Node2D;
            GD.Print($"[DebugPanel] _Ready() player reference: {_player}");

            LoadConfig();

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            if (_gridManager != null && (_responsiveCheck == null || !_responsiveCheck.ButtonPressed))
            {
                int gridSize = (int)_gridSizeSlider.Value;
                _gridManager.Call("SetGridSize", gridSize);
                GD.Print($"[DebugPanel] Re-applied grid size: {gridSize}");
            }

            ApplyLoadedPlayerSettings();

            // 从 NetworkManager 应用服务器角色数据
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm?.CachedRoleInfo != null && _player is Player playerObj)
            {
                playerObj.ApplyRoleInfo(nm.CachedRoleInfo);
                GD.Print("[DebugPanel] Applied server role info to player");
            }

            UpdateControlStates();
            PushCurrentStateToHistory();
            _panel.Visible = false;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey keyEvent && keyEvent.Pressed)
            {
                if (keyEvent.Keycode == Key.F1)
                {
                    OnTogglePressed();
                    GetViewport().SetInputAsHandled();
                }
                else if (keyEvent.Keycode == Key.Z && keyEvent.CtrlPressed)
                {
                    UndoLastChange();
                    GetViewport().SetInputAsHandled();
                }
            }
        }

        public override void _Input(InputEvent @event)
        {
            // Handle panel edge resize (takes priority)
            if (HandleResizeInput(@event))
                return;

            if (!_isPanelVisible)
            {
                _isPanelFocused = false;
                return;
            }

            if (@event is InputEventMouseButton || @event is InputEventMouseMotion)
            {
                Vector2 mousePos = GetViewport().GetMousePosition();
                Rect2 panelRect = _panel.GetGlobalRect();
                Rect2 toggleRect = _toggleButton.GetGlobalRect();

                bool onScrollbar = false;
                if (_scrollContainer != null)
                {
                    VScrollBar vScrollbar = _scrollContainer.GetVScrollBar();
                    if (vScrollbar != null && vScrollbar.GetGlobalRect().HasPoint(mousePos))
                        onScrollbar = true;
                }

                if (panelRect.HasPoint(mousePos) || toggleRect.HasPoint(mousePos) || onScrollbar)
                {
                    _isPanelFocused = true;
                    GetViewport().SetInputAsHandled();
                }
                else
                {
                    _isPanelFocused = false;
                }
            }
        }

        public override void _Process(double delta)
        {
            ProcessResizeCursor();

            if (_camera != null && _zoomSlider != null)
            {
                float cameraZoom = _camera.Zoom.X;
                if (Mathf.Abs(cameraZoom - (float)_zoomSlider.Value) > 0.01f && !_isZoomSliderDragging)
                {
                    _zoomSlider.Value = cameraZoom;
                    _zoomValue.Text = $"{cameraZoom:F1}";
                }
            }
        }
        #endregion

        #region Panel Toggle
        public void HidePanel()
        {
            _isPanelVisible = false;
            _panel.Visible = false;
            _toggleButton.Text = "⚙";
        }

        private void OnTogglePressed()
        {
            _isPanelVisible = !_panel.Visible;
            _panel.Visible = _isPanelVisible;
            _toggleButton.Text = _isPanelVisible ? "✕" : "⚙";

            if (_isPanelVisible)
            {
                SyncSlidersToCurrentValues();
                SyncMonsterDebugUI();
            }
        }

        public bool IsFocused()
        {
            return _isPanelFocused && _isPanelVisible;
        }

        /// <summary>
        /// 供 CameraController 调用：判断鼠标是否在调试面板上
        /// </summary>
        public bool IsMouseOverPanel()
        {
            // 正在调整大小时，始终视为在面板上（防止摄像机干扰）
            if (IsResizing) return true;
            if (_panel == null || !_panel.Visible)
                return false;
            Vector2 mousePos = GetViewport().GetMousePosition();
            Rect2 panelRect = _panel.GetGlobalRect();
            Rect2 toggleRect = _toggleButton?.GetGlobalRect() ?? new Rect2();
            if (panelRect.HasPoint(mousePos) || toggleRect.HasPoint(mousePos))
                return true;
            // 检查滚动条区域
            if (_scrollContainer != null)
            {
                VScrollBar vScrollbar = _scrollContainer.GetVScrollBar();
                if (vScrollbar != null && vScrollbar.GetGlobalRect().HasPoint(mousePos))
                    return true;
            }
            return false;
        }
        #endregion
    }
}
