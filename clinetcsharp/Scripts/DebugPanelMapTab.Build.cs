using Godot;

namespace ClinetCSharp
{
    public partial class DebugPanelMapTab
    {
        public override void BuildUI(VBoxContainer tabContainer)
        {
            _tabContainer = tabContainer;
            AddTabTitle(tabContainer, "地图设置", 13);
            AddSectionSeparator(tabContainer);

            BuildGridStrategySection(tabContainer);
            AddSectionSeparator(tabContainer);
            BuildLineWidthStrategySection(tabContainer);
            AddSectionSeparator(tabContainer);
            BuildCameraBehaviorSection(tabContainer);
            AddSectionSeparator(tabContainer);
            BuildDebugDisplaySection(tabContainer);
            AddSectionSeparator(tabContainer);
            BuildEditorInputSection(tabContainer);

            SetupEaseOptions();
            UpdateStrategySectionVisibility();

            Owner._gridSizeSlider = _gridSizeSlider;
        }

        private void BuildGridStrategySection(VBoxContainer parent)
        {
            _gridStrategySection = CreateSectionCard(parent, "格子方案");

            var modeRow = new HBoxContainer { Name = "GridSizeModeRow", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            modeRow.AddChild(new Label { Name = "_lbl", Text = "方案", CustomMinimumSize = new Vector2(90, 0) });
            _gridSizeModeOption = new OptionButton { Name = "GridSizeModeOption", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _gridSizeModeOption.AddItem("手动固定");
            _gridSizeModeOption.AddItem("响应式");
            modeRow.AddChild(_gridSizeModeOption);
            _gridStrategySection.AddChild(modeRow);

            _gridManualParams = CreateParamGroup(_gridStrategySection, "GridManualParams");
            (_gridSizeSlider, _gridSizeValue) = CreateSliderRow(_gridManualParams, "格子大小", 32, 256, 111, 1f);

            _gridResponsiveParams = CreateParamGroup(_gridStrategySection, "GridResponsiveParams");
            var responsiveRow = new HBoxContainer { Name = "ResponsiveToggleRow", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            responsiveRow.AddChild(new Label { Name = "_lbl", Text = "启用响应式", CustomMinimumSize = new Vector2(90, 0) });
            _responsiveCheck = new CheckButton { Name = "ResponsiveCheck" };
            responsiveRow.AddChild(_responsiveCheck);
            _gridResponsiveParams.AddChild(responsiveRow);

            var rowX = new HBoxContainer { Name = "VisibleGridsXRow", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            rowX.AddChild(new Label { Name = "_lbl", Text = "横向格子数", CustomMinimumSize = new Vector2(90, 0) });
            _visibleGridsXSpin = CreateSpinBox(1.0, 30.0, 0.5, 5.0, 70);
            _visibleGridsXSpin.UpdateOnTextChanged = true;
            rowX.AddChild(_visibleGridsXSpin);
            _gridResponsiveParams.AddChild(rowX);

            (_zoomSlider, _zoomValue) = CreateSliderRow(_gridStrategySection, "视角远近", 0.2f, 3.0f, 1.4f, DebugPanelLengthScalePolicy.StepF);
        }

        private void BuildLineWidthStrategySection(VBoxContainer parent)
        {
            _lineWidthStrategySection = CreateSectionCard(parent, "线宽方案");

            var modeRow = new HBoxContainer { Name = "LineWidthModeRow", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            modeRow.AddChild(new Label { Name = "_lbl", Text = "方案", CustomMinimumSize = new Vector2(90, 0) });
            _lineWidthModeOption = new OptionButton { Name = "LineWidthModeOption", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _lineWidthModeOption.AddItem("固定世界线宽");
            _lineWidthModeOption.AddItem("固定屏幕线宽");
            _lineWidthModeOption.AddItem("校准自适应");
            modeRow.AddChild(_lineWidthModeOption);
            _lineWidthStrategySection.AddChild(modeRow);

            _lineWidthFixedWorldParams = CreateParamGroup(_lineWidthStrategySection, "LineWidthFixedWorldParams");
            (_gridLineWidthSlider, _gridLineWidthValue) = CreateSliderRow(_lineWidthFixedWorldParams, "世界线宽", 0.1f, 5.0f, 2.0f, DebugPanelLengthScalePolicy.StepF);

            _lineWidthFixedScreenParams = CreateParamGroup(_lineWidthStrategySection, "LineWidthFixedScreenParams");
            CreateLineWidthScaleSlider(_lineWidthFixedScreenParams);

            _lineWidthAdaptiveParams = CreateParamGroup(_lineWidthStrategySection, "LineWidthAdaptiveParams");
            CreateLineWidthCalibrationUI(_lineWidthAdaptiveParams);

            (_gridLineBrightnessSlider, _gridLineBrightnessValue) = CreateSliderRow(_lineWidthStrategySection, "线条亮度", 0.1f, 1.0f, 0.7f, DebugPanelLengthScalePolicy.StepF);
            (_gridAntiAliasSoftnessSlider, _gridAntiAliasSoftnessValue) = CreateSliderRow(
                _lineWidthStrategySection,
                "抗锯齿柔化",
                GridOverlayAntiAliasSoftnessPolicy.Min,
                GridOverlayAntiAliasSoftnessPolicy.Max,
                GridOverlayAntiAliasSoftnessPolicy.Default,
                DebugPanelLengthScalePolicy.StepF);
            _gridAntiAliasSoftnessValue.Text = $"{GridOverlayAntiAliasSoftnessPolicy.Default:F1}x";
        }

        private void BuildCameraBehaviorSection(VBoxContainer parent)
        {
            var section = CreateSectionCard(parent, "镜头行为");
            CreateFreeLookToggle(section);
            (_cameraReturnDelaySlider, _cameraReturnDelayValue) = CreateSliderRow(section, "恢复延迟", 0.0f, 3.0f, 0.5f, DebugPanelLengthScalePolicy.StepF);
            (_cameraReturnSpeedSlider, _cameraReturnSpeedValue) = CreateSliderRow(section, "回退速度", 1.0f, 20.0f, 5.0f, 1f);

            var easeRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            easeRow.AddChild(new Label { Name = "_lbl", Text = "缓动曲线", CustomMinimumSize = new Vector2(90, 0) });
            _cameraEaseTypeOption = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            easeRow.AddChild(_cameraEaseTypeOption);
            section.AddChild(easeRow);

            (_cameraEasePowerSlider, _cameraEasePowerValue) = CreateSliderRow(section, "缓动强度", 1.0f, 5.0f, 2.0f, DebugPanelLengthScalePolicy.StepF);
        }

        private void BuildDebugDisplaySection(VBoxContainer parent)
        {
            var section = CreateSectionCard(parent, "调试显示");

            var coordsRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _gridCoordsCheck = new CheckButton { Text = "显示格子坐标" };
            coordsRow.AddChild(_gridCoordsCheck);
            section.AddChild(coordsRow);

            var debugRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _debugInfoCheck = new CheckButton { Text = "显示调试信息", ButtonPressed = true };
            debugRow.AddChild(_debugInfoCheck);
            section.AddChild(debugRow);

            var cameraDebugRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _cameraDebugCheck = new CheckButton { Text = "相机拖拽调试输出" };
            cameraDebugRow.AddChild(_cameraDebugCheck);
            section.AddChild(cameraDebugRow);

            var patrolOverlayRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _monsterPatrolOverlayCheck = new CheckButton { Text = "显示所有怪物巡逻区域" };
            patrolOverlayRow.AddChild(_monsterPatrolOverlayCheck);
            section.AddChild(patrolOverlayRow);

            var outsideMapGrayRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _showOutsideMapGrayCheck = new CheckButton { Text = "显示地图外灰色", ButtonPressed = false };
            outsideMapGrayRow.AddChild(_showOutsideMapGrayCheck);
            section.AddChild(outsideMapGrayRow);
        }

        private void BuildEditorInputSection(VBoxContainer parent)
        {
            var section = CreateSectionCard(parent, "编辑输入");
            CreateEditorKeyConfigUI(section);
        }

        private VBoxContainer CreateSectionCard(Container parent, string title)
        {
            var section = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                Name = $"{title.Replace(" ", string.Empty)}Section",
            };
            section.AddThemeConstantOverride("separation", 6);

            var frame = new PanelContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0.12f, 0.12f, 0.12f, 0.55f),
                BorderColor = new Color(0.34f, 0.34f, 0.34f, 0.95f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8,
                CornerRadiusBottomRight = 8,
                CornerRadiusBottomLeft = 8,
                ContentMarginLeft = 10,
                ContentMarginTop = 8,
                ContentMarginRight = 10,
                ContentMarginBottom = 10,
            });

            var body = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                Name = "Body",
            };
            body.AddThemeConstantOverride("separation", 6);

            var header = new Label
            {
                Name = "_lbl",
                Text = title,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            header.AddThemeFontSizeOverride("font_size", 12);
            body.AddChild(header);

            frame.AddChild(body);
            section.AddChild(frame);
            parent.AddChild(section);
            return body;
        }

        private VBoxContainer CreateParamGroup(Container parent, string name)
        {
            var group = new VBoxContainer
            {
                Name = name,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            group.AddThemeConstantOverride("separation", 6);
            parent.AddChild(group);
            return group;
        }

        private void CreateFreeLookToggle(Node mapTab)
        {
            var row = new HBoxContainer { Name = "FreeLookRow", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var label = new Label { Name = "_lbl", Text = "自由视角", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _freeLookCheck = new CheckButton { Name = "FreeLookCheck" };
            row.AddChild(label);
            row.AddChild(_freeLookCheck);
            mapTab.AddChild(row);
        }

        private void CreateLineWidthScaleSlider(Node mapTab)
        {
            _lineWidthScaleSlider = new HSlider
            {
                Name = "LineWidthScaleSlider",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MinValue = 1.0,
                MaxValue = 10.0,
                Step = DebugPanelLengthScalePolicy.Step,
                Value = 2.0,
                Scrollable = false
            };

            var row = new HBoxContainer { Name = "LineWidthScaleRow", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var label = new Label { Name = "_lbl", Text = "最大屏幕线宽", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _lineWidthScaleValue = new Label { Name = "LineWidthScaleValue", Text = "2.0", CustomMinimumSize = new Vector2(50, 0) };
            row.AddChild(label);
            row.AddChild(_lineWidthScaleValue);
            mapTab.AddChild(row);
            mapTab.AddChild(_lineWidthScaleSlider);
        }

        private void CreateLineWidthCalibrationUI(Node mapTab)
        {
            var rowToggle = new HBoxContainer { Name = "CalibrationToggleRow", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var label = new Label { Name = "_lbl", Text = "自适应校准", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _applyCalibrationBtn = new CheckButton { Name = "ApplyCalibrationBtn", Text = "启用" };
            rowToggle.AddChild(label);
            rowToggle.AddChild(_applyCalibrationBtn);
            mapTab.AddChild(rowToggle);

            CreateCalibrationRefUI(mapTab);
        }

        private void CreateCalibrationRefUI(Node parent)
        {
            var rowA = new HBoxContainer { Name = "RefPointARow", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var labelA = new Label { Name = "_lbl", Text = "参考点 A 缩放" };
            _refZoomASpin = CreateSpinBox(0.2, 3.0, 0.1, 0.4, 60);
            var labelWidthA = new Label { Name = "_lbl", Text = " 线宽" };
            _refWidthASpin = CreateSpinBox(0.1, 50.0, 0.1, 5.0, 60);
            rowA.AddChild(labelA);
            rowA.AddChild(_refZoomASpin);
            rowA.AddChild(labelWidthA);
            rowA.AddChild(_refWidthASpin);
            parent.AddChild(rowA);

            var rowB = new HBoxContainer { Name = "RefPointBRow", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var labelB = new Label { Name = "_lbl", Text = "参考点 B 缩放" };
            _refZoomBSpin = CreateSpinBox(0.2, 3.0, 0.1, 1.0, 60);
            var labelWidthB = new Label { Name = "_lbl", Text = " 线宽" };
            _refWidthBSpin = CreateSpinBox(0.1, 50.0, 0.1, 1.5, 60);
            rowB.AddChild(labelB);
            rowB.AddChild(_refZoomBSpin);
            rowB.AddChild(labelWidthB);
            rowB.AddChild(_refWidthBSpin);
            parent.AddChild(rowB);
        }

        private void CreateEditorKeyConfigUI(Container mapTab)
        {
            var rowDrag = new HBoxContainer { Name = "EditorDragButtonRow", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var labelDrag = new Label { Name = "_lbl", Text = "拖动视野按键", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _editorDragButtonOption = new OptionButton { Name = "EditorDragButtonOption" };
            _editorDragButtonOption.AddItem("左键");
            _editorDragButtonOption.AddItem("右键");
            _editorDragButtonOption.AddItem("中键");
            rowDrag.AddChild(labelDrag);
            rowDrag.AddChild(_editorDragButtonOption);
            mapTab.AddChild(rowDrag);

            var rowSelect = new HBoxContainer { Name = "EditorSelectModRow", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var labelSelect = new Label { Name = "_lbl", Text = "Ctrl+点击选中", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _editorSelectModCheck = new CheckButton { Name = "EditorSelectModCheck", ButtonPressed = true };
            rowSelect.AddChild(labelSelect);
            rowSelect.AddChild(_editorSelectModCheck);
            mapTab.AddChild(rowSelect);

            (_hoverTooltipWidthSlider, _hoverTooltipWidthValue) = CreateSliderRow(mapTab, "悬停提示框宽度", 120f, 400f, 200f, 1f);
            _hoverTooltipWidthValue.Text = "200";
        }

        private void SetupEaseOptions()
        {
            if (_cameraEaseTypeOption == null)
                return;

            _cameraEaseTypeOption.Clear();
            foreach (string easeName in DebugPanel.EASE_TYPE_NAMES)
                _cameraEaseTypeOption.AddItem(easeName);
        }
    }
}
