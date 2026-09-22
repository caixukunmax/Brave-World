using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ClinetCSharp
{
    /// <summary>
    /// 滑条值输入工具 — 把 Label 替换为可点击的 Button，点击后变成 LineEdit 直接输入数值。
    ///
    /// <para><b>关键约定（防止“滑条值与后方文本失同步”类 bug）：</b></para>
    /// <para><see cref="Attach"/> 会用一个新的 Button 替换你传入的 valueLabel，该 Button 才是 UI 上
    /// 真正显示数值的控件，并且它在点击编辑结束后会被原样放回（引用不变）。
    /// 因此组件<b>必须持有并使用 Attach 返回的这个 Button 作为“数值显示”</b>，
    /// 切勿再保留或更新被替换掉的原始 Label——否则会写到一个已脱离界面的节点上，
    /// 导致显示值与滑条实际值脱节（典型现象：显示 111，点一下滑条没拖动，值却变了）。</para>
    /// <para>各 CreateSliderRow / MakeSR 之类辅助方法已改为返回 (HSlider, Button)，请直接使用返回的 Button。</para>
    /// </summary>
    public static class SliderValueInput
    {
        private static LineEdit _activeEdit;
        private static Action _activeApply;

        /// <summary>当前是否有活跃的编辑框</summary>
        public static bool HasActiveEdit => _activeEdit != null;

        /// <summary>
        /// 将 slider 旁边的值 Label 替换为可点击输入的 Button，并返回该 Button（即 UI 上真正显示数值的控件）。
        /// 组件应当持有并使用这个返回的 Button，而不是被替换掉的原始 Label。
        /// </summary>
        public static Button Attach(HSlider slider, Label valueLabel, Func<double, string> formatValue = null)
        {
            if (slider == null || valueLabel == null) return null;
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

            // 用户拖动/点击滑条时，始终用同一来源刷新可见 Button 文本
            slider.ValueChanged += (v) =>
            {
                clickBtn.Text = formatValue(v);
            };

            var parent = valueLabel.GetParent();
            if (parent == null) return clickBtn;
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

                    if (double.TryParse(edit.Text, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out double val))
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
                edit.FocusExited += () =>
                {
                    // 延迟检查：如果 _Input 已经先 Apply 了，edit 会变成 null，直接跳过
                    if (edit == null) return;
                    ApplyValue();
                };
            };

            return clickBtn;
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
        /// 在 DebugPanel._Input 中调用，处理点击外部关闭编辑框。
        /// 点击 LineEdit 自身不关闭；点击其他任何位置都关闭并应用值。
        /// </summary>
        public static void HandleInput(InputEvent ev)
        {
            if (_activeEdit == null || _activeApply == null) return;
            if (ev is InputEventMouseButton mb && mb.Pressed)
            {
                // 点击在 LineEdit 内部 → 不关闭
                var editRect = _activeEdit.GetGlobalRect();
                if (editRect.HasPoint(mb.GlobalPosition)) return;

                // 点击在 LineEdit 外部 → 关闭并应用
                _activeApply.Invoke();
            }
        }

        /// <summary>
        /// 旧接口，保留兼容。现在内部转发到 HandleInput。
        /// </summary>
        public static void HandleUnhandledInput(InputEvent ev)
        {
            HandleInput(ev);
        }
    }
}
