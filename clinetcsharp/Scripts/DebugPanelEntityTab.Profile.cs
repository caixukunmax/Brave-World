using Godot;
using System.Linq;

namespace ClinetCSharp
{
    public partial class DebugPanelEntityTab
    {
        private void OnProfileOptionSelected(long index)
        {
            if (index < 0 || index >= _profileOption.GetItemCount())
                return;

            int newId = (int)_profileOption.GetItemMetadata((int)index);
            if (newId == _currentProfileId)
                return;

            SaveCurrentProfileData();
            _currentProfileId = newId;
            RefreshComponents();
            SyncProfileNameEdit();

            // 如果预览已打开，实时切换为新配置的预览
            if (_previewEntity != null && GodotObject.IsInstanceValid(_previewEntity))
            {
                var profileManager = EntityProfileManager.Instance;
                var profile = profileManager?.GetProfile(_currentProfileId);
                if (profileManager != null && profile != null)
                {
                    // 如果类型不匹配，重新创建实体以确保和地图算法一致
                    //（创建/判定统一走 EntityPreviewFactory，禁止在此重复 switch）
                    if (!EntityPreviewFactory.TypeMatches(profile, _previewEntity))
                    {
                        _previewPanel?.ClearPreviewEntity();
                        _previewEntity = null;
                        _previewEntity = EntityPreviewFactory.CreatePreviewEntity(profile);
                    }

                    profileManager.ApplyProfile(_previewEntity, _currentProfileId);
                    _previewPanel?.SetPreviewEntity(_previewEntity);
                    _previewEntity?.RefreshLabels();
                }

                if (_previewPanel != null && profile != null)
                {
                    _previewPanel.UpdateInfo(profile.Name, profile.EntityType, profile.Id);
                }
            }
        }

        private void OnAddProfilePressed()
        {
            _newProfileNameEdit.Text = "新配置";

            // 刷新参考模板列表
            _newProfileTemplateOption.Clear();
            var profileManager = EntityProfileManager.Instance;
            if (profileManager != null)
            {
                foreach (var profile in profileManager.GetAllProfiles())
                {
                    int index = _newProfileTemplateOption.GetItemCount();
                    string prefix = profileManager.IsDefaultProfile(profile.Id) ? "[默认] " : "";
                    _newProfileTemplateOption.AddItem($"{prefix}{profile.Name}");
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
                name = "新配置";

            var profileManager = EntityProfileManager.Instance;
            if (profileManager == null)
                return;

            // 获取选中的参考模板ID
            int selectedIndex = _newProfileTemplateOption.Selected;
            if (selectedIndex < 0 || selectedIndex >= _newProfileTemplateOption.GetItemCount())
                return;

            int templateId = (int)_newProfileTemplateOption.GetItemMetadata(selectedIndex);

            SaveCurrentProfileData();
            var profile = profileManager.CreateProfileFromTemplate(templateId, name);
            if (profile == null)
                return;

            _currentProfileId = profile.Id;
            RefreshProfileList();
            RefreshComponents();
            SyncProfileNameEdit();
            profileManager.ApplyProfileToAll(_currentProfileId);

            // 立即保存配置，确保新模板持久化
            profileManager.SaveConfig();
        }

        private void OnDeleteProfilePressed()
        {
            var profileManager = EntityProfileManager.Instance;
            if (profileManager == null)
                return;

            if (profileManager.IsDefaultProfile(_currentProfileId))
            {
                var warning = new AcceptDialog
                {
                    Title = "提示",
                    DialogText = "默认模板（玩家、怪物、NPC）不可删除。",
                };
                warning.Confirmed += () => warning.QueueFree();
                warning.Canceled += () => warning.QueueFree();
                Owner.AddChild(warning);
                warning.PopupCentered();
                return;
            }

            if (profileManager.GetAllProfiles().Count() <= 1)
            {
                var warning = new AcceptDialog
                {
                    Title = "提示",
                    DialogText = "至少需要保留一个配置。",
                };
                warning.Confirmed += () => warning.QueueFree();
                warning.Canceled += () => warning.QueueFree();
                Owner.AddChild(warning);
                warning.PopupCentered();
                return;
            }

            var profile = profileManager.GetProfile(_currentProfileId);
            if (profile == null)
                return;

            _deleteProfileDialog.DialogText = $"确定要删除配置\"{profile.Name}\" (ID: {profile.Id}) 吗？";
            _deleteProfileDialog.PopupCentered();
        }

        private void OnDeleteProfileConfirmed()
        {
            var profileManager = EntityProfileManager.Instance;
            if (profileManager == null)
                return;

            profileManager.DeleteProfile(_currentProfileId);
            _currentProfileId = profileManager.GetAllProfiles().FirstOrDefault()?.Id ?? -1;
            RefreshProfileList();
            RefreshComponents();
            SyncProfileNameEdit();
            if (_currentProfileId > 0)
                profileManager.ApplyProfileToAll(_currentProfileId);

            // 保存配置，确保删除操作持久化
            profileManager.SaveConfig();
        }

        private void OnProfileNameChanged(string newName)
        {
            if (_isRefreshing)
                return;

            var profileManager = EntityProfileManager.Instance;
            var profile = profileManager?.GetProfile(_currentProfileId);
            if (profile == null)
                return;

            profile.Name = newName;
            RefreshProfileList();
        }

        private void RefreshProfileList()
        {
            var profileManager = EntityProfileManager.Instance;
            if (profileManager == null)
                return;

            _profileOption.Clear();
            int selectedIndex = -1;

            // 先添加默认模板
            var defaultProfiles = profileManager.GetAllProfiles()
                .Where(p => profileManager.IsDefaultProfile(p.Id))
                .OrderBy(p => p.Id);
            foreach (var profile in defaultProfiles)
            {
                int index = _profileOption.GetItemCount();
                _profileOption.AddItem($"[默认] {profile.Name}");
                _profileOption.SetItemMetadata(index, profile.Id);
                if (profile.Id == _currentProfileId)
                    selectedIndex = index;
            }

            // 再添加自定义模板
            var customProfiles = profileManager.GetAllProfiles()
                .Where(p => !profileManager.IsDefaultProfile(p.Id))
                .OrderBy(p => p.Id);
            foreach (var profile in customProfiles)
            {
                int index = _profileOption.GetItemCount();
                _profileOption.AddItem($"{profile.Name} (ID:{profile.Id})");
                _profileOption.SetItemMetadata(index, profile.Id);
                if (profile.Id == _currentProfileId)
                    selectedIndex = index;
            }

            if (selectedIndex >= 0)
                _profileOption.Select(selectedIndex);
            else if (_profileOption.GetItemCount() > 0)
                _profileOption.Select(0);
        }

        private void SyncProfileNameEdit()
        {
            var profile = EntityProfileManager.Instance?.GetProfile(_currentProfileId);
            _isRefreshing = true;
            _profileNameEdit.Text = profile?.Name ?? "";
            _profileNameEdit.Editable = profile != null;
            _isRefreshing = false;
        }

        #region Preview Entity

        private void OnPreviewPressed()
        {
            var profileManager = EntityProfileManager.Instance;
            var profile = profileManager?.GetProfile(_currentProfileId);
            if (profile == null)
                return;

            // 销毁旧预览
            DestroyPreviewEntity();

            // 创建与地图算法一致的预览实体（统一走 EntityPreviewFactory，内部已完成 ApplyProfile）
            _previewEntity = EntityPreviewFactory.CreatePreviewEntity(profile);

            // 显示/更新面板
            if (_previewPanel == null)
            {
                _previewPanel = new EntityProfilePreviewPanel
                {
                    MinWidth = 340,
                    MinHeight = 320,
                };
                _previewPanel.OnClosePreview = DestroyPreviewEntity;
                Owner.AddChild(_previewPanel);
            }
            _previewPanel.SetPreviewEntity(_previewEntity);
            _previewEntity.RefreshLabels();
            _previewPanel.UpdateInfo(profile.Name, profile.EntityType, profile.Id);
            _previewPanel.Visible = true;
            _previewPanel.GlobalPosition = new Vector2(100, 100);
        }

        private void DestroyPreviewEntity()
        {
            _previewPanel?.ClearPreviewEntity();
            _previewEntity = null;
            if (_previewPanel != null)
            {
                _previewPanel.Visible = false;
            }
        }

        /// <summary>当模板数据变化时，同步更新预览实体</summary>
        private void SyncPreviewEntity()
        {
            if (_previewEntity == null || !GodotObject.IsInstanceValid(_previewEntity))
                return;

            var profileManager = EntityProfileManager.Instance;
            if (profileManager == null)
                return;

            profileManager.ApplyProfile(_previewEntity, _currentProfileId);

            // 重新适配预览实体位置。ApplyProfile 内部可能因尺寸(SizeX/SizeY)变化触发
            // OnGridSizeChanged，它会把多格建筑/装饰的 Position 重置回初始 (0,0) 网格锚点，
            // 导致实体偏出预览相机视野而“看不见”。此处借助 SetPreviewEntity 的同实例分支
            // (ApplyEntityPreviewState) 把实体重新居中到预览地图中心，避免偏移。
            _previewPanel?.SetPreviewEntity(_previewEntity);
        }



        #endregion
    }
}
