using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinetCSharp
{
    /// <summary>
    /// 调试面板 - 整理版本
    /// 功能：游戏调试和配置界面
    /// 结构：按功能分组，支持折叠展开
    /// </summary>
    public partial class DebugPanel : CanvasLayer
    {
        #region Constants
        private const int CONFIG_VERSION = 1;
        private const string CONFIG_PATH = "user://debug_panel_config.cfg";
        private const string PRESET_PATH = "user://debug_panel_presets.cfg";
        private const int MAX_HISTORY_STEPS = 20;

        // 颜色预设
        private static readonly Color[] COLOR_PRESETS = new Color[]
        {
            Colors.Black,
            Colors.White,
            new Color(1, 0, 0, 1),  // 红
            new Color(0, 1, 0, 1),  // 绿
            new Color(0, 0, 1, 1),  // 蓝
            new Color(1, 1, 0, 1),  // 黄
        };

        // 缓动类型名称
        private static readonly string[] EASE_TYPE_NAMES = new string[]
        {
            "线性",
            "平滑",
            "缓出",
            "缓入",
            "缓入缓出"
        };

        // 字体列表（与 FontOption 顺序对应）
        private static readonly string[] FONT_LIST = new string[]
        {
            "res://assets/fonts/SourceHanSansCN-Bold.otf",
            "res://assets/fonts/Alibaba-PuHuiTi-Bold.otf",
            "res://assets/fonts/LXGW WenKai TC-Bold.ttf"
        };

        // 内置字体名称
        private static readonly string[] FONT_NAMES = new string[]
        {
            "思源黑体",
            "阿里巴巴普惠体",
            "霞鹜文楷"
        };
        #endregion

        #region Node References - Main Controls
        private Panel _panel;
        private Button _toggleButton;
        private ScrollContainer _scrollContainer;
        private TabContainer _tabContainer;
        #endregion

        #region Node References - Map Tab - Section Buttons
        private Button _sectionBasicBtn;
        private Button _sectionGridBtn;
        private Button _sectionCameraBtn;
        private Button _sectionDebugBtn;
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

        #region Node References - Player Tab - Section Buttons
        private Button _sectionLookBtn;
        private Button _sectionTextBtn;
        private Button _sectionEffectBtn;
        #endregion

        #region Node References - Player Tab - Appearance
        private HSlider _playerSizeSlider;
        private Label _playerSizeValue;
        private HSlider _borderWidthSlider;
        private Label _borderWidthValue;
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

        #region Editor Key Config Controls
        private OptionButton _editorDragButtonOption;
        private CheckButton _editorSelectModCheck;
        #endregion

        #region Other References
        private FileDialog _fontFileDialog;
        #endregion

        #region Group Containers (for collapsing)
        private Godot.Collections.Dictionary _mapGroups = new Godot.Collections.Dictionary();
        private Godot.Collections.Dictionary _playerGroups = new Godot.Collections.Dictionary();
        private Godot.Collections.Dictionary _sectionStates = new Godot.Collections.Dictionary();
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
            
            // Add to debug_panel group (for camera controller detection)
            AddToGroup("debug_panel");

            // Initialize node references
            InitializeNodeReferences();
            
            GD.Print($"[DebugPanel] Nodes initialized: _toggleButton={_toggleButton != null}, _panel={_panel != null}");

            // 确保按钮存在
            if (_toggleButton == null)
            {
                GD.Print("[DebugPanel] Button not found in scene, will create in SetupPanel");
            }

            // Delay to get node references from groups
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            _gridManager = GetTree().GetFirstNodeInGroup("grid_manager") as Node2D;
            _player = GetTree().GetFirstNodeInGroup("player") as Node2D;
            _camera = GetTree().GetFirstNodeInGroup("camera") as Camera2D;

            // Initialize panel state
            SetupPanel();
            InitSectionSystem();
            SetupSliders();
            SetupLineColorButtons();
            SetupFontOptions();
            SetupEaseOptions();

            // Create dynamic UI
            CreateFreeLookToggle();
            CreateLineWidthScaleSlider();
            CreateLineWidthCalibrationUI();
            CreateResponsiveUI();
            CreateFontAutoSizeToggle();
            CreateEditorKeyConfigUI();

            // Create preset UI
            CreatePresetUI();

            // Connect signals
            ConnectSignals();

            // Wait multiple frames to ensure player is fully initialized
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            // Re-get player reference (ensure initialization is complete)
            _player = GetTree().GetFirstNodeInGroup("player") as Node2D;
            GD.Print($"[DebugPanel] _Ready() player reference: {_player}");

            // Load configuration
            LoadConfig();

            // Delay one frame to ensure player's _ready() has executed, then apply player settings
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            
            // 再次应用格子大小设置（确保 GridManager 已经准备好）
            // 但如果响应式模式已启用，则跳过（响应式模式会自己管理格子大小）
            if (_gridManager != null && (_responsiveCheck == null || !_responsiveCheck.ButtonPressed))
            {
                int gridSize = (int)_gridSizeSlider.Value;
                _gridManager.Call("SetGridSize", gridSize);
                GD.Print($"[DebugPanel] Re-applied grid size: {gridSize}");
            }
            
            ApplyLoadedPlayerSettings();

            // Initialize undo system
            PushCurrentStateToHistory();

            // Initial state
            _panel.Visible = false;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey keyEvent && keyEvent.Pressed)
            {
                // F1 - 切换调试面板
                if (keyEvent.Keycode == Key.F1)
                {
                    OnTogglePressed();
                    GetViewport().SetInputAsHandled();
                }
                // Ctrl+Z - 撤销
                else if (keyEvent.Keycode == Key.Z && keyEvent.CtrlPressed)
                {
                    UndoLastChange();
                    GetViewport().SetInputAsHandled();
                }
            }
        }

        public override void _Input(InputEvent @event)
        {
            if (!_isPanelVisible)
            {
                _isPanelFocused = false;
                return;
            }

            // Check if mouse is within panel or toggle button
            if (@event is InputEventMouseButton || @event is InputEventMouseMotion)
            {
                Vector2 mousePos = GetViewport().GetMousePosition();
                Rect2 panelRect = _panel.GetGlobalRect();
                Rect2 toggleRect = _toggleButton.GetGlobalRect();

                // Check if on scrollbar
                bool onScrollbar = false;
                if (_scrollContainer != null)
                {
                    VScrollBar vScrollbar = _scrollContainer.GetVScrollBar();
                    if (vScrollbar != null && vScrollbar.GetGlobalRect().HasPoint(mousePos))
                    {
                        onScrollbar = true;
                    }
                }

                if (panelRect.HasPoint(mousePos) || toggleRect.HasPoint(mousePos) || onScrollbar)
                {
                    _isPanelFocused = true;
                    // Note: Do not call GetViewport().SetInputAsHandled(),
                    // otherwise buttons and sliders won't receive events
                }
                else
                {
                    _isPanelFocused = false;
                }
            }
        }

        public override void _Process(double delta)
        {
            // Sync camera zoom value to slider (when zooming with scroll wheel)
            if (_camera != null && _zoomSlider != null)
            {
                float cameraZoom = _camera.Zoom.X;
                // If camera zoom differs from slider value and slider is not being dragged, update slider
                if (Mathf.Abs(cameraZoom - (float)_zoomSlider.Value) > 0.01f && !_isZoomSliderDragging)
                {
                    _zoomSlider.Value = cameraZoom;
                    _zoomValue.Text = $"{cameraZoom:F1}";
                }
            }
        }
        #endregion

        #region Initialization Methods
        private void InitializeNodeReferences()
        {
            // Main controls
            _panel = GetNode<Panel>("Control/Panel");
            // 注意：切换按钮现在由外部 TestDebugButton 控制，不再由 DebugPanel 内部管理
            _scrollContainer = GetNode<ScrollContainer>("Control/Panel/ScrollContainer");
            _tabContainer = GetNode<TabContainer>("Control/Panel/ScrollContainer/TabContainer");

            // Map tab - section buttons
            _sectionBasicBtn = GetNode<Button>("Control/Panel/ScrollContainer/TabContainer/地图/SectionBasic");
            _sectionGridBtn = GetNode<Button>("Control/Panel/ScrollContainer/TabContainer/地图/SectionGrid");
            _sectionCameraBtn = GetNode<Button>("Control/Panel/ScrollContainer/TabContainer/地图/SectionCamera");
            _sectionDebugBtn = GetNode<Button>("Control/Panel/ScrollContainer/TabContainer/地图/SectionDebug");

            // Map tab - basic settings
            _gridSizeSlider = GetNode<HSlider>("Control/Panel/ScrollContainer/TabContainer/地图/BasicGroup/GridSizeSlider");
            _gridSizeValue = GetNode<Label>("Control/Panel/ScrollContainer/TabContainer/地图/BasicGroup/GridSizeRow/ValueLabel");
            _zoomSlider = GetNode<HSlider>("Control/Panel/ScrollContainer/TabContainer/地图/BasicGroup/ZoomSlider");
            _zoomValue = GetNode<Label>("Control/Panel/ScrollContainer/TabContainer/地图/BasicGroup/ZoomRow/ValueLabel");

            // Map tab - grid line settings
            _gridLineWidthSlider = GetNode<HSlider>("Control/Panel/ScrollContainer/TabContainer/地图/GridGroup/GridLineWidthSlider");
            _gridLineWidthValue = GetNode<Label>("Control/Panel/ScrollContainer/TabContainer/地图/GridGroup/GridLineWidthRow/ValueLabel");
            _gridLineBrightnessSlider = GetNode<HSlider>("Control/Panel/ScrollContainer/TabContainer/地图/GridGroup/GridLineBrightnessSlider");
            _gridLineBrightnessValue = GetNode<Label>("Control/Panel/ScrollContainer/TabContainer/地图/GridGroup/GridLineBrightnessRow/ValueLabel");
            _gridCoordsCheck = GetNode<CheckButton>("Control/Panel/ScrollContainer/TabContainer/地图/GridGroup/GridCoordsRow/GridCoordsCheck");

            // Map tab - camera settings
            _cameraReturnDelaySlider = GetNode<HSlider>("Control/Panel/ScrollContainer/TabContainer/地图/CameraGroup/CameraReturnDelaySlider");
            _cameraReturnDelayValue = GetNode<Label>("Control/Panel/ScrollContainer/TabContainer/地图/CameraGroup/CameraReturnDelayRow/ValueLabel");
            _cameraReturnSpeedSlider = GetNode<HSlider>("Control/Panel/ScrollContainer/TabContainer/地图/CameraGroup/CameraReturnSpeedSlider");
            _cameraReturnSpeedValue = GetNode<Label>("Control/Panel/ScrollContainer/TabContainer/地图/CameraGroup/CameraReturnSpeedRow/ValueLabel");
            _cameraEaseTypeOption = GetNode<OptionButton>("Control/Panel/ScrollContainer/TabContainer/地图/CameraGroup/CameraEaseTypeRow/EaseTypeOption");
            _cameraEasePowerSlider = GetNode<HSlider>("Control/Panel/ScrollContainer/TabContainer/地图/CameraGroup/CameraEasePowerSlider");
            _cameraEasePowerValue = GetNode<Label>("Control/Panel/ScrollContainer/TabContainer/地图/CameraGroup/CameraEasePowerRow/ValueLabel");

            // Map tab - debug toggles
            _debugInfoCheck = GetNode<CheckButton>("Control/Panel/ScrollContainer/TabContainer/地图/DebugGroup/DebugInfoRow/DebugInfoCheck");
            _cameraDebugCheck = GetNode<CheckButton>("Control/Panel/ScrollContainer/TabContainer/地图/DebugGroup/CameraDebugRow/CameraDebugCheck");

            // Player tab - section buttons
            _sectionLookBtn = GetNode<Button>("Control/Panel/ScrollContainer/TabContainer/玩家/SectionLook");
            _sectionTextBtn = GetNode<Button>("Control/Panel/ScrollContainer/TabContainer/玩家/SectionText");
            _sectionEffectBtn = GetNode<Button>("Control/Panel/ScrollContainer/TabContainer/玩家/SectionEffect");

            // Player tab - appearance
            _playerSizeSlider = GetNode<HSlider>("Control/Panel/ScrollContainer/TabContainer/玩家/LookGroup/PlayerSizeSlider");
            _playerSizeValue = GetNode<Label>("Control/Panel/ScrollContainer/TabContainer/玩家/LookGroup/PlayerSizeRow/ValueLabel");
            _borderWidthSlider = GetNode<HSlider>("Control/Panel/ScrollContainer/TabContainer/玩家/LookGroup/BorderWidthSlider");
            _borderWidthValue = GetNode<Label>("Control/Panel/ScrollContainer/TabContainer/玩家/LookGroup/BorderRow/ValueLabel");
            _cornerRadiusSlider = GetNode<HSlider>("Control/Panel/ScrollContainer/TabContainer/玩家/LookGroup/CornerRadiusSlider");
            _cornerRadiusValue = GetNode<Label>("Control/Panel/ScrollContainer/TabContainer/玩家/LookGroup/CornerRadiusRow/ValueLabel");
            _bgOpacitySlider = GetNode<HSlider>("Control/Panel/ScrollContainer/TabContainer/玩家/LookGroup/BgOpacitySlider");
            _bgOpacityValue = GetNode<Label>("Control/Panel/ScrollContainer/TabContainer/玩家/LookGroup/BgOpacityRow/ValueLabel");

            // Player tab - text style
            _fontOption = GetNode<OptionButton>("Control/Panel/ScrollContainer/TabContainer/玩家/TextGroup/FontRow/FontOption");
            _loadFontBtn = GetNode<Button>("Control/Panel/ScrollContainer/TabContainer/玩家/TextGroup/FontRow/LoadFontBtn");
            _fontSizeSlider = GetNode<HSlider>("Control/Panel/ScrollContainer/TabContainer/玩家/TextGroup/FontSizeSlider");
            _fontSizeValue = GetNode<Label>("Control/Panel/ScrollContainer/TabContainer/玩家/TextGroup/FontSizeRow/ValueLabel");
            _lineSpacingSlider = GetNode<HSlider>("Control/Panel/ScrollContainer/TabContainer/玩家/TextGroup/LineSpacingSlider");
            _lineSpacingValue = GetNode<Label>("Control/Panel/ScrollContainer/TabContainer/玩家/TextGroup/LineSpacingRow/ValueLabel");
            _letterSpacingSlider = GetNode<HSlider>("Control/Panel/ScrollContainer/TabContainer/玩家/TextGroup/LetterSpacingSlider");
            _letterSpacingValue = GetNode<Label>("Control/Panel/ScrollContainer/TabContainer/玩家/TextGroup/LetterSpacingRow/ValueLabel");
            _alignLeftBtn = GetNode<Button>("Control/Panel/ScrollContainer/TabContainer/玩家/TextGroup/AlignRow/AlignLeft");
            _alignCenterBtn = GetNode<Button>("Control/Panel/ScrollContainer/TabContainer/玩家/TextGroup/AlignRow/AlignCenter");
            _alignRightBtn = GetNode<Button>("Control/Panel/ScrollContainer/TabContainer/玩家/TextGroup/AlignRow/AlignRight");

            // Player tab - visual effects
            _boldCheck = GetNode<CheckButton>("Control/Panel/ScrollContainer/TabContainer/玩家/EffectGroup/FontStyleRow/BoldCheck");
            _italicCheck = GetNode<CheckButton>("Control/Panel/ScrollContainer/TabContainer/玩家/EffectGroup/FontStyleRow/ItalicCheck");
            _shadowCheck = GetNode<CheckButton>("Control/Panel/ScrollContainer/TabContainer/玩家/EffectGroup/FontStyleRow/ShadowCheck");

            // Other references
            _fontFileDialog = GetNode<FileDialog>("Control/FontFileDialog");
        }

        private void SetupPanel()
        {
            GD.Print("[DebugPanel] SetupPanel() starting...");
            
            // Panel initially hidden - 只负责面板本身的初始化
            _panel.Visible = false;
            
            // 按钮现在由外部（TestDebugButton）控制，这里只做面板相关的初始化
            GD.Print("[DebugPanel] Panel initialized, button controlled externally");
        }

        private void InitSectionSystem()
        {
            // Map tab groups
            _mapGroups["SectionBasic"] = GetNode("Control/Panel/ScrollContainer/TabContainer/地图/BasicGroup");
            _mapGroups["SectionGrid"] = GetNode("Control/Panel/ScrollContainer/TabContainer/地图/GridGroup");
            _mapGroups["SectionCamera"] = GetNode("Control/Panel/ScrollContainer/TabContainer/地图/CameraGroup");
            _mapGroups["SectionDebug"] = GetNode("Control/Panel/ScrollContainer/TabContainer/地图/DebugGroup");

            // Player tab groups
            _playerGroups["SectionLook"] = GetNode("Control/Panel/ScrollContainer/TabContainer/玩家/LookGroup");
            _playerGroups["SectionText"] = GetNode("Control/Panel/ScrollContainer/TabContainer/玩家/TextGroup");
            _playerGroups["SectionEffect"] = GetNode("Control/Panel/ScrollContainer/TabContainer/玩家/EffectGroup");

            // Connect section title buttons
            _sectionBasicBtn.Pressed += () => ToggleGroup("SectionBasic", _sectionBasicBtn);
            _sectionGridBtn.Pressed += () => ToggleGroup("SectionGrid", _sectionGridBtn);
            _sectionCameraBtn.Pressed += () => ToggleGroup("SectionCamera", _sectionCameraBtn);
            _sectionDebugBtn.Pressed += () => ToggleGroup("SectionDebug", _sectionDebugBtn);
            _sectionLookBtn.Pressed += () => ToggleGroup("SectionLook", _sectionLookBtn);
            _sectionTextBtn.Pressed += () => ToggleGroup("SectionText", _sectionTextBtn);
            _sectionEffectBtn.Pressed += () => ToggleGroup("SectionEffect", _sectionEffectBtn);

            // Initialize all groups to expanded state
            foreach (var groupName in _mapGroups.Keys)
            {
                _sectionStates[groupName] = true;
            }
            foreach (var groupName in _playerGroups.Keys)
            {
                _sectionStates[groupName] = true;
            }
        }

        private void ToggleGroup(string groupName, Button button)
        {
            Node targetGroup = _mapGroups.ContainsKey(groupName) ? (Node)_mapGroups[groupName] : (_playerGroups.ContainsKey(groupName) ? (Node)_playerGroups[groupName] : null);
            if (targetGroup != null)
            {
                bool isExpanded = _sectionStates.ContainsKey(groupName) ? (bool)_sectionStates[groupName] : true;
                _sectionStates[groupName] = !isExpanded;
                targetGroup.Set("visible", !isExpanded);
                button.Text = (isExpanded ? "▶ " : "▼ ") + button.Text.Substring(2);
            }
        }

        private void SetupSliders()
        {
            // Set slider ranges
            _gridSizeSlider.MinValue = 32;
            _gridSizeSlider.MaxValue = 256;
            _gridSizeSlider.Step = 1;

            _zoomSlider.MinValue = 0.2;  // Consistent with camera controller's min_zoom
            _zoomSlider.MaxValue = 3.0;
            _zoomSlider.Step = 0.1;

            _gridLineWidthSlider.MinValue = 0.1;  // Minimum 0.1, allows thin lines
            _gridLineWidthSlider.MaxValue = 5.0;
            _gridLineWidthSlider.Step = 0.1;

            _gridLineBrightnessSlider.MinValue = 0.1;
            _gridLineBrightnessSlider.MaxValue = 1.0;
            _gridLineBrightnessSlider.Step = 0.1;

            _cameraReturnDelaySlider.MinValue = 0.0;
            _cameraReturnDelaySlider.MaxValue = 3.0;
            _cameraReturnDelaySlider.Step = 0.1;

            _cameraReturnSpeedSlider.MinValue = 1.0;
            _cameraReturnSpeedSlider.MaxValue = 20.0;
            _cameraReturnSpeedSlider.Step = 1.0;

            _cameraEasePowerSlider.MinValue = 1.0;
            _cameraEasePowerSlider.MaxValue = 5.0;
            _cameraEasePowerSlider.Step = 0.1;

            _playerSizeSlider.MinValue = 32;
            _playerSizeSlider.MaxValue = 256;
            _playerSizeSlider.Step = 1;

            _borderWidthSlider.MinValue = 1.0;
            _borderWidthSlider.MaxValue = 10.0;
            _borderWidthSlider.Step = 0.5;

            _cornerRadiusSlider.MinValue = 0.0;
            _cornerRadiusSlider.MaxValue = 30.0;
            _cornerRadiusSlider.Step = 1.0;

            _bgOpacitySlider.MinValue = 0.0;
            _bgOpacitySlider.MaxValue = 1.0;
            _bgOpacitySlider.Step = 0.05;

            _fontSizeSlider.MinValue = 0;
            _fontSizeSlider.MaxValue = 48;
            _fontSizeSlider.Step = 1;

            _lineSpacingSlider.MinValue = 0.5;
            _lineSpacingSlider.MaxValue = 1.5;
            _lineSpacingSlider.Step = 0.1;

            _letterSpacingSlider.MinValue = -5;
            _letterSpacingSlider.MaxValue = 10;
            _letterSpacingSlider.Step = 1;

            // Font alignment buttons
            _alignLeftBtn.Pressed += () => OnTextAlignChanged(HorizontalAlignment.Left);
            _alignCenterBtn.Pressed += () => OnTextAlignChanged(HorizontalAlignment.Center);
            _alignRightBtn.Pressed += () => OnTextAlignChanged(HorizontalAlignment.Right);

            // Load font button
            _loadFontBtn.Pressed += OnLoadFontPressed;
        }

        private void SetupLineColorButtons()
        {
            // Get 4 line color buttons
            _lineColorButtons = new List<Button>
            {
                GetNode<Button>("Control/Panel/ScrollContainer/TabContainer/玩家/EffectGroup/LineColorRow1/ColorButton"),
                GetNode<Button>("Control/Panel/ScrollContainer/TabContainer/玩家/EffectGroup/LineColorRow2/ColorButton"),
                GetNode<Button>("Control/Panel/ScrollContainer/TabContainer/玩家/EffectGroup/LineColorRow3/ColorButton"),
                GetNode<Button>("Control/Panel/ScrollContainer/TabContainer/玩家/EffectGroup/LineColorRow4/ColorButton"),
            };

            // Set initial color and connect signals for each button
            for (int i = 0; i < _lineColorButtons.Count; i++)
            {
                int index = i;  // Capture for closure
                Button btn = _lineColorButtons[i];
                btn.Modulate = COLOR_PRESETS[0];  // Default black
                btn.Pressed += () => OnLineColorButtonPressed(index);
            }
        }

        private void SetupFontOptions()
        {
            _fontOption.Clear();
            foreach (string fontName in FONT_NAMES)
            {
                _fontOption.AddItem(fontName);
            }
            _fontOption.AddItem("自定义...");
        }

        private void SetupEaseOptions()
        {
            _cameraEaseTypeOption.Clear();
            foreach (string easeName in EASE_TYPE_NAMES)
            {
                _cameraEaseTypeOption.AddItem(easeName);
            }
        }
        #endregion

        #region Preset UI Creation
        private void CreatePresetUI()
        {
            // Create main vertical container (bottom button area)
            VBoxContainer mainContainer = new VBoxContainer();
            mainContainer.Name = "PresetContainer";
            mainContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            mainContainer.AddThemeConstantOverride("separation", 8);
            mainContainer.Alignment = BoxContainer.AlignmentMode.Center;

            // Row 1: Global config operations
            HBoxContainer row1 = new HBoxContainer();
            row1.Name = "ConfigRow";
            row1.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row1.AddThemeConstantOverride("separation", 20);
            row1.Alignment = BoxContainer.AlignmentMode.Center;

            // Discard changes button
            Button discardButton = new Button();
            discardButton.Name = "DiscardButton";
            discardButton.Text = "↩ 放弃";
            discardButton.Pressed += OnDiscardChangesPressed;
            row1.AddChild(discardButton);

            // Save config button
            Button saveConfigButton = new Button();
            saveConfigButton.Name = "SaveConfigButton";
            saveConfigButton.Text = "✓ 保存";
            saveConfigButton.Pressed += OnMapSavePressed;
            row1.AddChild(saveConfigButton);

            // Row 2: Preset management (simplified)
            HBoxContainer row2 = new HBoxContainer();
            row2.Name = "PresetRow";
            row2.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row2.AddThemeConstantOverride("separation", 8);
            row2.Alignment = BoxContainer.AlignmentMode.Center;

            // Preset label
            Label presetLabel = CreateLabel("预设:");
            row2.AddChild(presetLabel);

            // Preset selection dropdown
            _presetOption = new OptionButton();
            _presetOption.Name = "PresetOption";
            _presetOption.CustomMinimumSize = new Vector2(100, 0);
            _presetOption.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _presetOption.ItemSelected += OnPresetSelected;
            row2.AddChild(_presetOption);

            // Delete preset button
            _deletePresetBtn = new Button();
            _deletePresetBtn.Name = "DeletePresetBtn";
            _deletePresetBtn.Text = "🗑";
            _deletePresetBtn.TooltipText = "删除选中预设";
            _deletePresetBtn.Pressed += OnDeletePresetPressed;
            row2.AddChild(_deletePresetBtn);

            // Spacer
            Control spacer = new Control();
            spacer.CustomMinimumSize = new Vector2(15, 0);
            row2.AddChild(spacer);

            // New preset button (click to popup input)
            _savePresetBtn = new Button();
            _savePresetBtn.Name = "SavePresetBtn";
            _savePresetBtn.Text = "+ 新建";
            _savePresetBtn.TooltipText = "将当前配置保存为新预设";
            _savePresetBtn.Pressed += OnSavePresetPressed;
            row2.AddChild(_savePresetBtn);

            // Hidden input box (no longer always visible, input via popup)
            _presetNameEdit = new LineEdit();
            _presetNameEdit.Name = "PresetNameEdit";
            _presetNameEdit.Visible = false;
            row2.AddChild(_presetNameEdit);

            // Add rows to main container
            mainContainer.AddChild(row1);
            mainContainer.AddChild(row2);

            // Add container to panel bottom
            _panel.AddChild(mainContainer);

            // Set position and size (increase height for bottom button area)
            mainContainer.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
            mainContainer.OffsetLeft = 10;
            mainContainer.OffsetTop = -80;
            mainContainer.OffsetRight = -10;
            mainContainer.OffsetBottom = -5;

            // Refresh preset list
            RefreshPresetList();
        }

        private void RefreshPresetList()
        {
            if (_presetOption == null)
                return;

            // Save current selection
            int currentIndex = _presetOption.Selected;
            string currentMetadata = "";
            if (currentIndex >= 0 && currentIndex < _presetOption.ItemCount)
            {
                currentMetadata = _presetOption.GetItemMetadata(currentIndex).AsString();
            }

            GD.Print($"[DebugPanel] Refreshing preset list, current selection: {currentIndex} ({currentMetadata})");

            _presetOption.Clear();
            _presetOption.AddItem("默认", 0);
            _presetOption.SetItemMetadata(0, "");

            List<string> presets = GetPresetList();
            int newSelectedIndex = 0;  // Default to "Default"

            for (int i = 0; i < presets.Count; i++)
            {
                _presetOption.AddItem(presets[i], i + 1);
                _presetOption.SetItemMetadata(i + 1, presets[i]);
                // If this was previously selected, remember its new index
                if (presets[i] == currentMetadata)
                {
                    newSelectedIndex = i + 1;
                }
            }

            // Restore selection
            _presetOption.Select(newSelectedIndex);
            GD.Print($"[DebugPanel] Preset list refreshed, selected index: {newSelectedIndex}");
        }
        #endregion

        #region Preset Operations
        private void OnPresetSelected(long index)
        {
            GD.Print($"[DebugPanel] Selected preset index: {index}");

            if (index < 0 || index >= _presetOption.ItemCount)
            {
                GD.PushError($"[DebugPanel] Invalid preset index: {index}");
                return;
            }

            string presetName = _presetOption.GetItemMetadata((int)index).AsString();
            GD.Print($"[DebugPanel] Selected preset name: '{presetName}'");

            if (string.IsNullOrEmpty(presetName))
            {
                // Selected "Default", no need to load
                GD.Print("[DebugPanel] Selected default preset, no need to load");
                return;
            }

            // Load preset (don't refresh list here to avoid resetting selection state)
            if (LoadPreset(presetName))
            {
                GD.Print($"[DebugPanel] Preset '{presetName}' loaded successfully");
            }
            else
            {
                GD.PushError($"[DebugPanel] Failed to load preset '{presetName}'");
            }
        }

        private void OnSavePresetPressed()
        {
            // Create temporary dialog
            AcceptDialog dialog = new AcceptDialog();
            dialog.Title = "新建预设";
            dialog.DialogText = "请输入新预设名称:";

            // Add input box
            LineEdit input = new LineEdit();
            input.Name = "PresetInput";
            input.PlaceholderText = "预设名称";
            input.Text = $"预设_{Time.GetUnixTimeFromSystem()}";
            input.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            input.CustomMinimumSize = new Vector2(200, 0);

            // Add to dialog
            dialog.AddChild(input);

            // Confirm callback
            dialog.Confirmed += () =>
            {
                string presetName = input.Text.StripEdges();
                if (string.IsNullOrEmpty(presetName))
                {
                    presetName = $"预设_{Time.GetUnixTimeFromSystem()}";
                }

                SavePreset(presetName);
                RefreshPresetList();

                // Select the newly saved preset
                for (int i = 0; i < _presetOption.ItemCount; i++)
                {
                    if (_presetOption.GetItemMetadata(i).AsString() == presetName)
                    {
                        _presetOption.Select(i);
                        break;
                    }
                }

                GD.Print($"[DebugPanel] Created new preset: {presetName}");
                dialog.QueueFree();
            };

            // Cleanup on cancel
            dialog.Canceled += () =>
            {
                dialog.QueueFree();
            };

            AddChild(dialog);
            dialog.PopupCentered(new Vector2I(300, 120));
            input.GrabFocus();
            input.SelectAll();
        }

        private void OnDeletePresetPressed()
        {
            int index = _presetOption.Selected;

            // Cannot delete "Default"
            if (index <= 0)
            {
                AcceptDialog warningDialog = new AcceptDialog();
                warningDialog.Title = "提示";
                warningDialog.DialogText = "默认预设无法删除";
                warningDialog.Confirmed += () =>
                {
                    warningDialog.QueueFree();
                };
                AddChild(warningDialog);
                warningDialog.PopupCentered();
                return;
            }

            string presetName = _presetOption.GetItemMetadata(index).AsString();
            if (string.IsNullOrEmpty(presetName))
                return;

            // Show confirmation dialog
            ConfirmationDialog dialog = new ConfirmationDialog();
            dialog.Title = "确认删除";
            dialog.DialogText = $"确定要删除预设 '{presetName}' 吗？";

            dialog.Confirmed += () =>
            {
                if (DeletePreset(presetName))
                {
                    GD.Print($"[DebugPanel] Preset '{presetName}' deleted");
                    RefreshPresetList();
                    _presetOption.Select(0);
                }
                else
                {
                    GD.PushError($"[DebugPanel] Failed to delete preset '{presetName}'");
                }
                dialog.QueueFree();
            };

            dialog.Canceled += () =>
            {
                dialog.QueueFree();
            };

            AddChild(dialog);
            dialog.PopupCentered();
        }

        private void SavePreset(string presetName)
        {
            ConfigFile presets = new ConfigFile();
            Error err = presets.Load(PRESET_PATH);
            if (err != Error.Ok && err != Error.FileNotFound)
            {
                GD.PushError($"Failed to load preset file: {err}");
                return;
            }

            // Save current config to specified preset
            string[] sections = { "meta", "map", "player", "calibration", "responsive", "editor", "sections" };
            ConfigFile currentConfig = new ConfigFile();
            Error currentErr = currentConfig.Load(CONFIG_PATH);

            if (currentErr == Error.Ok)
            {
                foreach (string section in sections)
                {
                    if (currentConfig.HasSection(section))
                    {
                        foreach (string key in currentConfig.GetSectionKeys(section))
                        {
                            Variant value = currentConfig.GetValue(section, key);
                            presets.SetValue(presetName, $"{section}/{key}", value);
                        }
                    }
                }
            }

            presets.SetValue("__presets__", presetName, true);

            err = presets.Save(PRESET_PATH);
            if (err == Error.Ok)
            {
                GD.Print($"[DebugPanel] Preset '{presetName}' saved");
            }
            else
            {
                GD.PushError($"Failed to save preset: {err}");
            }
        }

        private bool LoadPreset(string presetName)
        {
            ConfigFile presets = new ConfigFile();
            Error err = presets.Load(PRESET_PATH);
            if (err != Error.Ok)
            {
                GD.PushWarning("Preset file doesn't exist or cannot be loaded");
                return false;
            }

            if (!presets.HasSection(presetName))
            {
                GD.PushWarning($"Preset '{presetName}' doesn't exist");
                return false;
            }

            ConfigFile config = new ConfigFile();
            foreach (string key in presets.GetSectionKeys(presetName))
            {
                Variant value = presets.GetValue(presetName, key);
                string[] parts = key.Split('/');
                if (parts.Length == 2)
                {
                    config.SetValue(parts[0], parts[1], value);
                }
            }

            // Keep config_version
            config.SetValue("meta", "config_version", CONFIG_VERSION);

            err = config.Save(CONFIG_PATH);
            if (err == Error.Ok)
            {
                GD.Print($"[DebugPanel] Preset '{presetName}' loaded");
                LoadConfig();
                return true;
            }
            else
            {
                GD.PushError($"Failed to apply preset: {err}");
                return false;
            }
        }

        private bool DeletePreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName))
            {
                GD.PushError("[DebugPanel] Failed to delete preset: preset name is empty");
                return false;
            }

            ConfigFile presets = new ConfigFile();
            Error err = presets.Load(PRESET_PATH);
            if (err != Error.Ok && err != Error.FileNotFound)
            {
                GD.PushError($"[DebugPanel] Failed to load preset file: {err}");
                return false;
            }

            if (!presets.HasSection(presetName))
            {
                GD.PushError($"[DebugPanel] Failed to delete preset: preset '{presetName}' doesn't exist");
                return false;
            }

            // Delete preset section
            // Note: ConfigFile has no direct delete section method, implement by not saving that section
            ConfigFile newPresets = new ConfigFile();

            // Copy __presets__ section except the key to delete
            int remainingPresets = 0;
            if (presets.HasSection("__presets__"))
            {
                foreach (string key in presets.GetSectionKeys("__presets__"))
                {
                    if (key != presetName)
                    {
                        newPresets.SetValue("__presets__", key, true);
                        remainingPresets++;
                    }
                }
            }

            GD.Print($"[DebugPanel] Deleting preset '{presetName}', remaining presets: {remainingPresets}");

            // Copy other preset data sections
            foreach (string section in presets.GetSections())
            {
                if (section != presetName && section != "__presets__")
                {
                    foreach (string key in presets.GetSectionKeys(section))
                    {
                        Variant value = presets.GetValue(section, key);
                        newPresets.SetValue(section, key, value);
                    }
                }
            }

            err = newPresets.Save(PRESET_PATH);
            if (err != Error.Ok)
            {
                GD.PushError($"[DebugPanel] Failed to save preset file: {err}");
                return false;
            }

            return true;
        }

        private List<string> GetPresetList()
        {
            ConfigFile presets = new ConfigFile();
            Error err = presets.Load(PRESET_PATH);
            if (err != Error.Ok)
            {
                if (err != Error.FileNotFound)
                {
                    GD.PushError($"[DebugPanel] Failed to read preset list: {err}");
                }
                return new List<string>();
            }

            List<string> list = new List<string>();
            if (presets.HasSection("__presets__"))
            {
                foreach (string key in presets.GetSectionKeys("__presets__"))
                {
                    if (!string.IsNullOrEmpty(key))  // Filter empty strings
                    {
                        list.Add(key);
                    }
                }
            }

            GD.Print($"[DebugPanel] Current preset list: [{string.Join(", ", list)}]");
            return list;
        }
        #endregion

        #region Undo System
        private void PushCurrentStateToHistory()
        {
            if (_isRestoring)
                return;

            Godot.Collections.Dictionary state = CaptureCurrentState();

            // If same as last state, don't add
            if (_configHistory.Count > 0)
            {
                Godot.Collections.Dictionary lastState = _configHistory[_configHistory.Count - 1];
                if (StatesEqual(state, lastState))
                    return;
            }

            _configHistory.Add(state);

            // Limit history size
            if (_configHistory.Count > MAX_HISTORY_STEPS)
            {
                _configHistory.RemoveAt(0);
            }

            _historyIndex = _configHistory.Count - 1;
        }

        private Godot.Collections.Dictionary CaptureCurrentState()
        {
            Godot.Collections.Dictionary state = new Godot.Collections.Dictionary
            {
                ["grid_size"] = _gridSizeSlider.Value,
                ["zoom"] = _zoomSlider.Value,
                ["grid_line_width"] = _gridLineWidthSlider.Value,
                ["grid_line_brightness"] = _gridLineBrightnessSlider.Value,
                ["camera_return_delay"] = _cameraReturnDelaySlider.Value,
                ["camera_return_speed"] = _cameraReturnSpeedSlider.Value,
                ["camera_ease_type"] = _cameraEaseTypeOption.Selected,
                ["camera_ease_power"] = _cameraEasePowerSlider.Value,
                ["player_size"] = _playerSizeSlider.Value,
                ["border_width"] = _borderWidthSlider.Value,
                ["corner_radius"] = _cornerRadiusSlider.Value,
                ["bg_opacity"] = _bgOpacitySlider.Value,
                ["font_size"] = _fontSizeSlider.Value,
                ["line_spacing"] = _lineSpacingSlider.Value,
                ["letter_spacing"] = _letterSpacingSlider.Value,
            };

            if (_lineWidthScaleSlider != null)
                state["line_width_scale"] = _lineWidthScaleSlider.Value;
            if (_fontAutoSizeCheck != null)
                state["font_auto_size"] = _fontAutoSizeCheck.ButtonPressed;
            if (_responsiveCheck != null)
            {
                state["responsive_enabled"] = _responsiveCheck.ButtonPressed;
                state["visible_grids_x"] = _visibleGridsXSpin.Value;
            }
            if (_freeLookCheck != null)
                state["free_look"] = _freeLookCheck.ButtonPressed;

            return state;
        }

        private bool StatesEqual(Godot.Collections.Dictionary state1, Godot.Collections.Dictionary state2)
        {
            foreach (var key in state1.Keys)
            {
                if (!state2.ContainsKey(key) || !state1[key].Equals(state2[key]))
                    return false;
            }
            return true;
        }

        private void UndoLastChange()
        {
            if (_configHistory.Count <= 1)
            {
                GD.Print("[DebugPanel] No actions to undo");
                return;
            }

            // Remove current state
            _configHistory.RemoveAt(_configHistory.Count - 1);

            // Get previous state
            Godot.Collections.Dictionary prevState = _configHistory[_configHistory.Count - 1];

            _isRestoring = true;
            ApplyState(prevState);
            _isRestoring = false;

            GD.Print("[DebugPanel] Undo performed");

            // Show notification
            ShowUndoNotification();
        }

        private void ApplyState(Godot.Collections.Dictionary state)
        {
            // Basic settings
            if (state.ContainsKey("grid_size"))
                _gridSizeSlider.Value = (double)state["grid_size"];
            if (state.ContainsKey("zoom"))
                _zoomSlider.Value = (double)state["zoom"];

            // Grid line settings
            if (state.ContainsKey("grid_line_width"))
                _gridLineWidthSlider.Value = (double)state["grid_line_width"];
            if (state.ContainsKey("grid_line_brightness"))
                _gridLineBrightnessSlider.Value = (double)state["grid_line_brightness"];

            // Camera settings
            if (state.ContainsKey("camera_return_delay"))
                _cameraReturnDelaySlider.Value = (double)state["camera_return_delay"];
            if (state.ContainsKey("camera_return_speed"))
                _cameraReturnSpeedSlider.Value = (double)state["camera_return_speed"];
            if (state.ContainsKey("camera_ease_type"))
                _cameraEaseTypeOption.Select((int)state["camera_ease_type"]);
            if (state.ContainsKey("camera_ease_power"))
                _cameraEasePowerSlider.Value = (double)state["camera_ease_power"];

            // Player appearance
            if (state.ContainsKey("player_size"))
                _playerSizeSlider.Value = (double)state["player_size"];
            if (state.ContainsKey("border_width"))
                _borderWidthSlider.Value = (double)state["border_width"];
            if (state.ContainsKey("corner_radius"))
                _cornerRadiusSlider.Value = (double)state["corner_radius"];
            if (state.ContainsKey("bg_opacity"))
                _bgOpacitySlider.Value = (double)state["bg_opacity"];

            // Text style
            if (state.ContainsKey("font_size"))
                _fontSizeSlider.Value = (double)state["font_size"];
            if (state.ContainsKey("line_spacing"))
                _lineSpacingSlider.Value = (double)state["line_spacing"];
            if (state.ContainsKey("letter_spacing"))
                _letterSpacingSlider.Value = (double)state["letter_spacing"];

            // Dynamic controls
            if (state.ContainsKey("line_width_scale") && _lineWidthScaleSlider != null)
            {
                _lineWidthScaleSlider.SetBlockSignals(true);
                _lineWidthScaleSlider.Value = (double)state["line_width_scale"];
                _lineWidthScaleSlider.SetBlockSignals(false);
            }
            if (state.ContainsKey("font_auto_size") && _fontAutoSizeCheck != null)
                _fontAutoSizeCheck.ButtonPressed = (bool)state["font_auto_size"];
            if (state.ContainsKey("responsive_enabled") && _responsiveCheck != null)
                _responsiveCheck.ButtonPressed = (bool)state["responsive_enabled"];
            if (state.ContainsKey("visible_grids_x") && _visibleGridsXSpin != null)
                _visibleGridsXSpin.Value = (double)state["visible_grids_x"];
            if (state.ContainsKey("free_look") && _freeLookCheck != null)
                _freeLookCheck.ButtonPressed = (bool)state["free_look"];

            // Trigger all updates
            OnGridSizeDragEnded(true);
            OnZoomDragEnded(true);
            OnGridLineWidthDragEnded(true);
            OnGridLineBrightnessDragEnded(true);
            OnCameraReturnDelayDragEnded(true);
            OnCameraReturnSpeedDragEnded(true);
            OnCameraEaseTypeChanged(_cameraEaseTypeOption.Selected);
            OnCameraEasePowerDragEnded(true);
            OnPlayerSizeDragEnded(true);
            OnBorderWidthDragEnded(true);
            OnCornerRadiusDragEnded(true);
            OnBgOpacityDragEnded(true);
            OnFontSizeDragEnded(true);
            OnLineSpacingDragEnded(true);
            OnLetterSpacingDragEnded(true);
        }

        private async void ShowUndoNotification()
        {
            Label label = new Label();
            label.Text = "↩ 已撤销";
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.AddThemeColorOverride("font_color", Colors.Green);
            label.AddThemeFontSizeOverride("font_size", 16);

            CanvasLayer canvasLayer = new CanvasLayer();
            canvasLayer.Layer = 101;
            canvasLayer.AddChild(label);
            AddChild(canvasLayer);

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
            label.Position = new Vector2(
                (viewportSize.X - label.Size.X) / 2,
                viewportSize.Y - 200
            );

            await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
            canvasLayer.QueueFree();
        }
        #endregion

        #region UI Helper Methods
        private Label CreateSectionTitle(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AddThemeColorOverride("font_color", new Color(0.6f, 0.8f, 1.0f));
            label.AddThemeFontSizeOverride("font_size", 14);
            return label;
        }

        private Label CreateLabel(string text, bool expand = false)
        {
            Label label = new Label();
            label.Text = text;
            if (expand)
                label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            return label;
        }

        private SpinBox CreateSpinBox(double minV, double maxV, double step, double value, int width)
        {
            SpinBox spin = new SpinBox();
            spin.MinValue = minV;
            spin.MaxValue = maxV;
            spin.Step = step;
            spin.Value = value;
            spin.CustomMinimumSize = new Vector2(width, 0);
            return spin;
        }

        private HBoxContainer CreateToggleRow(string rowName, string labelText, string checkName)
        {
            HBoxContainer row = new HBoxContainer();
            row.Name = rowName;
            row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label label = new Label();
            label.Text = labelText;
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            CheckButton check = new CheckButton();
            check.Name = checkName;

            row.AddChild(label);
            row.AddChild(check);
            return row;
        }
        #endregion

        #region Signal Connections
        private void ConnectSignals()
        {
            // Basic settings - only apply on drag end
            _gridSizeSlider.ValueChanged += OnGridSizeChanged;
            _gridSizeSlider.DragEnded += OnGridSizeDragEnded;
            _zoomSlider.ValueChanged += OnZoomChanged;
            _zoomSlider.DragEnded += OnZoomDragEnded;
            _zoomSlider.DragStarted += OnZoomDragStarted;

            // Grid line settings - only apply on drag end
            _gridLineWidthSlider.ValueChanged += OnGridLineWidthChanged;
            _gridLineWidthSlider.DragEnded += OnGridLineWidthDragEnded;
            _gridLineBrightnessSlider.ValueChanged += OnGridLineBrightnessChanged;
            _gridLineBrightnessSlider.DragEnded += OnGridLineBrightnessDragEnded;
            _gridCoordsCheck.Toggled += OnGridCoordsToggled;
            if (_lineWidthScaleSlider != null)
            {
                _lineWidthScaleSlider.ValueChanged += OnLineWidthScaleChanged;
                _lineWidthScaleSlider.DragEnded += OnLineWidthScaleDragEnded;
            }

            // Camera settings - only apply on drag end
            _cameraReturnDelaySlider.ValueChanged += OnCameraReturnDelayChanged;
            _cameraReturnDelaySlider.DragEnded += OnCameraReturnDelayDragEnded;
            _cameraReturnSpeedSlider.ValueChanged += OnCameraReturnSpeedChanged;
            _cameraReturnSpeedSlider.DragEnded += OnCameraReturnSpeedDragEnded;
            _cameraEaseTypeOption.ItemSelected += OnCameraEaseTypeChanged;
            _cameraEasePowerSlider.ValueChanged += OnCameraEasePowerChanged;
            _cameraEasePowerSlider.DragEnded += OnCameraEasePowerDragEnded;
            if (_freeLookCheck != null)
                _freeLookCheck.Toggled += OnFreeLookToggled;

            // Debug toggles
            _debugInfoCheck.Toggled += OnDebugInfoToggled;
            _cameraDebugCheck.Toggled += OnCameraDebugToggled;

            // Player basic appearance - only apply on drag end
            _playerSizeSlider.ValueChanged += OnPlayerSizeChanged;
            _playerSizeSlider.DragEnded += OnPlayerSizeDragEnded;
            _borderWidthSlider.ValueChanged += OnBorderWidthChanged;
            _borderWidthSlider.DragEnded += OnBorderWidthDragEnded;
            _cornerRadiusSlider.ValueChanged += OnCornerRadiusChanged;
            _cornerRadiusSlider.DragEnded += OnCornerRadiusDragEnded;
            _bgOpacitySlider.ValueChanged += OnBgOpacityChanged;
            _bgOpacitySlider.DragEnded += OnBgOpacityDragEnded;

            // Text style - only apply on drag end
            _fontOption.ItemSelected += OnFontSelected;
            _fontSizeSlider.ValueChanged += OnFontSizeChanged;
            _fontSizeSlider.DragEnded += OnFontSizeDragEnded;
            _lineSpacingSlider.ValueChanged += OnLineSpacingChanged;
            _lineSpacingSlider.DragEnded += OnLineSpacingDragEnded;
            _letterSpacingSlider.ValueChanged += OnLetterSpacingChanged;
            _letterSpacingSlider.DragEnded += OnLetterSpacingDragEnded;
            _boldCheck.Toggled += OnBoldToggled;
            _italicCheck.Toggled += OnItalicToggled;
            _shadowCheck.Toggled += OnShadowToggled;

            // Font file dialog
            _fontFileDialog.FileSelected += OnFontFileSelected;
        }
        #endregion

        #region Event Handlers - Basic Settings
        private void OnGridSizeChanged(double value)
        {
            _gridSizeValue.Text = ((int)value).ToString();
        }

        private void OnGridSizeDragEnded(bool valueChanged)
        {
            int newGridSize = (int)_gridSizeSlider.Value;
            GD.Print($"[DebugPanel] GridSize changed to: {newGridSize}");
            
            if (_gridManager != null)
            {
                _gridManager.Call("SetGridSize", newGridSize);
                GD.Print($"[DebugPanel] Called GridManager.SetGridSize({newGridSize})");
            }
            else
            {
                GD.PushError("[DebugPanel] _gridManager is null!");
            }
            
            if (_player != null)
            {
                _player.Call("SetGridSize", newGridSize);
                _player.Call("SetVisualSize", newGridSize);
                GD.Print($"[DebugPanel] Called Player.SetGridSize and set_visual_size");
            }
            
            GridUpdate();
            PushCurrentStateToHistory();
        }

        private void OnZoomChanged(double value)
        {
            _zoomValue.Text = $"{value:F1}";
        }

        private void OnZoomDragStarted()
        {
            _isZoomSliderDragging = true;
        }

        private void OnZoomDragEnded(bool valueChanged)
        {
            _isZoomSliderDragging = false;
            if (_camera != null)
            {
                _camera.Zoom = Vector2.One * (float)_zoomSlider.Value;
            }
            GridUpdate();
            PushCurrentStateToHistory();
        }

        private void GridUpdate()
        {
            if (_gridManager != null)
            {
                _gridManager.Call("queue_redraw");
            }
        }

        private void SyncSlidersToCurrentValues()
        {
            // Sync slider display values to current actual values (without triggering apply)
            if (_gridManager != null)
            {
                _gridSizeSlider.SetBlockSignals(true);
                _gridSizeSlider.Value = (int)_gridManager.Get("grid_size");
                bool responsiveMode = (bool)_gridManager.Get("responsive_mode");
                _gridSizeValue.Text = responsiveMode ? $"{_gridManager.Get("grid_size")} (自动)" : _gridManager.Get("grid_size").ToString();
                _gridSizeSlider.SetBlockSignals(false);
            }
        }
        #endregion

        #region Event Handlers - Grid Line Settings
        private void OnGridLineWidthChanged(double value)
        {
            _gridLineWidthValue.Text = $"{value:F1}px";
            if (_calibrationEnabled)
            {
                _gridLineWidthValue.Text += " (自适应)";
            }
        }

        private void OnGridLineWidthDragEnded(bool valueChanged)
        {
            if (_gridManager != null)
            {
                if (_calibrationEnabled)
                {
                    // Preview mode: set preview value
                    _gridManager.Call("SetPreviewLineWidth", (float)_gridLineWidthSlider.Value);
                    GD.Print($"[DebugPanel] Set preview line width: {_gridLineWidthSlider.Value}");
                }
                else
                {
                    // Manual mode: set line_width_scale
                    _gridManager.Call("SetLineWidthScale", (float)_gridLineWidthSlider.Value);
                    GD.Print($"[DebugPanel] Set line width scale: {_gridLineWidthSlider.Value}");
                }
            }
            PushCurrentStateToHistory();
        }

        private void OnGridLineBrightnessDragEnded(bool valueChanged)
        {
            if (_gridManager != null)
            {
                float brightness = (float)_gridLineBrightnessSlider.Value;
                _gridManager.Set("line_color", new Color(brightness, brightness, brightness));
                _gridManager.Call("queue_redraw");
            }
            PushCurrentStateToHistory();
        }

        private void OnGridLineBrightnessChanged(double value)
        {
            _gridLineBrightnessValue.Text = $"{value:F1}";
        }

        private void OnGridCoordsToggled(bool enabled)
        {
            if (_gridManager != null)
            {
                _gridManager.Set("show_grid_coords", enabled);
                _gridManager.Call("queue_redraw");
            }
            PushCurrentStateToHistory();
        }

        private void OnLineWidthScaleChanged(double value)
        {
            if (_gridManager != null)
            {
                _gridManager.Call("set_line_width_scale", value);
                _gridManager.Call("queue_redraw");
            }
        }

        private void OnLineWidthScaleDragEnded(bool valueChanged)
        {
            PushCurrentStateToHistory();
        }
        #endregion

        #region Event Handlers - Camera Settings
        private void OnCameraReturnDelayChanged(double value)
        {
            _cameraReturnDelayValue.Text = $"{value:F1}s";
        }

        private void OnCameraReturnDelayDragEnded(bool valueChanged)
        {
            if (_camera != null && _camera.HasMethod("set_return_delay"))
            {
                _camera.Call("set_return_delay", _cameraReturnDelaySlider.Value);
            }
            PushCurrentStateToHistory();
        }

        private void OnCameraReturnSpeedChanged(double value)
        {
            _cameraReturnSpeedValue.Text = ((int)value).ToString();
        }

        private void OnCameraReturnSpeedDragEnded(bool valueChanged)
        {
            if (_camera != null && _camera.HasMethod("set_return_speed"))
            {
                _camera.Call("set_return_speed", _cameraReturnSpeedSlider.Value);
            }
            PushCurrentStateToHistory();
        }

        private void OnCameraEaseTypeChanged(long index)
        {
            if (_camera != null && _camera.HasMethod("set_ease_type"))
            {
                _camera.Call("set_ease_type", (int)index);
            }
            PushCurrentStateToHistory();
        }

        private void OnCameraEasePowerChanged(double value)
        {
            _cameraEasePowerValue.Text = $"{value:F1}";
        }

        private void OnCameraEasePowerDragEnded(bool valueChanged)
        {
            if (_camera != null && _camera.HasMethod("set_ease_power"))
            {
                _camera.Call("set_ease_power", _cameraEasePowerSlider.Value);
            }
            PushCurrentStateToHistory();
        }

        private void OnFreeLookToggled(bool enabled)
        {
            if (_camera != null && _camera.HasMethod("set_free_look_mode"))
            {
                _camera.Call("set_free_look_mode", enabled);
            }

            // Disable camera return settings in free look mode
            _cameraReturnDelaySlider.Editable = !enabled;
            _cameraReturnSpeedSlider.Editable = !enabled;
            _cameraEaseTypeOption.Disabled = enabled;
            _cameraEasePowerSlider.Editable = !enabled;

            // Zoom slider read-only in free look mode (displays current value)
            _zoomSlider.Editable = !enabled;
            if (enabled)
            {
                _zoomValue.Text = $"{_zoomSlider.Value:F1} (自动)";
            }

            // Update label hints
            if (enabled)
            {
                _cameraReturnDelayValue.Text = "自由视角";
                _cameraReturnSpeedValue.Text = "自由视角";
            }
            else
            {
                _cameraReturnDelayValue.Text = $"{_cameraReturnDelaySlider.Value:F1}s";
                _cameraReturnSpeedValue.Text = ((int)_cameraReturnSpeedSlider.Value).ToString();
            }
        }

        private void OnDebugInfoToggled(bool enabled)
        {
            GD.Print($"[DebugPanel] Debug info toggled: {enabled}");
            if (_player != null)
            {
                _player.Call("SetShowDebugInfo", enabled);
                GD.Print($"[DebugPanel] Called Player.SetShowDebugInfo({enabled})");
            }
            else
            {
                GD.PushError("[DebugPanel] Player is null, cannot toggle debug info");
            }
        }

        private void OnCameraDebugToggled(bool enabled)
        {
            if (_camera != null && _camera.HasMethod("set_debug_drag"))
            {
                _camera.Call("set_debug_drag", enabled);
            }
        }

        private void OnMapSavePressed()
        {
            SaveConfig();
        }

        private void OnDiscardChangesPressed()
        {
            GD.Print("[DebugPanel] Discarding changes, reloading config");
            LoadConfig();
            ShowDiscardNotification();
        }

        private void ShowDiscardNotification()
        {
            GD.Print("[DebugPanel] Restored to last saved state");
        }
        #endregion

        #region Event Handlers - Player Basic Appearance
        private void OnPlayerSizeChanged(double value)
        {
            _playerSizeValue.Text = ((int)value).ToString();
        }

        private void OnPlayerSizeDragEnded(bool valueChanged)
        {
            OnPlayerSizeDragEnded(valueChanged, true);
        }

        private void OnPlayerSizeDragEnded(bool valueChanged, bool pushToHistory)
        {
            if (_player != null)
            {
                _player.Call("SetVisualSize", (int)_playerSizeSlider.Value);
            }
            if (pushToHistory)
                PushCurrentStateToHistory();
        }

        private void OnBorderWidthChanged(double value)
        {
            _borderWidthValue.Text = ((int)value).ToString();
        }

        private void OnBorderWidthDragEnded(bool valueChanged)
        {
            OnBorderWidthDragEnded(valueChanged, true);
        }

        private void OnBorderWidthDragEnded(bool valueChanged, bool pushToHistory)
        {
            if (_player != null)
            {
                _player.Call("SetBorderWidth", _borderWidthSlider.Value);
            }
            if (pushToHistory)
                PushCurrentStateToHistory();
        }

        private void OnCornerRadiusChanged(double value)
        {
            _cornerRadiusValue.Text = ((int)value).ToString();
        }

        private void OnCornerRadiusDragEnded(bool valueChanged)
        {
            OnCornerRadiusDragEnded(valueChanged, true);
        }

        private void OnCornerRadiusDragEnded(bool valueChanged, bool pushToHistory)
        {
            if (_player != null)
            {
                _player.Call("SetCornerRadius", _cornerRadiusSlider.Value);
            }
            if (pushToHistory)
                PushCurrentStateToHistory();
        }

        private void OnBgOpacityChanged(double value)
        {
            _bgOpacityValue.Text = $"{value:F2}";
        }

        private void OnBgOpacityDragEnded(bool valueChanged)
        {
            OnBgOpacityDragEnded(valueChanged, true);
        }

        private void OnBgOpacityDragEnded(bool valueChanged, bool pushToHistory)
        {
            if (_player != null)
            {
                _player.Call("SetBgOpacity", (float)_bgOpacitySlider.Value);
                _player.Call("queue_redraw");
            }
            if (pushToHistory)
                PushCurrentStateToHistory();
        }
        #endregion

        #region Event Handlers - Text Style
        private void OnFontSelected(long index)
        {
            if (index < FONT_LIST.Length)
            {
                // Built-in font
                if (_player != null)
                {
                    _player.Call("SetFont", FONT_LIST[index]);
                    _player.Call("RefreshLabels");
                }
            }
            else
            {
                // Custom font - open file dialog
                _fontFileDialog.PopupCentered();
            }
        }

        private void OnLoadFontPressed()
        {
            _fontFileDialog.PopupCentered();
        }

        private void OnFontFileSelected(string path)
        {
            if (_player != null)
            {
                _player.Call("SetFont", path);
                _player.Call("RefreshLabels");
                GD.Print($"[DebugPanel] Loaded custom font: {path}");
            }
        }

        private void OnFontSizeChanged(double value)
        {
            if (_fontAutoSizeCheck != null && _fontAutoSizeCheck.ButtonPressed)
            {
                _fontSizeValue.Text = "自动";
            }
            else
            {
                _fontSizeValue.Text = ((int)value).ToString();
            }
        }

        private void OnFontSizeDragEnded(bool valueChanged)
        {
            OnFontSizeDragEnded(valueChanged, true);
        }

        private void OnFontSizeDragEnded(bool valueChanged, bool pushToHistory)
        {
            if (_player != null)
            {
                if (_fontAutoSizeCheck != null && _fontAutoSizeCheck.ButtonPressed)
                {
                    _player.Call("SetFontSize", 0);
                }
                else
                {
                    _player.Call("SetFontSize", (int)_fontSizeSlider.Value);
                    _player.Call("RefreshLabels");
                }
            }
            if (pushToHistory)
            {
                PushCurrentStateToHistory();
                _player?.Call("RefreshLabels");
            }
        }

        private void OnLineSpacingChanged(double value)
        {
            _lineSpacingValue.Text = $"{value:F1}";
        }

        private void OnLineSpacingDragEnded(bool valueChanged)
        {
            OnLineSpacingDragEnded(valueChanged, true);
        }

        private void OnLineSpacingDragEnded(bool valueChanged, bool pushToHistory)
        {
            if (_player != null)
            {
                _player.Call("SetLineSpacing", (float)_lineSpacingSlider.Value);
                _player.Call("RefreshLabels");
            }
            if (pushToHistory)
                PushCurrentStateToHistory();
        }

        private void OnLetterSpacingChanged(double value)
        {
            _letterSpacingValue.Text = ((int)value).ToString();
        }

        private void OnLetterSpacingDragEnded(bool valueChanged)
        {
            OnLetterSpacingDragEnded(valueChanged, true);
        }

        private void OnLetterSpacingDragEnded(bool valueChanged, bool pushToHistory)
        {
            if (_player != null)
            {
                _player.Call("SetLetterSpacing", (float)_letterSpacingSlider.Value);
                _player.Call("RefreshLabels");
            }
            if (pushToHistory)
                PushCurrentStateToHistory();
        }

        private void OnTextAlignChanged(HorizontalAlignment alignment)
        {
            if (_player != null)
            {
                _player.Call("SetTextAlignment", (int)alignment);
                _player.Call("RefreshLabels");
            }
            PushCurrentStateToHistory();
        }

        private void OnLineColorButtonPressed(int lineIndex)
        {
            // Cycle through colors
            Godot.Collections.Array<Color> lineColors = (Godot.Collections.Array<Color>)_player.Get("LineColors");
            Color currentColor = lineIndex < lineColors.Count ? lineColors[lineIndex] : Colors.Black;
            int currentIdx = System.Array.IndexOf(COLOR_PRESETS, currentColor);
            int nextIndex = (currentIdx + 1) % COLOR_PRESETS.Length;

            lineColors[lineIndex] = COLOR_PRESETS[nextIndex];
            _player.Set("LineColors", lineColors);
            _lineColorButtons[lineIndex].Modulate = COLOR_PRESETS[nextIndex];
            _player.Call("RefreshLabels");
            PushCurrentStateToHistory();
        }

        private void OnBoldToggled(bool enabled)
        {
            if (_player != null)
            {
                _player.Call("SetFontBold", enabled);
                _player.Call("RefreshLabels");
            }
            PushCurrentStateToHistory();
        }

        private void OnItalicToggled(bool enabled)
        {
            if (_player != null)
            {
                _player.Call("SetFontItalic", enabled);
                _player.Call("RefreshLabels");
            }
            PushCurrentStateToHistory();
        }

        private void OnShadowToggled(bool enabled)
        {
            if (_player != null)
            {
                _player.Call("SetFontShadow", enabled);
                _player.Call("RefreshLabels");
            }
            PushCurrentStateToHistory();
        }
        #endregion

        #region Dynamic UI Creation
        private void CreateFreeLookToggle()
        {
            // Create free look toggle (placed in camera settings group)
            Node cameraGroup = GetNode("Control/Panel/ScrollContainer/TabContainer/地图/CameraGroup");

            HBoxContainer row = new HBoxContainer();
            row.Name = "FreeLookRow";
            row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label label = new Label();
            label.Text = "自由视角";
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            _freeLookCheck = new CheckButton();
            _freeLookCheck.Name = "FreeLookCheck";

            row.AddChild(label);
            row.AddChild(_freeLookCheck);
            cameraGroup.AddChild(row);

            _freeLookCheck.Toggled += OnFreeLookToggled;
        }

        private void CreateLineWidthScaleSlider()
        {
            // Create line width scale slider (placed in grid line settings group)
            Node gridGroup = GetNode("Control/Panel/ScrollContainer/TabContainer/地图/GridGroup");

            _lineWidthScaleSlider = GetNodeOrNull<HSlider>("Control/Panel/ScrollContainer/TabContainer/地图/GridGroup/LineWidthScaleSlider");
            if (_lineWidthScaleSlider != null)
                return;

            _lineWidthScaleSlider = new HSlider();
            _lineWidthScaleSlider.Name = "LineWidthScaleSlider";
            _lineWidthScaleSlider.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _lineWidthScaleSlider.MinValue = 1.0;  // Consistent with grid line width
            _lineWidthScaleSlider.MaxValue = 10.0;
            _lineWidthScaleSlider.Step = 0.5;
            _lineWidthScaleSlider.Value = 2.0;

            HBoxContainer row = new HBoxContainer();
            row.Name = "LineWidthScaleRow";
            row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label label = new Label();
            label.Text = "网格线最大宽度";
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            _lineWidthScaleValue = new Label();
            _lineWidthScaleValue.Name = "LineWidthScaleValue";
            _lineWidthScaleValue.Text = "2.0";
            _lineWidthScaleValue.CustomMinimumSize = new Vector2(50, 0);

            row.AddChild(label);
            row.AddChild(_lineWidthScaleValue);
            gridGroup.AddChild(row);
            gridGroup.AddChild(_lineWidthScaleSlider);
        }

        private void CreateLineWidthCalibrationUI()
        {
            // Create line width adaptive calibration UI
            Node gridGroup = GetNode("Control/Panel/ScrollContainer/TabContainer/地图/GridGroup");

            // Toggle row
            HBoxContainer rowToggle = new HBoxContainer();
            rowToggle.Name = "CalibrationToggleRow";
            rowToggle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label label = new Label();
            label.Text = "线宽自适应校准";
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            _applyCalibrationBtn = new CheckButton();
            _applyCalibrationBtn.Name = "ApplyCalibrationBtn";
            _applyCalibrationBtn.Text = "启用自适应校准";
            _applyCalibrationBtn.Toggled += OnCalibrationToggled;

            rowToggle.AddChild(label);
            rowToggle.AddChild(_applyCalibrationBtn);
            gridGroup.AddChild(rowToggle);

            // Reference point setting UI (initially hidden, shown after enabling)
            CreateCalibrationRefUI(gridGroup);
        }

        private void CreateCalibrationRefUI(Node parent)
        {
            // Reference point A
            HBoxContainer rowA = new HBoxContainer();
            rowA.Name = "RefPointARow";
            rowA.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label labelA = new Label();
            labelA.Text = "参考点A: zoom=";

            _refZoomASpin = CreateSpinBox(0.2, 3.0, 0.1, 0.4, 60);  // Min zoom consistent with camera limit
            _refZoomASpin.ValueChanged += OnCalibrationValueChanged;

            Label labelWidthA = new Label();
            labelWidthA.Text = " 线宽=";

            _refWidthASpin = CreateSpinBox(0.1, 10.0, 0.1, 5.0, 60);
            _refWidthASpin.ValueChanged += OnCalibrationValueChanged;

            rowA.AddChild(labelA);
            rowA.AddChild(_refZoomASpin);
            rowA.AddChild(labelWidthA);
            rowA.AddChild(_refWidthASpin);
            parent.AddChild(rowA);

            // Reference point B
            HBoxContainer rowB = new HBoxContainer();
            rowB.Name = "RefPointBRow";
            rowB.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label labelB = new Label();
            labelB.Text = "参考点B: zoom=";

            _refZoomBSpin = CreateSpinBox(0.2, 3.0, 0.1, 1.0, 60);  // Min zoom consistent with camera limit
            _refZoomBSpin.ValueChanged += OnCalibrationValueChanged;

            Label labelWidthB = new Label();
            labelWidthB.Text = " 线宽=";

            _refWidthBSpin = CreateSpinBox(0.1, 10.0, 0.1, 1.5, 60);
            _refWidthBSpin.ValueChanged += OnCalibrationValueChanged;

            rowB.AddChild(labelB);
            rowB.AddChild(_refZoomBSpin);
            rowB.AddChild(labelWidthB);
            rowB.AddChild(_refWidthBSpin);
            parent.AddChild(rowB);

            // Reference point settings visible by default
            rowA.Set("visible", true);
            rowB.Set("visible", true);
        }

        private void CreateResponsiveUI()
        {
            // Create responsive layout UI
            Node cameraGroup = GetNode("Control/Panel/ScrollContainer/TabContainer/地图/CameraGroup");

            // Toggle row
            HBoxContainer rowToggle = new HBoxContainer();
            rowToggle.Name = "ResponsiveToggleRow";
            rowToggle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label label = new Label();
            label.Text = "响应式布局";
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            _responsiveCheck = new CheckButton();
            _responsiveCheck.Name = "ResponsiveCheck";

            rowToggle.AddChild(label);
            rowToggle.AddChild(_responsiveCheck);
            cameraGroup.AddChild(rowToggle);

            // Horizontal grid count setting
            HBoxContainer rowX = new HBoxContainer();
            rowX.Name = "VisibleGridsXRow";
            rowX.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label labelX = new Label();
            labelX.Text = "横向格子数";
            labelX.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            _visibleGridsXSpin = CreateSpinBox(1.0, 30.0, 0.5, 5.0, 70);
            _visibleGridsXSpin.ValueChanged += OnVisibleGridsChanged;

            rowX.AddChild(labelX);
            rowX.AddChild(_visibleGridsXSpin);
            cameraGroup.AddChild(rowX);

            _responsiveCheck.Toggled += OnResponsiveToggled;
        }

        private void CreateFontAutoSizeToggle()
        {
            // Create font auto size toggle
            // Check if already exists
            _fontAutoSizeCheck = GetNodeOrNull<CheckButton>("Control/Panel/ScrollContainer/TabContainer/玩家/TextGroup/FontAutoSizeRow/FontAutoSizeCheck");
            if (_fontAutoSizeCheck != null)
                return;

            Node textGroup = GetNode("Control/Panel/ScrollContainer/TabContainer/玩家/TextGroup");

            HBoxContainer row = new HBoxContainer();
            row.Name = "FontAutoSizeRow";
            row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label label = new Label();
            label.Text = "自动字号";
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            _fontAutoSizeCheck = new CheckButton();
            _fontAutoSizeCheck.Name = "FontAutoSizeCheck";

            row.AddChild(label);
            row.AddChild(_fontAutoSizeCheck);
            textGroup.AddChild(row);

            _fontAutoSizeCheck.Toggled += OnFontAutoSizeToggled;
        }

        private void CreateEditorKeyConfigUI()
        {
            // Create map editor key config UI
            Node debugGroup = GetNode("Control/Panel/ScrollContainer/TabContainer/地图/DebugGroup");

            // Drag view button selection
            HBoxContainer rowDrag = new HBoxContainer();
            rowDrag.Name = "EditorDragButtonRow";
            rowDrag.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label labelDrag = new Label();
            labelDrag.Text = "拖动视野按键";
            labelDrag.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            _editorDragButtonOption = new OptionButton();
            _editorDragButtonOption.Name = "EditorDragButtonOption";
            _editorDragButtonOption.AddItem("左键");
            _editorDragButtonOption.AddItem("右键");
            _editorDragButtonOption.AddItem("中键");

            rowDrag.AddChild(labelDrag);
            rowDrag.AddChild(_editorDragButtonOption);
            debugGroup.AddChild(rowDrag);

            _editorDragButtonOption.ItemSelected += OnEditorDragButtonChanged;

            // Ctrl+Click select toggle
            HBoxContainer rowSelect = new HBoxContainer();
            rowSelect.Name = "EditorSelectModRow";
            rowSelect.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label labelSelect = new Label();
            labelSelect.Text = "Ctrl+点击选中";
            labelSelect.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            _editorSelectModCheck = new CheckButton();
            _editorSelectModCheck.Name = "EditorSelectModCheck";
            _editorSelectModCheck.ButtonPressed = true;  // Default enabled

            rowSelect.AddChild(labelSelect);
            rowSelect.AddChild(_editorSelectModCheck);
            debugGroup.AddChild(rowSelect);

            _editorSelectModCheck.Toggled += OnEditorSelectModChanged;
        }
        #endregion

        #region Calibration and Responsive Logic
        private void OnCalibrationToggled(bool enabled)
        {
            _calibrationEnabled = enabled;

            if (_gridManager != null)
            {
                _gridManager.Call("SetAdaptiveCalibrationEnabled", enabled);
                GD.Print($"[DebugPanel] Calibration toggled: {enabled}");
            }

            // Reference point setting UI dim/normal (no longer hide)
            Node rowA = GetNodeOrNull("Control/Panel/ScrollContainer/TabContainer/地图/GridGroup/RefPointARow");
            Node rowB = GetNodeOrNull("Control/Panel/ScrollContainer/TabContainer/地图/GridGroup/RefPointBRow");
            if (rowA != null)
                rowA.Set("modulate", enabled ? new Color(1, 1, 1, 1) : new Color(0.6f, 0.6f, 0.6f, 0.6f));
            if (rowB != null)
                rowB.Set("modulate", enabled ? new Color(1, 1, 1, 1) : new Color(0.6f, 0.6f, 0.6f, 0.6f));

            if (enabled)
            {
                // On: immediately apply current reference point values
                ApplyCalibrationValues();
                // Enable calibration zoom range limit
                UpdateCalibrationZoomLimits();
                // Disable max width slider (controlled by algorithm)
                if (_lineWidthScaleSlider != null)
                    _lineWidthScaleSlider.Editable = false;
                // Update label display
                _gridLineWidthValue.Text = $"{_gridLineWidthSlider.Value:F1}px (自适应)";
                // Responsive layout hint
                if (_responsiveCheck != null && _responsiveCheck.ButtonPressed)
                    GD.Print("[DebugPanel] Hint: Responsive layout will change camera zoom, line width calibration reference points may need adjustment");
            }
            else
            {
                // Off: disable calibration zoom range limit
                if (_camera != null)
                {
                    _camera.Call("set_calibration_zoom_limits", false);
                    GD.Print("[DebugPanel] Calibration zoom limit disabled");
                }
                // Use current slider value as fixed value
                if (_lineWidthScaleSlider != null)
                    _lineWidthScaleSlider.Editable = true;
                if (_gridManager != null)
                    _gridManager.Call("SetLineWidthScale", (float)_gridLineWidthSlider.Value);
                _gridLineWidthValue.Text = $"{_gridLineWidthSlider.Value:F1}px";
            }
        }

        private void OnCalibrationValueChanged(double value)
        {
            // If any reference point changes, apply in real-time when switch is on
            if (_calibrationEnabled && _gridManager != null)
            {
                ApplyCalibrationValues();
            }
            // If reference point zoom value changes, update zoom limit
            if (_calibrationEnabled)
            {
                UpdateCalibrationZoomLimits();
            }
        }

        private void ApplyCalibrationValues()
        {
            double zoomA = _refZoomASpin.Value;
            double widthA = _refWidthASpin.Value;
            double zoomB = _refZoomBSpin.Value;
            double widthB = _refWidthBSpin.Value;
            _gridManager.Call("set_line_width_calibration", zoomA, widthA, zoomB, widthB);

            // Update slider display to current zoom corresponding value
            SyncLineWidthToAdaptiveValue();
        }

        private void SyncLineWidthToAdaptiveValue()
        {
            // Calculate corresponding line width based on current camera zoom, update slider position (display only)
            if (_camera != null && _gridManager != null)
            {
                // Can add logic to calculate expected line width based on zoom here
            }
        }

        private void UpdateCalibrationZoomLimits()
        {
            // Set zoom range limit based on reference point A and B zoom values
            if (_camera != null && _refZoomASpin != null && _refZoomBSpin != null)
            {
                double zoomA = _refZoomASpin.Value;
                double zoomB = _refZoomBSpin.Value;
                // Reference point A is usually farther view (smaller zoom), B is closer view (larger zoom)
                double minZoomLimit = Mathf.Min((float)zoomA, (float)zoomB);
                double maxZoomLimit = Mathf.Max((float)zoomA, (float)zoomB);
                _camera.Call("set_calibration_zoom_limits", true, minZoomLimit, maxZoomLimit);
                GD.Print($"[DebugPanel] Calibration zoom limit enabled: zoom [{minZoomLimit:F1} - {maxZoomLimit:F1}]");
            }
        }

        private void OnResponsiveToggled(bool enabled)
        {
            if (_gridManager != null)
            {
                GD.Print($"[DebugPanel] Setting responsive mode: {enabled}, visibleGridsX={_visibleGridsXSpin.Value}");
                _gridManager.Call("set_responsive_mode", enabled);
                if (enabled)
                    _gridManager.Set("visible_grids_x", _visibleGridsXSpin.Value);
            }
            else
            {
                GD.PrintErr("[DebugPanel] Cannot toggle responsive: _gridManager is null");
            }

            // Responsive layout and grid size slider are mutually exclusive
            _gridSizeSlider.Editable = !enabled;
            if (enabled)
                _gridSizeValue.Text = $"{(int)_gridSizeSlider.Value} (自动)";
            else
                _gridSizeValue.Text = ((int)_gridSizeSlider.Value).ToString();

            // Responsive layout and line width calibration can be used simultaneously, no longer auto-disable

            // Note: Responsive layout and line width calibration can be used simultaneously, no longer mutually exclusive
            if (_applyCalibrationBtn != null)
                _applyCalibrationBtn.TooltipText = "启用自适应校准";
        }

        private void OnVisibleGridsChanged(double value)
        {
            if (_gridManager != null && (bool)_gridManager.Get("responsive_mode"))
            {
                _gridManager.Set("visible_grids_x", _visibleGridsXSpin.Value);
                _gridManager.Call("update_responsive_grid_size");
            }
        }

        private void OnEditorDragButtonChanged(long index)
        {
            // 0=Left, 1=Right, 2=Middle
            MouseButton[] buttonMap = new MouseButton[] { MouseButton.Left, MouseButton.Right, MouseButton.Middle };
            MouseButton selectedButton = buttonMap[index];

            // Update camera controller
            Godot.Collections.Array<MouseButton> buttons = new Godot.Collections.Array<MouseButton> { selectedButton };
            if (_camera != null && _camera.HasMethod("set_drag_buttons"))
            {
                _camera.Call("set_drag_buttons", buttons);
            }
            else if (_camera != null)
            {
                _camera.Set("drag_buttons", buttons);
            }

            string[] buttonNames = new string[] { "左键", "右键", "中键" };
            GD.Print($"[DebugPanel] Drag view button changed to: {buttonNames[index]}");
        }

        private void OnEditorSelectModChanged(bool enabled)
        {
            // Notify map editor
            Node mapEditor = GetTree().GetFirstNodeInGroup("map_editor");
            if (mapEditor != null)
            {
                mapEditor.Set("require_ctrl_for_selection", enabled);
            }

            GD.Print($"[DebugPanel] Ctrl+Click select: {enabled}");
        }

        private void OnFontAutoSizeToggled(bool enabled)
        {
            if (enabled)
            {
                _fontSizeValue.Text = "自动";
                if (_player != null)
                {
                    _player.Call("SetFontSize", 0);
                    _player.Call("RefreshLabels");
                }
            }
            else
            {
                _fontSizeValue.Text = ((int)_fontSizeSlider.Value).ToString();
                if (_player != null)
                {
                    _player.Call("SetFontSize", (int)_fontSizeSlider.Value);
                    _player.Call("RefreshLabels");
                }
            }
        }
        #endregion

        #region Config Save/Load
        private void SaveConfig()
        {
            ConfigFile config = new ConfigFile();

            // Metadata
            config.SetValue("meta", "config_version", CONFIG_VERSION);
            config.SetValue("meta", "last_save_time", Time.GetDatetimeStringFromSystem());

            // Map settings
            config.SetValue("map", "grid_size", _gridSizeSlider.Value);
            config.SetValue("map", "zoom", _zoomSlider.Value);
            config.SetValue("map", "grid_line_width", _gridLineWidthSlider.Value);
            config.SetValue("map", "grid_line_brightness", _gridLineBrightnessSlider.Value);
            config.SetValue("map", "show_grid_coords", _gridCoordsCheck.ButtonPressed);

            // Player settings
            config.SetValue("player", "player_size", _playerSizeSlider.Value);
            GD.Print($"[DebugPanel] Saving player size: {_playerSizeSlider.Value}");
            config.SetValue("player", "border_width", _borderWidthSlider.Value);
            config.SetValue("player", "corner_radius", _cornerRadiusSlider.Value);
            config.SetValue("player", "bg_opacity", _bgOpacitySlider.Value);
            config.SetValue("player", "font_size", _fontSizeSlider.Value);
            config.SetValue("player", "line_spacing", _lineSpacingSlider.Value);
            config.SetValue("player", "letter_spacing", _letterSpacingSlider.Value);
            config.SetValue("player", "text_alignment", _player.Get("text_alignment"));
            config.SetValue("player", "font_bold", _boldCheck.ButtonPressed);
            config.SetValue("player", "font_italic", _italicCheck.ButtonPressed);
            config.SetValue("player", "font_shadow", _shadowCheck.ButtonPressed);
            config.SetValue("player", "font_auto_size", _fontAutoSizeCheck?.ButtonPressed ?? false);

            // Camera settings
            config.SetValue("camera", "return_delay", _cameraReturnDelaySlider.Value);
            config.SetValue("camera", "return_speed", _cameraReturnSpeedSlider.Value);
            config.SetValue("camera", "ease_type", _cameraEaseTypeOption.Selected);
            config.SetValue("camera", "ease_power", _cameraEasePowerSlider.Value);
            config.SetValue("camera", "free_look", _freeLookCheck?.ButtonPressed ?? false);

            // Line width calibration settings
            if (_lineWidthScaleSlider != null)
                config.SetValue("map", "line_width_scale", _lineWidthScaleSlider.Value);
            config.SetValue("calibration", "enabled", _calibrationEnabled);
            if (_refZoomASpin != null)
            {
                config.SetValue("calibration", "ref_zoom_a", _refZoomASpin.Value);
                config.SetValue("calibration", "ref_width_a", _refWidthASpin.Value);
                config.SetValue("calibration", "ref_zoom_b", _refZoomBSpin.Value);
                config.SetValue("calibration", "ref_width_b", _refWidthBSpin.Value);
            }

            // Responsive layout settings
            if (_responsiveCheck != null)
            {
                config.SetValue("responsive", "enabled", _responsiveCheck.ButtonPressed);
                config.SetValue("responsive", "visible_grids_x", _visibleGridsXSpin.Value);
            }

            // Editor key settings
            if (_editorDragButtonOption != null)
            {
                config.SetValue("editor", "drag_button", _editorDragButtonOption.Selected);
                config.SetValue("editor", "require_ctrl_for_selection", _editorSelectModCheck?.ButtonPressed ?? true);
            }

            // Debug settings
            config.SetValue("debug", "show_debug_info", _debugInfoCheck.ButtonPressed);

            // Section collapse states
            foreach (var groupName in _sectionStates.Keys)
            {
                config.SetValue("sections", (string)groupName, _sectionStates[groupName]);
            }

            Error err = config.Save(CONFIG_PATH);
            if (err == Error.Ok)
            {
                GD.Print("[DebugPanel] Config saved");
            }
            else
            {
                GD.PushError($"Failed to save config: {err}");
            }
        }

        private async void LoadConfig()
        {
            GD.Print($"[DebugPanel] Starting load config... player={_player}, grid_manager={_gridManager}");

            // If player not ready, delay retry
            if (_player == null)
            {
                GD.Print("[DebugPanel] Player not ready, delaying config load...");
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                _player = GetTree().GetFirstNodeInGroup("player") as Node2D;
                if (_player == null)
                {
                    GD.Print("[DebugPanel] Player still doesn't exist, skipping config load");
                    return;
                }
                GD.Print($"[DebugPanel] Player obtained: {_player}");
            }

            ConfigFile config = new ConfigFile();
            Error err = config.Load(CONFIG_PATH);
            bool configLoaded = (err == Error.Ok);

            if (!configLoaded)
            {
                if (err == Error.FileNotFound)
                {
                    GD.Print("[DebugPanel] Config file doesn't exist, using default settings");
                }
                else
                {
                    GD.PushError($"[DebugPanel] Failed to load config: {err}, using defaults");
                }
                // 使用默认值，不返回，继续执行应用设置
            }
            else
            {
                // Check version
                int savedVersion = (int)config.GetValue("meta", "config_version", 0);
                if (savedVersion < CONFIG_VERSION)
                {
                    GD.Print($"[DebugPanel] Config version upgraded from {savedVersion} to {CONFIG_VERSION}");
                    MigrateConfig(config, savedVersion);
                }

                // Load map settings from config
                double loadedGridSize = (double)config.GetValue("map", "grid_size", 111);
                if (loadedGridSize < 32 || loadedGridSize > 256)
                {
                    GD.PushError($"[DebugPanel] Invalid grid_size in config: {loadedGridSize}, using default 111");
                    loadedGridSize = 111;
                }
                _gridSizeSlider.Value = loadedGridSize;
                _zoomSlider.Value = (double)config.GetValue("map", "zoom", 1.0);
                _gridLineWidthSlider.Value = (double)config.GetValue("map", "grid_line_width", 2.0);
                _gridLineBrightnessSlider.Value = (double)config.GetValue("map", "grid_line_brightness", 0.7);
                _gridCoordsCheck.ButtonPressed = (bool)config.GetValue("map", "show_grid_coords", false);
            }
            
            GD.Print($"[DebugPanel] Final grid_size slider value: {_gridSizeSlider.Value}");

            // Apply map settings
            if (_gridManager != null)
            {
                _gridManager.Call("SetGridSize", (int)_gridSizeSlider.Value);
                _gridManager.Call("SetLineWidthScale", (float)_gridLineWidthSlider.Value);
                float brightness = (float)_gridLineBrightnessSlider.Value;
                _gridManager.Call("SetLineBrightness", brightness);
                _gridManager.Set("show_grid_coords", _gridCoordsCheck.ButtonPressed);
                GD.Print($"[DebugPanel] Applied settings to GridManager: grid_size={_gridSizeSlider.Value}, line_width={_gridLineWidthSlider.Value}");
            }

            if (_camera != null)
            {
                _camera.Zoom = Vector2.One * (float)_zoomSlider.Value;
            }

            // Load player settings (block signals to prevent triggering updates)
            double savedPlayerSize = (double)config.GetValue("player", "player_size", 111);
            GD.Print($"[DebugPanel] Loading player size: {savedPlayerSize}");
            _playerSizeSlider.SetBlockSignals(true);
            _playerSizeSlider.Value = savedPlayerSize;
            _playerSizeSlider.SetBlockSignals(false);

            _borderWidthSlider.SetBlockSignals(true);
            _borderWidthSlider.Value = (double)config.GetValue("player", "border_width", 3.0);
            _borderWidthSlider.SetBlockSignals(false);

            _cornerRadiusSlider.SetBlockSignals(true);
            _cornerRadiusSlider.Value = (double)config.GetValue("player", "corner_radius", 0.0);
            _cornerRadiusSlider.SetBlockSignals(false);

            _bgOpacitySlider.SetBlockSignals(true);
            _bgOpacitySlider.Value = (double)config.GetValue("player", "bg_opacity", 0.1);
            _bgOpacitySlider.SetBlockSignals(false);

            _fontSizeSlider.SetBlockSignals(true);
            _fontSizeSlider.Value = (double)config.GetValue("player", "font_size", 0);
            _fontSizeSlider.SetBlockSignals(false);

            _lineSpacingSlider.SetBlockSignals(true);
            _lineSpacingSlider.Value = (double)config.GetValue("player", "line_spacing", 0.8);
            _lineSpacingSlider.SetBlockSignals(false);

            _letterSpacingSlider.SetBlockSignals(true);
            _letterSpacingSlider.Value = (double)config.GetValue("player", "letter_spacing", 0.0);
            _letterSpacingSlider.SetBlockSignals(false);

            _boldCheck.ButtonPressed = (bool)config.GetValue("player", "font_bold", false);
            _italicCheck.ButtonPressed = (bool)config.GetValue("player", "font_italic", false);
            _shadowCheck.ButtonPressed = (bool)config.GetValue("player", "font_shadow", false);

            if (_fontAutoSizeCheck != null)
            {
                _fontAutoSizeCheck.ButtonPressed = (bool)config.GetValue("player", "font_auto_size", false);
                OnFontAutoSizeToggled(_fontAutoSizeCheck.ButtonPressed);
            }

            if (!(_fontAutoSizeCheck != null && _fontAutoSizeCheck.ButtonPressed))
            {
                if (_player != null)
                {
                    _player.Call("SetFontSize", (int)_fontSizeSlider.Value);
                }
            }

            // Update display labels (force immediate refresh)
            _playerSizeValue.Text = ((int)_playerSizeSlider.Value).ToString();
            _playerSizeValue.QueueRedraw();
            _borderWidthValue.Text = ((int)_borderWidthSlider.Value).ToString();
            _borderWidthValue.QueueRedraw();
            _cornerRadiusValue.Text = ((int)_cornerRadiusSlider.Value).ToString();
            _cornerRadiusValue.QueueRedraw();
            _bgOpacityValue.Text = $"{_bgOpacitySlider.Value:F2}";
            _bgOpacityValue.QueueRedraw();
            _fontSizeValue.Text = ((int)_fontSizeSlider.Value).ToString();
            _fontSizeValue.QueueRedraw();
            _lineSpacingValue.Text = $"{_lineSpacingSlider.Value:F1}";
            _lineSpacingValue.QueueRedraw();
            _letterSpacingValue.Text = ((int)_letterSpacingSlider.Value).ToString();
            _letterSpacingValue.QueueRedraw();

            HorizontalAlignment savedAlignment = (HorizontalAlignment)(int)config.GetValue("player", "text_alignment", (int)HorizontalAlignment.Center);
            GD.Print($"[DebugPanel] Preparing to apply player settings, player exists: {_player != null}");
            if (_player != null)
            {
                _player.Call("SetTextAlignment", (int)savedAlignment);
                _player.Call("SetBorderWidth", (float)_borderWidthSlider.Value);
                _player.Call("SetCornerRadius", (float)_cornerRadiusSlider.Value);
                _player.Call("SetBgOpacity", (float)_bgOpacitySlider.Value);
                _player.Call("SetLineSpacing", (float)_lineSpacingSlider.Value);
                _player.Call("SetLetterSpacing", (float)_letterSpacingSlider.Value);
                _player.Call("SetFontBold", _boldCheck.ButtonPressed);
                _player.Call("SetFontItalic", _italicCheck.ButtonPressed);
                _player.Call("SetFontShadow", _shadowCheck.ButtonPressed);
                // Player settings will be applied in ApplyLoadedPlayerSettings() with delay
                GD.Print("[DebugPanel] Player settings will be applied after delay");
            }

            // Load camera settings
            _cameraReturnDelaySlider.Value = (double)config.GetValue("camera", "return_delay", 0.5);
            _cameraReturnSpeedSlider.Value = (double)config.GetValue("camera", "return_speed", 5.0);
            _cameraEaseTypeOption.Select((int)config.GetValue("camera", "ease_type", 3));
            _cameraEasePowerSlider.Value = (double)config.GetValue("camera", "ease_power", 2.0);

            if (_camera != null)
            {
                if (_camera.HasMethod("set_return_delay"))
                    _camera.Call("set_return_delay", _cameraReturnDelaySlider.Value);
                if (_camera.HasMethod("set_return_speed"))
                    _camera.Call("set_return_speed", _cameraReturnSpeedSlider.Value);
                if (_camera.HasMethod("set_ease_type"))
                    _camera.Call("set_ease_type", _cameraEaseTypeOption.Selected);
                if (_camera.HasMethod("set_ease_power"))
                    _camera.Call("set_ease_power", _cameraEasePowerSlider.Value);
            }

            if (_freeLookCheck != null)
            {
                _freeLookCheck.ButtonPressed = (bool)config.GetValue("camera", "free_look", false);
                OnFreeLookToggled(_freeLookCheck.ButtonPressed);
            }

            // Load line width calibration settings
            if (_lineWidthScaleSlider != null)
            {
                double savedLineWidth = (double)config.GetValue("map", "line_width_scale", 2.0);
                _lineWidthScaleSlider.SetBlockSignals(true);
                _lineWidthScaleSlider.Value = savedLineWidth;
                _lineWidthScaleSlider.SetBlockSignals(false);
            }

            _calibrationEnabled = (bool)config.GetValue("calibration", "enabled", false);
            if (_applyCalibrationBtn != null && _gridManager != null)
            {
                _applyCalibrationBtn.ButtonPressed = _calibrationEnabled;
                _gridManager.Call("SetAdaptiveCalibrationEnabled", _calibrationEnabled);
            }

            if (_refZoomASpin != null)
            {
                _refZoomASpin.Value = (double)config.GetValue("calibration", "ref_zoom_a", 0.2);  // Default consistent with new min_zoom
                _refWidthASpin.Value = (double)config.GetValue("calibration", "ref_width_a", 3.0);  // Adjust default line width
                _refZoomBSpin.Value = (double)config.GetValue("calibration", "ref_zoom_b", 1.0);
                _refWidthBSpin.Value = (double)config.GetValue("calibration", "ref_width_b", 1.5);
                if (_calibrationEnabled)
                    ApplyCalibrationValues();
            }

            // Load responsive layout settings
            if (_responsiveCheck != null && _gridManager != null)
            {
                _responsiveCheck.ButtonPressed = (bool)config.GetValue("responsive", "enabled", false);
                _visibleGridsXSpin.Value = (double)config.GetValue("responsive", "visible_grids_x", 5.0);
                if (_responsiveCheck.ButtonPressed)
                {
                    _gridManager.Set("visible_grids_x", _visibleGridsXSpin.Value);
                    _gridManager.Call("set_responsive_mode", true);
                }
                OnResponsiveToggled(_responsiveCheck.ButtonPressed);
            }

            // Load editor key settings
            if (_editorDragButtonOption != null)
            {
                _editorDragButtonOption.Select((int)config.GetValue("editor", "drag_button", 0));
                OnEditorDragButtonChanged(_editorDragButtonOption.Selected);
            }
            if (_editorSelectModCheck != null)
            {
                _editorSelectModCheck.ButtonPressed = (bool)config.GetValue("editor", "require_ctrl_for_selection", true);
                OnEditorSelectModChanged(_editorSelectModCheck.ButtonPressed);
            }

            // Load debug settings
            _debugInfoCheck.ButtonPressed = (bool)config.GetValue("debug", "show_debug_info", false);
            OnDebugInfoToggled(_debugInfoCheck.ButtonPressed);

            // Load section collapse states
            foreach (var groupName in _sectionStates.Keys)
            {
                bool isExpanded = (bool)config.GetValue("sections", (string)groupName, true);
                _sectionStates[groupName] = isExpanded;
                string mapPath = $"Control/Panel/ScrollContainer/TabContainer/地图/{groupName}";
                string playerPath = $"Control/Panel/ScrollContainer/TabContainer/玩家/{groupName}";
                Button button = _mapGroups.ContainsKey(groupName) ? GetNodeOrNull<Button>(mapPath) : GetNodeOrNull<Button>(playerPath);
                if (button != null)
                {
                    Node targetGroup = _mapGroups.ContainsKey(groupName) ? (Node)_mapGroups[groupName] : (_playerGroups.ContainsKey(groupName) ? (Node)_playerGroups[groupName] : null);
                    if (targetGroup != null)
                    {
                        targetGroup.Set("visible", isExpanded);
                        button.Text = (isExpanded ? "▼ " : "▶ ") + button.Text.Substring(2);
                    }
                }
            }

            GD.Print("[DebugPanel] Using default settings");
        }

        private void MigrateConfig(ConfigFile config, int fromVersion)
        {
            // Config migration: upgrade from old version to current version
            if (fromVersion < 1)
            {
                // v0 -> v1: Initial version, no special migration needed
            }
            // Future version migrations add here
            // if (fromVersion < 2)
            // {
            //     // v1 -> v2 migration logic
            // }
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
                // When panel opens, sync slider display values (but don't reapply settings to avoid resetting player position)
                SyncSlidersToCurrentValues();
            }
        }

        public bool IsFocused()
        {
            return _isPanelFocused && _isPanelVisible;
        }
        #endregion

        #region Apply Loaded Player Settings
        private void ApplyLoadedPlayerSettings()
        {
            GD.Print($"[DebugPanel] ApplyLoadedPlayerSettings() called, player={_player}");
            if (_player == null)
            {
                _player = GetTree().GetFirstNodeInGroup("player") as Node2D;
                GD.Print($"[DebugPanel] Re-acquired player: {_player}");
            }

            if (_player != null)
            {
                ConfigFile config = new ConfigFile();
                Error err = config.Load(CONFIG_PATH);
                if (err == Error.Ok)
                {
                    // Read and apply directly from config
                    double savedSize = (double)config.GetValue("player", "player_size", 111);
                    GD.Print($"[DebugPanel] Read player size from config: {savedSize}");
                    _player.Call("SetVisualSize", (int)savedSize);
                    _player.Call("SetBorderWidth", (float)(double)config.GetValue("player", "border_width", 3.0));
                    _player.Call("SetCornerRadius", (float)(double)config.GetValue("player", "corner_radius", 0.0));
                    _player.Call("SetBgOpacity", (float)(double)config.GetValue("player", "bg_opacity", 0.1));
                    _player.Call("SetLineSpacing", (float)(double)config.GetValue("player", "line_spacing", 0.8));
                    _player.Call("SetLetterSpacing", (float)(double)config.GetValue("player", "letter_spacing", 0.0));
                    _player.Call("SetFontBold", (bool)config.GetValue("player", "font_bold", false));
                    _player.Call("SetFontItalic", (bool)config.GetValue("player", "font_italic", false));
                    _player.Call("SetFontShadow", (bool)config.GetValue("player", "font_shadow", false));
                    _player.Call("SetTextAlignment", (int)(HorizontalAlignment)(int)config.GetValue("player", "text_alignment", (int)HorizontalAlignment.Center));

                    // Apply font size
                    double savedFontSize = (double)config.GetValue("player", "font_size", 0);
                    bool savedAutoSize = (bool)config.GetValue("player", "font_auto_size", false);
                    if (savedAutoSize)
                    {
                        _player.Call("SetFontSize", 0);
                    }
                    else if (savedFontSize > 0)
                    {
                        _player.Call("SetFontSize", (int)savedFontSize);
                    }

                    _player.Call("RefreshLabels");
                    _player.Call("queue_redraw");
                    GD.Print($"[DebugPanel] Player settings applied, visual_size={_player.Get("visual_size")}");
                }
                else
                {
                    GD.Print("[DebugPanel] Cannot read config, skipping player settings apply");
                }
            }
            else
            {
                GD.Print("[DebugPanel] Player is still null, cannot apply settings");
            }
        }
        #endregion
    }
}
