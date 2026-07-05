using Godot;

namespace ClinetCSharp
{
    public partial class DebugPanel
    {
        public override void _Ready()
        {
            MinWidth = 250;
            MinHeight = 200;
            SetToggleKey(Key.F11);
            base._Ready();

            AddToGroup("debug_panel");
            VisibilityChanged += OnPanelVisibilityChanged;
            EntityBase.EntityClicked += OnEntityClicked;

            InitializeNodeReferences();
            CallDeferred(MethodName.DeferredInit);
        }

        private void DeferredInit()
        {
            CacheSceneReferences();
            SetupPanel();
            CreateAndBuildTabs();
            CreatePresetUI();
            CallDeferred(MethodName.DeferredLoadConfig);
        }

        private void CacheSceneReferences()
        {
            _gridManager = GetTree().GetFirstNodeInGroup("grid_manager") as GridManager;
            _player = GetTree().GetFirstNodeInGroup("player") as Player;
            _camera = GetTree().GetFirstNodeInGroup("camera") as CameraController;
            _monsterManager = GetTree()?.GetFirstNodeInGroup("monster_manager") as MonsterManager;
            _npcManager = GetTree()?.GetFirstNodeInGroup("npc_manager") as NpcManager;
        }

        private void CreateAndBuildTabs()
        {
            _mapTab = new DebugPanelMapTab(this);
            _entityTab = new DebugPanelEntityTab(this);
            _systemTab = new DebugPanelSystemTab(this);
            _uiTab = new DebugPanelUITab(this);
            _decorationTab = new DebugPanelDecorationTab(this);
            _tabs = new DebugPanelTab[] { _mapTab, _entityTab, _systemTab, _uiTab, _decorationTab };

            var tabContainer = GetNode<TabContainer>("VBoxContainer/Content/ScrollContainer/TabContainer");
            var mapTab = tabContainer.GetNode<VBoxContainer>("地图");
            var entityTab = tabContainer.GetNode<VBoxContainer>("实体");
            var systemTab = tabContainer.GetNode<VBoxContainer>("系统");
            var uiTab = tabContainer.GetNode<VBoxContainer>("UI");
            var decorationTab = tabContainer.GetNode<VBoxContainer>("建筑");

            _mapTab.BuildUI(mapTab);
            _entityTab.BuildUI(entityTab);
            _systemTab.BuildUI(systemTab);
            _uiTab.BuildUI(uiTab);
            _decorationTab.BuildUI(decorationTab);

            foreach (var tab in _tabs)
                tab.ConnectSignals();
        }

        private void DeferredLoadConfig()
        {
            _player = GetTree().GetFirstNodeInGroup("player") as Player;
            LoadConfig();

            _mapTab?.ApplyInitialGridSize();
            _entityTab?.SyncToCurrentValues();
            EntityProfileManager.Instance?.ApplyAllProfiles();

            var networkManager = UiServices.GetNetworkManager(this);
            if (networkManager?.CachedRoleInfo != null && _player is Player player)
                player.ApplyRoleInfo(networkManager.CachedRoleInfo);

            UpdateControlStates();
            Visible = false;
        }

        public override void _ExitTree()
        {
            if (_tabs != null)
            {
                foreach (var tab in _tabs)
                    tab.DisconnectSignals();
            }

            var panelManager = GetNodeOrNull<PanelManager>("/root/UICanvas/PanelManager");
            panelManager?.UnregisterPanel(this);

            VisibilityChanged -= OnPanelVisibilityChanged;
            EntityBase.EntityClicked -= OnEntityClicked;
            base._ExitTree();
        }

        public override void _Input(InputEvent @event)
        {
            base._Input(@event);

            SliderValueInput.HandleInput(@event);

            if (_activeLineEdit != null &&
                @event is InputEventMouseButton mouseButton &&
                mouseButton.Pressed)
            {
                var editRect = _activeLineEdit.GetGlobalRect();
                if (!editRect.HasPoint(mouseButton.GlobalPosition))
                {
                    _activeLineEditApply?.Invoke();
                    ClearActiveLineEdit();
                }
            }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey keyEvent &&
                keyEvent.Pressed &&
                keyEvent.Keycode == Key.F11 &&
                PanelManager.Instance == null)
            {
                Toggle();
                GetViewport().SetInputAsHandled();
                return;
            }

            // Ctrl+Shift+B：在建筑工坊页签下进入地图编辑器放建筑
            if (@event is InputEventKey keyEvent2 &&
                keyEvent2.Pressed &&
                keyEvent2.Keycode == Key.B &&
                keyEvent2.CtrlPressed &&
                keyEvent2.ShiftPressed &&
                _tabContainer != null &&
                _tabs != null &&
                _tabContainer.CurrentTab >= 0 &&
                _tabContainer.CurrentTab < _tabs.Length &&
                ReferenceEquals(_tabs[_tabContainer.CurrentTab], _decorationTab))
            {
                var mapEditor = GetTree()?.GetFirstNodeInGroup("map_editor") as MapEditor;
                if (mapEditor != null)
                {
                    mapEditor.EnterPlaceDecorationMode();
                    Visible = false;
                    GetViewport().SetInputAsHandled();
                }
            }
        }

        public override void _Process(double delta)
        {
            base._Process(delta);
            _mapTab?.SyncZoomFromCamera();
        }
    }
}
