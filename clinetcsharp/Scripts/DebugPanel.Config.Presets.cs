using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    public partial class DebugPanel
    {
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
                GD.Print("[DebugPanel] Selected default preset, no need to load");
                return;
            }

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
            AcceptDialog dialog = new AcceptDialog();
            dialog.Title = "新建预设";
            dialog.DialogText = "请输入新预设名称:";

            LineEdit input = new LineEdit();
            input.Name = "PresetInput";
            input.PlaceholderText = "预设名称";
            input.Text = $"预设_{Time.GetUnixTimeFromSystem()}";
            input.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            input.CustomMinimumSize = new Vector2(200, 0);

            dialog.AddChild(input);

            dialog.Confirmed += () =>
            {
                string presetName = input.Text.StripEdges();
                if (string.IsNullOrEmpty(presetName))
                    presetName = $"预设_{Time.GetUnixTimeFromSystem()}";

                SavePreset(presetName);
                RefreshPresetList();

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

            dialog.Canceled += () => dialog.QueueFree();

            AddChild(dialog);
            dialog.PopupCentered(new Vector2I(300, 120));
            input.GrabFocus();
            input.SelectAll();
        }

        private void OnDeletePresetPressed()
        {
            int index = _presetOption.Selected;
            if (index <= 0)
            {
                AcceptDialog warningDialog = new AcceptDialog();
                warningDialog.Title = "提示";
                warningDialog.DialogText = "默认预设无法删除";
                warningDialog.Confirmed += () => warningDialog.QueueFree();
                AddChild(warningDialog);
                warningDialog.PopupCentered();
                return;
            }

            string presetName = _presetOption.GetItemMetadata(index).AsString();
            if (string.IsNullOrEmpty(presetName))
                return;

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

            dialog.Canceled += () => dialog.QueueFree();

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

            ConfigFile currentConfig = BuildCurrentConfigSnapshot();
            foreach (string section in currentConfig.GetSections())
            {
                foreach (string key in currentConfig.GetSectionKeys(section))
                {
                    Variant value = currentConfig.GetValue(section, key);
                    presets.SetValue(presetName, $"{section}/{key}", value);
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

            ConfigFile config = BuildCurrentConfigSnapshot();
            foreach (string key in presets.GetSectionKeys(presetName))
            {
                Variant value = presets.GetValue(presetName, key);
                string[] parts = key.Split('/');
                if (parts.Length == 2)
                    config.SetValue(parts[0], parts[1], value);
            }

            config.SetValue("meta", "config_version", CONFIG_VERSION);

            err = config.Save(CONFIG_PATH);
            if (err == Error.Ok)
            {
                GD.Print($"[DebugPanel] Preset '{presetName}' loaded");
                LoadConfig();
                return true;
            }

            GD.PushError($"Failed to apply preset: {err}");
            return false;
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

            ConfigFile newPresets = new ConfigFile();

            if (presets.HasSection("__presets__"))
            {
                foreach (string key in presets.GetSectionKeys("__presets__"))
                {
                    if (key != presetName)
                        newPresets.SetValue("__presets__", key, true);
                }
            }

            foreach (string section in presets.GetSections())
            {
                if (section == presetName || section == "__presets__")
                    continue;

                foreach (string key in presets.GetSectionKeys(section))
                {
                    Variant value = presets.GetValue(section, key);
                    newPresets.SetValue(section, key, value);
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
                    GD.PushError($"[DebugPanel] Failed to read preset list: {err}");
                return new List<string>();
            }

            List<string> list = new List<string>();
            if (presets.HasSection("__presets__"))
            {
                foreach (string key in presets.GetSectionKeys("__presets__"))
                {
                    if (!string.IsNullOrEmpty(key))
                        list.Add(key);
                }
            }

            GD.Print($"[DebugPanel] Current preset list: [{string.Join(", ", list)}]");
            return list;
        }
    }
}
