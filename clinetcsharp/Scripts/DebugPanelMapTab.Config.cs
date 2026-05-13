using Godot;

namespace ClinetCSharp
{
    public partial class DebugPanelMapTab
    {
        public override void SaveConfig(ConfigFile cfg)
        {
            cfg.SetValue("map", "grid_size_mode", _gridSizeModeOption?.Selected ?? (int)GridManager.GridSizeMode.Manual);
            cfg.SetValue("map", "line_width_mode", _lineWidthModeOption?.Selected ?? (int)GridManager.GridLineWidthMode.FixedScreen);
            cfg.SetValue("map", "grid_size", _gridSizeSlider.Value);
            cfg.SetValue("map", "zoom", _zoomSlider.Value);
            cfg.SetValue("map", "grid_line_width", _gridLineWidthSlider.Value);
            cfg.SetValue("map", "grid_line_brightness", _gridLineBrightnessSlider.Value);
            cfg.SetValue("map", "show_grid_coords", _gridCoordsCheck.ButtonPressed);

            cfg.SetValue("camera", "return_delay", _cameraReturnDelaySlider.Value);
            cfg.SetValue("camera", "return_speed", _cameraReturnSpeedSlider.Value);
            cfg.SetValue("camera", "ease_type", _cameraEaseTypeOption.Selected);
            cfg.SetValue("camera", "ease_power", _cameraEasePowerSlider.Value);
            cfg.SetValue("camera", "free_look", _freeLookCheck?.ButtonPressed ?? false);

            if (_lineWidthScaleSlider != null)
                cfg.SetValue("map", "line_width_scale", _lineWidthScaleSlider.Value);

            cfg.SetValue("calibration", "enabled", _calibrationEnabled);
            if (_refZoomASpin != null)
            {
                cfg.SetValue("calibration", "ref_zoom_a", _refZoomASpin.Value);
                cfg.SetValue("calibration", "ref_width_a", _refWidthASpin.Value);
                cfg.SetValue("calibration", "ref_zoom_b", _refZoomBSpin.Value);
                cfg.SetValue("calibration", "ref_width_b", _refWidthBSpin.Value);
            }

            if (_responsiveCheck != null)
            {
                cfg.SetValue("responsive", "enabled", _responsiveCheck.ButtonPressed);
                cfg.SetValue("responsive", "visible_grids_x", _visibleGridsXSpin.Value);
            }

            if (_editorDragButtonOption != null)
            {
                cfg.SetValue("editor", "drag_button", _editorDragButtonOption.Selected);
                cfg.SetValue("editor", "require_ctrl_for_selection", _editorSelectModCheck?.ButtonPressed ?? true);
            }

            cfg.SetValue("debug", "show_debug_info", _debugInfoCheck.ButtonPressed);
            cfg.SetValue("debug", "show_monster_patrol_areas", _monsterPatrolOverlayCheck?.ButtonPressed ?? false);
        }

        public override void LoadConfig(ConfigFile cfg, bool configLoaded)
        {
            if (!configLoaded)
            {
                ApplyMapSettingsToManagers();
                return;
            }

            double loadedGridSize = (double)cfg.GetValue("map", "grid_size", 111);
            if (loadedGridSize < 32 || loadedGridSize > 256)
            {
                GD.PushError($"[DebugPanel] Invalid grid_size in config: {loadedGridSize}");
                loadedGridSize = 111;
            }

            _gridSizeSlider.SetBlockSignals(true);
            _zoomSlider.SetBlockSignals(true);
            _gridSizeModeOption?.SetBlockSignals(true);
            _lineWidthModeOption?.SetBlockSignals(true);
            _gridLineWidthSlider.SetBlockSignals(true);
            _gridLineBrightnessSlider.SetBlockSignals(true);

            int gridSizeMode = (int)(double)cfg.GetValue("map", "grid_size_mode",
                (double)((bool)cfg.GetValue("responsive", "enabled", false)
                    ? (int)GridManager.GridSizeMode.ResponsiveVisibleCount
                    : (int)GridManager.GridSizeMode.Manual));
            int lineWidthMode = (int)(double)cfg.GetValue("map", "line_width_mode",
                (double)((bool)cfg.GetValue("calibration", "enabled", false)
                    ? (int)GridManager.GridLineWidthMode.AdaptiveCalibration
                    : (int)GridManager.GridLineWidthMode.FixedScreen));

            _gridSizeModeOption?.Select(gridSizeMode);
            _lineWidthModeOption?.Select(lineWidthMode);
            _gridSizeSlider.Value = loadedGridSize;
            _zoomSlider.Value = (double)cfg.GetValue("map", "zoom", 1.0);
            _gridLineWidthSlider.Value = (double)cfg.GetValue("map", "grid_line_width", 2.0);
            _gridLineBrightnessSlider.Value = (double)cfg.GetValue("map", "grid_line_brightness", 0.7);
            _gridCoordsCheck.ButtonPressed = (bool)cfg.GetValue("map", "show_grid_coords", false);

            _gridSizeModeOption?.SetBlockSignals(false);
            _lineWidthModeOption?.SetBlockSignals(false);
            _gridSizeSlider.SetBlockSignals(false);
            _zoomSlider.SetBlockSignals(false);
            _gridLineWidthSlider.SetBlockSignals(false);
            _gridLineBrightnessSlider.SetBlockSignals(false);

            ApplyMapSettingsToManagers();

            _cameraReturnDelaySlider.Value = (double)cfg.GetValue("camera", "return_delay", 0.5);
            _cameraReturnSpeedSlider.Value = (double)cfg.GetValue("camera", "return_speed", 5.0);
            _cameraEaseTypeOption.Select((int)cfg.GetValue("camera", "ease_type", 3));
            _cameraEasePowerSlider.Value = (double)cfg.GetValue("camera", "ease_power", 2.0);

            if (Camera != null)
            {
                Camera.SetReturnDelay((float)_cameraReturnDelaySlider.Value);
                Camera.SetReturnSpeed((float)_cameraReturnSpeedSlider.Value);
                Camera.SetEaseType((CameraController.EaseType)_cameraEaseTypeOption.Selected);
                Camera.SetEasePower((float)_cameraEasePowerSlider.Value);
            }

            if (_freeLookCheck != null)
            {
                _freeLookCheck.ButtonPressed = (bool)cfg.GetValue("camera", "free_look", false);
                OnFreeLookToggled(_freeLookCheck.ButtonPressed);
            }

            if (_lineWidthScaleSlider != null)
            {
                double savedLineWidth = (double)cfg.GetValue("map", "line_width_scale", 2.0);
                _lineWidthScaleSlider.SetBlockSignals(true);
                _lineWidthScaleSlider.Value = savedLineWidth;
                _lineWidthScaleSlider.SetBlockSignals(false);
            }

            _calibrationEnabled = lineWidthMode == (int)GridManager.GridLineWidthMode.AdaptiveCalibration;
            if (_applyCalibrationBtn != null && GridManager != null)
            {
                _applyCalibrationBtn.ButtonPressed = _calibrationEnabled;
            }

            if (_refZoomASpin != null)
            {
                _refZoomASpin.Value = (double)cfg.GetValue("calibration", "ref_zoom_a", 0.2);
                _refWidthASpin.Value = (double)cfg.GetValue("calibration", "ref_width_a", 3.0);
                _refZoomBSpin.Value = (double)cfg.GetValue("calibration", "ref_zoom_b", 1.0);
                _refWidthBSpin.Value = (double)cfg.GetValue("calibration", "ref_width_b", 1.5);
                if (_calibrationEnabled)
                    ApplyCalibrationValues();
            }

            if (_responsiveCheck != null && GridManager != null)
            {
                _responsiveCheck.ButtonPressed = gridSizeMode == (int)GridManager.GridSizeMode.ResponsiveVisibleCount;
                _visibleGridsXSpin.Value = (double)cfg.GetValue("responsive", "visible_grids_x", 5.0);
            }

            ApplyGridSizeMode((GridManager.GridSizeMode)gridSizeMode);
            ApplyLineWidthMode((GridManager.GridLineWidthMode)lineWidthMode);

            if (_editorDragButtonOption != null)
            {
                _editorDragButtonOption.Select((int)cfg.GetValue("editor", "drag_button", 0));
                OnEditorDragButtonChanged(_editorDragButtonOption.Selected);
            }
            if (_editorSelectModCheck != null)
            {
                _editorSelectModCheck.ButtonPressed = (bool)cfg.GetValue("editor", "require_ctrl_for_selection", true);
                OnEditorSelectModChanged(_editorSelectModCheck.ButtonPressed);
            }

            _debugInfoCheck.ButtonPressed = (bool)cfg.GetValue("debug", "show_debug_info", false);
            OnDebugInfoToggled(_debugInfoCheck.ButtonPressed);
            if (_monsterPatrolOverlayCheck != null)
            {
                _monsterPatrolOverlayCheck.ButtonPressed = (bool)cfg.GetValue("debug", "show_monster_patrol_areas", false);
                OnMonsterPatrolOverlayToggled(_monsterPatrolOverlayCheck.ButtonPressed);
            }
        }

        private void ApplyMapSettingsToManagers()
        {
            if (GridManager != null)
            {
                if (CurrentGridSizeMode == GridManager.GridSizeMode.Manual)
                    GridManager.SetGridSize((int)_gridSizeSlider.Value);

                switch (CurrentLineWidthMode)
                {
                    case GridManager.GridLineWidthMode.FixedWorld:
                        GridManager.SetFixedWorldLineWidth((float)_gridLineWidthSlider.Value);
                        break;
                    case GridManager.GridLineWidthMode.FixedScreen:
                        GridManager.SetLineWidthScale((float)_gridLineWidthSlider.Value);
                        break;
                }
                float brightness = (float)_gridLineBrightnessSlider.Value;
                GridManager.SetLineBrightness(brightness);
                GridManager.ShowGridCoords = _gridCoordsCheck.ButtonPressed;
            }

            if (Camera != null)
                Camera.Zoom = Vector2.One * (float)_zoomSlider.Value;
        }

        public void ApplyInitialGridSize()
        {
            if (GridManager != null && (_responsiveCheck == null || !_responsiveCheck.ButtonPressed))
            {
                int gridSize = (int)_gridSizeSlider.Value;
                GridManager.SetGridSize(gridSize);
            }
        }

        public override Godot.Collections.Dictionary ExportConfigData()
        {
            var data = new Godot.Collections.Dictionary
            {
                ["grid_size_mode"] = _gridSizeModeOption?.Selected ?? (int)GridManager.GridSizeMode.Manual,
                ["line_width_mode"] = _lineWidthModeOption?.Selected ?? (int)GridManager.GridLineWidthMode.FixedScreen,
                ["grid_size"] = _gridSizeSlider?.Value ?? 111,
                ["zoom"] = _zoomSlider?.Value ?? 1.0,
                ["grid_line_width"] = _gridLineWidthSlider?.Value ?? 2.0,
                ["grid_line_brightness"] = _gridLineBrightnessSlider?.Value ?? 0.7,
                ["show_grid_coords"] = _gridCoordsCheck?.ButtonPressed ?? false
            };
            if (_lineWidthScaleSlider != null)
                data["line_width_scale"] = _lineWidthScaleSlider.Value;

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

            data["debug"] = new Godot.Collections.Dictionary
            {
                ["show_debug_info"] = _debugInfoCheck?.ButtonPressed ?? false,
                ["show_monster_patrol_areas"] = _monsterPatrolOverlayCheck?.ButtonPressed ?? false
            };
            return data;
        }
    }
}
