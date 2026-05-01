using Godot;
using System.Collections.Generic;
using System.Linq;

namespace ClinetCSharp
{
    /// <summary>
    /// DebugPanel NPC Tab — NPC 全局样式相关控件和逻辑
    /// 支持多配置：每个配置ID绑定一组样式，NPC按NpcType查找配置
    /// </summary>
    public class DebugPanelNpcTab : DebugPanelTab
    {
        #region Fields - Config ID Selector
        private SpinBox _configIdSpin;
        private OptionButton _configIdOption;
        private Button _addConfigBtn;
        private Button _deleteConfigBtn;
        private int _selectedConfigId = 1;
        #endregion

        #region Fields - Visual Style
        private HSlider _npcSizeSlider;
        private Label _npcSizeValue;
        private HSlider _npcSizeScaleSlider;
        private Label _npcSizeScaleValue;
        private HSlider _npcBorderWidthSlider;
        private Label _npcBorderWidthValue;
        private HSlider _npcBorderWidthScaleSlider;
        private Label _npcBorderWidthScaleValue;
        private HSlider _npcCornerRadiusSlider;
        private Label _npcCornerRadiusValue;
        private HSlider _npcBgOpacitySlider;
        private Label _npcBgOpacityValue;
        private HSlider _npcFontSizeSlider;
        private Label _npcFontSizeValue;

        private ColorPickerButton _npcBorderColorPicker;
        private ColorPickerButton _npcBgColorPicker;
        private ColorPickerButton _npcTextColorPicker;
        #endregion

        #region Fields - Interact Menu Offset
        private HSlider _interactMenuOffsetAXSlider;
        private Label _interactMenuOffsetAXValue;
        private HSlider _interactMenuOffsetAYSlider;
        private Label _interactMenuOffsetAYValue;
        private HSlider _interactMenuOffsetBXSlider;
        private Label _interactMenuOffsetBXValue;
        private HSlider _interactMenuOffsetBYSlider;
        private Label _interactMenuOffsetBYValue;
        #endregion

        #region Fields - Label Controls (4 independent)
        private LineEdit[] _npcLabelEdits = new LineEdit[4];
        private HSlider[] _npcLabelFontSizeSliders = new HSlider[4];
        private Label[] _npcLabelFontSizeValues = new Label[4];
        private HSlider[] _npcLabelXOffsetSliders = new HSlider[4];
        private Label[] _npcLabelXOffsetValues = new Label[4];
        private HSlider[] _npcLabelYOffsetSliders = new HSlider[4];
        private Label[] _npcLabelYOffsetValues = new Label[4];
        private CheckButton[] _npcLabelCenterXChecks = new CheckButton[4];
        #endregion

        public DebugPanelNpcTab(DebugPanel owner) : base(owner) { }

        public override string TabKey => "npc";

        #region BuildUI
        public override void BuildUI(VBoxContainer tabContainer)
        {
            var title = new Label { Text = "NPC 全局样式", HorizontalAlignment = HorizontalAlignment.Center };
            title.AddThemeFontSizeOverride("font_size", 13);
            tabContainer.AddChild(title);

            tabContainer.AddChild(new HSeparator());

            // 配置ID选择器
            BuildConfigIdSelector(tabContainer);

            tabContainer.AddChild(new HSeparator());

            // 滑块
            (_npcSizeSlider, _npcSizeValue) = CreateMonsterSliderRow(tabContainer, "视觉大小", 32, 256, 111);
            (_npcSizeScaleSlider, _npcSizeScaleValue) = CreateMonsterSliderRow(tabContainer, "角色比例", 0.1f, 1.0f, 1.0f);
            (_npcBorderWidthSlider, _npcBorderWidthValue) = CreateMonsterSliderRow(tabContainer, "边框粗细", 0, 20, 3);
            (_npcBorderWidthScaleSlider, _npcBorderWidthScaleValue) = CreateMonsterSliderRow(tabContainer, "边框比例", 0.0f, 0.2f, 3.0f / 111.0f, DebugPanelLengthScalePolicy.StepF);
            (_npcCornerRadiusSlider, _npcCornerRadiusValue) = CreateMonsterSliderRow(tabContainer, "圆角半径", 0, 60, 12);
            (_npcBgOpacitySlider, _npcBgOpacityValue) = CreateMonsterSliderRow(tabContainer, "背景不透明度", 0, 1, 0.9f);
            (_npcFontSizeSlider, _npcFontSizeValue) = CreateMonsterSliderRow(tabContainer, "字体大小", 0, 48, 0);

            // 颜色
            var bcRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            bcRow.AddChild(new Label { Text = "边框颜色:", CustomMinimumSize = new Vector2(80, 0) });
            _npcBorderColorPicker = new ColorPickerButton { Color = new Color(0.3f, 0.5f, 0.9f), CustomMinimumSize = new Vector2(60, 26) };
            bcRow.AddChild(_npcBorderColorPicker);
            tabContainer.AddChild(bcRow);

            var bgcRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            bgcRow.AddChild(new Label { Text = "背景颜色:", CustomMinimumSize = new Vector2(80, 0) });
            _npcBgColorPicker = new ColorPickerButton { Color = new Color(0.2f, 0.4f, 0.8f), CustomMinimumSize = new Vector2(60, 26) };
            bgcRow.AddChild(_npcBgColorPicker);
            tabContainer.AddChild(bgcRow);

            var tcRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            tcRow.AddChild(new Label { Text = "文字颜色:", CustomMinimumSize = new Vector2(80, 0) });
            _npcTextColorPicker = new ColorPickerButton { Color = new Color(0.95f, 0.97f, 1.0f), CustomMinimumSize = new Vector2(60, 26) };
            tcRow.AddChild(_npcTextColorPicker);
            tabContainer.AddChild(tcRow);

            tabContainer.AddChild(new HSeparator());

            // 交互面板偏移
            tabContainer.AddChild(new Label { Text = "交互面板偏移:" });
            tabContainer.AddChild(new Label { Text = "  A位置(玩家在左,面板在右):" });
            (_interactMenuOffsetAXSlider, _interactMenuOffsetAXValue) = CreateMonsterSliderRow(tabContainer, "  A-X", -200, 200, 60);
            (_interactMenuOffsetAYSlider, _interactMenuOffsetAYValue) = CreateMonsterSliderRow(tabContainer, "  A-Y", -200, 200, -20);
            tabContainer.AddChild(new Label { Text = "  B位置(玩家在右,面板在左):" });
            (_interactMenuOffsetBXSlider, _interactMenuOffsetBXValue) = CreateMonsterSliderRow(tabContainer, "  B-X", -200, 200, -60);
            (_interactMenuOffsetBYSlider, _interactMenuOffsetBYValue) = CreateMonsterSliderRow(tabContainer, "  B-Y", -200, 200, -20);

            tabContainer.AddChild(new HSeparator());

            // 4 行文字
            tabContainer.AddChild(new Label { Text = "显示文字:" });
            for (int i = 0; i < 4; i++)
            {
                var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                row.AddChild(new Label { Text = $"行{i + 1}:", CustomMinimumSize = new Vector2(40, 0) });
                var edit = new LineEdit { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 26) };
                row.AddChild(edit);
                _npcLabelEdits[i] = edit;
                tabContainer.AddChild(row);

                var fsRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                fsRow.AddChild(new Control { CustomMinimumSize = new Vector2(40, 0) });
                fsRow.AddChild(new Label { Text = "字号:", CustomMinimumSize = new Vector2(36, 0) });
                var fsSlider = new HSlider { MinValue = 0, MaxValue = 48, Value = 0, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 20), Step = 1 , Scrollable = false };
                fsRow.AddChild(fsSlider);
                var fsVal = new Label { Text = "0", CustomMinimumSize = new Vector2(24, 0) };
                fsRow.AddChild(fsVal);
                fsSlider.ValueChanged += (v) => fsVal.Text = ((int)v).ToString();
                _npcLabelFontSizeSliders[i] = fsSlider;
                _npcLabelFontSizeValues[i] = fsVal;
                tabContainer.AddChild(fsRow);

                var xRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                xRow.AddChild(new Control { CustomMinimumSize = new Vector2(40, 0) });
                xRow.AddChild(new Label { Text = "X:", CustomMinimumSize = new Vector2(24, 0) });
                var xSlider = new HSlider { MinValue = -40, MaxValue = 40, Value = 0, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 20), Step = 1 , Scrollable = false };
                xRow.AddChild(xSlider);
                var xVal = new Label { Text = "0", CustomMinimumSize = new Vector2(28, 0) };
                xRow.AddChild(xVal);
                var centerCheck = new CheckButton { Text = "居中", ButtonPressed = true };
                xRow.AddChild(centerCheck);
                xSlider.ValueChanged += (v) => xVal.Text = ((int)v).ToString();
                centerCheck.Toggled += (enabled) => { xSlider.Editable = !enabled; xSlider.Modulate = enabled ? new Color(0.5f, 0.5f, 0.5f, 1) : new Color(1, 1, 1, 1); };
                _npcLabelXOffsetSliders[i] = xSlider;
                _npcLabelXOffsetValues[i] = xVal;
                _npcLabelCenterXChecks[i] = centerCheck;
                tabContainer.AddChild(xRow);

                var yRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                yRow.AddChild(new Control { CustomMinimumSize = new Vector2(40, 0) });
                yRow.AddChild(new Label { Text = "Y:", CustomMinimumSize = new Vector2(24, 0) });
                var ySlider = new HSlider { MinValue = -40, MaxValue = 40, Value = 0, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 20), Step = 1 , Scrollable = false };
                yRow.AddChild(ySlider);
                var yVal = new Label { Text = "0", CustomMinimumSize = new Vector2(28, 0) };
                yRow.AddChild(yVal);
                ySlider.ValueChanged += (v) => yVal.Text = ((int)v).ToString();
                _npcLabelYOffsetSliders[i] = ySlider;
                _npcLabelYOffsetValues[i] = yVal;
                tabContainer.AddChild(yRow);
            }

            // 事件绑定
            _npcSizeSlider.ValueChanged += (_) => ApplyNpcDebugChanges();
            _npcSizeSlider.DragEnded += (_) => ApplyNpcDebugChanges();
            _npcSizeScaleSlider.ValueChanged += (_) => ApplyNpcDebugChanges();
            _npcSizeScaleSlider.DragEnded += (_) => ApplyNpcDebugChanges();
            _npcBorderWidthSlider.ValueChanged += (_) => ApplyNpcDebugChanges();
            _npcBorderWidthSlider.DragEnded += (_) => ApplyNpcDebugChanges();
            _npcBorderWidthScaleSlider.ValueChanged += (_) => ApplyNpcDebugChanges();
            _npcBorderWidthScaleSlider.DragEnded += (_) => ApplyNpcDebugChanges();
            _npcCornerRadiusSlider.ValueChanged += (_) => ApplyNpcDebugChanges();
            _npcCornerRadiusSlider.DragEnded += (_) => ApplyNpcDebugChanges();
            _npcBgOpacitySlider.ValueChanged += (_) => ApplyNpcDebugChanges();
            _npcBgOpacitySlider.DragEnded += (_) => ApplyNpcDebugChanges();
            _npcFontSizeSlider.ValueChanged += (_) => ApplyNpcDebugChanges();
            _npcFontSizeSlider.DragEnded += (_) => ApplyNpcDebugChanges();
            _npcBorderColorPicker.ColorChanged += (_) => ApplyNpcDebugChanges();
            _npcBgColorPicker.ColorChanged += (_) => ApplyNpcDebugChanges();
            _npcTextColorPicker.ColorChanged += (_) => ApplyNpcDebugChanges();
            _interactMenuOffsetAXSlider.ValueChanged += (_) => ApplyNpcDebugChanges();
            _interactMenuOffsetAYSlider.ValueChanged += (_) => ApplyNpcDebugChanges();
            _interactMenuOffsetBXSlider.ValueChanged += (_) => ApplyNpcDebugChanges();
            _interactMenuOffsetBYSlider.ValueChanged += (_) => ApplyNpcDebugChanges();
            for (int i = 0; i < 4; i++)
            {
                _npcLabelEdits[i].TextChanged += (_) => ApplyNpcDebugChanges();
                _npcLabelFontSizeSliders[i].ValueChanged += (_) => ApplyNpcDebugChanges();
                _npcLabelFontSizeSliders[i].DragEnded += (_) => ApplyNpcDebugChanges();
                _npcLabelXOffsetSliders[i].ValueChanged += (_) => ApplyNpcDebugChanges();
                _npcLabelXOffsetSliders[i].DragEnded += (_) => ApplyNpcDebugChanges();
                _npcLabelCenterXChecks[i].Toggled += (_) => ApplyNpcDebugChanges();
                _npcLabelYOffsetSliders[i].ValueChanged += (_) => ApplyNpcDebugChanges();
                _npcLabelYOffsetSliders[i].DragEnded += (_) => ApplyNpcDebugChanges();
            }
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
            _configIdOption.Clear();
            foreach (var kv in NpcManager.StyleConfigs)
            {
                int idx = _configIdOption.GetItemCount();
                _configIdOption.AddItem(kv.Key.ToString());
                _configIdOption.SetItemMetadata(idx, kv.Key);
            }
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
            SyncNpcDebugUI();
        }

        private void OnAddConfigPressed()
        {
            int newId = (int)_configIdSpin.Value;
            if (NpcManager.StyleConfigs.ContainsKey(newId))
            {
                _selectedConfigId = newId;
                RefreshConfigIdList();
                SyncNpcDebugUI();
                return;
            }
            NpcManager.GetOrCreateStyleConfig(newId);
            _selectedConfigId = newId;
            RefreshConfigIdList();
            SyncNpcDebugUI();
        }

        private void OnDeleteConfigPressed()
        {
            // 禁止删除到0
            if (NpcManager.StyleConfigs.Count <= 1) return;
            if (!NpcManager.StyleConfigs.ContainsKey(_selectedConfigId)) return;
            NpcManager.StyleConfigs.Remove(_selectedConfigId);
            _selectedConfigId = NpcManager.StyleConfigs.Keys.First();
            RefreshConfigIdList();
            SyncNpcDebugUI();
            NpcManager.Instance?.ApplyStyleToAll();
        }
        #endregion

        #region ConnectSignals / DisconnectSignals
        public override void ConnectSignals() { }
        public override void DisconnectSignals() { }
        #endregion

        #region Apply NPC Debug Changes
        private void ApplyNpcDebugChanges()
        {
            var cfg = NpcManager.GetOrCreateStyleConfig(_selectedConfigId);
            cfg.VisualSizeScale = (float)_npcSizeScaleSlider.Value;
            cfg.BorderWidthScale = (float)_npcBorderWidthScaleSlider.Value;
            cfg.CornerRadius = (float)_npcCornerRadiusSlider.Value;
            cfg.BgOpacity = (float)_npcBgOpacitySlider.Value;
            cfg.FontSize = (int)_npcFontSizeSlider.Value;
            cfg.BorderColor = _npcBorderColorPicker.Color;
            cfg.BgColor = _npcBgColorPicker.Color;
            cfg.TextColor = _npcTextColorPicker.Color;
            cfg.InteractMenuOffsetAX = (float)_interactMenuOffsetAXSlider.Value;
            cfg.InteractMenuOffsetAY = (float)_interactMenuOffsetAYSlider.Value;
            cfg.InteractMenuOffsetBX = (float)_interactMenuOffsetBXSlider.Value;
            cfg.InteractMenuOffsetBY = (float)_interactMenuOffsetBYSlider.Value;
            for (int i = 0; i < 4; i++)
            {
                cfg.LabelTexts[i] = _npcLabelEdits[i].Text;
                cfg.LabelFontSizes[i] = (int)_npcLabelFontSizeSliders[i].Value;
                cfg.LabelXOffsets[i] = (float)_npcLabelXOffsetSliders[i].Value;
                cfg.LabelCenterX[i] = _npcLabelCenterXChecks[i].ButtonPressed;
                cfg.LabelYOffsets[i] = (float)_npcLabelYOffsetSliders[i].Value;
            }

            NpcManager.Instance?.ApplyStyleToAll();
            NpcManager.Instance?.RefreshInteractMenuPosition();
        }
        #endregion

        #region SyncToCurrentValues
        public override void SyncToCurrentValues()
        {
            RefreshConfigIdList();
            SyncNpcDebugUI();
        }

        public void SyncNpcDebugUI()
        {
            var cfg = NpcManager.GetStyleConfig(_selectedConfigId);

            _npcSizeSlider.SetBlockSignals(true);
            _npcSizeScaleSlider.SetBlockSignals(true);
            _npcBorderWidthSlider.SetBlockSignals(true);
            _npcBorderWidthScaleSlider.SetBlockSignals(true);
            _npcCornerRadiusSlider.SetBlockSignals(true);
            _npcBgOpacitySlider.SetBlockSignals(true);
            _npcFontSizeSlider.SetBlockSignals(true);
            _interactMenuOffsetAXSlider.SetBlockSignals(true);
            _interactMenuOffsetAYSlider.SetBlockSignals(true);
            _interactMenuOffsetBXSlider.SetBlockSignals(true);
            _interactMenuOffsetBYSlider.SetBlockSignals(true);

            _npcSizeScaleSlider.Value = cfg.VisualSizeScale;
            _npcSizeScaleValue.Text = cfg.VisualSizeScale.ToString(DebugPanelLengthScalePolicy.FormatStr);
            _npcBorderWidthScaleSlider.Value = cfg.BorderWidthScale;
            _npcBorderWidthScaleValue.Text = cfg.BorderWidthScale.ToString(DebugPanelLengthScalePolicy.FormatStr);
            _npcCornerRadiusSlider.Value = cfg.CornerRadius;
            _npcBgOpacitySlider.Value = cfg.BgOpacity;
            _npcFontSizeSlider.Value = cfg.FontSize;
            _npcBorderColorPicker.Color = cfg.BorderColor;
            _npcBgColorPicker.Color = cfg.BgColor;
            _npcTextColorPicker.Color = cfg.TextColor;
            _interactMenuOffsetAXSlider.Value = cfg.InteractMenuOffsetAX;
            _interactMenuOffsetAYSlider.Value = cfg.InteractMenuOffsetAY;
            _interactMenuOffsetBXSlider.Value = cfg.InteractMenuOffsetBX;
            _interactMenuOffsetBYSlider.Value = cfg.InteractMenuOffsetBY;

            _npcSizeSlider.SetBlockSignals(false);
            _npcSizeScaleSlider.SetBlockSignals(false);
            _npcBorderWidthSlider.SetBlockSignals(false);
            _npcBorderWidthScaleSlider.SetBlockSignals(false);
            _npcCornerRadiusSlider.SetBlockSignals(false);
            _npcBgOpacitySlider.SetBlockSignals(false);
            _npcFontSizeSlider.SetBlockSignals(false);
            _interactMenuOffsetAXSlider.SetBlockSignals(false);
            _interactMenuOffsetAYSlider.SetBlockSignals(false);
            _interactMenuOffsetBXSlider.SetBlockSignals(false);
            _interactMenuOffsetBYSlider.SetBlockSignals(false);

            for (int i = 0; i < 4; i++)
            {
                _npcLabelEdits[i].Text = cfg.LabelTexts[i] ?? "";
                _npcLabelFontSizeSliders[i].Value = cfg.LabelFontSizes[i];
                _npcLabelFontSizeValues[i].Text = cfg.LabelFontSizes[i].ToString();
                _npcLabelXOffsetSliders[i].Value = cfg.LabelXOffsets[i];
                _npcLabelXOffsetValues[i].Text = cfg.LabelXOffsets[i].ToString("F0");
                _npcLabelCenterXChecks[i].ButtonPressed = cfg.LabelCenterX[i];
                _npcLabelXOffsetSliders[i].Editable = !cfg.LabelCenterX[i];
                _npcLabelXOffsetSliders[i].Modulate = cfg.LabelCenterX[i] ? new Color(0.5f, 0.5f, 0.5f, 1) : new Color(1, 1, 1, 1);
                _npcLabelYOffsetSliders[i].Value = cfg.LabelYOffsets[i];
                _npcLabelYOffsetValues[i].Text = cfg.LabelYOffsets[i].ToString("F0");
            }
        }
        #endregion

        #region SaveConfig
        public override void SaveConfig(ConfigFile cfg)
        {
            // 写入所有 [npc_*] sections
            foreach (var kv in NpcManager.StyleConfigs)
            {
                string sec = $"npc_{kv.Key}";
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
                cfg.SetValue(sec, "interact_menu_offset_ax", (double)c.InteractMenuOffsetAX);
                cfg.SetValue(sec, "interact_menu_offset_ay", (double)c.InteractMenuOffsetAY);
                cfg.SetValue(sec, "interact_menu_offset_bx", (double)c.InteractMenuOffsetBX);
                cfg.SetValue(sec, "interact_menu_offset_by", (double)c.InteractMenuOffsetBY);
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
            // NpcManager._Ready already loaded StyleConfigs from config file,
            // so just sync UI to the current state
            RefreshConfigIdList();
            NpcManager.Instance?.ApplyStyleToAll();
            SyncNpcDebugUI();
        }
        #endregion

        #region Undo State
        public override Godot.Collections.Dictionary CaptureUndoState() => new Godot.Collections.Dictionary();
        public override void ApplyUndoState(Godot.Collections.Dictionary state) { }
        #endregion

        #region ExportConfigData
        public override Godot.Collections.Dictionary ExportConfigData()
        {
            var data = new Godot.Collections.Dictionary();
            foreach (var kv in NpcManager.StyleConfigs)
            {
                var c = kv.Value;
                data[$"config_{kv.Key}"] = new Godot.Collections.Dictionary
                {
                    ["visual_size_scale"] = c.VisualSizeScale,
                    ["border_width_scale"] = c.BorderWidthScale,
                    ["corner_radius"] = c.CornerRadius,
                    ["bg_opacity"] = c.BgOpacity,
                    ["font_size"] = c.FontSize,
                    ["interact_menu_offset_ax"] = c.InteractMenuOffsetAX,
                    ["interact_menu_offset_ay"] = c.InteractMenuOffsetAY,
                    ["interact_menu_offset_bx"] = c.InteractMenuOffsetBX,
                    ["interact_menu_offset_by"] = c.InteractMenuOffsetBY,
                };
            }
            return data;
        }
        #endregion
    }
}
