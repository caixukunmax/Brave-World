using Godot;
using System;
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
    ///
    /// Tab 类（各自独立管理控件、事件、配置）：
    ///   DebugPanelMapTab        — 地图/摄像机/校准/响应式/编辑器键位
    ///   DebugPanelPlayerTab     — 玩家外观/文字/标签/血条/施法条/动作栏/等级徽章
    ///   DebugPanelMonsterTab    — 怪物全局样式/标签/AI配置
    ///   DebugPanelNpcTab        — NPC全局样式/标签
    ///   DebugPanelSystemTab     — 移动系统配置
    ///   DebugPanelUITab         — 技能栏配置
    /// </summary>
    public partial class DebugPanel : DraggablePanel
    {
        #region Constants
        private const int CONFIG_VERSION = 2;
        internal const string CONFIG_PATH = "user://debug_panel_config.cfg";
        private const string PRESET_PATH = "user://debug_panel_presets.cfg";

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
        internal Control _panel;
        internal VBoxContainer _content;
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
        internal bool _calibrationEnabled = false;

        /// <summary>全局输入回调，用于 LineEdit 编辑模式下点击外部取消编辑</summary>
        internal Action<InputEvent>? _inputCallback;
        internal bool _isZoomSliderDragging = false;
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
            MinWidth = 250;
            MinHeight = 200;
            SetToggleKey(Key.F12);
            base._Ready();
            AddToGroup("debug_panel");
            VisibilityChanged += OnPanelVisibilityChanged;

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
            var mapTab = GetNode<VBoxContainer>("VBoxContainer/Content/ScrollContainer/TabContainer/地图");
            var playerTab = GetNode<VBoxContainer>("VBoxContainer/Content/ScrollContainer/TabContainer/玩家");
            var monsterTab = GetNode<VBoxContainer>("VBoxContainer/Content/ScrollContainer/TabContainer/怪物");
            var npcTab = GetNode<VBoxContainer>("VBoxContainer/Content/ScrollContainer/TabContainer/NPC");
            var sysTab = GetNode<VBoxContainer>("VBoxContainer/Content/ScrollContainer/TabContainer/系统");
            var uiTab = GetNode<VBoxContainer>("VBoxContainer/Content/ScrollContainer/TabContainer/UI");

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
            Visible = false;
        }

        public override void _ExitTree()
        {
            if (_tabs != null)
                foreach (var tab in _tabs)
                    tab.DisconnectSignals();

            var pm = GetNodeOrNull<PanelManager>("/root/UICanvas/PanelManager");
            if (pm != null)
                pm.UnregisterPanel(this);

            VisibilityChanged -= OnPanelVisibilityChanged;
            base._ExitTree();
        }

        public override void _Input(InputEvent @event)
        {
            // LineEdit 编辑模式下点击外部取消编辑
            if (_inputCallback != null)
            {
                _inputCallback(@event);
            }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey keyEvent && keyEvent.Pressed)
            {
                if (keyEvent.Keycode == Key.F12)
                {
                    if (PanelManager.Instance == null)
                    {
                        Toggle();
                        GetViewport().SetInputAsHandled();
                    }
                }
            }
        }

        public override void _Process(double delta)
        {
            base._Process(delta);

            // Zoom slider sync — delegate to MapTab
            _mapTab?.SyncZoomFromCamera();
        }
        #endregion

        #region Panel State
        private void OnPanelVisibilityChanged()
        {
            if (!Visible || _tabs == null)
                return;

            foreach (var tab in _tabs)
                tab.SyncToCurrentValues();
        }

        public void HidePanel()
        {
            Visible = false;
        }

        public bool IsFocused()
        {
            return Visible && PanelManager.Instance?.GetFocusedPanel() == this;
        }

        /// <summary>
        /// 供 CameraController 调用：判断鼠标是否在调试面板上
        /// </summary>
        public bool IsMouseOverPanel()
        {
            return IsMouseOver();
        }
        #endregion
    }
}