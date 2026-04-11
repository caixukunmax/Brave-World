using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// DebugPanel partial — 动态 UI 创建
    /// </summary>
    public partial class DebugPanel
    {
        #region Dynamic UI Creation
        private void CreateFreeLookToggle()
        {
            Node cameraGroup = GetNode("Control/Panel/ScrollContainer/TabContainer/地图/CameraGroup");

            HBoxContainer row = new HBoxContainer();
            row.Name = "FreeLookRow";
            row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label label = new Label();
            label.Text = "自由视角";
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            _freeLookCheck = new CheckButton();
            _freeLookCheck.Name = "FreeLookCheck";

            row.AddChild(label);
            row.AddChild(_freeLookCheck);
            cameraGroup.AddChild(row);

            _freeLookCheck.Toggled += OnFreeLookToggled;
        }

        private void CreateLineWidthScaleSlider()
        {
            Node gridGroup = GetNode("Control/Panel/ScrollContainer/TabContainer/地图/GridGroup");

            _lineWidthScaleSlider = GetNodeOrNull<HSlider>("Control/Panel/ScrollContainer/TabContainer/地图/GridGroup/LineWidthScaleSlider");
            if (_lineWidthScaleSlider != null)
                return;

            _lineWidthScaleSlider = new HSlider();
            _lineWidthScaleSlider.Name = "LineWidthScaleSlider";
            _lineWidthScaleSlider.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _lineWidthScaleSlider.MinValue = 1.0;
            _lineWidthScaleSlider.MaxValue = 10.0;
            _lineWidthScaleSlider.Step = 0.5;
            _lineWidthScaleSlider.Value = 2.0;

            HBoxContainer row = new HBoxContainer();
            row.Name = "LineWidthScaleRow";
            row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label label = new Label();
            label.Text = "网格线最大宽度";
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            _lineWidthScaleValue = new Label();
            _lineWidthScaleValue.Name = "LineWidthScaleValue";
            _lineWidthScaleValue.Text = "2.0";
            _lineWidthScaleValue.CustomMinimumSize = new Vector2(50, 0);

            row.AddChild(label);
            row.AddChild(_lineWidthScaleValue);
            gridGroup.AddChild(row);
            gridGroup.AddChild(_lineWidthScaleSlider);
        }

        private void CreateLineWidthCalibrationUI()
        {
            Node gridGroup = GetNode("Control/Panel/ScrollContainer/TabContainer/地图/GridGroup");

            HBoxContainer rowToggle = new HBoxContainer();
            rowToggle.Name = "CalibrationToggleRow";
            rowToggle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label label = new Label();
            label.Text = "线宽自适应校准";
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            _applyCalibrationBtn = new CheckButton();
            _applyCalibrationBtn.Name = "ApplyCalibrationBtn";
            _applyCalibrationBtn.Text = "启用自适应校准";
            _applyCalibrationBtn.Toggled += OnCalibrationToggled;

            rowToggle.AddChild(label);
            rowToggle.AddChild(_applyCalibrationBtn);
            gridGroup.AddChild(rowToggle);

            CreateCalibrationRefUI(gridGroup);
        }

        private void CreateCalibrationRefUI(Node parent)
        {
            HBoxContainer rowA = new HBoxContainer();
            rowA.Name = "RefPointARow";
            rowA.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label labelA = new Label();
            labelA.Text = "参考点A: zoom=";

            _refZoomASpin = CreateSpinBox(0.2, 3.0, 0.1, 0.4, 60);
            _refZoomASpin.ValueChanged += OnCalibrationValueChanged;

            Label labelWidthA = new Label();
            labelWidthA.Text = " 线宽=";

            _refWidthASpin = CreateSpinBox(0.1, 10.0, 0.1, 5.0, 60);
            _refWidthASpin.ValueChanged += OnCalibrationValueChanged;

            rowA.AddChild(labelA);
            rowA.AddChild(_refZoomASpin);
            rowA.AddChild(labelWidthA);
            rowA.AddChild(_refWidthASpin);
            parent.AddChild(rowA);

            HBoxContainer rowB = new HBoxContainer();
            rowB.Name = "RefPointBRow";
            rowB.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label labelB = new Label();
            labelB.Text = "参考点B: zoom=";

            _refZoomBSpin = CreateSpinBox(0.2, 3.0, 0.1, 1.0, 60);
            _refZoomBSpin.ValueChanged += OnCalibrationValueChanged;

            Label labelWidthB = new Label();
            labelWidthB.Text = " 线宽=";

            _refWidthBSpin = CreateSpinBox(0.1, 10.0, 0.1, 1.5, 60);
            _refWidthBSpin.ValueChanged += OnCalibrationValueChanged;

            rowB.AddChild(labelB);
            rowB.AddChild(_refZoomBSpin);
            rowB.AddChild(labelWidthB);
            rowB.AddChild(_refWidthBSpin);
            parent.AddChild(rowB);
        }

        private void CreateResponsiveUI()
        {
            Node cameraGroup = GetNode("Control/Panel/ScrollContainer/TabContainer/地图/CameraGroup");

            HBoxContainer rowToggle = new HBoxContainer();
            rowToggle.Name = "ResponsiveToggleRow";
            rowToggle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label label = new Label();
            label.Text = "响应式布局";
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            _responsiveCheck = new CheckButton();
            _responsiveCheck.Name = "ResponsiveCheck";

            rowToggle.AddChild(label);
            rowToggle.AddChild(_responsiveCheck);
            cameraGroup.AddChild(rowToggle);

            HBoxContainer rowX = new HBoxContainer();
            rowX.Name = "VisibleGridsXRow";
            rowX.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label labelX = new Label();
            labelX.Text = "横向格子数";
            labelX.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            _visibleGridsXSpin = CreateSpinBox(1.0, 30.0, 0.5, 5.0, 70);
            _visibleGridsXSpin.ValueChanged += OnVisibleGridsChanged;

            rowX.AddChild(labelX);
            rowX.AddChild(_visibleGridsXSpin);
            cameraGroup.AddChild(rowX);

            _responsiveCheck.Toggled += OnResponsiveToggled;
        }

        private void CreateFontAutoSizeToggle()
        {
            _fontAutoSizeCheck = GetNodeOrNull<CheckButton>("Control/Panel/ScrollContainer/TabContainer/玩家/TextGroup/FontAutoSizeRow/FontAutoSizeCheck");
            if (_fontAutoSizeCheck != null)
                return;

            Node textGroup = GetNode("Control/Panel/ScrollContainer/TabContainer/玩家/TextGroup");

            HBoxContainer row = new HBoxContainer();
            row.Name = "FontAutoSizeRow";
            row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label label = new Label();
            label.Text = "自动字号";
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            _fontAutoSizeCheck = new CheckButton();
            _fontAutoSizeCheck.Name = "FontAutoSizeCheck";

            row.AddChild(label);
            row.AddChild(_fontAutoSizeCheck);
            textGroup.AddChild(row);

            _fontAutoSizeCheck.Toggled += OnFontAutoSizeToggled;
        }

        private void CreateEditorKeyConfigUI()
        {
            Node debugGroup = GetNode("Control/Panel/ScrollContainer/TabContainer/地图/DebugGroup");

            HBoxContainer rowDrag = new HBoxContainer();
            rowDrag.Name = "EditorDragButtonRow";
            rowDrag.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label labelDrag = new Label();
            labelDrag.Text = "拖动视野按键";
            labelDrag.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            _editorDragButtonOption = new OptionButton();
            _editorDragButtonOption.Name = "EditorDragButtonOption";
            _editorDragButtonOption.AddItem("左键");
            _editorDragButtonOption.AddItem("右键");
            _editorDragButtonOption.AddItem("中键");

            rowDrag.AddChild(labelDrag);
            rowDrag.AddChild(_editorDragButtonOption);
            debugGroup.AddChild(rowDrag);

            _editorDragButtonOption.ItemSelected += OnEditorDragButtonChanged;

            HBoxContainer rowSelect = new HBoxContainer();
            rowSelect.Name = "EditorSelectModRow";
            rowSelect.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label labelSelect = new Label();
            labelSelect.Text = "Ctrl+点击选中";
            labelSelect.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            _editorSelectModCheck = new CheckButton();
            _editorSelectModCheck.Name = "EditorSelectModCheck";
            _editorSelectModCheck.ButtonPressed = true;

            rowSelect.AddChild(labelSelect);
            rowSelect.AddChild(_editorSelectModCheck);
            debugGroup.AddChild(rowSelect);

            _editorSelectModCheck.Toggled += OnEditorSelectModChanged;
        }

        private void CreateLabelControlsUI()
        {
            var labelCtrlGroup = GetNode<VBoxContainer>("Control/Panel/ScrollContainer/TabContainer/玩家/LabelCtrlGroup");

            for (int i = 0; i < LabelCount; i++)
            {
                int idx = i;

                // Separator
                var headerRow = new HBoxContainer();
                headerRow.Name = $"LabelHeader_{i}";
                headerRow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                var headerSep = new HSeparator();
                headerSep.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                headerRow.AddChild(headerSep);
                labelCtrlGroup.AddChild(headerRow);

                // Title row: name edit + visible + color + reset
                var titleRow = new HBoxContainer();
                titleRow.Name = $"LabelTitle_{i}";
                titleRow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

                var nameEdit = new LineEdit { CustomMinimumSize = new Vector2(50, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                nameEdit.Name = $"LabelName_{i}";
                nameEdit.PlaceholderText = "名称";
                _labelNameEdits[i] = nameEdit;
                titleRow.AddChild(nameEdit);

                var visibleCheck = new CheckButton();
                visibleCheck.Name = $"LabelVisible_{i}";
                visibleCheck.ButtonPressed = true;
                _labelVisibleChecks[i] = visibleCheck;
                titleRow.AddChild(visibleCheck);

                var colorBtn = new Button();
                colorBtn.Name = $"LabelColor_{i}";
                colorBtn.Text = "色";
                colorBtn.CustomMinimumSize = new Vector2(30, 24);
                colorBtn.Modulate = Colors.Black;
                _labelColorButtons[i] = colorBtn;
                titleRow.AddChild(colorBtn);

                var resetBtn = new Button();
                resetBtn.Name = $"LabelReset_{i}";
                resetBtn.Text = "重置";
                resetBtn.CustomMinimumSize = new Vector2(40, 24);
                _labelResetButtons[i] = resetBtn;
                titleRow.AddChild(resetBtn);

                labelCtrlGroup.AddChild(titleRow);

                // Content edit row
                var contentRow = new HBoxContainer();
                contentRow.Name = $"LabelContentRow_{i}";
                contentRow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                var contentLabel = new Label { Text = "内容", CustomMinimumSize = new Vector2(35, 0) };
                contentRow.AddChild(contentLabel);
                var textEdit = new LineEdit { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(100, 0) };
                textEdit.Name = $"LabelText_{i}";
                _labelTextEdits[i] = textEdit;
                contentRow.AddChild(textEdit);
                labelCtrlGroup.AddChild(contentRow);

                // Font size
                var fontSizeRow = new HBoxContainer();
                fontSizeRow.Name = $"LabelFontSizeRow_{i}";
                fontSizeRow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                var fsLabel = new Label { Text = "字号", CustomMinimumSize = new Vector2(35, 0) };
                fontSizeRow.AddChild(fsLabel);
                var fsValue = new Label { Text = "0", CustomMinimumSize = new Vector2(25, 0), HorizontalAlignment = HorizontalAlignment.Right };
                _labelFontSizeValues[i] = fsValue;
                fontSizeRow.AddChild(fsValue);
                labelCtrlGroup.AddChild(fontSizeRow);

                var fontSizeSlider = new HSlider();
                fontSizeSlider.Name = $"LabelFontSizeSlider_{i}";
                fontSizeSlider.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                fontSizeSlider.MinValue = 0; fontSizeSlider.MaxValue = 48; fontSizeSlider.Step = 1; fontSizeSlider.Value = 0;
                _labelFontSizeSliders[i] = fontSizeSlider;
                labelCtrlGroup.AddChild(fontSizeSlider);

                // Offset X
                var offsetXRow = new HBoxContainer();
                offsetXRow.Name = $"LabelOffsetXRow_{i}";
                offsetXRow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                var oxLabel = new Label { Text = "X偏移", CustomMinimumSize = new Vector2(45, 0) };
                offsetXRow.AddChild(oxLabel);
                var oxValue = new Label { Text = "0", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
                _labelOffsetXValues[i] = oxValue;
                offsetXRow.AddChild(oxValue);
                labelCtrlGroup.AddChild(offsetXRow);

                var offsetXSlider = new HSlider();
                offsetXSlider.Name = $"LabelOffsetXSlider_{i}";
                offsetXSlider.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                offsetXSlider.MinValue = -150; offsetXSlider.MaxValue = 150; offsetXSlider.Step = 1;
                offsetXSlider.Value = Player.DefaultOffsets[i].X;
                _labelOffsetXSliders[i] = offsetXSlider;
                labelCtrlGroup.AddChild(offsetXSlider);

                // Offset Y
                var offsetYRow = new HBoxContainer();
                offsetYRow.Name = $"LabelOffsetYRow_{i}";
                offsetYRow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                var oyLabel = new Label { Text = "Y偏移", CustomMinimumSize = new Vector2(45, 0) };
                offsetYRow.AddChild(oyLabel);
                var oyValue = new Label { Text = "0", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
                _labelOffsetYValues[i] = oyValue;
                offsetYRow.AddChild(oyValue);
                labelCtrlGroup.AddChild(offsetYRow);

                var offsetYSlider = new HSlider();
                offsetYSlider.Name = $"LabelOffsetYSlider_{i}";
                offsetYSlider.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                offsetYSlider.MinValue = -150; offsetYSlider.MaxValue = 150; offsetYSlider.Step = 1;
                offsetYSlider.Value = Player.DefaultOffsets[i].Y;
                _labelOffsetYSliders[i] = offsetYSlider;
                labelCtrlGroup.AddChild(offsetYSlider);

                // Signals
                visibleCheck.Toggled += (enabled) => OnLabelVisibleToggled(idx, enabled);
                nameEdit.TextChanged += (newName) => OnLabelNameChanged(idx, newName);
                textEdit.TextChanged += (newText) => OnLabelTextChanged(idx, newText);
                colorBtn.Pressed += () => OnLabelColorPressed(idx);
                resetBtn.Pressed += () => OnLabelResetPressed(idx);
                fontSizeSlider.ValueChanged += (val) => OnLabelFontSizeChanged(idx, val);
                fontSizeSlider.DragEnded += (changed) => OnLabelFontSizeDragEnded(idx, changed);
                offsetXSlider.ValueChanged += (val) => OnLabelOffsetXChanged(idx, val);
                offsetXSlider.DragEnded += (changed) => OnLabelOffsetDragEnded(idx, changed);
                offsetYSlider.ValueChanged += (val) => OnLabelOffsetYChanged(idx, val);
                offsetYSlider.DragEnded += (changed) => OnLabelOffsetDragEnded(idx, changed);
            }

            // ========== 血条控制 ==========
            // Separator
            var hpSep = new HSeparator { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            labelCtrlGroup.AddChild(hpSep);

            // Title row: "血条" + visible + color
            var hpTitleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var hpNameLabel = new Label { Text = "血条", CustomMinimumSize = new Vector2(45, 0) };
            hpTitleRow.AddChild(hpNameLabel);

            _healthBarVisibleCheck = new CheckButton { ButtonPressed = true };
            hpTitleRow.AddChild(_healthBarVisibleCheck);

            var hpSpacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            hpTitleRow.AddChild(hpSpacer);

            _healthBarColorBtn = new Button { Text = "色", CustomMinimumSize = new Vector2(30, 24) };
            _healthBarColorBtn.Modulate = new Color(0, 0.8f, 0, 1);
            hpTitleRow.AddChild(_healthBarColorBtn);
            labelCtrlGroup.AddChild(hpTitleRow);

            // Length slider
            var lenRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            lenRow.AddChild(new Label { Text = "长度", CustomMinimumSize = new Vector2(35, 0) });
            _healthBarLengthValue = new Label { Text = "80", CustomMinimumSize = new Vector2(25, 0), HorizontalAlignment = HorizontalAlignment.Right };
            lenRow.AddChild(_healthBarLengthValue);
            labelCtrlGroup.AddChild(lenRow);
            _healthBarLengthSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 20, MaxValue = 200, Step = 1, Value = 80 };
            labelCtrlGroup.AddChild(_healthBarLengthSlider);

            // Height slider
            var hRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            hRow.AddChild(new Label { Text = "高度", CustomMinimumSize = new Vector2(35, 0) });
            _healthBarHeightValue = new Label { Text = "6", CustomMinimumSize = new Vector2(25, 0), HorizontalAlignment = HorizontalAlignment.Right };
            hRow.AddChild(_healthBarHeightValue);
            labelCtrlGroup.AddChild(hRow);
            _healthBarHeightSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 2, MaxValue = 20, Step = 1, Value = 6 };
            labelCtrlGroup.AddChild(_healthBarHeightSlider);

            // Fill slider
            var fillRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            fillRow.AddChild(new Label { Text = "填充", CustomMinimumSize = new Vector2(35, 0) });
            _healthBarFillValue = new Label { Text = "100%", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            fillRow.AddChild(_healthBarFillValue);
            labelCtrlGroup.AddChild(fillRow);

            _healthBarFillSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 0, MaxValue = 100, Step = 1, Value = 100 };
            labelCtrlGroup.AddChild(_healthBarFillSlider);

            // Offset X
            var hpxRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            hpxRow.AddChild(new Label { Text = "X偏移", CustomMinimumSize = new Vector2(45, 0) });
            _healthBarOffsetXValue = new Label { Text = "0", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            hpxRow.AddChild(_healthBarOffsetXValue);
            labelCtrlGroup.AddChild(hpxRow);

            _healthBarOffsetXSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = -150, MaxValue = 150, Step = 1, Value = 0 };
            labelCtrlGroup.AddChild(_healthBarOffsetXSlider);

            // Offset Y
            var hpyRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            hpyRow.AddChild(new Label { Text = "Y偏移", CustomMinimumSize = new Vector2(45, 0) });
            _healthBarOffsetYValue = new Label { Text = "-70", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            hpyRow.AddChild(_healthBarOffsetYValue);
            labelCtrlGroup.AddChild(hpyRow);

            _healthBarOffsetYSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = -150, MaxValue = 150, Step = 1, Value = -70 };
            labelCtrlGroup.AddChild(_healthBarOffsetYSlider);

            // Connect signals
            _healthBarVisibleCheck.Toggled += OnHealthBarVisibleToggled;
            _healthBarColorBtn.Pressed += OnHealthBarColorPressed;
            _healthBarLengthSlider.ValueChanged += OnHealthBarLengthChanged;
            _healthBarLengthSlider.DragEnded += (changed) => PushCurrentStateToHistory();
            _healthBarHeightSlider.ValueChanged += OnHealthBarHeightChanged;
            _healthBarHeightSlider.DragEnded += (changed) => PushCurrentStateToHistory();
            _healthBarFillSlider.ValueChanged += OnHealthBarFillChanged;
            _healthBarFillSlider.DragEnded += (changed) => PushCurrentStateToHistory();
            _healthBarOffsetXSlider.ValueChanged += OnHealthBarOffsetXChanged;
            _healthBarOffsetXSlider.DragEnded += (changed) => PushCurrentStateToHistory();
            _healthBarOffsetYSlider.ValueChanged += OnHealthBarOffsetYChanged;
            _healthBarOffsetYSlider.DragEnded += (changed) => PushCurrentStateToHistory();

            // ========== 施法条控制 ==========
            labelCtrlGroup.AddChild(new HSeparator { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

            // Title row
            var ctTitleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            ctTitleRow.AddChild(new Label { Text = "施法", CustomMinimumSize = new Vector2(45, 0) });
            _castBarVisibleCheck = new CheckButton { ButtonPressed = true };
            ctTitleRow.AddChild(_castBarVisibleCheck);
            ctTitleRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            _castBarColorBtn = new Button { Text = "色", CustomMinimumSize = new Vector2(30, 24) };
            _castBarColorBtn.Modulate = new Color(0.3f, 0.5f, 1, 1);
            ctTitleRow.AddChild(_castBarColorBtn);
            labelCtrlGroup.AddChild(ctTitleRow);

            // Length
            var ctLenRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            ctLenRow.AddChild(new Label { Text = "长度", CustomMinimumSize = new Vector2(35, 0) });
            _castBarLengthValue = new Label { Text = "60", CustomMinimumSize = new Vector2(25, 0), HorizontalAlignment = HorizontalAlignment.Right };
            ctLenRow.AddChild(_castBarLengthValue);
            labelCtrlGroup.AddChild(ctLenRow);
            _castBarLengthSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 20, MaxValue = 200, Step = 1, Value = 60 };
            labelCtrlGroup.AddChild(_castBarLengthSlider);

            // Height
            var ctHtRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            ctHtRow.AddChild(new Label { Text = "高度", CustomMinimumSize = new Vector2(35, 0) });
            _castBarHeightValue = new Label { Text = "4", CustomMinimumSize = new Vector2(25, 0), HorizontalAlignment = HorizontalAlignment.Right };
            ctHtRow.AddChild(_castBarHeightValue);
            labelCtrlGroup.AddChild(ctHtRow);
            _castBarHeightSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 2, MaxValue = 20, Step = 1, Value = 4 };
            labelCtrlGroup.AddChild(_castBarHeightSlider);

            // Fill
            var ctFillRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            ctFillRow.AddChild(new Label { Text = "填充", CustomMinimumSize = new Vector2(35, 0) });
            _castBarFillValue = new Label { Text = "60%", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            ctFillRow.AddChild(_castBarFillValue);
            labelCtrlGroup.AddChild(ctFillRow);
            _castBarFillSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 0, MaxValue = 100, Step = 1, Value = 60 };
            labelCtrlGroup.AddChild(_castBarFillSlider);

            // Offset X
            var ctOxRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            ctOxRow.AddChild(new Label { Text = "X偏移", CustomMinimumSize = new Vector2(45, 0) });
            _castBarOffsetXValue = new Label { Text = "0", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            ctOxRow.AddChild(_castBarOffsetXValue);
            labelCtrlGroup.AddChild(ctOxRow);
            _castBarOffsetXSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = -150, MaxValue = 150, Step = 1, Value = 0 };
            labelCtrlGroup.AddChild(_castBarOffsetXSlider);

            // Offset Y
            var ctOyRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            ctOyRow.AddChild(new Label { Text = "Y偏移", CustomMinimumSize = new Vector2(45, 0) });
            _castBarOffsetYValue = new Label { Text = "-80", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            ctOyRow.AddChild(_castBarOffsetYValue);
            labelCtrlGroup.AddChild(ctOyRow);
            _castBarOffsetYSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = -150, MaxValue = 150, Step = 1, Value = -80 };
            labelCtrlGroup.AddChild(_castBarOffsetYSlider);

            // Signals
            _castBarVisibleCheck.Toggled += OnCastBarVisibleToggled;
            _castBarColorBtn.Pressed += OnCastBarColorPressed;
            _castBarLengthSlider.ValueChanged += OnCastBarLengthChanged;
            _castBarLengthSlider.DragEnded += (changed) => PushCurrentStateToHistory();
            _castBarHeightSlider.ValueChanged += OnCastBarHeightChanged;
            _castBarHeightSlider.DragEnded += (changed) => PushCurrentStateToHistory();
            _castBarFillSlider.ValueChanged += OnCastBarFillChanged;
            _castBarFillSlider.DragEnded += (changed) => PushCurrentStateToHistory();
            _castBarOffsetXSlider.ValueChanged += OnCastBarOffsetXChanged;
            _castBarOffsetXSlider.DragEnded += (changed) => PushCurrentStateToHistory();
            _castBarOffsetYSlider.ValueChanged += OnCastBarOffsetYChanged;
            _castBarOffsetYSlider.DragEnded += (changed) => PushCurrentStateToHistory();

            // ========== 等级徽章控制 ==========
            labelCtrlGroup.AddChild(new HSeparator { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

            // Title row
            var lvTitleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            lvTitleRow.AddChild(new Label { Text = "等级", CustomMinimumSize = new Vector2(45, 0) });
            _levelBadgeVisibleCheck = new CheckButton { ButtonPressed = true };
            lvTitleRow.AddChild(_levelBadgeVisibleCheck);
            lvTitleRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            _levelBadgeTextColorBtn = new Button { Text = "色", CustomMinimumSize = new Vector2(30, 24) };
            _levelBadgeTextColorBtn.Modulate = Colors.Yellow;
            lvTitleRow.AddChild(_levelBadgeTextColorBtn);
            labelCtrlGroup.AddChild(lvTitleRow);

            // Text content
            var lvTxtRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            lvTxtRow.AddChild(new Label { Text = "内容", CustomMinimumSize = new Vector2(35, 0) });
            _levelBadgeTextEdit = new LineEdit { Text = "Lv.{level}", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(100, 0), PlaceholderText = "可用: {level} {name} {job}" };
            lvTxtRow.AddChild(_levelBadgeTextEdit);
            labelCtrlGroup.AddChild(lvTxtRow);

            // Font size
            var lvSzRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            lvSzRow.AddChild(new Label { Text = "字号", CustomMinimumSize = new Vector2(35, 0) });
            _levelBadgeFontSizeValue = new Label { Text = "12", CustomMinimumSize = new Vector2(25, 0), HorizontalAlignment = HorizontalAlignment.Right };
            lvSzRow.AddChild(_levelBadgeFontSizeValue);
            labelCtrlGroup.AddChild(lvSzRow);
            _levelBadgeFontSizeSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 6, MaxValue = 24, Step = 1, Value = 12 };
            labelCtrlGroup.AddChild(_levelBadgeFontSizeSlider);

            // Offset X
            var lvOxRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            lvOxRow.AddChild(new Label { Text = "X偏移", CustomMinimumSize = new Vector2(45, 0) });
            _levelBadgeOffsetXValue = new Label { Text = "-35", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            lvOxRow.AddChild(_levelBadgeOffsetXValue);
            labelCtrlGroup.AddChild(lvOxRow);
            _levelBadgeOffsetXSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = -150, MaxValue = 150, Step = 1, Value = -35 };
            labelCtrlGroup.AddChild(_levelBadgeOffsetXSlider);

            // Offset Y
            var lvOyRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            lvOyRow.AddChild(new Label { Text = "Y偏移", CustomMinimumSize = new Vector2(45, 0) });
            _levelBadgeOffsetYValue = new Label { Text = "-35", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            lvOyRow.AddChild(_levelBadgeOffsetYValue);
            labelCtrlGroup.AddChild(lvOyRow);
            _levelBadgeOffsetYSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = -150, MaxValue = 150, Step = 1, Value = -35 };
            labelCtrlGroup.AddChild(_levelBadgeOffsetYSlider);

            // Signals
            _levelBadgeVisibleCheck.Toggled += OnLevelBadgeVisibleToggled;
            _levelBadgeTextColorBtn.Pressed += OnLevelBadgeTextColorPressed;
            _levelBadgeTextEdit.TextChanged += OnLevelBadgeTextChanged;
            _levelBadgeFontSizeSlider.ValueChanged += OnLevelBadgeFontSizeChanged;
            _levelBadgeFontSizeSlider.DragEnded += (changed) => PushCurrentStateToHistory();
            _levelBadgeOffsetXSlider.ValueChanged += OnLevelBadgeOffsetXChanged;
            _levelBadgeOffsetXSlider.DragEnded += (changed) => PushCurrentStateToHistory();
            _levelBadgeOffsetYSlider.ValueChanged += OnLevelBadgeOffsetYChanged;
            _levelBadgeOffsetYSlider.DragEnded += (changed) => PushCurrentStateToHistory();
        }
        #endregion

        #region Preset UI Creation
        private void CreatePresetUI()
        {
            VBoxContainer mainContainer = new VBoxContainer();
            mainContainer.Name = "PresetContainer";
            mainContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            mainContainer.AddThemeConstantOverride("separation", 8);
            mainContainer.Alignment = BoxContainer.AlignmentMode.Center;

            HBoxContainer row1 = new HBoxContainer();
            row1.Name = "ConfigRow";
            row1.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row1.AddThemeConstantOverride("separation", 20);
            row1.Alignment = BoxContainer.AlignmentMode.Center;

            Button discardButton = new Button();
            discardButton.Name = "DiscardButton";
            discardButton.Text = "↩ 放弃";
            discardButton.Pressed += OnDiscardChangesPressed;
            row1.AddChild(discardButton);

            Button saveConfigButton = new Button();
            saveConfigButton.Name = "SaveConfigButton";
            saveConfigButton.Text = "✓ 保存";
            saveConfigButton.Pressed += OnMapSavePressed;
            row1.AddChild(saveConfigButton);

            HBoxContainer row2 = new HBoxContainer();
            row2.Name = "PresetRow";
            row2.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row2.AddThemeConstantOverride("separation", 8);
            row2.Alignment = BoxContainer.AlignmentMode.Center;

            row2.AddChild(CreateLabel("预设:"));

            _presetOption = new OptionButton();
            _presetOption.Name = "PresetOption";
            _presetOption.CustomMinimumSize = new Vector2(100, 0);
            _presetOption.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _presetOption.ItemSelected += OnPresetSelected;
            row2.AddChild(_presetOption);

            _deletePresetBtn = new Button();
            _deletePresetBtn.Name = "DeletePresetBtn";
            _deletePresetBtn.Text = "🗑";
            _deletePresetBtn.TooltipText = "删除选中预设";
            _deletePresetBtn.Pressed += OnDeletePresetPressed;
            row2.AddChild(_deletePresetBtn);

            Control spacer = new Control();
            spacer.CustomMinimumSize = new Vector2(15, 0);
            row2.AddChild(spacer);

            _savePresetBtn = new Button();
            _savePresetBtn.Name = "SavePresetBtn";
            _savePresetBtn.Text = "+ 新建";
            _savePresetBtn.TooltipText = "将当前配置保存为新预设";
            _savePresetBtn.Pressed += OnSavePresetPressed;
            row2.AddChild(_savePresetBtn);

            _presetNameEdit = new LineEdit();
            _presetNameEdit.Name = "PresetNameEdit";
            _presetNameEdit.Visible = false;
            row2.AddChild(_presetNameEdit);

            mainContainer.AddChild(row1);
            mainContainer.AddChild(row2);
            _panel.AddChild(mainContainer);

            mainContainer.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
            mainContainer.OffsetLeft = 10;
            mainContainer.OffsetTop = -80;
            mainContainer.OffsetRight = -10;
            mainContainer.OffsetBottom = -5;

            RefreshPresetList();
        }

        private void RefreshPresetList()
        {
            if (_presetOption == null) return;

            int currentIndex = _presetOption.Selected;
            string currentMetadata = "";
            if (currentIndex >= 0 && currentIndex < _presetOption.ItemCount)
                currentMetadata = _presetOption.GetItemMetadata(currentIndex).AsString();

            _presetOption.Clear();
            _presetOption.AddItem("默认", 0);
            _presetOption.SetItemMetadata(0, "");

            List<string> presets = GetPresetList();
            int newSelectedIndex = 0;

            for (int i = 0; i < presets.Count; i++)
            {
                _presetOption.AddItem(presets[i], i + 1);
                _presetOption.SetItemMetadata(i + 1, presets[i]);
                if (presets[i] == currentMetadata)
                    newSelectedIndex = i + 1;
            }

            _presetOption.Select(newSelectedIndex);
        }
        #endregion

        #region UI Helper Methods
        private Label CreateSectionTitle(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AddThemeColorOverride("font_color", new Color(0.6f, 0.8f, 1.0f));
            label.AddThemeFontSizeOverride("font_size", 14);
            return label;
        }

        private Label CreateLabel(string text, bool expand = false)
        {
            Label label = new Label();
            label.Text = text;
            if (expand)
                label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            return label;
        }

        private SpinBox CreateSpinBox(double minV, double maxV, double step, double value, int width)
        {
            SpinBox spin = new SpinBox();
            spin.MinValue = minV;
            spin.MaxValue = maxV;
            spin.Step = step;
            spin.Value = value;
            spin.CustomMinimumSize = new Vector2(width, 0);
            return spin;
        }

        private HBoxContainer CreateToggleRow(string rowName, string labelText, string checkName)
        {
            HBoxContainer row = new HBoxContainer();
            row.Name = rowName;
            row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label label = new Label();
            label.Text = labelText;
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            CheckButton check = new CheckButton();
            check.Name = checkName;

            row.AddChild(label);
            row.AddChild(check);
            return row;
        }
        #endregion
    }
}
