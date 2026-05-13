using Godot;
using System.Collections.Generic;
using System.Linq;

namespace ClinetCSharp
{
    public partial class DebugPanelEntityTab
    {
        public override void SyncToCurrentValues()
        {
            var profileManager = EntityProfileManager.Instance;
            if (profileManager == null)
                return;

            var profile = profileManager.GetProfile(_currentProfileId);
            if (profile == null)
                return;

            foreach (var kv in _activeComponents)
            {
                var data = profile.GetData(kv.Key);
                if (data != null)
                    kv.Value.SyncFromData(data);
            }
        }

        public override void SaveConfig(ConfigFile cfg)
        {
            SaveCurrentProfileData();
            cfg.SetValue("entity_tab", "current_profile_id", _currentProfileId);
            cfg.SetValue("entity_tab", "show_all_components", _showAllComponents);
            EntityProfileManager.Instance?.WriteProfileConfig(cfg);
        }

        public override void LoadConfig(ConfigFile cfg, bool configLoaded)
        {
            if (!configLoaded)
                return;

            var profileManager = EntityProfileManager.Instance;
            if (profileManager == null)
                return;

            int savedId = (int)(double)cfg.GetValue("entity_tab", "current_profile_id", -1.0);
            _showAllComponents = (bool)cfg.GetValue("entity_tab", "show_all_components", false);
            if (savedId > 0 && profileManager.GetProfile(savedId) != null)
                _currentProfileId = savedId;
            else
                _currentProfileId = profileManager.GetAllProfiles().FirstOrDefault()?.Id ?? -1;

            RefreshProfileList();
            RefreshComponents();
            SyncProfileNameEdit();
        }

        public override Godot.Collections.Dictionary CaptureUndoState()
        {
            SaveCurrentProfileData();

            _undoProfileId = _currentProfileId;
            _undoComponentSnapshots.Clear();

            var profile = EntityProfileManager.Instance?.GetProfile(_currentProfileId);
            if (profile != null)
            {
                foreach (string componentName in profile.ComponentNames)
                {
                    var data = profile.GetData(componentName);
                    if (data != null)
                        _undoComponentSnapshots[componentName] = data.Clone();
                }
            }

            var state = new Godot.Collections.Dictionary();
            state["profile_id"] = _currentProfileId;
            return state;
        }

        public override void ApplyUndoState(Godot.Collections.Dictionary state)
        {
            if (_undoProfileId < 0)
                return;

            var profileManager = EntityProfileManager.Instance;
            if (profileManager == null)
                return;

            if (profileManager.GetProfile(_undoProfileId) != null)
            {
                _currentProfileId = _undoProfileId;
                RefreshProfileList();
            }

            var profile = profileManager.GetProfile(_currentProfileId);
            if (profile != null)
            {
                foreach (var kv in _undoComponentSnapshots)
                    profile.SetData(kv.Key, kv.Value.Clone());
            }

            RefreshComponents();
            SyncProfileNameEdit();
            if (_currentProfileId > 0)
                profileManager.ApplyProfileToAll(_currentProfileId);
        }

        public override Godot.Collections.Dictionary ExportConfigData()
        {
            var data = new Godot.Collections.Dictionary();
            var profileManager = EntityProfileManager.Instance;
            var profile = profileManager?.GetProfile(_currentProfileId);
            if (profile == null)
                return data;

            data["profile_id"] = profile.Id;
            data["profile_name"] = profile.Name;
            data["entity_type"] = profile.EntityType;
            data["component_names"] = string.Join(",", profile.ComponentNames);
            return data;
        }
    }
}
