using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

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
    ///   DebugPanelEntityTab     — 统一实体配置（玩家/怪物/NPC）
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
        internal DebugPanelEntityTab _entityTab;
        internal DebugPanelSystemTab _systemTab;
        internal DebugPanelUITab _uiTab;
        #endregion

        #region State Variables
        internal GridManager _gridManager;
        internal Player _player;
        internal CameraController _camera;
        internal bool _calibrationEnabled = false;

        /// <summary>当前活跃的 LineEdit（用于点击外部取消编辑）</summary>
        private LineEdit _activeLineEdit;
        private Action _activeLineEditApply;
        internal bool _isZoomSliderDragging = false;
        #endregion

        /// <summary>
        /// 设置当前活跃的 LineEdit。替换旧的全局 _inputCallback 方案。
        /// 同一时间只有一个 LineEdit 可以处于编辑状态。
        /// </summary>
        internal void SetActiveLineEdit(LineEdit edit, Action applyAction)
        {
            // 如果已有活跃 LineEdit，先应用它的值
            if (_activeLineEdit != null && _activeLineEdit != edit)
            {
                _activeLineEditApply?.Invoke();
            }
            _activeLineEdit = edit;
            _activeLineEditApply = applyAction;
        }

        internal void ClearActiveLineEdit()
        {
            _activeLineEdit = null;
            _activeLineEditApply = null;
        }

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

            // 订阅实体点击事件
            EntityBase.EntityClicked += OnEntityClicked;

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
            _entityTab = new DebugPanelEntityTab(this);
            _systemTab = new DebugPanelSystemTab(this);
            _uiTab = new DebugPanelUITab(this);
            _tabs = new DebugPanelTab[] { _mapTab, _entityTab, _systemTab, _uiTab };

            // Build UI for each tab — get containers from TabContainer
            var tabContainer = GetNode<TabContainer>("VBoxContainer/Content/ScrollContainer/TabContainer");
            var mapTab = tabContainer.GetNode<VBoxContainer>("地图");
            var entityTab = tabContainer.GetNode<VBoxContainer>("实体");
            var sysTab = tabContainer.GetNode<VBoxContainer>("系统");
            var uiTab = tabContainer.GetNode<VBoxContainer>("UI");

            _mapTab.BuildUI(mapTab);
            _entityTab.BuildUI(entityTab);
            _systemTab.BuildUI(sysTab);
            _uiTab.BuildUI(uiTab);

            // 在 TabContainer 上方插入"隐藏标签"按钮
            CreateToggleLabelsButton(tabContainer);

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

            _entityTab?.SyncToCurrentValues();

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
            EntityBase.EntityClicked -= OnEntityClicked;
            base._ExitTree();
        }

        public override void _Input(InputEvent @event)
        {
            base._Input(@event); // DraggablePanel 拖拽/resize 逻辑

            // LineEdit 编辑模式下点击外部取消编辑
            if (_activeLineEdit != null && @event is InputEventMouseButton mb && mb.Pressed)
            {
                var editRect = _activeLineEdit.GetGlobalRect();
                if (!editRect.HasPoint(mb.GlobalPosition))
                {
                    _activeLineEditApply?.Invoke();
                    ClearActiveLineEdit();
                }
            }
        }

        /// <summary>
        /// 实体被点击时，自动切到 EntityTab 并选中对应 Profile
        /// </summary>
        private void OnEntityClicked(EntityBase entity)
        {
            GD.Print($"[DebugPanel] OnEntityClicked: {entity.GetType().Name}, visible={IsVisibleInTree()}");
            if (!IsVisibleInTree()) return; // 面板不可见时不切

            // 切到 EntityTab（index 1）
            _tabContainer.CurrentTab = 1;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            // 先处理 SliderValueInput 的点击外部关闭
            SliderValueInput.HandleUnhandledInput(@event);

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

        private bool _labelsVisible = true;

        private void CreateToggleLabelsButton(TabContainer tabContainer)
        {
            // 在 TabContainer 的父级（ScrollContainer）里，在 TabContainer 前面插入按钮
            var scroll = tabContainer.GetParent();
            var btn = new Button
            {
                Text = "隐藏标签",
                Name = "ToggleLabelsBtn",
                CustomMinimumSize = new Vector2(0, 24),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                Flat = false,
            };
            btn.AddThemeFontSizeOverride("font_size", 11);

            int tabIndex = tabContainer.GetIndex();
            scroll.AddChild(btn);
            scroll.MoveChild(btn, tabIndex); // 放在 TabContainer 前面

            btn.Pressed += () =>
            {
                _labelsVisible = !_labelsVisible;
                btn.Text = _labelsVisible ? "隐藏标签" : "显示标签";
                foreach (var tab in _tabs)
                    tab.SetLabelsVisible(_labelsVisible);
                // 同步控制实体身上的标签
                EntityBase.GlobalLabelsVisible = _labelsVisible;
                foreach (var entity in GetTree().GetNodesInGroup("monster").Cast<EntityBase>())
                    entity.RefreshLabelVisibility();
                foreach (var entity in GetTree().GetNodesInGroup("npc").Cast<EntityBase>())
                    entity.RefreshLabelVisibility();
                // Player 也更新
                var player = GetTree().GetFirstNodeInGroup("player");
                if (player is EntityBase p)
                    p.RefreshLabelVisibility();
            };
        }

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