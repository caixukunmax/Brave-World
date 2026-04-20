using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// DebugPanel partial — 配置保存/加载/预设
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
            string[] sections = { "meta", "map", "player", "calibration", "responsive", "editor", "panel_geo" };
            ConfigFile currentConfig = new ConfigFile();
            Error currentErr = currentConfig.Load(CONFIG_PATH);

            if (currentErr == Error.Ok)
            {
                foreach (string section in sections)
                {
                    if (currentConfig.HasSection(section))
                    {
                        foreach (string key in currentConfig.GetSectionKeys(section))
                        {
                            Variant value = currentConfig.GetValue(section, key);
                            presets.SetValue(presetName, $"{section}/{key}", value);
                        }
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

        #region Config Save/Load
        private void SaveConfig()
        {
            ConfigFile config = new ConfigFile();

            // Metadata
            config.SetValue("meta", "config_version", CONFIG_VERSION);
            config.SetValue("meta", "last_save_time", Time.GetDatetimeStringFromSystem());

            // Map settings
            config.SetValue("map", "grid_size", _gridSizeSlider.Value);
            config.SetValue("map", "zoom", _zoomSlider.Value);
            config.SetValue("map", "grid_line_width", _gridLineWidthSlider.Value);
            config.SetValue("map", "grid_line_brightness", _gridLineBrightnessSlider.Value);
            config.SetValue("map", "show_grid_coords", _gridCoordsCheck.ButtonPressed);

            // Player settings
            config.SetValue("player", "player_size", _playerSizeSlider.Value);
            config.SetValue("player", "visual_size_scale", _playerSizeScaleSlider?.Value ?? 1.0);
            GD.Print($"[DebugPanel] Saving player size: {_playerSizeSlider.Value}");
            config.SetValue("player", "border_width", _borderWidthSlider.Value);
            config.SetValue("player", "border_width_scale", _borderWidthScaleSlider?.Value ?? (3.0 / 111.0));
            config.SetValue("player", "corner_radius", _cornerRadiusSlider.Value);
            config.SetValue("player", "bg_opacity", _bgOpacitySlider.Value);
            config.SetValue("player", "font_size", _fontSizeSlider.Value);
            config.SetValue("player", "line_spacing", _lineSpacingSlider.Value);
            config.SetValue("player", "letter_spacing", _letterSpacingSlider.Value);
            config.SetValue("player", "text_alignment", _player.TextAlignment);
            config.SetValue("player", "font_bold", _boldCheck.ButtonPressed);
            config.SetValue("player", "font_italic", _italicCheck.ButtonPressed);
            config.SetValue("player", "font_shadow", _shadowCheck.ButtonPressed);
            config.SetValue("player", "font_auto_size", _fontAutoSizeCheck?.ButtonPressed ?? false);

            // Camera settings
            config.SetValue("camera", "return_delay", _cameraReturnDelaySlider.Value);
            config.SetValue("camera", "return_speed", _cameraReturnSpeedSlider.Value);
            config.SetValue("camera", "ease_type", _cameraEaseTypeOption.Selected);
            config.SetValue("camera", "ease_power", _cameraEasePowerSlider.Value);
            config.SetValue("camera", "free_look", _freeLookCheck?.ButtonPressed ?? false);

            // Line width calibration settings
            if (_lineWidthScaleSlider != null)
                config.SetValue("map", "line_width_scale", _lineWidthScaleSlider.Value);
            config.SetValue("calibration", "enabled", _calibrationEnabled);
            if (_refZoomASpin != null)
            {
                config.SetValue("calibration", "ref_zoom_a", _refZoomASpin.Value);
                config.SetValue("calibration", "ref_width_a", _refWidthASpin.Value);
                config.SetValue("calibration", "ref_zoom_b", _refZoomBSpin.Value);
                config.SetValue("calibration", "ref_width_b", _refWidthBSpin.Value);
            }

            // Responsive layout settings
            if (_responsiveCheck != null)
            {
                config.SetValue("responsive", "enabled", _responsiveCheck.ButtonPressed);
                config.SetValue("responsive", "visible_grids_x", _visibleGridsXSpin.Value);
            }

            // Editor key settings
            if (_editorDragButtonOption != null)
            {
                config.SetValue("editor", "drag_button", _editorDragButtonOption.Selected);
                config.SetValue("editor", "require_ctrl_for_selection", _editorSelectModCheck?.ButtonPressed ?? true);
            }

            // Debug settings
            config.SetValue("debug", "show_debug_info", _debugInfoCheck.ButtonPressed);

            // Label control settings (4 independent labels)
            if (_player != null)
            {
                bool autoCenterX = _player.LabelAutoCenterX;
                config.SetValue("labels", "auto_center_x", autoCenterX);
                for (int i = 0; i < LabelCount; i++)
                {
                    string prefix = $"label_{i}";
                    config.SetValue("labels", $"{prefix}_visible", _labelVisibleChecks[i]?.ButtonPressed ?? true);
                    config.SetValue("labels", $"{prefix}_name", _player.GetLabelName(i));
                    config.SetValue("labels", $"{prefix}_text", _player.GetLabelText(i));
                    config.SetValue("labels", $"{prefix}_font_size", _labelFontSizeSliders[i]?.Value ?? 0);
                    var offset = _player.GetLabelOffset(i);
                    config.SetValue("labels", $"{prefix}_offset_x", (double)offset.X);
                    config.SetValue("labels", $"{prefix}_offset_y", (double)offset.Y);
                    var lineColors = _player.LineColors;
                    if (i < lineColors.Count)
                    {
                        var c = lineColors[i];
                        config.SetValue("labels", $"{prefix}_color_r", c.R);
                        config.SetValue("labels", $"{prefix}_color_g", c.G);
                        config.SetValue("labels", $"{prefix}_color_b", c.B);
                        config.SetValue("labels", $"{prefix}_color_a", c.A);
                    }
                }
            }

            // Health bar settings
            if (_player != null)
            {
                var hpOffset = _player.GetHealthBarOffset();
                config.SetValue("healthbar", "visible", _healthBarVisibleCheck?.ButtonPressed ?? true);
                config.SetValue("healthbar", "length", _healthBarLengthSlider?.Value ?? 80);
                config.SetValue("healthbar", "length_scale", _healthBarLengthScaleSlider?.Value ?? (80.0 / 111.0));
                config.SetValue("healthbar", "height", _healthBarHeightSlider?.Value ?? 6);
                config.SetValue("healthbar", "height_scale", _healthBarHeightScaleSlider?.Value ?? (6.0 / 111.0));
                config.SetValue("healthbar", "fill", _healthBarFillSlider?.Value ?? 100);
                config.SetValue("healthbar", "offset_x", (double)hpOffset.X);
                config.SetValue("healthbar", "offset_y", (double)hpOffset.Y);
                var hpColor = _player.HealthBarColor;
                config.SetValue("healthbar", "color_r", hpColor.R);
                config.SetValue("healthbar", "color_g", hpColor.G);
                config.SetValue("healthbar", "color_b", hpColor.B);
            }

            // Cast bar settings
            if (_player != null)
            {
                var ctOffset = _player.GetCastBarOffset();
                config.SetValue("castbar", "visible", _castBarVisibleCheck?.ButtonPressed ?? true);
                config.SetValue("castbar", "length", _castBarLengthSlider?.Value ?? 60);
                config.SetValue("castbar", "height", _castBarHeightSlider?.Value ?? 4);
                config.SetValue("castbar", "fill", _castBarFillSlider?.Value ?? 60);
                config.SetValue("castbar", "offset_x", (double)ctOffset.X);
                config.SetValue("castbar", "offset_y", (double)ctOffset.Y);
                var ctColor = _player.CastBarColor;
                config.SetValue("castbar", "color_r", ctColor.R);
                config.SetValue("castbar", "color_g", ctColor.G);
                config.SetValue("castbar", "color_b", ctColor.B);
            }

            // Level badge settings
            if (_player != null)
            {
                var lvOffset = _player.GetLevelBadgeOffset();
                config.SetValue("levelbadge", "visible", _levelBadgeVisibleCheck?.ButtonPressed ?? true);
                config.SetValue("levelbadge", "font_size", _levelBadgeFontSizeSlider?.Value ?? 12);
                config.SetValue("levelbadge", "text", _player.LevelBadgeText);
                config.SetValue("levelbadge", "offset_x", (double)lvOffset.X);
                config.SetValue("levelbadge", "offset_y", (double)lvOffset.Y);
                var lvTxtColor = _player.LevelBadgeTextColor;
                config.SetValue("levelbadge", "txt_r", lvTxtColor.R);
                config.SetValue("levelbadge", "txt_g", lvTxtColor.G);
                config.SetValue("levelbadge", "txt_b", lvTxtColor.B);
            }

            // Monster settings
            {
                config.SetValue("monster", "visual_size", _monsterSizeSlider?.Value ?? 111);
                config.SetValue("monster", "visual_size_scale", _monsterSizeScaleSlider?.Value ?? 1.0);
                config.SetValue("monster", "border_width", _monsterBorderWidthSlider?.Value ?? 3.0);
                config.SetValue("monster", "border_width_scale", _monsterBorderWidthScaleSlider?.Value ?? (3.0 / 111.0));
                config.SetValue("monster", "corner_radius", _monsterCornerRadiusSlider?.Value ?? 12.0);
                config.SetValue("monster", "bg_opacity", _monsterBgOpacitySlider?.Value ?? 0.9);
                config.SetValue("monster", "font_size", _monsterFontSizeSlider?.Value ?? 0);
                var mBorderColor = _monsterBorderColorPicker?.Color ?? new Color(0.9f, 0.3f, 0.3f);
                config.SetValue("monster", "border_color_r", mBorderColor.R);
                config.SetValue("monster", "border_color_g", mBorderColor.G);
                config.SetValue("monster", "border_color_b", mBorderColor.B);
                var mBgColor = _monsterBgColorPicker?.Color ?? new Color(0.8f, 0.2f, 0.2f);
                config.SetValue("monster", "bg_color_r", mBgColor.R);
                config.SetValue("monster", "bg_color_g", mBgColor.G);
                config.SetValue("monster", "bg_color_b", mBgColor.B);
                var mTextColor = _monsterTextColorPicker?.Color ?? new Color(1, 0.95f, 0.95f);
                config.SetValue("monster", "text_color_r", mTextColor.R);
                config.SetValue("monster", "text_color_g", mTextColor.G);
                config.SetValue("monster", "text_color_b", mTextColor.B);
                for (int i = 0; i < 4; i++)
                {
                    string prefix = $"monster_label_{i}";
                    config.SetValue("monster", $"{prefix}_text", _monsterLabelEdits[i]?.Text ?? "");
                    config.SetValue("monster", $"{prefix}_font_size", _monsterLabelFontSizeSliders[i]?.Value ?? 0);
                    config.SetValue("monster", $"{prefix}_offset_x", _monsterLabelXOffsetSliders[i]?.Value ?? 0);
                    config.SetValue("monster", $"{prefix}_center_x", _monsterLabelCenterXChecks[i]?.ButtonPressed ?? true);
                    config.SetValue("monster", $"{prefix}_offset_y", _monsterLabelYOffsetSliders[i]?.Value ?? 0);
                }
            }

            // Panel geometry (size/position)
            if (_panel != null)
            {
                config.SetValue("panel_geo", "offset_left", _panel.OffsetLeft);
                config.SetValue("panel_geo", "offset_top", _panel.OffsetTop);
                config.SetValue("panel_geo", "offset_right", _panel.OffsetRight);
                config.SetValue("panel_geo", "offset_bottom", _panel.OffsetBottom);
            }

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

        private async void LoadConfig()
        {
            GD.Print($"[DebugPanel] Starting load config... player={_player}, grid_manager={_gridManager}");

            // If player not ready, delay retry
            if (_player == null)
            {
                GD.Print("[DebugPanel] Player not ready, delaying config load...");
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                _player = GetTree().GetFirstNodeInGroup("player") as Player;
                if (_player == null)
                {
                    GD.Print("[DebugPanel] Player still doesn't exist, skipping config load");
                    return;
                }
                GD.Print($"[DebugPanel] Player obtained: {_player}");
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

                // Load panel geometry
                if (config.HasSection("panel_geo") && _panel != null)
                {
                    _panel.OffsetLeft   = (float)(double)config.GetValue("panel_geo", "offset_left",   (double)_panel.OffsetLeft);
                    _panel.OffsetTop    = (float)(double)config.GetValue("panel_geo", "offset_top",    (double)_panel.OffsetTop);
                    _panel.OffsetRight  = (float)(double)config.GetValue("panel_geo", "offset_right",  (double)_panel.OffsetRight);
                    _panel.OffsetBottom = (float)(double)config.GetValue("panel_geo", "offset_bottom", (double)_panel.OffsetBottom);
                    GD.Print("[DebugPanel] Restored panel geometry");
                }

                // Load map settings from config
                double loadedGridSize = (double)config.GetValue("map", "grid_size", 111);
                if (loadedGridSize < 32 || loadedGridSize > 256)
                {
                    GD.PushError($"[DebugPanel] Invalid grid_size in config: {loadedGridSize}, resetting config");
                    ResetConfigAndReload();
                    return;
                }

                _gridSizeSlider.SetBlockSignals(true);
                _zoomSlider.SetBlockSignals(true);
                _gridLineWidthSlider.SetBlockSignals(true);
                _gridLineBrightnessSlider.SetBlockSignals(true);

                _gridSizeSlider.Value = loadedGridSize;
                _zoomSlider.Value = (double)config.GetValue("map", "zoom", 1.0);
                _gridLineWidthSlider.Value = (double)config.GetValue("map", "grid_line_width", 2.0);
                _gridLineBrightnessSlider.Value = (double)config.GetValue("map", "grid_line_brightness", 0.7);
                _gridCoordsCheck.ButtonPressed = (bool)config.GetValue("map", "show_grid_coords", false);

                _gridSizeSlider.SetBlockSignals(false);
                _zoomSlider.SetBlockSignals(false);
                _gridLineWidthSlider.SetBlockSignals(false);
                _gridLineBrightnessSlider.SetBlockSignals(false);
            }

            GD.Print($"[DebugPanel] Final grid_size slider value: {_gridSizeSlider.Value}");

            // Apply map settings
            if (_gridManager != null)
            {
                _gridManager.SetGridSize((int)_gridSizeSlider.Value);
                _gridManager.SetLineWidthScale((float)_gridLineWidthSlider.Value);
                float brightness = (float)_gridLineBrightnessSlider.Value;
                _gridManager.SetLineBrightness(brightness);
                _gridManager.ShowGridCoords = _gridCoordsCheck.ButtonPressed;
                GD.Print($"[DebugPanel] Applied settings to GridManager: grid_size={_gridSizeSlider.Value}, line_width={_gridLineWidthSlider.Value}");
            }

            if (_camera != null)
            {
                _camera.Zoom = Vector2.One * (float)_zoomSlider.Value;
            }

            // Load player settings (block signals to prevent triggering updates)
            double savedPlayerSize = (double)config.GetValue("player", "player_size", 111);
            GD.Print($"[DebugPanel] Loading player size: {savedPlayerSize}");
            _playerSizeSlider.SetBlockSignals(true);
            _playerSizeSlider.Value = savedPlayerSize;
            _playerSizeSlider.SetBlockSignals(false);

            _playerSizeScaleSlider?.SetBlockSignals(true);
            double loadedVisualScale = (double)config.GetValue("player", "visual_size_scale", 1.0);
            _playerSizeScaleSlider?.SetValue(loadedVisualScale);
            _playerSizeScaleSlider?.SetBlockSignals(false);
            if (_playerSizeScaleValue != null)
                _playerSizeScaleValue.Text = loadedVisualScale.ToString("F2");

            _borderWidthSlider.SetBlockSignals(true);
            _borderWidthSlider.Value = (double)config.GetValue("player", "border_width", 3.0);
            _borderWidthSlider.SetBlockSignals(false);

            _borderWidthScaleSlider?.SetBlockSignals(true);
            double loadedScale = (double)config.GetValue("player", "border_width_scale", 3.0 / 111.0);
            _borderWidthScaleSlider?.SetValue(loadedScale);
            _borderWidthScaleSlider?.SetBlockSignals(false);
            if (_borderWidthScaleValue != null)
                _borderWidthScaleValue.Text = loadedScale.ToString("F2");

            _cornerRadiusSlider.SetBlockSignals(true);
            _cornerRadiusSlider.Value = (double)config.GetValue("player", "corner_radius", 0.0);
            _cornerRadiusSlider.SetBlockSignals(false);

            _bgOpacitySlider.SetBlockSignals(true);
            _bgOpacitySlider.Value = (double)config.GetValue("player", "bg_opacity", 0.1);
            _bgOpacitySlider.SetBlockSignals(false);

            _fontSizeSlider.SetBlockSignals(true);
            _fontSizeSlider.Value = (double)config.GetValue("player", "font_size", 0);
            _fontSizeSlider.SetBlockSignals(false);

            _lineSpacingSlider.SetBlockSignals(true);
            _lineSpacingSlider.Value = (double)config.GetValue("player", "line_spacing", 0.8);
            _lineSpacingSlider.SetBlockSignals(false);

            _letterSpacingSlider.SetBlockSignals(true);
            _letterSpacingSlider.Value = (double)config.GetValue("player", "letter_spacing", 0.0);
            _letterSpacingSlider.SetBlockSignals(false);

            _boldCheck.ButtonPressed = (bool)config.GetValue("player", "font_bold", false);
            _italicCheck.ButtonPressed = (bool)config.GetValue("player", "font_italic", false);
            _shadowCheck.ButtonPressed = (bool)config.GetValue("player", "font_shadow", false);

            if (_fontAutoSizeCheck != null)
            {
                _fontAutoSizeCheck.ButtonPressed = (bool)config.GetValue("player", "font_auto_size", false);
                OnFontAutoSizeToggled(_fontAutoSizeCheck.ButtonPressed);
            }

            if (!(_fontAutoSizeCheck != null && _fontAutoSizeCheck.ButtonPressed))
            {
                if (_player != null)
                {
                    _player.SetFontSize((int)_fontSizeSlider.Value);
                }
            }

            // Update display labels (force immediate refresh)
            _playerSizeValue.Text = ((int)_playerSizeSlider.Value).ToString();
            _playerSizeValue.QueueRedraw();
            _borderWidthValue.Text = ((int)_borderWidthSlider.Value).ToString();
            _borderWidthValue.QueueRedraw();
            _cornerRadiusValue.Text = ((int)_cornerRadiusSlider.Value).ToString();
            _cornerRadiusValue.QueueRedraw();
            _bgOpacityValue.Text = $"{_bgOpacitySlider.Value:F2}";
            _bgOpacityValue.QueueRedraw();
            _fontSizeValue.Text = ((int)_fontSizeSlider.Value).ToString();
            _fontSizeValue.QueueRedraw();
            _lineSpacingValue.Text = $"{_lineSpacingSlider.Value:F1}";
            _lineSpacingValue.QueueRedraw();
            _letterSpacingValue.Text = ((int)_letterSpacingSlider.Value).ToString();
            _letterSpacingValue.QueueRedraw();

            HorizontalAlignment savedAlignment = (HorizontalAlignment)(int)config.GetValue("player", "text_alignment", (int)HorizontalAlignment.Center);
            GD.Print($"[DebugPanel] Preparing to apply player settings, player exists: {_player != null}");
            if (_player != null)
            {
                _player.SetTextAlignment((int)savedAlignment);
                _player.SetVisualSizeScale((float)(_playerSizeScaleSlider?.Value ?? 1.0));
                _player.SetBorderWidthScale((float)(_borderWidthScaleSlider?.Value ?? (3.0 / 111.0)));
                _player.SetCornerRadius((float)_cornerRadiusSlider.Value);
                _player.SetBgOpacity((float)_bgOpacitySlider.Value);
                _player.SetLineSpacing((float)_lineSpacingSlider.Value);
                _player.SetLetterSpacing((float)_letterSpacingSlider.Value);
                _player.SetFontBold(_boldCheck.ButtonPressed);
                _player.SetFontItalic(_italicCheck.ButtonPressed);
                _player.SetFontShadow(_shadowCheck.ButtonPressed);
                // Player settings will be applied in ApplyLoadedPlayerSettings() with delay
                GD.Print("[DebugPanel] Player settings will be applied after delay");
            }

            // Load camera settings
            _cameraReturnDelaySlider.Value = (double)config.GetValue("camera", "return_delay", 0.5);
            _cameraReturnSpeedSlider.Value = (double)config.GetValue("camera", "return_speed", 5.0);
            _cameraEaseTypeOption.Select((int)config.GetValue("camera", "ease_type", 3));
            _cameraEasePowerSlider.Value = (double)config.GetValue("camera", "ease_power", 2.0);

            if (_camera != null)
            {
                _camera.SetReturnDelay((float)_cameraReturnDelaySlider.Value);
                _camera.SetReturnSpeed((float)_cameraReturnSpeedSlider.Value);
                _camera.SetEaseType((int)_cameraEaseTypeOption.Selected);
                _camera.SetEasePower((float)_cameraEasePowerSlider.Value);
            }

            if (_freeLookCheck != null)
            {
                _freeLookCheck.ButtonPressed = (bool)config.GetValue("camera", "free_look", false);
                OnFreeLookToggled(_freeLookCheck.ButtonPressed);
            }

            // Load line width calibration settings
            if (_lineWidthScaleSlider != null)
            {
                double savedLineWidth = (double)config.GetValue("map", "line_width_scale", 2.0);
                _lineWidthScaleSlider.SetBlockSignals(true);
                _lineWidthScaleSlider.Value = savedLineWidth;
                _lineWidthScaleSlider.SetBlockSignals(false);
            }

            _calibrationEnabled = (bool)config.GetValue("calibration", "enabled", false);
            if (_applyCalibrationBtn != null && _gridManager != null)
            {
                _applyCalibrationBtn.ButtonPressed = _calibrationEnabled;
                _gridManager.SetAdaptiveCalibrationEnabled(_calibrationEnabled);
            }

            if (_refZoomASpin != null)
            {
                _refZoomASpin.Value = (double)config.GetValue("calibration", "ref_zoom_a", 0.2);  // Default consistent with new min_zoom
                _refWidthASpin.Value = (double)config.GetValue("calibration", "ref_width_a", 3.0);  // Adjust default line width
                _refZoomBSpin.Value = (double)config.GetValue("calibration", "ref_zoom_b", 1.0);
                _refWidthBSpin.Value = (double)config.GetValue("calibration", "ref_width_b", 1.5);
                if (_calibrationEnabled)
                    ApplyCalibrationValues();
            }

            // Load responsive layout settings
            if (_responsiveCheck != null && _gridManager != null)
            {
                _responsiveCheck.ButtonPressed = (bool)config.GetValue("responsive", "enabled", false);
                _visibleGridsXSpin.Value = (double)config.GetValue("responsive", "visible_grids_x", 5.0);
                if (_responsiveCheck.ButtonPressed)
                {
                    _gridManager.VisibleGridsX = (float)_visibleGridsXSpin.Value;
                    _gridManager.SetResponsiveMode(true);
                }
                OnResponsiveToggled(_responsiveCheck.ButtonPressed);
            }

            // Load editor key settings
            if (_editorDragButtonOption != null)
            {
                _editorDragButtonOption.Select((int)config.GetValue("editor", "drag_button", 0));
                OnEditorDragButtonChanged(_editorDragButtonOption.Selected);
            }
            if (_editorSelectModCheck != null)
            {
                _editorSelectModCheck.ButtonPressed = (bool)config.GetValue("editor", "require_ctrl_for_selection", true);
                OnEditorSelectModChanged(_editorSelectModCheck.ButtonPressed);
            }

            // Load debug settings
            _debugInfoCheck.ButtonPressed = (bool)config.GetValue("debug", "show_debug_info", false);
            OnDebugInfoToggled(_debugInfoCheck.ButtonPressed);

            // Load label control settings
            if (_player != null)
            {
                bool autoCenterX = (bool)config.GetValue("labels", "auto_center_x", false);
                _player.SetLabelAutoCenterX(autoCenterX);
                if (_labelAutoCenterXCheck != null)
                {
                    _labelAutoCenterXCheck.SetBlockSignals(true);
                    _labelAutoCenterXCheck.ButtonPressed = autoCenterX;
                    _labelAutoCenterXCheck.SetBlockSignals(false);
                }
                for (int i = 0; i < LabelCount; i++)
                {
                    string prefix = $"label_{i}";
                    bool visible = (bool)config.GetValue("labels", $"{prefix}_visible", true);
                    string name = (string)config.GetValue("labels", $"{prefix}_name", new[] { "名称", "职业", "称号", "状态" }[i]);
                    string text = (string)config.GetValue("labels", $"{prefix}_text", "");
                    double fontSize = (double)config.GetValue("labels", $"{prefix}_font_size", 0);
                    double offsetX = (double)config.GetValue("labels", $"{prefix}_offset_x", Player.DefaultOffsets[i].X);
                    double offsetY = (double)config.GetValue("labels", $"{prefix}_offset_y", Player.DefaultOffsets[i].Y);

                    // Apply to player
                    _player.SetLabelName(i, name);
                    if (!string.IsNullOrEmpty(text)) _player.SetLabelText(i, text);
                    _player.SetLabelVisible(i, visible);
                    _player.SetLabelFontSize(i, (int)fontSize);
                    _player.SetLabelOffset(i, new Vector2((float)offsetX, (float)offsetY));

                    // Load color
                    float cr = (float)(double)config.GetValue("labels", $"{prefix}_color_r", 0.0);
                    float cg = (float)(double)config.GetValue("labels", $"{prefix}_color_g", 0.0);
                    float cb = (float)(double)config.GetValue("labels", $"{prefix}_color_b", 0.0);
                    float ca = (float)(double)config.GetValue("labels", $"{prefix}_color_a", 1.0);
                    _player.SetLineColor(i, new Color(cr, cg, cb, ca));

                    // Update UI controls
                    if (_labelVisibleChecks[i] != null)
                    {
                        _labelVisibleChecks[i].SetBlockSignals(true);
                        _labelVisibleChecks[i].ButtonPressed = visible;
                        _labelVisibleChecks[i].SetBlockSignals(false);
                    }
                    if (_labelNameEdits[i] != null)
                        _labelNameEdits[i].Text = name;
                    if (_labelTextEdits[i] != null && !string.IsNullOrEmpty(text))
                        _labelTextEdits[i].Text = text;
                    if (_labelFontSizeSliders[i] != null)
                    {
                        _labelFontSizeSliders[i].SetBlockSignals(true);
                        _labelFontSizeSliders[i].Value = fontSize;
                        _labelFontSizeSliders[i].SetBlockSignals(false);
                    }
                    if (_labelFontSizeValues[i] != null)
                        _labelFontSizeValues[i].Text = fontSize > 0 ? ((int)fontSize).ToString() : "自动";
                    if (_labelColorButtons[i] != null)
                        _labelColorButtons[i].Modulate = new Color(cr, cg, cb, ca);
                    if (_labelOffsetXSliders[i] != null)
                    {
                        _labelOffsetXSliders[i].SetBlockSignals(true);
                        _labelOffsetXSliders[i].Value = offsetX;
                        _labelOffsetXSliders[i].SetBlockSignals(false);
                    }
                    if (_labelOffsetYSliders[i] != null)
                    {
                        _labelOffsetYSliders[i].SetBlockSignals(true);
                        _labelOffsetYSliders[i].Value = offsetY;
                        _labelOffsetYSliders[i].SetBlockSignals(false);
                    }
                    if (_labelOffsetXValues[i] != null)
                        _labelOffsetXValues[i].Text = ((int)offsetX).ToString();
                    if (_labelOffsetYValues[i] != null)
                        _labelOffsetYValues[i].Text = ((int)offsetY).ToString();
                }
                _player.RefreshLabels();
            }

            // Load health bar settings
            {
                bool hpVisible = (bool)config.GetValue("healthbar", "visible", true);
                double hpLength = (double)config.GetValue("healthbar", "length", 80);
                double hpLengthScale = (double)config.GetValue("healthbar", "length_scale", 80.0 / 111.0);
                double hpHeight = (double)config.GetValue("healthbar", "height", 6);
                double hpHeightScale = (double)config.GetValue("healthbar", "height_scale", 6.0 / 111.0);
                double hpFill = (double)config.GetValue("healthbar", "fill", 100);
                double hpOffX = (double)config.GetValue("healthbar", "offset_x", 0);
                double hpOffY = (double)config.GetValue("healthbar", "offset_y", -70);
                float hpR = (float)(double)config.GetValue("healthbar", "color_r", 0.0);
                float hpG = (float)(double)config.GetValue("healthbar", "color_g", 0.8);
                float hpB = (float)(double)config.GetValue("healthbar", "color_b", 0.0);

                if (_player != null)
                {
                    _player.SetHealthBarVisible(hpVisible);
                    _player.SetHealthBarLength((float)hpLength);
                    _player.SetHealthBarHeight((float)hpHeight);
                    _player.SetHealthBarFillPercent((float)(hpFill / 100.0));
                    _player.SetHealthBarOffset(new Vector2((float)hpOffX, (float)hpOffY));
                    _player.SetHealthBarColor(new Color(hpR, hpG, hpB));
                }

                if (_healthBarVisibleCheck != null)
                {
                    _healthBarVisibleCheck.SetBlockSignals(true);
                    _healthBarVisibleCheck.ButtonPressed = hpVisible;
                    _healthBarVisibleCheck.SetBlockSignals(false);
                }
                if (_healthBarLengthSlider != null) { _healthBarLengthSlider.SetBlockSignals(true); _healthBarLengthSlider.Value = hpLength; _healthBarLengthSlider.SetBlockSignals(false); }
                if (_healthBarLengthScaleSlider != null) { _healthBarLengthScaleSlider.SetBlockSignals(true); _healthBarLengthScaleSlider.Value = hpLengthScale; _healthBarLengthScaleSlider.SetBlockSignals(false); }
                if (_healthBarLengthScaleValue != null) _healthBarLengthScaleValue.Text = hpLengthScale.ToString("F2");
                if (_healthBarHeightSlider != null) { _healthBarHeightSlider.SetBlockSignals(true); _healthBarHeightSlider.Value = hpHeight; _healthBarHeightSlider.SetBlockSignals(false); }
                if (_healthBarHeightScaleSlider != null) { _healthBarHeightScaleSlider.SetBlockSignals(true); _healthBarHeightScaleSlider.Value = hpHeightScale; _healthBarHeightScaleSlider.SetBlockSignals(false); }
                if (_healthBarHeightScaleValue != null) _healthBarHeightScaleValue.Text = hpHeightScale.ToString("F2");
                if (_healthBarFillSlider != null) { _healthBarFillSlider.SetBlockSignals(true); _healthBarFillSlider.Value = hpFill; _healthBarFillSlider.SetBlockSignals(false); }
                if (_healthBarOffsetXSlider != null) { _healthBarOffsetXSlider.SetBlockSignals(true); _healthBarOffsetXSlider.Value = hpOffX; _healthBarOffsetXSlider.SetBlockSignals(false); }
                if (_healthBarOffsetYSlider != null) { _healthBarOffsetYSlider.SetBlockSignals(true); _healthBarOffsetYSlider.Value = hpOffY; _healthBarOffsetYSlider.SetBlockSignals(false); }
                if (_healthBarLengthValue != null) _healthBarLengthValue.Text = ((int)hpLength).ToString();
                if (_healthBarHeightValue != null) _healthBarHeightValue.Text = ((int)hpHeight).ToString();
                if (_healthBarFillValue != null) _healthBarFillValue.Text = $"{(int)hpFill}%";
                if (_healthBarOffsetXValue != null) _healthBarOffsetXValue.Text = ((int)hpOffX).ToString();
                if (_healthBarOffsetYValue != null) _healthBarOffsetYValue.Text = ((int)hpOffY).ToString();
                if (_healthBarColorBtn != null) _healthBarColorBtn.Modulate = new Color(hpR, hpG, hpB);
            }

            // Load cast bar settings
            {
                bool ctVisible = (bool)config.GetValue("castbar", "visible", true);
                double ctLength = (double)config.GetValue("castbar", "length", 60);
                double ctHeight = (double)config.GetValue("castbar", "height", 4);
                double ctFill = (double)config.GetValue("castbar", "fill", 60);
                double ctOffX = (double)config.GetValue("castbar", "offset_x", 0);
                double ctOffY = (double)config.GetValue("castbar", "offset_y", -80);
                float ctR = (float)(double)config.GetValue("castbar", "color_r", 0.3);
                float ctG = (float)(double)config.GetValue("castbar", "color_g", 0.5);
                float ctB = (float)(double)config.GetValue("castbar", "color_b", 1.0);

                if (_player != null)
                {
                    _player.SetCastBarVisible(ctVisible);
                    _player.SetCastBarLength((float)ctLength);
                    _player.SetCastBarHeight((float)ctHeight);
                    _player.SetCastBarFillPercent((float)(ctFill / 100.0));
                    _player.SetCastBarOffset(new Vector2((float)ctOffX, (float)ctOffY));
                    _player.SetCastBarColor(new Color(ctR, ctG, ctB));
                }

                if (_castBarVisibleCheck != null) { _castBarVisibleCheck.SetBlockSignals(true); _castBarVisibleCheck.ButtonPressed = ctVisible; _castBarVisibleCheck.SetBlockSignals(false); }
                if (_castBarLengthSlider != null) { _castBarLengthSlider.SetBlockSignals(true); _castBarLengthSlider.Value = ctLength; _castBarLengthSlider.SetBlockSignals(false); }
                if (_castBarHeightSlider != null) { _castBarHeightSlider.SetBlockSignals(true); _castBarHeightSlider.Value = ctHeight; _castBarHeightSlider.SetBlockSignals(false); }
                if (_castBarFillSlider != null) { _castBarFillSlider.SetBlockSignals(true); _castBarFillSlider.Value = ctFill; _castBarFillSlider.SetBlockSignals(false); }
                if (_castBarOffsetXSlider != null) { _castBarOffsetXSlider.SetBlockSignals(true); _castBarOffsetXSlider.Value = ctOffX; _castBarOffsetXSlider.SetBlockSignals(false); }
                if (_castBarOffsetYSlider != null) { _castBarOffsetYSlider.SetBlockSignals(true); _castBarOffsetYSlider.Value = ctOffY; _castBarOffsetYSlider.SetBlockSignals(false); }
                if (_castBarLengthValue != null) _castBarLengthValue.Text = ((int)ctLength).ToString();
                if (_castBarHeightValue != null) _castBarHeightValue.Text = ((int)ctHeight).ToString();
                if (_castBarFillValue != null) _castBarFillValue.Text = $"{(int)ctFill}%";
                if (_castBarOffsetXValue != null) _castBarOffsetXValue.Text = ((int)ctOffX).ToString();
                if (_castBarOffsetYValue != null) _castBarOffsetYValue.Text = ((int)ctOffY).ToString();
                if (_castBarColorBtn != null) _castBarColorBtn.Modulate = new Color(ctR, ctG, ctB);
            }

            // Load level badge settings
            {
                bool lvVisible = (bool)config.GetValue("levelbadge", "visible", true);
                double lvFontSize = (double)config.GetValue("levelbadge", "font_size", 12);
                string lvText = (string)config.GetValue("levelbadge", "text", "Lv.{level}");
                double lvOffX = (double)config.GetValue("levelbadge", "offset_x", -35);
                double lvOffY = (double)config.GetValue("levelbadge", "offset_y", -35);
                float lvTxtR = (float)(double)config.GetValue("levelbadge", "txt_r", 1.0);
                float lvTxtG = (float)(double)config.GetValue("levelbadge", "txt_g", 1.0);
                float lvTxtB = (float)(double)config.GetValue("levelbadge", "txt_b", 0.0);

                if (_player != null)
                {
                    _player.SetLevelBadgeVisible(lvVisible);
                    _player.SetLevelBadgeFontSize((float)lvFontSize);
                    _player.SetLevelBadgeText(lvText);
                    _player.SetLevelBadgeOffset(new Vector2((float)lvOffX, (float)lvOffY));
                    _player.SetLevelBadgeTextColor(new Color(lvTxtR, lvTxtG, lvTxtB));
                }

                if (_levelBadgeVisibleCheck != null) { _levelBadgeVisibleCheck.SetBlockSignals(true); _levelBadgeVisibleCheck.ButtonPressed = lvVisible; _levelBadgeVisibleCheck.SetBlockSignals(false); }
                if (_levelBadgeFontSizeSlider != null) { _levelBadgeFontSizeSlider.SetBlockSignals(true); _levelBadgeFontSizeSlider.Value = lvFontSize; _levelBadgeFontSizeSlider.SetBlockSignals(false); }
                if (_levelBadgeOffsetXSlider != null) { _levelBadgeOffsetXSlider.SetBlockSignals(true); _levelBadgeOffsetXSlider.Value = lvOffX; _levelBadgeOffsetXSlider.SetBlockSignals(false); }
                if (_levelBadgeOffsetYSlider != null) { _levelBadgeOffsetYSlider.SetBlockSignals(true); _levelBadgeOffsetYSlider.Value = lvOffY; _levelBadgeOffsetYSlider.SetBlockSignals(false); }
                if (_levelBadgeFontSizeValue != null) _levelBadgeFontSizeValue.Text = ((int)lvFontSize).ToString();
                if (_levelBadgeOffsetXValue != null) _levelBadgeOffsetXValue.Text = ((int)lvOffX).ToString();
                if (_levelBadgeOffsetYValue != null) _levelBadgeOffsetYValue.Text = ((int)lvOffY).ToString();
                if (_levelBadgeTextColorBtn != null) _levelBadgeTextColorBtn.Modulate = new Color(lvTxtR, lvTxtG, lvTxtB);
                if (_levelBadgeTextEdit != null) _levelBadgeTextEdit.Text = lvText;
            }

            // Load monster settings
            {
                var mm = GetTree()?.GetFirstNodeInGroup("monster_manager") as MonsterManager;
                if (mm != null)
                {
                    mm.DefaultVisualSize = (int)(double)config.GetValue("monster", "visual_size", 111);
                    mm.DefaultVisualSizeScale = (float)(double)config.GetValue("monster", "visual_size_scale", 1.0);
                    mm.DefaultBorderWidth = (float)(double)config.GetValue("monster", "border_width", 3.0);
                    mm.DefaultBorderWidthScale = (float)(double)config.GetValue("monster", "border_width_scale", 3.0 / 111.0);
                    mm.DefaultCornerRadius = (float)(double)config.GetValue("monster", "corner_radius", 12.0);
                    mm.DefaultBgOpacity = (float)(double)config.GetValue("monster", "bg_opacity", 0.9);
                    mm.DefaultFontSize = (int)(double)config.GetValue("monster", "font_size", 0);
                    float mbR = (float)(double)config.GetValue("monster", "border_color_r", 0.9);
                    float mbG = (float)(double)config.GetValue("monster", "border_color_g", 0.3);
                    float mbB = (float)(double)config.GetValue("monster", "border_color_b", 0.3);
                    mm.DefaultBorderColor = new Color(mbR, mbG, mbB);
                    float mbgR = (float)(double)config.GetValue("monster", "bg_color_r", 0.8);
                    float mbgG = (float)(double)config.GetValue("monster", "bg_color_g", 0.2);
                    float mbgB = (float)(double)config.GetValue("monster", "bg_color_b", 0.2);
                    mm.DefaultBgColor = new Color(mbgR, mbgG, mbgB);
                    float mtR = (float)(double)config.GetValue("monster", "text_color_r", 1.0);
                    float mtG = (float)(double)config.GetValue("monster", "text_color_g", 0.95);
                    float mtB = (float)(double)config.GetValue("monster", "text_color_b", 0.95);
                    mm.DefaultTextColor = new Color(mtR, mtG, mtB);
                    for (int i = 0; i < 4; i++)
                    {
                        string prefix = $"monster_label_{i}";
                        mm.DefaultLabelTexts[i] = (string)config.GetValue("monster", $"{prefix}_text", "");
                        mm.DefaultLabelFontSizes[i] = (int)(double)config.GetValue("monster", $"{prefix}_font_size", 0);
                        mm.DefaultLabelXOffsets[i] = (float)(double)config.GetValue("monster", $"{prefix}_offset_x", 0);
                        mm.DefaultLabelCenterX[i] = (bool)config.GetValue("monster", $"{prefix}_center_x", true);
                        mm.DefaultLabelYOffsets[i] = (float)(double)config.GetValue("monster", $"{prefix}_offset_y", 0);
                    }
                    mm.ApplyStyleToAll();
                }
                SyncMonsterDebugUI();
            }

            GD.Print("[DebugPanel] Using default settings");
            UpdateControlStates();
        }

        private void MigrateConfig(ConfigFile config, int fromVersion)
        {
            // Config migration: upgrade from old version to current version
            if (fromVersion < 1)
            {
                // v0 -> v1: Initial version, no special migration needed
            }
            // Future version migrations add here
            // if (fromVersion < 2)
            // {
            //     // v1 -> v2 migration logic
            // }
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

        #region Apply Loaded Player Settings
        private void ApplyLoadedPlayerSettings()
        {
            GD.Print($"[DebugPanel] ApplyLoadedPlayerSettings() called, player={_player}");
            if (_player == null)
            {
                _player = GetTree().GetFirstNodeInGroup("player") as Player;
                GD.Print($"[DebugPanel] Re-acquired player: {_player}");
            }

            if (_player != null)
            {
                ConfigFile config = new ConfigFile();
                Error err = config.Load(CONFIG_PATH);
                if (err == Error.Ok)
                {
                    // Read and apply directly from config
                    double savedSize = (double)config.GetValue("player", "player_size", 111);
                    GD.Print($"[DebugPanel] Read player size from config: {savedSize}");
                    _player.SetVisualSize((int)savedSize);
                    _player.VisualSizeScale = (float)(double)config.GetValue("player", "visual_size_scale", 1.0);
                    _player.SetBorderWidth((float)(double)config.GetValue("player", "border_width", 3.0));
                    _player.BorderWidthScale = (float)(double)config.GetValue("player", "border_width_scale", 3.0 / 111.0);
                    _player.SetCornerRadius((float)(double)config.GetValue("player", "corner_radius", 0.0));
                    _player.SetBgOpacity((float)(double)config.GetValue("player", "bg_opacity", 0.1));
                    _player.SetLineSpacing((float)(double)config.GetValue("player", "line_spacing", 0.8));
                    _player.SetLetterSpacing((float)(double)config.GetValue("player", "letter_spacing", 0.0));
                    _player.SetFontBold((bool)config.GetValue("player", "font_bold", false));
                    _player.SetFontItalic((bool)config.GetValue("player", "font_italic", false));
                    _player.SetFontShadow((bool)config.GetValue("player", "font_shadow", false));
                    _player.SetTextAlignment((int)(HorizontalAlignment)(int)config.GetValue("player", "text_alignment", (int)HorizontalAlignment.Center));

                    // Apply font size
                    double savedFontSize = (double)config.GetValue("player", "font_size", 0);
                    bool savedAutoSize = (bool)config.GetValue("player", "font_auto_size", false);
                    if (savedAutoSize)
                    {
                        _player.SetFontSize(0);
                    }
                    else if (savedFontSize > 0)
                    {
                        _player.SetFontSize((int)savedFontSize);
                    }

                    _player.RefreshLabels();
                    _player.QueueRedraw();

                    // Apply label control settings
                    for (int i = 0; i < LabelCount; i++)
                    {
                        string prefix = $"label_{i}";
                        bool visible = (bool)config.GetValue("labels", $"{prefix}_visible", true);
                        string name = (string)config.GetValue("labels", $"{prefix}_name", new[] { "名称", "职业", "称号", "状态" }[i]);
                        string text = (string)config.GetValue("labels", $"{prefix}_text", "");
                        double fontSize = (double)config.GetValue("labels", $"{prefix}_font_size", 0);
                        double offsetX = (double)config.GetValue("labels", $"{prefix}_offset_x", Player.DefaultOffsets[i].X);
                        double offsetY = (double)config.GetValue("labels", $"{prefix}_offset_y", Player.DefaultOffsets[i].Y);

                        _player.SetLabelName(i, name);
                        if (!string.IsNullOrEmpty(text)) _player.SetLabelText(i, text);
                        _player.SetLabelVisible(i, visible);
                        _player.SetLabelFontSize(i, (int)fontSize);
                        _player.SetLabelOffset(i, new Vector2((float)offsetX, (float)offsetY));

                        float cr = (float)(double)config.GetValue("labels", $"{prefix}_color_r", 0.0);
                        float cg = (float)(double)config.GetValue("labels", $"{prefix}_color_g", 0.0);
                        float cb = (float)(double)config.GetValue("labels", $"{prefix}_color_b", 0.0);
                        float ca = (float)(double)config.GetValue("labels", $"{prefix}_color_a", 1.0);
                        _player.SetLineColor(i, new Color(cr, cg, cb, ca));
                    }
                    _player.RefreshLabels();

                    // Apply health bar settings
                    _player.SetHealthBarVisible((bool)config.GetValue("healthbar", "visible", true));
                    _player.SetHealthBarLength((float)(double)config.GetValue("healthbar", "length", 80));
                    _player.HealthBarLengthScale = (float)(double)config.GetValue("healthbar", "length_scale", 80.0 / 111.0);
                    _player.SetHealthBarHeight((float)(double)config.GetValue("healthbar", "height", 6));
                    _player.HealthBarHeightScale = (float)(double)config.GetValue("healthbar", "height_scale", 6.0 / 111.0);
                    _player.SetHealthBarFillPercent((float)((double)config.GetValue("healthbar", "fill", 100) / 100.0));
                    _player.SetHealthBarOffset(new Vector2(
                        (float)(double)config.GetValue("healthbar", "offset_x", 0),
                        (float)(double)config.GetValue("healthbar", "offset_y", -70)));
                    float hr = (float)(double)config.GetValue("healthbar", "color_r", 0.0);
                    float hg = (float)(double)config.GetValue("healthbar", "color_g", 0.8);
                    float hb = (float)(double)config.GetValue("healthbar", "color_b", 0.0);
                    _player.SetHealthBarColor(new Color(hr, hg, hb));

                    // Apply cast bar settings
                    _player.SetCastBarVisible((bool)config.GetValue("castbar", "visible", true));
                    _player.SetCastBarLength((float)(double)config.GetValue("castbar", "length", 60));
                    _player.SetCastBarHeight((float)(double)config.GetValue("castbar", "height", 4));
                    _player.SetCastBarFillPercent((float)((double)config.GetValue("castbar", "fill", 60) / 100.0));
                    _player.SetCastBarOffset(new Vector2(
                        (float)(double)config.GetValue("castbar", "offset_x", 0),
                        (float)(double)config.GetValue("castbar", "offset_y", -80)));
                    float cr2 = (float)(double)config.GetValue("castbar", "color_r", 0.3);
                    float cg2 = (float)(double)config.GetValue("castbar", "color_g", 0.5);
                    float cb2 = (float)(double)config.GetValue("castbar", "color_b", 1.0);
                    _player.SetCastBarColor(new Color(cr2, cg2, cb2));

                    // Apply level badge settings
                    _player.SetLevelBadgeVisible((bool)config.GetValue("levelbadge", "visible", true));
                    _player.SetLevelBadgeFontSize((float)(double)config.GetValue("levelbadge", "font_size", 12));
                    _player.SetLevelBadgeText((string)config.GetValue("levelbadge", "text", "Lv.{level}"));
                    _player.SetLevelBadgeOffset(new Vector2(
                        (float)(double)config.GetValue("levelbadge", "offset_x", -35),
                        (float)(double)config.GetValue("levelbadge", "offset_y", -35)));
                    float lvTxtR = (float)(double)config.GetValue("levelbadge", "txt_r", 1.0);
                    float lvTxtG = (float)(double)config.GetValue("levelbadge", "txt_g", 1.0);
                    float lvTxtB = (float)(double)config.GetValue("levelbadge", "txt_b", 0.0);
                    _player.SetLevelBadgeTextColor(new Color(lvTxtR, lvTxtG, lvTxtB));

                    GD.Print($"[DebugPanel] Player settings applied, visual_size={_player.VisualSize}");
                }
                else
                {
                    GD.Print("[DebugPanel] Cannot read config, skipping player settings apply");
                }
            }
            else
            {
                GD.Print("[DebugPanel] Player is still null, cannot apply settings");
            }
        }
        #endregion

        #region Export Config to JSON
        public string ExportConfigToJson()
        {
            var data = new Godot.Collections.Dictionary();

            data["timestamp"] = Time.GetDatetimeStringFromSystem();
            data["config_version"] = CONFIG_VERSION;

            var mapData = new Godot.Collections.Dictionary
            {
                ["grid_size"] = _gridSizeSlider?.Value ?? 111,
                ["zoom"] = _zoomSlider?.Value ?? 1.0,
                ["grid_line_width"] = _gridLineWidthSlider?.Value ?? 2.0,
                ["grid_line_brightness"] = _gridLineBrightnessSlider?.Value ?? 0.7,
                ["show_grid_coords"] = _gridCoordsCheck?.ButtonPressed ?? false
            };
            if (_lineWidthScaleSlider != null)
                mapData["line_width_scale"] = _lineWidthScaleSlider.Value;
            data["map"] = mapData;

            var playerData = new Godot.Collections.Dictionary
            {
                ["player_size"] = _playerSizeSlider?.Value ?? 111,
                ["visual_size_scale"] = _playerSizeScaleSlider?.Value ?? 1.0,
                ["border_width"] = _borderWidthSlider?.Value ?? 3.0,
                ["border_width_scale"] = _borderWidthScaleSlider?.Value ?? (3.0 / 111.0),
                ["corner_radius"] = _cornerRadiusSlider?.Value ?? 0.0,
                ["bg_opacity"] = _bgOpacitySlider?.Value ?? 0.1,
                ["font_size"] = _fontSizeSlider?.Value ?? 0,
                ["line_spacing"] = _lineSpacingSlider?.Value ?? 0.8,
                ["letter_spacing"] = _letterSpacingSlider?.Value ?? 0.0,
                ["font_bold"] = _boldCheck?.ButtonPressed ?? false,
                ["font_italic"] = _italicCheck?.ButtonPressed ?? false,
                ["font_shadow"] = _shadowCheck?.ButtonPressed ?? false,
                ["font_auto_size"] = _fontAutoSizeCheck?.ButtonPressed ?? false
            };
            data["player"] = playerData;

            var cameraData = new Godot.Collections.Dictionary
            {
                ["return_delay"] = _cameraReturnDelaySlider?.Value ?? 0.5,
                ["return_speed"] = _cameraReturnSpeedSlider?.Value ?? 5.0,
                ["ease_type"] = _cameraEaseTypeOption?.Selected ?? 3,
                ["ease_power"] = _cameraEasePowerSlider?.Value ?? 2.0,
                ["free_look"] = _freeLookCheck?.ButtonPressed ?? false
            };
            data["camera"] = cameraData;

            var calibrationData = new Godot.Collections.Dictionary { ["enabled"] = _calibrationEnabled };
            if (_refZoomASpin != null)
            {
                calibrationData["ref_zoom_a"] = _refZoomASpin.Value;
                calibrationData["ref_width_a"] = _refWidthASpin.Value;
                calibrationData["ref_zoom_b"] = _refZoomBSpin.Value;
                calibrationData["ref_width_b"] = _refWidthBSpin.Value;
            }
            data["calibration"] = calibrationData;

            if (_responsiveCheck != null)
            {
                data["responsive"] = new Godot.Collections.Dictionary
                {
                    ["enabled"] = _responsiveCheck.ButtonPressed,
                    ["visible_grids_x"] = _visibleGridsXSpin?.Value ?? 5.0
                };
            }

            if (_editorDragButtonOption != null)
            {
                data["editor"] = new Godot.Collections.Dictionary
                {
                    ["drag_button"] = _editorDragButtonOption.Selected,
                    ["require_ctrl_for_selection"] = _editorSelectModCheck?.ButtonPressed ?? true
                };
            }

            data["debug"] = new Godot.Collections.Dictionary { ["show_debug_info"] = _debugInfoCheck?.ButtonPressed ?? false };

            var labelsData = new Godot.Collections.Dictionary();
            for (int i = 0; i < LabelCount; i++)
            {
                labelsData[$"label_{i}"] = new Godot.Collections.Dictionary
                {
                    ["visible"] = _labelVisibleChecks[i]?.ButtonPressed ?? true,
                    ["font_size"] = _labelFontSizeSliders[i]?.Value ?? 0,
                    ["offset_x"] = _labelOffsetXSliders[i]?.Value ?? 0,
                    ["offset_y"] = _labelOffsetYSliders[i]?.Value ?? 0
                };
            }
            data["labels"] = labelsData;

            var monsterData = new Godot.Collections.Dictionary
            {
                ["visual_size"] = _monsterSizeSlider?.Value ?? 111,
                ["visual_size_scale"] = _monsterSizeScaleSlider?.Value ?? 1.0,
                ["border_width"] = _monsterBorderWidthSlider?.Value ?? 3.0,
                ["border_width_scale"] = _monsterBorderWidthScaleSlider?.Value ?? (3.0 / 111.0),
                ["corner_radius"] = _monsterCornerRadiusSlider?.Value ?? 12.0,
                ["bg_opacity"] = _monsterBgOpacitySlider?.Value ?? 0.9,
                ["font_size"] = _monsterFontSizeSlider?.Value ?? 0
            };
            var mLabelsData = new Godot.Collections.Dictionary();
            for (int i = 0; i < 4; i++)
            {
                mLabelsData[$"label_{i}"] = new Godot.Collections.Dictionary
                {
                    ["text"] = _monsterLabelEdits[i]?.Text ?? "",
                    ["font_size"] = _monsterLabelFontSizeSliders[i]?.Value ?? 0,
                    ["offset_x"] = _monsterLabelXOffsetSliders[i]?.Value ?? 0,
                    ["center_x"] = _monsterLabelCenterXChecks[i]?.ButtonPressed ?? true,
                    ["offset_y"] = _monsterLabelYOffsetSliders[i]?.Value ?? 0
                };
            }
            monsterData["labels"] = mLabelsData;
            data["monster"] = monsterData;

            return Json.Stringify(data, "  ");
        }
        #endregion
    }
}
