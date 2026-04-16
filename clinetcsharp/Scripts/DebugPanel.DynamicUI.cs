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
        private void CreateFreeLookToggle(Node mapTab)
        {
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
            mapTab.AddChild(row);

            _freeLookCheck.Toggled += OnFreeLookToggled;
        }

        private void CreateLineWidthScaleSlider(Node mapTab)
        {
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
            mapTab.AddChild(row);
            mapTab.AddChild(_lineWidthScaleSlider);
        }

        private void CreateLineWidthCalibrationUI(Node mapTab)
        {
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
            mapTab.AddChild(rowToggle);

            CreateCalibrationRefUI(mapTab);
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

        private void CreateResponsiveUI(Node mapTab)
        {
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
            mapTab.AddChild(rowToggle);

            HBoxContainer rowX = new HBoxContainer();
            rowX.Name = "VisibleGridsXRow";
            rowX.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label labelX = new Label();
            labelX.Text = "横向格子数";
            labelX.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            _visibleGridsXSpin = CreateSpinBox(1.0, 30.0, 0.5, 5.0, 70);
            _visibleGridsXSpin.UpdateOnTextChanged = true;
            _visibleGridsXSpin.ValueChanged += OnVisibleGridsChanged;

            rowX.AddChild(labelX);
            rowX.AddChild(_visibleGridsXSpin);
            mapTab.AddChild(rowX);

            _responsiveCheck.Toggled += OnResponsiveToggled;
        }

        private void CreateFontAutoSizeToggle(Node playerTab)
        {
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
            playerTab.AddChild(row);

            _fontAutoSizeCheck.Toggled += OnFontAutoSizeToggled;
        }

        private void CreateEditorKeyConfigUI(Node mapTab)
        {
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
            mapTab.AddChild(rowDrag);

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
            mapTab.AddChild(rowSelect);

            _editorSelectModCheck.Toggled += OnEditorSelectModChanged;
        }

        private void CreateLabelControlsUI(Node playerTab)
        {
            var labelCtrlGroup = new VBoxContainer { Name = "LabelCtrlGroup", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            playerTab.AddChild(labelCtrlGroup);

            var autoCenterRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _labelAutoCenterXCheck = new CheckButton { Text = "X轴自动居中" };
            autoCenterRow.AddChild(_labelAutoCenterXCheck);
            labelCtrlGroup.AddChild(autoCenterRow);

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
            _healthBarLengthSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 20, MaxValue = 400, Step = 1, Value = 80 };
            labelCtrlGroup.AddChild(_healthBarLengthSlider);

            var lenScaleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            lenScaleRow.AddChild(new Label { Text = "长度比例", CustomMinimumSize = new Vector2(60, 0) });
            _healthBarLengthScaleValue = new Label { Text = "0.72", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            lenScaleRow.AddChild(_healthBarLengthScaleValue);
            labelCtrlGroup.AddChild(lenScaleRow);
            _healthBarLengthScaleSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 0.1, MaxValue = 2.0, Step = 0.05, Value = 80.0 / 111.0 };
            labelCtrlGroup.AddChild(_healthBarLengthScaleSlider);

            // Height slider
            var hRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            hRow.AddChild(new Label { Text = "高度", CustomMinimumSize = new Vector2(35, 0) });
            _healthBarHeightValue = new Label { Text = "6", CustomMinimumSize = new Vector2(25, 0), HorizontalAlignment = HorizontalAlignment.Right };
            hRow.AddChild(_healthBarHeightValue);
            labelCtrlGroup.AddChild(hRow);
            _healthBarHeightSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 2, MaxValue = 40, Step = 1, Value = 6 };
            labelCtrlGroup.AddChild(_healthBarHeightSlider);

            var hScaleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            hScaleRow.AddChild(new Label { Text = "高度比例", CustomMinimumSize = new Vector2(60, 0) });
            _healthBarHeightScaleValue = new Label { Text = "0.05", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            hScaleRow.AddChild(_healthBarHeightScaleValue);
            labelCtrlGroup.AddChild(hScaleRow);
            _healthBarHeightScaleSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 0.01, MaxValue = 0.3, Step = 0.01, Value = 6.0 / 111.0 };
            labelCtrlGroup.AddChild(_healthBarHeightScaleSlider);

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
            _healthBarLengthScaleSlider.ValueChanged += OnHealthBarLengthScaleChanged;
            _healthBarLengthScaleSlider.DragEnded += (changed) => PushCurrentStateToHistory();
            _healthBarHeightSlider.ValueChanged += OnHealthBarHeightChanged;
            _healthBarHeightSlider.DragEnded += (changed) => PushCurrentStateToHistory();
            _healthBarHeightScaleSlider.ValueChanged += OnHealthBarHeightScaleChanged;
            _healthBarHeightScaleSlider.DragEnded += (changed) => PushCurrentStateToHistory();
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

        #region Map Debug UI Creation
        private void CreateMapDebugUI()
        {
            var mapTab = GetNode<VBoxContainer>("Control/Panel/ScrollContainer/TabContainer/地图");

            var title = new Label { Text = "地图设置", HorizontalAlignment = HorizontalAlignment.Center };
            title.AddThemeFontSizeOverride("font_size", 13);
            mapTab.AddChild(title);
            mapTab.AddChild(new HSeparator());

            (_gridSizeSlider, _gridSizeValue) = CreateDebugSliderRow(mapTab, "格子大小", 32, 256, 111, 1f);
            (_zoomSlider, _zoomValue) = CreateDebugSliderRow(mapTab, "视角远近", 0.2f, 3.0f, 1.4f, 0.1f);
            (_gridLineWidthSlider, _gridLineWidthValue) = CreateDebugSliderRow(mapTab, "网格线宽", 0.1f, 5.0f, 2.0f, 0.1f);
            (_gridLineBrightnessSlider, _gridLineBrightnessValue) = CreateDebugSliderRow(mapTab, "网格线亮度", 0.1f, 1.0f, 0.7f, 0.1f);

            var coordsRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _gridCoordsCheck = new CheckButton { Text = "显示格子坐标" };
            coordsRow.AddChild(_gridCoordsCheck);
            mapTab.AddChild(coordsRow);

            mapTab.AddChild(new HSeparator());

            (_cameraReturnDelaySlider, _cameraReturnDelayValue) = CreateDebugSliderRow(mapTab, "恢复延迟", 0.0f, 3.0f, 0.5f, 0.1f);
            (_cameraReturnSpeedSlider, _cameraReturnSpeedValue) = CreateDebugSliderRow(mapTab, "回退速度", 1.0f, 20.0f, 5.0f, 1f);

            var easeRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            easeRow.AddChild(new Label { Text = "缓动曲线:", CustomMinimumSize = new Vector2(80, 0) });
            _cameraEaseTypeOption = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            easeRow.AddChild(_cameraEaseTypeOption);
            mapTab.AddChild(easeRow);

            (_cameraEasePowerSlider, _cameraEasePowerValue) = CreateDebugSliderRow(mapTab, "缓动强度", 1.0f, 5.0f, 2.0f, 0.1f);

            var debugRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _debugInfoCheck = new CheckButton { Text = "显示调试信息", ButtonPressed = true };
            debugRow.AddChild(_debugInfoCheck);
            mapTab.AddChild(debugRow);

            var camDebugRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _cameraDebugCheck = new CheckButton { Text = "相机拖拽调试输出" };
            camDebugRow.AddChild(_cameraDebugCheck);
            mapTab.AddChild(camDebugRow);

            // 附加高级动态控件
            CreateFreeLookToggle(mapTab);
            CreateLineWidthScaleSlider(mapTab);
            CreateLineWidthCalibrationUI(mapTab);
            CreateResponsiveUI(mapTab);
            CreateEditorKeyConfigUI(mapTab);
        }
        #endregion

        #region Player Debug UI Creation
        private void CreatePlayerDebugUI()
        {
            var playerTab = GetNode<VBoxContainer>("Control/Panel/ScrollContainer/TabContainer/玩家");

            var title = new Label { Text = "玩家样式", HorizontalAlignment = HorizontalAlignment.Center };
            title.AddThemeFontSizeOverride("font_size", 13);
            playerTab.AddChild(title);
            playerTab.AddChild(new HSeparator());

            (_playerSizeSlider, _playerSizeValue) = CreateDebugSliderRow(playerTab, "角色大小", 32, 256, 111, 1f);
            (_playerSizeScaleSlider, _playerSizeScaleValue) = CreateDebugSliderRow(playerTab, "角色比例", 0.1f, 1.0f, 1.0f, 0.05f);
            (_borderWidthSlider, _borderWidthValue) = CreateDebugSliderRow(playerTab, "边框粗细", 1.0f, 20.0f, 3.0f, 0.5f);
            (_borderWidthScaleSlider, _borderWidthScaleValue) = CreateDebugSliderRow(playerTab, "边框比例", 0.0f, 0.2f, 3.0f / 111.0f, 0.01f);
            (_cornerRadiusSlider, _cornerRadiusValue) = CreateDebugSliderRow(playerTab, "圆角半径", 0.0f, 30.0f, 0.0f, 1f);
            (_bgOpacitySlider, _bgOpacityValue) = CreateDebugSliderRow(playerTab, "背景明度", 0.0f, 1.0f, 0.1f, 0.05f);

            playerTab.AddChild(new HSeparator());

            var fontRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            fontRow.AddChild(new Label { Text = "字体:", CustomMinimumSize = new Vector2(40, 0) });
            _fontOption = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            fontRow.AddChild(_fontOption);
            _loadFontBtn = new Button { Text = "📂 加载", CustomMinimumSize = new Vector2(60, 26) };
            fontRow.AddChild(_loadFontBtn);
            playerTab.AddChild(fontRow);

            (_fontSizeSlider, _fontSizeValue) = CreateDebugSliderRow(playerTab, "字体大小", 0, 48, 0, 1f);
            (_lineSpacingSlider, _lineSpacingValue) = CreateDebugSliderRow(playerTab, "行间距", 0.5f, 1.5f, 0.8f, 0.1f);
            (_letterSpacingSlider, _letterSpacingValue) = CreateDebugSliderRow(playerTab, "字间距", -5, 10, 0, 1f);

            playerTab.AddChild(new Label { Text = "文字对齐:" });
            var alignRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _alignLeftBtn = new Button { Text = "左对齐", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _alignCenterBtn = new Button { Text = "居中", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _alignRightBtn = new Button { Text = "右对齐", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            alignRow.AddChild(_alignLeftBtn);
            alignRow.AddChild(_alignCenterBtn);
            alignRow.AddChild(_alignRightBtn);
            playerTab.AddChild(alignRow);

            var styleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _boldCheck = new CheckButton { Text = "加粗", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _italicCheck = new CheckButton { Text = "斜体", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _shadowCheck = new CheckButton { Text = "阴影", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            styleRow.AddChild(_boldCheck);
            styleRow.AddChild(_italicCheck);
            styleRow.AddChild(_shadowCheck);
            playerTab.AddChild(styleRow);

            playerTab.AddChild(new Label { Text = "行颜色:" });
            _lineColorButtons = new List<Button>();
            for (int i = 0; i < 4; i++)
            {
                var cRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                cRow.AddChild(new Label { Text = $"行{i + 1}:", CustomMinimumSize = new Vector2(40, 0) });
                var cBtn = new Button { Text = "", CustomMinimumSize = new Vector2(40, 24) };
                cRow.AddChild(cBtn);
                _lineColorButtons.Add(cBtn);
                playerTab.AddChild(cRow);
            }

            CreateFontAutoSizeToggle(playerTab);
            CreateLabelControlsUI(playerTab);
        }
        #endregion

        #region Monster Debug UI Creation
        private void CreateMonsterDebugUI()
        {
            var monsterTab = GetNode<VBoxContainer>("Control/Panel/ScrollContainer/TabContainer/怪物");

            // 标题
            var title = new Label { Text = "怪物全局样式", HorizontalAlignment = HorizontalAlignment.Center };
            title.AddThemeFontSizeOverride("font_size", 13);
            monsterTab.AddChild(title);

            monsterTab.AddChild(new HSeparator());

            // 滑块
            (_monsterSizeSlider, _monsterSizeValue) = CreateMonsterSliderRow(monsterTab, "视觉大小", 32, 256, 111);
            (_monsterSizeScaleSlider, _monsterSizeScaleValue) = CreateMonsterSliderRow(monsterTab, "角色比例", 0.1f, 1.0f, 1.0f);
            (_monsterBorderWidthSlider, _monsterBorderWidthValue) = CreateMonsterSliderRow(monsterTab, "边框粗细", 0, 20, 3);
            (_monsterBorderWidthScaleSlider, _monsterBorderWidthScaleValue) = CreateMonsterSliderRow(monsterTab, "边框比例", 0.0f, 0.2f, 3.0f / 111.0f, 0.01f);
            (_monsterCornerRadiusSlider, _monsterCornerRadiusValue) = CreateMonsterSliderRow(monsterTab, "圆角半径", 0, 60, 12);
            (_monsterBgOpacitySlider, _monsterBgOpacityValue) = CreateMonsterSliderRow(monsterTab, "背景不透明度", 0, 1, 0.9f);
            (_monsterFontSizeSlider, _monsterFontSizeValue) = CreateMonsterSliderRow(monsterTab, "字体大小", 0, 48, 0);

            // 颜色
            var bcRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            bcRow.AddChild(new Label { Text = "边框颜色:", CustomMinimumSize = new Vector2(80, 0) });
            _monsterBorderColorPicker = new ColorPickerButton { Color = new Color(0.9f, 0.3f, 0.3f), CustomMinimumSize = new Vector2(60, 26) };
            bcRow.AddChild(_monsterBorderColorPicker);
            monsterTab.AddChild(bcRow);

            var bgcRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            bgcRow.AddChild(new Label { Text = "背景颜色:", CustomMinimumSize = new Vector2(80, 0) });
            _monsterBgColorPicker = new ColorPickerButton { Color = new Color(0.8f, 0.2f, 0.2f), CustomMinimumSize = new Vector2(60, 26) };
            bgcRow.AddChild(_monsterBgColorPicker);
            monsterTab.AddChild(bgcRow);

            var tcRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            tcRow.AddChild(new Label { Text = "文字颜色:", CustomMinimumSize = new Vector2(80, 0) });
            _monsterTextColorPicker = new ColorPickerButton { Color = new Color(1, 0.95f, 0.95f), CustomMinimumSize = new Vector2(60, 26) };
            tcRow.AddChild(_monsterTextColorPicker);
            monsterTab.AddChild(tcRow);

            monsterTab.AddChild(new HSeparator());

            // 4 行文字（每行含内容、字号、X偏移、X居中、Y偏移）
            monsterTab.AddChild(new Label { Text = "显示文字:" });
            for (int i = 0; i < 4; i++)
            {
                var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                row.AddChild(new Label { Text = $"行{i + 1}:", CustomMinimumSize = new Vector2(40, 0) });
                var edit = new LineEdit { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 26) };
                row.AddChild(edit);
                _monsterLabelEdits[i] = edit;
                monsterTab.AddChild(row);

                // 字号行
                var fsRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                fsRow.AddChild(new Control { CustomMinimumSize = new Vector2(40, 0) }); // 缩进占位
                var fsLbl = new Label { Text = "字号:", CustomMinimumSize = new Vector2(36, 0) };
                fsRow.AddChild(fsLbl);
                var fsSlider = new HSlider { MinValue = 0, MaxValue = 48, Value = 0, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 20), Step = 1 };
                fsRow.AddChild(fsSlider);
                var fsVal = new Label { Text = "0", CustomMinimumSize = new Vector2(24, 0) };
                fsRow.AddChild(fsVal);
                fsSlider.ValueChanged += (v) => fsVal.Text = ((int)v).ToString();
                _monsterLabelFontSizeSliders[i] = fsSlider;
                _monsterLabelFontSizeValues[i] = fsVal;
                monsterTab.AddChild(fsRow);

                // X偏移 + 居中行
                var xRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                xRow.AddChild(new Control { CustomMinimumSize = new Vector2(40, 0) }); // 缩进占位
                var xLbl = new Label { Text = "X:", CustomMinimumSize = new Vector2(24, 0) };
                xRow.AddChild(xLbl);
                var xSlider = new HSlider { MinValue = -40, MaxValue = 40, Value = 0, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 20), Step = 1 };
                xRow.AddChild(xSlider);
                var xVal = new Label { Text = "0", CustomMinimumSize = new Vector2(28, 0) };
                xRow.AddChild(xVal);
                var centerCheck = new CheckButton { Text = "居中", ButtonPressed = true };
                xRow.AddChild(centerCheck);
                xSlider.ValueChanged += (v) => xVal.Text = ((int)v).ToString();
                centerCheck.Toggled += (enabled) => { xSlider.Editable = !enabled; xSlider.Modulate = enabled ? new Color(0.5f, 0.5f, 0.5f, 1) : new Color(1, 1, 1, 1); };
                _monsterLabelXOffsetSliders[i] = xSlider;
                _monsterLabelXOffsetValues[i] = xVal;
                _monsterLabelCenterXChecks[i] = centerCheck;
                monsterTab.AddChild(xRow);

                // Y偏移行
                var yRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                yRow.AddChild(new Control { CustomMinimumSize = new Vector2(40, 0) }); // 缩进占位
                var yLbl = new Label { Text = "Y:", CustomMinimumSize = new Vector2(24, 0) };
                yRow.AddChild(yLbl);
                var ySlider = new HSlider { MinValue = -40, MaxValue = 40, Value = 0, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 20), Step = 1 };
                yRow.AddChild(ySlider);
                var yVal = new Label { Text = "0", CustomMinimumSize = new Vector2(28, 0) };
                yRow.AddChild(yVal);
                ySlider.ValueChanged += (v) => yVal.Text = ((int)v).ToString();
                _monsterLabelYOffsetSliders[i] = ySlider;
                _monsterLabelYOffsetValues[i] = yVal;
                monsterTab.AddChild(yRow);
            }

            // 事件绑定
            _monsterSizeSlider.ValueChanged += (_) => ApplyMonsterDebugChanges();
            _monsterSizeSlider.DragEnded += (_) => ApplyMonsterDebugChanges();
            _monsterSizeScaleSlider.ValueChanged += (_) => ApplyMonsterDebugChanges();
            _monsterSizeScaleSlider.DragEnded += (_) => ApplyMonsterDebugChanges();
            _monsterBorderWidthSlider.ValueChanged += (v) => OnMonsterBorderWidthChanged(v);
            _monsterBorderWidthSlider.DragEnded += (v) => OnMonsterBorderWidthDragEnded(v);
            _monsterBorderWidthScaleSlider.ValueChanged += (v) => OnMonsterBorderWidthScaleChanged(v);
            _monsterBorderWidthScaleSlider.DragEnded += (v) => OnMonsterBorderWidthScaleDragEnded(v);
            _monsterCornerRadiusSlider.ValueChanged += (_) => ApplyMonsterDebugChanges();
            _monsterCornerRadiusSlider.DragEnded += (_) => ApplyMonsterDebugChanges();
            _monsterBgOpacitySlider.ValueChanged += (_) => ApplyMonsterDebugChanges();
            _monsterBgOpacitySlider.DragEnded += (_) => ApplyMonsterDebugChanges();
            _monsterFontSizeSlider.ValueChanged += (_) => ApplyMonsterDebugChanges();
            _monsterFontSizeSlider.DragEnded += (_) => ApplyMonsterDebugChanges();
            _monsterBorderColorPicker.ColorChanged += (_) => ApplyMonsterDebugChanges();
            _monsterBgColorPicker.ColorChanged += (_) => ApplyMonsterDebugChanges();
            _monsterTextColorPicker.ColorChanged += (_) => ApplyMonsterDebugChanges();
            for (int i = 0; i < 4; i++)
            {
                _monsterLabelEdits[i].TextChanged += (_) => ApplyMonsterDebugChanges();
                _monsterLabelFontSizeSliders[i].ValueChanged += (_) => ApplyMonsterDebugChanges();
                _monsterLabelFontSizeSliders[i].DragEnded += (_) => ApplyMonsterDebugChanges();
                _monsterLabelXOffsetSliders[i].ValueChanged += (_) => ApplyMonsterDebugChanges();
                _monsterLabelXOffsetSliders[i].DragEnded += (_) => ApplyMonsterDebugChanges();
                _monsterLabelCenterXChecks[i].Toggled += (_) => ApplyMonsterDebugChanges();
                _monsterLabelYOffsetSliders[i].ValueChanged += (_) => ApplyMonsterDebugChanges();
                _monsterLabelYOffsetSliders[i].DragEnded += (_) => ApplyMonsterDebugChanges();
            }
        }

        private (HSlider slider, Label valueLabel) CreateMonsterSliderRow(Container parent, string label, float min, float max, float def, float? step = null)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(new Label { Text = label + ":", CustomMinimumSize = new Vector2(80, 0) });

            var slider = new HSlider { MinValue = min, MaxValue = max, Value = def, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 20), Step = step ?? (max <= 1 ? 0.05f : 1f) };
            row.AddChild(slider);

            var valLbl = new Label { Text = def.ToString("F1"), CustomMinimumSize = new Vector2(36, 0) };
            row.AddChild(valLbl);

            slider.ValueChanged += (v) => valLbl.Text = (max <= 1 ? v.ToString("F2") : ((int)v).ToString());
            parent.AddChild(row);
            return (slider, valLbl);
        }

        private void SyncMonsterDebugUI()
        {
            var mm = GetTree()?.GetFirstNodeInGroup("monster_manager") as MonsterManager;
            if (mm == null) return;

            _monsterSizeSlider.Value = mm.DefaultVisualSize;
            _monsterSizeScaleSlider.Value = mm.DefaultVisualSizeScale;
            _monsterBorderWidthSlider.Value = mm.DefaultBorderWidth;
            _monsterBorderWidthScaleSlider.Value = mm.DefaultBorderWidthScale;
            _monsterCornerRadiusSlider.Value = mm.DefaultCornerRadius;
            _monsterBgOpacitySlider.Value = mm.DefaultBgOpacity;
            _monsterFontSizeSlider.Value = mm.DefaultFontSize;
            _monsterBorderColorPicker.Color = mm.DefaultBorderColor;
            _monsterBgColorPicker.Color = mm.DefaultBgColor;
            _monsterTextColorPicker.Color = mm.DefaultTextColor;
            for (int i = 0; i < 4; i++)
            {
                _monsterLabelEdits[i].Text = mm.DefaultLabelTexts[i] ?? "";
                _monsterLabelFontSizeSliders[i].Value = mm.DefaultLabelFontSizes[i];
                _monsterLabelFontSizeValues[i].Text = mm.DefaultLabelFontSizes[i].ToString();
                _monsterLabelXOffsetSliders[i].Value = mm.DefaultLabelXOffsets[i];
                _monsterLabelXOffsetValues[i].Text = mm.DefaultLabelXOffsets[i].ToString("F0");
                _monsterLabelCenterXChecks[i].ButtonPressed = mm.DefaultLabelCenterX[i];
                _monsterLabelXOffsetSliders[i].Editable = !mm.DefaultLabelCenterX[i];
                _monsterLabelXOffsetSliders[i].Modulate = mm.DefaultLabelCenterX[i] ? new Color(0.5f, 0.5f, 0.5f, 1) : new Color(1, 1, 1, 1);
                _monsterLabelYOffsetSliders[i].Value = mm.DefaultLabelYOffsets[i];
                _monsterLabelYOffsetValues[i].Text = mm.DefaultLabelYOffsets[i].ToString("F0");
            }
        }

        private void OnMonsterBorderWidthChanged(double value)
        {
            if (_monsterBorderWidthValue != null)
                _monsterBorderWidthValue.Text = ((int)value).ToString();
        }

        private void OnMonsterBorderWidthDragEnded(bool valueChanged)
        {
            if (!valueChanged) return;
            int gridSize = (int)_gridSizeSlider.Value;
            float scale = gridSize > 0 ? (float)(_monsterBorderWidthSlider.Value / gridSize) : 0.0f;
            if (_monsterBorderWidthScaleSlider != null)
            {
                _monsterBorderWidthScaleSlider.SetBlockSignals(true);
                _monsterBorderWidthScaleSlider.Value = scale;
                _monsterBorderWidthScaleSlider.SetBlockSignals(false);
                _monsterBorderWidthScaleValue.Text = scale.ToString("F2");
            }
            ApplyMonsterDebugChanges();
        }

        private void OnMonsterBorderWidthScaleChanged(double value)
        {
            if (_monsterBorderWidthScaleValue != null)
                _monsterBorderWidthScaleValue.Text = value.ToString("F2");
        }

        private void OnMonsterBorderWidthScaleDragEnded(bool valueChanged)
        {
            if (!valueChanged) return;
            int gridSize = (int)_gridSizeSlider.Value;
            float newWidth = Mathf.Clamp((float)_monsterBorderWidthScaleSlider.Value * gridSize, 1.0f, 20.0f);
            if (_monsterBorderWidthSlider != null)
            {
                _monsterBorderWidthSlider.SetBlockSignals(true);
                _monsterBorderWidthSlider.Value = newWidth;
                _monsterBorderWidthSlider.SetBlockSignals(false);
                _monsterBorderWidthValue.Text = ((int)newWidth).ToString();
            }
            ApplyMonsterDebugChanges();
        }

        private void ApplyMonsterDebugChanges()
        {
            var mm = GetTree()?.GetFirstNodeInGroup("monster_manager") as MonsterManager;
            if (mm == null) return;

            mm.DefaultVisualSize = (int)_monsterSizeSlider.Value;
            mm.DefaultVisualSizeScale = (float)_monsterSizeScaleSlider.Value;
            mm.DefaultBorderWidth = (float)_monsterBorderWidthSlider.Value;
            mm.DefaultBorderWidthScale = (float)_monsterBorderWidthScaleSlider.Value;
            mm.DefaultCornerRadius = (float)_monsterCornerRadiusSlider.Value;
            mm.DefaultBgOpacity = (float)_monsterBgOpacitySlider.Value;
            mm.DefaultFontSize = (int)_monsterFontSizeSlider.Value;
            mm.DefaultBorderColor = _monsterBorderColorPicker.Color;
            mm.DefaultBgColor = _monsterBgColorPicker.Color;
            mm.DefaultTextColor = _monsterTextColorPicker.Color;
            for (int i = 0; i < 4; i++)
            {
                mm.DefaultLabelTexts[i] = _monsterLabelEdits[i].Text;
                mm.DefaultLabelFontSizes[i] = (int)_monsterLabelFontSizeSliders[i].Value;
                mm.DefaultLabelXOffsets[i] = (float)_monsterLabelXOffsetSliders[i].Value;
                mm.DefaultLabelCenterX[i] = _monsterLabelCenterXChecks[i].ButtonPressed;
                mm.DefaultLabelYOffsets[i] = (float)_monsterLabelYOffsetSliders[i].Value;
            }

            mm.ApplyStyleToAll();
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

        private (HSlider slider, Label valueLabel) CreateDebugSliderRow(Container parent, string label, float min, float max, float def, float step = -1f)
        {
            float actualStep = step > 0 ? step : (max <= 1.0f ? 0.05f : 1f);
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(new Label { Text = label + ":", CustomMinimumSize = new Vector2(80, 0) });

            var slider = new HSlider { MinValue = min, MaxValue = max, Value = def, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 20), Step = actualStep };
            row.AddChild(slider);

            string initialText = actualStep < 1.0f ? def.ToString("F1") : ((int)def).ToString();
            var valLbl = new Label { Text = initialText, CustomMinimumSize = new Vector2(36, 0) };
            row.AddChild(valLbl);

            slider.ValueChanged += (v) => valLbl.Text = (actualStep < 1.0f ? v.ToString("F1") : ((int)v).ToString());
            parent.AddChild(row);
            return (slider, valLbl);
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
