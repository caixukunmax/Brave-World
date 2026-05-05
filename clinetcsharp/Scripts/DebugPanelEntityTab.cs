using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinetCSharp
{
    /// <summary>
    /// DebugPanel Entity Tab ?统一的实体配置面?    /// 替代 DebugPanelPlayerTab + DebugPanelMonsterTab + DebugPanelNpcTab + DebugPanelEntityStyleTabBase
    /// 基于 EntityProfile + ComponentRegistry 的组件化架构
    /// </summary>
    public class DebugPanelEntityTab : DebugPanelTab
    {
        public DebugPanelEntityTab(DebugPanel owner) : base(owner) { }

        public override string TabKey => "entity";

        #region Fields
        private OptionButton _profileOption;
        private Button _addProfileBtn;
        private Button _deleteProfileBtn;
        private LineEdit _profileNameEdit;
        private int _currentProfileId = -1;

        private Button _manageComponentsBtn;
        private AcceptDialog _manageComponentsDialog;
        private Action _manageComponentsHandler;

        private VBoxContainer _componentContainer;
        private readonly Dictionary<string, IEntityTabComponent> _activeComponents = new();
        private readonly Dictionary<string, CollapsibleContainer> _componentContainers = new();

        private AcceptDialog _newProfileDialog;
        private LineEdit _newProfileNameEdit;
        private OptionButton _newProfileTypeOption;
        private ConfirmationDialog _deleteProfileDialog;
        private ConfirmationDialog _deleteComponentDialog;
        private string _pendingDeleteComponentName;

        private bool _isRefreshing = false;
        #endregion

        public override void BuildUI(VBoxContainer tabContainer)
        {
            _tabContainer = tabContainer;
            var title = new Label { Name = "_lbl", Text = "实体配置", HorizontalAlignment = HorizontalAlignment.Center };
            title.Name = "_lbl";
            title.AddThemeFontSizeOverride("font_size", 13);
            tabContainer.AddChild(title);
            tabContainer.AddChild(new HSeparator());
            BuildProfileSelector(tabContainer);
            tabContainer.AddChild(new HSeparator());
            BuildComponentManagement(tabContainer);
            tabContainer.AddChild(new HSeparator());
            _componentContainer = new VBoxContainer { Name = "ComponentContainer", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            tabContainer.AddChild(_componentContainer);
            BuildDialogs();
        }

        private void BuildProfileSelector(Container parent)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(new Label { Name = "_lbl", Text = "配置:", CustomMinimumSize = new Vector2(40, 0) });
            _profileOption = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(100, 0) };
            row.AddChild(_profileOption);
            _addProfileBtn = new Button { Text = "+", CustomMinimumSize = new Vector2(28, 26), TooltipText = "新建 Profile" };
            row.AddChild(_addProfileBtn);
            _deleteProfileBtn = new Button { Text = "🗑", CustomMinimumSize = new Vector2(28, 26), TooltipText = "删除 Profile" };
            row.AddChild(_deleteProfileBtn);
            parent.AddChild(row);

            var nameRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            nameRow.AddChild(new Label { Name = "_lbl", Text = "名称:", CustomMinimumSize = new Vector2(40, 0) });
            _profileNameEdit = new LineEdit { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, PlaceholderText = "Profile 名称" };
            nameRow.AddChild(_profileNameEdit);
            parent.AddChild(nameRow);

            _profileOption.ItemSelected += OnProfileOptionSelected;
            _addProfileBtn.Pressed += OnAddProfilePressed;
            _deleteProfileBtn.Pressed += OnDeleteProfilePressed;
            _profileNameEdit.TextChanged += OnProfileNameChanged;
        }

        private void BuildComponentManagement(Container parent)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(new Label { Name = "_lbl", Text = "组件:", CustomMinimumSize = new Vector2(40, 0) });
            _manageComponentsBtn = new Button { Text = "管理组件", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(_manageComponentsBtn);
            parent.AddChild(row);
            _manageComponentsBtn.Pressed += OnManageComponentsPressed;
        }

        private void BuildDialogs()
        {
            _newProfileDialog = new AcceptDialog { Title = "新建 Profile", DialogText = "输入名称和类型：" };
            var vbox = new VBoxContainer();
            _newProfileDialog.AddChild(vbox);
            var nameRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            nameRow.AddChild(new Label { Name = "_lbl", Text = "名称:", CustomMinimumSize = new Vector2(40, 0) });
            _newProfileNameEdit = new LineEdit { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, PlaceholderText = "新 Profile 名称", Text = "新配置" };
            nameRow.AddChild(_newProfileNameEdit);
            vbox.AddChild(nameRow);
            var typeRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            typeRow.AddChild(new Label { Name = "_lbl", Text = "类型:", CustomMinimumSize = new Vector2(40, 0) });
            _newProfileTypeOption = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _newProfileTypeOption.AddItem("player"); _newProfileTypeOption.AddItem("monster"); _newProfileTypeOption.AddItem("npc");
            typeRow.AddChild(_newProfileTypeOption);
            vbox.AddChild(typeRow);
            _newProfileDialog.Confirmed += OnNewProfileConfirmed;
            Owner.AddChild(_newProfileDialog);

            _deleteProfileDialog = new ConfirmationDialog { Title = "确认删除" };
            _deleteProfileDialog.Confirmed += OnDeleteProfileConfirmed;
            Owner.AddChild(_deleteProfileDialog);

            _deleteComponentDialog = new ConfirmationDialog { Title = "确认删除组件" };
            _deleteComponentDialog.Confirmed += OnDeleteComponentConfirmed;
            _deleteComponentDialog.Canceled += () => { _pendingDeleteComponentName = null; };
            Owner.AddChild(_deleteComponentDialog);

            // 管理组件对话框
            _manageComponentsDialog = new AcceptDialog { Title = "管理组件" };
            Owner.AddChild(_manageComponentsDialog);
        }

        public override void ConnectSignals()
        {
            EntityBase.EntityClicked += OnEntityClicked;
            // TODO: 连接 EntityBase.StyleChanged 信号，当服务端推送属性变更时自动刷新 UI
            // 需要先?EntityBase 中添?[Signal] public delegate void StyleChangedEventHandler();
            // 然后在此处：EntityBase.StyleChanged += OnEntityStyleChanged;
        }

        public override void DisconnectSignals()
        {
            EntityBase.EntityClicked -= OnEntityClicked;
            foreach (var comp in _activeComponents.Values) comp.DisconnectSignals();
        }

        // ══════════════════════════════════════════════════════════════════════?        //  Profile Handlers
        // ══════════════════════════════════════════════════════════════════════?
        private void OnProfileOptionSelected(long index)
        {
            if (index < 0 || index >= _profileOption.GetItemCount()) return;
            int newId = (int)_profileOption.GetItemMetadata((int)index);
            if (newId == _currentProfileId) return;
            SaveCurrentProfileData();
            _currentProfileId = newId;
            RefreshComponents();
            SyncProfileNameEdit();
            }

        private void OnAddProfilePressed()
        {
            _newProfileNameEdit.Text = "新配置";
            _newProfileTypeOption.Select(0);
            _newProfileDialog.PopupCentered(new Vector2I(300, 140));
            _newProfileNameEdit.GrabFocus();
            _newProfileNameEdit.SelectAll();
        }

        private void OnNewProfileConfirmed()
        {
            string name = _newProfileNameEdit.Text.StripEdges();
            if (string.IsNullOrEmpty(name)) name = "新配置";
            string entityType = _newProfileTypeOption.Selected switch { 0 => "player", 1 => "monster", 2 => "npc", _ => "player" };
            var pm = EntityProfileManager.Instance;
            if (pm == null) return;
            SaveCurrentProfileData();
            var profile = pm.CreateProfile(name, entityType);
            foreach (var (cn, _) in ComponentRegistry.GetComponentsForType(entityType))
            {
                var c = ComponentRegistry.Create(cn);
                if (c != null) { profile.SetData(cn, c.SyncToData()); c.Dispose(); }
            }
            _currentProfileId = profile.Id;
            RefreshProfileList(); RefreshComponents(); SyncProfileNameEdit(); pm.ApplyProfileToAll(_currentProfileId);
        }

        private void OnDeleteProfilePressed()
        {
            var pm = EntityProfileManager.Instance;
            if (pm == null) return;
            if (pm.GetAllProfiles().Count() <= 1)
            {
                var w = new AcceptDialog { Title = "提示", DialogText = "至少需要保留一?Profile" };
                w.Confirmed += () => w.QueueFree(); w.Canceled += () => w.QueueFree();
                Owner.AddChild(w); w.PopupCentered(); return;
            }
            var p = pm.GetProfile(_currentProfileId);
            if (p == null) return;
            _deleteProfileDialog.DialogText = $"确定要删?Profile '{p.Name}' (ID: {p.Id}) 吗？";
            _deleteProfileDialog.PopupCentered();
        }

        private void OnDeleteProfileConfirmed()
        {
            var pm = EntityProfileManager.Instance;
            if (pm == null) return;
            pm.DeleteProfile(_currentProfileId);
            _currentProfileId = pm.GetAllProfiles().FirstOrDefault()?.Id ?? -1;
            RefreshProfileList(); RefreshComponents(); SyncProfileNameEdit(); if (_currentProfileId > 0) pm.ApplyProfileToAll(_currentProfileId);
        }

        private void OnProfileNameChanged(string newName)
        {
            if (_isRefreshing) return;
            var pm = EntityProfileManager.Instance;
            var p = pm?.GetProfile(_currentProfileId);
            if (p == null) return;
            p.Name = newName;
            RefreshProfileList();
        }

        // ══════════════════════════════════════════════════════════════════════?        //  Component Handlers
        // ══════════════════════════════════════════════════════════════════════?
        private void OnManageComponentsPressed()
        {
            var pm = EntityProfileManager.Instance;
            var profile = pm?.GetProfile(_currentProfileId);
            if (profile == null) return;

            // 清空对话框旧内容
            var oldVbox = _manageComponentsDialog.GetChildOrNull<VBoxContainer>(0);
            if (oldVbox != null) oldVbox.QueueFree();

            var vbox = new VBoxContainer();
            _manageComponentsDialog.AddChild(vbox);

            // 收集所有可用组件
            var allComponents = ComponentRegistry.GetComponentsForType(profile.EntityType).ToList();
            foreach (var c in ComponentRegistry.GetAllComponents())
                if (!allComponents.Any(x => x.name == c.name))
                    allComponents.Add(c);

            // 跟踪变更
            var toAdd = new HashSet<string>();
            var toRemove = new HashSet<string>();
            var toDisable = new HashSet<string>();
            var toEnable = new HashSet<string>();

            void RefreshList()
            {
                foreach (var child in vbox.GetChildren()) { if (child is Node n) n.QueueFree(); }

                // 三种状态：启用、停用、未添加
                var enabled = new List<(string name, string displayName)>();
                var disabled = new List<(string name, string displayName)>();
                var available = new List<(string name, string displayName)>();

                foreach (var (name, displayName) in allComponents)
                {
                    bool has = profile.HasComponent(name) && !toRemove.Contains(name);
                    bool pending = toAdd.Contains(name) && !profile.HasComponent(name);
                    bool isActive = has || pending;
                    if (!isActive) { available.Add((name, displayName)); continue; }

                    bool isDisabled = (profile.IsComponentDisabled(name) && !toEnable.Contains(name)) || toDisable.Contains(name);
                    if (isDisabled) disabled.Add((name, displayName));
                    else enabled.Add((name, displayName));
                }

                if (enabled.Count > 0)
                {
                    vbox.AddChild(new Label { Text = "启用", HorizontalAlignment = HorizontalAlignment.Left });
                    foreach (var (name, displayName) in enabled)
                    {
                        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                        row.AddChild(new Label { Text = "●", CustomMinimumSize = new Vector2(20, 0), HorizontalAlignment = HorizontalAlignment.Center });
                        row.AddChild(new Label { Text = displayName, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
                        var disableBtn = new Button { Text = "停用", CustomMinimumSize = new Vector2(44, 26), SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
                        var removeBtn = new Button { Text = "移除", CustomMinimumSize = new Vector2(44, 26), SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
                        string capturedName = name;
                        disableBtn.Pressed += () => { toDisable.Add(capturedName); toEnable.Remove(capturedName); RefreshList(); };
                        removeBtn.Pressed += () => { if (profile.HasComponent(capturedName)) toRemove.Add(capturedName); toAdd.Remove(capturedName); RefreshList(); };
                        row.AddChild(disableBtn);
                        row.AddChild(removeBtn);
                        vbox.AddChild(row);
                    }
                }

                if (disabled.Count > 0)
                {
                    vbox.AddChild(new HSeparator());
                    vbox.AddChild(new Label { Text = "停用", HorizontalAlignment = HorizontalAlignment.Left });
                    foreach (var (name, displayName) in disabled)
                    {
                        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                        row.AddChild(new Label { Text = "◐", CustomMinimumSize = new Vector2(20, 0), HorizontalAlignment = HorizontalAlignment.Center });
                        var nameLabel = new Label { Text = displayName, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                        nameLabel.Modulate = new Color(0.6f, 0.6f, 0.6f); // 灰色表示停用
                        row.AddChild(nameLabel);
                        var enableBtn = new Button { Text = "启用", CustomMinimumSize = new Vector2(44, 26), SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
                        var removeBtn = new Button { Text = "移除", CustomMinimumSize = new Vector2(44, 26), SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
                        string capturedName = name;
                        enableBtn.Pressed += () => { toEnable.Add(capturedName); toDisable.Remove(capturedName); RefreshList(); };
                        removeBtn.Pressed += () => { if (profile.HasComponent(capturedName)) toRemove.Add(capturedName); toAdd.Remove(capturedName); RefreshList(); };
                        row.AddChild(enableBtn);
                        row.AddChild(removeBtn);
                        vbox.AddChild(row);
                    }
                }

                if (available.Count > 0)
                {
                    vbox.AddChild(new HSeparator());
                    var addRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                    addRow.AddChild(new Label { Text = "添加:", CustomMinimumSize = new Vector2(35, 0) });
                    var addOption = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                    foreach (var (name, displayName) in available)
                    {
                        int idx = addOption.GetItemCount();
                        addOption.AddItem(displayName);
                        addOption.SetItemMetadata(idx, name);
                    }
                    addRow.AddChild(addOption);
                    var addBtn = new Button { Text = "+", CustomMinimumSize = new Vector2(32, 26) };
                    addBtn.Pressed += () =>
                    {
                        int idx = addOption.Selected;
                        if (idx < 0 || idx >= addOption.GetItemCount()) return;
                        string capturedName = (string)addOption.GetItemMetadata(idx);
                        toRemove.Remove(capturedName);
                        if (!profile.HasComponent(capturedName)) toAdd.Add(capturedName);
                        RefreshList();
                    };
                    addRow.AddChild(addBtn);
                    vbox.AddChild(addRow);
                }
            }

            RefreshList();

            // 确认时应用变更（先断开旧 handler 避免重复）
            if (_manageComponentsHandler != null)
                _manageComponentsDialog.Confirmed -= _manageComponentsHandler;
            _manageComponentsHandler = () =>
            {
                bool changed = false;
                foreach (string name in toRemove)
                {
                    profile.RemoveComponent(name);
                    RemoveComponentUI(name);
                    changed = true;
                }
                foreach (string name in toAdd)
                {
                    var comp = ComponentRegistry.Create(name);
                    if (comp != null)
                    {
                        profile.SetData(name, comp.SyncToData());
                        comp.Dispose();
                        AddComponentUI(name);
                        changed = true;
                    }
                }
                foreach (string name in toDisable)
                {
                    profile.SetComponentDisabled(name, true);
                    changed = true;
                }
                foreach (string name in toEnable)
                {
                    profile.SetComponentDisabled(name, false);
                    changed = true;
                }
                if (changed) pm.ApplyProfileToAll(_currentProfileId);
            };
            _manageComponentsDialog.Confirmed += _manageComponentsHandler;

            _manageComponentsDialog.PopupCentered(new Vector2I(280, 0));
        }

        private void OnRemoveComponentPressed(string compName)
        {
            _pendingDeleteComponentName = compName;
            string dn = ComponentRegistry.GetAllComponents().FirstOrDefault(c => c.name == compName).displayName ?? compName;
            _deleteComponentDialog.DialogText = $"确定要删除组?'{dn}' 吗？";
            _deleteComponentDialog.PopupCentered();
        }

        private void OnDeleteComponentConfirmed()
        {
            if (string.IsNullOrEmpty(_pendingDeleteComponentName)) return;
            var pm = EntityProfileManager.Instance;
            var profile = pm?.GetProfile(_currentProfileId);
            if (profile == null) return;
            string cn = _pendingDeleteComponentName; _pendingDeleteComponentName = null;
            profile.RemoveComponent(cn);
            RemoveComponentUI(cn);
            pm.ApplyProfileToAll(_currentProfileId);
        }

        // ══════════════════════════════════════════════════════════════════════?        //  Data Flow
        // ══════════════════════════════════════════════════════════════════════?
        private void OnComponentChanged()
        {
            if (_isRefreshing) return;
            var pm = EntityProfileManager.Instance;
            var profile = pm?.GetProfile(_currentProfileId);
            if (profile == null)
            {
                GD.PrintErr($"[EntityTab] OnComponentChanged: pm={pm?.GetType().Name}, profileId={_currentProfileId}, profile=null");
                return;
            }
            foreach (var kv in _activeComponents) profile.SetData(kv.Key, kv.Value.SyncToData());
            pm.ApplyProfileToAll(_currentProfileId);
            ScheduleSave();
        }

        private bool _saveScheduled = false;
        private void ScheduleSave()
        {
            if (_saveScheduled) return;
            _saveScheduled = true;
            var tree = Owner?.GetTree();
            if (tree == null) { _saveScheduled = false; return; }
            var timer = tree.CreateTimer(1.0);
            timer.Timeout += () =>
            {
                _saveScheduled = false;
                // 通过 DebugPanel 统一保存，避免覆盖其?tab 数据
                Owner?.SaveConfigFromTab();
            };
        }

        private void SaveCurrentProfileData()
        {
            if (_currentProfileId < 0) return;
            var pm = EntityProfileManager.Instance;
            var profile = pm?.GetProfile(_currentProfileId);
            if (profile == null) return;
            foreach (var kv in _activeComponents) profile.SetData(kv.Key, kv.Value.SyncToData());
        }

        // ══════════════════════════════════════════════════════════════════════?        //  Refresh
        // ══════════════════════════════════════════════════════════════════════?
        private void RefreshProfileList()
        {
            var pm = EntityProfileManager.Instance;
            if (pm == null) return;
            _profileOption.Clear();
            int sel = -1;
            foreach (var p in pm.GetAllProfiles())
            {
                int i = _profileOption.GetItemCount();
                _profileOption.AddItem($"{p.Name} ({p.Id})");
                _profileOption.SetItemMetadata(i, p.Id);
                if (p.Id == _currentProfileId) sel = i;
            }
            if (sel >= 0) _profileOption.Select(sel);
            else if (_profileOption.GetItemCount() > 0) _profileOption.Select(0);
        }

        private void SyncProfileNameEdit()
        {
            var p = EntityProfileManager.Instance?.GetProfile(_currentProfileId);
            _isRefreshing = true;
            _profileNameEdit.Text = p?.Name ?? "";
            _profileNameEdit.Editable = p != null;
            _isRefreshing = false;
        }

        private void RefreshComponents()
        {
            _isRefreshing = true;
            foreach (var c in _activeComponents.Values) c.DisconnectSignals();
            _activeComponents.Clear();
            foreach (var ct in _componentContainers.Values) { if (ct != null && GodotObject.IsInstanceValid(ct)) ct.QueueFree(); }
            _componentContainers.Clear();
            var profile = EntityProfileManager.Instance?.GetProfile(_currentProfileId);
            if (profile != null) foreach (string cn in profile.ComponentNames) AddComponentUI(cn);
            _isRefreshing = false;
        }

        private void AddComponentUI(string compName)
        {
            var pm = EntityProfileManager.Instance;
            var profile = pm?.GetProfile(_currentProfileId);
            if (profile == null) return;
            var comp = ComponentRegistry.Create(compName);
            if (comp == null) return;

            string displayName = ComponentRegistry.GetAllComponents()
                .FirstOrDefault(c => c.name == compName).displayName ?? compName;

            var container = new CollapsibleContainer(displayName);

            // 停用的组件灰色显示 + 默认折叠
            bool isDisabled = profile.IsComponentDisabled(compName);
            if (isDisabled)
            {
                container.Modulate = new Color(0.6f, 0.6f, 0.6f);
                container.SetCollapsedSilent(true);
            }

            // Build component UI inside CollapsibleContainer's content area
            comp.BuildUI(container.Content);

            // Sync data ?UI
            var data = profile.GetData(compName);
            if (data != null) comp.SyncFromData(data);

            // Connect signals
            comp.ConnectSignals(OnComponentChanged);

            _activeComponents[compName] = comp;
            _componentContainers[compName] = container;

            _componentContainer.AddChild(container);
        }

        private void RemoveComponentUI(string compName)
        {
            if (_activeComponents.TryGetValue(compName, out var comp))
            {
                comp.DisconnectSignals();
                comp.Dispose();
                _activeComponents.Remove(compName);
            }
            if (_componentContainers.TryGetValue(compName, out var container))
            {
                _componentContainers.Remove(compName);
                if (container != null && GodotObject.IsInstanceValid(container)) container.QueueFree();
            }
        }

        // ══════════════════════════════════════════════════════════════════════?        //  Entity Click
        // ══════════════════════════════════════════════════════════════════════?
        private void OnEntityClicked(EntityBase entity)
        {
            if (entity.ProfileId <= 0) return;
            SaveCurrentProfileData();
            _currentProfileId = entity.ProfileId;
            RefreshProfileList();
            RefreshComponents();
            SyncProfileNameEdit();
            // Sync from entity's live values (server-pushed data)
            foreach (var comp in _activeComponents.Values)
                comp.SyncFromEntity(entity);
        }

        // ══════════════════════════════════════════════════════════════════════?        //  SyncToCurrentValues
        // ══════════════════════════════════════════════════════════════════════?
        public override void SyncToCurrentValues()
        {
            // Find an entity with current profile and sync from it
            var pm = EntityProfileManager.Instance;
            if (pm == null) return;
            var profile = pm.GetProfile(_currentProfileId);
            if (profile == null) return;

            // Re-sync all components from profile data
            foreach (var kv in _activeComponents)
            {
                var data = profile.GetData(kv.Key);
                if (data != null) kv.Value.SyncFromData(data);
            }
        }

        // ══════════════════════════════════════════════════════════════════════?        //  SaveConfig / LoadConfig
        // ══════════════════════════════════════════════════════════════════════?
        public override void SaveConfig(ConfigFile cfg)
        {
            SaveCurrentProfileData();
            cfg.SetValue("entity_tab", "current_profile_id", _currentProfileId);
            // ?Profile 数据写入同一?ConfigFile
            EntityProfileManager.Instance?.WriteProfileConfig(cfg);
        }

        public override void LoadConfig(ConfigFile cfg, bool configLoaded)
        {
            if (!configLoaded) return;

            var pm = EntityProfileManager.Instance;
            if (pm == null) return;

            // Profile 数据?EntityProfileManager._Ready 加载，这里不再重复加?            // 只读取当前选中?Profile ID
            int savedId = (int)(double)cfg.GetValue("entity_tab", "current_profile_id", -1.0);

            // Validate the saved profile ID exists
            if (savedId > 0 && pm.GetProfile(savedId) != null)
                _currentProfileId = savedId;
            else
                _currentProfileId = pm.GetAllProfiles().FirstOrDefault()?.Id ?? -1;

            RefreshProfileList();
            RefreshComponents();
            SyncProfileNameEdit();
            // 不再在这里延迟 ApplyProfileToAll
            // Player 的 SetupLabels 完成后会自己调 ApplyProfile
            // Monster/NPC 在创建时由 Manager 设置 ProfileId，等 AssignDefaultProfileIds 或手动触发
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Undo System
        // ══════════════════════════════════════════════════════════════════════?
        // Undo state stored in C# dictionary (IComponentData is not a Godot Variant)
        private int _undoProfileId = -1;
        private Dictionary<string, IComponentData> _undoComponentSnapshots = new();

        public override Godot.Collections.Dictionary CaptureUndoState()
        {
            // Save current data to profile first
            SaveCurrentProfileData();

            _undoProfileId = _currentProfileId;
            _undoComponentSnapshots.Clear();

            var profile = EntityProfileManager.Instance?.GetProfile(_currentProfileId);
            if (profile != null)
            {
                foreach (string cn in profile.ComponentNames)
                {
                    var data = profile.GetData(cn);
                    if (data != null) _undoComponentSnapshots[cn] = data.Clone();
                }
            }

            // Return minimal Godot dict (actual state stored in C# fields)
            var state = new Godot.Collections.Dictionary();
            state["profile_id"] = _currentProfileId;
            return state;
        }

        public override void ApplyUndoState(Godot.Collections.Dictionary state)
        {
            if (_undoProfileId < 0) return;

            var pm = EntityProfileManager.Instance;
            if (pm == null) return;

            // Restore profile ID
            if (pm.GetProfile(_undoProfileId) != null)
            {
                _currentProfileId = _undoProfileId;
                RefreshProfileList();
            }

            // Restore component data to profile
            var profile = pm.GetProfile(_currentProfileId);
            if (profile != null)
            {
                foreach (var kv in _undoComponentSnapshots)
                    profile.SetData(kv.Key, kv.Value.Clone());
            }

            // Rebuild UI from restored state
            RefreshComponents();
            SyncProfileNameEdit();
            if (_currentProfileId > 0) pm.ApplyProfileToAll(_currentProfileId);
        }

        // ══════════════════════════════════════════════════════════════════════?        //  Export
        // ══════════════════════════════════════════════════════════════════════?
        public override Godot.Collections.Dictionary ExportConfigData()
        {
            var data = new Godot.Collections.Dictionary();
            var pm = EntityProfileManager.Instance;
            var profile = pm?.GetProfile(_currentProfileId);
            if (profile == null) return data;

            data["profile_id"] = profile.Id;
            data["profile_name"] = profile.Name;
            data["entity_type"] = profile.EntityType;
            data["component_names"] = string.Join(",", profile.ComponentNames);
            return data;
        }
    }
}