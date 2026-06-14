using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// DebugPanel Map Tab — 地图/摄像机/校准/响应式/编辑器键位相关控件和逻辑
    /// </summary>
    public partial class DebugPanelMapTab : DebugPanelTab
    {
        #region Fields - Map Tab Basic Settings
        private HSlider _gridSizeSlider;
        private Label _gridSizeValue;
        private HSlider _zoomSlider;
        private Label _zoomValue;
        #endregion

        #region Fields - Map Tab Grid Line Settings
        private HSlider _gridLineWidthSlider;
        private Label _gridLineWidthValue;
        private HSlider _gridLineBrightnessSlider;
        private Label _gridLineBrightnessValue;
        private HSlider _gridAntiAliasSoftnessSlider;
        private Label _gridAntiAliasSoftnessValue;
        private CheckButton _gridCoordsCheck;
        #endregion

        #region Fields - Map Tab Camera Settings
        private HSlider _cameraReturnDelaySlider;
        private Label _cameraReturnDelayValue;
        private HSlider _cameraReturnSpeedSlider;
        private Label _cameraReturnSpeedValue;
        private OptionButton _cameraEaseTypeOption;
        private HSlider _cameraEasePowerSlider;
        private Label _cameraEasePowerValue;
        #endregion

        #region Fields - Map Tab Debug Toggles
        private CheckButton _debugInfoCheck;
        private CheckButton _cameraDebugCheck;
        private CheckButton _showOutsideMapGrayCheck;
        #endregion

        #region Fields - Dynamic Created Controls (map-related)
        private CheckButton _freeLookCheck;
        private HSlider _lineWidthScaleSlider;
        private Label _lineWidthScaleValue;
        private SpinBox _refZoomASpin;
        private SpinBox _refWidthASpin;
        private SpinBox _refZoomBSpin;
        private SpinBox _refWidthBSpin;
        private CheckButton _applyCalibrationBtn;
        private CheckButton _responsiveCheck;
        private SpinBox _visibleGridsXSpin;
        #endregion

        #region Fields - Editor Key Config Controls
        private OptionButton _editorDragButtonOption;
        private CheckButton _editorSelectModCheck;
        private CheckButton _monsterPatrolOverlayCheck;
        private HSlider _hoverTooltipWidthSlider;
        private Label _hoverTooltipWidthValue;

        private VBoxContainer _gridStrategySection;
        private VBoxContainer _gridManualParams;
        private VBoxContainer _gridResponsiveParams;
        private OptionButton _gridSizeModeOption;
        private VBoxContainer _lineWidthStrategySection;
        private VBoxContainer _lineWidthFixedWorldParams;
        private VBoxContainer _lineWidthFixedScreenParams;
        private VBoxContainer _lineWidthAdaptiveParams;
        private OptionButton _lineWidthModeOption;

        #endregion

        #region State
        private bool _calibrationEnabled = false;
        #endregion

        public DebugPanelMapTab(DebugPanel owner) : base(owner) { }

        public override string TabKey => "map";

        // Expose _gridSizeSlider for cross-tab access (Owner._gridSizeSlider)
        public HSlider GridSizeSlider => _gridSizeSlider;
        // Expose _zoomSlider for _Process sync in Owner
        public HSlider ZoomSlider => _zoomSlider;
        public Label ZoomValue => _zoomValue;
        // Expose _freeLookCheck so Owner.UpdateControlStates can reference it
        public CheckButton FreeLookCheck => _freeLookCheck;
        // Expose _responsiveCheck
        public CheckButton ResponsiveCheck => _responsiveCheck;
        // Expose _calibrationEnabled
        public bool CalibrationEnabled => _calibrationEnabled;

        private GridManager.GridLineWidthMode CurrentLineWidthMode =>
            _lineWidthModeOption != null
                ? (GridManager.GridLineWidthMode)_lineWidthModeOption.Selected
                : (_calibrationEnabled ? GridManager.GridLineWidthMode.AdaptiveCalibration : GridManager.GridLineWidthMode.FixedScreen);

        private GridManager.GridSizeMode CurrentGridSizeMode =>
            _gridSizeModeOption != null
                ? (GridManager.GridSizeMode)_gridSizeModeOption.Selected
                : ((_responsiveCheck?.ButtonPressed ?? false) ? GridManager.GridSizeMode.ResponsiveVisibleCount : GridManager.GridSizeMode.Manual);

        #region Event Handlers - Basic Settings
        private void OnGridSizeChanged(double value)
        {
            _gridSizeValue.Text = ((int)value).ToString();
        }

        private void OnGridSizeDragEnded(bool valueChanged)
        {
            int newGridSize = (int)_gridSizeSlider.Value;
            GD.Print($"[DebugPanel] GridSize changed to: {newGridSize}");

            if (GridManager != null)
            {
                GridManager.SetGridSize(newGridSize);
                GD.Print($"[DebugPanel] Called GridManager.SetGridSize({newGridSize})");
            }
            else
            {
                GD.PushError("[DebugPanel] _gridManager is null!");
            }

            // Sync entity tab after grid resize
            // (SetGridSize auto-recalculates scale-dependent values on entities)
            // TODO: EntityTab should react to grid size changes via EntityProfileManager

            GridUpdate();
            Owner.PushCurrentStateToHistory();
        }

        private void OnZoomChanged(double value)
        {
            _zoomValue.Text = $"{value:F1}";
        }

        private void OnZoomDragStarted()
        {
            Owner._isZoomSliderDragging = true;
        }

        private void OnZoomDragEnded(bool valueChanged)
        {
            Owner._isZoomSliderDragging = false;
            if (Camera != null)
            {
                Camera.Zoom = Vector2.One * (float)_zoomSlider.Value;
            }
            GridUpdate();
            Owner.PushCurrentStateToHistory();
        }

        private void GridUpdate()
        {
            if (GridManager != null)
            {
                GridManager.QueueRedraw();
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
            if (GridManager != null)
            {
                if (_calibrationEnabled)
                {
                    GridManager.SetPreviewLineWidth((float)_gridLineWidthSlider.Value);
                    GD.Print($"[DebugPanel] Set preview line width: {_gridLineWidthSlider.Value}");
                }
                else
                {
                    GridManager.SetLineWidthScale((float)_gridLineWidthSlider.Value);
                    GD.Print($"[DebugPanel] Set line width scale: {_gridLineWidthSlider.Value}");
                }
            }
            Owner.PushCurrentStateToHistory();
        }

        private void OnGridLineBrightnessChanged(double value)
        {
            _gridLineBrightnessValue.Text = $"{value:F1}";
        }

        private void OnGridLineBrightnessDragEnded(bool valueChanged)
        {
            if (GridManager != null)
            {
                float brightness = (float)_gridLineBrightnessSlider.Value;
                GridManager.LineColor = new Color(brightness, brightness, brightness);
                GridManager.QueueRedraw();
            }
            Owner.PushCurrentStateToHistory();
        }

        private void OnGridAntiAliasSoftnessChanged(double value)
        {
            _gridAntiAliasSoftnessValue.Text = $"{value:F1}x";
            if (GridManager != null)
            {
                GridManager.SetGridAntiAliasSoftness((float)value);
            }
        }

        private void OnGridAntiAliasSoftnessDragEnded(bool valueChanged)
        {
            Owner.PushCurrentStateToHistory();
        }

        private void OnGridCoordsToggled(bool enabled)
        {
            if (GridManager != null)
            {
                GridManager.SetShowGridCoords(enabled);
            }
            Owner.PushCurrentStateToHistory();
        }

        private void OnLineWidthScaleChanged(double value)
        {
            if (GridManager != null)
            {
                GridManager.SetLineWidthScale((float)value);
                GridManager.QueueRedraw();
            }
        }

        private void OnLineWidthScaleDragEnded(bool valueChanged)
        {
            Owner.PushCurrentStateToHistory();
        }
        #endregion

        #region Event Handlers - Camera Settings
        private void OnCameraReturnDelayChanged(double value)
        {
            _cameraReturnDelayValue.Text = $"{value:F1}s";
        }

        private void OnCameraReturnDelayDragEnded(bool valueChanged)
        {
            if (Camera != null)
            {
                Camera.SetReturnDelay((float)_cameraReturnDelaySlider.Value);
            }
            Owner.PushCurrentStateToHistory();
        }

        private void OnCameraReturnSpeedChanged(double value)
        {
            _cameraReturnSpeedValue.Text = ((int)value).ToString();
        }

        private void OnCameraReturnSpeedDragEnded(bool valueChanged)
        {
            if (Camera != null)
            {
                Camera.SetReturnSpeed((float)_cameraReturnSpeedSlider.Value);
            }
            Owner.PushCurrentStateToHistory();
        }

        private void OnCameraEaseTypeChanged(long index)
        {
            if (Camera != null)
            {
                Camera.SetEaseType((CameraController.EaseType)index);
            }
            Owner.PushCurrentStateToHistory();
        }

        private void OnCameraEasePowerChanged(double value)
        {
            _cameraEasePowerValue.Text = $"{value:F1}";
        }

        private void OnCameraEasePowerDragEnded(bool valueChanged)
        {
            if (Camera != null)
            {
                Camera.SetEasePower((float)_cameraEasePowerSlider.Value);
            }
            Owner.PushCurrentStateToHistory();
        }

        private void OnFreeLookToggled(bool enabled)
        {
            if (Camera != null)
            {
                Camera.SetFreeLookMode(enabled);
            }
            Owner.UpdateControlStates();
        }
        #endregion

        #region Event Handlers - Debug Toggles
        private void OnDebugInfoToggled(bool enabled)
        {
            GD.Print($"[DebugPanel] Debug info toggled: {enabled}");
            if (Player != null)
            {
                Player.SetShowDebugInfo(enabled);
                GD.Print($"[DebugPanel] Called Player.SetShowDebugInfo({enabled})");
            }
            else
            {
                GD.PushError("[DebugPanel] Player is null, cannot toggle debug info");
            }
        }

        private void OnCameraDebugToggled(bool enabled)
        {
            if (Camera != null)
            {
                Camera.SetDebugDrag(enabled);
            }
        }
        #endregion

        #region Calibration and Responsive Logic
        private void OnCalibrationToggled(bool enabled)
        {
            _calibrationEnabled = enabled;

            if (GridManager != null)
            {
                GridManager.SetAdaptiveCalibrationEnabled(enabled);
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
                if (Camera != null)
                {
                    Camera.SetCalibrationZoomLimits(false);
                    GD.Print("[DebugPanel] Calibration zoom limit disabled");
                }
                if (GridManager != null)
                    GridManager.SetLineWidthScale((float)_gridLineWidthSlider.Value);
            }
            Owner.UpdateControlStates();
        }

        private void OnCalibrationValueChanged(double value)
        {
            if (_calibrationEnabled && GridManager != null)
            {
                ApplyCalibrationValues();
            }
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
            GridManager.SetLineWidthCalibration((float)zoomA, (float)widthA, (float)zoomB, (float)widthB);

            SyncLineWidthToAdaptiveValue();
        }

        private void SyncLineWidthToAdaptiveValue()
        {
            // Calculate corresponding line width based on current camera zoom, update slider position (display only)
            // Currently no-op; can add logic later
        }

        private void UpdateCalibrationZoomLimits()
        {
            if (Camera != null && _refZoomASpin != null && _refZoomBSpin != null)
            {
                double zoomA = _refZoomASpin.Value;
                double zoomB = _refZoomBSpin.Value;
                double minZoomLimit = Mathf.Min((float)zoomA, (float)zoomB);
                double maxZoomLimit = Mathf.Max((float)zoomA, (float)zoomB);
                Camera.SetCalibrationZoomLimits(true, (float)minZoomLimit, (float)maxZoomLimit);
                GD.Print($"[DebugPanel] Calibration zoom limit enabled: zoom [{minZoomLimit:F1} - {maxZoomLimit:F1}]");
            }
        }

        private void OnResponsiveToggled(bool enabled)
        {
            if (GridManager != null)
            {
                GD.Print($"[DebugPanel] Setting responsive mode: {enabled}, visibleGridsX={_visibleGridsXSpin.Value}");
                var gm = GridManager as GridManager;
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

            Owner.UpdateControlStates();
        }

        private void OnVisibleGridsChanged(double value)
        {
            var gm = GridManager as GridManager;
            if (gm != null && gm.ResponsiveMode)
            {
                gm.VisibleGridsX = (float)_visibleGridsXSpin.Value;
                gm.UpdateResponsiveGridSize();
            }
        }

        private void OnEditorDragButtonChanged(long index)
        {
            MouseButton[] buttonMap = new MouseButton[] { MouseButton.Left, MouseButton.Right, MouseButton.Middle };
            MouseButton selectedButton = buttonMap[index];

            var buttons = new List<MouseButton> { selectedButton };
            if (Camera != null)
            {
                Camera.SetDragButtons(buttons);
            }

            string[] buttonNames = new string[] { "左键", "右键", "中键" };
            GD.Print($"[DebugPanel] Drag view button changed to: {buttonNames[index]}");
        }

        private void OnEditorSelectModChanged(bool enabled)
        {
            Node mapEditor = Owner.GetTree().GetFirstNodeInGroup("map_editor");
            if (mapEditor != null)
            {
                mapEditor.Set("require_ctrl_for_selection", enabled);
            }

            GD.Print($"[DebugPanel] Ctrl+Click select: {enabled}");
        }

        private void OnHoverTooltipWidthChanged(double value)
        {
            _hoverTooltipWidthValue.Text = ((int)value).ToString();

            var mapEditor = Owner.GetTree().GetFirstNodeInGroup("map_editor") as MapEditor;
            if (mapEditor != null)
            {
                mapEditor.HoverTooltipWidth = (float)value;
            }
        }

        private void OnHoverTooltipWidthDragEnded(bool valueChanged)
        {
            Owner.PushCurrentStateToHistory();
        }
        #endregion

        private void OnGridSizeModeChanged(long index)
        {
            ApplyGridSizeMode((GridManager.GridSizeMode)index);
        }

        private void ApplyGridSizeMode(GridManager.GridSizeMode mode)
        {
            if (GridManager != null)
            {
                GridManager.SetGridSizeMode(mode);
                if (mode == GridManager.GridSizeMode.ResponsiveVisibleCount)
                {
                    GridManager.VisibleGridsX = (float)_visibleGridsXSpin.Value;
                    GridManager.UpdateResponsiveGridSize();
                }
                else
                {
                    GridManager.SetGridSize((int)_gridSizeSlider.Value);
                }
            }
            if (_responsiveCheck != null)
                _responsiveCheck.ButtonPressed = mode == GridManager.GridSizeMode.ResponsiveVisibleCount;
            UpdateStrategySectionVisibility();
            Owner.UpdateControlStates();
        }

        private void OnLineWidthModeChanged(long index)
        {
            ApplyLineWidthMode((GridManager.GridLineWidthMode)index);
        }

        private void ApplyLineWidthMode(GridManager.GridLineWidthMode mode)
        {
            _calibrationEnabled = mode == GridManager.GridLineWidthMode.AdaptiveCalibration;
            if (GridManager != null)
            {
                GridManager.SetGridLineWidthMode(mode);
                switch (mode)
                {
                    case GridManager.GridLineWidthMode.FixedWorld:
                        GridManager.SetFixedWorldLineWidth((float)_gridLineWidthSlider.Value);
                        break;
                    case GridManager.GridLineWidthMode.FixedScreen:
                        GridManager.SetLineWidthScale((float)_gridLineWidthSlider.Value);
                        break;
                    case GridManager.GridLineWidthMode.AdaptiveCalibration:
                        ApplyCalibrationValues();
                        break;
                }
            }
            if (_applyCalibrationBtn != null)
                _applyCalibrationBtn.ButtonPressed = _calibrationEnabled;
            OnGridLineWidthChanged(_gridLineWidthSlider?.Value ?? 0.0);
            UpdateStrategySectionVisibility();
            Owner.UpdateControlStates();
        }

        private void OnMonsterPatrolOverlayToggled(bool enabled)
        {
            var overlay = GetOrCreateMonsterPatrolOverlay();
            overlay?.SetOverlayEnabled(enabled);
        }

        private void OnShowOutsideMapGrayToggled(bool enabled)
        {
            var gm = GridManager as GridManager;
            if (gm != null)
            {
                gm.ShowOutsideMapGray = enabled;
                gm.RefreshOutsideMapVisibility();
            }
#if DEBUG
            GD.Print($"[DebugPanel] ShowOutsideMapGray: {enabled}");
#endif
        }

        private MonsterPatrolOverlay GetOrCreateMonsterPatrolOverlay()
        {
            if (GridManager == null)
                return null;
            var overlay = GridManager.GetNodeOrNull<MonsterPatrolOverlay>("MonsterPatrolOverlay");
            if (overlay != null)
                return overlay;
            overlay = new MonsterPatrolOverlay { Name = "MonsterPatrolOverlay" };
            GridManager.AddChild(overlay);
            return overlay;
        }

        private void UpdateStrategySectionVisibility()
        {
            if (_gridManualParams != null)
                _gridManualParams.Visible = CurrentGridSizeMode == GridManager.GridSizeMode.Manual;
            if (_gridResponsiveParams != null)
                _gridResponsiveParams.Visible = CurrentGridSizeMode == GridManager.GridSizeMode.ResponsiveVisibleCount;
            if (_lineWidthFixedWorldParams != null)
                _lineWidthFixedWorldParams.Visible = CurrentLineWidthMode == GridManager.GridLineWidthMode.FixedWorld;
            if (_lineWidthFixedScreenParams != null)
                _lineWidthFixedScreenParams.Visible = CurrentLineWidthMode == GridManager.GridLineWidthMode.FixedScreen;
            if (_lineWidthAdaptiveParams != null)
                _lineWidthAdaptiveParams.Visible = CurrentLineWidthMode == GridManager.GridLineWidthMode.AdaptiveCalibration;
        }
    }
}
