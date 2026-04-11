using Godot;
using System;

namespace ClinetCSharp
{
    /// <summary>
    /// DebugPanel partial — 事件处理方法
    /// </summary>
    public partial class DebugPanel
    {
        #region Event Handlers - Basic Settings
        private void OnGridSizeChanged(double value)
        {
            _gridSizeValue.Text = ((int)value).ToString();
        }

        private void OnGridSizeDragEnded(bool valueChanged)
        {
            int newGridSize = (int)_gridSizeSlider.Value;
            GD.Print($"[DebugPanel] GridSize changed to: {newGridSize}");

            if (_gridManager != null)
            {
                _gridManager.Call("SetGridSize", newGridSize);
                GD.Print($"[DebugPanel] Called GridManager.SetGridSize({newGridSize})");
            }
            else
            {
                GD.PushError("[DebugPanel] _gridManager is null!");
            }

            if (_player != null)
            {
                _player.Call("SetGridSize", newGridSize);
                _player.Call("SetVisualSize", newGridSize);
                GD.Print($"[DebugPanel] Called Player.SetGridSize and set_visual_size");
            }

            GridUpdate();
            PushCurrentStateToHistory();
        }

        private void OnZoomChanged(double value)
        {
            _zoomValue.Text = $"{value:F1}";
        }

        private void OnZoomDragStarted()
        {
            _isZoomSliderDragging = true;
        }

        private void OnZoomDragEnded(bool valueChanged)
        {
            _isZoomSliderDragging = false;
            if (_camera != null)
            {
                _camera.Zoom = Vector2.One * (float)_zoomSlider.Value;
            }
            GridUpdate();
            PushCurrentStateToHistory();
        }

        private void GridUpdate()
        {
            if (_gridManager != null)
            {
                _gridManager.Call("queue_redraw");
            }
        }

        private void SyncSlidersToCurrentValues()
        {
            // Sync slider display values to current actual values (without triggering apply)
            if (_gridManager != null)
            {
                _gridSizeSlider.SetBlockSignals(true);
                _gridSizeSlider.Value = (int)_gridManager.Get("grid_size");
                bool responsiveMode = (bool)_gridManager.Get("responsive_mode");
                _gridSizeValue.Text = responsiveMode ? $"{_gridManager.Get("grid_size")} (自动)" : _gridManager.Get("grid_size").ToString();
                _gridSizeSlider.SetBlockSignals(false);
            }
        }
        #endregion

        #region Event Handlers - Grid Line Settings
        private void OnGridLineWidthChanged(double value)
        {
            _gridLineWidthValue.Text = $"{value:F1}px";
            if (_calibrationEnabled)
            {
                _gridLineWidthValue.Text += " (自适应)";
            }
        }

        private void OnGridLineWidthDragEnded(bool valueChanged)
        {
            if (_gridManager != null)
            {
                if (_calibrationEnabled)
                {
                    // Preview mode: set preview value
                    _gridManager.Call("SetPreviewLineWidth", (float)_gridLineWidthSlider.Value);
                    GD.Print($"[DebugPanel] Set preview line width: {_gridLineWidthSlider.Value}");
                }
                else
                {
                    // Manual mode: set line_width_scale
                    _gridManager.Call("SetLineWidthScale", (float)_gridLineWidthSlider.Value);
                    GD.Print($"[DebugPanel] Set line width scale: {_gridLineWidthSlider.Value}");
                }
            }
            PushCurrentStateToHistory();
        }

        private void OnGridLineBrightnessDragEnded(bool valueChanged)
        {
            if (_gridManager != null)
            {
                float brightness = (float)_gridLineBrightnessSlider.Value;
                _gridManager.Set("line_color", new Color(brightness, brightness, brightness));
                _gridManager.Call("queue_redraw");
            }
            PushCurrentStateToHistory();
        }

        private void OnGridLineBrightnessChanged(double value)
        {
            _gridLineBrightnessValue.Text = $"{value:F1}";
        }

        private void OnGridCoordsToggled(bool enabled)
        {
            if (_gridManager != null)
            {
                _gridManager.Set("show_grid_coords", enabled);
                _gridManager.Call("queue_redraw");
            }
            PushCurrentStateToHistory();
        }

        private void OnLineWidthScaleChanged(double value)
        {
            if (_gridManager != null)
            {
                _gridManager.Call("set_line_width_scale", value);
                _gridManager.Call("queue_redraw");
            }
        }

        private void OnLineWidthScaleDragEnded(bool valueChanged)
        {
            PushCurrentStateToHistory();
        }
        #endregion

        #region Event Handlers - Camera Settings
        private void OnCameraReturnDelayChanged(double value)
        {
            _cameraReturnDelayValue.Text = $"{value:F1}s";
        }

        private void OnCameraReturnDelayDragEnded(bool valueChanged)
        {
            if (_camera != null && _camera.HasMethod("set_return_delay"))
            {
                _camera.Call("set_return_delay", _cameraReturnDelaySlider.Value);
            }
            PushCurrentStateToHistory();
        }

        private void OnCameraReturnSpeedChanged(double value)
        {
            _cameraReturnSpeedValue.Text = ((int)value).ToString();
        }

        private void OnCameraReturnSpeedDragEnded(bool valueChanged)
        {
            if (_camera != null && _camera.HasMethod("set_return_speed"))
            {
                _camera.Call("set_return_speed", _cameraReturnSpeedSlider.Value);
            }
            PushCurrentStateToHistory();
        }

        private void OnCameraEaseTypeChanged(long index)
        {
            if (_camera != null && _camera.HasMethod("set_ease_type"))
            {
                _camera.Call("set_ease_type", (int)index);
            }
            PushCurrentStateToHistory();
        }

        private void OnCameraEasePowerChanged(double value)
        {
            _cameraEasePowerValue.Text = $"{value:F1}";
        }

        private void OnCameraEasePowerDragEnded(bool valueChanged)
        {
            if (_camera != null && _camera.HasMethod("set_ease_power"))
            {
                _camera.Call("set_ease_power", _cameraEasePowerSlider.Value);
            }
            PushCurrentStateToHistory();
        }

        private void OnFreeLookToggled(bool enabled)
        {
            if (_camera != null && _camera.HasMethod("set_free_look_mode"))
            {
                _camera.Call("set_free_look_mode", enabled);
            }

            // Disable camera return settings in free look mode
            _cameraReturnDelaySlider.Editable = !enabled;
            _cameraReturnSpeedSlider.Editable = !enabled;
            _cameraEaseTypeOption.Disabled = enabled;
            _cameraEasePowerSlider.Editable = !enabled;

            // Zoom slider read-only in free look mode (displays current value)
            _zoomSlider.Editable = !enabled;
            if (enabled)
            {
                _zoomValue.Text = $"{_zoomSlider.Value:F1} (自动)";
            }

            // Update label hints
            if (enabled)
            {
                _cameraReturnDelayValue.Text = "自由视角";
                _cameraReturnSpeedValue.Text = "自由视角";
            }
            else
            {
                _cameraReturnDelayValue.Text = $"{_cameraReturnDelaySlider.Value:F1}s";
                _cameraReturnSpeedValue.Text = ((int)_cameraReturnSpeedSlider.Value).ToString();
            }
        }
        #endregion

        #region Event Handlers - Debug Toggles and Save/Discard
        private void OnDebugInfoToggled(bool enabled)
        {
            GD.Print($"[DebugPanel] Debug info toggled: {enabled}");
            if (_player != null)
            {
                _player.Call("SetShowDebugInfo", enabled);
                GD.Print($"[DebugPanel] Called Player.SetShowDebugInfo({enabled})");
            }
            else
            {
                GD.PushError("[DebugPanel] Player is null, cannot toggle debug info");
            }
        }

        private void OnCameraDebugToggled(bool enabled)
        {
            if (_camera != null && _camera.HasMethod("set_debug_drag"))
            {
                _camera.Call("set_debug_drag", enabled);
            }
        }

        private void OnMapSavePressed()
        {
            SaveConfig();
        }

        private void OnDiscardChangesPressed()
        {
            GD.Print("[DebugPanel] Discarding changes, reloading config");
            LoadConfig();
            ShowDiscardNotification();
        }

        private void ShowDiscardNotification()
        {
            GD.Print("[DebugPanel] Restored to last saved state");
        }
        #endregion

        #region Event Handlers - Player Basic Appearance
        private void OnPlayerSizeChanged(double value)
        {
            _playerSizeValue.Text = ((int)value).ToString();
        }

        private void OnPlayerSizeDragEnded(bool valueChanged)
        {
            OnPlayerSizeDragEnded(valueChanged, true);
        }

        private void OnPlayerSizeDragEnded(bool valueChanged, bool pushToHistory)
        {
            if (_player != null)
            {
                _player.Call("SetVisualSize", (int)_playerSizeSlider.Value);
            }
            if (pushToHistory)
                PushCurrentStateToHistory();
        }

        private void OnBorderWidthChanged(double value)
        {
            _borderWidthValue.Text = ((int)value).ToString();
        }

        private void OnBorderWidthDragEnded(bool valueChanged)
        {
            OnBorderWidthDragEnded(valueChanged, true);
        }

        private void OnBorderWidthDragEnded(bool valueChanged, bool pushToHistory)
        {
            if (_player != null)
            {
                _player.Call("SetBorderWidth", _borderWidthSlider.Value);
            }
            if (pushToHistory)
                PushCurrentStateToHistory();
        }

        private void OnCornerRadiusChanged(double value)
        {
            _cornerRadiusValue.Text = ((int)value).ToString();
        }

        private void OnCornerRadiusDragEnded(bool valueChanged)
        {
            OnCornerRadiusDragEnded(valueChanged, true);
        }

        private void OnCornerRadiusDragEnded(bool valueChanged, bool pushToHistory)
        {
            if (_player != null)
            {
                _player.Call("SetCornerRadius", _cornerRadiusSlider.Value);
            }
            if (pushToHistory)
                PushCurrentStateToHistory();
        }

        private void OnBgOpacityChanged(double value)
        {
            _bgOpacityValue.Text = $"{value:F2}";
        }

        private void OnBgOpacityDragEnded(bool valueChanged)
        {
            OnBgOpacityDragEnded(valueChanged, true);
        }

        private void OnBgOpacityDragEnded(bool valueChanged, bool pushToHistory)
        {
            if (_player != null)
            {
                _player.Call("SetBgOpacity", (float)_bgOpacitySlider.Value);
                _player.Call("queue_redraw");
            }
            if (pushToHistory)
                PushCurrentStateToHistory();
        }
        #endregion

        #region Event Handlers - Text Style
        private void OnFontSelected(long index)
        {
            if (index < FONT_LIST.Length)
            {
                // Built-in font
                if (_player != null)
                {
                    _player.Call("SetFont", FONT_LIST[index]);
                    _player.Call("RefreshLabels");
                }
            }
            else
            {
                // Custom font - open file dialog
                _fontFileDialog.PopupCentered();
            }
        }

        private void OnLoadFontPressed()
        {
            _fontFileDialog.PopupCentered();
        }

        private void OnFontFileSelected(string path)
        {
            if (_player != null)
            {
                _player.Call("SetFont", path);
                _player.Call("RefreshLabels");
                GD.Print($"[DebugPanel] Loaded custom font: {path}");
            }
        }

        private void OnFontSizeChanged(double value)
        {
            if (_fontAutoSizeCheck != null && _fontAutoSizeCheck.ButtonPressed)
            {
                _fontSizeValue.Text = "自动";
            }
            else
            {
                _fontSizeValue.Text = ((int)value).ToString();
            }
        }

        private void OnFontSizeDragEnded(bool valueChanged)
        {
            OnFontSizeDragEnded(valueChanged, true);
        }

        private void OnFontSizeDragEnded(bool valueChanged, bool pushToHistory)
        {
            if (_player != null)
            {
                if (_fontAutoSizeCheck != null && _fontAutoSizeCheck.ButtonPressed)
                {
                    _player.Call("SetFontSize", 0);
                }
                else
                {
                    _player.Call("SetFontSize", (int)_fontSizeSlider.Value);
                    _player.Call("RefreshLabels");
                }
            }
            if (pushToHistory)
            {
                PushCurrentStateToHistory();
                _player?.Call("RefreshLabels");
            }
        }

        private void OnLineSpacingChanged(double value)
        {
            _lineSpacingValue.Text = $"{value:F1}";
        }

        private void OnLineSpacingDragEnded(bool valueChanged)
        {
            OnLineSpacingDragEnded(valueChanged, true);
        }

        private void OnLineSpacingDragEnded(bool valueChanged, bool pushToHistory)
        {
            if (_player != null)
            {
                _player.Call("SetLineSpacing", (float)_lineSpacingSlider.Value);
                _player.Call("RefreshLabels");
            }
            if (pushToHistory)
                PushCurrentStateToHistory();
        }

        private void OnLetterSpacingChanged(double value)
        {
            _letterSpacingValue.Text = ((int)value).ToString();
        }

        private void OnLetterSpacingDragEnded(bool valueChanged)
        {
            OnLetterSpacingDragEnded(valueChanged, true);
        }

        private void OnLetterSpacingDragEnded(bool valueChanged, bool pushToHistory)
        {
            if (_player != null)
            {
                _player.Call("SetLetterSpacing", (float)_letterSpacingSlider.Value);
                _player.Call("RefreshLabels");
            }
            if (pushToHistory)
                PushCurrentStateToHistory();
        }

        private void OnTextAlignChanged(HorizontalAlignment alignment)
        {
            if (_player != null)
            {
                _player.Call("SetTextAlignment", (int)alignment);
                _player.Call("RefreshLabels");
            }
            PushCurrentStateToHistory();
        }

        private void OnLineColorButtonPressed(int lineIndex)
        {
            // Cycle through colors
            Godot.Collections.Array<Color> lineColors = (Godot.Collections.Array<Color>)_player.Get("LineColors");
            Color currentColor = lineIndex < lineColors.Count ? lineColors[lineIndex] : Colors.Black;
            int currentIdx = System.Array.IndexOf(COLOR_PRESETS, currentColor);
            int nextIndex = (currentIdx + 1) % COLOR_PRESETS.Length;

            lineColors[lineIndex] = COLOR_PRESETS[nextIndex];
            _player.Set("LineColors", lineColors);
            _lineColorButtons[lineIndex].Modulate = COLOR_PRESETS[nextIndex];
            _player.Call("RefreshLabels");
            PushCurrentStateToHistory();
        }

        private void OnBoldToggled(bool enabled)
        {
            if (_player != null)
            {
                _player.Call("SetFontBold", enabled);
                _player.Call("RefreshLabels");
            }
            PushCurrentStateToHistory();
        }

        private void OnItalicToggled(bool enabled)
        {
            if (_player != null)
            {
                _player.Call("SetFontItalic", enabled);
                _player.Call("RefreshLabels");
            }
            PushCurrentStateToHistory();
        }

        private void OnShadowToggled(bool enabled)
        {
            if (_player != null)
            {
                _player.Call("SetFontShadow", enabled);
                _player.Call("RefreshLabels");
            }
            PushCurrentStateToHistory();
        }
        #endregion

        #region Event Handlers - Label Controls (4 independent)
        private void OnLabelVisibleToggled(int index, bool enabled)
        {
            if (_player != null)
                _player.Call("SetLabelVisible", index, enabled);
            PushCurrentStateToHistory();
        }

        private void OnLabelNameChanged(int index, string newName)
        {
            if (_player != null)
                _player.Call("SetLabelName", index, newName);
        }

        private void OnLabelTextChanged(int index, string newText)
        {
            if (_player != null)
                _player.Call("SetLabelText", index, newText);
        }

        private void OnLabelColorPressed(int index)
        {
            if (_player == null) return;
            var lineColors = (Godot.Collections.Array<Color>)_player.Get("LineColors");
            Color currentColor = index < lineColors.Count ? lineColors[index] : Colors.Black;
            int currentIdx = System.Array.IndexOf(COLOR_PRESETS, currentColor);
            int nextIndex = (currentIdx + 1) % COLOR_PRESETS.Length;

            _player.Call("SetLineColor", index, COLOR_PRESETS[nextIndex]);
            _labelColorButtons[index].Modulate = COLOR_PRESETS[nextIndex];
            _player.Call("RefreshLabels");
            PushCurrentStateToHistory();
        }

        private void OnLabelResetPressed(int index)
        {
            if (_player == null) return;
            _player.Call("ResetLabelOffset", index);
            SyncLabelOffsetSlidersFromPlayer();
            PushCurrentStateToHistory();
        }

        private void OnLabelFontSizeChanged(int index, double value)
        {
            _labelFontSizeValues[index].Text = value > 0 ? ((int)value).ToString() : "自动";
        }

        private void OnLabelFontSizeDragEnded(int index, bool valueChanged)
        {
            if (_player != null)
            {
                _player.Call("SetLabelFontSize", index, (int)_labelFontSizeSliders[index].Value);
                _player.Call("RefreshLabels");
            }
            PushCurrentStateToHistory();
        }

        private void OnLabelOffsetXChanged(int index, double value)
        {
            _labelOffsetXValues[index].Text = ((int)value).ToString();
            // Apply immediately for real-time feedback
            if (_player != null)
            {
                var currentOffset = (Vector2)_player.Call("GetLabelOffset", index);
                _player.Call("SetLabelOffset", index, new Vector2((float)value, currentOffset.Y));
            }
        }

        private void OnLabelOffsetYChanged(int index, double value)
        {
            _labelOffsetYValues[index].Text = ((int)value).ToString();
            if (_player != null)
            {
                var currentOffset = (Vector2)_player.Call("GetLabelOffset", index);
                _player.Call("SetLabelOffset", index, new Vector2(currentOffset.X, (float)value));
            }
        }

        private void OnLabelOffsetDragEnded(int index, bool valueChanged)
        {
            PushCurrentStateToHistory();
        }

        /// <summary>
        /// Sync all label offset sliders to current player label offsets
        /// </summary>
        public void SyncLabelOffsetSlidersFromPlayer()
        {
            if (_player == null) return;
            for (int i = 0; i < LabelCount; i++)
            {
                var offset = (Vector2)_player.Call("GetLabelOffset", i);
                _labelOffsetXSliders[i].SetBlockSignals(true);
                _labelOffsetYSliders[i].SetBlockSignals(true);
                _labelOffsetXSliders[i].Value = (double)offset.X;
                _labelOffsetYSliders[i].Value = (double)offset.Y;
                _labelOffsetXValues[i].Text = ((int)offset.X).ToString();
                _labelOffsetYValues[i].Text = ((int)offset.Y).ToString();
                _labelOffsetXSliders[i].SetBlockSignals(false);
                _labelOffsetYSliders[i].SetBlockSignals(false);
            }
        }
        #endregion

        #region Calibration and Responsive Logic
        private void OnCalibrationToggled(bool enabled)
        {
            _calibrationEnabled = enabled;

            if (_gridManager != null)
            {
                _gridManager.Call("SetAdaptiveCalibrationEnabled", enabled);
                GD.Print($"[DebugPanel] Calibration toggled: {enabled}");
            }

            // Reference point setting UI dim/normal (no longer hide)
            Node rowA = GetNodeOrNull("Control/Panel/ScrollContainer/TabContainer/地图/GridGroup/RefPointARow");
            Node rowB = GetNodeOrNull("Control/Panel/ScrollContainer/TabContainer/地图/GridGroup/RefPointBRow");
            if (rowA != null)
                rowA.Set("modulate", enabled ? new Color(1, 1, 1, 1) : new Color(0.6f, 0.6f, 0.6f, 0.6f));
            if (rowB != null)
                rowB.Set("modulate", enabled ? new Color(1, 1, 1, 1) : new Color(0.6f, 0.6f, 0.6f, 0.6f));

            if (enabled)
            {
                // On: immediately apply current reference point values
                ApplyCalibrationValues();
                // Enable calibration zoom range limit
                UpdateCalibrationZoomLimits();
                // Disable max width slider (controlled by algorithm)
                if (_lineWidthScaleSlider != null)
                    _lineWidthScaleSlider.Editable = false;
                // Update label display
                _gridLineWidthValue.Text = $"{_gridLineWidthSlider.Value:F1}px (自适应)";
                // Responsive layout hint
                if (_responsiveCheck != null && _responsiveCheck.ButtonPressed)
                    GD.Print("[DebugPanel] Hint: Responsive layout will change camera zoom, line width calibration reference points may need adjustment");
            }
            else
            {
                // Off: disable calibration zoom range limit
                if (_camera != null)
                {
                    _camera.Call("set_calibration_zoom_limits", false);
                    GD.Print("[DebugPanel] Calibration zoom limit disabled");
                }
                // Use current slider value as fixed value
                if (_lineWidthScaleSlider != null)
                    _lineWidthScaleSlider.Editable = true;
                if (_gridManager != null)
                    _gridManager.Call("SetLineWidthScale", (float)_gridLineWidthSlider.Value);
                _gridLineWidthValue.Text = $"{_gridLineWidthSlider.Value:F1}px";
            }
        }

        private void OnCalibrationValueChanged(double value)
        {
            // If any reference point changes, apply in real-time when switch is on
            if (_calibrationEnabled && _gridManager != null)
            {
                ApplyCalibrationValues();
            }
            // If reference point zoom value changes, update zoom limit
            if (_calibrationEnabled)
            {
                UpdateCalibrationZoomLimits();
            }
        }

        private void ApplyCalibrationValues()
        {
            double zoomA = _refZoomASpin.Value;
            double widthA = _refWidthASpin.Value;
            double zoomB = _refZoomBSpin.Value;
            double widthB = _refWidthBSpin.Value;
            _gridManager.Call("set_line_width_calibration", zoomA, widthA, zoomB, widthB);

            // Update slider display to current zoom corresponding value
            SyncLineWidthToAdaptiveValue();
        }

        private void SyncLineWidthToAdaptiveValue()
        {
            // Calculate corresponding line width based on current camera zoom, update slider position (display only)
            if (_camera != null && _gridManager != null)
            {
                // Can add logic to calculate expected line width based on zoom here
            }
        }

        private void UpdateCalibrationZoomLimits()
        {
            // Set zoom range limit based on reference point A and B zoom values
            if (_camera != null && _refZoomASpin != null && _refZoomBSpin != null)
            {
                double zoomA = _refZoomASpin.Value;
                double zoomB = _refZoomBSpin.Value;
                // Reference point A is usually farther view (smaller zoom), B is closer view (larger zoom)
                double minZoomLimit = Mathf.Min((float)zoomA, (float)zoomB);
                double maxZoomLimit = Mathf.Max((float)zoomA, (float)zoomB);
                _camera.Call("set_calibration_zoom_limits", true, minZoomLimit, maxZoomLimit);
                GD.Print($"[DebugPanel] Calibration zoom limit enabled: zoom [{minZoomLimit:F1} - {maxZoomLimit:F1}]");
            }
        }

        private void OnResponsiveToggled(bool enabled)
        {
            if (_gridManager != null)
            {
                GD.Print($"[DebugPanel] Setting responsive mode: {enabled}, visibleGridsX={_visibleGridsXSpin.Value}");
                _gridManager.Call("set_responsive_mode", enabled);
                if (enabled)
                    _gridManager.Set("visible_grids_x", _visibleGridsXSpin.Value);
            }
            else
            {
                GD.PrintErr("[DebugPanel] Cannot toggle responsive: _gridManager is null");
            }

            // Responsive layout and grid size slider are mutually exclusive
            _gridSizeSlider.Editable = !enabled;
            if (enabled)
                _gridSizeValue.Text = $"{(int)_gridSizeSlider.Value} (自动)";
            else
                _gridSizeValue.Text = ((int)_gridSizeSlider.Value).ToString();

            // Responsive layout and line width calibration can be used simultaneously, no longer auto-disable

            // Note: Responsive layout and line width calibration can be used simultaneously, no longer mutually exclusive
            if (_applyCalibrationBtn != null)
                _applyCalibrationBtn.TooltipText = "启用自适应校准";
        }

        private void OnVisibleGridsChanged(double value)
        {
            if (_gridManager != null && (bool)_gridManager.Get("responsive_mode"))
            {
                _gridManager.Set("visible_grids_x", _visibleGridsXSpin.Value);
                _gridManager.Call("update_responsive_grid_size");
            }
        }

        private void OnEditorDragButtonChanged(long index)
        {
            // 0=Left, 1=Right, 2=Middle
            MouseButton[] buttonMap = new MouseButton[] { MouseButton.Left, MouseButton.Right, MouseButton.Middle };
            MouseButton selectedButton = buttonMap[index];

            // Update camera controller
            Godot.Collections.Array<MouseButton> buttons = new Godot.Collections.Array<MouseButton> { selectedButton };
            if (_camera != null && _camera.HasMethod("set_drag_buttons"))
            {
                _camera.Call("set_drag_buttons", buttons);
            }
            else if (_camera != null)
            {
                _camera.Set("drag_buttons", buttons);
            }

            string[] buttonNames = new string[] { "左键", "右键", "中键" };
            GD.Print($"[DebugPanel] Drag view button changed to: {buttonNames[index]}");
        }

        private void OnEditorSelectModChanged(bool enabled)
        {
            // Notify map editor
            Node mapEditor = GetTree().GetFirstNodeInGroup("map_editor");
            if (mapEditor != null)
            {
                mapEditor.Set("require_ctrl_for_selection", enabled);
            }

            GD.Print($"[DebugPanel] Ctrl+Click select: {enabled}");
        }

        private void OnFontAutoSizeToggled(bool enabled)
        {
            if (enabled)
            {
                _fontSizeValue.Text = "自动";
                if (_player != null)
                {
                    _player.Call("SetFontSize", 0);
                    _player.Call("RefreshLabels");
                }
            }
            else
            {
                _fontSizeValue.Text = ((int)_fontSizeSlider.Value).ToString();
                if (_player != null)
                {
                    _player.Call("SetFontSize", (int)_fontSizeSlider.Value);
                    _player.Call("RefreshLabels");
                }
            }
        }
        #endregion

        #region Event Handlers - Health Bar
        private void OnHealthBarVisibleToggled(bool enabled)
        {
            if (_player != null)
                _player.Call("SetHealthBarVisible", enabled);
            PushCurrentStateToHistory();
        }

        private void OnHealthBarColorPressed()
        {
            if (_player == null) return;
            var currentColor = (Color)_player.Get("HealthBarColor");
            int currentIdx = System.Array.IndexOf(COLOR_PRESETS, currentColor);
            // Add green to cycle if not found
            Color[] hpColors = new Color[] { new Color(0, 0.8f, 0, 1), Colors.Red, Colors.Yellow, Colors.Cyan, Colors.White };
            int idx = 0;
            for (int i = 0; i < hpColors.Length; i++)
            {
                if (currentColor.IsEqualApprox(hpColors[i])) { idx = i; break; }
            }
            int nextIdx = (idx + 1) % hpColors.Length;
            _player.Call("SetHealthBarColor", hpColors[nextIdx]);
            _healthBarColorBtn.Modulate = hpColors[nextIdx];
            PushCurrentStateToHistory();
        }

        private void OnHealthBarLengthChanged(double value)
        {
            _healthBarLengthValue.Text = ((int)value).ToString();
            if (_player != null)
                _player.Call("SetHealthBarLength", (float)value);
        }

        private void OnHealthBarHeightChanged(double value)
        {
            _healthBarHeightValue.Text = ((int)value).ToString();
            if (_player != null)
                _player.Call("SetHealthBarHeight", (float)value);
        }

        private void OnHealthBarFillChanged(double value)
        {
            _healthBarFillValue.Text = $"{(int)value}%";
            if (_player != null)
                _player.Call("SetHealthBarFillPercent", (float)(value / 100.0));
        }

        private void OnHealthBarOffsetXChanged(double value)
        {
            _healthBarOffsetXValue.Text = ((int)value).ToString();
            if (_player != null)
            {
                var offset = (Vector2)_player.Call("GetHealthBarOffset");
                _player.Call("SetHealthBarOffset", new Vector2((float)value, offset.Y));
            }
        }

        private void OnHealthBarOffsetYChanged(double value)
        {
            _healthBarOffsetYValue.Text = ((int)value).ToString();
            if (_player != null)
            {
                var offset = (Vector2)_player.Call("GetHealthBarOffset");
                _player.Call("SetHealthBarOffset", new Vector2(offset.X, (float)value));
            }
        }
        #endregion

        #region Event Handlers - Cast Bar
        private void OnCastBarVisibleToggled(bool enabled)
        {
            if (_player != null)
                _player.Call("SetCastBarVisible", enabled);
            PushCurrentStateToHistory();
        }

        private void OnCastBarColorPressed()
        {
            if (_player == null) return;
            var currentColor = (Color)_player.Get("CastBarColor");
            Color[] ctColors = new Color[] { new Color(0.3f, 0.5f, 1, 1), new Color(1, 0.5f, 0, 1), new Color(0.8f, 0.2f, 1, 1), Colors.White };
            int idx = 0;
            for (int i = 0; i < ctColors.Length; i++)
            {
                if (currentColor.IsEqualApprox(ctColors[i])) { idx = i; break; }
            }
            int nextIdx = (idx + 1) % ctColors.Length;
            _player.Call("SetCastBarColor", ctColors[nextIdx]);
            _castBarColorBtn.Modulate = ctColors[nextIdx];
            PushCurrentStateToHistory();
        }

        private void OnCastBarLengthChanged(double value)
        {
            _castBarLengthValue.Text = ((int)value).ToString();
            if (_player != null) _player.Call("SetCastBarLength", (float)value);
        }

        private void OnCastBarHeightChanged(double value)
        {
            _castBarHeightValue.Text = ((int)value).ToString();
            if (_player != null) _player.Call("SetCastBarHeight", (float)value);
        }

        private void OnCastBarFillChanged(double value)
        {
            _castBarFillValue.Text = $"{(int)value}%";
            if (_player != null) _player.Call("SetCastBarFillPercent", (float)(value / 100.0));
        }

        private void OnCastBarOffsetXChanged(double value)
        {
            _castBarOffsetXValue.Text = ((int)value).ToString();
            if (_player != null)
            {
                var offset = (Vector2)_player.Call("GetCastBarOffset");
                _player.Call("SetCastBarOffset", new Vector2((float)value, offset.Y));
            }
        }

        private void OnCastBarOffsetYChanged(double value)
        {
            _castBarOffsetYValue.Text = ((int)value).ToString();
            if (_player != null)
            {
                var offset = (Vector2)_player.Call("GetCastBarOffset");
                _player.Call("SetCastBarOffset", new Vector2(offset.X, (float)value));
            }
        }
        #endregion

        #region Event Handlers - Level Badge
        private void OnLevelBadgeVisibleToggled(bool enabled)
        {
            if (_player != null) _player.Call("SetLevelBadgeVisible", enabled);
            PushCurrentStateToHistory();
        }

        private void OnLevelBadgeTextColorPressed()
        {
            if (_player == null) return;
            var current = (Color)_player.Get("LevelBadgeTextColor");
            Color[] colors = new Color[] { Colors.Yellow, Colors.White, Colors.Cyan, new Color(1, 0.5f, 0, 1), Colors.Black };
            int idx = 0;
            for (int i = 0; i < colors.Length; i++) { if (current.IsEqualApprox(colors[i])) { idx = i; break; } }
            int next = (idx + 1) % colors.Length;
            _player.Call("SetLevelBadgeTextColor", colors[next]);
            _levelBadgeTextColorBtn.Modulate = colors[next];
            PushCurrentStateToHistory();
        }

        private void OnLevelBadgeTextChanged(string newText)
        {
            if (_player != null) _player.Call("SetLevelBadgeText", newText);
        }

        private void OnLevelBadgeFontSizeChanged(double value)
        {
            _levelBadgeFontSizeValue.Text = ((int)value).ToString();
            if (_player != null) _player.Call("SetLevelBadgeFontSize", (float)value);
        }

        private void OnLevelBadgeOffsetXChanged(double value)
        {
            _levelBadgeOffsetXValue.Text = ((int)value).ToString();
            if (_player != null)
            {
                var offset = (Vector2)_player.Call("GetLevelBadgeOffset");
                _player.Call("SetLevelBadgeOffset", new Vector2((float)value, offset.Y));
            }
        }

        private void OnLevelBadgeOffsetYChanged(double value)
        {
            _levelBadgeOffsetYValue.Text = ((int)value).ToString();
            if (_player != null)
            {
                var offset = (Vector2)_player.Call("GetLevelBadgeOffset");
                _player.Call("SetLevelBadgeOffset", new Vector2(offset.X, (float)value));
            }
        }
        #endregion
    }
}
