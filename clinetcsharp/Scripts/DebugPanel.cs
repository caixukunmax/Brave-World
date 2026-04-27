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
    ///   DebugPanel.Nodes.cs     — 节点初始化
    ///   DebugPanel.Handlers.cs  — 面板级事件处理
    ///   DebugPanel.DynamicUI.cs — 预设 UI
    ///   DebugPanel.Config.cs    — 配置保存/加载/预设操作
    ///   DebugPanel.Undo.cs      — 撤销系统
    ///   DebugPanel.Resize.cs    — 面板边缘调整大小
    ///
    /// Tab 类（各自独立管理控件、事件、配置）：
    ///   DebugPanelMapTab        — 地图/摄像机/校准/响应式/编辑器键位
    ///   DebugPanelPlayerTab     — 玩家外观/文字/标签/血条/施法条/动作栏/等级徽章
    ///   DebugPanelMonsterTab    — 怪物全局样式/标签/AI配置
    ///   DebugPanelNpcTab        — NPC全局样式/标签
    ///   DebugPanelSystemTab     — 移动系统配置
    ///   DebugPanelUITab         — 技能栏配置
    /// </summary>
    public partial class DebugPanel : Control, IPanel
    {
        #region Constants
        private const int CONFIG_VERSION = 2;
        internal const string CONFIG_PATH = "user://debug_panel_config.cfg";
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

        internal static readonly string[] EASE_TYPE_NAMES = new string[]
        {
            "线性", "平滑", "缓出", "缓入", "缓入缓出"
        };

        public static readonly (string name, string path)[] FONTS = new (string, string)[]
        {
            ("思源黑体", "res://assets/fonts/SourceHanSansCN-Bold.otf"),
            ("阿里巴巴普惠体", "res://assets/fonts/Alibaba-PuHuiTi-Bold.otf"),
            ("霞鹜文楷", "res://assets/fonts/LXGW WenKai TC-Bold.ttf"),
        };
        #endregion

        #region Node References - Main Controls
        internal Panel _panel;
        private Control _outerControl;
        private ScrollContainer _scrollContainer;
        private TabContainer _tabContainer;
        internal MonsterManager _monsterManager;
        internal NpcManager _npcManager;
        #endregion

        #region Tab System
        internal DebugPanelTab[] _tabs;
        internal DebugPanelMapTab _mapTab;
        internal DebugPanelPlayerTab _playerTab;
        internal DebugPanelMonsterTab _monsterTab;
        internal DebugPanelNpcTab _npcTab;
        internal DebugPanelSystemTab _systemTab;
        internal DebugPanelUITab _uiTab;
        #endregion

        #region State Variables
        internal GridManager _gridManager;
        internal Player _player;
        internal CameraController _camera;
        private bool _isPanelVisible = false;
        private bool _isPanelFocused = false;
        internal bool _calibrationEnabled = false;
        private List<Godot.Collections.Dictionary> _configHistory = new List<Godot.Collections.Dictionary>();
        private int _historyIndex = -1;
        internal bool _isZoomSliderDragging = false;
        private bool _isDragging = false;
        private Vector2 _dragOffset;
        internal bool _isRestoring = false;
        #endregion

        #region Cross-Tab Reference — set by MapTab.BuildUI
        internal HSlider _gridSizeSlider;
        #endregion

        #region Preset System Controls
        private OptionButton _presetOption;
        private Button _savePresetBtn;
        private Button _deletePresetBtn;
        private LineEdit _presetNameEdit;
        #endregion

        #region Godot Lifecycle Methods
        public override void _Ready()
        {
            AddToGroup("debug_panel");

            InitializeNodeReferences();

            // 延迟初始化，等待其他节点就绪
            CallDeferred(MethodName.DeferredInit);
        }

        private void DeferredInit()
        {
            _gridManager = GetTree().GetFirstNodeInGroup("grid_manager") as GridManager;
            _player = GetTree().GetFirstNodeInGroup("player") as Player;
            _camera = GetTree().GetFirstNodeInGroup("camera") as CameraController;
            _monsterManager = GetTree()?.GetFirstNodeInGroup("monster_manager") as MonsterManager;
            _npcManager = GetTree()?.GetFirstNodeInGroup("npc_manager") as NpcManager;

            SetupPanel();

            // Create tab instances
            _mapTab = new DebugPanelMapTab(this);
            _playerTab = new DebugPanelPlayerTab(this);
            _monsterTab = new DebugPanelMonsterTab(this);
            _npcTab = new DebugPanelNpcTab(this);
            _systemTab = new DebugPanelSystemTab(this);
            _uiTab = new DebugPanelUITab(this);
            _tabs = new DebugPanelTab[] { _mapTab, _playerTab, _monsterTab, _npcTab, _systemTab, _uiTab };

            // Build UI for each tab
            var mapTab = GetNode<VBoxContainer>("Control/Panel/ScrollContainer/TabContainer/地图");
            var playerTab = GetNode<VBoxContainer>("Control/Panel/ScrollContainer/TabContainer/玩家");
            var monsterTab = GetNode<VBoxContainer>("Control/Panel/ScrollContainer/TabContainer/怪物");
            var npcTab = GetNode<VBoxContainer>("Control/Panel/ScrollContainer/TabContainer/NPC");
            var sysTab = GetNode<VBoxContainer>("Control/Panel/ScrollContainer/TabContainer/系统");
            var uiTab = GetNode<VBoxContainer>("Control/Panel/ScrollContainer/TabContainer/UI");

            _mapTab.BuildUI(mapTab);
            _playerTab.BuildUI(playerTab);
            _monsterTab.BuildUI(monsterTab);
            _npcTab.BuildUI(npcTab);
            _systemTab.BuildUI(sysTab);
            _uiTab.BuildUI(uiTab);

            // Connect all tab signals
            foreach (var tab in _tabs)
                tab.ConnectSignals();

            CreatePresetUI();

            CallDeferred(MethodName.DeferredLoadConfig);
        }

        private void DeferredLoadConfig()
        {
            _player = GetTree().GetFirstNodeInGroup("player") as Player;
            LoadConfig();

            // MapTab handles grid size application
            _mapTab?.ApplyInitialGridSize();

            _playerTab?.ApplyLoadedPlayerSettings();

            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm?.CachedRoleInfo != null && _player is Player playerObj)
                playerObj.ApplyRoleInfo(nm.CachedRoleInfo);

            UpdateControlStates();
            PushCurrentStateToHistory();
            _panel.Visible = false;
            if (_outerControl != null)
                _outerControl.MouseFilter = Control.MouseFilterEnum.Ignore;
        }

        public override void _ExitTree()
        {
            if (_tabs != null)
                foreach (var tab in _tabs)
                    tab.DisconnectSignals();

            var pm = GetNodeOrNull<PanelManager>("/root/UICanvas/PanelManager");
            if (pm != null)
                pm.UnregisterPanel(this);
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey keyEvent && keyEvent.Pressed)
            {
                if (keyEvent.Keycode == Key.F12)
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

            // Drag logic
            if (_isPanelVisible && _outerControl != null)
            {
                if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
                {
                    if (mb.Pressed)
                    {
                        // 用 GuiGetHoveredControl 确认鼠标确实在 DebugPanel 上方
                        // 关键：不同 CanvasLayer 的 GUI 系统是独立的，GuiGetHoveredControl()
                        // 只考虑本 CanvasLayer 内的控件。因此还需要检查是否有更高层
                        // CanvasLayer 的面板正在遮挡（通过 IsAnyDragging 全局标志协调）。
                        var hovered = GetViewport().GuiGetHoveredControl();
                        bool mouseOverSelf = hovered != null
                            && (_panel == hovered || _panel.IsAncestorOf(hovered));

                        Vector2 mousePos = GetViewport().GetMousePosition();
                        Rect2 panelRect = _panel.GetGlobalRect();
                        // 顶部区域为拖拽区域（与 ScrollContainer 顶部偏移对齐）
                        Rect2 dragRect = new Rect2(panelRect.Position, new Vector2(panelRect.Size.X, 36));
                        // 如果鼠标悬停在交互控件上（如 TabBar），不启动拖拽，让控件正常响应
                        bool onInteractive = UiUtils.IsInteractiveControl(hovered);
                        if (mouseOverSelf && dragRect.HasPoint(mousePos) && !DraggablePanel.IsAnyDragging && !onInteractive)
                        {
                            _isDragging = true;
                            DraggablePanel.IsAnyDragging = true;
                            _dragOffset = mousePos - _outerControl.Position;
                            GetViewport().SetInputAsHandled();
                            return;
                        }
                    }
                    else if (_isDragging)
                    {
                        _isDragging = false;
                        DraggablePanel.IsAnyDragging = false;
                        GetViewport().SetInputAsHandled();
                        return;
                    }
                }

                if (_isDragging && @event is InputEventMouseMotion motion)
                {
                    Vector2 newPos = motion.GlobalPosition - _dragOffset;
                    // 允许部分移出屏幕，至少保留 40px 可拖回
                    var vpSize = GetViewport().GetVisibleRect().Size;
                    var panelSize = _panel.GetGlobalRect().Size;
                    newPos.X = Mathf.Clamp(newPos.X, -panelSize.X + 40, vpSize.X - 40);
                    newPos.Y = Mathf.Clamp(newPos.Y, -panelSize.Y + 40, vpSize.Y - 40);
                    _outerControl.Position = newPos;
                    GetViewport().SetInputAsHandled();
                    return;
                }
            }

            if (!_isPanelVisible)
            {
                _isPanelFocused = false;
                return;
            }

            if (@event is InputEventMouseButton || @event is InputEventMouseMotion)
            {
                var hovered = GetViewport().GuiGetHoveredControl();

                bool mouseOverPanel = hovered != null && _panel != null
                    && (hovered == _panel || _panel.IsAncestorOf(hovered));

                bool onScrollbar = false;
                if (_scrollContainer != null)
                {
                    VScrollBar vScrollbar = _scrollContainer.GetVScrollBar();
                    if (vScrollbar != null && vScrollbar.GetGlobalRect().HasPoint(GetViewport().GetMousePosition()))
                        onScrollbar = true;
                }

                if (mouseOverPanel || onScrollbar)
                {
                    _isPanelFocused = true;
                    // 交互控件（Button/Slider 等）需要接收事件才能工作，不消费
                    bool isInteractive = UiUtils.IsInteractiveControl(hovered);
                    if (!isInteractive)
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

            // Safety: auto-release drag if mouse button is no longer pressed
            // (in case the release event didn't reach _Input)
            if (_isDragging && !Input.IsMouseButtonPressed(MouseButton.Left))
            {
                _isDragging = false;
                DraggablePanel.IsAnyDragging = false;
            }

            // Zoom slider sync — delegate to MapTab
            _mapTab?.SyncZoomFromCamera();
        }
        #endregion

        #region Panel Toggle
        public void HidePanel()
        {
            _isPanelVisible = false;
            _panel.Visible = false;
            if (_outerControl != null)
                _outerControl.MouseFilter = Control.MouseFilterEnum.Ignore;
        }

        private void OnTogglePressed()
        {
            _isPanelVisible = !_panel.Visible;
            _panel.Visible = _isPanelVisible;

            if (_isPanelVisible)
            {
                if (_outerControl != null)
                    _outerControl.MouseFilter = Control.MouseFilterEnum.Stop;
                foreach (var tab in _tabs)
                    tab.SyncToCurrentValues();
            }
            else
            {
                if (_outerControl != null)
                    _outerControl.MouseFilter = Control.MouseFilterEnum.Ignore;
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
            if (panelRect.HasPoint(mousePos))
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

        #region IPanel Implementation
        bool IPanel.IsVisible() => _isPanelVisible;
        void IPanel.ShowPanel() { if (!_isPanelVisible) OnTogglePressed(); }
        void IPanel.HidePanel() { if (_isPanelVisible) OnTogglePressed(); }
        string IPanel.PanelName => "DebugPanel";
        #endregion
    }
}