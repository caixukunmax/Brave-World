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
                _gridManager.SetGridSize(newGridSize);
                GD.Print($"[DebugPanel] Called GridManager.SetGridSize({newGridSize})");
            }
            else
            {
                GD.PushError("[DebugPanel] _gridManager is null!");
            }

            // 同步玩家大小滑块和比例滑块（SetGridSize 已自动按比例重算）
            if (_player != null)
            {
                int newPlayerSize = _player.VisualSize;
                float newPlayerScale = _player.VisualSizeScale;
                if (_playerSizeSlider != null)
                {
                    _playerSizeSlider.SetBlockSignals(true);
                    _playerSizeSlider.Value = newPlayerSize;
                    _playerSizeSlider.SetBlockSignals(false);
                    _playerSizeValue.Text = newPlayerSize.ToString();
                }
                if (_playerSizeScaleSlider != null)
                {
                    _playerSizeScaleSlider.SetBlockSignals(true);
                    _playerSizeScaleSlider.Value = newPlayerScale;
                    _playerSizeScaleSlider.SetBlockSignals(false);
                    _playerSizeScaleValue.Text = newPlayerScale.ToString("F2");
                }

                float newBorderWidth = _player.BorderWidth;
                float newBorderScale = _player.BorderWidthScale;
                if (_borderWidthSlider != null)
                {
                    _borderWidthSlider.SetBlockSignals(true);
                    _borderWidthSlider.Value = newBorderWidth;
                    _borderWidthSlider.SetBlockSignals(false);
                    _borderWidthValue.Text = ((int)newBorderWidth).ToString();
                }
                if (_borderWidthScaleSlider != null)
                {
                    _borderWidthScaleSlider.SetBlockSignals(true);
                    _borderWidthScaleSlider.Value = newBorderScale;
                    _borderWidthScaleSlider.SetBlockSignals(false);
                    _borderWidthScaleValue.Text = newBorderScale.ToString("F2");
                }

                float newHpLength = _player.HealthBarLength;
                float newHpLengthScale = _player.HealthBarLengthScale;
                if (_healthBarLengthSlider != null)
                {
                    _healthBarLengthSlider.SetBlockSignals(true);
                    _healthBarLengthSlider.Value = newHpLength;
                    _healthBarLengthSlider.SetBlockSignals(false);
                    _healthBarLengthValue.Text = ((int)newHpLength).ToString();
                }
                if (_healthBarLengthScaleSlider != null)
                {
                    _healthBarLengthScaleSlider.SetBlockSignals(true);
                    _healthBarLengthScaleSlider.Value = newHpLengthScale;
                    _healthBarLengthScaleSlider.SetBlockSignals(false);
                    _healthBarLengthScaleValue.Text = newHpLengthScale.ToString("F2");
                }

                float newHpHeight = _player.HealthBarHeight;
                float newHpHeightScale = _player.HealthBarHeightScale;
                if (_healthBarHeightSlider != null)
                {
                    _healthBarHeightSlider.SetBlockSignals(true);
                    _healthBarHeightSlider.Value = newHpHeight;
                    _healthBarHeightSlider.SetBlockSignals(false);
                    _healthBarHeightValue.Text = ((int)newHpHeight).ToString();
                }
                if (_healthBarHeightScaleSlider != null)
                {
                    _healthBarHeightScaleSlider.SetBlockSignals(true);
                    _healthBarHeightScaleSlider.Value = newHpHeightScale;
                    _healthBarHeightScaleSlider.SetBlockSignals(false);
                    _healthBarHeightScaleValue.Text = newHpHeightScale.ToString("F2");
                }
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
                _gridManager.QueueRedraw();
            }
        }

        private void SyncSlidersToCurrentValues()
        {
            // Sync slider display values to current actual values (without triggering apply)
            if (_gridManager != null)
            {
                _gridSizeSlider.SetBlockSignals(true);
                _gridSizeSlider.Value = (int)_gridManager.GridSize;
                bool responsiveMode = _gridManager.ResponsiveMode;
                _gridSizeValue.Text = responsiveMode ? $"{_gridManager.GridSize} (自动)" : _gridManager.GridSize.ToString();
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
                    _gridManager.SetPreviewLineWidth((float)_gridLineWidthSlider.Value);
                    GD.Print($"[DebugPanel] Set preview line width: {_gridLineWidthSlider.Value}");
                }
                else
                {
                    // Manual mode: set line_width_scale
                    _gridManager.SetLineWidthScale((float)_gridLineWidthSlider.Value);
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
                _gridManager.LineColor = new Color(brightness, brightness, brightness);
                _gridManager.QueueRedraw();
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
                _gridManager.SetShowGridCoords(enabled);
            }
            PushCurrentStateToHistory();
        }

        private void OnLineWidthScaleChanged(double value)
        {
            if (_gridManager != null)
            {
                _gridManager.SetLineWidthScale((float)value);
                _gridManager.QueueRedraw();
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
            if (_camera != null)
            {
                _camera.SetReturnDelay((float)_cameraReturnDelaySlider.Value);
            }
            PushCurrentStateToHistory();
        }

        private void OnCameraReturnSpeedChanged(double value)
        {
            _cameraReturnSpeedValue.Text = ((int)value).ToString();
        }

        private void OnCameraReturnSpeedDragEnded(bool valueChanged)
        {
            if (_camera != null)
            {
                _camera.SetReturnSpeed((float)_cameraReturnSpeedSlider.Value);
            }
            PushCurrentStateToHistory();
        }

        private void OnCameraEaseTypeChanged(long index)
        {
            if (_camera != null)
            {
                _camera.SetEaseType((CameraController.EaseType)index);
            }
            PushCurrentStateToHistory();
        }

        private void OnCameraEasePowerChanged(double value)
        {
            _cameraEasePowerValue.Text = $"{value:F1}";
        }

        private void OnCameraEasePowerDragEnded(bool valueChanged)
        {
            if (_camera != null)
            {
                _camera.SetEasePower((float)_cameraEasePowerSlider.Value);
            }
            PushCurrentStateToHistory();
        }

        private void OnFreeLookToggled(bool enabled)
        {
            if (_camera != null)
            {
                _camera.SetFreeLookMode(enabled);
            }
            UpdateControlStates();
        }
        #endregion

        #region Event Handlers - Debug Toggles and Save/Discard
        private void OnDebugInfoToggled(bool enabled)
        {
            GD.Print($"[DebugPanel] Debug info toggled: {enabled}");
            if (_player != null)
            {
                _player.SetShowDebugInfo(enabled);
                GD.Print($"[DebugPanel] Called Player.SetShowDebugInfo({enabled})");
            }
            else
            {
                GD.PushError("[DebugPanel] Player is null, cannot toggle debug info");
            }
        }

        private void OnCameraDebugToggled(bool enabled)
        {
            if (_camera != null)
            {
                _camera.SetDebugDrag(enabled);
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
                _player.SetVisualSize((int)_playerSizeSlider.Value);
                int gridSize = (int)_gridSizeSlider.Value;
                float scale = gridSize > 0 ? (float)(_playerSizeSlider.Value / gridSize) : 1.0f;
                _player.VisualSizeScale = scale;
                if (_playerSizeScaleSlider != null)
                {
                    _playerSizeScaleSlider.SetBlockSignals(true);
                    _playerSizeScaleSlider.Value = scale;
                    _playerSizeScaleSlider.SetBlockSignals(false);
                    _playerSizeScaleValue.Text = scale.ToString("F2");
                }
            }
            if (pushToHistory)
                PushCurrentStateToHistory();
        }

        private void OnPlayerSizeScaleChanged(double value)
        {
            if (_playerSizeScaleValue != null)
                _playerSizeScaleValue.Text = value.ToString("F2");
        }

        private void OnPlayerSizeScaleDragEnded(bool valueChanged)
        {
            if (_player != null)
            {
                _player.SetVisualSizeScale((float)_playerSizeScaleSlider.Value);
                int newSize = _player.VisualSize;
                if (_playerSizeSlider != null)
                {
                    _playerSizeSlider.SetBlockSignals(true);
                    _playerSizeSlider.Value = newSize;
                    _playerSizeSlider.SetBlockSignals(false);
                    _playerSizeValue.Text = newSize.ToString();
                }
            }
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
                int gridSize = (int)_gridSizeSlider.Value;
                float scale = gridSize > 0 ? (float)(_borderWidthSlider.Value / gridSize) : 0.0f;
                _player.SetBorderWidth((float)_borderWidthSlider.Value);
                _player.BorderWidthScale = scale;
                if (_borderWidthScaleSlider != null)
                {
                    _borderWidthScaleSlider.SetBlockSignals(true);
                    _borderWidthScaleSlider.Value = scale;
                    _borderWidthScaleSlider.SetBlockSignals(false);
                    _borderWidthScaleValue.Text = scale.ToString("F2");
                }
            }
            if (pushToHistory)
                PushCurrentStateToHistory();
        }

        private void OnBorderWidthScaleChanged(double value)
        {
            if (_borderWidthScaleValue != null)
                _borderWidthScaleValue.Text = value.ToString("F2");
        }

        private void OnBorderWidthScaleDragEnded(bool valueChanged)
        {
            if (_player != null)
            {
                _player.SetBorderWidthScale((float)_borderWidthScaleSlider.Value);
                float newWidth = _player.BorderWidth;
                if (_borderWidthSlider != null)
                {
                    _borderWidthSlider.SetBlockSignals(true);
                    _borderWidthSlider.Value = newWidth;
                    _borderWidthSlider.SetBlockSignals(false);
                    _borderWidthValue.Text = ((int)newWidth).ToString();
                }
            }
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
                _player.SetCornerRadius((float)_cornerRadiusSlider.Value);
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
                _player.SetBgOpacity((float)_bgOpacitySlider.Value);
                _player.QueueRedraw();
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
                    _player.SetFont(FONT_LIST[index]);
                    _player.RefreshLabels();
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
                _player.SetFont(path);
                _player.RefreshLabels();
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
                    _player.SetFontSize(0);
                }
                else
                {
                    _player.SetFontSize((int)_fontSizeSlider.Value);
                    _player.RefreshLabels();
                }
            }
            if (pushToHistory)
            {
                PushCurrentStateToHistory();
                _player?.RefreshLabels();
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
                _player.SetLineSpacing((float)_lineSpacingSlider.Value);
                _player.RefreshLabels();
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
                _player.SetLetterSpacing((float)_letterSpacingSlider.Value);
                _player.RefreshLabels();
            }
            if (pushToHistory)
                PushCurrentStateToHistory();
        }

        private void OnTextAlignChanged(HorizontalAlignment alignment)
        {
            if (_player != null)
            {
                _player.SetTextAlignment(alignment);
                _player.RefreshLabels();
            }
            PushCurrentStateToHistory();
        }

        private void OnLineColorButtonPressed(int lineIndex)
        {
            // Cycle through colors
            Godot.Collections.Array<Color> lineColors = _player.LineColors;
            Color currentColor = lineIndex < lineColors.Count ? lineColors[lineIndex] : Colors.Black;
            int currentIdx = System.Array.IndexOf(COLOR_PRESETS, currentColor);
            int nextIndex = (currentIdx + 1) % COLOR_PRESETS.Length;

            lineColors[lineIndex] = COLOR_PRESETS[nextIndex];
            _player.LineColors = lineColors;
            _lineColorButtons[lineIndex].Modulate = COLOR_PRESETS[nextIndex];
            _player.RefreshLabels();
            PushCurrentStateToHistory();
        }

        private void OnBoldToggled(bool enabled)
        {
            if (_player != null)
            {
                _player.SetFontBold(enabled);
                _player.RefreshLabels();
            }
            PushCurrentStateToHistory();
        }

        private void OnItalicToggled(bool enabled)
        {
            if (_player != null)
            {
                _player.SetFontItalic(enabled);
                _player.RefreshLabels();
            }
            PushCurrentStateToHistory();
        }

        private void OnShadowToggled(bool enabled)
        {
            if (_player != null)
            {
                _player.SetFontShadow(enabled);
                _player.RefreshLabels();
            }
            PushCurrentStateToHistory();
        }
        #endregion

        #region Event Handlers - Label Controls (4 independent)
        private void OnLabelVisibleToggled(int index, bool enabled)
        {
            if (_player != null)
                _player.SetLabelVisible(index, enabled);
            PushCurrentStateToHistory();
        }

        private void OnLabelNameChanged(int index, string newName)
        {
            if (_player != null)
                _player.SetLabelName(index, newName);
        }

        private void OnLabelTextChanged(int index, string newText)
        {
            if (_player != null)
                _player.SetLabelText(index, newText);
        }

        private void OnLabelColorPressed(int index)
        {
            if (_player == null) return;
            var lineColors = _player.LineColors;
            Color currentColor = index < lineColors.Count ? lineColors[index] : Colors.Black;
            int currentIdx = System.Array.IndexOf(COLOR_PRESETS, currentColor);
            int nextIndex = (currentIdx + 1) % COLOR_PRESETS.Length;

            _player.SetLineColor(index, COLOR_PRESETS[nextIndex]);
            _labelColorButtons[index].Modulate = COLOR_PRESETS[nextIndex];
            _player.RefreshLabels();
            PushCurrentStateToHistory();
        }

        private void OnLabelResetPressed(int index)
        {
            if (_player == null) return;
            _player.ResetLabelOffset(index);
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
                _player.SetLabelFontSize(index, (int)_labelFontSizeSliders[index].Value);
                _player.RefreshLabels();
            }
            PushCurrentStateToHistory();
        }

        private void OnLabelOffsetXChanged(int index, double value)
        {
            _labelOffsetXValues[index].Text = ((int)value).ToString();
            // Apply immediately for real-time feedback
            if (_player != null)
            {
                var currentOffset = _player.GetLabelOffset(index);
                _player.SetLabelOffset(index, new Vector2((float)value, currentOffset.Y));
            }
        }

        private void OnLabelOffsetYChanged(int index, double value)
        {
            _labelOffsetYValues[index].Text = ((int)value).ToString();
            if (_player != null)
            {
                var currentOffset = _player.GetLabelOffset(index);
                _player.SetLabelOffset(index, new Vector2(currentOffset.X, (float)value));
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
            bool autoCenter = _player.LabelAutoCenterX;
            if (_labelAutoCenterXCheck != null)
            {
                _labelAutoCenterXCheck.SetBlockSignals(true);
                _labelAutoCenterXCheck.ButtonPressed = autoCenter;
                _labelAutoCenterXCheck.SetBlockSignals(false);
            }
            for (int i = 0; i < LabelCount; i++)
            {
                var offset = _player.GetLabelOffset(i);
                _labelOffsetXSliders[i].SetBlockSignals(true);
                _labelOffsetYSliders[i].SetBlockSignals(true);
                if (autoCenter)
                {
                    _labelOffsetXSliders[i].Value = 0;
                    _labelOffsetXSliders[i].Editable = false;
                    _labelOffsetXValues[i].Text = "居中";
                }
                else
                {
                    _labelOffsetXSliders[i].Value = (double)offset.X;
                    _labelOffsetXSliders[i].Editable = true;
                    _labelOffsetXValues[i].Text = ((int)offset.X).ToString();
                }
                _labelOffsetYSliders[i].Value = (double)offset.Y;
                _labelOffsetYValues[i].Text = ((int)offset.Y).ToString();
                _labelOffsetXSliders[i].SetBlockSignals(false);
                _labelOffsetYSliders[i].SetBlockSignals(false);
            }
        }

        private void OnLabelAutoCenterXToggled(bool enabled)
        {
            if (_player != null)
            {
                _player.SetLabelAutoCenterX(enabled);
            }
            SyncLabelOffsetSlidersFromPlayer();
            PushCurrentStateToHistory();
        }
        #endregion

        #region Calibration and Responsive Logic
        private void OnCalibrationToggled(bool enabled)
        {
            _calibrationEnabled = enabled;

            if (_gridManager != null)
            {
                _gridManager.SetAdaptiveCalibrationEnabled(enabled);
                GD.Print($"[DebugPanel] Calibration toggled: {enabled}");
            }

            if (enabled)
            {
                ApplyCalibrationValues();
                UpdateCalibrationZoomLimits();
                if (_responsiveCheck != null && _responsiveCheck.ButtonPressed)
                    GD.Print("[DebugPanel] Hint: Responsive layout will change camera zoom, line width calibration reference points may need adjustment");
            }
            else
            {
                if (_camera != null)
                {
                    _camera.SetCalibrationZoomLimits(false);
                    GD.Print("[DebugPanel] Calibration zoom limit disabled");
                }
                if (_gridManager != null)
                    _gridManager.SetLineWidthScale((float)_gridLineWidthSlider.Value);
            }
            UpdateControlStates();
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
            _gridManager.SetLineWidthCalibration((float)zoomA, (float)widthA, (float)zoomB, (float)widthB);

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
                _camera.SetCalibrationZoomLimits(true, (float)minZoomLimit, (float)maxZoomLimit);
                GD.Print($"[DebugPanel] Calibration zoom limit enabled: zoom [{minZoomLimit:F1} - {maxZoomLimit:F1}]");
            }
        }

        private void OnResponsiveToggled(bool enabled)
        {
            if (_gridManager != null)
            {
                GD.Print($"[DebugPanel] Setting responsive mode: {enabled}, visibleGridsX={_visibleGridsXSpin.Value}");
                var gm = _gridManager as GridManager;
                if (enabled && gm != null)
                    gm.VisibleGridsX = (float)_visibleGridsXSpin.Value;
                gm?.SetResponsiveMode(enabled);
            }
            else
            {
                GD.PrintErr("[DebugPanel] Cannot toggle responsive: _gridManager is null");
            }

            if (_applyCalibrationBtn != null)
                _applyCalibrationBtn.TooltipText = "启用自适应校准";

            UpdateControlStates();
        }

        private void OnVisibleGridsChanged(double value)
        {
            var gm = _gridManager as GridManager;
            if (gm != null && gm.ResponsiveMode)
            {
                gm.VisibleGridsX = (float)_visibleGridsXSpin.Value;
                gm.UpdateResponsiveGridSize();
            }
        }

        private void OnEditorDragButtonChanged(long index)
        {
            // 0=Left, 1=Right, 2=Middle
            MouseButton[] buttonMap = new MouseButton[] { MouseButton.Left, MouseButton.Right, MouseButton.Middle };
            MouseButton selectedButton = buttonMap[index];

            // Update camera controller
            var buttons = new System.Collections.Generic.List<MouseButton> { selectedButton };
            if (_camera != null)
            {
                _camera.SetDragButtons(buttons);
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
                if (_player != null)
                {
                    _player.SetFontSize(0);
                    _player.RefreshLabels();
                }
            }
            else
            {
                if (_player != null)
                {
                    _player.SetFontSize((int)_fontSizeSlider.Value);
                    _player.RefreshLabels();
                }
            }
            UpdateControlStates();
        }

        private void UpdateControlStates()
        {
            bool freeLook = _freeLookCheck?.ButtonPressed ?? false;
            bool responsive = _responsiveCheck?.ButtonPressed ?? false;
            bool calibration = _calibrationEnabled;
            bool fontAutoSize = _fontAutoSizeCheck?.ButtonPressed ?? false;

            Color dim = new Color(0.5f, 0.5f, 0.5f, 1.0f);
            Color normal = new Color(1.0f, 1.0f, 1.0f, 1.0f);

            // Zoom slider: disabled by free look OR responsive layout
            if (_zoomSlider != null)
            {
                bool enabled = !freeLook && !responsive;
                _zoomSlider.Editable = enabled;
                _zoomSlider.Modulate = enabled ? normal : dim;
                if (_zoomValue != null)
                {
                    _zoomValue.Modulate = enabled ? normal : dim;
                    if (freeLook || responsive)
                        _zoomValue.Text = $"{_zoomSlider.Value:F1} (自动)";
                    else
                        _zoomValue.Text = $"{_zoomSlider.Value:F1}";
                }
            }

            // Grid size slider: disabled by responsive layout
            if (_gridSizeSlider != null)
            {
                _gridSizeSlider.Editable = !responsive;
                _gridSizeSlider.Modulate = responsive ? dim : normal;
                if (_gridSizeValue != null)
                {
                    _gridSizeValue.Modulate = responsive ? dim : normal;
                    if (responsive)
                        _gridSizeValue.Text = $"{(int)_gridSizeSlider.Value} (自动)";
                    else
                        _gridSizeValue.Text = ((int)_gridSizeSlider.Value).ToString();
                }
            }

            // Camera return settings: disabled by free look
            if (_cameraReturnDelaySlider != null)
            {
                _cameraReturnDelaySlider.Editable = !freeLook;
                _cameraReturnDelaySlider.Modulate = freeLook ? dim : normal;
            }
            if (_cameraReturnDelayValue != null)
                _cameraReturnDelayValue.Modulate = freeLook ? dim : normal;
            if (_cameraReturnSpeedSlider != null)
            {
                _cameraReturnSpeedSlider.Editable = !freeLook;
                _cameraReturnSpeedSlider.Modulate = freeLook ? dim : normal;
            }
            if (_cameraReturnSpeedValue != null)
                _cameraReturnSpeedValue.Modulate = freeLook ? dim : normal;
            if (_cameraEaseTypeOption != null)
            {
                _cameraEaseTypeOption.Disabled = freeLook;
                _cameraEaseTypeOption.Modulate = freeLook ? dim : normal;
            }
            if (_cameraEasePowerSlider != null)
            {
                _cameraEasePowerSlider.Editable = !freeLook;
                _cameraEasePowerSlider.Modulate = freeLook ? dim : normal;
            }
            if (_cameraEasePowerValue != null)
                _cameraEasePowerValue.Modulate = freeLook ? dim : normal;

            if (freeLook)
            {
                if (_cameraReturnDelayValue != null)
                    _cameraReturnDelayValue.Text = "自由视角";
                if (_cameraReturnSpeedValue != null)
                    _cameraReturnSpeedValue.Text = "自由视角";
            }
            else
            {
                if (_cameraReturnDelayValue != null)
                    _cameraReturnDelayValue.Text = $"{_cameraReturnDelaySlider.Value:F1}s";
                if (_cameraReturnSpeedValue != null)
                    _cameraReturnSpeedValue.Text = ((int)_cameraReturnSpeedSlider.Value).ToString();
            }

            // Line width scale: disabled by calibration
            if (_lineWidthScaleSlider != null)
            {
                _lineWidthScaleSlider.Editable = !calibration;
                _lineWidthScaleSlider.Modulate = calibration ? dim : normal;
            }
            if (_lineWidthScaleValue != null)
                _lineWidthScaleValue.Modulate = calibration ? dim : normal;

            // Grid line width label
            if (_gridLineWidthValue != null)
            {
                if (calibration)
                    _gridLineWidthValue.Text = $"{_gridLineWidthSlider.Value:F1}px (自适应)";
                else
                    _gridLineWidthValue.Text = $"{_gridLineWidthSlider.Value:F1}px";
            }

            // Font size: disabled by auto size
            if (_fontSizeSlider != null)
            {
                _fontSizeSlider.Editable = !fontAutoSize;
                _fontSizeSlider.Modulate = fontAutoSize ? dim : normal;
            }
            if (_fontSizeValue != null)
            {
                _fontSizeValue.Modulate = fontAutoSize ? dim : normal;
                if (fontAutoSize)
                    _fontSizeValue.Text = "自动";
                else
                    _fontSizeValue.Text = ((int)_fontSizeSlider.Value).ToString();
            }

            // Calibration reference points: dim when disabled
            float refModulate = calibration ? 1.0f : 0.6f;
            if (_refZoomASpin != null) _refZoomASpin.Modulate = new Color(refModulate, refModulate, refModulate, refModulate);
            if (_refWidthASpin != null) _refWidthASpin.Modulate = new Color(refModulate, refModulate, refModulate, refModulate);
            if (_refZoomBSpin != null) _refZoomBSpin.Modulate = new Color(refModulate, refModulate, refModulate, refModulate);
            if (_refWidthBSpin != null) _refWidthBSpin.Modulate = new Color(refModulate, refModulate, refModulate, refModulate);
        }
        #endregion

        #region Event Handlers - Health Bar
        private void OnHealthBarVisibleToggled(bool enabled)
        {
            if (_player != null)
                _player.SetHealthBarVisible(enabled);
            PushCurrentStateToHistory();
        }

        private void OnHealthBarColorPressed()
        {
            if (_player == null) return;
            var currentColor = _player.HealthBarColor;
            int currentIdx = System.Array.IndexOf(COLOR_PRESETS, currentColor);
            // Add green to cycle if not found
            Color[] hpColors = new Color[] { new Color(0, 0.8f, 0, 1), Colors.Red, Colors.Yellow, Colors.Cyan, Colors.White };
            int idx = 0;
            for (int i = 0; i < hpColors.Length; i++)
            {
                if (currentColor.IsEqualApprox(hpColors[i])) { idx = i; break; }
            }
            int nextIdx = (idx + 1) % hpColors.Length;
            _player.SetHealthBarColor(hpColors[nextIdx]);
            _healthBarColorBtn.Modulate = hpColors[nextIdx];
            PushCurrentStateToHistory();
        }

        private void OnHealthBarLengthChanged(double value)
        {
            _healthBarLengthValue.Text = ((int)value).ToString();
            if (_player != null)
            {
                int gridSize = (int)_gridSizeSlider.Value;
                float scale = gridSize > 0 ? (float)(value / gridSize) : 0.0f;
                _player.SetHealthBarLength((float)value);
                _player.HealthBarLengthScale = scale;
                if (_healthBarLengthScaleSlider != null)
                {
                    _healthBarLengthScaleSlider.SetBlockSignals(true);
                    _healthBarLengthScaleSlider.Value = scale;
                    _healthBarLengthScaleSlider.SetBlockSignals(false);
                    _healthBarLengthScaleValue.Text = scale.ToString("F2");
                }
            }
        }

        private void OnHealthBarLengthScaleChanged(double value)
        {
            _healthBarLengthScaleValue.Text = value.ToString("F2");
            if (_player != null)
            {
                _player.SetHealthBarLengthScale((float)value);
                float newLength = _player.HealthBarLength;
                if (_healthBarLengthSlider != null)
                {
                    _healthBarLengthSlider.SetBlockSignals(true);
                    _healthBarLengthSlider.Value = newLength;
                    _healthBarLengthSlider.SetBlockSignals(false);
                    _healthBarLengthValue.Text = ((int)newLength).ToString();
                }
            }
        }

        private void OnHealthBarHeightChanged(double value)
        {
            _healthBarHeightValue.Text = ((int)value).ToString();
            if (_player != null)
            {
                int gridSize = (int)_gridSizeSlider.Value;
                float scale = gridSize > 0 ? (float)(value / gridSize) : 0.0f;
                _player.SetHealthBarHeight((float)value);
                _player.HealthBarHeightScale = scale;
                if (_healthBarHeightScaleSlider != null)
                {
                    _healthBarHeightScaleSlider.SetBlockSignals(true);
                    _healthBarHeightScaleSlider.Value = scale;
                    _healthBarHeightScaleSlider.SetBlockSignals(false);
                    _healthBarHeightScaleValue.Text = scale.ToString("F2");
                }
            }
        }

        private void OnHealthBarHeightScaleChanged(double value)
        {
            _healthBarHeightScaleValue.Text = value.ToString("F2");
            if (_player != null)
            {
                _player.SetHealthBarHeightScale((float)value);
                float newHeight = _player.HealthBarHeight;
                if (_healthBarHeightSlider != null)
                {
                    _healthBarHeightSlider.SetBlockSignals(true);
                    _healthBarHeightSlider.Value = newHeight;
                    _healthBarHeightSlider.SetBlockSignals(false);
                    _healthBarHeightValue.Text = ((int)newHeight).ToString();
                }
            }
        }

        private void OnHealthBarFillChanged(double value)
        {
            _healthBarFillValue.Text = $"{(int)value}%";
            if (_player != null)
                _player.SetHealthBarFillPercent((float)(value / 100.0));
        }

        private void OnHealthBarOffsetXChanged(double value)
        {
            _healthBarOffsetXValue.Text = ((int)value).ToString();
            if (_player != null)
            {
                var offset = _player.GetHealthBarOffset();
                _player.SetHealthBarOffset(new Vector2((float)value, offset.Y));
            }
        }

        private void OnHealthBarOffsetYChanged(double value)
        {
            _healthBarOffsetYValue.Text = ((int)value).ToString();
            if (_player != null)
            {
                var offset = _player.GetHealthBarOffset();
                _player.SetHealthBarOffset(new Vector2(offset.X, (float)value));
            }
        }
        #endregion

        #region Event Handlers - Cast Bar
        private void OnCastBarVisibleToggled(bool enabled)
        {
            if (_player != null)
                _player.SetCastBarVisible(enabled);
            PushCurrentStateToHistory();
        }

        private void OnCastBarColorPressed()
        {
            if (_player == null) return;
            var currentColor = _player.CastBarColor;
            Color[] ctColors = new Color[] { new Color(0.3f, 0.5f, 1, 1), new Color(1, 0.5f, 0, 1), new Color(0.8f, 0.2f, 1, 1), Colors.White };
            int idx = 0;
            for (int i = 0; i < ctColors.Length; i++)
            {
                if (currentColor.IsEqualApprox(ctColors[i])) { idx = i; break; }
            }
            int nextIdx = (idx + 1) % ctColors.Length;
            _player.SetCastBarColor(ctColors[nextIdx]);
            _castBarColorBtn.Modulate = ctColors[nextIdx];
            PushCurrentStateToHistory();
        }

        private void OnCastBarLengthChanged(double value)
        {
            _castBarLengthValue.Text = ((int)value).ToString();
            if (_player != null) _player.SetCastBarLength((float)value);
        }

        private void OnCastBarHeightChanged(double value)
        {
            _castBarHeightValue.Text = ((int)value).ToString();
            if (_player != null) _player.SetCastBarHeight((float)value);
        }

        private void OnCastBarFillChanged(double value)
        {
            _castBarFillValue.Text = $"{(int)value}%";
            if (_player != null) _player.SetCastBarFillPercent((float)(value / 100.0));
        }

        private void OnCastBarOffsetXChanged(double value)
        {
            _castBarOffsetXValue.Text = ((int)value).ToString();
            if (_player != null)
            {
                var offset = _player.GetCastBarOffset();
                _player.SetCastBarOffset(new Vector2((float)value, offset.Y));
            }
        }

        private void OnCastBarOffsetYChanged(double value)
        {
            _castBarOffsetYValue.Text = ((int)value).ToString();
            if (_player != null)
            {
                var offset = _player.GetCastBarOffset();
                _player.SetCastBarOffset(new Vector2(offset.X, (float)value));
            }
        }
        #endregion

        #region Event Handlers - Level Badge
        private void OnLevelBadgeVisibleToggled(bool enabled)
        {
            if (_player != null) _player.SetLevelBadgeVisible(enabled);
            PushCurrentStateToHistory();
        }

        private void OnLevelBadgeTextColorPressed()
        {
            if (_player == null) return;
            var current = _player.LevelBadgeTextColor;
            Color[] colors = new Color[] { Colors.Yellow, Colors.White, Colors.Cyan, new Color(1, 0.5f, 0, 1), Colors.Black };
            int idx = 0;
            for (int i = 0; i < colors.Length; i++) { if (current.IsEqualApprox(colors[i])) { idx = i; break; } }
            int next = (idx + 1) % colors.Length;
            _player.SetLevelBadgeTextColor(colors[next]);
            _levelBadgeTextColorBtn.Modulate = colors[next];
            PushCurrentStateToHistory();
        }

        private void OnLevelBadgeTextChanged(string newText)
        {
            if (_player != null) _player.SetLevelBadgeText(newText);
        }

        private void OnLevelBadgeFontSizeChanged(double value)
        {
            _levelBadgeFontSizeValue.Text = ((int)value).ToString();
            if (_player != null) _player.SetLevelBadgeFontSize((float)value);
        }

        private void OnLevelBadgeOffsetXChanged(double value)
        {
            _levelBadgeOffsetXValue.Text = ((int)value).ToString();
            if (_player != null)
            {
                var offset = _player.GetLevelBadgeOffset();
                _player.SetLevelBadgeOffset(new Vector2((float)value, offset.Y));
            }
        }

        private void OnLevelBadgeOffsetYChanged(double value)
        {
            _levelBadgeOffsetYValue.Text = ((int)value).ToString();
            if (_player != null)
            {
                var offset = _player.GetLevelBadgeOffset();
                _player.SetLevelBadgeOffset(new Vector2(offset.X, (float)value));
            }
        }
        #endregion

        #region Monster Config Handlers
        private void OnSaveMonsterConfigPressed()
        {
            var cm = GetTree()?.GetFirstNodeInGroup("monster_config_manager") as MonsterConfigManager;
            if (cm == null)
            {
                GD.PushError("[DebugPanel] MonsterConfigManager not found");
                return;
            }

            cm.SetMoveSpeedMs((int)_monsterMoveSpeedSlider.Value);
            var ai = new AiDefaults
            {
                PatrolRange = (int)_monsterPatrolRangeSlider.Value,
                AggroRange = (int)_monsterAggroRangeSlider.Value,
                MoveIntervalMs = (int)_monsterMoveIntervalSlider.Value,
                ChaseIntervalMs = (int)_monsterMoveIntervalSlider.Value / 4,
            };
            cm.SetAiDefaults("patrol_chase", ai);
            cm.SetAiDefaults("patrol", new AiDefaults
            {
                PatrolRange = (int)_monsterPatrolRangeSlider.Value,
                MoveIntervalMs = (int)_monsterMoveIntervalSlider.Value,
            });
            cm.SetAiDefaults("guard", new AiDefaults
            {
                PatrolRange = 0,
                AggroRange = (int)_monsterAggroRangeSlider.Value,
                MoveIntervalMs = (int)_monsterMoveIntervalSlider.Value,
            });
            cm.SetMoveSystem(new MoveSystem
            {
                CheckRatio = (int)_moveCheckRatioSlider.Value,
                DualGridStartRatio = (int)_moveDualStartSlider.Value,
                DualGridEndRatio = (int)_moveDualEndSlider.Value,
            });
            cm.SaveConfig();
            GD.Print("[DebugPanel] Monster + Move config saved to JSON");
        }
        #endregion
    }
}
