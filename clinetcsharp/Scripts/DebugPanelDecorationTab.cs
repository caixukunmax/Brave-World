using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinetCSharp
{
    /// <summary>
    /// DebugPanel 建筑页签 — 复用 EntityProfile + IEntityTabComponent 体系管理建筑配置。
    /// </summary>
    public class DebugPanelDecorationTab : DebugPanelTab
    {
        public override string TabKey => "decoration";

        private Label _buildingTypeLabel;
        private Label _currentNameLabel;
        private LineEdit _profileSearchEdit;
        private Button _profileDropdownBtn;
        private PopupPanel _profilePopup;
        private ItemList _profilePopupItemList;
        private VBoxContainer _componentContainer;

        private bool _isUpdatingSearchText;
        private Button _addProfileBtn;
        private Button _deleteProfileBtn;
        private Button _manageComponentsBtn;

        private int _currentProfileId = -1;
        private bool _isRefreshing;
        private bool _showAllComponents;

        private readonly Dictionary<string, IEntityTabComponent> _activeComponents = new();
        private readonly Dictionary<string, CollapsibleContainer> _componentContainers = new();

        private AcceptDialog _newProfileDialog;
        private LineEdit _newProfileNameEdit;
        private OptionButton _newProfileTemplateOption;
        private ConfirmationDialog _deleteProfileDialog;
        private AcceptDialog _manageComponentsDialog;
        private Action _manageComponentsConfirmedHandler;

        public DebugPanelDecorationTab(DebugPanel owner) : base(owner) { }

        public override void BuildUI(VBoxContainer tabContainer)
        {
            _tabContainer = tabContainer;
            AddTabTitle(tabContainer, "建筑工坊", 13);

            var enterEditorBtn = new Button
            {
                Text = "进入地图编辑-放建筑",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 24),
            };
            enterEditorBtn.Pressed += OnEnterEditorPressed;
            enterEditorBtn.AddThemeFontSizeOverride("font_size", 11);
            tabContainer.AddChild(enterEditorBtn);

            AddSectionSeparator(tabContainer);

            BuildCurrentProfileSection(tabContainer);
            AddSectionSeparator(tabContainer);
            BuildComponentManagementSection(tabContainer);
            AddSectionSeparator(tabContainer);
            BuildCurrentComponentsSection(tabContainer);

            BuildDialogs();

            var profileManager = EntityProfileManager.Instance;
            if (profileManager != null)
            {
                if (_currentProfileId < 0 || profileManager.GetProfile(_currentProfileId) == null)
                    _currentProfileId = profileManager.GetProfilesByType("decoration").FirstOrDefault()?.Id ?? -1;

                RefreshProfileList();
                SyncProfileNameEdit();
                RefreshComponents();
            }
        }

        public override void ConnectSignals()
        {
            // 无需连接全局实体点击事件
        }

        public override void DisconnectSignals()
        {
            foreach (var component in _activeComponents.Values)
                component.DisconnectSignals();
        }

        public override void SaveConfig(ConfigFile cfg)
        {
            SaveCurrentProfileData();
            EntityProfileManager.Instance?.SaveConfig();
        }

        public override void LoadConfig(ConfigFile cfg, bool configLoaded)
        {
            // EntityProfileManager 已负责加载/保存
        }

        public override void SyncToCurrentValues()
        {
            RefreshProfileList();
            SyncProfileNameEdit();
            RefreshComponents();
        }

        #region UI Building

        private void BuildCurrentProfileSection(Container parent)
        {
            var section = CreateSectionCard(parent, "当前建筑");

            var currentRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _currentNameLabel = new Label
            {
                CustomMinimumSize = new Vector2(48, 0),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            };
            currentRow.AddChild(_currentNameLabel);
            _profileSearchEdit = new LineEdit
            {
                PlaceholderText = "ID",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            _profileSearchEdit.TextChanged += OnProfileSearchTextChanged;
            _profileSearchEdit.FocusEntered += OnProfileSearchFocusEntered;
            currentRow.AddChild(_profileSearchEdit);

            _profileDropdownBtn = new Button { Text = "▼", CustomMinimumSize = new Vector2(28, 0) };
            _profileDropdownBtn.Pressed += OnProfileDropdownPressed;
            currentRow.AddChild(_profileDropdownBtn);
            section.AddChild(currentRow);

            _profilePopup = new PopupPanel();
            var popupVBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            popupVBox.AddThemeConstantOverride("separation", 4);
            _profilePopup.AddChild(popupVBox);

            _profilePopupItemList = new ItemList
            {
                CustomMinimumSize = new Vector2(0, 120),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            };
            _profilePopupItemList.ItemSelected += OnProfilePopupItemSelected;
            popupVBox.AddChild(_profilePopupItemList);
            Owner.AddChild(_profilePopup);

            var typeRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            typeRow.AddChild(new Label { Name = "_lbl", Text = "建筑类型", CustomMinimumSize = new Vector2(48, 0) });
            _buildingTypeLabel = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            typeRow.AddChild(_buildingTypeLabel);
            section.AddChild(typeRow);
        }

        private void BuildComponentManagementSection(Container parent)
        {
            var section = CreateSectionCard(parent, "组件管理");

            var actionRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _addProfileBtn = new Button { Text = "新建建筑", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _deleteProfileBtn = new Button { Text = "删除", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _manageComponentsBtn = new Button { Text = "管理组件", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            actionRow.AddChild(_addProfileBtn);
            actionRow.AddChild(_deleteProfileBtn);
            actionRow.AddChild(_manageComponentsBtn);
            section.AddChild(actionRow);

            _addProfileBtn.Pressed += OnAddProfilePressed;
            _deleteProfileBtn.Pressed += OnDeleteProfilePressed;
            _manageComponentsBtn.Pressed += OnManageComponentsPressed;
        }

        private void BuildCurrentComponentsSection(Container parent)
        {
            var section = CreateSectionCard(parent, "当前组件");
            _componentContainer = new VBoxContainer
            {
                Name = "ComponentContainer",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            _componentContainer.AddThemeConstantOverride("separation", 8);
            section.AddChild(_componentContainer);
        }

        private void BuildDialogs()
        {
            _newProfileDialog = new AcceptDialog { Title = "新建建筑" };
            var dialogVBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            dialogVBox.AddThemeConstantOverride("separation", 12);
            _newProfileDialog.AddChild(dialogVBox);

            dialogVBox.AddChild(new Label { Text = "选择参考模板并输入新建筑名称：", AutowrapMode = TextServer.AutowrapMode.WordSmart });

            var nameRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            nameRow.AddChild(new Label { Name = "_lbl", Text = "名称", CustomMinimumSize = new Vector2(48, 0) });
            _newProfileNameEdit = new LineEdit { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, PlaceholderText = "英文标识", Text = "NewBuilding" };
            nameRow.AddChild(_newProfileNameEdit);
            dialogVBox.AddChild(nameRow);

            var templateRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            templateRow.AddChild(new Label { Name = "_lbl", Text = "模板", CustomMinimumSize = new Vector2(48, 0) });
            _newProfileTemplateOption = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            templateRow.AddChild(_newProfileTemplateOption);
            dialogVBox.AddChild(templateRow);

            _newProfileDialog.Confirmed += OnNewProfileConfirmed;
            Owner.AddChild(_newProfileDialog);

            _deleteProfileDialog = new ConfirmationDialog { Title = "确认删除建筑" };
            _deleteProfileDialog.Confirmed += OnDeleteProfileConfirmed;
            Owner.AddChild(_deleteProfileDialog);

            _manageComponentsDialog = new AcceptDialog { Title = "管理组件" };
            Owner.AddChild(_manageComponentsDialog);
        }

        private static VBoxContainer CreateSectionCard(Container parent, string title)
        {
            var section = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Name = $"{title}Section" };
            section.AddThemeConstantOverride("separation", 6);

            var frame = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0.12f, 0.12f, 0.12f, 0.55f),
                BorderColor = new Color(0.34f, 0.34f, 0.34f, 0.95f),
                BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
                CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomRight = 8, CornerRadiusBottomLeft = 8,
                ContentMarginLeft = 10, ContentMarginTop = 8, ContentMarginRight = 10, ContentMarginBottom = 10,
            });

            var body = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Name = "Body" };
            body.AddThemeConstantOverride("separation", 8);

            var header = new Label { Name = "_lbl", Text = title, HorizontalAlignment = HorizontalAlignment.Left };
            header.AddThemeFontSizeOverride("font_size", 12);
            body.AddChild(header);

            frame.AddChild(body);
            section.AddChild(frame);
            parent.AddChild(section);
            return body;
        }

        #endregion

        #region Profile List & Selection

        private void RefreshProfileList()
        {
            var profileManager = EntityProfileManager.Instance;
            if (profileManager == null) return;

            RefreshProfilePopupItems(_profileSearchEdit?.Text ?? "");
            UpdateSearchEditText();
        }

        private void RefreshProfilePopupItems(string filter)
        {
            if (_profilePopupItemList == null) return;

            _profilePopupItemList.Clear();
            var profileManager = EntityProfileManager.Instance;
            if (profileManager == null) return;

            var profiles = profileManager.GetProfilesByType("decoration").OrderBy(p => p.Id).ToList();
            var normalizedFilter = filter.Trim().ToLowerInvariant();
            int selectedIndex = -1;
            int displayIndex = 0;

            for (int i = 0; i < profiles.Count; i++)
            {
                var profile = profiles[i];
                string displayName = GetProfileDisplayName(profile);
                if (!string.IsNullOrEmpty(normalizedFilter)
                    && !profile.Name.ToLowerInvariant().Contains(normalizedFilter)
                    && !displayName.ToLowerInvariant().Contains(normalizedFilter)
                    && !profile.Id.ToString().Contains(normalizedFilter))
                    continue;

                var app = profile.GetData<AppearanceData>("appearance");
                var color = app?.BgColor ?? Colors.Gray;
                _profilePopupItemList.AddItem($"{displayName} [{profile.Id}]");
                _profilePopupItemList.SetItemMetadata(displayIndex, profile.Id);
                _profilePopupItemList.SetItemIconModulate(displayIndex, new Color(color.R, color.G, color.B, app?.BgOpacity ?? 1.0f));
                if (profile.Id == _currentProfileId)
                    selectedIndex = displayIndex;
                displayIndex++;
            }

            if (selectedIndex >= 0)
                _profilePopupItemList.Select(selectedIndex);
            else if (_profilePopupItemList.GetItemCount() > 0)
            {
                _profilePopupItemList.Select(0);
                _currentProfileId = (int)_profilePopupItemList.GetItemMetadata(0);
            }
        }

        private void UpdateSearchEditText()
        {
            var profile = EntityProfileManager.Instance?.GetProfile(_currentProfileId);
            _isUpdatingSearchText = true;
            if (_currentNameLabel != null)
                _currentNameLabel.Text = profile != null ? GetProfileDisplayName(profile) : "";
            if (_profileSearchEdit != null)
                _profileSearchEdit.Text = profile != null ? $"{profile.Id}" : "";
            _isUpdatingSearchText = false;
        }

        private void OpenProfilePopup()
        {
            if (_profilePopup == null || _profileSearchEdit == null) return;
            if (_profilePopup.Visible) return;

            var globalPos = _profileSearchEdit.GlobalPosition;
            int width = Mathf.CeilToInt(_profileSearchEdit.Size.X + (_profileDropdownBtn?.Size.X ?? 0));
            _profilePopup.Size = new Vector2I(width, 160);
            _profilePopup.Position = new Vector2I(Mathf.CeilToInt(globalPos.X), Mathf.CeilToInt(globalPos.Y + _profileSearchEdit.Size.Y));
            _profilePopup.Popup();
        }

        private void SelectProfile(int id)
        {
            SaveCurrentProfileData();
            _currentProfileId = id;
            RefreshProfileList();
            SyncProfileNameEdit();
            RefreshComponents();
        }

        private void OnProfilePopupItemSelected(long index)
        {
            int id = (int)_profilePopupItemList.GetItemMetadata((int)index);
            _profilePopup?.Hide();
            SelectProfile(id);
        }

        private void OnProfileSearchTextChanged(string text)
        {
            if (_isUpdatingSearchText) return;
            OpenProfilePopup();
            RefreshProfilePopupItems(text);
        }

        private void OnProfileSearchFocusEntered()
        {
            OpenProfilePopup();
            RefreshProfilePopupItems(_profileSearchEdit?.Text ?? "");
        }

        private void OnProfileDropdownPressed()
        {
            if (_profilePopup != null && _profilePopup.Visible)
            {
                _profilePopup.Hide();
                return;
            }
            OpenProfilePopup();
            RefreshProfilePopupItems(_profileSearchEdit?.Text ?? "");
        }

        private void SyncProfileNameEdit()
        {
            var profile = EntityProfileManager.Instance?.GetProfile(_currentProfileId);
            _isRefreshing = true;

            if (profile != null)
            {
                int buildingType = BuildingType.GetTypeFromConfigId(profile.Id);
                string typeName = buildingType == BuildingType.House ? "房舍" : buildingType == BuildingType.Shop ? "商店" : "未知";
                _buildingTypeLabel.Text = $"{typeName} ({buildingType})";
            }
            else
            {
                _buildingTypeLabel.Text = "";
            }

            _isRefreshing = false;
        }

        private static string GetProfileDisplayName(EntityProfile profile)
        {
            if (profile == null) return "";
            var labels = profile.GetData<LabelGroupData>("labels");
            if (!string.IsNullOrEmpty(labels?.ContentPreview[0]))
                return labels.ContentPreview[0];
            return profile.Name;
        }

        #endregion

        #region Profile CRUD

        private void OnAddProfilePressed()
        {
            _newProfileNameEdit.Text = "NewBuilding";

            _newProfileTemplateOption.Clear();
            var profileManager = EntityProfileManager.Instance;
            if (profileManager != null)
            {
                foreach (var profile in profileManager.GetProfilesByType("decoration").OrderBy(p => p.Id))
                {
                    int index = _newProfileTemplateOption.GetItemCount();
                    _newProfileTemplateOption.AddItem($"{GetProfileDisplayName(profile)} (ID:{profile.Id})");
                    _newProfileTemplateOption.SetItemMetadata(index, profile.Id);
                }
            }
            _newProfileTemplateOption.Select(0);

            _newProfileDialog.PopupCentered(new Vector2I(360, 180));
            _newProfileNameEdit.GrabFocus();
            _newProfileNameEdit.SelectAll();
        }

        private void OnNewProfileConfirmed()
        {
            string name = _newProfileNameEdit.Text.StripEdges();
            if (string.IsNullOrEmpty(name))
                name = "NewBuilding";

            var profileManager = EntityProfileManager.Instance;
            if (profileManager == null) return;

            int selectedIndex = _newProfileTemplateOption.Selected;
            if (selectedIndex < 0 || selectedIndex >= _newProfileTemplateOption.GetItemCount())
                return;

            int templateId = (int)_newProfileTemplateOption.GetItemMetadata(selectedIndex);
            var template = profileManager.GetProfile(templateId);
            if (template == null) return;

            SaveCurrentProfileData();
            var profile = profileManager.CreateBuildingProfileFromTemplate(templateId, name);
            if (profile == null) return;

            _currentProfileId = profile.Id;
            RefreshProfileList();
            SyncProfileNameEdit();
            RefreshComponents();
            profileManager.ApplyProfileToAll(profile.Id);
            profileManager.SaveConfig();
        }

        private void OnDeleteProfilePressed()
        {
            var profileManager = EntityProfileManager.Instance;
            if (profileManager == null) return;

            var profile = profileManager.GetProfile(_currentProfileId);
            if (profile == null) return;

            // 默认建筑配置不可删除
            if (profileManager.IsDefaultProfile(_currentProfileId))
            {
                ShowWarning("默认建筑配置不可删除。");
                return;
            }

            _deleteProfileDialog.DialogText = $"确定要删除建筑\"{GetProfileDisplayName(profile)}\" (ID: {profile.Id}) 吗？";
            _deleteProfileDialog.PopupCentered();
        }

        private void OnDeleteProfileConfirmed()
        {
            var profileManager = EntityProfileManager.Instance;
            if (profileManager == null) return;

            profileManager.DeleteProfile(_currentProfileId);
            var remaining = profileManager.GetProfilesByType("decoration").ToList();
            _currentProfileId = remaining.FirstOrDefault()?.Id ?? -1;
            RefreshProfileList();
            SyncProfileNameEdit();
            RefreshComponents();
            if (_currentProfileId > 0)
                profileManager.ApplyProfileToAll(_currentProfileId);
            profileManager.SaveConfig();
        }

        private void ShowWarning(string text)
        {
            var warning = new AcceptDialog { Title = "提示", DialogText = text };
            warning.Confirmed += () => warning.QueueFree();
            warning.Canceled += () => warning.QueueFree();
            Owner.AddChild(warning);
            warning.PopupCentered();
        }

        #endregion

        #region Component Management

        private void RefreshComponents()
        {
            _isRefreshing = true;

            foreach (var component in _activeComponents.Values)
                component.DisconnectSignals();
            _activeComponents.Clear();

            foreach (var container in _componentContainers.Values)
            {
                if (container != null && GodotObject.IsInstanceValid(container))
                    container.QueueFree();
            }
            _componentContainers.Clear();

            var profile = EntityProfileManager.Instance?.GetProfile(_currentProfileId);
            if (profile != null)
            {
                foreach (string componentName in profile.ComponentNames)
                    AddComponentUI(componentName);
            }

            _isRefreshing = false;
        }

        private void AddComponentUI(string componentName)
        {
            var profileManager = EntityProfileManager.Instance;
            var profile = profileManager?.GetProfile(_currentProfileId);
            if (profile == null) return;

            var component = ComponentRegistry.Create(componentName);
            if (component == null) return;

            string displayName = ComponentRegistry.GetAllComponents()
                .FirstOrDefault(c => c.name == componentName).displayName ?? componentName;

            var container = new CollapsibleContainer(displayName);
            bool isDisabled = profile.IsComponentDisabled(componentName);
            if (isDisabled)
            {
                container.Modulate = new Color(0.6f, 0.6f, 0.6f);
                container.SetCollapsedSilent(true);
            }

            component.BuildUI(container.Content);
            var data = profile.GetData(componentName);
            if (data != null)
                component.SyncFromData(data);
            component.ConnectSignals(OnComponentChanged);

            _activeComponents[componentName] = component;
            _componentContainers[componentName] = container;
            _componentContainer.AddChild(container);
        }

        private void OnComponentChanged()
        {
            if (_isRefreshing) return;

            var profileManager = EntityProfileManager.Instance;
            var profile = profileManager?.GetProfile(_currentProfileId);
            if (profile == null) return;

            SaveCurrentProfileData();
            SyncProfileNameFromBuildingType(profile);
            profileManager.ApplyProfileToAll(_currentProfileId);
            DecorationConfigUtil.RefreshFromProfileManager();
            profileManager.SaveConfig();
            RefreshProfileList();
        }

        private void SyncProfileNameFromBuildingType(EntityProfile profile)
        {
            var bt = profile.GetData<BuildingTypeData>("building_type");
            int type = bt?.Type ?? BuildingType.House;
            string name = type == BuildingType.House ? "房舍" : type == BuildingType.Shop ? "商店" : "建筑";
            profile.Name = name;
            var labels = profile.GetData<LabelGroupData>("labels");
            if (labels != null)
            {
                labels.ContentPreview[0] = name;
                labels.Names[0] = "名称";
                labels.Visible[0] = true;
                labels.CenterX[0] = true;
            }
        }

        private void SaveCurrentProfileData()
        {
            if (_currentProfileId < 0) return;
            var profile = EntityProfileManager.Instance?.GetProfile(_currentProfileId);
            if (profile == null) return;

            foreach (var kv in _activeComponents)
                profile.SetData(kv.Key, kv.Value.SyncToData());
        }

        private void OnManageComponentsPressed()
        {
            var profileManager = EntityProfileManager.Instance;
            var profile = profileManager?.GetProfile(_currentProfileId);
            if (profile == null) return;

            var oldVBox = _manageComponentsDialog.GetChildOrNull<VBoxContainer>(0);
            if (oldVBox != null)
                oldVBox.QueueFree();

            var dialogVBox = new VBoxContainer();
            dialogVBox.AddThemeConstantOverride("separation", 8);
            _manageComponentsDialog.AddChild(dialogVBox);

            var toAdd = new HashSet<string>();
            var toRemove = new HashSet<string>();
            var toDisable = new HashSet<string>();
            var toEnable = new HashSet<string>();

            var advancedModeCheck = new CheckBox { Text = "显示全部组件（高级模式）", ButtonPressed = _showAllComponents };
            var advancedHint = new Label
            {
                Text = "高级模式会显示建筑白名单之外的实验组件。",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Modulate = new Color(0.82f, 0.72f, 0.42f),
                Visible = _showAllComponents,
            };

            void RefreshList()
            {
                foreach (var child in dialogVBox.GetChildren())
                    if (child is Node node)
                        node.QueueFree();

                dialogVBox.AddChild(advancedModeCheck);
                dialogVBox.AddChild(advancedHint);

                var catalog = BuildComponentCatalog(profile);
                var enabled = new List<(string name, string displayName)>();
                var disabled = new List<(string name, string displayName)>();
                var available = new List<(string name, string displayName)>();

                foreach (var (name, displayName) in catalog)
                {
                    bool has = profile.HasComponent(name) && !toRemove.Contains(name);
                    bool pending = toAdd.Contains(name) && !profile.HasComponent(name);
                    bool isActive = has || pending;
                    if (!isActive)
                    {
                        available.Add((name, displayName));
                        continue;
                    }

                    bool isDisabled = (profile.IsComponentDisabled(name) && !toEnable.Contains(name)) || toDisable.Contains(name);
                    if (isDisabled)
                        disabled.Add((name, displayName));
                    else
                        enabled.Add((name, displayName));
                }

                BuildEnabledComponentList(dialogVBox, enabled, toDisable, toEnable, toRemove, toAdd);
                BuildDisabledComponentList(dialogVBox, disabled, toDisable, toEnable, toRemove, toAdd);
                BuildAvailableComponentList(dialogVBox, available, profile, toRemove, toAdd, RefreshList);
            }

            advancedModeCheck.Toggled += enabled =>
            {
                _showAllComponents = enabled;
                advancedHint.Visible = enabled;
                RefreshList();
            };

            RefreshList();

            // 避免重复订阅 Confirmed
            if (_manageComponentsConfirmedHandler != null)
                _manageComponentsDialog.Confirmed -= _manageComponentsConfirmedHandler;

            _manageComponentsConfirmedHandler = () =>
            {
                bool changed = false;
                foreach (string name in toRemove)
                {
                    profile.RemoveComponent(name);
                    changed = true;
                }
                foreach (string name in toAdd)
                {
                    var component = ComponentRegistry.Create(name);
                    if (component != null)
                    {
                        profile.SetData(name, component.SyncToData());
                        component.Dispose();
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
                if (changed)
                {
                    RefreshComponents();
                    profileManager.ApplyProfileToAll(profile.Id);
                    DecorationConfigUtil.RefreshFromProfileManager();
                    profileManager.SaveConfig();
                    RefreshProfileList();
                }
            };
            _manageComponentsDialog.Confirmed += _manageComponentsConfirmedHandler;

            _manageComponentsDialog.PopupCentered(new Vector2I(360, 0));
        }

        private List<(string name, string displayName)> BuildComponentCatalog(EntityProfile profile)
        {
            var allowed = ComponentRegistry.GetComponentsForType("decoration").ToList();
            var catalog = new List<(string name, string displayName)>(allowed);

            foreach (string componentName in profile.ComponentNames)
            {
                if (catalog.Any(x => x.name == componentName))
                    continue;
                catalog.Add((componentName, ResolveComponentDisplayName(componentName)));
            }

            if (!_showAllComponents)
                return catalog;

            foreach (var component in ComponentRegistry.GetAllComponents())
            {
                if (catalog.Any(x => x.name == component.name))
                    continue;
                bool isExperimental = !allowed.Any(x => x.name == component.name);
                string displayName = isExperimental ? $"{component.displayName}（实验）" : component.displayName;
                catalog.Add((component.name, displayName));
            }

            return catalog;
        }

        private static string ResolveComponentDisplayName(string componentName)
        {
            return ComponentRegistry.GetAllComponents()
                .FirstOrDefault(c => c.name == componentName).displayName ?? componentName;
        }

        private static void BuildEnabledComponentList(
            VBoxContainer dialogVBox,
            List<(string name, string displayName)> enabled,
            HashSet<string> toDisable,
            HashSet<string> toEnable,
            HashSet<string> toRemove,
            HashSet<string> toAdd)
        {
            if (enabled.Count <= 0) return;

            dialogVBox.AddChild(new Label { Text = "已启用", HorizontalAlignment = HorizontalAlignment.Left });
            foreach (var (name, displayName) in enabled)
            {
                var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                row.AddChild(new Label { Text = "*", CustomMinimumSize = new Vector2(20, 0), HorizontalAlignment = HorizontalAlignment.Center });
                row.AddChild(new Label { Text = displayName, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

                var disableButton = new Button { Text = "停用", CustomMinimumSize = new Vector2(52, 26), SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
                var removeButton = new Button { Text = "移除", CustomMinimumSize = new Vector2(52, 26), SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
                string capturedName = name;
                disableButton.Pressed += () => { toDisable.Add(capturedName); toEnable.Remove(capturedName); };
                removeButton.Pressed += () => { toAdd.Remove(capturedName); toRemove.Add(capturedName); };
                row.AddChild(disableButton);
                row.AddChild(removeButton);
                dialogVBox.AddChild(row);
            }
        }

        private static void BuildDisabledComponentList(
            VBoxContainer dialogVBox,
            List<(string name, string displayName)> disabled,
            HashSet<string> toDisable,
            HashSet<string> toEnable,
            HashSet<string> toRemove,
            HashSet<string> toAdd)
        {
            if (disabled.Count <= 0) return;

            dialogVBox.AddChild(new HSeparator());
            dialogVBox.AddChild(new Label { Text = "已停用", HorizontalAlignment = HorizontalAlignment.Left });
            foreach (var (name, displayName) in disabled)
            {
                var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                row.AddChild(new Label { Text = "*", CustomMinimumSize = new Vector2(20, 0), HorizontalAlignment = HorizontalAlignment.Center });
                var nameLabel = new Label { Text = displayName, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                nameLabel.Modulate = new Color(0.6f, 0.6f, 0.6f);
                row.AddChild(nameLabel);

                var enableButton = new Button { Text = "启用", CustomMinimumSize = new Vector2(52, 26), SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
                var removeButton = new Button { Text = "移除", CustomMinimumSize = new Vector2(52, 26), SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
                string capturedName = name;
                enableButton.Pressed += () => { toEnable.Add(capturedName); toDisable.Remove(capturedName); };
                removeButton.Pressed += () => { toAdd.Remove(capturedName); toRemove.Add(capturedName); };
                row.AddChild(enableButton);
                row.AddChild(removeButton);
                dialogVBox.AddChild(row);
            }
        }

        private static void BuildAvailableComponentList(
            VBoxContainer dialogVBox,
            List<(string name, string displayName)> available,
            EntityProfile profile,
            HashSet<string> toRemove,
            HashSet<string> toAdd,
            Action refreshList)
        {
            if (available.Count <= 0) return;

            dialogVBox.AddChild(new HSeparator());
            var addRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            addRow.AddChild(new Label { Text = "添加", CustomMinimumSize = new Vector2(40, 0) });
            var addOption = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            foreach (var (name, displayName) in available)
            {
                int index = addOption.GetItemCount();
                addOption.AddItem(displayName);
                addOption.SetItemMetadata(index, name);
            }
            addRow.AddChild(addOption);

            var addButton = new Button { Text = "+", CustomMinimumSize = new Vector2(32, 26) };
            addButton.Pressed += () =>
            {
                int index = addOption.Selected;
                if (index < 0 || index >= addOption.GetItemCount()) return;
                string capturedName = (string)addOption.GetItemMetadata(index);
                toRemove.Remove(capturedName);
                if (!profile.HasComponent(capturedName))
                    toAdd.Add(capturedName);
                refreshList();
            };
            addRow.AddChild(addButton);
            dialogVBox.AddChild(addRow);
        }

        #endregion

        #region Cross-Panel Navigation

        private void OnEnterEditorPressed()
        {
            var mapEditor = Owner.GetTree()?.GetFirstNodeInGroup("map_editor") as MapEditor;
            if (mapEditor == null)
            {
                ShowWarning("未找到地图编辑器节点。");
                return;
            }

            mapEditor.SetEditMode(true);
            mapEditor.CurrentTool = MapEditor.EditorTool.PlaceDecoration;
            mapEditor.RefreshToolContent();
            Owner.Visible = false;
        }

        #endregion
    }
}
