using Godot;
using System.Collections.Generic;
using System.Linq;

namespace ClinetCSharp
{
    /// <summary>
    /// DebugPanel Monster Tab — 怪物全局样式、标签、AI/移动配置相关控件和逻辑
    /// 支持多配置：每个配置ID绑定一组样式，怪物按MonsterId查找配置
    /// </summary>
    public class DebugPanelMonsterTab : DebugPanelTab
    {
        #region Fields - Config ID Selector
        private SpinBox _configIdSpin;
        private OptionButton _configIdOption;
        private Button _addConfigBtn;
        private Button _deleteConfigBtn;
        private int _selectedConfigId = 1;
        #endregion

        #region Fields - Visual Style
        private HSlider _monsterSizeSlider;
        private Label _monsterSizeValue;
        private HSlider _monsterSizeScaleSlider;
        private Label _monsterSizeScaleValue;
        private HSlider _monsterBorderWidthSlider;
        private Label _monsterBorderWidthValue;
        private HSlider _monsterBorderWidthScaleSlider;
        private Label _monsterBorderWidthScaleValue;
        private HSlider _monsterCornerRadiusSlider;
        private Label _monsterCornerRadiusValue;
        private HSlider _monsterBgOpacitySlider;
        private Label _monsterBgOpacityValue;
        private HSlider _monsterFontSizeSlider;
        private Label _monsterFontSizeValue;

        private ColorPickerButton _monsterBorderColorPicker;
        private ColorPickerButton _monsterBgColorPicker;
        private ColorPickerButton _monsterTextColorPicker;
        #endregion

        #region Fields - Label Controls (4 independent)
        private LineEdit[] _monsterLabelEdits = new LineEdit[4];
        private HSlider[] _monsterLabelFontSizeSliders = new HSlider[4];
        private Label[] _monsterLabelFontSizeValues = new Label[4];
        private HSlider[] _monsterLabelXOffsetSliders = new HSlider[4];
        private Label[] _monsterLabelXOffsetValues = new Label[4];
        private HSlider[] _monsterLabelYOffsetSliders = new HSlider[4];
        private Label[] _monsterLabelYOffsetValues = new Label[4];
        private CheckButton[] _monsterLabelCenterXChecks = new CheckButton[4];
        #endregion

        #region Fields - AI / Movement Config
        private HSlider _monsterMoveSpeedSlider;
        private Label _monsterMoveSpeedValue;
        private HSlider _monsterPatrolRangeSlider;
        private Label _monsterPatrolRangeValue;
        private HSlider _monsterAggroRangeSlider;
        private Label _monsterAggroRangeValue;
        private HSlider _monsterMoveIntervalSlider;
        private Label _monsterMoveIntervalValue;
        private Button _saveMonsterConfigBtn;

        // 血条
        private CheckButton _monsterHpBarVisibleCheck;
        private Button _monsterHpBarColorBtn;
        private HSlider _monsterHpBarLengthScaleSlider;
        private Label _monsterHpBarLengthScaleValue;
        private HSlider _monsterHpBarHeightScaleSlider;
        private Label _monsterHpBarHeightScaleValue;
        private HSlider _monsterHpBarFillSlider;
        private Label _monsterHpBarFillValue;
        private HSlider _monsterHpBarOffsetXSlider;
        private Label _monsterHpBarOffsetXValue;
        private HSlider _monsterHpBarOffsetYSlider;
        private Label _monsterHpBarOffsetYValue;

        // MP 条
        private CheckButton _monsterMpBarVisibleCheck;
        private Button _monsterMpBarColorBtn;
        private HSlider _monsterMpBarLengthScaleSlider;
        private Label _monsterMpBarLengthScaleValue;
        private HSlider _monsterMpBarHeightScaleSlider;
        private Label _monsterMpBarHeightScaleValue;
        private HSlider _monsterMpBarFillSlider;
        private Label _monsterMpBarFillValue;
        private HSlider _monsterMpBarOffsetXSlider;
        private Label _monsterMpBarOffsetXValue;
        private HSlider _monsterMpBarOffsetYSlider;
        private Label _monsterMpBarOffsetYValue;
        #endregion

        public DebugPanelMonsterTab(DebugPanel owner) : base(owner) { }

        public override string TabKey => "monster";

        #region BuildUI
        public override void BuildUI(VBoxContainer tabContainer)
        {
            // 标题
            var title = new Label { Text = "怪物全局样式", HorizontalAlignment = HorizontalAlignment.Center };
            title.AddThemeFontSizeOverride("font_size", 13);
            tabContainer.AddChild(title);

            tabContainer.AddChild(new HSeparator());

            // 配置ID选择器
            BuildConfigIdSelector(tabContainer);

            tabContainer.AddChild(new HSeparator());

            // 滑块
            (_monsterSizeSlider, _monsterSizeValue) = CreateMonsterSliderRow(tabContainer, "视觉大小", 32, 256, 111);
            (_monsterSizeScaleSlider, _monsterSizeScaleValue) = CreateMonsterSliderRow(tabContainer, "角色比例", 0.1f, 1.0f, 1.0f);
            (_monsterBorderWidthSlider, _monsterBorderWidthValue) = CreateMonsterSliderRow(tabContainer, "边框粗细", 0, 20, 3);
            (_monsterBorderWidthScaleSlider, _monsterBorderWidthScaleValue) = CreateMonsterSliderRow(tabContainer, "边框比例", 0.0f, 0.2f, 3.0f / 111.0f, 0.01f);
            (_monsterCornerRadiusSlider, _monsterCornerRadiusValue) = CreateMonsterSliderRow(tabContainer, "圆角半径", 0, 60, 12);
            (_monsterBgOpacitySlider, _monsterBgOpacityValue) = CreateMonsterSliderRow(tabContainer, "背景不透明度", 0, 1, 0.9f);
            (_monsterFontSizeSlider, _monsterFontSizeValue) = CreateMonsterSliderRow(tabContainer, "字体大小", 0, 48, 0);

            // 颜色
            var bcRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            bcRow.AddChild(new Label { Text = "边框颜色:", CustomMinimumSize = new Vector2(80, 0) });
            _monsterBorderColorPicker = new ColorPickerButton { Color = new Color(0.9f, 0.3f, 0.3f), CustomMinimumSize = new Vector2(60, 26) };
            bcRow.AddChild(_monsterBorderColorPicker);
            tabContainer.AddChild(bcRow);

            var bgcRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            bgcRow.AddChild(new Label { Text = "背景颜色:", CustomMinimumSize = new Vector2(80, 0) });
            _monsterBgColorPicker = new ColorPickerButton { Color = new Color(0.8f, 0.2f, 0.2f), CustomMinimumSize = new Vector2(60, 26) };
            bgcRow.AddChild(_monsterBgColorPicker);
            tabContainer.AddChild(bgcRow);

            var tcRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            tcRow.AddChild(new Label { Text = "文字颜色:", CustomMinimumSize = new Vector2(80, 0) });
            _monsterTextColorPicker = new ColorPickerButton { Color = new Color(1, 0.95f, 0.95f), CustomMinimumSize = new Vector2(60, 26) };
            tcRow.AddChild(_monsterTextColorPicker);
            tabContainer.AddChild(tcRow);

            tabContainer.AddChild(new HSeparator());

            // 4 行文字（每行含内容、字号、X偏移、X居中、Y偏移）
            tabContainer.AddChild(new Label { Text = "显示文字:" });
            for (int i = 0; i < 4; i++)
            {
                var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                row.AddChild(new Label { Text = $"行{i + 1}:", CustomMinimumSize = new Vector2(40, 0) });
                var edit = new LineEdit { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 26) };
                row.AddChild(edit);
                _monsterLabelEdits[i] = edit;
                tabContainer.AddChild(row);

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
                tabContainer.AddChild(fsRow);

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
                tabContainer.AddChild(xRow);

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
                tabContainer.AddChild(yRow);
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

                        // ---- 血条 ----
            tabContainer.AddChild(new HSeparator());

            var hpTitleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            hpTitleRow.AddChild(new Label { Text = "血条", CustomMinimumSize = new Vector2(45, 0) });
            _monsterHpBarVisibleCheck = new CheckButton { ButtonPressed = true };
            hpTitleRow.AddChild(_monsterHpBarVisibleCheck);
            hpTitleRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            _monsterHpBarColorBtn = new Button { Text = "色", CustomMinimumSize = new Vector2(30, 24) };
            _monsterHpBarColorBtn.Modulate = new Color(0, 0.8f, 0, 1);
            hpTitleRow.AddChild(_monsterHpBarColorBtn);
            tabContainer.AddChild(hpTitleRow);

            // 长度比例（Length 是计算值，只读显示）
            var hpLenScaleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            hpLenScaleRow.AddChild(new Label { Text = "长度比例", CustomMinimumSize = new Vector2(60, 0) });
            _monsterHpBarLengthScaleValue = new Label { Text = "0.92", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            hpLenScaleRow.AddChild(_monsterHpBarLengthScaleValue);
            tabContainer.AddChild(hpLenScaleRow);
            _monsterHpBarLengthScaleSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 0.1, MaxValue = 2.0, Step = 0.05, Value = 102.0 / 111.0 };
            tabContainer.AddChild(_monsterHpBarLengthScaleSlider);

            // 高度比例（Height 是计算值，只读显示）
            var hpHScaleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            hpHScaleRow.AddChild(new Label { Text = "高度比例", CustomMinimumSize = new Vector2(60, 0) });
            _monsterHpBarHeightScaleValue = new Label { Text = "0.05", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            hpHScaleRow.AddChild(_monsterHpBarHeightScaleValue);
            tabContainer.AddChild(hpHScaleRow);
            _monsterHpBarHeightScaleSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 0.01, MaxValue = 0.3, Step = 0.01, Value = 6.0 / 111.0 };
            tabContainer.AddChild(_monsterHpBarHeightScaleSlider);

            // 填充
            var hpFillRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            hpFillRow.AddChild(new Label { Text = "填充", CustomMinimumSize = new Vector2(35, 0) });
            _monsterHpBarFillValue = new Label { Text = "100%", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            hpFillRow.AddChild(_monsterHpBarFillValue);
            tabContainer.AddChild(hpFillRow);
            _monsterHpBarFillSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 0, MaxValue = 100, Step = 1, Value = 100 };
            tabContainer.AddChild(_monsterHpBarFillSlider);

            // X偏移
            var hpOffXRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            hpOffXRow.AddChild(new Label { Text = "X偏移", CustomMinimumSize = new Vector2(45, 0) });
            _monsterHpBarOffsetXValue = new Label { Text = "0", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            hpOffXRow.AddChild(_monsterHpBarOffsetXValue);
            tabContainer.AddChild(hpOffXRow);
            _monsterHpBarOffsetXSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = -150, MaxValue = 150, Step = 1, Value = 0 };
            tabContainer.AddChild(_monsterHpBarOffsetXSlider);

            // Y偏移
            var hpOffYRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            hpOffYRow.AddChild(new Label { Text = "Y偏移", CustomMinimumSize = new Vector2(45, 0) });
            _monsterHpBarOffsetYValue = new Label { Text = "-70", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            hpOffYRow.AddChild(_monsterHpBarOffsetYValue);
            tabContainer.AddChild(hpOffYRow);
            _monsterHpBarOffsetYSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = -150, MaxValue = 150, Step = 1, Value = -70 };
            tabContainer.AddChild(_monsterHpBarOffsetYSlider);

            // ---- MP 条 ----
            tabContainer.AddChild(new HSeparator());

            var mpTitleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            mpTitleRow.AddChild(new Label { Text = "MP条", CustomMinimumSize = new Vector2(45, 0) });
            _monsterMpBarVisibleCheck = new CheckButton { ButtonPressed = true };
            mpTitleRow.AddChild(_monsterMpBarVisibleCheck);
            mpTitleRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            _monsterMpBarColorBtn = new Button { Text = "色", CustomMinimumSize = new Vector2(30, 24) };
            _monsterMpBarColorBtn.Modulate = new Color(0.2f, 0.4f, 1.0f, 1);
            mpTitleRow.AddChild(_monsterMpBarColorBtn);
            tabContainer.AddChild(mpTitleRow);

            // 长度
            // MP 长度比例
            var mpLenScaleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            mpLenScaleRow.AddChild(new Label { Text = "长度比例", CustomMinimumSize = new Vector2(60, 0) });
            _monsterMpBarLengthScaleValue = new Label { Text = "0.72", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            mpLenScaleRow.AddChild(_monsterMpBarLengthScaleValue);
            tabContainer.AddChild(mpLenScaleRow);
            _monsterMpBarLengthScaleSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 0.1, MaxValue = 2.0, Step = 0.05, Value = 80.0 / 111.0 };
            tabContainer.AddChild(_monsterMpBarLengthScaleSlider);

            // MP 高度比例
            var mpHScaleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            mpHScaleRow.AddChild(new Label { Text = "高度比例", CustomMinimumSize = new Vector2(60, 0) });
            _monsterMpBarHeightScaleValue = new Label { Text = "0.04", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            mpHScaleRow.AddChild(_monsterMpBarHeightScaleValue);
            tabContainer.AddChild(mpHScaleRow);
            _monsterMpBarHeightScaleSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 0.01, MaxValue = 0.3, Step = 0.01, Value = 4.0 / 111.0 };
            tabContainer.AddChild(_monsterMpBarHeightScaleSlider);

            // 填充
            var mpFillRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            mpFillRow.AddChild(new Label { Text = "填充", CustomMinimumSize = new Vector2(35, 0) });
            _monsterMpBarFillValue = new Label { Text = "100%", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            mpFillRow.AddChild(_monsterMpBarFillValue);
            tabContainer.AddChild(mpFillRow);
            _monsterMpBarFillSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 0, MaxValue = 100, Step = 1, Value = 100 };
            tabContainer.AddChild(_monsterMpBarFillSlider);

            // X偏移
            var mpOffXRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            mpOffXRow.AddChild(new Label { Text = "X偏移", CustomMinimumSize = new Vector2(45, 0) });
            _monsterMpBarOffsetXValue = new Label { Text = "0", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            mpOffXRow.AddChild(_monsterMpBarOffsetXValue);
            tabContainer.AddChild(mpOffXRow);
            _monsterMpBarOffsetXSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = -150, MaxValue = 150, Step = 1, Value = 0 };
            tabContainer.AddChild(_monsterMpBarOffsetXSlider);

            // Y偏移
            var mpOffYRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            mpOffYRow.AddChild(new Label { Text = "Y偏移", CustomMinimumSize = new Vector2(45, 0) });
            _monsterMpBarOffsetYValue = new Label { Text = "-62", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            mpOffYRow.AddChild(_monsterMpBarOffsetYValue);
            tabContainer.AddChild(mpOffYRow);
            _monsterMpBarOffsetYSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = -150, MaxValue = 150, Step = 1, Value = -62 };
            tabContainer.AddChild(_monsterMpBarOffsetYSlider);

            // 信号绑定
            _monsterHpBarVisibleCheck.Toggled += _ => ApplyMonsterDebugChanges();
            _monsterHpBarColorBtn.Pressed += OnMonsterHpBarColorPressed;
            _monsterHpBarLengthScaleSlider.ValueChanged += _ => ApplyMonsterDebugChanges();
            _monsterHpBarHeightScaleSlider.ValueChanged += _ => ApplyMonsterDebugChanges();
            _monsterHpBarFillSlider.ValueChanged += _ => ApplyMonsterDebugChanges();
            _monsterHpBarOffsetXSlider.ValueChanged += _ => ApplyMonsterDebugChanges();
            _monsterHpBarOffsetYSlider.ValueChanged += _ => ApplyMonsterDebugChanges();
            _monsterMpBarVisibleCheck.Toggled += _ => ApplyMonsterDebugChanges();
            _monsterMpBarColorBtn.Pressed += OnMonsterMpBarColorPressed;
            _monsterMpBarLengthScaleSlider.ValueChanged += _ => ApplyMonsterDebugChanges();
            _monsterMpBarHeightScaleSlider.ValueChanged += _ => ApplyMonsterDebugChanges();
            _monsterMpBarFillSlider.ValueChanged += _ => ApplyMonsterDebugChanges();
            _monsterMpBarOffsetXSlider.ValueChanged += _ => ApplyMonsterDebugChanges();
            _monsterMpBarOffsetYSlider.ValueChanged += _ => ApplyMonsterDebugChanges();

            // ---- 怪物配置（AI / 移动） ----
            tabContainer.AddChild(new HSeparator());
            var configTitle = new Label { Text = "怪物配置 (JSON)", HorizontalAlignment = HorizontalAlignment.Center };
            configTitle.AddThemeFontSizeOverride("font_size", 13);
            tabContainer.AddChild(configTitle);

            var cm = Owner.GetTree()?.GetFirstNodeInGroup("monster_config_manager") as MonsterConfigManager;
            int moveSpeed = cm?.GetMoveSpeedMs() ?? 800;
            int patrolRange = 3;
            int aggroRange = 5;
            int moveInterval = 2000;
            if (cm != null)
            {
                var ai = cm.GetAiDefaults("patrol_chase");
                patrolRange = ai.PatrolRange ?? 3;
                aggroRange = ai.AggroRange ?? 5;
                moveInterval = (int)(ai.MoveIntervalMs ?? 2000);
            }

            (_monsterMoveSpeedSlider, _monsterMoveSpeedValue) = CreateMonsterSliderRow(tabContainer, "怪物移速(ms)", 100, 3000, moveSpeed, 50f);
            (_monsterPatrolRangeSlider, _monsterPatrolRangeValue) = CreateMonsterSliderRow(tabContainer, "巡逻范围", 0, 10, patrolRange, 1f);
            (_monsterAggroRangeSlider, _monsterAggroRangeValue) = CreateMonsterSliderRow(tabContainer, "仇恨范围", 0, 20, aggroRange, 1f);
            (_monsterMoveIntervalSlider, _monsterMoveIntervalValue) = CreateMonsterSliderRow(tabContainer, "移动间隔(ms)", 100, 10000, moveInterval, 100f);

            _saveMonsterConfigBtn = new Button { Text = "保存配置到 JSON", CustomMinimumSize = new Vector2(0, 32) };
            _saveMonsterConfigBtn.Pressed += OnSaveMonsterConfigPressed;
            tabContainer.AddChild(_saveMonsterConfigBtn);
        }

        private void BuildConfigIdSelector(Container parent)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(new Label { Text = "配置ID:", CustomMinimumSize = new Vector2(56, 0) });

            _configIdSpin = new SpinBox { MinValue = 1, MaxValue = 99999, Step = 1, Value = 1, CustomMinimumSize = new Vector2(60, 0) };
            row.AddChild(_configIdSpin);

            _configIdOption = new OptionButton { CustomMinimumSize = new Vector2(80, 0) };
            row.AddChild(_configIdOption);

            _addConfigBtn = new Button { Text = "新增", CustomMinimumSize = new Vector2(44, 0) };
            row.AddChild(_addConfigBtn);

            _deleteConfigBtn = new Button { Text = "删除", CustomMinimumSize = new Vector2(44, 0) };
            row.AddChild(_deleteConfigBtn);

            parent.AddChild(row);

            _configIdOption.ItemSelected += OnConfigIdOptionSelected;
            _addConfigBtn.Pressed += OnAddConfigPressed;
            _deleteConfigBtn.Pressed += OnDeleteConfigPressed;
        }

        private void RefreshConfigIdList()
        {
            var mm = MonsterManager;
            if (mm == null) return;
            _configIdOption.Clear();
            foreach (var kv in mm.StyleConfigs)
            {
                int idx = _configIdOption.GetItemCount();
                _configIdOption.AddItem(kv.Key.ToString());
                _configIdOption.SetItemMetadata(idx, kv.Key);
            }
            // 选中当前 _selectedConfigId
            for (int i = 0; i < _configIdOption.GetItemCount(); i++)
            {
                if ((int)_configIdOption.GetItemMetadata(i) == _selectedConfigId)
                {
                    _configIdOption.Select(i);
                    break;
                }
            }
        }

        private void OnConfigIdOptionSelected(long index)
        {
            if (index < 0 || index >= _configIdOption.GetItemCount()) return;
            _selectedConfigId = (int)_configIdOption.GetItemMetadata((int)index);
            _configIdSpin.Value = _selectedConfigId;
            SyncMonsterDebugUI();
        }

        private void OnAddConfigPressed()
        {
            var mm = MonsterManager;
            if (mm == null) return;
            int newId = (int)_configIdSpin.Value;
            if (mm.StyleConfigs.ContainsKey(newId))
            {
                _selectedConfigId = newId;
                RefreshConfigIdList();
                SyncMonsterDebugUI();
                return;
            }
            mm.GetOrCreateStyleConfig(newId);
            _selectedConfigId = newId;
            RefreshConfigIdList();
            SyncMonsterDebugUI();
        }

        private void OnDeleteConfigPressed()
        {
            var mm = MonsterManager;
            if (mm == null) return;
            // 禁止删除到0
            if (mm.StyleConfigs.Count <= 1) return;
            if (!mm.StyleConfigs.ContainsKey(_selectedConfigId)) return;
            mm.StyleConfigs.Remove(_selectedConfigId);
            // 切换到第一个剩余配置
            _selectedConfigId = mm.StyleConfigs.Keys.First();
            RefreshConfigIdList();
            SyncMonsterDebugUI();
            mm.ApplyStyleToAll();
        }
        #endregion

        #region ConnectSignals / DisconnectSignals
        public override void ConnectSignals()
        {
            // All monster signals are connected inline in BuildUI (lambdas / delegate +=).
            // This tab has no separately-referenceable handlers that need += / -= management.
        }

        public override void DisconnectSignals()
        {
            // Matching the above: no separate handlers to disconnect.
            // Lambda-based connections will be GC'd when the tab is freed.
        }
        #endregion

        #region Event Handlers - Monster Border Width
        private void OnMonsterBorderWidthChanged(double value)
        {
            if (_monsterBorderWidthValue != null)
                _monsterBorderWidthValue.Text = ((int)value).ToString();
        }

        private void OnMonsterBorderWidthDragEnded(bool valueChanged)
        {
            if (!valueChanged) return;
            int gridSize = (int)Owner._gridSizeSlider.Value;
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
            int gridSize = (int)Owner._gridSizeSlider.Value;
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
        #endregion

        #region Apply Monster Debug Changes
        private void ApplyMonsterDebugChanges()
        {
            var mm = MonsterManager;
            if (mm == null) return;

            var cfg = mm.GetOrCreateStyleConfig(_selectedConfigId);
            cfg.VisualSizeScale = (float)_monsterSizeScaleSlider.Value;
            cfg.BorderWidthScale = (float)_monsterBorderWidthScaleSlider.Value;
            cfg.CornerRadius = (float)_monsterCornerRadiusSlider.Value;
            cfg.BgOpacity = (float)_monsterBgOpacitySlider.Value;
            cfg.FontSize = (int)_monsterFontSizeSlider.Value;
            cfg.BorderColor = _monsterBorderColorPicker.Color;
            cfg.BgColor = _monsterBgColorPicker.Color;
            cfg.TextColor = _monsterTextColorPicker.Color;
            for (int i = 0; i < 4; i++)
            {
                cfg.LabelTexts[i] = _monsterLabelEdits[i].Text;
                cfg.LabelFontSizes[i] = (int)_monsterLabelFontSizeSliders[i].Value;
                cfg.LabelXOffsets[i] = (float)_monsterLabelXOffsetSliders[i].Value;
                cfg.LabelCenterX[i] = _monsterLabelCenterXChecks[i].ButtonPressed;
                cfg.LabelYOffsets[i] = (float)_monsterLabelYOffsetSliders[i].Value;
            }

            cfg.HpBarVisible = _monsterHpBarVisibleCheck.ButtonPressed;
            cfg.HpBarLengthScale = (float)_monsterHpBarLengthScaleSlider.Value;
            cfg.HpBarHeightScale = (float)_monsterHpBarHeightScaleSlider.Value;
            cfg.HpBarFillPercent = (float)(_monsterHpBarFillSlider.Value / 100.0);
            cfg.HpBarOffsetX = (float)_monsterHpBarOffsetXSlider.Value;
            cfg.HpBarOffsetY = (float)_monsterHpBarOffsetYSlider.Value;
            cfg.MpBarVisible = _monsterMpBarVisibleCheck.ButtonPressed;
            cfg.MpBarLengthScale = (float)_monsterMpBarLengthScaleSlider.Value;
            cfg.MpBarHeightScale = (float)_monsterMpBarHeightScaleSlider.Value;
            cfg.MpBarFillPercent = (float)(_monsterMpBarFillSlider.Value / 100.0);
            cfg.MpBarOffsetX = (float)_monsterMpBarOffsetXSlider.Value;
            cfg.MpBarOffsetY = (float)_monsterMpBarOffsetYSlider.Value;

            mm.ApplyStyleToAll();

            // 同步到玩家实体，确保调试面板两侧参数一致
            var player = Player;
            if (player != null)
            {
                player.SetHealthBarVisible(cfg.HpBarVisible);
                player.SetHealthBarLengthScale(cfg.HpBarLengthScale);
                player.SetHealthBarHeightScale(cfg.HpBarHeightScale);
                player.SetHealthBarFillPercent(cfg.HpBarFillPercent);
                player.SetHealthBarOffset(new Vector2(cfg.HpBarOffsetX, cfg.HpBarOffsetY));
                player.SetHealthBarColor(cfg.HpBarColor);
                player.SetMpBarVisible(cfg.MpBarVisible);
                player.SetMpBarLengthScale(cfg.MpBarLengthScale);
                player.SetMpBarHeightScale(cfg.MpBarHeightScale);
                player.SetMpBarFillPercent(cfg.MpBarFillPercent);
                player.SetMpBarOffset(new Vector2(cfg.MpBarOffsetX, cfg.MpBarOffsetY));
                player.SetMpBarColor(cfg.MpBarColor);
            }
        }
        #endregion

        #region Monster Config Handlers
        private void OnSaveMonsterConfigPressed()
        {
            var cm = Owner.GetTree()?.GetFirstNodeInGroup("monster_config_manager") as MonsterConfigManager;
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
            var moveVals = Owner._systemTab?.GetMoveSystemValues() ?? (0, 0, 0);
            cm.SetMoveSystem(new MoveSystem
            {
                CheckRatio = moveVals.checkRatio,
                DualGridStartRatio = moveVals.dualStart,
                DualGridEndRatio = moveVals.dualEnd,
            });
            cm.SaveConfig();
            GD.Print("[DebugPanel] Monster + Move config saved to JSON");
        }
        #endregion

        #region SyncToCurrentValues
        public override void SyncToCurrentValues()
        {
            RefreshConfigIdList();
            SyncMonsterDebugUI();
        }

        /// <summary>
        /// Sync all monster debug UI controls to current selected config values.
        /// Called when the panel is opened and during config load.
        /// </summary>
        public void SyncMonsterDebugUI()
        {
            var mm = MonsterManager;
            if (mm == null) return;

            var cfg = mm.GetStyleConfig(_selectedConfigId);

            // Block signals while syncing to prevent ValueChanged from firing
            _monsterSizeSlider.SetBlockSignals(true);
            _monsterSizeScaleSlider.SetBlockSignals(true);
            _monsterBorderWidthSlider.SetBlockSignals(true);
            _monsterBorderWidthScaleSlider.SetBlockSignals(true);
            _monsterCornerRadiusSlider.SetBlockSignals(true);
            _monsterBgOpacitySlider.SetBlockSignals(true);
            _monsterFontSizeSlider.SetBlockSignals(true);

            _monsterSizeScaleSlider.Value = cfg.VisualSizeScale;
            _monsterSizeScaleValue.Text = cfg.VisualSizeScale.ToString("F2");
            _monsterBorderWidthScaleSlider.Value = cfg.BorderWidthScale;
            _monsterBorderWidthScaleValue.Text = cfg.BorderWidthScale.ToString("F2");
            _monsterCornerRadiusSlider.Value = cfg.CornerRadius;
            _monsterBgOpacitySlider.Value = cfg.BgOpacity;
            _monsterFontSizeSlider.Value = cfg.FontSize;
            _monsterBorderColorPicker.Color = cfg.BorderColor;
            _monsterBgColorPicker.Color = cfg.BgColor;
            _monsterTextColorPicker.Color = cfg.TextColor;

            _monsterSizeSlider.SetBlockSignals(false);
            _monsterSizeScaleSlider.SetBlockSignals(false);
            _monsterBorderWidthSlider.SetBlockSignals(false);
            _monsterBorderWidthScaleSlider.SetBlockSignals(false);
            _monsterCornerRadiusSlider.SetBlockSignals(false);
            _monsterBgOpacitySlider.SetBlockSignals(false);
            _monsterFontSizeSlider.SetBlockSignals(false);

            for (int i = 0; i < 4; i++)
            {
                _monsterLabelEdits[i].Text = cfg.LabelTexts[i] ?? "";
                _monsterLabelFontSizeSliders[i].Value = cfg.LabelFontSizes[i];
                _monsterLabelFontSizeValues[i].Text = cfg.LabelFontSizes[i].ToString();
                _monsterLabelXOffsetSliders[i].Value = cfg.LabelXOffsets[i];
                _monsterLabelXOffsetValues[i].Text = cfg.LabelXOffsets[i].ToString("F0");
                _monsterLabelCenterXChecks[i].ButtonPressed = cfg.LabelCenterX[i];
                _monsterLabelXOffsetSliders[i].Editable = !cfg.LabelCenterX[i];
                _monsterLabelXOffsetSliders[i].Modulate = cfg.LabelCenterX[i] ? new Color(0.5f, 0.5f, 0.5f, 1) : new Color(1, 1, 1, 1);
                _monsterLabelYOffsetSliders[i].Value = cfg.LabelYOffsets[i];
                _monsterLabelYOffsetValues[i].Text = cfg.LabelYOffsets[i].ToString("F0");
            }

            // 血条
            _monsterHpBarVisibleCheck.SetBlockSignals(true);
            _monsterHpBarVisibleCheck.ButtonPressed = cfg.HpBarVisible;
            _monsterHpBarVisibleCheck.SetBlockSignals(false);
            _monsterHpBarColorBtn.Modulate = cfg.HpBarColor;
            _monsterHpBarLengthScaleSlider.SetBlockSignals(true);
            _monsterHpBarLengthScaleSlider.Value = cfg.HpBarLengthScale;
            _monsterHpBarLengthScaleSlider.SetBlockSignals(false);
            _monsterHpBarLengthScaleValue.Text = cfg.HpBarLengthScale.ToString("F2");
            _monsterHpBarHeightScaleSlider.SetBlockSignals(true);
            _monsterHpBarHeightScaleSlider.Value = cfg.HpBarHeightScale;
            _monsterHpBarHeightScaleSlider.SetBlockSignals(false);
            _monsterHpBarHeightScaleValue.Text = cfg.HpBarHeightScale.ToString("F2");
            _monsterHpBarFillSlider.SetBlockSignals(true);
            _monsterHpBarFillSlider.Value = cfg.HpBarFillPercent * 100;
            _monsterHpBarFillSlider.SetBlockSignals(false);
            _monsterHpBarFillValue.Text = $"{(int)(cfg.HpBarFillPercent * 100)}%";
            _monsterHpBarOffsetXSlider.SetBlockSignals(true);
            _monsterHpBarOffsetXSlider.Value = cfg.HpBarOffsetX;
            _monsterHpBarOffsetXSlider.SetBlockSignals(false);
            _monsterHpBarOffsetXValue.Text = ((int)cfg.HpBarOffsetX).ToString();
            _monsterHpBarOffsetYSlider.SetBlockSignals(true);
            _monsterHpBarOffsetYSlider.Value = cfg.HpBarOffsetY;
            _monsterHpBarOffsetYSlider.SetBlockSignals(false);
            _monsterHpBarOffsetYValue.Text = ((int)cfg.HpBarOffsetY).ToString();

            // MP 条
            _monsterMpBarVisibleCheck.SetBlockSignals(true);
            _monsterMpBarVisibleCheck.ButtonPressed = cfg.MpBarVisible;
            _monsterMpBarVisibleCheck.SetBlockSignals(false);
            _monsterMpBarColorBtn.Modulate = cfg.MpBarColor;
            _monsterMpBarLengthScaleSlider.SetBlockSignals(true);
            _monsterMpBarLengthScaleSlider.Value = cfg.MpBarLengthScale;
            _monsterMpBarLengthScaleSlider.SetBlockSignals(false);
            _monsterMpBarLengthScaleValue.Text = cfg.MpBarLengthScale.ToString("F2");
            _monsterMpBarHeightScaleSlider.SetBlockSignals(true);
            _monsterMpBarHeightScaleSlider.Value = cfg.MpBarHeightScale;
            _monsterMpBarHeightScaleSlider.SetBlockSignals(false);
            _monsterMpBarHeightScaleValue.Text = cfg.MpBarHeightScale.ToString("F2");
            _monsterMpBarFillSlider.SetBlockSignals(true);
            _monsterMpBarFillSlider.Value = cfg.MpBarFillPercent * 100;
            _monsterMpBarFillSlider.SetBlockSignals(false);
            _monsterMpBarFillValue.Text = $"{(int)(cfg.MpBarFillPercent * 100)}%";
            _monsterMpBarOffsetXSlider.SetBlockSignals(true);
            _monsterMpBarOffsetXSlider.Value = cfg.MpBarOffsetX;
            _monsterMpBarOffsetXSlider.SetBlockSignals(false);
            _monsterMpBarOffsetXValue.Text = ((int)cfg.MpBarOffsetX).ToString();
            _monsterMpBarOffsetYSlider.SetBlockSignals(true);
            _monsterMpBarOffsetYSlider.Value = cfg.MpBarOffsetY;
            _monsterMpBarOffsetYSlider.SetBlockSignals(false);
            _monsterMpBarOffsetYValue.Text = ((int)cfg.MpBarOffsetY).ToString();
        }
        #endregion

        #region SaveConfig
        public override void SaveConfig(ConfigFile cfg)
        {
            var mm = MonsterManager;
            if (mm == null) return;

            // 写入所有 [monster_*] sections
            foreach (var kv in mm.StyleConfigs)
            {
                string sec = $"monster_{kv.Key}";
                var c = kv.Value;
                cfg.SetValue(sec, "visual_size_scale", (double)c.VisualSizeScale);
                cfg.SetValue(sec, "border_width_scale", (double)c.BorderWidthScale);
                cfg.SetValue(sec, "corner_radius", (double)c.CornerRadius);
                cfg.SetValue(sec, "bg_opacity", (double)c.BgOpacity);
                cfg.SetValue(sec, "font_size", (double)c.FontSize);
                cfg.SetValue(sec, "border_color_r", (double)c.BorderColor.R);
                cfg.SetValue(sec, "border_color_g", (double)c.BorderColor.G);
                cfg.SetValue(sec, "border_color_b", (double)c.BorderColor.B);
                cfg.SetValue(sec, "bg_color_r", (double)c.BgColor.R);
                cfg.SetValue(sec, "bg_color_g", (double)c.BgColor.G);
                cfg.SetValue(sec, "bg_color_b", (double)c.BgColor.B);
                cfg.SetValue(sec, "text_color_r", (double)c.TextColor.R);
                cfg.SetValue(sec, "text_color_g", (double)c.TextColor.G);
                cfg.SetValue(sec, "text_color_b", (double)c.TextColor.B);
                for (int i = 0; i < 4; i++)
                {
                    cfg.SetValue(sec, $"label_text_{i}", c.LabelTexts[i] ?? "");
                    cfg.SetValue(sec, $"label_font_size_{i}", (double)c.LabelFontSizes[i]);
                    cfg.SetValue(sec, $"label_x_offset_{i}", (double)c.LabelXOffsets[i]);
                    cfg.SetValue(sec, $"label_center_x_{i}", c.LabelCenterX[i]);
                    cfg.SetValue(sec, $"label_y_offset_{i}", (double)c.LabelYOffsets[i]);
                }
            }
        }
        #endregion

        #region LoadConfig
        public override void LoadConfig(ConfigFile cfg, bool configLoaded)
        {
            var mm = MonsterManager;
            if (mm == null) return;

            // MonsterManager._Ready already loaded StyleConfigs from config file,
            // so just sync UI to the current state
            RefreshConfigIdList();
            mm.ApplyStyleToAll();
            SyncMonsterDebugUI();
        }
        #endregion

        #region Undo State
        public override Godot.Collections.Dictionary CaptureUndoState()
        {
            return new Godot.Collections.Dictionary();
        }

        public override void ApplyUndoState(Godot.Collections.Dictionary state)
        {
        }
        #endregion

        #region Export to JSON (for Owner.ExportConfigToJson)
        public override Godot.Collections.Dictionary ExportConfigData()
        {
            var mm = MonsterManager;
            if (mm == null) return null;

            var data = new Godot.Collections.Dictionary();
            foreach (var kv in mm.StyleConfigs)
            {
                var c = kv.Value;
                data[$"config_{kv.Key}"] = new Godot.Collections.Dictionary
                {
                    ["visual_size_scale"] = c.VisualSizeScale,
                    ["border_width_scale"] = c.BorderWidthScale,
                    ["corner_radius"] = c.CornerRadius,
                    ["bg_opacity"] = c.BgOpacity,
                    ["font_size"] = c.FontSize,
                };
            }
            return data;
        }
        #endregion

        #region HP/MP Bar Color Handlers
        private static readonly Color[] HpColors = {
            new Color(0, 0.8f, 0, 1),     // 绿
            new Color(1, 0.2f, 0.2f, 1),   // 红
            new Color(1, 0.8f, 0, 1),     // 黄
            new Color(0.5f, 0.5f, 0.5f, 1), // 灰
        };
        private static readonly Color[] MpColors = {
            new Color(0.2f, 0.4f, 1.0f, 1),  // 蓝
            new Color(0.6f, 0.2f, 0.8f, 1),  // 紫
            new Color(0.2f, 0.8f, 0.8f, 1),  // 青
            new Color(0.8f, 0.4f, 0.2f, 1),  // 橙
        };

        private void OnMonsterHpBarColorPressed()
        {
            var mm = MonsterManager;
            if (mm == null) return;
            var cfg = mm.GetOrCreateStyleConfig(_selectedConfigId);
            int nextIdx = 0;
            for (int i = 0; i < HpColors.Length; i++)
                if (HpColors[i].IsEqualApprox(cfg.HpBarColor)) { nextIdx = (i + 1) % HpColors.Length; break; }
            cfg.HpBarColor = HpColors[nextIdx];
            _monsterHpBarColorBtn.Modulate = HpColors[nextIdx];
            mm.ApplyStyleToAll();
        }

        private void OnMonsterMpBarColorPressed()
        {
            var mm = MonsterManager;
            if (mm == null) return;
            var cfg = mm.GetOrCreateStyleConfig(_selectedConfigId);
            int nextIdx = 0;
            for (int i = 0; i < MpColors.Length; i++)
                if (MpColors[i].IsEqualApprox(cfg.MpBarColor)) { nextIdx = (i + 1) % MpColors.Length; break; }
            cfg.MpBarColor = MpColors[nextIdx];
            _monsterMpBarColorBtn.Modulate = MpColors[nextIdx];
            mm.ApplyStyleToAll();
        }
        #endregion
    }
}
