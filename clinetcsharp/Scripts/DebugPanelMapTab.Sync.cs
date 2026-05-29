using Godot;

namespace ClinetCSharp
{
    public partial class DebugPanelMapTab
    {
        public override void SyncToCurrentValues()
        {
            if (GridManager != null)
            {
                if (_gridSizeModeOption != null)
                    _gridSizeModeOption.Select((int)GridManager.GetGridSizeMode());
                if (_lineWidthModeOption != null)
                    _lineWidthModeOption.Select((int)GridManager.GetGridLineWidthMode());

                _gridSizeSlider.SetBlockSignals(true);
                _gridSizeSlider.Value = (int)GridManager.GridSize;
                bool responsiveMode = GridManager.ResponsiveMode;
                _gridSizeValue.Text = responsiveMode ? $"{GridManager.GridSize}（自动）" : GridManager.GridSize.ToString();
                _gridSizeSlider.SetBlockSignals(false);

                if (_gridAntiAliasSoftnessSlider != null)
                {
                    _gridAntiAliasSoftnessSlider.SetBlockSignals(true);
                    _gridAntiAliasSoftnessSlider.Value = GridManager.GetGridAntiAliasSoftness();
                    _gridAntiAliasSoftnessSlider.SetBlockSignals(false);
                    if (_gridAntiAliasSoftnessValue != null)
                        _gridAntiAliasSoftnessValue.Text = $"{GridManager.GetGridAntiAliasSoftness():F1}x";
                }
            }

            UpdateStrategySectionVisibility();
        }

        public void SyncZoomFromCamera()
        {
            if (Camera != null && _zoomSlider != null)
            {
                float cameraZoom = Camera.Zoom.X;
                if (Mathf.Abs(cameraZoom - (float)_zoomSlider.Value) > 0.01f && !Owner._isZoomSliderDragging)
                {
                    _zoomSlider.Value = cameraZoom;
                    _zoomValue.Text = $"{cameraZoom:F1}";
                }
            }
        }

        public void UpdateControlStates()
        {
            UpdateControlStates(false);
        }

        public void UpdateControlStates(bool fontAutoSize)
        {
            bool freeLook = _freeLookCheck?.ButtonPressed ?? false;
            bool responsive = CurrentGridSizeMode == GridManager.GridSizeMode.ResponsiveVisibleCount;
            bool calibration = CurrentLineWidthMode == GridManager.GridLineWidthMode.AdaptiveCalibration;
            bool fixedScreenLineWidth = CurrentLineWidthMode == GridManager.GridLineWidthMode.FixedScreen;

            Color dim = new(0.5f, 0.5f, 0.5f, 1.0f);
            Color normal = new(1.0f, 1.0f, 1.0f, 1.0f);

            if (_zoomSlider != null)
            {
                bool enabled = !freeLook && !responsive;
                _zoomSlider.Editable = enabled;
                _zoomSlider.Modulate = enabled ? normal : dim;
                if (_zoomValue != null)
                {
                    _zoomValue.Modulate = enabled ? normal : dim;
                    _zoomValue.Text = freeLook || responsive ? $"{_zoomSlider.Value:F1}（自动）" : $"{_zoomSlider.Value:F1}";
                }
            }

            if (_gridSizeSlider != null)
            {
                _gridSizeSlider.Editable = !responsive;
                _gridSizeSlider.Modulate = responsive ? dim : normal;
                if (_gridSizeValue != null)
                {
                    _gridSizeValue.Modulate = responsive ? dim : normal;
                    _gridSizeValue.Text = responsive ? $"{(int)_gridSizeSlider.Value}（自动）" : ((int)_gridSizeSlider.Value).ToString();
                }
            }

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

            if (_lineWidthScaleSlider != null)
            {
                _lineWidthScaleSlider.Editable = fixedScreenLineWidth;
                _lineWidthScaleSlider.Modulate = fixedScreenLineWidth ? normal : dim;
            }
            if (_lineWidthScaleValue != null)
                _lineWidthScaleValue.Modulate = fixedScreenLineWidth ? normal : dim;

            if (_gridLineWidthSlider != null)
            {
                bool enabled = !calibration;
                _gridLineWidthSlider.Editable = enabled;
                _gridLineWidthSlider.Modulate = enabled ? normal : dim;
            }
            if (_gridLineWidthValue != null)
                _gridLineWidthValue.Modulate = calibration ? dim : normal;

            float refModulate = calibration ? 1.0f : 0.6f;
            if (_refZoomASpin != null) _refZoomASpin.Modulate = new Color(refModulate, refModulate, refModulate, refModulate);
            if (_refWidthASpin != null) _refWidthASpin.Modulate = new Color(refModulate, refModulate, refModulate, refModulate);
            if (_refZoomBSpin != null) _refZoomBSpin.Modulate = new Color(refModulate, refModulate, refModulate, refModulate);
            if (_refWidthBSpin != null) _refWidthBSpin.Modulate = new Color(refModulate, refModulate, refModulate, refModulate);

            UpdateStrategySectionVisibility();
        }
    }
}
