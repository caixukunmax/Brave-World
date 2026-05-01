using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// DebugPanel Player Tab — extracted from DebugPanel partial classes.
    /// Controls player appearance, text, labels, healthbar, castbar, actionbar, levelbadge.
    /// </summary>
    public class DebugPanelPlayerTab : DebugPanelTab
    {
        public DebugPanelPlayerTab(DebugPanel owner) : base(owner) { }

        public override string TabKey => "player";

        /// <summary>Whether font auto-size is currently enabled (for Owner.UpdateControlStates)</summary>
        public bool FontAutoSize => _fontAutoSizeCheck?.ButtonPressed ?? false;

        #region Player Tab - Appearance
        private HSlider _playerSizeSlider;
        private Label _playerSizeValue;
        private HSlider _playerSizeScaleSlider;
        private Label _playerSizeScaleValue;
        private HSlider _borderWidthSlider;
        private Label _borderWidthValue;
        private HSlider _borderWidthScaleSlider;
        private Label _borderWidthScaleValue;
        private HSlider _cornerRadiusSlider;
        private Label _cornerRadiusValue;
        private HSlider _bgOpacitySlider;
        private Label _bgOpacityValue;
        #endregion

        #region Player Tab - Text Style
        private OptionButton _fontOption;
        private Button _loadFontBtn;
        private HSlider _fontSizeSlider;
        private Label _fontSizeValue;
        private HSlider _lineSpacingSlider;
        private Label _lineSpacingValue;
        private HSlider _letterSpacingSlider;
        private Label _letterSpacingValue;
        private Button _alignLeftBtn;
        private Button _alignCenterBtn;
        private Button _alignRightBtn;
        #endregion

        #region Player Tab - Visual Effects
        private List<Button> _lineColorButtons = new List<Button>();
        private CheckButton _boldCheck;
        private CheckButton _italicCheck;
        private CheckButton _shadowCheck;
        private CheckButton _fontAutoSizeCheck;
        #endregion

        #region Player Tab - Label Controls (4 independent)
        private const int LabelCount = 4;
        private CheckButton[] _labelVisibleChecks = new CheckButton[LabelCount];
        private LineEdit[] _labelNameEdits = new LineEdit[LabelCount];
        private LineEdit[] _labelTextEdits = new LineEdit[LabelCount];
        private HSlider[] _labelFontSizeSliders = new HSlider[LabelCount];
        private Label[] _labelFontSizeValues = new Label[LabelCount];
        private Button[] _labelColorButtons = new Button[LabelCount];
        private HSlider[] _labelOffsetXSliders = new HSlider[LabelCount];
        private HSlider[] _labelOffsetYSliders = new HSlider[LabelCount];
        private Label[] _labelOffsetXValues = new Label[LabelCount];
        private Label[] _labelOffsetYValues = new Label[LabelCount];
        private Button[] _labelResetButtons = new Button[LabelCount];
        private CheckButton _labelAutoCenterXCheck;
        #endregion

        #region Health Bar Controls
        private CheckButton _healthBarVisibleCheck;
        private HSlider _healthBarLengthSlider;
        private Label _healthBarLengthValue;
        private HSlider _healthBarLengthScaleSlider;
        private Label _healthBarLengthScaleValue;
        private HSlider _healthBarHeightSlider;
        private Label _healthBarHeightValue;
        private HSlider _healthBarHeightScaleSlider;
        private Label _healthBarHeightScaleValue;
        private HSlider _healthBarFillSlider;
        private Label _healthBarFillValue;
        private Button _healthBarColorBtn;
        private HSlider _healthBarOffsetXSlider;
        private HSlider _healthBarOffsetYSlider;
        private Label _healthBarOffsetXValue;
        private Label _healthBarOffsetYValue;
        #endregion

        #region MP Bar Controls
        private CheckButton _mpBarVisibleCheck;
        private HSlider _mpBarLengthSlider;
        private Label _mpBarLengthValue;
        private HSlider _mpBarLengthScaleSlider;
        private Label _mpBarLengthScaleValue;
        private HSlider _mpBarHeightSlider;
        private Label _mpBarHeightValue;
        private HSlider _mpBarHeightScaleSlider;
        private Label _mpBarHeightScaleValue;
        private HSlider _mpBarFillSlider;
        private Label _mpBarFillValue;
        private Button _mpBarColorBtn;
        private HSlider _mpBarOffsetXSlider;
        private HSlider _mpBarOffsetYSlider;
        private Label _mpBarOffsetXValue;
        private Label _mpBarOffsetYValue;
        #endregion

        #region Cast Bar Controls
        private CheckButton _castBarVisibleCheck;
        private HSlider _castBarLengthSlider;
        private Label _castBarLengthValue;
        private HSlider _castBarHeightSlider;
        private Label _castBarHeightValue;
        private HSlider _castBarFillSlider;
        private Label _castBarFillValue;
        private Button _castBarColorBtn;
        private HSlider _castBarOffsetXSlider;
        private HSlider _castBarOffsetYSlider;
        private Label _castBarOffsetXValue;
        private Label _castBarOffsetYValue;
        #endregion

        #region Action Bar Controls
        private CheckButton _actionBarForceShowCheck;
        private HSlider _actionBarTextYOffsetSlider;
        private Label _actionBarTextYOffsetValue;
        private HSlider _actionBarProgressHeightSlider;
        private Label _actionBarProgressHeightValue;
        #endregion

        #region Level Badge Controls
        private CheckButton _levelBadgeVisibleCheck;
        private HSlider _levelBadgeFontSizeSlider;
        private Label _levelBadgeFontSizeValue;
        private Button _levelBadgeTextColorBtn;
        private LineEdit _levelBadgeTextEdit;
        private HSlider _levelBadgeOffsetXSlider;
        private HSlider _levelBadgeOffsetYSlider;
        private Label _levelBadgeOffsetXValue;
        private Label _levelBadgeOffsetYValue;
        #endregion

        #region Dynamic controls
        private FileDialog _fontFileDialog = null!;
        #endregion

        // ═══════════════════════════════════════════════════════════════════════
        //  BuildUI
        // ═══════════════════════════════════════════════════════════════════════

        public override void BuildUI(VBoxContainer tabContainer)
        {
            CreatePlayerDebugUI(tabContainer);
        }

        private void CreatePlayerDebugUI(VBoxContainer playerTab)
        {
            var title = new Label { Text = "玩家样式", HorizontalAlignment = HorizontalAlignment.Center };
            title.AddThemeFontSizeOverride("font_size", 13);
            playerTab.AddChild(title);
            playerTab.AddChild(new HSeparator());

            (_playerSizeSlider, _playerSizeValue) = CreateSliderRow(playerTab, "角色大小", 32, 256, 111, 1f);
            (_playerSizeScaleSlider, _playerSizeScaleValue) = CreateSliderRow(playerTab, "角色比例", 0.1f, 1.0f, 1.0f, DebugPanelLengthScalePolicy.StepF);
            (_borderWidthSlider, _borderWidthValue) = CreateSliderRow(playerTab, "边框粗细", 1.0f, 20.0f, 3.0f, 0.5f);
            (_borderWidthScaleSlider, _borderWidthScaleValue) = CreateSliderRow(playerTab, "边框比例", 0.0f, 0.2f, 3.0f / 111.0f, DebugPanelLengthScalePolicy.StepF);
            (_cornerRadiusSlider, _cornerRadiusValue) = CreateSliderRow(playerTab, "圆角半径", 0.0f, 30.0f, 0.0f, 1f);
            (_bgOpacitySlider, _bgOpacityValue) = CreateSliderRow(playerTab, "背景明度", 0.0f, 1.0f, 0.1f, DebugPanelLengthScalePolicy.StepF);

            playerTab.AddChild(new HSeparator());

            var fontRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            fontRow.AddChild(new Label { Text = "字体:", CustomMinimumSize = new Vector2(40, 0) });
            _fontOption = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            fontRow.AddChild(_fontOption);
            _loadFontBtn = new Button { Text = "加载", CustomMinimumSize = new Vector2(60, 26) };
            fontRow.AddChild(_loadFontBtn);
            playerTab.AddChild(fontRow);

            (_fontSizeSlider, _fontSizeValue) = CreateSliderRow(playerTab, "字体大小", 0, 48, 0, 1f);
            (_lineSpacingSlider, _lineSpacingValue) = CreateSliderRow(playerTab, "行间距", 0.5f, 1.5f, 0.8f, DebugPanelLengthScalePolicy.StepF);
            (_letterSpacingSlider, _letterSpacingValue) = CreateSliderRow(playerTab, "字间距", -5, 10, 0, 1f);

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

                var fontSizeSlider = new HSlider { Scrollable = false };
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

                var offsetXSlider = new HSlider { Scrollable = false };
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

                var offsetYSlider = new HSlider { Scrollable = false };
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
            labelCtrlGroup.AddChild(new HSeparator { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

            var hpTitleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var hpNameLabel = new Label { Text = "血条", CustomMinimumSize = new Vector2(45, 0) };
            hpTitleRow.AddChild(hpNameLabel);
            _healthBarVisibleCheck = new CheckButton { ButtonPressed = true };
            hpTitleRow.AddChild(_healthBarVisibleCheck);
            hpTitleRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            _healthBarColorBtn = new Button { Text = "色", CustomMinimumSize = new Vector2(30, 24) };
            _healthBarColorBtn.Modulate = new Color(0, 0.8f, 0, 1);
            hpTitleRow.AddChild(_healthBarColorBtn);
            labelCtrlGroup.AddChild(hpTitleRow);

            // Length
            var lenRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            lenRow.AddChild(new Label { Text = "长度", CustomMinimumSize = new Vector2(35, 0) });
            _healthBarLengthValue = new Label { Text = "80", CustomMinimumSize = new Vector2(25, 0), HorizontalAlignment = HorizontalAlignment.Right };
            lenRow.AddChild(_healthBarLengthValue);
            labelCtrlGroup.AddChild(lenRow);
            _healthBarLengthSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 20, MaxValue = 400, Step = 1, Value = 80 , Scrollable = false };
            labelCtrlGroup.AddChild(_healthBarLengthSlider);

            var lenScaleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            lenScaleRow.AddChild(new Label { Text = "长度比例", CustomMinimumSize = new Vector2(60, 0) });
            _healthBarLengthScaleValue = new Label { Text = "0.72", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            lenScaleRow.AddChild(_healthBarLengthScaleValue);
            labelCtrlGroup.AddChild(lenScaleRow);
            _healthBarLengthScaleSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 0.1, MaxValue = 2.0, Step = DebugPanelLengthScalePolicy.Step, Value = 80.0 / 111.0 , Scrollable = false };
            labelCtrlGroup.AddChild(_healthBarLengthScaleSlider);

            // Height
            var hRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            hRow.AddChild(new Label { Text = "高度", CustomMinimumSize = new Vector2(35, 0) });
            _healthBarHeightValue = new Label { Text = "6", CustomMinimumSize = new Vector2(25, 0), HorizontalAlignment = HorizontalAlignment.Right };
            hRow.AddChild(_healthBarHeightValue);
            labelCtrlGroup.AddChild(hRow);
            _healthBarHeightSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 2, MaxValue = 40, Step = 1, Value = 6 , Scrollable = false };
            labelCtrlGroup.AddChild(_healthBarHeightSlider);

            var hScaleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            hScaleRow.AddChild(new Label { Text = "高度比例", CustomMinimumSize = new Vector2(60, 0) });
            _healthBarHeightScaleValue = new Label { Text = "0.05", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            hScaleRow.AddChild(_healthBarHeightScaleValue);
            labelCtrlGroup.AddChild(hScaleRow);
            _healthBarHeightScaleSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 0.01, MaxValue = 0.3, Step = DebugPanelLengthScalePolicy.Step, Value = 6.0 / 111.0 , Scrollable = false };
            labelCtrlGroup.AddChild(_healthBarHeightScaleSlider);

            // Fill
            var fillRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            fillRow.AddChild(new Label { Text = "填充", CustomMinimumSize = new Vector2(35, 0) });
            _healthBarFillValue = new Label { Text = "100%", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            fillRow.AddChild(_healthBarFillValue);
            labelCtrlGroup.AddChild(fillRow);
            _healthBarFillSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 0, MaxValue = 100, Step = 1, Value = 100 , Scrollable = false };
            labelCtrlGroup.AddChild(_healthBarFillSlider);

            // HealthBar Offset X
            var hpxRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            hpxRow.AddChild(new Label { Text = "X偏移", CustomMinimumSize = new Vector2(45, 0) });
            _healthBarOffsetXValue = new Label { Text = "0", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            hpxRow.AddChild(_healthBarOffsetXValue);
            labelCtrlGroup.AddChild(hpxRow);
            _healthBarOffsetXSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = -150, MaxValue = 150, Step = 1, Value = 0 , Scrollable = false };
            labelCtrlGroup.AddChild(_healthBarOffsetXSlider);

            // HealthBar Offset Y
            var hpyRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            hpyRow.AddChild(new Label { Text = "Y偏移", CustomMinimumSize = new Vector2(45, 0) });
            _healthBarOffsetYValue = new Label { Text = "-70", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            hpyRow.AddChild(_healthBarOffsetYValue);
            labelCtrlGroup.AddChild(hpyRow);
            _healthBarOffsetYSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = -150, MaxValue = 150, Step = 1, Value = -70 , Scrollable = false };
            labelCtrlGroup.AddChild(_healthBarOffsetYSlider);

            // HealthBar signals
            _healthBarVisibleCheck.Toggled += OnHealthBarVisibleToggled;
            _healthBarColorBtn.Pressed += OnHealthBarColorPressed;
            _healthBarLengthSlider.ValueChanged += OnHealthBarLengthChanged;
            _healthBarLengthSlider.DragEnded += (changed) => Owner.PushCurrentStateToHistory();
            AttachValueLineEdit(_healthBarLengthSlider, _healthBarLengthValue);
            _healthBarLengthScaleSlider.ValueChanged += OnHealthBarLengthScaleChanged;
            _healthBarLengthScaleSlider.DragEnded += (changed) => Owner.PushCurrentStateToHistory();
            AttachValueLineEdit(_healthBarLengthScaleSlider, _healthBarLengthScaleValue);
            _healthBarHeightSlider.ValueChanged += OnHealthBarHeightChanged;
            _healthBarHeightSlider.DragEnded += (changed) => Owner.PushCurrentStateToHistory();
            AttachValueLineEdit(_healthBarHeightSlider, _healthBarHeightValue);
            _healthBarHeightScaleSlider.ValueChanged += OnHealthBarHeightScaleChanged;
            _healthBarHeightScaleSlider.DragEnded += (changed) => Owner.PushCurrentStateToHistory();
            AttachValueLineEdit(_healthBarHeightScaleSlider, _healthBarHeightScaleValue);
            _healthBarFillSlider.ValueChanged += OnHealthBarFillChanged;
            _healthBarFillSlider.DragEnded += (changed) => Owner.PushCurrentStateToHistory();
            AttachValueLineEdit(_healthBarFillSlider, _healthBarFillValue);
            _healthBarOffsetXSlider.ValueChanged += OnHealthBarOffsetXChanged;
            _healthBarOffsetXSlider.DragEnded += (changed) => Owner.PushCurrentStateToHistory();
            AttachValueLineEdit(_healthBarOffsetXSlider, _healthBarOffsetXValue);
            _healthBarOffsetYSlider.ValueChanged += OnHealthBarOffsetYChanged;
            _healthBarOffsetYSlider.DragEnded += (changed) => Owner.PushCurrentStateToHistory();
            AttachValueLineEdit(_healthBarOffsetYSlider, _healthBarOffsetYValue);

            // ========== MP条控制 ==========
            labelCtrlGroup.AddChild(new HSeparator { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

            var mpTitleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            mpTitleRow.AddChild(new Label { Text = "MP条", CustomMinimumSize = new Vector2(45, 0) });
            _mpBarVisibleCheck = new CheckButton { ButtonPressed = true };
            mpTitleRow.AddChild(_mpBarVisibleCheck);
            mpTitleRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            _mpBarColorBtn = new Button { Text = "色", CustomMinimumSize = new Vector2(30, 24) };
            _mpBarColorBtn.Modulate = new Color(0.2f, 0.4f, 1.0f, 1);
            mpTitleRow.AddChild(_mpBarColorBtn);
            labelCtrlGroup.AddChild(mpTitleRow);

            var mpLenRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            mpLenRow.AddChild(new Label { Text = "长度", CustomMinimumSize = new Vector2(35, 0) });
            _mpBarLengthValue = new Label { Text = "80", CustomMinimumSize = new Vector2(25, 0), HorizontalAlignment = HorizontalAlignment.Right };
            mpLenRow.AddChild(_mpBarLengthValue);
            labelCtrlGroup.AddChild(mpLenRow);
            _mpBarLengthSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 20, MaxValue = 400, Step = 1, Value = 80 , Scrollable = false };
            labelCtrlGroup.AddChild(_mpBarLengthSlider);

            var mpLenScaleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            mpLenScaleRow.AddChild(new Label { Text = "长度比例", CustomMinimumSize = new Vector2(60, 0) });
            _mpBarLengthScaleValue = new Label { Text = "0.72", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            mpLenScaleRow.AddChild(_mpBarLengthScaleValue);
            labelCtrlGroup.AddChild(mpLenScaleRow);
            _mpBarLengthScaleSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 0.1, MaxValue = 2.0, Step = DebugPanelLengthScalePolicy.Step, Value = 80.0 / 111.0 , Scrollable = false };
            labelCtrlGroup.AddChild(_mpBarLengthScaleSlider);

            var mpHRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            mpHRow.AddChild(new Label { Text = "高度", CustomMinimumSize = new Vector2(35, 0) });
            _mpBarHeightValue = new Label { Text = "4", CustomMinimumSize = new Vector2(25, 0), HorizontalAlignment = HorizontalAlignment.Right };
            mpHRow.AddChild(_mpBarHeightValue);
            labelCtrlGroup.AddChild(mpHRow);
            _mpBarHeightSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 2, MaxValue = 40, Step = 1, Value = 4 , Scrollable = false };
            labelCtrlGroup.AddChild(_mpBarHeightSlider);

            var mpHScaleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            mpHScaleRow.AddChild(new Label { Text = "高度比例", CustomMinimumSize = new Vector2(60, 0) });
            _mpBarHeightScaleValue = new Label { Text = "0.04", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            mpHScaleRow.AddChild(_mpBarHeightScaleValue);
            labelCtrlGroup.AddChild(mpHScaleRow);
            _mpBarHeightScaleSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 0.01, MaxValue = 0.3, Step = DebugPanelLengthScalePolicy.Step, Value = 4.0 / 111.0 , Scrollable = false };
            labelCtrlGroup.AddChild(_mpBarHeightScaleSlider);

            var mpFillRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            mpFillRow.AddChild(new Label { Text = "填充", CustomMinimumSize = new Vector2(35, 0) });
            _mpBarFillValue = new Label { Text = "100%", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            mpFillRow.AddChild(_mpBarFillValue);
            labelCtrlGroup.AddChild(mpFillRow);
            _mpBarFillSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 0, MaxValue = 100, Step = 1, Value = 100 , Scrollable = false };
            labelCtrlGroup.AddChild(_mpBarFillSlider);

            var mpOffXRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            mpOffXRow.AddChild(new Label { Text = "X偏移", CustomMinimumSize = new Vector2(45, 0) });
            _mpBarOffsetXValue = new Label { Text = "0", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            mpOffXRow.AddChild(_mpBarOffsetXValue);
            labelCtrlGroup.AddChild(mpOffXRow);
            _mpBarOffsetXSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = -150, MaxValue = 150, Step = 1, Value = 0 , Scrollable = false };
            labelCtrlGroup.AddChild(_mpBarOffsetXSlider);

            var mpOffYRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            mpOffYRow.AddChild(new Label { Text = "Y偏移", CustomMinimumSize = new Vector2(45, 0) });
            _mpBarOffsetYValue = new Label { Text = "-62", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            mpOffYRow.AddChild(_mpBarOffsetYValue);
            labelCtrlGroup.AddChild(mpOffYRow);
            _mpBarOffsetYSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = -150, MaxValue = 150, Step = 1, Value = -62 , Scrollable = false };
            labelCtrlGroup.AddChild(_mpBarOffsetYSlider);

            // MPBar signals
            _mpBarVisibleCheck.Toggled += OnMpBarVisibleToggled;
            _mpBarColorBtn.Pressed += OnMpBarColorPressed;
            _mpBarLengthSlider.ValueChanged += OnMpBarLengthChanged;
            _mpBarLengthSlider.DragEnded += (changed) => Owner.PushCurrentStateToHistory();
            AttachValueLineEdit(_mpBarLengthSlider, _mpBarLengthValue);
            _mpBarLengthScaleSlider.ValueChanged += OnMpBarLengthScaleChanged;
            _mpBarLengthScaleSlider.DragEnded += (changed) => Owner.PushCurrentStateToHistory();
            AttachValueLineEdit(_mpBarLengthScaleSlider, _mpBarLengthScaleValue);
            _mpBarHeightSlider.ValueChanged += OnMpBarHeightChanged;
            _mpBarHeightSlider.DragEnded += (changed) => Owner.PushCurrentStateToHistory();
            AttachValueLineEdit(_mpBarHeightSlider, _mpBarHeightValue);
            _mpBarHeightScaleSlider.ValueChanged += OnMpBarHeightScaleChanged;
            _mpBarHeightScaleSlider.DragEnded += (changed) => Owner.PushCurrentStateToHistory();
            AttachValueLineEdit(_mpBarHeightScaleSlider, _mpBarHeightScaleValue);
            _mpBarFillSlider.ValueChanged += OnMpBarFillChanged;
            _mpBarFillSlider.DragEnded += (changed) => Owner.PushCurrentStateToHistory();
            AttachValueLineEdit(_mpBarFillSlider, _mpBarFillValue);
            _mpBarOffsetXSlider.ValueChanged += OnMpBarOffsetXChanged;
            _mpBarOffsetXSlider.DragEnded += (changed) => Owner.PushCurrentStateToHistory();
            AttachValueLineEdit(_mpBarOffsetXSlider, _mpBarOffsetXValue);
            _mpBarOffsetYSlider.ValueChanged += OnMpBarOffsetYChanged;
            _mpBarOffsetYSlider.DragEnded += (changed) => Owner.PushCurrentStateToHistory();
            AttachValueLineEdit(_mpBarOffsetYSlider, _mpBarOffsetYValue);

            // ========== 施法条控制 ==========
            labelCtrlGroup.AddChild(new HSeparator { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

            var ctTitleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            ctTitleRow.AddChild(new Label { Text = "施法", CustomMinimumSize = new Vector2(45, 0) });
            _castBarVisibleCheck = new CheckButton { ButtonPressed = true };
            ctTitleRow.AddChild(_castBarVisibleCheck);
            ctTitleRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            _castBarColorBtn = new Button { Text = "色", CustomMinimumSize = new Vector2(30, 24) };
            _castBarColorBtn.Modulate = new Color(0.3f, 0.5f, 1, 1);
            ctTitleRow.AddChild(_castBarColorBtn);
            labelCtrlGroup.AddChild(ctTitleRow);

            // CastBar Length
            var ctLenRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            ctLenRow.AddChild(new Label { Text = "长度", CustomMinimumSize = new Vector2(35, 0) });
            _castBarLengthValue = new Label { Text = "60", CustomMinimumSize = new Vector2(25, 0), HorizontalAlignment = HorizontalAlignment.Right };
            ctLenRow.AddChild(_castBarLengthValue);
            labelCtrlGroup.AddChild(ctLenRow);
            _castBarLengthSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 20, MaxValue = 200, Step = 1, Value = 60 , Scrollable = false };
            labelCtrlGroup.AddChild(_castBarLengthSlider);

            // CastBar Height
            var ctHtRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            ctHtRow.AddChild(new Label { Text = "高度", CustomMinimumSize = new Vector2(35, 0) });
            _castBarHeightValue = new Label { Text = "4", CustomMinimumSize = new Vector2(25, 0), HorizontalAlignment = HorizontalAlignment.Right };
            ctHtRow.AddChild(_castBarHeightValue);
            labelCtrlGroup.AddChild(ctHtRow);
            _castBarHeightSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 2, MaxValue = 20, Step = 1, Value = 4 , Scrollable = false };
            labelCtrlGroup.AddChild(_castBarHeightSlider);

            // CastBar Fill
            var ctFillRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            ctFillRow.AddChild(new Label { Text = "填充", CustomMinimumSize = new Vector2(35, 0) });
            _castBarFillValue = new Label { Text = "60%", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            ctFillRow.AddChild(_castBarFillValue);
            labelCtrlGroup.AddChild(ctFillRow);
            _castBarFillSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 0, MaxValue = 100, Step = 1, Value = 60 , Scrollable = false };
            labelCtrlGroup.AddChild(_castBarFillSlider);

            // CastBar Offset X
            var ctOxRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            ctOxRow.AddChild(new Label { Text = "X偏移", CustomMinimumSize = new Vector2(45, 0) });
            _castBarOffsetXValue = new Label { Text = "0", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            ctOxRow.AddChild(_castBarOffsetXValue);
            labelCtrlGroup.AddChild(ctOxRow);
            _castBarOffsetXSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = -150, MaxValue = 150, Step = 1, Value = 0 , Scrollable = false };
            labelCtrlGroup.AddChild(_castBarOffsetXSlider);

            // CastBar Offset Y
            var ctOyRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            ctOyRow.AddChild(new Label { Text = "Y偏移", CustomMinimumSize = new Vector2(45, 0) });
            _castBarOffsetYValue = new Label { Text = "-80", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            ctOyRow.AddChild(_castBarOffsetYValue);
            labelCtrlGroup.AddChild(ctOyRow);
            _castBarOffsetYSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = -150, MaxValue = 150, Step = 1, Value = -80 , Scrollable = false };
            labelCtrlGroup.AddChild(_castBarOffsetYSlider);

            // CastBar signals
            _castBarVisibleCheck.Toggled += OnCastBarVisibleToggled;
            _castBarColorBtn.Pressed += OnCastBarColorPressed;
            _castBarLengthSlider.ValueChanged += OnCastBarLengthChanged;
            _castBarLengthSlider.DragEnded += (changed) => Owner.PushCurrentStateToHistory();
            AttachValueLineEdit(_castBarLengthSlider, _castBarLengthValue);
            _castBarHeightSlider.ValueChanged += OnCastBarHeightChanged;
            _castBarHeightSlider.DragEnded += (changed) => Owner.PushCurrentStateToHistory();
            AttachValueLineEdit(_castBarHeightSlider, _castBarHeightValue);
            _castBarFillSlider.ValueChanged += OnCastBarFillChanged;
            _castBarFillSlider.DragEnded += (changed) => Owner.PushCurrentStateToHistory();
            AttachValueLineEdit(_castBarFillSlider, _castBarFillValue);
            _castBarOffsetXSlider.ValueChanged += OnCastBarOffsetXChanged;
            _castBarOffsetXSlider.DragEnded += (changed) => Owner.PushCurrentStateToHistory();
            AttachValueLineEdit(_castBarOffsetXSlider, _castBarOffsetXValue);
            _castBarOffsetYSlider.ValueChanged += OnCastBarOffsetYChanged;
            _castBarOffsetYSlider.DragEnded += (changed) => Owner.PushCurrentStateToHistory();
            AttachValueLineEdit(_castBarOffsetYSlider, _castBarOffsetYValue);

            // ========== 动作栏控制 ==========
            labelCtrlGroup.AddChild(new HSeparator { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

            var abTitleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            abTitleRow.AddChild(new Label { Text = "动作栏", CustomMinimumSize = new Vector2(45, 0) });
            _actionBarForceShowCheck = new CheckButton { ButtonPressed = false };
            abTitleRow.AddChild(_actionBarForceShowCheck);
            abTitleRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            labelCtrlGroup.AddChild(abTitleRow);

            // Text Y offset
            var abTxtRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            abTxtRow.AddChild(new Label { Text = "文字Y偏移", CustomMinimumSize = new Vector2(60, 0) });
            _actionBarTextYOffsetValue = new Label { Text = "0", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            abTxtRow.AddChild(_actionBarTextYOffsetValue);
            labelCtrlGroup.AddChild(abTxtRow);
            _actionBarTextYOffsetSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = -30, MaxValue = 30, Step = 1, Value = 0 , Scrollable = false };
            labelCtrlGroup.AddChild(_actionBarTextYOffsetSlider);

            // Progress height
            var abPhRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            abPhRow.AddChild(new Label { Text = "进度条高度", CustomMinimumSize = new Vector2(60, 0) });
            _actionBarProgressHeightValue = new Label { Text = "4", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            abPhRow.AddChild(_actionBarProgressHeightValue);
            labelCtrlGroup.AddChild(abPhRow);
            _actionBarProgressHeightSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 1, MaxValue = 20, Step = 1, Value = 4 , Scrollable = false };
            labelCtrlGroup.AddChild(_actionBarProgressHeightSlider);

            _actionBarForceShowCheck.Toggled += OnActionBarForceShowToggled;
            _actionBarTextYOffsetSlider.ValueChanged += OnActionBarTextYOffsetChanged;
            _actionBarTextYOffsetSlider.DragEnded += (changed) => Owner.PushCurrentStateToHistory();
            AttachValueLineEdit(_actionBarTextYOffsetSlider, _actionBarTextYOffsetValue);
            _actionBarProgressHeightSlider.ValueChanged += OnActionBarProgressHeightChanged;
            _actionBarProgressHeightSlider.DragEnded += (changed) => Owner.PushCurrentStateToHistory();
            AttachValueLineEdit(_actionBarProgressHeightSlider, _actionBarProgressHeightValue);

            // ========== 等级徽章控制 ==========
            labelCtrlGroup.AddChild(new HSeparator { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

            var lvTitleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            lvTitleRow.AddChild(new Label { Text = "等级", CustomMinimumSize = new Vector2(45, 0) });
            _levelBadgeVisibleCheck = new CheckButton { ButtonPressed = true };
            lvTitleRow.AddChild(_levelBadgeVisibleCheck);
            lvTitleRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            _levelBadgeTextColorBtn = new Button { Text = "色", CustomMinimumSize = new Vector2(30, 24) };
            _levelBadgeTextColorBtn.Modulate = Colors.Yellow;
            lvTitleRow.AddChild(_levelBadgeTextColorBtn);
            labelCtrlGroup.AddChild(lvTitleRow);

            // LevelBadge Text content
            var lvTxtRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            lvTxtRow.AddChild(new Label { Text = "内容", CustomMinimumSize = new Vector2(35, 0) });
            _levelBadgeTextEdit = new LineEdit { Text = "Lv.{level}", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(100, 0), PlaceholderText = "可用: {level} {name} {job}" };
            lvTxtRow.AddChild(_levelBadgeTextEdit);
            labelCtrlGroup.AddChild(lvTxtRow);

            // LevelBadge Font size
            var lvSzRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            lvSzRow.AddChild(new Label { Text = "字号", CustomMinimumSize = new Vector2(35, 0) });
            _levelBadgeFontSizeValue = new Label { Text = "12", CustomMinimumSize = new Vector2(25, 0), HorizontalAlignment = HorizontalAlignment.Right };
            lvSzRow.AddChild(_levelBadgeFontSizeValue);
            labelCtrlGroup.AddChild(lvSzRow);
            _levelBadgeFontSizeSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 6, MaxValue = 24, Step = 1, Value = 12 , Scrollable = false };
            labelCtrlGroup.AddChild(_levelBadgeFontSizeSlider);

            // LevelBadge Offset X
            var lvOxRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            lvOxRow.AddChild(new Label { Text = "X偏移", CustomMinimumSize = new Vector2(45, 0) });
            _levelBadgeOffsetXValue = new Label { Text = "-35", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            lvOxRow.AddChild(_levelBadgeOffsetXValue);
            labelCtrlGroup.AddChild(lvOxRow);
            _levelBadgeOffsetXSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = -150, MaxValue = 150, Step = 1, Value = -35 , Scrollable = false };
            labelCtrlGroup.AddChild(_levelBadgeOffsetXSlider);

            // LevelBadge Offset Y
            var lvOyRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            lvOyRow.AddChild(new Label { Text = "Y偏移", CustomMinimumSize = new Vector2(45, 0) });
            _levelBadgeOffsetYValue = new Label { Text = "-35", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            lvOyRow.AddChild(_levelBadgeOffsetYValue);
            labelCtrlGroup.AddChild(lvOyRow);
            _levelBadgeOffsetYSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = -150, MaxValue = 150, Step = 1, Value = -35 , Scrollable = false };
            labelCtrlGroup.AddChild(_levelBadgeOffsetYSlider);

            // LevelBadge signals
            _levelBadgeVisibleCheck.Toggled += OnLevelBadgeVisibleToggled;
            _levelBadgeTextColorBtn.Pressed += OnLevelBadgeTextColorPressed;
            _levelBadgeTextEdit.TextChanged += OnLevelBadgeTextChanged;
            _levelBadgeFontSizeSlider.ValueChanged += OnLevelBadgeFontSizeChanged;
            _levelBadgeFontSizeSlider.DragEnded += (changed) => Owner.PushCurrentStateToHistory();
            AttachValueLineEdit(_levelBadgeFontSizeSlider, _levelBadgeFontSizeValue);
            _levelBadgeOffsetXSlider.ValueChanged += OnLevelBadgeOffsetXChanged;
            _levelBadgeOffsetXSlider.DragEnded += (changed) => Owner.PushCurrentStateToHistory();
            AttachValueLineEdit(_levelBadgeOffsetXSlider, _levelBadgeOffsetXValue);
            _levelBadgeOffsetYSlider.ValueChanged += OnLevelBadgeOffsetYChanged;
            _levelBadgeOffsetYSlider.DragEnded += (changed) => Owner.PushCurrentStateToHistory();
            AttachValueLineEdit(_levelBadgeOffsetYSlider, _levelBadgeOffsetYValue);
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  ConnectSignals / DisconnectSignals
        // ═══════════════════════════════════════════════════════════════════════

        public override void ConnectSignals()
        {
            if (_playerSizeSlider != null)
            {
                _playerSizeSlider.ValueChanged += OnPlayerSizeChanged;
                _playerSizeSlider.DragEnded += OnPlayerSizeDragEnded;
            }
            if (_playerSizeScaleSlider != null)
            {
                _playerSizeScaleSlider.ValueChanged += OnPlayerSizeScaleChanged;
                _playerSizeScaleSlider.DragEnded += OnPlayerSizeScaleDragEnded;
            }
            if (_borderWidthSlider != null)
            {
                _borderWidthSlider.ValueChanged += OnBorderWidthChanged;
                _borderWidthSlider.DragEnded += OnBorderWidthDragEnded;
            }
            if (_borderWidthScaleSlider != null)
            {
                _borderWidthScaleSlider.ValueChanged += OnBorderWidthScaleChanged;
                _borderWidthScaleSlider.DragEnded += OnBorderWidthScaleDragEnded;
            }
            if (_cornerRadiusSlider != null)
            {
                _cornerRadiusSlider.ValueChanged += OnCornerRadiusChanged;
                _cornerRadiusSlider.DragEnded += OnCornerRadiusDragEnded;
            }
            if (_bgOpacitySlider != null)
            {
                _bgOpacitySlider.ValueChanged += OnBgOpacityChanged;
                _bgOpacitySlider.DragEnded += OnBgOpacityDragEnded;
            }

            if (_fontOption != null)
                _fontOption.ItemSelected += OnFontSelected;
            if (_fontSizeSlider != null)
            {
                _fontSizeSlider.ValueChanged += OnFontSizeChanged;
                _fontSizeSlider.DragEnded += OnFontSizeDragEnded;
            }
            if (_lineSpacingSlider != null)
            {
                _lineSpacingSlider.ValueChanged += OnLineSpacingChanged;
                _lineSpacingSlider.DragEnded += OnLineSpacingDragEnded;
            }
            if (_letterSpacingSlider != null)
            {
                _letterSpacingSlider.ValueChanged += OnLetterSpacingChanged;
                _letterSpacingSlider.DragEnded += OnLetterSpacingDragEnded;
            }
            if (_boldCheck != null)
                _boldCheck.Toggled += OnBoldToggled;
            if (_italicCheck != null)
                _italicCheck.Toggled += OnItalicToggled;
            if (_shadowCheck != null)
                _shadowCheck.Toggled += OnShadowToggled;
            if (_labelAutoCenterXCheck != null)
                _labelAutoCenterXCheck.Toggled += OnLabelAutoCenterXToggled;

            if (_fontAutoSizeCheck != null)
                _fontAutoSizeCheck.Toggled += OnFontAutoSizeToggled;

            if (_alignLeftBtn != null)
                _alignLeftBtn.Pressed += () => OnTextAlignChanged(HorizontalAlignment.Left);
            if (_alignCenterBtn != null)
                _alignCenterBtn.Pressed += () => OnTextAlignChanged(HorizontalAlignment.Center);
            if (_alignRightBtn != null)
                _alignRightBtn.Pressed += () => OnTextAlignChanged(HorizontalAlignment.Right);
            if (_loadFontBtn != null)
                _loadFontBtn.Pressed += OnLoadFontPressed;

            if (_fontFileDialog != null)
                _fontFileDialog.FileSelected += OnFontFileSelected;

            SetupFontOptions();
            SetupLineColorButtons();
        }

        public override void DisconnectSignals()
        {
            if (_playerSizeSlider != null)
            {
                _playerSizeSlider.ValueChanged -= OnPlayerSizeChanged;
                _playerSizeSlider.DragEnded -= OnPlayerSizeDragEnded;
            }
            if (_playerSizeScaleSlider != null)
            {
                _playerSizeScaleSlider.ValueChanged -= OnPlayerSizeScaleChanged;
                _playerSizeScaleSlider.DragEnded -= OnPlayerSizeScaleDragEnded;
            }
            if (_borderWidthSlider != null)
            {
                _borderWidthSlider.ValueChanged -= OnBorderWidthChanged;
                _borderWidthSlider.DragEnded -= OnBorderWidthDragEnded;
            }
            if (_borderWidthScaleSlider != null)
            {
                _borderWidthScaleSlider.ValueChanged -= OnBorderWidthScaleChanged;
                _borderWidthScaleSlider.DragEnded -= OnBorderWidthScaleDragEnded;
            }
            if (_cornerRadiusSlider != null)
            {
                _cornerRadiusSlider.ValueChanged -= OnCornerRadiusChanged;
                _cornerRadiusSlider.DragEnded -= OnCornerRadiusDragEnded;
            }
            if (_bgOpacitySlider != null)
            {
                _bgOpacitySlider.ValueChanged -= OnBgOpacityChanged;
                _bgOpacitySlider.DragEnded -= OnBgOpacityDragEnded;
            }

            if (_fontOption != null)
                _fontOption.ItemSelected -= OnFontSelected;
            if (_fontSizeSlider != null)
            {
                _fontSizeSlider.ValueChanged -= OnFontSizeChanged;
                _fontSizeSlider.DragEnded -= OnFontSizeDragEnded;
            }
            if (_lineSpacingSlider != null)
            {
                _lineSpacingSlider.ValueChanged -= OnLineSpacingChanged;
                _lineSpacingSlider.DragEnded -= OnLineSpacingDragEnded;
            }
            if (_letterSpacingSlider != null)
            {
                _letterSpacingSlider.ValueChanged -= OnLetterSpacingChanged;
                _letterSpacingSlider.DragEnded -= OnLetterSpacingDragEnded;
            }
            if (_boldCheck != null)
                _boldCheck.Toggled -= OnBoldToggled;
            if (_italicCheck != null)
                _italicCheck.Toggled -= OnItalicToggled;
            if (_shadowCheck != null)
                _shadowCheck.Toggled -= OnShadowToggled;
            if (_labelAutoCenterXCheck != null)
                _labelAutoCenterXCheck.Toggled -= OnLabelAutoCenterXToggled;

            if (_fontAutoSizeCheck != null)
                _fontAutoSizeCheck.Toggled -= OnFontAutoSizeToggled;

            if (_fontFileDialog != null)
                _fontFileDialog.FileSelected -= OnFontFileSelected;
        }

        private void SetupFontOptions()
        {
            if (_fontOption == null) return;
            _fontOption.Clear();
            foreach (var font in DebugPanel.FONTS)
                _fontOption.AddItem(font.name);
            _fontOption.AddItem("自定义...");
        }

        private void SetupLineColorButtons()
        {
            if (_lineColorButtons == null || _lineColorButtons.Count == 0) return;
            for (int i = 0; i < _lineColorButtons.Count; i++)
            {
                int index = i;
                Button btn = _lineColorButtons[i];
                if (btn != null)
                {
                    btn.Modulate = DebugPanel.COLOR_PRESETS[0];
                    btn.Pressed += () => OnLineColorButtonPressed(index);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Handlers — Player Basic Appearance
        // ═══════════════════════════════════════════════════════════════════════

        private void OnPlayerSizeChanged(double value)
        {
            _playerSizeValue.Text = ((int)value).ToString();
        }

        private void OnPlayerSizeDragEnded(bool valueChanged)
        {
            if (Player != null)
            {
                int gridSize = (int)Owner._gridSizeSlider.Value;
                float scale = gridSize > 0 ? (float)(_playerSizeSlider.Value / gridSize) : 1.0f;
                Player.SetVisualSizeScale(scale);
                if (_playerSizeScaleSlider != null)
                {
                    _playerSizeScaleSlider.SetBlockSignals(true);
                    _playerSizeScaleSlider.Value = scale;
                    _playerSizeScaleSlider.SetBlockSignals(false);
                    _playerSizeScaleValue.Text = scale.ToString(DebugPanelLengthScalePolicy.FormatStr);
                }
            }
            Owner.PushCurrentStateToHistory();
        }

        private void OnPlayerSizeScaleChanged(double value)
        {
            if (_playerSizeScaleValue != null)
                _playerSizeScaleValue.Text = value.ToString(DebugPanelLengthScalePolicy.FormatStr);
        }

        private void OnPlayerSizeScaleDragEnded(bool valueChanged)
        {
            if (Player != null)
            {
                Player.SetVisualSizeScale((float)_playerSizeScaleSlider.Value);
                int newSize = Player.VisualSize;
                if (_playerSizeSlider != null)
                {
                    _playerSizeSlider.SetBlockSignals(true);
                    _playerSizeSlider.Value = newSize;
                    _playerSizeSlider.SetBlockSignals(false);
                    _playerSizeValue.Text = newSize.ToString();
                }
            }
            Owner.PushCurrentStateToHistory();
        }

        private void OnBorderWidthChanged(double value)
        {
            _borderWidthValue.Text = ((int)value).ToString();
        }

        private void OnBorderWidthDragEnded(bool valueChanged)
        {
            if (Player != null)
            {
                int gridSize = (int)Owner._gridSizeSlider.Value;
                float scale = gridSize > 0 ? (float)(_borderWidthSlider.Value / gridSize) : 0.0f;
                Player.SetBorderWidthScale(scale);
                if (_borderWidthScaleSlider != null)
                {
                    _borderWidthScaleSlider.SetBlockSignals(true);
                    _borderWidthScaleSlider.Value = scale;
                    _borderWidthScaleSlider.SetBlockSignals(false);
                    _borderWidthScaleValue.Text = scale.ToString(DebugPanelLengthScalePolicy.FormatStr);
                }
            }
            Owner.PushCurrentStateToHistory();
        }

        private void OnBorderWidthScaleChanged(double value)
        {
            if (_borderWidthScaleValue != null)
                _borderWidthScaleValue.Text = value.ToString(DebugPanelLengthScalePolicy.FormatStr);
        }

        private void OnBorderWidthScaleDragEnded(bool valueChanged)
        {
            if (Player != null)
            {
                Player.SetBorderWidthScale((float)_borderWidthScaleSlider.Value);
                float newWidth = Player.BorderWidth;
                if (_borderWidthSlider != null)
                {
                    _borderWidthSlider.SetBlockSignals(true);
                    _borderWidthSlider.Value = newWidth;
                    _borderWidthSlider.SetBlockSignals(false);
                    _borderWidthValue.Text = ((int)newWidth).ToString();
                }
            }
            Owner.PushCurrentStateToHistory();
        }

        private void OnCornerRadiusChanged(double value)
        {
            _cornerRadiusValue.Text = ((int)value).ToString();
        }

        private void OnCornerRadiusDragEnded(bool valueChanged)
        {
            if (Player != null)
            {
                Player.SetCornerRadius((float)_cornerRadiusSlider.Value);
            }
            Owner.PushCurrentStateToHistory();
        }

        private void OnBgOpacityChanged(double value)
        {
            _bgOpacityValue.Text = $"{value:F2}";
        }

        private void OnBgOpacityDragEnded(bool valueChanged)
        {
            if (Player != null)
            {
                Player.SetBgOpacity((float)_bgOpacitySlider.Value);
                Player.QueueRedraw();
            }
            Owner.PushCurrentStateToHistory();
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Handlers — Text Style
        // ═══════════════════════════════════════════════════════════════════════

        private void OnFontSelected(long index)
        {
            if (index < DebugPanel.FONTS.Length)
            {
                if (Player != null)
                {
                    Player.SetFont(DebugPanel.FONTS[index].path);
                    Player.RefreshLabels();
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
            if (Player != null)
            {
                Player.SetFont(path);
                Player.RefreshLabels();
                GD.Print($"[DebugPanelPlayerTab] Loaded custom font: {path}");
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
            if (Player != null)
            {
                if (_fontAutoSizeCheck != null && _fontAutoSizeCheck.ButtonPressed)
                {
                    Player.SetFontSize(0);
                }
                else
                {
                    Player.SetFontSize((int)_fontSizeSlider.Value);
                    Player.RefreshLabels();
                }
            }
            Owner.PushCurrentStateToHistory();
            Player?.RefreshLabels();
        }

        private void OnLineSpacingChanged(double value)
        {
            _lineSpacingValue.Text = $"{value:F1}";
        }

        private void OnLineSpacingDragEnded(bool valueChanged)
        {
            if (Player != null)
            {
                Player.SetLineSpacing((float)_lineSpacingSlider.Value);
                Player.RefreshLabels();
            }
            Owner.PushCurrentStateToHistory();
        }

        private void OnLetterSpacingChanged(double value)
        {
            _letterSpacingValue.Text = ((int)value).ToString();
        }

        private void OnLetterSpacingDragEnded(bool valueChanged)
        {
            if (Player != null)
            {
                Player.SetLetterSpacing((float)_letterSpacingSlider.Value);
                Player.RefreshLabels();
            }
            Owner.PushCurrentStateToHistory();
        }

        private void OnTextAlignChanged(HorizontalAlignment alignment)
        {
            if (Player != null)
            {
                Player.SetTextAlignment(alignment);
                Player.RefreshLabels();
            }
            Owner.PushCurrentStateToHistory();
        }

        private void OnLineColorButtonPressed(int lineIndex)
        {
            Godot.Collections.Array<Color> lineColors = Player.LineColors;
            Color currentColor = lineIndex < lineColors.Count ? lineColors[lineIndex] : Colors.Black;
            int currentIdx = System.Array.IndexOf(DebugPanel.COLOR_PRESETS, currentColor);
            int nextIndex = (currentIdx + 1) % DebugPanel.COLOR_PRESETS.Length;

            lineColors[lineIndex] = DebugPanel.COLOR_PRESETS[nextIndex];
            Player.LineColors = lineColors;
            _lineColorButtons[lineIndex].Modulate = DebugPanel.COLOR_PRESETS[nextIndex];
            Player.RefreshLabels();
            Owner.PushCurrentStateToHistory();
        }

        private void OnBoldToggled(bool enabled)
        {
            if (Player != null)
            {
                Player.SetFontBold(enabled);
                Player.RefreshLabels();
            }
            Owner.PushCurrentStateToHistory();
        }

        private void OnItalicToggled(bool enabled)
        {
            if (Player != null)
            {
                Player.SetFontItalic(enabled);
                Player.RefreshLabels();
            }
            Owner.PushCurrentStateToHistory();
        }

        private void OnShadowToggled(bool enabled)
        {
            if (Player != null)
            {
                Player.SetFontShadow(enabled);
                Player.RefreshLabels();
            }
            Owner.PushCurrentStateToHistory();
        }

        private void OnFontAutoSizeToggled(bool enabled)
        {
            if (enabled)
            {
                if (Player != null)
                {
                    Player.SetFontSize(0);
                    Player.RefreshLabels();
                }
            }
            else
            {
                if (Player != null)
                {
                    Player.SetFontSize((int)_fontSizeSlider.Value);
                    Player.RefreshLabels();
                }
            }
            Owner.UpdateControlStates();
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Handlers — Label Controls
        // ═══════════════════════════════════════════════════════════════════════

        private void OnLabelVisibleToggled(int index, bool enabled)
        {
            if (Player != null)
                Player.SetLabelVisible(index, enabled);
            Owner.PushCurrentStateToHistory();
        }

        private void OnLabelNameChanged(int index, string newName)
        {
            if (Player != null)
                Player.SetLabelName(index, newName);
        }

        private void OnLabelTextChanged(int index, string newText)
        {
            if (Player != null)
                Player.SetLabelText(index, newText);
        }

        private void OnLabelColorPressed(int index)
        {
            if (Player == null) return;
            var lineColors = Player.LineColors;
            Color currentColor = index < lineColors.Count ? lineColors[index] : Colors.Black;
            int currentIdx = System.Array.IndexOf(DebugPanel.COLOR_PRESETS, currentColor);
            int nextIndex = (currentIdx + 1) % DebugPanel.COLOR_PRESETS.Length;

            Player.SetLineColor(index, DebugPanel.COLOR_PRESETS[nextIndex]);
            _labelColorButtons[index].Modulate = DebugPanel.COLOR_PRESETS[nextIndex];
            Player.RefreshLabels();
            Owner.PushCurrentStateToHistory();
        }

        private void OnLabelResetPressed(int index)
        {
            if (Player == null) return;
            Player.ResetLabelOffset(index);
            SyncLabelOffsetSlidersFromPlayer();
            Owner.PushCurrentStateToHistory();
        }

        private void OnLabelFontSizeChanged(int index, double value)
        {
            _labelFontSizeValues[index].Text = value > 0 ? ((int)value).ToString() : "自动";
        }

        private void OnLabelFontSizeDragEnded(int index, bool valueChanged)
        {
            if (Player != null)
            {
                Player.SetLabelFontSize(index, (int)_labelFontSizeSliders[index].Value);
                Player.RefreshLabels();
            }
            Owner.PushCurrentStateToHistory();
        }

        private void OnLabelOffsetXChanged(int index, double value)
        {
            _labelOffsetXValues[index].Text = ((int)value).ToString();
            if (Player != null)
            {
                var currentOffset = Player.GetLabelOffset(index);
                Player.SetLabelOffset(index, new Vector2((float)value, currentOffset.Y));
            }
        }

        private void OnLabelOffsetYChanged(int index, double value)
        {
            _labelOffsetYValues[index].Text = ((int)value).ToString();
            if (Player != null)
            {
                var currentOffset = Player.GetLabelOffset(index);
                Player.SetLabelOffset(index, new Vector2(currentOffset.X, (float)value));
            }
        }

        private void OnLabelOffsetDragEnded(int index, bool valueChanged)
        {
            Owner.PushCurrentStateToHistory();
        }

        /// <summary>
        /// Called when GridSize changes — the player's scale-dependent values
        /// are automatically recalculated by SetGridSize, so we must sync
        /// the size/border/healthbar sliders and their scale counterparts.
        /// </summary>
        public void SyncSizeSlidersFromPlayer()
        {
            if (Player == null) return;

            // Player size + scale
            if (_playerSizeSlider != null)
            {
                _playerSizeSlider.SetBlockSignals(true);
                _playerSizeSlider.Value = Player.VisualSize;
                _playerSizeSlider.SetBlockSignals(false);
                _playerSizeValue.Text = Player.VisualSize.ToString();
            }
            if (_playerSizeScaleSlider != null)
            {
                _playerSizeScaleSlider.SetBlockSignals(true);
                _playerSizeScaleSlider.Value = Player.VisualSizeScale;
                _playerSizeScaleSlider.SetBlockSignals(false);
                _playerSizeScaleValue.Text = Player.VisualSizeScale.ToString(DebugPanelLengthScalePolicy.FormatStr);
            }

            // Border width + scale
            if (_borderWidthSlider != null)
            {
                _borderWidthSlider.SetBlockSignals(true);
                _borderWidthSlider.Value = Player.BorderWidth;
                _borderWidthSlider.SetBlockSignals(false);
                _borderWidthValue.Text = ((int)Player.BorderWidth).ToString();
            }
            if (_borderWidthScaleSlider != null)
            {
                _borderWidthScaleSlider.SetBlockSignals(true);
                _borderWidthScaleSlider.Value = Player.BorderWidthScale;
                _borderWidthScaleSlider.SetBlockSignals(false);
                _borderWidthScaleValue.Text = Player.BorderWidthScale.ToString(DebugPanelLengthScalePolicy.FormatStr);
            }

            // Health bar length + scale
            if (_healthBarLengthSlider != null)
            {
                _healthBarLengthSlider.SetBlockSignals(true);
                _healthBarLengthSlider.Value = Player.HealthBarLength;
                _healthBarLengthSlider.SetBlockSignals(false);
                _healthBarLengthValue.Text = ((int)Player.HealthBarLength).ToString();
            }
            if (_healthBarLengthScaleSlider != null)
            {
                _healthBarLengthScaleSlider.SetBlockSignals(true);
                _healthBarLengthScaleSlider.Value = Player.HealthBarLengthScale;
                _healthBarLengthScaleSlider.SetBlockSignals(false);
                _healthBarLengthScaleValue.Text = Player.HealthBarLengthScale.ToString(DebugPanelLengthScalePolicy.FormatStr);
            }

            // Health bar height + scale
            if (_healthBarHeightSlider != null)
            {
                _healthBarHeightSlider.SetBlockSignals(true);
                _healthBarHeightSlider.Value = Player.HealthBarHeight;
                _healthBarHeightSlider.SetBlockSignals(false);
                _healthBarHeightValue.Text = ((int)Player.HealthBarHeight).ToString();
            }
            if (_healthBarHeightScaleSlider != null)
            {
                _healthBarHeightScaleSlider.SetBlockSignals(true);
                _healthBarHeightScaleSlider.Value = Player.HealthBarHeightScale;
                _healthBarHeightScaleSlider.SetBlockSignals(false);
                _healthBarHeightScaleValue.Text = Player.HealthBarHeightScale.ToString(DebugPanelLengthScalePolicy.FormatStr);
            }
        }

        public void SyncLabelOffsetSlidersFromPlayer()
        {
            if (Player == null) return;
            bool autoCenter = Player.LabelAutoCenterX;
            if (_labelAutoCenterXCheck != null)
            {
                _labelAutoCenterXCheck.SetBlockSignals(true);
                _labelAutoCenterXCheck.ButtonPressed = autoCenter;
                _labelAutoCenterXCheck.SetBlockSignals(false);
            }
            for (int i = 0; i < LabelCount; i++)
            {
                var offset = Player.GetLabelOffset(i);
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
            if (Player != null)
            {
                Player.SetLabelAutoCenterX(enabled);
            }
            SyncLabelOffsetSlidersFromPlayer();
            Owner.PushCurrentStateToHistory();
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Handlers — Health Bar
        // ═══════════════════════════════════════════════════════════════════════

        private void OnHealthBarVisibleToggled(bool enabled)
        {
            if (Player != null)
                Player.SetHealthBarVisible(enabled);
            Owner.PushCurrentStateToHistory();
        }

        private void OnHealthBarColorPressed()
        {
            if (Player == null) return;
            var currentColor = Player.HealthBarColor;
            Color[] hpColors = new Color[] { new Color(0, 0.8f, 0, 1), Colors.Red, Colors.Yellow, Colors.Cyan, Colors.White };
            int idx = 0;
            for (int i = 0; i < hpColors.Length; i++)
            {
                if (currentColor.IsEqualApprox(hpColors[i])) { idx = i; break; }
            }
            int nextIdx = (idx + 1) % hpColors.Length;
            Player.SetHealthBarColor(hpColors[nextIdx]);
            _healthBarColorBtn.Modulate = hpColors[nextIdx];
            Owner.PushCurrentStateToHistory();
        }

        private void OnHealthBarLengthChanged(double value)
        {
            _healthBarLengthValue.Text = ((int)value).ToString();
            int gridSize = (int)Owner._gridSizeSlider.Value;
            if (Player != null)
            {
                float scale = gridSize > 0 ? (float)(value / gridSize) : 0.0f;
                Player.SetHealthBarLengthScale(scale);
                if (_healthBarLengthScaleSlider != null)
                {
                    _healthBarLengthScaleSlider.SetBlockSignals(true);
                    _healthBarLengthScaleSlider.Value = scale;
                    _healthBarLengthScaleSlider.SetBlockSignals(false);
                    _healthBarLengthScaleValue.Text = scale.ToString(DebugPanelLengthScalePolicy.FormatStr);
                }
            }
        }

        private void OnHealthBarLengthScaleChanged(double value)
        {
            _healthBarLengthScaleValue.Text = value.ToString(DebugPanelLengthScalePolicy.FormatStr);
            if (Player != null)
            {
                Player.SetHealthBarLengthScale((float)value);
                float newLength = Player.HealthBarLength;
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
            int gridSize = (int)Owner._gridSizeSlider.Value;
            if (Player != null)
            {
                float scale = gridSize > 0 ? (float)(value / gridSize) : 0.0f;
                Player.SetHealthBarHeightScale(scale);
                if (_healthBarHeightScaleSlider != null)
                {
                    _healthBarHeightScaleSlider.SetBlockSignals(true);
                    _healthBarHeightScaleSlider.Value = scale;
                    _healthBarHeightScaleSlider.SetBlockSignals(false);
                    _healthBarHeightScaleValue.Text = scale.ToString(DebugPanelLengthScalePolicy.FormatStr);
                }
            }
        }

        private void OnHealthBarHeightScaleChanged(double value)
        {
            _healthBarHeightScaleValue.Text = value.ToString(DebugPanelLengthScalePolicy.FormatStr);
            if (Player != null)
            {
                Player.SetHealthBarHeightScale((float)value);
                float newHeight = Player.HealthBarHeight;
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
            if (Player != null)
                Player.SetHealthBarFillPercent((float)(value / 100.0));
        }

        private void OnHealthBarOffsetXChanged(double value)
        {
            _healthBarOffsetXValue.Text = ((int)value).ToString();
            if (Player != null)
            {
                var offset = Player.GetHealthBarOffset();
                Player.SetHealthBarOffset(new Vector2((float)value, offset.Y));
            }
            {
                var offset = Player.GetHealthBarOffset();
            }
        }

        private void OnHealthBarOffsetYChanged(double value)
        {
            _healthBarOffsetYValue.Text = ((int)value).ToString();
            if (Player != null)
            {
                var offset = Player.GetHealthBarOffset();
                Player.SetHealthBarOffset(new Vector2(offset.X, (float)value));
            }
            {
                var offset = Player.GetHealthBarOffset();
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Handlers — MP Bar
        // ═══════════════════════════════════════════════════════════════════════

        private static readonly Color[] MpColors = {
            new Color(0.2f, 0.4f, 1.0f, 1),  // 蓝
            new Color(0.6f, 0.2f, 1.0f, 1),  // 紫
            new Color(0.2f, 0.8f, 0.8f, 1),  // 青
        };

        private void OnMpBarVisibleToggled(bool enabled)
        {
            if (Player != null) Player.SetMpBarVisible(enabled);
        }

        private void OnMpBarColorPressed()
        {
            var currentColor = Player.MpBarColor;
            int nextIdx = 0;
            for (int i = 0; i < MpColors.Length; i++)
            {
                if (MpColors[i].IsEqualApprox(currentColor))
                { nextIdx = (i + 1) % MpColors.Length; break; }
            }
            Player.SetMpBarColor(MpColors[nextIdx]);
            _mpBarColorBtn.Modulate = MpColors[nextIdx];
        }

        private void OnMpBarLengthChanged(double value)
        {
            _mpBarLengthValue.Text = ((int)value).ToString();
            if (Player != null)
            {
                float scale = Player.GridSize > 0 ? (float)(value / Player.GridSize) : 0;
                Player.SetMpBarLengthScale(scale);
                if (_mpBarLengthScaleSlider != null)
                {
                    _mpBarLengthScaleSlider.SetBlockSignals(true);
                    _mpBarLengthScaleSlider.Value = scale;
                    _mpBarLengthScaleSlider.SetBlockSignals(false);
                    _mpBarLengthScaleValue.Text = scale.ToString(DebugPanelLengthScalePolicy.FormatStr);
                }
            }
        }

        private void OnMpBarLengthScaleChanged(double value)
        {
            _mpBarLengthScaleValue.Text = value.ToString(DebugPanelLengthScalePolicy.FormatStr);
            if (Player != null)
            {
                Player.SetMpBarLengthScale((float)value);
                float newLength = Player.MpBarLength;
                if (_mpBarLengthSlider != null)
                {
                    _mpBarLengthSlider.SetBlockSignals(true);
                    _mpBarLengthSlider.Value = newLength;
                    _mpBarLengthSlider.SetBlockSignals(false);
                    _mpBarLengthValue.Text = ((int)newLength).ToString();
                }
            }
        }

        private void OnMpBarHeightChanged(double value)
        {
            _mpBarHeightValue.Text = ((int)value).ToString();
            int gridSize = (int)Owner._gridSizeSlider.Value;
            if (Player != null)
            {
                float scale = gridSize > 0 ? (float)(value / gridSize) : 0.0f;
                Player.SetMpBarHeightScale(scale);
                if (_mpBarHeightScaleSlider != null)
                {
                    _mpBarHeightScaleSlider.SetBlockSignals(true);
                    _mpBarHeightScaleSlider.Value = scale;
                    _mpBarHeightScaleSlider.SetBlockSignals(false);
                    _mpBarHeightScaleValue.Text = scale.ToString(DebugPanelLengthScalePolicy.FormatStr);
                }
            }
        }

        private void OnMpBarHeightScaleChanged(double value)
        {
            _mpBarHeightScaleValue.Text = value.ToString(DebugPanelLengthScalePolicy.FormatStr);
            if (Player != null)
            {
                Player.SetMpBarHeightScale((float)value);
                float newHeight = Player.MpBarHeight;
                if (_mpBarHeightSlider != null)
                {
                    _mpBarHeightSlider.SetBlockSignals(true);
                    _mpBarHeightSlider.Value = newHeight;
                    _mpBarHeightSlider.SetBlockSignals(false);
                    _mpBarHeightValue.Text = ((int)newHeight).ToString();
                }
            }
        }

        private void OnMpBarFillChanged(double value)
        {
            _mpBarFillValue.Text = $"{(int)value}%";
            if (Player != null)
                Player.SetMpBarFillPercent((float)(value / 100.0));
        }

        private void OnMpBarOffsetXChanged(double value)
        {
            _mpBarOffsetXValue.Text = ((int)value).ToString();
            if (Player != null)
            {
                var offset = Player.GetMpBarOffset();
                Player.SetMpBarOffset(new Vector2((float)value, offset.Y));
            }
            {
                var offset = Player.GetMpBarOffset();
            }
        }

        private void OnMpBarOffsetYChanged(double value)
        {
            _mpBarOffsetYValue.Text = ((int)value).ToString();
            if (Player != null)
            {
                var offset = Player.GetMpBarOffset();
                Player.SetMpBarOffset(new Vector2(offset.X, (float)value));
            }
            {
                var offset = Player.GetMpBarOffset();
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Handlers — Cast Bar
        // ═══════════════════════════════════════════════════════════════════════

        private void OnCastBarVisibleToggled(bool enabled)
        {
            if (Player != null)
                Player.SetCastBarVisible(enabled);
            Owner.PushCurrentStateToHistory();
        }

        private void OnCastBarColorPressed()
        {
            if (Player == null) return;
            var currentColor = Player.CastBarColor;
            Color[] ctColors = new Color[] { new Color(0.3f, 0.5f, 1, 1), new Color(1, 0.5f, 0, 1), new Color(0.8f, 0.2f, 1, 1), Colors.White };
            int idx = 0;
            for (int i = 0; i < ctColors.Length; i++)
            {
                if (currentColor.IsEqualApprox(ctColors[i])) { idx = i; break; }
            }
            int nextIdx = (idx + 1) % ctColors.Length;
            Player.SetCastBarColor(ctColors[nextIdx]);
            _castBarColorBtn.Modulate = ctColors[nextIdx];
            Owner.PushCurrentStateToHistory();
        }

        private void OnCastBarLengthChanged(double value)
        {
            _castBarLengthValue.Text = ((int)value).ToString();
            if (Player != null)
            {
                float scale = Player.GridSize > 0 ? (float)(value / Player.GridSize) : 0;
                Player.SetCastBarLengthScale(scale);
            }
        }

        private void OnCastBarHeightChanged(double value)
        {
            _castBarHeightValue.Text = ((int)value).ToString();
            if (Player != null)
            {
                float scale = Player.GridSize > 0 ? (float)(value / Player.GridSize) : 0;
                Player.SetCastBarHeightScale(scale);
            }
        }

        private void OnCastBarFillChanged(double value)
        {
            _castBarFillValue.Text = $"{(int)value}%";
            if (Player != null) Player.SetCastBarFillPercent((float)(value / 100.0));
        }

        private void OnCastBarOffsetXChanged(double value)
        {
            _castBarOffsetXValue.Text = ((int)value).ToString();
            if (Player != null)
            {
                var offset = Player.GetCastBarOffset();
                Player.SetCastBarOffset(new Vector2((float)value, offset.Y));
            }
        }

        private void OnCastBarOffsetYChanged(double value)
        {
            _castBarOffsetYValue.Text = ((int)value).ToString();
            if (Player != null)
            {
                var offset = Player.GetCastBarOffset();
                Player.SetCastBarOffset(new Vector2(offset.X, (float)value));
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Handlers — Action Bar
        // ═══════════════════════════════════════════════════════════════════════

        private void OnActionBarForceShowToggled(bool enabled)
        {
            if (Player != null)
            {
                Player.ActionBarForceShow = enabled;
                Player.QueueRedraw();
            }
        }

        private void OnActionBarTextYOffsetChanged(double value)
        {
            _actionBarTextYOffsetValue.Text = ((int)value).ToString();
            if (Player != null) Player.SetActionBarTextYOffset((float)value);
        }

        private void OnActionBarProgressHeightChanged(double value)
        {
            _actionBarProgressHeightValue.Text = ((int)value).ToString();
            if (Player != null) Player.SetActionBarProgressHeight((float)value);
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Handlers — Level Badge
        // ═══════════════════════════════════════════════════════════════════════

        private void OnLevelBadgeVisibleToggled(bool enabled)
        {
            if (Player != null) Player.SetLevelBadgeVisible(enabled);
            Owner.PushCurrentStateToHistory();
        }

        private void OnLevelBadgeTextColorPressed()
        {
            if (Player == null) return;
            var current = Player.LevelBadgeTextColor;
            Color[] colors = new Color[] { Colors.Yellow, Colors.White, Colors.Cyan, new Color(1, 0.5f, 0, 1), Colors.Black };
            int idx = 0;
            for (int i = 0; i < colors.Length; i++) { if (current.IsEqualApprox(colors[i])) { idx = i; break; } }
            int next = (idx + 1) % colors.Length;
            Player.SetLevelBadgeTextColor(colors[next]);
            _levelBadgeTextColorBtn.Modulate = colors[next];
            Owner.PushCurrentStateToHistory();
        }

        private void OnLevelBadgeTextChanged(string newText)
        {
            if (Player != null) Player.SetLevelBadgeText(newText);
        }

        private void OnLevelBadgeFontSizeChanged(double value)
        {
            _levelBadgeFontSizeValue.Text = ((int)value).ToString();
            if (Player != null) Player.SetLevelBadgeFontSize((float)value);
        }

        private void OnLevelBadgeOffsetXChanged(double value)
        {
            _levelBadgeOffsetXValue.Text = ((int)value).ToString();
            if (Player != null)
            {
                var offset = Player.GetLevelBadgeOffset();
                Player.SetLevelBadgeOffset(new Vector2((float)value, offset.Y));
            }
        }

        private void OnLevelBadgeOffsetYChanged(double value)
        {
            _levelBadgeOffsetYValue.Text = ((int)value).ToString();
            if (Player != null)
            {
                var offset = Player.GetLevelBadgeOffset();
                Player.SetLevelBadgeOffset(new Vector2(offset.X, (float)value));
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  SyncToCurrentValues
        // ═══════════════════════════════════════════════════════════════════════

        public override void SyncToCurrentValues()
        {
            if (Player == null) return;

            _playerSizeSlider.SetBlockSignals(true);
            _playerSizeSlider.Value = Player.VisualSize;
            _playerSizeSlider.SetBlockSignals(false);
            _playerSizeValue.Text = Player.VisualSize.ToString();

            _playerSizeScaleSlider.SetBlockSignals(true);
            _playerSizeScaleSlider.Value = Player.VisualSizeScale;
            _playerSizeScaleSlider.SetBlockSignals(false);
            _playerSizeScaleValue.Text = Player.VisualSizeScale.ToString(DebugPanelLengthScalePolicy.FormatStr);

            _borderWidthSlider.SetBlockSignals(true);
            _borderWidthSlider.Value = Player.BorderWidth;
            _borderWidthSlider.SetBlockSignals(false);
            _borderWidthValue.Text = ((int)Player.BorderWidth).ToString();

            _borderWidthScaleSlider.SetBlockSignals(true);
            _borderWidthScaleSlider.Value = Player.BorderWidthScale;
            _borderWidthScaleSlider.SetBlockSignals(false);
            _borderWidthScaleValue.Text = Player.BorderWidthScale.ToString(DebugPanelLengthScalePolicy.FormatStr);

            _cornerRadiusSlider.SetBlockSignals(true);
            _cornerRadiusSlider.Value = Player.CornerRadius;
            _cornerRadiusSlider.SetBlockSignals(false);
            _cornerRadiusValue.Text = ((int)Player.CornerRadius).ToString();

            _bgOpacitySlider.SetBlockSignals(true);
            _bgOpacitySlider.Value = Player.BgOpacity;
            _bgOpacitySlider.SetBlockSignals(false);
            _bgOpacityValue.Text = Player.BgOpacity.ToString(DebugPanelLengthScalePolicy.FormatStr);

            _fontSizeSlider.SetBlockSignals(true);
            _fontSizeSlider.Value = Player.FontSizeOverride;
            _fontSizeSlider.SetBlockSignals(false);
            _fontSizeValue.Text = Player.FontSizeOverride > 0 ? Player.FontSizeOverride.ToString() : "自动";

            _lineSpacingSlider.SetBlockSignals(true);
            _lineSpacingSlider.Value = Player.LineSpacing;
            _lineSpacingSlider.SetBlockSignals(false);
            _lineSpacingValue.Text = Player.LineSpacing.ToString("F1");

            _letterSpacingSlider.SetBlockSignals(true);
            _letterSpacingSlider.Value = Player.LetterSpacing;
            _letterSpacingSlider.SetBlockSignals(false);
            _letterSpacingValue.Text = ((int)Player.LetterSpacing).ToString();

            _boldCheck.ButtonPressed = Player.FontBold;
            _italicCheck.ButtonPressed = Player.FontItalic;
            _shadowCheck.ButtonPressed = Player.FontShadow;

            if (_fontAutoSizeCheck != null)
                _fontAutoSizeCheck.ButtonPressed = Player.FontSizeOverride == 0;

            SyncLabelOffsetSlidersFromPlayer();
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  UpdateControlStates
        // ═══════════════════════════════════════════════════════════════════════

        public void UpdateControlStates()
        {
            bool fontAutoSize = _fontAutoSizeCheck?.ButtonPressed ?? false;

            Color dim = new Color(0.5f, 0.5f, 0.5f, 1.0f);
            Color normal = new Color(1.0f, 1.0f, 1.0f, 1.0f);

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
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  SaveConfig / LoadConfig (abstract overrides)
        // ═══════════════════════════════════════════════════════════════════════

        public override void SaveConfig(ConfigFile cfg)
        {
            // Player settings
            cfg.SetValue("player", "player_size", _playerSizeSlider.Value);
            cfg.SetValue("player", "visual_size_scale", _playerSizeScaleSlider?.Value ?? 1.0);
            cfg.SetValue("player", "border_width", _borderWidthSlider.Value);
            cfg.SetValue("player", "border_width_scale", _borderWidthScaleSlider?.Value ?? (3.0 / 111.0));
            cfg.SetValue("player", "corner_radius", _cornerRadiusSlider.Value);
            cfg.SetValue("player", "bg_opacity", _bgOpacitySlider.Value);
            cfg.SetValue("player", "font_size", _fontSizeSlider.Value);
            cfg.SetValue("player", "line_spacing", _lineSpacingSlider.Value);
            cfg.SetValue("player", "letter_spacing", _letterSpacingSlider.Value);
            cfg.SetValue("player", "text_alignment", Player != null ? (int)Player.TextAlignment : (int)HorizontalAlignment.Center);
            cfg.SetValue("player", "font_bold", _boldCheck.ButtonPressed);
            cfg.SetValue("player", "font_italic", _italicCheck.ButtonPressed);
            cfg.SetValue("player", "font_shadow", _shadowCheck.ButtonPressed);
            cfg.SetValue("player", "font_auto_size", _fontAutoSizeCheck?.ButtonPressed ?? false);

            // Label settings
            if (Player != null)
            {
                bool autoCenterX = Player.LabelAutoCenterX;
                cfg.SetValue("labels", "auto_center_x", autoCenterX);
                for (int i = 0; i < LabelCount; i++)
                {
                    string prefix = $"label_{i}";
                    cfg.SetValue("labels", $"{prefix}_visible", _labelVisibleChecks[i]?.ButtonPressed ?? true);
                    cfg.SetValue("labels", $"{prefix}_name", Player.GetLabelName(i));
                    cfg.SetValue("labels", $"{prefix}_text", Player.GetLabelText(i));
                    cfg.SetValue("labels", $"{prefix}_font_size", _labelFontSizeSliders[i]?.Value ?? 0);
                    var offset = Player.GetLabelOffset(i);
                    cfg.SetValue("labels", $"{prefix}_offset_x", (double)offset.X);
                    cfg.SetValue("labels", $"{prefix}_offset_y", (double)offset.Y);
                    var lineColors = Player.LineColors;
                    if (i < lineColors.Count)
                    {
                        var c = lineColors[i];
                        cfg.SetValue("labels", $"{prefix}_color_r", c.R);
                        cfg.SetValue("labels", $"{prefix}_color_g", c.G);
                        cfg.SetValue("labels", $"{prefix}_color_b", c.B);
                        cfg.SetValue("labels", $"{prefix}_color_a", c.A);
                    }
                }
            }

            // Health bar settings
            if (Player != null)
            {
                var hpOffset = Player.GetHealthBarOffset();
                cfg.SetValue("healthbar", "visible", _healthBarVisibleCheck?.ButtonPressed ?? true);
                cfg.SetValue("healthbar", "length", _healthBarLengthSlider?.Value ?? 80);
                cfg.SetValue("healthbar", "length_scale", _healthBarLengthScaleSlider?.Value ?? (80.0 / 111.0));
                cfg.SetValue("healthbar", "height", _healthBarHeightSlider?.Value ?? 6);
                cfg.SetValue("healthbar", "height_scale", _healthBarHeightScaleSlider?.Value ?? (6.0 / 111.0));
                cfg.SetValue("healthbar", "fill", _healthBarFillSlider?.Value ?? 100);
                cfg.SetValue("healthbar", "offset_x", (double)hpOffset.X);
                cfg.SetValue("healthbar", "offset_y", (double)hpOffset.Y);
                var hpColor = Player.HealthBarColor;
                cfg.SetValue("healthbar", "color_r", hpColor.R);
                cfg.SetValue("healthbar", "color_g", hpColor.G);
                cfg.SetValue("healthbar", "color_b", hpColor.B);
            }

            // Cast bar settings
            if (Player != null)
            {
                var ctOffset = Player.GetCastBarOffset();
                cfg.SetValue("castbar", "visible", _castBarVisibleCheck?.ButtonPressed ?? true);
                cfg.SetValue("castbar", "length", _castBarLengthSlider?.Value ?? 60);
                cfg.SetValue("castbar", "height", _castBarHeightSlider?.Value ?? 4);
                cfg.SetValue("castbar", "fill", _castBarFillSlider?.Value ?? 60);
                cfg.SetValue("castbar", "offset_x", (double)ctOffset.X);
                cfg.SetValue("castbar", "offset_y", (double)ctOffset.Y);
                var ctColor = Player.CastBarColor;
                cfg.SetValue("castbar", "color_r", ctColor.R);
                cfg.SetValue("castbar", "color_g", ctColor.G);
                cfg.SetValue("castbar", "color_b", ctColor.B);
            }

            // Action bar settings
            if (Player != null)
            {
                cfg.SetValue("actionbar", "text_y_offset", _actionBarTextYOffsetSlider?.Value ?? 0);
                cfg.SetValue("actionbar", "progress_height", _actionBarProgressHeightSlider?.Value ?? 4);
            }

            // Level badge settings
            if (Player != null)
            {
                var lvOffset = Player.GetLevelBadgeOffset();
                cfg.SetValue("levelbadge", "visible", _levelBadgeVisibleCheck?.ButtonPressed ?? true);
                cfg.SetValue("levelbadge", "font_size", _levelBadgeFontSizeSlider?.Value ?? 12);
                cfg.SetValue("levelbadge", "text", Player.LevelBadgeText);
                cfg.SetValue("levelbadge", "offset_x", (double)lvOffset.X);
                cfg.SetValue("levelbadge", "offset_y", (double)lvOffset.Y);
                var lvTxtColor = Player.LevelBadgeTextColor;
                cfg.SetValue("levelbadge", "txt_r", lvTxtColor.R);
                cfg.SetValue("levelbadge", "txt_g", lvTxtColor.G);
                cfg.SetValue("levelbadge", "txt_b", lvTxtColor.B);
            }
        }

        public override void LoadConfig(ConfigFile cfg, bool configLoaded)
        {
            if (Player == null) return;

            // Player sliders
            _playerSizeSlider.SetBlockSignals(true);
            _playerSizeSlider.Value = (double)cfg.GetValue("player", "player_size", 111);
            _playerSizeSlider.SetBlockSignals(false);

            _playerSizeScaleSlider?.SetBlockSignals(true);
            double loadedVisualScale = (double)cfg.GetValue("player", "visual_size_scale", 1.0);
            _playerSizeScaleSlider?.SetValue(loadedVisualScale);
            _playerSizeScaleSlider?.SetBlockSignals(false);
            if (_playerSizeScaleValue != null)
                _playerSizeScaleValue.Text = loadedVisualScale.ToString(DebugPanelLengthScalePolicy.FormatStr);

            _borderWidthSlider.SetBlockSignals(true);
            _borderWidthSlider.Value = (double)cfg.GetValue("player", "border_width", 3.0);
            _borderWidthSlider.SetBlockSignals(false);

            _borderWidthScaleSlider?.SetBlockSignals(true);
            double loadedScale = (double)cfg.GetValue("player", "border_width_scale", 3.0 / 111.0);
            _borderWidthScaleSlider?.SetValue(loadedScale);
            _borderWidthScaleSlider?.SetBlockSignals(false);
            if (_borderWidthScaleValue != null)
                _borderWidthScaleValue.Text = loadedScale.ToString(DebugPanelLengthScalePolicy.FormatStr);

            _cornerRadiusSlider.SetBlockSignals(true);
            _cornerRadiusSlider.Value = (double)cfg.GetValue("player", "corner_radius", 0.0);
            _cornerRadiusSlider.SetBlockSignals(false);

            _bgOpacitySlider.SetBlockSignals(true);
            _bgOpacitySlider.Value = (double)cfg.GetValue("player", "bg_opacity", 0.1);
            _bgOpacitySlider.SetBlockSignals(false);

            _fontSizeSlider.SetBlockSignals(true);
            _fontSizeSlider.Value = (double)cfg.GetValue("player", "font_size", 0);
            _fontSizeSlider.SetBlockSignals(false);

            _lineSpacingSlider.SetBlockSignals(true);
            _lineSpacingSlider.Value = (double)cfg.GetValue("player", "line_spacing", 0.8);
            _lineSpacingSlider.SetBlockSignals(false);

            _letterSpacingSlider.SetBlockSignals(true);
            _letterSpacingSlider.Value = (double)cfg.GetValue("player", "letter_spacing", 0.0);
            _letterSpacingSlider.SetBlockSignals(false);

            _boldCheck.ButtonPressed = (bool)cfg.GetValue("player", "font_bold", false);
            _italicCheck.ButtonPressed = (bool)cfg.GetValue("player", "font_italic", false);
            _shadowCheck.ButtonPressed = (bool)cfg.GetValue("player", "font_shadow", false);

            if (_fontAutoSizeCheck != null)
            {
                _fontAutoSizeCheck.ButtonPressed = (bool)cfg.GetValue("player", "font_auto_size", false);
                OnFontAutoSizeToggled(_fontAutoSizeCheck.ButtonPressed);
            }

            // Update display labels
            _playerSizeValue.Text = ((int)_playerSizeSlider.Value).ToString();
            _borderWidthValue.Text = ((int)_borderWidthSlider.Value).ToString();
            _cornerRadiusValue.Text = ((int)_cornerRadiusSlider.Value).ToString();
            _bgOpacityValue.Text = $"{_bgOpacitySlider.Value:F2}";
            _fontSizeValue.Text = ((int)_fontSizeSlider.Value).ToString();
            _lineSpacingValue.Text = $"{_lineSpacingSlider.Value:F1}";
            _letterSpacingValue.Text = ((int)_letterSpacingSlider.Value).ToString();

            // Apply to player
            HorizontalAlignment savedAlignment = (HorizontalAlignment)(int)cfg.GetValue("player", "text_alignment", (int)HorizontalAlignment.Center);
            if (Player != null)
            {
                Player.SetTextAlignment(savedAlignment);
                Player.SetVisualSizeScale((float)(_playerSizeScaleSlider?.Value ?? 1.0));
                Player.SetBorderWidthScale((float)(_borderWidthScaleSlider?.Value ?? (3.0 / 111.0)));
                Player.SetCornerRadius((float)_cornerRadiusSlider.Value);
                Player.SetBgOpacity((float)_bgOpacitySlider.Value);
                Player.SetLineSpacing((float)_lineSpacingSlider.Value);
                Player.SetLetterSpacing((float)_letterSpacingSlider.Value);
                Player.SetFontBold(_boldCheck.ButtonPressed);
                Player.SetFontItalic(_italicCheck.ButtonPressed);
                Player.SetFontShadow(_shadowCheck.ButtonPressed);
            }

            // Label settings
            if (Player != null)
            {
                bool autoCenterX = (bool)cfg.GetValue("labels", "auto_center_x", false);
                Player.SetLabelAutoCenterX(autoCenterX);
                if (_labelAutoCenterXCheck != null)
                {
                    _labelAutoCenterXCheck.SetBlockSignals(true);
                    _labelAutoCenterXCheck.ButtonPressed = autoCenterX;
                    _labelAutoCenterXCheck.SetBlockSignals(false);
                }
                for (int i = 0; i < LabelCount; i++)
                {
                    string prefix = $"label_{i}";
                    bool visible = (bool)cfg.GetValue("labels", $"{prefix}_visible", true);
                    string name = (string)cfg.GetValue("labels", $"{prefix}_name", new[] { "名称", "职业", "称号", "状态" }[i]);
                    string text = (string)cfg.GetValue("labels", $"{prefix}_text", "");
                    double fontSize = (double)cfg.GetValue("labels", $"{prefix}_font_size", 0);
                    double offsetX = (double)cfg.GetValue("labels", $"{prefix}_offset_x", Player.DefaultOffsets[i].X);
                    double offsetY = (double)cfg.GetValue("labels", $"{prefix}_offset_y", Player.DefaultOffsets[i].Y);

                    Player.SetLabelName(i, name);
                    if (!string.IsNullOrEmpty(text)) Player.SetLabelText(i, text);
                    Player.SetLabelVisible(i, visible);
                    Player.SetLabelFontSize(i, (int)fontSize);
                    Player.SetLabelOffset(i, new Vector2((float)offsetX, (float)offsetY));

                    float cr = (float)(double)cfg.GetValue("labels", $"{prefix}_color_r", 0.0);
                    float cg = (float)(double)cfg.GetValue("labels", $"{prefix}_color_g", 0.0);
                    float cb = (float)(double)cfg.GetValue("labels", $"{prefix}_color_b", 0.0);
                    float ca = (float)(double)cfg.GetValue("labels", $"{prefix}_color_a", 1.0);
                    Player.SetLineColor(i, new Color(cr, cg, cb, ca));

                    // Update UI
                    if (_labelVisibleChecks[i] != null) { _labelVisibleChecks[i].SetBlockSignals(true); _labelVisibleChecks[i].ButtonPressed = visible; _labelVisibleChecks[i].SetBlockSignals(false); }
                    if (_labelNameEdits[i] != null) _labelNameEdits[i].Text = name;
                    if (_labelTextEdits[i] != null && !string.IsNullOrEmpty(text)) _labelTextEdits[i].Text = text;
                    if (_labelFontSizeSliders[i] != null) { _labelFontSizeSliders[i].SetBlockSignals(true); _labelFontSizeSliders[i].Value = fontSize; _labelFontSizeSliders[i].SetBlockSignals(false); }
                    if (_labelFontSizeValues[i] != null) _labelFontSizeValues[i].Text = fontSize > 0 ? ((int)fontSize).ToString() : "自动";
                    if (_labelColorButtons[i] != null) _labelColorButtons[i].Modulate = new Color(cr, cg, cb, ca);
                    if (_labelOffsetXSliders[i] != null) { _labelOffsetXSliders[i].SetBlockSignals(true); _labelOffsetXSliders[i].Value = offsetX; _labelOffsetXSliders[i].SetBlockSignals(false); }
                    if (_labelOffsetYSliders[i] != null) { _labelOffsetYSliders[i].SetBlockSignals(true); _labelOffsetYSliders[i].Value = offsetY; _labelOffsetYSliders[i].SetBlockSignals(false); }
                    if (_labelOffsetXValues[i] != null) _labelOffsetXValues[i].Text = ((int)offsetX).ToString();
                    if (_labelOffsetYValues[i] != null) _labelOffsetYValues[i].Text = ((int)offsetY).ToString();
                }
                Player.RefreshLabels();
            }

            // Health bar
            {
                bool hpVisible = (bool)cfg.GetValue("healthbar", "visible", true);
                double hpLength = (double)cfg.GetValue("healthbar", "length", 102);
                double hpLengthScale = (double)cfg.GetValue("healthbar", "length_scale", 102.0 / 111.0);
                double hpHeight = (double)cfg.GetValue("healthbar", "height", 6);
                double hpHeightScale = (double)cfg.GetValue("healthbar", "height_scale", 6.0 / 111.0);
                double hpFill = (double)cfg.GetValue("healthbar", "fill", 100);
                double hpOffX = (double)cfg.GetValue("healthbar", "offset_x", 0);
                double hpOffY = (double)cfg.GetValue("healthbar", "offset_y", -70);
                float hpR = (float)(double)cfg.GetValue("healthbar", "color_r", 0.0);
                float hpG = (float)(double)cfg.GetValue("healthbar", "color_g", 0.8);
                float hpB = (float)(double)cfg.GetValue("healthbar", "color_b", 0.0);

                if (Player != null)
                {
                    Player.SetHealthBarVisible(hpVisible);
                    Player.SetHealthBarLengthScale((float)hpLengthScale);
                    Player.SetHealthBarHeightScale((float)hpHeightScale);
                    Player.SetHealthBarFillPercent((float)(hpFill / 100.0));
                    Player.SetHealthBarOffset(new Vector2((float)hpOffX, (float)hpOffY));
                    Player.SetHealthBarColor(new Color(hpR, hpG, hpB));
                }

                if (_healthBarVisibleCheck != null) { _healthBarVisibleCheck.SetBlockSignals(true); _healthBarVisibleCheck.ButtonPressed = hpVisible; _healthBarVisibleCheck.SetBlockSignals(false); }
                if (_healthBarLengthSlider != null) { _healthBarLengthSlider.SetBlockSignals(true); _healthBarLengthSlider.Value = hpLength; _healthBarLengthSlider.SetBlockSignals(false); }
                if (_healthBarLengthScaleSlider != null) { _healthBarLengthScaleSlider.SetBlockSignals(true); _healthBarLengthScaleSlider.Value = hpLengthScale; _healthBarLengthScaleSlider.SetBlockSignals(false); }
                if (_healthBarLengthScaleValue != null) _healthBarLengthScaleValue.Text = hpLengthScale.ToString(DebugPanelLengthScalePolicy.FormatStr);
                if (_healthBarHeightSlider != null) { _healthBarHeightSlider.SetBlockSignals(true); _healthBarHeightSlider.Value = hpHeight; _healthBarHeightSlider.SetBlockSignals(false); }
                if (_healthBarHeightScaleSlider != null) { _healthBarHeightScaleSlider.SetBlockSignals(true); _healthBarHeightScaleSlider.Value = hpHeightScale; _healthBarHeightScaleSlider.SetBlockSignals(false); }
                if (_healthBarHeightScaleValue != null) _healthBarHeightScaleValue.Text = hpHeightScale.ToString(DebugPanelLengthScalePolicy.FormatStr);
                if (_healthBarFillSlider != null) { _healthBarFillSlider.SetBlockSignals(true); _healthBarFillSlider.Value = hpFill; _healthBarFillSlider.SetBlockSignals(false); }
                if (_healthBarOffsetXSlider != null) { _healthBarOffsetXSlider.SetBlockSignals(true); _healthBarOffsetXSlider.Value = hpOffX; _healthBarOffsetXSlider.SetBlockSignals(false); }
                if (_healthBarOffsetYSlider != null) { _healthBarOffsetYSlider.SetBlockSignals(true); _healthBarOffsetYSlider.Value = hpOffY; _healthBarOffsetYSlider.SetBlockSignals(false); }
                if (_healthBarLengthValue != null) _healthBarLengthValue.Text = ((int)hpLength).ToString();
                if (_healthBarHeightValue != null) _healthBarHeightValue.Text = ((int)hpHeight).ToString();
                if (_healthBarFillValue != null) _healthBarFillValue.Text = $"{(int)hpFill}%";
                if (_healthBarOffsetXValue != null) _healthBarOffsetXValue.Text = ((int)hpOffX).ToString();
                if (_healthBarOffsetYValue != null) _healthBarOffsetYValue.Text = ((int)hpOffY).ToString();
                if (_healthBarColorBtn != null) _healthBarColorBtn.Modulate = new Color(hpR, hpG, hpB);
            }

            // Cast bar
            {
                bool ctVisible = (bool)cfg.GetValue("castbar", "visible", true);
                double ctLength = (double)cfg.GetValue("castbar", "length", 60);
                double ctHeight = (double)cfg.GetValue("castbar", "height", 4);
                double ctFill = (double)cfg.GetValue("castbar", "fill", 60);
                double ctOffX = (double)cfg.GetValue("castbar", "offset_x", 0);
                double ctOffY = (double)cfg.GetValue("castbar", "offset_y", -80);
                float ctR = (float)(double)cfg.GetValue("castbar", "color_r", 0.3);
                float ctG = (float)(double)cfg.GetValue("castbar", "color_g", 0.5);
                float ctB = (float)(double)cfg.GetValue("castbar", "color_b", 1.0);

                if (Player != null)
                {
                    Player.SetCastBarVisible(ctVisible);
                    float ctLenScale = Player.GridSize > 0 ? (float)(ctLength / Player.GridSize) : 0;
                    float ctHScale = Player.GridSize > 0 ? (float)(ctHeight / Player.GridSize) : 0;
                    Player.SetCastBarLengthScale(ctLenScale);
                    Player.SetCastBarHeightScale(ctHScale);
                    Player.SetCastBarFillPercent((float)(ctFill / 100.0));
                    Player.SetCastBarOffset(new Vector2((float)ctOffX, (float)ctOffY));
                    Player.SetCastBarColor(new Color(ctR, ctG, ctB));
                }

                if (_castBarVisibleCheck != null) { _castBarVisibleCheck.SetBlockSignals(true); _castBarVisibleCheck.ButtonPressed = ctVisible; _castBarVisibleCheck.SetBlockSignals(false); }
                if (_castBarLengthSlider != null) { _castBarLengthSlider.SetBlockSignals(true); _castBarLengthSlider.Value = ctLength; _castBarLengthSlider.SetBlockSignals(false); }
                if (_castBarHeightSlider != null) { _castBarHeightSlider.SetBlockSignals(true); _castBarHeightSlider.Value = ctHeight; _castBarHeightSlider.SetBlockSignals(false); }
                if (_castBarFillSlider != null) { _castBarFillSlider.SetBlockSignals(true); _castBarFillSlider.Value = ctFill; _castBarFillSlider.SetBlockSignals(false); }
                if (_castBarOffsetXSlider != null) { _castBarOffsetXSlider.SetBlockSignals(true); _castBarOffsetXSlider.Value = ctOffX; _castBarOffsetXSlider.SetBlockSignals(false); }
                if (_castBarOffsetYSlider != null) { _castBarOffsetYSlider.SetBlockSignals(true); _castBarOffsetYSlider.Value = ctOffY; _castBarOffsetYSlider.SetBlockSignals(false); }
                if (_castBarLengthValue != null) _castBarLengthValue.Text = ((int)ctLength).ToString();
                if (_castBarHeightValue != null) _castBarHeightValue.Text = ((int)ctHeight).ToString();
                if (_castBarFillValue != null) _castBarFillValue.Text = $"{(int)ctFill}%";
                if (_castBarOffsetXValue != null) _castBarOffsetXValue.Text = ((int)ctOffX).ToString();
                if (_castBarOffsetYValue != null) _castBarOffsetYValue.Text = ((int)ctOffY).ToString();
                if (_castBarColorBtn != null) _castBarColorBtn.Modulate = new Color(ctR, ctG, ctB);
            }

            // Action bar
            {
                double abTextY = (double)cfg.GetValue("actionbar", "text_y_offset", 0);
                double abPh = (double)cfg.GetValue("actionbar", "progress_height", 4);
                if (_actionBarTextYOffsetSlider != null) { _actionBarTextYOffsetSlider.SetBlockSignals(true); _actionBarTextYOffsetSlider.Value = abTextY; _actionBarTextYOffsetSlider.SetBlockSignals(false); }
                if (_actionBarProgressHeightSlider != null) { _actionBarProgressHeightSlider.SetBlockSignals(true); _actionBarProgressHeightSlider.Value = abPh; _actionBarProgressHeightSlider.SetBlockSignals(false); }
                if (_actionBarTextYOffsetValue != null) _actionBarTextYOffsetValue.Text = ((int)abTextY).ToString();
                if (_actionBarProgressHeightValue != null) _actionBarProgressHeightValue.Text = ((int)abPh).ToString();
            }

            // Level badge
            {
                bool lvVisible = (bool)cfg.GetValue("levelbadge", "visible", true);
                double lvFontSize = (double)cfg.GetValue("levelbadge", "font_size", 12);
                string lvText = (string)cfg.GetValue("levelbadge", "text", "Lv.{level}");
                double lvOffX = (double)cfg.GetValue("levelbadge", "offset_x", -35);
                double lvOffY = (double)cfg.GetValue("levelbadge", "offset_y", -35);
                float lvTxtR = (float)(double)cfg.GetValue("levelbadge", "txt_r", 1.0);
                float lvTxtG = (float)(double)cfg.GetValue("levelbadge", "txt_g", 1.0);
                float lvTxtB = (float)(double)cfg.GetValue("levelbadge", "txt_b", 0.0);

                if (Player != null)
                {
                    Player.SetLevelBadgeVisible(lvVisible);
                    Player.SetLevelBadgeFontSize((float)lvFontSize);
                    Player.SetLevelBadgeText(lvText);
                    Player.SetLevelBadgeOffset(new Vector2((float)lvOffX, (float)lvOffY));
                    Player.SetLevelBadgeTextColor(new Color(lvTxtR, lvTxtG, lvTxtB));
                }

                if (_levelBadgeVisibleCheck != null) { _levelBadgeVisibleCheck.SetBlockSignals(true); _levelBadgeVisibleCheck.ButtonPressed = lvVisible; _levelBadgeVisibleCheck.SetBlockSignals(false); }
                if (_levelBadgeFontSizeSlider != null) { _levelBadgeFontSizeSlider.SetBlockSignals(true); _levelBadgeFontSizeSlider.Value = lvFontSize; _levelBadgeFontSizeSlider.SetBlockSignals(false); }
                if (_levelBadgeOffsetXSlider != null) { _levelBadgeOffsetXSlider.SetBlockSignals(true); _levelBadgeOffsetXSlider.Value = lvOffX; _levelBadgeOffsetXSlider.SetBlockSignals(false); }
                if (_levelBadgeOffsetYSlider != null) { _levelBadgeOffsetYSlider.SetBlockSignals(true); _levelBadgeOffsetYSlider.Value = lvOffY; _levelBadgeOffsetYSlider.SetBlockSignals(false); }
                if (_levelBadgeFontSizeValue != null) _levelBadgeFontSizeValue.Text = ((int)lvFontSize).ToString();
                if (_levelBadgeOffsetXValue != null) _levelBadgeOffsetXValue.Text = ((int)lvOffX).ToString();
                if (_levelBadgeOffsetYValue != null) _levelBadgeOffsetYValue.Text = ((int)lvOffY).ToString();
                if (_levelBadgeTextColorBtn != null) _levelBadgeTextColorBtn.Modulate = new Color(lvTxtR, lvTxtG, lvTxtB);
                if (_levelBadgeTextEdit != null) _levelBadgeTextEdit.Text = lvText;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Undo — CaptureUndoState / ApplyUndoState (abstract overrides)
        // ═══════════════════════════════════════════════════════════════════════

        public override Godot.Collections.Dictionary CaptureUndoState()
        {
            var state = new Godot.Collections.Dictionary
            {
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
            if (_fontAutoSizeCheck != null)
                state["font_auto_size"] = _fontAutoSizeCheck.ButtonPressed;
            if (_labelAutoCenterXCheck != null)
                state["label_auto_center_x"] = _labelAutoCenterXCheck.ButtonPressed;
            if (_healthBarLengthScaleSlider != null)
                state["healthbar_length_scale"] = _healthBarLengthScaleSlider.Value;
            if (_healthBarHeightScaleSlider != null)
                state["healthbar_height_scale"] = _healthBarHeightScaleSlider.Value;
            return state;
        }

        public override void ApplyUndoState(Godot.Collections.Dictionary state)
        {
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
            if (state.ContainsKey("font_size"))
                _fontSizeSlider.Value = (double)state["font_size"];
            if (state.ContainsKey("line_spacing"))
                _lineSpacingSlider.Value = (double)state["line_spacing"];
            if (state.ContainsKey("letter_spacing"))
                _letterSpacingSlider.Value = (double)state["letter_spacing"];
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

            // Trigger all player updates
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

        #region ApplyLoadedPlayerSettings — called by Owner.DeferredLoadConfig
        /// <summary>
        /// Apply loaded player settings from config to the Player object.
        /// This method reads the config file and applies values directly.
        /// </summary>
        public void ApplyLoadedPlayerSettings()
        {
            GD.Print($"[DebugPanel] ApplyLoadedPlayerSettings() called, player={Player}");
            if (Player == null)
            {
                GD.Print("[DebugPanel] Player is still null, cannot apply settings");
                return;
            }

            ConfigFile config = new ConfigFile();
            Error err = config.Load(DebugPanel.CONFIG_PATH);
            if (err == Error.Ok)
            {
                // Read and apply directly from config
                // SetVisualSizeScale recalculates VisualSize = GridSize * scale,
                // so call it last to ensure scale takes effect.
                double savedScale = (double)config.GetValue("player", "visual_size_scale", 1.0);
                Player.SetVisualSizeScale((float)savedScale);
                double savedBorderScale = (double)config.GetValue("player", "border_width_scale", 3.0 / 111.0);
                Player.SetBorderWidthScale((float)savedBorderScale);
                Player.SetCornerRadius((float)(double)config.GetValue("player", "corner_radius", 0.0));
                Player.SetBgOpacity((float)(double)config.GetValue("player", "bg_opacity", 0.1));
                Player.SetLineSpacing((float)(double)config.GetValue("player", "line_spacing", 0.8));
                Player.SetLetterSpacing((float)(double)config.GetValue("player", "letter_spacing", 0.0));
                Player.SetFontBold((bool)config.GetValue("player", "font_bold", false));
                Player.SetFontItalic((bool)config.GetValue("player", "font_italic", false));
                Player.SetFontShadow((bool)config.GetValue("player", "font_shadow", false));
                Player.SetTextAlignment((HorizontalAlignment)(int)config.GetValue("player", "text_alignment", (int)HorizontalAlignment.Center));

                // Apply font size
                double savedFontSize = (double)config.GetValue("player", "font_size", 0);
                bool savedAutoSize = (bool)config.GetValue("player", "font_auto_size", false);
                if (savedAutoSize)
                    Player.SetFontSize(0);
                else if (savedFontSize > 0)
                    Player.SetFontSize((int)savedFontSize);

                Player.RefreshLabels();
                Player.QueueRedraw();

                // Apply label control settings
                for (int i = 0; i < LabelCount; i++)
                {
                    string prefix = $"label_{i}";
                    bool visible = (bool)config.GetValue("labels", $"{prefix}_visible", true);
                    string name = (string)config.GetValue("labels", $"{prefix}_name", new[] { "名称", "职业", "称号", "状态" }[i]);
                    string text = (string)config.GetValue("labels", $"{prefix}_text", "");
                    double fontSize = (double)config.GetValue("labels", $"{prefix}_font_size", 0);
                    double offsetX = (double)config.GetValue("labels", $"{prefix}_offset_x", Player.DefaultOffsets[i].X);
                    double offsetY = (double)config.GetValue("labels", $"{prefix}_offset_y", Player.DefaultOffsets[i].Y);

                    Player.SetLabelName(i, name);
                    if (!string.IsNullOrEmpty(text)) Player.SetLabelText(i, text);
                    Player.SetLabelVisible(i, visible);
                    Player.SetLabelFontSize(i, (int)fontSize);
                    Player.SetLabelOffset(i, new Vector2((float)offsetX, (float)offsetY));

                    float cr = (float)(double)config.GetValue("labels", $"{prefix}_color_r", 0.0);
                    float cg = (float)(double)config.GetValue("labels", $"{prefix}_color_g", 0.0);
                    float cb = (float)(double)config.GetValue("labels", $"{prefix}_color_b", 0.0);
                    float ca = (float)(double)config.GetValue("labels", $"{prefix}_color_a", 1.0);
                    Player.SetLineColor(i, new Color(cr, cg, cb, ca));
                }
                Player.RefreshLabels();

                // Apply health bar settings
                Player.SetHealthBarVisible((bool)config.GetValue("healthbar", "visible", true));
                Player.SetHealthBarLengthScale((float)(double)config.GetValue("healthbar", "length_scale", 102.0 / 111.0));
                Player.SetHealthBarHeightScale((float)(double)config.GetValue("healthbar", "height_scale", 6.0 / 111.0));
                Player.SetHealthBarFillPercent((float)((double)config.GetValue("healthbar", "fill", 100) / 100.0));
                Player.SetHealthBarOffset(new Vector2(
                    (float)(double)config.GetValue("healthbar", "offset_x", 0),
                    (float)(double)config.GetValue("healthbar", "offset_y", -70)));
                float hr = (float)(double)config.GetValue("healthbar", "color_r", 0.0);
                float hg = (float)(double)config.GetValue("healthbar", "color_g", 0.8);
                float hb = (float)(double)config.GetValue("healthbar", "color_b", 0.0);
                Player.SetHealthBarColor(new Color(hr, hg, hb));

                // Apply cast bar settings
                Player.SetCastBarVisible((bool)config.GetValue("castbar", "visible", true));
                Player.SetCastBarLengthScale((float)(double)config.GetValue("castbar", "length_scale", 60.0 / 111.0));
                Player.SetCastBarHeightScale((float)(double)config.GetValue("castbar", "height_scale", 4.0 / 111.0));
                Player.SetCastBarFillPercent((float)((double)config.GetValue("castbar", "fill", 60) / 100.0));
                Player.SetCastBarOffset(new Vector2(
                    (float)(double)config.GetValue("castbar", "offset_x", 0),
                    (float)(double)config.GetValue("castbar", "offset_y", -80)));
                float cr2 = (float)(double)config.GetValue("castbar", "color_r", 0.3);
                float cg2 = (float)(double)config.GetValue("castbar", "color_g", 0.5);
                float cb2 = (float)(double)config.GetValue("castbar", "color_b", 1.0);
                Player.SetCastBarColor(new Color(cr2, cg2, cb2));

                // Apply action bar settings
                float abTextY = (float)(double)config.GetValue("actionbar", "text_y_offset", 0);
                float abPh = (float)(double)config.GetValue("actionbar", "progress_height", 4);
                Player.SetActionBarTextYOffset(abTextY);
                Player.SetActionBarProgressHeight(abPh);

                // Apply level badge settings
                Player.SetLevelBadgeVisible((bool)config.GetValue("levelbadge", "visible", true));
                Player.SetLevelBadgeFontSize((float)(double)config.GetValue("levelbadge", "font_size", 12));
                Player.SetLevelBadgeText((string)config.GetValue("levelbadge", "text", "Lv.{level}"));
                Player.SetLevelBadgeOffset(new Vector2(
                    (float)(double)config.GetValue("levelbadge", "offset_x", -35),
                    (float)(double)config.GetValue("levelbadge", "offset_y", -35)));
                float lvTxtR = (float)(double)config.GetValue("levelbadge", "txt_r", 1.0);
                float lvTxtG = (float)(double)config.GetValue("levelbadge", "txt_g", 1.0);
                float lvTxtB = (float)(double)config.GetValue("levelbadge", "txt_b", 0.0);
                Player.SetLevelBadgeTextColor(new Color(lvTxtR, lvTxtG, lvTxtB));

                GD.Print($"[DebugPanel] Player settings applied, visual_size={Player.VisualSize}");
            }
        }
        #endregion

        #region ExportConfigData
        public override Godot.Collections.Dictionary ExportConfigData()
        {
            var playerData = new Godot.Collections.Dictionary
            {
                ["player_size"] = _playerSizeSlider?.Value ?? 111,
                ["visual_size_scale"] = _playerSizeScaleSlider?.Value ?? 1.0,
                ["border_width"] = _borderWidthSlider?.Value ?? 3.0,
                ["border_width_scale"] = _borderWidthScaleSlider?.Value ?? (3.0 / 111.0),
                ["corner_radius"] = _cornerRadiusSlider?.Value ?? 0.0,
                ["bg_opacity"] = _bgOpacitySlider?.Value ?? 0.1,
                ["font_size"] = _fontSizeSlider?.Value ?? 0,
                ["line_spacing"] = _lineSpacingSlider?.Value ?? 0.8,
                ["letter_spacing"] = _letterSpacingSlider?.Value ?? 0.0,
                ["font_bold"] = _boldCheck?.ButtonPressed ?? false,
                ["font_italic"] = _italicCheck?.ButtonPressed ?? false,
                ["font_shadow"] = _shadowCheck?.ButtonPressed ?? false,
                ["font_auto_size"] = _fontAutoSizeCheck?.ButtonPressed ?? false
            };
            return playerData;
        }
        #endregion
    }
}
