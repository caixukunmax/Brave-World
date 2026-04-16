using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// DebugPanel partial — 撤销系统
    /// </summary>
    public partial class DebugPanel
    {
        #region Undo System
        private void PushCurrentStateToHistory()
        {
            if (_isRestoring)
                return;

            Godot.Collections.Dictionary state = CaptureCurrentState();

            // If same as last state, don't add
            if (_configHistory.Count > 0)
            {
                Godot.Collections.Dictionary lastState = _configHistory[_configHistory.Count - 1];
                if (StatesEqual(state, lastState))
                    return;
            }

            _configHistory.Add(state);

            // Limit history size
            if (_configHistory.Count > MAX_HISTORY_STEPS)
            {
                _configHistory.RemoveAt(0);
            }

            _historyIndex = _configHistory.Count - 1;
        }

        private Godot.Collections.Dictionary CaptureCurrentState()
        {
            Godot.Collections.Dictionary state = new Godot.Collections.Dictionary
            {
                ["grid_size"] = _gridSizeSlider.Value,
                ["zoom"] = _zoomSlider.Value,
                ["grid_line_width"] = _gridLineWidthSlider.Value,
                ["grid_line_brightness"] = _gridLineBrightnessSlider.Value,
                ["camera_return_delay"] = _cameraReturnDelaySlider.Value,
                ["camera_return_speed"] = _cameraReturnSpeedSlider.Value,
                ["camera_ease_type"] = _cameraEaseTypeOption.Selected,
                ["camera_ease_power"] = _cameraEasePowerSlider.Value,
                ["player_size"] = _playerSizeSlider.Value,
                ["visual_size_scale"] = _playerSizeScaleSlider?.Value ?? 1.0,
                ["border_width"] = _borderWidthSlider.Value,
                ["border_width_scale"] = _borderWidthScaleSlider?.Value ?? (3.0 / 111.0),
                ["corner_radius"] = _cornerRadiusSlider.Value,
                ["bg_opacity"] = _bgOpacitySlider.Value,
                ["font_size"] = _fontSizeSlider.Value,
                ["line_spacing"] = _lineSpacingSlider.Value,
                ["letter_spacing"] = _letterSpacingSlider.Value,
            };

            if (_lineWidthScaleSlider != null)
                state["line_width_scale"] = _lineWidthScaleSlider.Value;
            if (_fontAutoSizeCheck != null)
                state["font_auto_size"] = _fontAutoSizeCheck.ButtonPressed;
            if (_labelAutoCenterXCheck != null)
                state["label_auto_center_x"] = _labelAutoCenterXCheck.ButtonPressed;
            if (_healthBarLengthScaleSlider != null)
                state["healthbar_length_scale"] = _healthBarLengthScaleSlider.Value;
            if (_healthBarHeightScaleSlider != null)
                state["healthbar_height_scale"] = _healthBarHeightScaleSlider.Value;
            if (_responsiveCheck != null)
            {
                state["responsive_enabled"] = _responsiveCheck.ButtonPressed;
                state["visible_grids_x"] = _visibleGridsXSpin.Value;
            }
            if (_freeLookCheck != null)
                state["free_look"] = _freeLookCheck.ButtonPressed;

            return state;
        }

        private bool StatesEqual(Godot.Collections.Dictionary state1, Godot.Collections.Dictionary state2)
        {
            foreach (var key in state1.Keys)
            {
                if (!state2.ContainsKey(key) || !state1[key].Equals(state2[key]))
                    return false;
            }
            return true;
        }

        private void UndoLastChange()
        {
            if (_configHistory.Count <= 1)
            {
                GD.Print("[DebugPanel] No actions to undo");
                return;
            }

            // Remove current state
            _configHistory.RemoveAt(_configHistory.Count - 1);

            // Get previous state
            Godot.Collections.Dictionary prevState = _configHistory[_configHistory.Count - 1];

            _isRestoring = true;
            ApplyState(prevState);
            _isRestoring = false;

            GD.Print("[DebugPanel] Undo performed");

            // Show notification
            ShowUndoNotification();
        }

        private void ApplyState(Godot.Collections.Dictionary state)
        {
            // Basic settings
            if (state.ContainsKey("grid_size"))
                _gridSizeSlider.Value = (double)state["grid_size"];
            if (state.ContainsKey("zoom"))
                _zoomSlider.Value = (double)state["zoom"];

            // Grid line settings
            if (state.ContainsKey("grid_line_width"))
                _gridLineWidthSlider.Value = (double)state["grid_line_width"];
            if (state.ContainsKey("grid_line_brightness"))
                _gridLineBrightnessSlider.Value = (double)state["grid_line_brightness"];

            // Camera settings
            if (state.ContainsKey("camera_return_delay"))
                _cameraReturnDelaySlider.Value = (double)state["camera_return_delay"];
            if (state.ContainsKey("camera_return_speed"))
                _cameraReturnSpeedSlider.Value = (double)state["camera_return_speed"];
            if (state.ContainsKey("camera_ease_type"))
                _cameraEaseTypeOption.Select((int)state["camera_ease_type"]);
            if (state.ContainsKey("camera_ease_power"))
                _cameraEasePowerSlider.Value = (double)state["camera_ease_power"];

            // Player appearance
            if (state.ContainsKey("player_size"))
                _playerSizeSlider.Value = (double)state["player_size"];
            if (state.ContainsKey("visual_size_scale") && _playerSizeScaleSlider != null)
            {
                _playerSizeScaleSlider.SetBlockSignals(true);
                _playerSizeScaleSlider.Value = (double)state["visual_size_scale"];
                _playerSizeScaleSlider.SetBlockSignals(false);
            }
            if (state.ContainsKey("border_width"))
                _borderWidthSlider.Value = (double)state["border_width"];
            if (state.ContainsKey("border_width_scale") && _borderWidthScaleSlider != null)
            {
                _borderWidthScaleSlider.SetBlockSignals(true);
                _borderWidthScaleSlider.Value = (double)state["border_width_scale"];
                _borderWidthScaleSlider.SetBlockSignals(false);
            }
            if (state.ContainsKey("corner_radius"))
                _cornerRadiusSlider.Value = (double)state["corner_radius"];
            if (state.ContainsKey("bg_opacity"))
                _bgOpacitySlider.Value = (double)state["bg_opacity"];

            // Text style
            if (state.ContainsKey("font_size"))
                _fontSizeSlider.Value = (double)state["font_size"];
            if (state.ContainsKey("line_spacing"))
                _lineSpacingSlider.Value = (double)state["line_spacing"];
            if (state.ContainsKey("letter_spacing"))
                _letterSpacingSlider.Value = (double)state["letter_spacing"];

            // Dynamic controls
            if (state.ContainsKey("line_width_scale") && _lineWidthScaleSlider != null)
            {
                _lineWidthScaleSlider.SetBlockSignals(true);
                _lineWidthScaleSlider.Value = (double)state["line_width_scale"];
                _lineWidthScaleSlider.SetBlockSignals(false);
            }
            if (state.ContainsKey("font_auto_size") && _fontAutoSizeCheck != null)
                _fontAutoSizeCheck.ButtonPressed = (bool)state["font_auto_size"];
            if (state.ContainsKey("label_auto_center_x") && _labelAutoCenterXCheck != null)
            {
                _labelAutoCenterXCheck.SetBlockSignals(true);
                _labelAutoCenterXCheck.ButtonPressed = (bool)state["label_auto_center_x"];
                _labelAutoCenterXCheck.SetBlockSignals(false);
                OnLabelAutoCenterXToggled(_labelAutoCenterXCheck.ButtonPressed);
            }
            if (state.ContainsKey("healthbar_length_scale") && _healthBarLengthScaleSlider != null)
            {
                _healthBarLengthScaleSlider.SetBlockSignals(true);
                _healthBarLengthScaleSlider.Value = (double)state["healthbar_length_scale"];
                _healthBarLengthScaleSlider.SetBlockSignals(false);
            }
            if (state.ContainsKey("healthbar_height_scale") && _healthBarHeightScaleSlider != null)
            {
                _healthBarHeightScaleSlider.SetBlockSignals(true);
                _healthBarHeightScaleSlider.Value = (double)state["healthbar_height_scale"];
                _healthBarHeightScaleSlider.SetBlockSignals(false);
            }
            if (state.ContainsKey("responsive_enabled") && _responsiveCheck != null)
                _responsiveCheck.ButtonPressed = (bool)state["responsive_enabled"];
            if (state.ContainsKey("visible_grids_x") && _visibleGridsXSpin != null)
                _visibleGridsXSpin.Value = (double)state["visible_grids_x"];
            if (state.ContainsKey("free_look") && _freeLookCheck != null)
                _freeLookCheck.ButtonPressed = (bool)state["free_look"];

            // Trigger all updates
            OnGridSizeDragEnded(true);
            OnZoomDragEnded(true);
            OnGridLineWidthDragEnded(true);
            OnGridLineBrightnessDragEnded(true);
            OnCameraReturnDelayDragEnded(true);
            OnCameraReturnSpeedDragEnded(true);
            OnCameraEaseTypeChanged(_cameraEaseTypeOption.Selected);
            OnCameraEasePowerDragEnded(true);
            OnPlayerSizeDragEnded(true);
            OnPlayerSizeScaleDragEnded(true);
            OnBorderWidthDragEnded(true);
            OnBorderWidthScaleDragEnded(true);
            OnCornerRadiusDragEnded(true);
            OnBgOpacityDragEnded(true);
            OnFontSizeDragEnded(true);
            OnLineSpacingDragEnded(true);
            OnLetterSpacingDragEnded(true);
        }

        private async void ShowUndoNotification()
        {
            Label label = new Label();
            label.Text = "↩ 已撤销";
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.AddThemeColorOverride("font_color", Colors.Green);
            label.AddThemeFontSizeOverride("font_size", 16);

            CanvasLayer canvasLayer = new CanvasLayer();
            canvasLayer.Layer = 101;
            canvasLayer.AddChild(label);
            AddChild(canvasLayer);

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
            label.Position = new Vector2(
                (viewportSize.X - label.Size.X) / 2,
                viewportSize.Y - 200
            );

            await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
            canvasLayer.QueueFree();
        }
        #endregion
    }
}
