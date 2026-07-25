using Godot;
using System;
using System.Collections.Generic;

namespace ClinetCSharp
{
    public class LabelGroupComponent : IEntityTabComponent
    {
        private const int N = LabelGroupData.LabelCount;

        public string ComponentName => "labels";
        public string DisplayName => "标签";
        public Type DataType => typeof(LabelGroupData);

        private Action _onChanged;

        private HSlider _fontSizeSlider;
        private Button _fontSizeValue;
        private CheckButton _boldCheck;
        private CheckButton _italicCheck;
        private CheckButton _shadowCheck;

        private readonly CollapsibleContainer[] _collapsibles = new CollapsibleContainer[N];
        private readonly CheckButton[] _vis = new CheckButton[N];
        private readonly LineEdit[] _names = new LineEdit[N];
        private readonly LineEdit[] _texts = new LineEdit[N];
        private readonly CheckButton[] _useGlobalFontChecks = new CheckButton[N];
        private readonly HSlider[] _fontSizeSliders = new HSlider[N];
        private readonly Button[] _fontSizeValues = new Button[N];
        private readonly HSlider[] _offsetXSliders = new HSlider[N];
        private readonly Button[] _offsetXValues = new Button[N];
        private readonly CheckButton[] _centerXChecks = new CheckButton[N];
        private readonly HSlider[] _offsetYSliders = new HSlider[N];
        private readonly Button[] _offsetYValues = new Button[N];
        private readonly Button[] _resetButtons = new Button[N];
        private HashSet<string> _locked = new();

        private static string DefaultLabelName(int i) => $"标签{i + 1}";

        public void BuildUI(VBoxContainer parent)
        {
            (_fontSizeSlider, _fontSizeValue) = CreateSliderRow(parent, "默认字号", 0, 48, 0, 1);

            var styleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _boldCheck = new CheckButton { Text = "粗体", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _italicCheck = new CheckButton { Text = "斜体", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _shadowCheck = new CheckButton { Text = "阴影", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            styleRow.AddChild(_boldCheck);
            styleRow.AddChild(_italicCheck);
            styleRow.AddChild(_shadowCheck);
            parent.AddChild(styleRow);
            parent.AddChild(new HSeparator());

            for (int i = 0; i < N; i++)
                BuildLabelRow(parent, i);
        }

        private void BuildLabelRow(VBoxContainer parent, int index)
        {
            var collapsible = new CollapsibleContainer(DefaultLabelName(index), collapsed: true);
            _collapsibles[index] = collapsible;

            var header = collapsible.HeaderRow;
            _names[index] = new LineEdit
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                PlaceholderText = "标签名称"
            };
            header.AddChild(_names[index]);

            _texts[index] = new LineEdit
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                PlaceholderText = "标签内容"
            };
            header.AddChild(_texts[index]);

            _vis[index] = new CheckButton
            {
                ButtonPressed = true,
                TooltipText = "是否可见"
            };
            header.AddChild(_vis[index]);

            _resetButtons[index] = new Button
            {
                Text = "重置",
                CustomMinimumSize = new Vector2(48, 24)
            };
            header.AddChild(_resetButtons[index]);

            var content = collapsible.Content;

            _useGlobalFontChecks[index] = new CheckButton
            {
                Text = "使用全局字号",
                ButtonPressed = true,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            content.AddChild(_useGlobalFontChecks[index]);

            (_fontSizeSliders[index], _fontSizeValues[index]) = CreateSliderRow(content, "局部字号", 0, 48, 0, 1, 72);

            var offsetXRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            offsetXRow.AddChild(new Label { Text = "X偏移", CustomMinimumSize = new Vector2(52, 0) });
            _centerXChecks[index] = new CheckButton { Text = "居中", ButtonPressed = true };
            offsetXRow.AddChild(_centerXChecks[index]);
            _offsetXSliders[index] = CreateSlider(-150, 150, 0, 1);
            offsetXRow.AddChild(_offsetXSliders[index]);
            var offsetXValLbl = CreateValueLabel("0");
            offsetXRow.AddChild(offsetXValLbl);
            content.AddChild(offsetXRow);
            _offsetXValues[index] = SliderValueInput.Attach(_offsetXSliders[index], offsetXValLbl, v => ((int)v).ToString());

            var offsetYRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            offsetYRow.AddChild(new Label { Text = "Y偏移", CustomMinimumSize = new Vector2(52, 0) });
            _offsetYSliders[index] = CreateSlider(-150, 150, 0, 1);
            offsetYRow.AddChild(_offsetYSliders[index]);
            var offsetYValLbl = CreateValueLabel("0");
            offsetYRow.AddChild(offsetYValLbl);
            content.AddChild(offsetYRow);
            _offsetYValues[index] = SliderValueInput.Attach(_offsetYSliders[index], offsetYValLbl, v => ((int)v).ToString());

            _centerXChecks[index].Toggled += centered => ApplyCenterXState(index, centered);
            _useGlobalFontChecks[index].Toggled += useGlobal => ApplyGlobalFontState(index, useGlobal);

            parent.AddChild(collapsible);
        }

        public void SyncFromData(IComponentData data)
        {
            if (data is not LabelGroupData labels)
                return;

            SetSliderSilent(_fontSizeSlider, labels.DefaultFontSize, _fontSizeValue, labels.DefaultFontSize.ToString());
            SetCheckSilent(_boldCheck, labels.Bold);
            SetCheckSilent(_italicCheck, labels.Italic);
            SetCheckSilent(_shadowCheck, labels.Shadow);

            for (int i = 0; i < N; i++)
            {
                SetCheckSilent(_vis[i], labels.Visible[i]);
                _names[i].Text = labels.Names[i] ?? "";
                _texts[i].Text = labels.ContentPreview[i] ?? "";
                SetCheckSilent(_useGlobalFontChecks[i], labels.UseGlobalFontSize[i]);
                SetSliderSilent(_fontSizeSliders[i], labels.FontSizes[i], _fontSizeValues[i], labels.FontSizes[i].ToString());
                SetSliderSilent(_offsetXSliders[i], labels.XOffset[i], _offsetXValues[i], ((int)labels.XOffset[i]).ToString());
                SetCheckSilent(_centerXChecks[i], labels.CenterX[i]);
                SetSliderSilent(_offsetYSliders[i], labels.YOffset[i], _offsetYValues[i], ((int)labels.YOffset[i]).ToString());

                ApplyCenterXState(i, labels.CenterX[i]);
                ApplyGlobalFontState(i, labels.UseGlobalFontSize[i]);
            }

            _locked = new HashSet<string>(labels.LockedProperties);
            ApplyLocks();
        }

        public IComponentData SyncToData()
        {
            var labels = new LabelGroupData
            {
                DefaultFontSize = (int)_fontSizeSlider.Value,
                Bold = _boldCheck.ButtonPressed,
                Italic = _italicCheck.ButtonPressed,
                Shadow = _shadowCheck.ButtonPressed,
                LockedProperties = new HashSet<string>(_locked),
            };

            for (int i = 0; i < N; i++)
            {
                labels.Visible[i] = _vis[i].ButtonPressed;
                labels.Names[i] = _names[i].Text;
                labels.ContentPreview[i] = _texts[i].Text;
                labels.UseGlobalFontSize[i] = _useGlobalFontChecks[i].ButtonPressed;
                labels.FontSizes[i] = (int)_fontSizeSliders[i].Value;
                labels.XOffset[i] = (float)_offsetXSliders[i].Value;
                labels.CenterX[i] = _centerXChecks[i].ButtonPressed;
                labels.YOffset[i] = (float)_offsetYSliders[i].Value;
            }

            return labels;
        }

        public void ConnectSignals(Action onChanged)
        {
            _onChanged = onChanged;

            _fontSizeSlider.ValueChanged += OnValueChanged;
            _boldCheck.Toggled += OnToggleChanged;
            _italicCheck.Toggled += OnToggleChanged;
            _shadowCheck.Toggled += OnToggleChanged;

            for (int i = 0; i < N; i++)
            {
                _vis[i].Toggled += OnToggleChanged;
                _names[i].TextChanged += OnTextChanged;
                _names[i].FocusExited += OnFocusExited;
                _texts[i].TextChanged += OnTextChanged;
                _texts[i].FocusExited += OnFocusExited;
                _useGlobalFontChecks[i].Toggled += OnToggleChanged;
                _fontSizeSliders[i].ValueChanged += OnValueChanged;
                _offsetXSliders[i].ValueChanged += OnValueChanged;
                _centerXChecks[i].Toggled += OnToggleChanged;
                _offsetYSliders[i].ValueChanged += OnValueChanged;

                int capturedIndex = i;
                _resetButtons[i].Pressed += () => ResetLabel(capturedIndex);
            }
        }

        public void DisconnectSignals()
        {
            _fontSizeSlider.ValueChanged -= OnValueChanged;
            _boldCheck.Toggled -= OnToggleChanged;
            _italicCheck.Toggled -= OnToggleChanged;
            _shadowCheck.Toggled -= OnToggleChanged;

            for (int i = 0; i < N; i++)
            {
                _vis[i].Toggled -= OnToggleChanged;
                _names[i].TextChanged -= OnTextChanged;
                _names[i].FocusExited -= OnFocusExited;
                _texts[i].TextChanged -= OnTextChanged;
                _texts[i].FocusExited -= OnFocusExited;
                _useGlobalFontChecks[i].Toggled -= OnToggleChanged;
                _fontSizeSliders[i].ValueChanged -= OnValueChanged;
                _offsetXSliders[i].ValueChanged -= OnValueChanged;
                _centerXChecks[i].Toggled -= OnToggleChanged;
                _offsetYSliders[i].ValueChanged -= OnValueChanged;
            }
        }

        public void SyncFromEntity(EntityBase entity)
        {
            if (entity == null)
                return;

            for (int i = 0; i < N; i++)
            {
                SetCheckSilent(_vis[i], entity.GetLabelVisible(i));
                _texts[i].Text = entity.LabelTexts[i] ?? "";

                bool useGlobalFont = entity.LabelFontSizes[i] <= 0;
                SetCheckSilent(_useGlobalFontChecks[i], useGlobalFont);
                int labelFontSize = useGlobalFont ? entity.FontSize : entity.LabelFontSizes[i];
                SetSliderSilent(_fontSizeSliders[i], labelFontSize, _fontSizeValues[i], labelFontSize.ToString());

                SetSliderSilent(_offsetXSliders[i], entity.LabelXOffsets[i], _offsetXValues[i], ((int)entity.LabelXOffsets[i]).ToString());
                SetCheckSilent(_centerXChecks[i], entity.LabelCenterX[i]);
                SetSliderSilent(_offsetYSliders[i], entity.LabelYOffsets[i], _offsetYValues[i], ((int)entity.LabelYOffsets[i]).ToString());

                ApplyCenterXState(i, entity.LabelCenterX[i]);
                ApplyGlobalFontState(i, useGlobalFont);
            }

            if (entity is Player player)
            {
                SetCheckSilent(_boldCheck, player.FontBold);
                SetCheckSilent(_italicCheck, player.FontItalic);
                SetCheckSilent(_shadowCheck, player.FontShadow);
                SetSliderSilent(_fontSizeSlider, player.FontSizeOverride, _fontSizeValue, player.FontSizeOverride.ToString());
            }
            else
            {
                SetSliderSilent(_fontSizeSlider, entity.FontSize, _fontSizeValue, entity.FontSize.ToString());
            }
        }

        public void SetPropertyLocked(string propertyName, bool locked)
        {
            if (locked)
                _locked.Add(propertyName);
            else
                _locked.Remove(propertyName);
            ApplyLocks();
        }

        public void SetCollapsed(bool collapsed)
        {
            // Managed by DebugPanelEntityTab
        }

        public void Dispose()
        {
            DisconnectSignals();
        }

        private void ApplyLocks()
        {
            for (int i = 0; i < N; i++)
            {
                bool contentLocked = _locked.Contains($"content_{i}");
                _texts[i].Editable = !contentLocked;
                _texts[i].Modulate = contentLocked ? new Color(0.5f, 0.5f, 0.5f, 1) : Colors.White;
            }
        }

        private void ApplyCenterXState(int index, bool centered)
        {
            _offsetXSliders[index].Editable = !centered;
            _offsetXSliders[index].Modulate = centered ? new Color(0.5f, 0.5f, 0.5f, 1) : Colors.White;
            if (centered)
            {
                _offsetXSliders[index].SetBlockSignals(true);
                _offsetXSliders[index].Value = 0;
                _offsetXSliders[index].SetBlockSignals(false);
                _offsetXValues[index].Text = "0";
            }
        }

        private void ApplyGlobalFontState(int index, bool useGlobal)
        {
            _fontSizeSliders[index].Editable = !useGlobal;
            _fontSizeSliders[index].Modulate = useGlobal ? new Color(0.5f, 0.5f, 0.5f, 1) : Colors.White;
            _fontSizeValues[index].Modulate = useGlobal ? new Color(0.5f, 0.5f, 0.5f, 1) : Colors.White;
        }

        private void ResetLabel(int index)
        {
            _texts[index].Text = "";
            SetCheckSilent(_useGlobalFontChecks[index], true);
            SetSliderSilent(_fontSizeSliders[index], 0, _fontSizeValues[index], "0");
            SetSliderSilent(_offsetXSliders[index], 0, _offsetXValues[index], "0");
            SetCheckSilent(_centerXChecks[index], true);
            SetSliderSilent(_offsetYSliders[index], 0, _offsetYValues[index], "0");
            ApplyCenterXState(index, true);
            ApplyGlobalFontState(index, true);
            _onChanged?.Invoke();
        }

        private void OnValueChanged(double _)
        {
            _onChanged?.Invoke();
        }

        private void OnToggleChanged(bool _)
        {
            _onChanged?.Invoke();
        }

        private void OnTextChanged(string _)
        {
            _onChanged?.Invoke();
        }

        private void OnFocusExited()
        {
            _onChanged?.Invoke();
        }

        private static (HSlider slider, Button valueDisplay) CreateSliderRow(
            Container parent,
            string label,
            double min,
            double max,
            double value,
            double step,
            int labelWidth = 80)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(new Label { Text = label + ":", CustomMinimumSize = new Vector2(labelWidth, 0) });

            var slider = CreateSlider(min, max, value, step);
            row.AddChild(slider);

            Func<double, string> formatter = step < 1
                ? v => v.ToString(DebugPanelLengthScalePolicy.FormatStr)
                : v => ((int)v).ToString();

            var valueLabel = CreateValueLabel(formatter(value));
            row.AddChild(valueLabel);

            parent.AddChild(row);
            var valueDisplay = SliderValueInput.Attach(slider, valueLabel, formatter);
            return (slider, valueDisplay);
        }

        private static HSlider CreateSlider(double min, double max, double value, double step)
        {
            return new HSlider
            {
                MinValue = min,
                MaxValue = max,
                Value = value,
                Step = step,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 20),
                FocusMode = Control.FocusModeEnum.Click,
                Scrollable = false,
            };
        }

        private static Label CreateValueLabel(string text)
        {
            return new Label
            {
                Text = text,
                CustomMinimumSize = new Vector2(36, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
            };
        }

        private static void SetSliderSilent(HSlider slider, double value, Button valueLabel, string text)
        {
            slider?.SetBlockSignals(true);
            if (slider != null)
                slider.Value = value;
            slider?.SetBlockSignals(false);
            if (valueLabel != null)
                valueLabel.Text = text;
        }

        private static void SetCheckSilent(CheckButton checkButton, bool value)
        {
            checkButton?.SetBlockSignals(true);
            if (checkButton != null)
                checkButton.ButtonPressed = value;
            checkButton?.SetBlockSignals(false);
        }
    }
}
