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
        }

        private void OnAddProfilePressed()
        {
            _newProfileNameEdit.Text = "新配置";
            _newProfileTypeOption.Select(0);
            _newProfileDialog.PopupCentered(new Vector2I(320, 150));
            _newProfileNameEdit.GrabFocus();
            _newProfileNameEdit.SelectAll();
        }

        private void OnNewProfileConfirmed()
        {
            string name = _newProfileNameEdit.Text.StripEdges();
            if (string.IsNullOrEmpty(name))
                name = "新配置";

            string entityType = _newProfileTypeOption.Selected switch
            {
                0 => "player",
                1 => "monster",
                2 => "npc",
                _ => "player",
            };

            var profileManager = EntityProfileManager.Instance;
            if (profileManager == null)
                return;

            SaveCurrentProfileData();
            var profile = profileManager.CreateProfile(name, entityType);
            foreach (var (componentName, _) in ComponentRegistry.GetComponentsForType(entityType))
            {
                var component = ComponentRegistry.Create(componentName);
                if (component == null)
                    continue;

                profile.SetData(componentName, component.SyncToData());
                component.Dispose();
            }

            _currentProfileId = profile.Id;
            RefreshProfileList();
            RefreshComponents();
            SyncProfileNameEdit();
            profileManager.ApplyProfileToAll(_currentProfileId);
        }

        private void OnDeleteProfilePressed()
        {
            var profileManager = EntityProfileManager.Instance;
            if (profileManager == null)
                return;

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

            _deleteProfileDialog.DialogText = $"确定要删除配置“{profile.Name}” (ID: {profile.Id}) 吗？";
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
            foreach (var profile in profileManager.GetAllProfiles())
            {
                int index = _profileOption.GetItemCount();
                _profileOption.AddItem($"{profile.Name} ({profile.Id})");
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
    }
}
