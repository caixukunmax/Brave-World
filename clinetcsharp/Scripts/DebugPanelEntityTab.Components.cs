using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinetCSharp
{
    public partial class DebugPanelEntityTab
    {
        private void OnManageComponentsPressed()
        {
            var profileManager = EntityProfileManager.Instance;
            var profile = profileManager?.GetProfile(_currentProfileId);
            if (profile == null)
                return;

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

            void RefreshList()
            {
                foreach (var child in dialogVBox.GetChildren())
                {
                    if (child is Node node)
                        node.QueueFree();
                }

                var advancedModeCheck = new CheckBox
                {
                    Text = "显示全部组件（高级模式）",
                    ButtonPressed = _showAllComponents,
                };
                var advancedHint = new Label
                {
                    Text = "高级模式会显示当前实体类型白名单之外的实验组件。",
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    Modulate = new Color(0.82f, 0.72f, 0.42f),
                    Visible = _showAllComponents,
                };

                dialogVBox.AddChild(advancedModeCheck);
                dialogVBox.AddChild(advancedHint);

                advancedModeCheck.Toggled += enabled =>
                {
                    _showAllComponents = enabled;
                    advancedHint.Visible = enabled;
                    RefreshList();
                };

                var allComponents = BuildComponentCatalog(profile);
                var enabled = new List<(string name, string displayName)>();
                var disabled = new List<(string name, string displayName)>();
                var available = new List<(string name, string displayName)>();

                foreach (var (name, displayName) in allComponents)
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

                BuildEnabledComponentList(dialogVBox, enabled, toDisable, toEnable, toRemove, toAdd, RefreshList);
                BuildDisabledComponentList(dialogVBox, disabled, toDisable, toEnable, toRemove, toAdd, RefreshList);
                BuildAvailableComponentList(dialogVBox, available, profile, toRemove, toAdd, RefreshList);
            }

            RefreshList();

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
                    var component = ComponentRegistry.Create(name);
                    if (component != null)
                    {
                        // 必须先 BuildUI，否则 SyncToData 会访问未初始化的控件。
                        // tempContainer 无需加入场景树：Godot 控件属性（Slider.Value 等）
                        // 在脱离场景树时也可正常读写，各组件 BuildUI 均不依赖 _Ready。
                        // QueueFree 会释放 tempContainer 及其子节点，信号订阅随之断开。
                        var tempContainer = new VBoxContainer();
                        component.BuildUI(tempContainer);
                        profile.SetData(name, component.SyncToData());
                        component.Dispose();
                        tempContainer.QueueFree();
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
                if (changed)
                {
                    profileManager.ApplyProfileToAll(_currentProfileId);
                    // 组件结构变更属于低频但重要的操作，立即落盘，避免重启后丢失
                    profileManager.SaveConfig();
                }
            };
            _manageComponentsDialog.Confirmed += _manageComponentsHandler;

            _manageComponentsDialog.PopupCentered(new Vector2I(360, 0));
        }

        private List<(string name, string displayName)> BuildComponentCatalog(EntityProfile profile)
        {
            var allowed = ComponentRegistry.GetComponentsForType(profile.EntityType).ToList();
            var catalog = new List<(string name, string displayName)>(allowed);

            foreach (string componentName in profile.ComponentNames)
            {
                if (catalog.Any(x => x.name == componentName))
                    continue;

                // profile 已有但不在 allowed 白名单中的组件也标记为"实验"，与高级模式下的标记保持一致
                bool isExperimental = !allowed.Any(x => x.name == componentName);
                string resolved = ResolveComponentDisplayName(componentName);
                string displayName = isExperimental ? $"{resolved}（实验）" : resolved;
                catalog.Add((componentName, displayName));
            }

            if (!_showAllComponents)
                return catalog;

            foreach (var component in ComponentRegistry.GetAllComponents())
            {
                if (catalog.Any(x => x.name == component.name))
                    continue;

                bool isExperimental = !allowed.Any(x => x.name == component.name);
                string displayName = isExperimental
                    ? $"{component.displayName}（实验）"
                    : component.displayName;
                catalog.Add((component.name, displayName));
            }

            return catalog;
        }

        private static string ResolveComponentDisplayName(string componentName)
        {
            return ComponentRegistry.GetAllComponents()
                       .FirstOrDefault(c => c.name == componentName).displayName
                   ?? componentName;
        }

        private static void BuildEnabledComponentList(
            VBoxContainer dialogVBox,
            List<(string name, string displayName)> enabled,
            HashSet<string> toDisable,
            HashSet<string> toEnable,
            HashSet<string> toRemove,
            HashSet<string> toAdd,
            Action refreshList)
        {
            if (enabled.Count <= 0)
                return;

            dialogVBox.AddChild(new Label { Text = "已启用", HorizontalAlignment = HorizontalAlignment.Left });
            foreach (var (name, displayName) in enabled)
            {
                var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                row.AddChild(new Label { Text = "*", CustomMinimumSize = new Vector2(20, 0), HorizontalAlignment = HorizontalAlignment.Center });
                row.AddChild(new Label { Text = displayName, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

                var disableButton = new Button { Text = "停用", CustomMinimumSize = new Vector2(52, 26), SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
                var removeButton = new Button { Text = "移除", CustomMinimumSize = new Vector2(52, 26), SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
                string capturedName = name;
                disableButton.Pressed += () => { toDisable.Add(capturedName); toEnable.Remove(capturedName); refreshList(); };
                removeButton.Pressed += () => { toAdd.Remove(capturedName); toRemove.Add(capturedName); refreshList(); };
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
            HashSet<string> toAdd,
            Action refreshList)
        {
            if (disabled.Count <= 0)
                return;

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
                enableButton.Pressed += () => { toEnable.Add(capturedName); toDisable.Remove(capturedName); refreshList(); };
                removeButton.Pressed += () => { toAdd.Remove(capturedName); toRemove.Add(capturedName); refreshList(); };
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
            if (available.Count <= 0)
                return;

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
                if (index < 0 || index >= addOption.GetItemCount())
                    return;

                string capturedName = (string)addOption.GetItemMetadata(index);
                toRemove.Remove(capturedName);
                if (!profile.HasComponent(capturedName))
                    toAdd.Add(capturedName);
                refreshList();
            };
            addRow.AddChild(addButton);
            dialogVBox.AddChild(addRow);
        }

        private void OnRemoveComponentPressed(string componentName)
        {
            _pendingDeleteComponentName = componentName;
            string displayName = ResolveComponentDisplayName(componentName);
            _deleteComponentDialog.DialogText = $"确定要删除组件“{displayName}”吗？";
            _deleteComponentDialog.PopupCentered();
        }

        private void OnDeleteComponentConfirmed()
        {
            if (string.IsNullOrEmpty(_pendingDeleteComponentName))
                return;

            var profileManager = EntityProfileManager.Instance;
            var profile = profileManager?.GetProfile(_currentProfileId);
            if (profile == null)
                return;

            string componentName = _pendingDeleteComponentName;
            _pendingDeleteComponentName = null;
            profile.RemoveComponent(componentName);
            RemoveComponentUI(componentName);
            profileManager.ApplyProfileToAll(_currentProfileId);
            profileManager.SaveConfig();
        }

        private void OnComponentChanged()
        {
            if (_isRefreshing)
                return;

            var profileManager = EntityProfileManager.Instance;
            var profile = profileManager?.GetProfile(_currentProfileId);
            if (profile == null)
            {
                GD.PrintErr($"[EntityTab] OnComponentChanged: pm={profileManager?.GetType().Name}, profileId={_currentProfileId}, profile=null");
                return;
            }

            foreach (var kv in _activeComponents)
                profile.SetData(kv.Key, kv.Value.SyncToData());

            profileManager.ApplyProfileToAll(_currentProfileId);
            SyncPreviewEntity();
            // 防抖落盘：拖动 slider 期间不会频繁写盘，停止操作 1 秒后自动保存
            ScheduleSave();
        }

        private void SaveCurrentProfileData()
        {
            if (_currentProfileId < 0)
                return;

            var profileManager = EntityProfileManager.Instance;
            var profile = profileManager?.GetProfile(_currentProfileId);
            if (profile == null)
                return;

            foreach (var kv in _activeComponents)
                profile.SetData(kv.Key, kv.Value.SyncToData());
        }

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
            if (profile == null)
                return;

            var component = ComponentRegistry.Create(componentName);
            if (component == null)
                return;

            var meta = ComponentRegistry.GetComponentMeta(componentName);
            var container = new CollapsibleContainer(meta.DisplayName);
            container.SetAccentColor(meta.AccentColor);
            container.SetHeaderIcon(meta.Icon);

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

        private void RemoveComponentUI(string componentName)
        {
            if (_activeComponents.TryGetValue(componentName, out var component))
            {
                component.DisconnectSignals();
                component.Dispose();
                _activeComponents.Remove(componentName);
            }

            if (_componentContainers.TryGetValue(componentName, out var container))
            {
                _componentContainers.Remove(componentName);
                if (container != null && GodotObject.IsInstanceValid(container))
                    container.QueueFree();
            }
        }

        private void OnEntityClicked(EntityBase entity)
        {
            if (!Owner.IsVisibleInTree())
                return;

            if (entity.ProfileId <= 0)
                return;

            SaveCurrentProfileData();
            _currentProfileId = entity.ProfileId;
            RefreshProfileList();
            RefreshComponents();
            SyncProfileNameEdit();

            // 注意：这里不调用 SyncFromEntity。
            // 调试面板是 Profile 配置编辑器，UI 必须始终代表 Profile 配置数据，
            // 而非实体的运行时瞬时状态（如血量、施法进度）。
            // 若用 SyncFromEntity 把实体真值塞进 UI，后续切换 Profile 时
            // SaveCurrentProfileData 会把实体瞬时状态误存为配置，污染 Profile。
            // SyncFromEntity 仅用于服务端推送等"读实体真值"场景，不在此调用。
        }
    }
}
