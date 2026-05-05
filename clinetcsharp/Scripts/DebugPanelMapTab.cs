using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// DebugPanel Map Tab — 地图/摄像机/校准/响应式/编辑器键位相关控件和逻辑
    /// </summary>
    public class DebugPanelMapTab : DebugPanelTab
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

        #region BuildUI
        public override void BuildUI(VBoxContainer tabContainer)
        {
            _tabContainer = tabContainer;
            var title = new Label { Name = "_lbl", Text = "地图设置", HorizontalAlignment = HorizontalAlignment.Center };
            title.Name = "_lbl";
            title.AddThemeFontSizeOverride("font_size", 13);
            tabContainer.AddChild(title);
            tabContainer.AddChild(new HSeparator());

            (_gridSizeSlider, _gridSizeValue) = CreateSliderRow(tabContainer, "格子大小", 32, 256, 111, 1f);
            (_zoomSlider, _zoomValue) = CreateSliderRow(tabContainer, "视角远近", 0.2f, 3.0f, 1.4f, DebugPanelLengthScalePolicy.StepF);
            (_gridLineWidthSlider, _gridLineWidthValue) = CreateSliderRow(tabContainer, "网格线宽", 0.1f, 5.0f, 2.0f, DebugPanelLengthScalePolicy.StepF);
            (_gridLineBrightnessSlider, _gridLineBrightnessValue) = CreateSliderRow(tabContainer, "网格线亮度", 0.1f, 1.0f, 0.7f, DebugPanelLengthScalePolicy.StepF);

            var coordsRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _gridCoordsCheck = new CheckButton { Text = "显示格子坐标" };
            coordsRow.AddChild(_gridCoordsCheck);
            tabContainer.AddChild(coordsRow);

            tabContainer.AddChild(new HSeparator());

            (_cameraReturnDelaySlider, _cameraReturnDelayValue) = CreateSliderRow(tabContainer, "恢复延迟", 0.0f, 3.0f, 0.5f, DebugPanelLengthScalePolicy.StepF);
            (_cameraReturnSpeedSlider, _cameraReturnSpeedValue) = CreateSliderRow(tabContainer, "回退速度", 1.0f, 20.0f, 5.0f, 1f);

            var easeRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            easeRow.AddChild(new Label { Name = "_lbl", Text = "缓动曲线:", CustomMinimumSize = new Vector2(80, 0) });
            _cameraEaseTypeOption = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            easeRow.AddChild(_cameraEaseTypeOption);
            tabContainer.AddChild(easeRow);

            (_cameraEasePowerSlider, _cameraEasePowerValue) = CreateSliderRow(tabContainer, "缓动强度", 1.0f, 5.0f, 2.0f, DebugPanelLengthScalePolicy.StepF);

            var debugRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _debugInfoCheck = new CheckButton { Text = "显示调试信息", ButtonPressed = true };
            debugRow.AddChild(_debugInfoCheck);
            tabContainer.AddChild(debugRow);

            var camDebugRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _cameraDebugCheck = new CheckButton { Text = "相机拖拽调试输出" };
            camDebugRow.AddChild(_cameraDebugCheck);
            tabContainer.AddChild(camDebugRow);

            // 附加高级动态控件
            CreateFreeLookToggle(tabContainer);
            CreateLineWidthScaleSlider(tabContainer);
            CreateLineWidthCalibrationUI(tabContainer);
            CreateResponsiveUI(tabContainer);
            CreateEditorKeyConfigUI(tabContainer);

            // Populate ease type options
            SetupEaseOptions();

            // Publish shared references to Owner so other tabs can access them
            Owner._gridSizeSlider = _gridSizeSlider;
        }

        private void CreateFreeLookToggle(Node mapTab)
        {
            HBoxContainer row = new HBoxContainer();
            row.Name = "FreeLookRow";
            row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label label = new Label { Name = "_lbl" };
            label.Text = "自由视角";
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            _freeLookCheck = new CheckButton();
            _freeLookCheck.Name = "FreeLookCheck";

            row.AddChild(label);
            row.AddChild(_freeLookCheck);
            mapTab.AddChild(row);
        }

        private void CreateLineWidthScaleSlider(Node mapTab)
        {
            _lineWidthScaleSlider = new HSlider { Scrollable = false };
            _lineWidthScaleSlider.Name = "LineWidthScaleSlider";
            _lineWidthScaleSlider.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _lineWidthScaleSlider.MinValue = 1.0;
            _lineWidthScaleSlider.MaxValue = 10.0;
            _lineWidthScaleSlider.Step = DebugPanelLengthScalePolicy.Step;
            _lineWidthScaleSlider.Value = 2.0;

            HBoxContainer row = new HBoxContainer();
            row.Name = "LineWidthScaleRow";
            row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label label = new Label { Name = "_lbl" };
            label.Text = "网格线最大宽度";
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            _lineWidthScaleValue = new Label();
            _lineWidthScaleValue.Name = "LineWidthScaleValue";
            _lineWidthScaleValue.Text = "2.0";
            _lineWidthScaleValue.CustomMinimumSize = new Vector2(50, 0);

            row.AddChild(label);
            row.AddChild(_lineWidthScaleValue);
            mapTab.AddChild(row);
            mapTab.AddChild(_lineWidthScaleSlider);
        }

        private void CreateLineWidthCalibrationUI(Node mapTab)
        {
            HBoxContainer rowToggle = new HBoxContainer();
            rowToggle.Name = "CalibrationToggleRow";
            rowToggle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label label = new Label { Name = "_lbl" };
            label.Text = "线宽自适应校准";
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            _applyCalibrationBtn = new CheckButton();
            _applyCalibrationBtn.Name = "ApplyCalibrationBtn";
            _applyCalibrationBtn.Text = "启用自适应校准";

            rowToggle.AddChild(label);
            rowToggle.AddChild(_applyCalibrationBtn);
            mapTab.AddChild(rowToggle);

            CreateCalibrationRefUI(mapTab);
        }

        private void CreateCalibrationRefUI(Node parent)
        {
            HBoxContainer rowA = new HBoxContainer();
            rowA.Name = "RefPointARow";
            rowA.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label labelA = new Label { Name = "_lbl" };
            labelA.Text = "参考点A: zoom=";

            _refZoomASpin = CreateSpinBox(0.2, 3.0, 0.1, 0.4, 60);

            Label labelWidthA = new Label { Name = "_lbl" };
            labelWidthA.Text = " 线宽=";

            _refWidthASpin = CreateSpinBox(0.1, 10.0, 0.1, 5.0, 60);

            rowA.AddChild(labelA);
            rowA.AddChild(_refZoomASpin);
            rowA.AddChild(labelWidthA);
            rowA.AddChild(_refWidthASpin);
            parent.AddChild(rowA);

            HBoxContainer rowB = new HBoxContainer();
            rowB.Name = "RefPointBRow";
            rowB.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label labelB = new Label { Name = "_lbl" };
            labelB.Text = "参考点B: zoom=";

            _refZoomBSpin = CreateSpinBox(0.2, 3.0, 0.1, 1.0, 60);

            Label labelWidthB = new Label { Name = "_lbl" };
            labelWidthB.Text = " 线宽=";

            _refWidthBSpin = CreateSpinBox(0.1, 10.0, 0.1, 1.5, 60);

            rowB.AddChild(labelB);
            rowB.AddChild(_refZoomBSpin);
            rowB.AddChild(labelWidthB);
            rowB.AddChild(_refWidthBSpin);
            parent.AddChild(rowB);
        }

        private void CreateResponsiveUI(Node mapTab)
        {
            HBoxContainer rowToggle = new HBoxContainer();
            rowToggle.Name = "ResponsiveToggleRow";
            rowToggle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label label = new Label { Name = "_lbl" };
            label.Text = "响应式布局";
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            _responsiveCheck = new CheckButton();
            _responsiveCheck.Name = "ResponsiveCheck";

            rowToggle.AddChild(label);
            rowToggle.AddChild(_responsiveCheck);
            mapTab.AddChild(rowToggle);

            HBoxContainer rowX = new HBoxContainer();
            rowX.Name = "VisibleGridsXRow";
            rowX.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label labelX = new Label { Name = "_lbl" };
            labelX.Text = "横向格子数";
            labelX.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            _visibleGridsXSpin = CreateSpinBox(1.0, 30.0, 0.5, 5.0, 70);
            _visibleGridsXSpin.UpdateOnTextChanged = true;

            rowX.AddChild(labelX);
            rowX.AddChild(_visibleGridsXSpin);
            mapTab.AddChild(rowX);
        }

        private void CreateEditorKeyConfigUI(Node mapTab)
        {
            HBoxContainer rowDrag = new HBoxContainer();
            rowDrag.Name = "EditorDragButtonRow";
            rowDrag.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label labelDrag = new Label { Name = "_lbl" };
            labelDrag.Text = "拖动视野按键";
            labelDrag.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            _editorDragButtonOption = new OptionButton();
            _editorDragButtonOption.Name = "EditorDragButtonOption";
            _editorDragButtonOption.AddItem("左键");
            _editorDragButtonOption.AddItem("右键");
            _editorDragButtonOption.AddItem("中键");

            rowDrag.AddChild(labelDrag);
            rowDrag.AddChild(_editorDragButtonOption);
            mapTab.AddChild(rowDrag);

            HBoxContainer rowSelect = new HBoxContainer();
            rowSelect.Name = "EditorSelectModRow";
            rowSelect.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label labelSelect = new Label { Name = "_lbl" };
            labelSelect.Text = "Ctrl+点击选中";
            labelSelect.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            _editorSelectModCheck = new CheckButton();
            _editorSelectModCheck.Name = "EditorSelectModCheck";
            _editorSelectModCheck.ButtonPressed = true;

            rowSelect.AddChild(labelSelect);
            rowSelect.AddChild(_editorSelectModCheck);
            mapTab.AddChild(rowSelect);
        }

        private void SetupEaseOptions()
        {
            if (_cameraEaseTypeOption == null) return;
            _cameraEaseTypeOption.Clear();
            foreach (string easeName in DebugPanel.EASE_TYPE_NAMES)
                _cameraEaseTypeOption.AddItem(easeName);
        }
        #endregion

        #region ConnectSignals / DisconnectSignals
        public override void ConnectSignals()
        {
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

            // Dynamic controls
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

            // Dynamic controls
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
        #endregion

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
        #endregion

        #region SyncToCurrentValues
        public override void SyncToCurrentValues()
        {
            if (GridManager != null)
            {
                _gridSizeSlider.SetBlockSignals(true);
                _gridSizeSlider.Value = (int)GridManager.GridSize;
                bool responsiveMode = GridManager.ResponsiveMode;
                _gridSizeValue.Text = responsiveMode ? $"{GridManager.GridSize} (自动)" : GridManager.GridSize.ToString();
                _gridSizeSlider.SetBlockSignals(false);
            }
        }
        #endregion

        /// <summary>
        /// Sync zoom slider from camera position (called by Owner._Process).
        /// </summary>
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

        #region UpdateControlStates (map-specific parts)
        /// <summary>
        /// Parameterless overload — called by Owner.UpdateControlStates().
        /// </summary>
        public void UpdateControlStates()
        {
            UpdateControlStates(false);
        }

        /// <summary>
        /// Update editable/enabled state of map-related controls based on current toggle states.
        /// Called by Owner.UpdateControlStates().
        /// </summary>
        public void UpdateControlStates(bool fontAutoSize)
        {
            bool freeLook = _freeLookCheck?.ButtonPressed ?? false;
            bool responsive = _responsiveCheck?.ButtonPressed ?? false;
            bool calibration = _calibrationEnabled;

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

            // Calibration reference points: dim when disabled
            float refModulate = calibration ? 1.0f : 0.6f;
            if (_refZoomASpin != null) _refZoomASpin.Modulate = new Color(refModulate, refModulate, refModulate, refModulate);
            if (_refWidthASpin != null) _refWidthASpin.Modulate = new Color(refModulate, refModulate, refModulate, refModulate);
            if (_refZoomBSpin != null) _refZoomBSpin.Modulate = new Color(refModulate, refModulate, refModulate, refModulate);
            if (_refWidthBSpin != null) _refWidthBSpin.Modulate = new Color(refModulate, refModulate, refModulate, refModulate);
        }
        #endregion

        #region SaveConfig
        public override void SaveConfig(ConfigFile cfg)
        {
            // Map settings
            cfg.SetValue("map", "grid_size", _gridSizeSlider.Value);
            cfg.SetValue("map", "zoom", _zoomSlider.Value);
            cfg.SetValue("map", "grid_line_width", _gridLineWidthSlider.Value);
            cfg.SetValue("map", "grid_line_brightness", _gridLineBrightnessSlider.Value);
            cfg.SetValue("map", "show_grid_coords", _gridCoordsCheck.ButtonPressed);

            // Camera settings
            cfg.SetValue("camera", "return_delay", _cameraReturnDelaySlider.Value);
            cfg.SetValue("camera", "return_speed", _cameraReturnSpeedSlider.Value);
            cfg.SetValue("camera", "ease_type", _cameraEaseTypeOption.Selected);
            cfg.SetValue("camera", "ease_power", _cameraEasePowerSlider.Value);
            cfg.SetValue("camera", "free_look", _freeLookCheck?.ButtonPressed ?? false);

            // Line width scale
            if (_lineWidthScaleSlider != null)
                cfg.SetValue("map", "line_width_scale", _lineWidthScaleSlider.Value);

            // Calibration settings
            cfg.SetValue("calibration", "enabled", _calibrationEnabled);
            if (_refZoomASpin != null)
            {
                cfg.SetValue("calibration", "ref_zoom_a", _refZoomASpin.Value);
                cfg.SetValue("calibration", "ref_width_a", _refWidthASpin.Value);
                cfg.SetValue("calibration", "ref_zoom_b", _refZoomBSpin.Value);
                cfg.SetValue("calibration", "ref_width_b", _refWidthBSpin.Value);
            }

            // Responsive layout settings
            if (_responsiveCheck != null)
            {
                cfg.SetValue("responsive", "enabled", _responsiveCheck.ButtonPressed);
                cfg.SetValue("responsive", "visible_grids_x", _visibleGridsXSpin.Value);
            }

            // Editor key settings
            if (_editorDragButtonOption != null)
            {
                cfg.SetValue("editor", "drag_button", _editorDragButtonOption.Selected);
                cfg.SetValue("editor", "require_ctrl_for_selection", _editorSelectModCheck?.ButtonPressed ?? true);
            }

            // Debug settings
            cfg.SetValue("debug", "show_debug_info", _debugInfoCheck.ButtonPressed);
        }
        #endregion

        #region LoadConfig
        public override void LoadConfig(ConfigFile cfg, bool configLoaded)
        {
            if (!configLoaded)
            {
                // Apply defaults from slider initial values
                ApplyMapSettingsToManagers();
                return;
            }

            // Load map settings
            double loadedGridSize = (double)cfg.GetValue("map", "grid_size", 111);
            if (loadedGridSize < 32 || loadedGridSize > 256)
            {
                GD.PushError($"[DebugPanel] Invalid grid_size in config: {loadedGridSize}");
                loadedGridSize = 111;
            }

            _gridSizeSlider.SetBlockSignals(true);
            _zoomSlider.SetBlockSignals(true);
            _gridLineWidthSlider.SetBlockSignals(true);
            _gridLineBrightnessSlider.SetBlockSignals(true);

            _gridSizeSlider.Value = loadedGridSize;
            _zoomSlider.Value = (double)cfg.GetValue("map", "zoom", 1.0);
            _gridLineWidthSlider.Value = (double)cfg.GetValue("map", "grid_line_width", 2.0);
            _gridLineBrightnessSlider.Value = (double)cfg.GetValue("map", "grid_line_brightness", 0.7);
            _gridCoordsCheck.ButtonPressed = (bool)cfg.GetValue("map", "show_grid_coords", false);

            _gridSizeSlider.SetBlockSignals(false);
            _zoomSlider.SetBlockSignals(false);
            _gridLineWidthSlider.SetBlockSignals(false);
            _gridLineBrightnessSlider.SetBlockSignals(false);

            ApplyMapSettingsToManagers();

            // Load camera settings
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

            // Load line width scale
            if (_lineWidthScaleSlider != null)
            {
                double savedLineWidth = (double)cfg.GetValue("map", "line_width_scale", 2.0);
                _lineWidthScaleSlider.SetBlockSignals(true);
                _lineWidthScaleSlider.Value = savedLineWidth;
                _lineWidthScaleSlider.SetBlockSignals(false);
            }

            // Load calibration settings
            _calibrationEnabled = (bool)cfg.GetValue("calibration", "enabled", false);
            if (_applyCalibrationBtn != null && GridManager != null)
            {
                _applyCalibrationBtn.ButtonPressed = _calibrationEnabled;
                GridManager.SetAdaptiveCalibrationEnabled(_calibrationEnabled);
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

            // Load responsive layout settings
            if (_responsiveCheck != null && GridManager != null)
            {
                _responsiveCheck.ButtonPressed = (bool)cfg.GetValue("responsive", "enabled", false);
                _visibleGridsXSpin.Value = (double)cfg.GetValue("responsive", "visible_grids_x", 5.0);
                if (_responsiveCheck.ButtonPressed)
                {
                    GridManager.VisibleGridsX = (float)_visibleGridsXSpin.Value;
                    GridManager.SetResponsiveMode(true);
                }
                OnResponsiveToggled(_responsiveCheck.ButtonPressed);
            }

            // Load editor key settings
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

            // Load debug settings
            _debugInfoCheck.ButtonPressed = (bool)cfg.GetValue("debug", "show_debug_info", false);
            OnDebugInfoToggled(_debugInfoCheck.ButtonPressed);
        }

        private void ApplyMapSettingsToManagers()
        {
            if (GridManager != null)
            {
                GridManager.SetGridSize((int)_gridSizeSlider.Value);
                GridManager.SetLineWidthScale((float)_gridLineWidthSlider.Value);
                float brightness = (float)_gridLineBrightnessSlider.Value;
                GridManager.SetLineBrightness(brightness);
                GridManager.ShowGridCoords = _gridCoordsCheck.ButtonPressed;
            }

            if (Camera != null)
            {
                Camera.Zoom = Vector2.One * (float)_zoomSlider.Value;
            }
        }
        #endregion

        #region DeferredInit helpers — called by Owner after BuildUI
        /// <summary>
        /// Called after Owner finishes DeferredInit to apply grid size from config
        /// (DeferredLoadConfig needs _gridManager ready)
        /// </summary>
        public void ApplyInitialGridSize()
        {
            if (GridManager != null && (_responsiveCheck == null || !_responsiveCheck.ButtonPressed))
            {
                int gridSize = (int)_gridSizeSlider.Value;
                GridManager.SetGridSize(gridSize);
            }
        }
        #endregion

        #region ExportConfigData
        public override Godot.Collections.Dictionary ExportConfigData()
        {
            var data = new Godot.Collections.Dictionary
            {
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

            data["debug"] = new Godot.Collections.Dictionary { ["show_debug_info"] = _debugInfoCheck?.ButtonPressed ?? false };

            return data;
        }
        #endregion
    }
}
