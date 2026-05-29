using Godot;

namespace ClinetCSharp
{
    public partial class DebugPanelMapTab
    {
        public override void ConnectSignals()
        {
            if (_gridSizeModeOption != null)
                _gridSizeModeOption.ItemSelected += OnGridSizeModeChanged;
            if (_gridSizeSlider != null)
            {
                _gridSizeSlider.ValueChanged += OnGridSizeChanged;
                _gridSizeSlider.DragEnded += OnGridSizeDragEnded;
            }
            if (_zoomSlider != null)
            {
                _zoomSlider.ValueChanged += OnZoomChanged;
                _zoomSlider.DragEnded += OnZoomDragEnded;
                _zoomSlider.DragStarted += OnZoomDragStarted;
            }

            if (_lineWidthModeOption != null)
                _lineWidthModeOption.ItemSelected += OnLineWidthModeChanged;
            if (_gridLineWidthSlider != null)
            {
                _gridLineWidthSlider.ValueChanged += OnGridLineWidthChanged;
                _gridLineWidthSlider.DragEnded += OnGridLineWidthDragEnded;
            }
            if (_gridLineBrightnessSlider != null)
            {
                _gridLineBrightnessSlider.ValueChanged += OnGridLineBrightnessChanged;
                _gridLineBrightnessSlider.DragEnded += OnGridLineBrightnessDragEnded;
            }
            if (_gridAntiAliasSoftnessSlider != null)
            {
                _gridAntiAliasSoftnessSlider.ValueChanged += OnGridAntiAliasSoftnessChanged;
                _gridAntiAliasSoftnessSlider.DragEnded += OnGridAntiAliasSoftnessDragEnded;
            }
            if (_gridCoordsCheck != null)
                _gridCoordsCheck.Toggled += OnGridCoordsToggled;

            if (_cameraReturnDelaySlider != null)
            {
                _cameraReturnDelaySlider.ValueChanged += OnCameraReturnDelayChanged;
                _cameraReturnDelaySlider.DragEnded += OnCameraReturnDelayDragEnded;
            }
            if (_cameraReturnSpeedSlider != null)
            {
                _cameraReturnSpeedSlider.ValueChanged += OnCameraReturnSpeedChanged;
                _cameraReturnSpeedSlider.DragEnded += OnCameraReturnSpeedDragEnded;
            }
            if (_cameraEaseTypeOption != null)
                _cameraEaseTypeOption.ItemSelected += OnCameraEaseTypeChanged;
            if (_cameraEasePowerSlider != null)
            {
                _cameraEasePowerSlider.ValueChanged += OnCameraEasePowerChanged;
                _cameraEasePowerSlider.DragEnded += OnCameraEasePowerDragEnded;
            }
            if (_debugInfoCheck != null)
                _debugInfoCheck.Toggled += OnDebugInfoToggled;
            if (_cameraDebugCheck != null)
                _cameraDebugCheck.Toggled += OnCameraDebugToggled;
            if (_monsterPatrolOverlayCheck != null)
                _monsterPatrolOverlayCheck.Toggled += OnMonsterPatrolOverlayToggled;

            if (_freeLookCheck != null)
                _freeLookCheck.Toggled += OnFreeLookToggled;
            if (_lineWidthScaleSlider != null)
            {
                _lineWidthScaleSlider.ValueChanged += OnLineWidthScaleChanged;
                _lineWidthScaleSlider.DragEnded += OnLineWidthScaleDragEnded;
            }
            if (_applyCalibrationBtn != null)
                _applyCalibrationBtn.Toggled += OnCalibrationToggled;
            if (_refZoomASpin != null)
                _refZoomASpin.ValueChanged += OnCalibrationValueChanged;
            if (_refWidthASpin != null)
                _refWidthASpin.ValueChanged += OnCalibrationValueChanged;
            if (_refZoomBSpin != null)
                _refZoomBSpin.ValueChanged += OnCalibrationValueChanged;
            if (_refWidthBSpin != null)
                _refWidthBSpin.ValueChanged += OnCalibrationValueChanged;
            if (_responsiveCheck != null)
                _responsiveCheck.Toggled += OnResponsiveToggled;
            if (_visibleGridsXSpin != null)
                _visibleGridsXSpin.ValueChanged += OnVisibleGridsChanged;
            if (_editorDragButtonOption != null)
                _editorDragButtonOption.ItemSelected += OnEditorDragButtonChanged;
            if (_editorSelectModCheck != null)
                _editorSelectModCheck.Toggled += OnEditorSelectModChanged;
        }

        public override void DisconnectSignals()
        {
            if (_gridSizeModeOption != null)
                _gridSizeModeOption.ItemSelected -= OnGridSizeModeChanged;
            if (_gridSizeSlider != null)
            {
                _gridSizeSlider.ValueChanged -= OnGridSizeChanged;
                _gridSizeSlider.DragEnded -= OnGridSizeDragEnded;
            }
            if (_zoomSlider != null)
            {
                _zoomSlider.ValueChanged -= OnZoomChanged;
                _zoomSlider.DragEnded -= OnZoomDragEnded;
                _zoomSlider.DragStarted -= OnZoomDragStarted;
            }

            if (_lineWidthModeOption != null)
                _lineWidthModeOption.ItemSelected -= OnLineWidthModeChanged;
            if (_gridLineWidthSlider != null)
            {
                _gridLineWidthSlider.ValueChanged -= OnGridLineWidthChanged;
                _gridLineWidthSlider.DragEnded -= OnGridLineWidthDragEnded;
            }
            if (_gridLineBrightnessSlider != null)
            {
                _gridLineBrightnessSlider.ValueChanged -= OnGridLineBrightnessChanged;
                _gridLineBrightnessSlider.DragEnded -= OnGridLineBrightnessDragEnded;
            }
            if (_gridAntiAliasSoftnessSlider != null)
            {
                _gridAntiAliasSoftnessSlider.ValueChanged -= OnGridAntiAliasSoftnessChanged;
                _gridAntiAliasSoftnessSlider.DragEnded -= OnGridAntiAliasSoftnessDragEnded;
            }
            if (_gridCoordsCheck != null)
                _gridCoordsCheck.Toggled -= OnGridCoordsToggled;

            if (_cameraReturnDelaySlider != null)
            {
                _cameraReturnDelaySlider.ValueChanged -= OnCameraReturnDelayChanged;
                _cameraReturnDelaySlider.DragEnded -= OnCameraReturnDelayDragEnded;
            }
            if (_cameraReturnSpeedSlider != null)
            {
                _cameraReturnSpeedSlider.ValueChanged -= OnCameraReturnSpeedChanged;
                _cameraReturnSpeedSlider.DragEnded -= OnCameraReturnSpeedDragEnded;
            }
            if (_cameraEaseTypeOption != null)
                _cameraEaseTypeOption.ItemSelected -= OnCameraEaseTypeChanged;
            if (_cameraEasePowerSlider != null)
            {
                _cameraEasePowerSlider.ValueChanged -= OnCameraEasePowerChanged;
                _cameraEasePowerSlider.DragEnded -= OnCameraEasePowerDragEnded;
            }
            if (_debugInfoCheck != null)
                _debugInfoCheck.Toggled -= OnDebugInfoToggled;
            if (_cameraDebugCheck != null)
                _cameraDebugCheck.Toggled -= OnCameraDebugToggled;
            if (_monsterPatrolOverlayCheck != null)
                _monsterPatrolOverlayCheck.Toggled -= OnMonsterPatrolOverlayToggled;

            if (_freeLookCheck != null)
                _freeLookCheck.Toggled -= OnFreeLookToggled;
            if (_lineWidthScaleSlider != null)
            {
                _lineWidthScaleSlider.ValueChanged -= OnLineWidthScaleChanged;
                _lineWidthScaleSlider.DragEnded -= OnLineWidthScaleDragEnded;
            }
            if (_applyCalibrationBtn != null)
                _applyCalibrationBtn.Toggled -= OnCalibrationToggled;
            if (_refZoomASpin != null)
                _refZoomASpin.ValueChanged -= OnCalibrationValueChanged;
            if (_refWidthASpin != null)
                _refWidthASpin.ValueChanged -= OnCalibrationValueChanged;
            if (_refZoomBSpin != null)
                _refZoomBSpin.ValueChanged -= OnCalibrationValueChanged;
            if (_refWidthBSpin != null)
                _refWidthBSpin.ValueChanged -= OnCalibrationValueChanged;
            if (_responsiveCheck != null)
                _responsiveCheck.Toggled -= OnResponsiveToggled;
            if (_visibleGridsXSpin != null)
                _visibleGridsXSpin.ValueChanged -= OnVisibleGridsChanged;
            if (_editorDragButtonOption != null)
                _editorDragButtonOption.ItemSelected -= OnEditorDragButtonChanged;
            if (_editorSelectModCheck != null)
                _editorSelectModCheck.Toggled -= OnEditorSelectModChanged;
        }
    }
}
