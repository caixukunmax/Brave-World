using Godot;
using System;

namespace ClinetCSharp
{
    /// <summary>
    /// 滑条值输入工具 — 把 Label 替换为可点击的 Button，点击后变成 LineEdit 直接输入数值
    /// </summary>
    public static class SliderValueInput
    {
        private static LineEdit _activeEdit;
        private static Action _activeApply;

        /// <summary>
        /// 将 slider 旁边的值 Label 替换为可点击输入的 Button
        /// </summary>
        public static void Attach(HSlider slider, Label valueLabel, Func<double, string> formatValue = null)
        {
            if (slider == null || valueLabel == null) return;
            if (formatValue == null)
                formatValue = v => v.ToString(DebugPanelLengthScalePolicy.FormatStr);

            var clickBtn = new Button
            {
                Text = formatValue(slider.Value),
                CustomMinimumSize = valueLabel.CustomMinimumSize,
                SizeFlagsHorizontal = valueLabel.SizeFlagsHorizontal,
                Flat = true,
                FocusMode = Control.FocusModeEnum.Click,
                MouseFilter = Control.MouseFilterEnum.Stop,
                Name = valueLabel.Name + "_ClickBtn"
            };
            clickBtn.AddThemeConstantOverride("h_separation", 0);
            clickBtn.AddThemeConstantOverride("outline_size", 0);

            slider.ValueChanged += (v) =>
            {
                clickBtn.Text = formatValue(v);
            };

            var parent = valueLabel.GetParent();
            if (parent == null) return;
            int labelIndex = valueLabel.GetIndex();
            parent.RemoveChild(valueLabel);
            parent.AddChild(clickBtn);
            parent.MoveChild(clickBtn, labelIndex);

            LineEdit edit = null;
            bool applying = false;

            clickBtn.Pressed += () =>
            {
                if (edit != null) return;

                // 先关闭其他正在编辑的 LineEdit
                CloseActiveEdit();

                edit = new LineEdit
                {
                    Text = clickBtn.Text,
                    CustomMinimumSize = new Vector2(50, 0),
                    SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
                    PlaceholderText = clickBtn.Text,
                    FocusMode = Control.FocusModeEnum.Click
                };

                int btnIndex = clickBtn.GetIndex();
                parent.RemoveChild(clickBtn);
                parent.AddChild(edit);
                parent.MoveChild(edit, btnIndex);
                edit.GrabFocus();
                edit.SelectAll();

                void ApplyValue()
                {
                    if (applying) return;
                    applying = true;

                    if (double.TryParse(edit.Text, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double val))
                    {
                        val = Mathf.Clamp((float)val, (float)slider.MinValue, (float)slider.MaxValue);
                        slider.Value = val;
                    }

                    int editIndex = edit.GetIndex();
                    parent.RemoveChild(edit);
                    parent.AddChild(clickBtn);
                    parent.MoveChild(clickBtn, editIndex);
                    clickBtn.Text = formatValue(slider.Value);

                    if (_activeEdit == edit)
                    {
                        _activeEdit = null;
                        _activeApply = null;
                    }
                    edit = null;
                    applying = false;
                }

                _activeEdit = edit;
                _activeApply = ApplyValue;

                edit.TextSubmitted += (txt) => ApplyValue();
                edit.FocusExited += () => ApplyValue();
            };
        }

        /// <summary>
        /// 关闭当前正在编辑的 LineEdit
        /// </summary>
        public static void CloseActiveEdit()
        {
            if (_activeEdit != null && _activeApply != null)
            {
                _activeApply.Invoke();
            }
        }

        /// <summary>
        /// 在 SceneTree 的 _UnhandledInput 中调用，处理点击外部关闭编辑框
        /// </summary>
        public static void HandleUnhandledInput(InputEvent ev)
        {
            if (_activeEdit == null) return;
            if (ev is InputEventMouseButton mb && mb.Pressed)
            {
                // 点击的不是当前编辑框本身，关闭
                CloseActiveEdit();
            }
        }
    }
}
