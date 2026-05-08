using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// DebugPanel partial — 配置保存/加载/预设操作
    /// Tab-specific config is delegated to each tab's SaveConfig/LoadConfig.
    /// </summary>
    public partial class DebugPanel
    {
        #region Preset Operations
        private void OnPresetSelected(long index)
        {
            GD.Print($"[DebugPanel] Selected preset index: {index}");

            if (index < 0 || index >= _presetOption.ItemCount)
            {
                GD.PushError($"[DebugPanel] Invalid preset index: {index}");
                return;
            }

            string presetName = _presetOption.GetItemMetadata((int)index).AsString();
            GD.Print($"[DebugPanel] Selected preset name: '{presetName}'");

            if (string.IsNullOrEmpty(presetName))
            {
                // Selected "Default", no need to load
                GD.Print("[DebugPanel] Selected default preset, no need to load");
                return;
            }

            // Load preset (don't refresh list here to avoid resetting selection state)
            if (LoadPreset(presetName))
            {
                GD.Print($"[DebugPanel] Preset '{presetName}' loaded successfully");
            }
            else
            {
                GD.PushError($"[DebugPanel] Failed to load preset '{presetName}'");
            }
        }

        private void OnSavePresetPressed()
        {
            // Create temporary dialog
            AcceptDialog dialog = new AcceptDialog();
            dialog.Title = "新建预设";
            dialog.DialogText = "请输入新预设名称:";

            // Add input box
            LineEdit input = new LineEdit();
            input.Name = "PresetInput";
            input.PlaceholderText = "预设名称";
            input.Text = $"预设_{Time.GetUnixTimeFromSystem()}";
            input.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            input.CustomMinimumSize = new Vector2(200, 0);

            // Add to dialog
            dialog.AddChild(input);

            // Confirm callback
            dialog.Confirmed += () =>
            {
                string presetName = input.Text.StripEdges();
                if (string.IsNullOrEmpty(presetName))
                {
                    presetName = $"预设_{Time.GetUnixTimeFromSystem()}";
                }

                SavePreset(presetName);
                RefreshPresetList();

                // Select the newly saved preset
                for (int i = 0; i < _presetOption.ItemCount; i++)
                {
                    if (_presetOption.GetItemMetadata(i).AsString() == presetName)
                    {
                        _presetOption.Select(i);
                        break;
                    }
                }

                GD.Print($"[DebugPanel] Created new preset: {presetName}");
                dialog.QueueFree();
            };

            // Cleanup on cancel
            dialog.Canceled += () =>
            {
                dialog.QueueFree();
            };

            AddChild(dialog);
            dialog.PopupCentered(new Vector2I(300, 120));
            input.GrabFocus();
            input.SelectAll();
        }

        private void OnDeletePresetPressed()
        {
            int index = _presetOption.Selected;

            // Cannot delete "Default"
            if (index <= 0)
            {
                AcceptDialog warningDialog = new AcceptDialog();
                warningDialog.Title = "提示";
                warningDialog.DialogText = "默认预设无法删除";
                warningDialog.Confirmed += () =>
                {
                    warningDialog.QueueFree();
                };
                AddChild(warningDialog);
                warningDialog.PopupCentered();
                return;
            }

            string presetName = _presetOption.GetItemMetadata(index).AsString();
            if (string.IsNullOrEmpty(presetName))
                return;

            // Show confirmation dialog
            ConfirmationDialog dialog = new ConfirmationDialog();
            dialog.Title = "确认删除";
            dialog.DialogText = $"确定要删除预设 '{presetName}' 吗？";

            dialog.Confirmed += () =>
            {
                if (DeletePreset(presetName))
                {
                    GD.Print($"[DebugPanel] Preset '{presetName}' deleted");
                    RefreshPresetList();
                    _presetOption.Select(0);
                }
                else
                {
                    GD.PushError($"[DebugPanel] Failed to delete preset '{presetName}'");
                }
                dialog.QueueFree();
            };

            dialog.Canceled += () =>
            {
                dialog.QueueFree();
            };

            AddChild(dialog);
            dialog.PopupCentered();
        }

        private void SavePreset(string presetName)
        {
            ConfigFile presets = new ConfigFile();
            Error err = presets.Load(PRESET_PATH);
            if (err != Error.Ok && err != Error.FileNotFound)
            {
                GD.PushError($"Failed to load preset file: {err}");
                return;
            }

            // Save current config to specified preset
            // Dynamic: save all sections except __presets__
            ConfigFile currentConfig = new ConfigFile();
            Error currentErr = currentConfig.Load(CONFIG_PATH);

            if (currentErr == Error.Ok)
            {
                // Save all sections
                foreach (string section in currentConfig.GetSections())
                {
                    foreach (string key in currentConfig.GetSectionKeys(section))
                    {
                        Variant value = currentConfig.GetValue(section, key);
                        presets.SetValue(presetName, $"{section}/{key}", value);
                    }
                }
            }

            presets.SetValue("__presets__", presetName, true);

            err = presets.Save(PRESET_PATH);
            if (err == Error.Ok)
            {
                GD.Print($"[DebugPanel] Preset '{presetName}' saved");
            }
            else
            {
                GD.PushError($"Failed to save preset: {err}");
            }
        }

        private bool LoadPreset(string presetName)
        {
            ConfigFile presets = new ConfigFile();
            Error err = presets.Load(PRESET_PATH);
            if (err != Error.Ok)
            {
                GD.PushWarning("Preset file doesn't exist or cannot be loaded");
                return false;
            }

            if (!presets.HasSection(presetName))
            {
                GD.PushWarning($"Preset '{presetName}' doesn't exist");
                return false;
            }

            ConfigFile config = new ConfigFile();
            foreach (string key in presets.GetSectionKeys(presetName))
            {
                Variant value = presets.GetValue(presetName, key);
                string[] parts = key.Split('/');
                if (parts.Length == 2)
                {
                    config.SetValue(parts[0], parts[1], value);
                }
            }

            // Keep config_version
            config.SetValue("meta", "config_version", CONFIG_VERSION);

            err = config.Save(CONFIG_PATH);
            if (err == Error.Ok)
            {
                GD.Print($"[DebugPanel] Preset '{presetName}' loaded");
                LoadConfig();
                return true;
            }
            else
            {
                GD.PushError($"Failed to apply preset: {err}");
                return false;
            }
        }

        private bool DeletePreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName))
            {
                GD.PushError("[DebugPanel] Failed to delete preset: preset name is empty");
                return false;
            }

            ConfigFile presets = new ConfigFile();
            Error err = presets.Load(PRESET_PATH);
            if (err != Error.Ok && err != Error.FileNotFound)
            {
                GD.PushError($"[DebugPanel] Failed to load preset file: {err}");
                return false;
            }

            if (!presets.HasSection(presetName))
            {
                GD.PushError($"[DebugPanel] Failed to delete preset: preset '{presetName}' doesn't exist");
                return false;
            }

            // Delete preset section
            // Note: ConfigFile has no direct delete section method, implement by not saving that section
            ConfigFile newPresets = new ConfigFile();

            // Copy __presets__ section except the key to delete
            int remainingPresets = 0;
            if (presets.HasSection("__presets__"))
            {
                foreach (string key in presets.GetSectionKeys("__presets__"))
                {
                    if (key != presetName)
                    {
                        newPresets.SetValue("__presets__", key, true);
                        remainingPresets++;
                    }
                }
            }

            GD.Print($"[DebugPanel] Deleting preset '{presetName}', remaining presets: {remainingPresets}");

            // Copy other preset data sections
            foreach (string section in presets.GetSections())
            {
                if (section != presetName && section != "__presets__")
                {
                    foreach (string key in presets.GetSectionKeys(section))
                    {
                        Variant value = presets.GetValue(section, key);
                        newPresets.SetValue(section, key, value);
                    }
                }
            }

            err = newPresets.Save(PRESET_PATH);
            if (err != Error.Ok)
            {
                GD.PushError($"[DebugPanel] Failed to save preset file: {err}");
                return false;
            }

            return true;
        }

        private List<string> GetPresetList()
        {
            ConfigFile presets = new ConfigFile();
            Error err = presets.Load(PRESET_PATH);
            if (err != Error.Ok)
            {
                if (err != Error.FileNotFound)
                {
                    GD.PushError($"[DebugPanel] Failed to read preset list: {err}");
                }
                return new List<string>();
            }

            List<string> list = new List<string>();
            if (presets.HasSection("__presets__"))
            {
                foreach (string key in presets.GetSectionKeys("__presets__"))
                {
                    if (!string.IsNullOrEmpty(key))  // Filter empty strings
                    {
                        list.Add(key);
                    }
                }
            }

            GD.Print($"[DebugPanel] Current preset list: [{string.Join(", ", list)}]");
            return list;
        }
        #endregion

        #region Config Save/Load — delegates to tabs
        protected override void SavePosition()
        {
            SavePanelGeometry();
        }

        private void SavePanelGeometry()
        {
            ConfigFile config = new ConfigFile();
            Error err = config.Load(CONFIG_PATH);
            if (err != Error.Ok && err != Error.FileNotFound)
            {
                GD.PushError($"[DebugPanel] Failed to load config before saving panel geometry: {err}");
                return;
            }

            config.SetValue("meta", "config_version", CONFIG_VERSION);
            WritePanelGeometry(config);

            err = config.Save(CONFIG_PATH);
            if (err != Error.Ok)
                GD.PushError($"[DebugPanel] Failed to save panel geometry: {err}");
        }

        private void WritePanelGeometry(ConfigFile config)
        {
            float persistedHeight = IsMinimized ? NormalHeight : Size.Y;

            config.SetValue("panel_geo", "offset_left", Position.X);
            config.SetValue("panel_geo", "offset_top", Position.Y);
            config.SetValue("panel_geo", "offset_right", Position.X + Size.X);
            config.SetValue("panel_geo", "offset_bottom", Position.Y + persistedHeight);
        }

        internal void SaveConfigFromTab() => SaveConfig();

        private void SaveConfig()
        {
            ConfigFile config = new ConfigFile();

            // Metadata
            config.SetValue("meta", "config_version", CONFIG_VERSION);
            config.SetValue("meta", "last_save_time", Time.GetDatetimeStringFromSystem());

            // Delegate to each tab for tab-specific config
            foreach (var tab in _tabs)
                tab.SaveConfig(config);

            // Panel geometry (size/position) — keep legacy keys for compatibility
            WritePanelGeometry(config);

            Error err = config.Save(CONFIG_PATH);
            if (err == Error.Ok)
            {
                GD.Print("[DebugPanel] Config saved");
            }
            else
            {
                GD.PushError($"Failed to save config: {err}");
            }
        }

        private void LoadConfig()
        {
            if (_player == null)
            {
                _player = GetTree().GetFirstNodeInGroup("player") as Player;
                if (_player == null) return;
            }

            ConfigFile config = new ConfigFile();
            Error err = config.Load(CONFIG_PATH);
            bool configLoaded = (err == Error.Ok);

            if (!configLoaded)
            {
                if (err == Error.FileNotFound)
                {
                    GD.Print("[DebugPanel] Config file doesn't exist, using default settings");
                }
                else
                {
                    GD.PushError($"[DebugPanel] Failed to load config: {err}, using defaults");
                }
                // 使用默认值，不返回，继续执行应用设置
            }
            else
            {
                // Check version
                int savedVersion = (int)config.GetValue("meta", "config_version", 0);
                if (savedVersion < CONFIG_VERSION)
                {
                    GD.Print($"[DebugPanel] Config version upgraded from {savedVersion} to {CONFIG_VERSION}");
                    MigrateConfig(config, savedVersion);
                }

                // Load panel geometry (legacy offset_* keys mapped to root position/size)
                if (config.HasSection("panel_geo"))
                {
                    float left = (float)(double)config.GetValue("panel_geo", "offset_left", (double)Position.X);
                    float top = (float)(double)config.GetValue("panel_geo", "offset_top", (double)Position.Y);
                    float right = (float)(double)config.GetValue("panel_geo", "offset_right", (double)(Position.X + Size.X));
                    float bottom = (float)(double)config.GetValue("panel_geo", "offset_bottom", (double)(Position.Y + Size.Y));

                    RestorePosition(left, top, Mathf.Max(250.0f, right - left), Mathf.Max(200.0f, bottom - top));
                    GD.Print("[DebugPanel] Restored panel geometry");
                }
            }

            // Delegate to each tab for tab-specific config loading
            foreach (var tab in _tabs)
                tab.LoadConfig(config, configLoaded);

            UpdateControlStates();
        }

        private void MigrateConfig(ConfigFile config, int fromVersion)
        {
            // Config migration: upgrade from old version to current version
            if (fromVersion < 1)
            {
                // v0 -> v1: Initial version, no special migration needed
            }
            if (fromVersion < 2)
            {
                // v1 -> v2: Migrate [monster] -> [monster_1], [npc] -> [npc_1]
                if (config.HasSection("monster"))
                {
                    foreach (string key in config.GetSectionKeys("monster"))
                    {
                        Variant value = config.GetValue("monster", key);
                        config.SetValue("monster_1", key, value);
                    }
                    config.EraseSection("monster");
                    GD.Print("[DebugPanel] Migrated [monster] -> [monster_1]");
                }
                if (config.HasSection("npc"))
                {
                    foreach (string key in config.GetSectionKeys("npc"))
                    {
                        Variant value = config.GetValue("npc", key);
                        config.SetValue("npc_1", key, value);
                    }
                    config.EraseSection("npc");
                    GD.Print("[DebugPanel] Migrated [npc] -> [npc_1]");
                }
            }
            // v3 -> v4: 比例类字段从 double 转为定点整数
            // （与 EntityProfileManager.MigrateConfig 相同逻辑，双重保险）
            if (fromVersion < 4)
            {
                var scaleKeys = new HashSet<string>
                {
                    "visual_size_scale", "border_width_scale", "bg_opacity",
                    "length_scale", "height_scale", "fill_percent",
                    "hp_bar_length_scale", "hp_bar_height_scale", "hp_bar_fill_percent",
                    "mp_bar_length_scale", "mp_bar_height_scale", "mp_bar_fill_percent",
                };
                foreach (string section in config.GetSections())
                {
                    foreach (string key in config.GetSectionKeys(section))
                    {
                        if (!scaleKeys.Contains(key)) continue;
                        var v = config.GetValue(section, key, 0);
                        if (v.VariantType == Variant.Type.Float)
                            config.SetValue(section, key, EntityProfileManager.ToFpD((double)v));
                    }
                }
                GD.Print("[DebugPanel] Migrated scale values to fixed-point format (v3→v4)");
            }
        }

        private void ResetConfigAndReload()
        {
            GD.Print("[DebugPanel] Resetting config to defaults");
            ConfigFile config = new ConfigFile();
            config.SetValue("meta", "config_version", CONFIG_VERSION);
            config.SetValue("meta", "last_save_time", Time.GetDatetimeStringFromSystem());
            Error err = config.Save(CONFIG_PATH);
            if (err != Error.Ok)
            {
                GD.PushError($"[DebugPanel] Failed to reset config: {err}");
            }
            LoadConfig();
        }
        #endregion

        #region Export Config to JSON — delegates to tabs
        public string ExportConfigToJson()
        {
            var data = new Godot.Collections.Dictionary();

            data["timestamp"] = Time.GetDatetimeStringFromSystem();
            data["config_version"] = CONFIG_VERSION;

            // Delegate to each tab for tab-specific data
            foreach (var tab in _tabs)
            {
                var tabData = tab.ExportConfigData();
                if (tabData != null)
                    data[tab.TabKey] = tabData;
            }

            return Json.Stringify(data, "  ");
        }
        #endregion
    }
}