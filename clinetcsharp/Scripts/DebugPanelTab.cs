using Godot;
using System;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// DebugPanel tab base class — each tab independently manages its controls, events, and config.
    /// Subclasses: DebugPanelMapTab, DebugPanelPlayerTab, DebugPanelMonsterTab, DebugPanelSystemTab, DebugPanelUITab
    /// </summary>
    public abstract class DebugPanelTab
    {
        internal DebugPanel Owner { get; }

        /// <summary>slider → AttachValueLineEdit 创建的 Button，用于外部更新文本</summary>
        private readonly Dictionary<HSlider, Button> _attachedValueButtons = new();

        protected DebugPanelTab(DebugPanel owner)
        {
            Owner = owner;
        }

        // Convenience accessors — avoid writing Owner._player in every handler
        protected Player Player => Owner._player;
        protected MonsterManager MonsterManager => Owner._monsterManager;
        protected NpcManager NpcManager => Owner._npcManager;
        protected GridManager GridManager => Owner._gridManager;
        protected CameraController Camera => Owner._camera;

        /// <summary>Unique key for this tab, used in config/undo dictionaries</summary>
        public abstract string TabKey { get; }

        /// <summary>Create all controls for this tab and add to tabContainer</summary>
        public abstract void BuildUI(VBoxContainer tabContainer);

        /// <summary>Connect all event subscriptions</summary>
        public abstract void ConnectSignals();

        /// <summary>Disconnect all event subscriptions</summary>
        public abstract void DisconnectSignals();

        /// <summary>Write current state to ConfigFile</summary>
        public abstract void SaveConfig(ConfigFile cfg);

        /// <summary>Restore state from ConfigFile</summary>
        public abstract void LoadConfig(ConfigFile cfg, bool configLoaded);

        /// <summary>When panel opens, sync sliders to current values</summary>
        public abstract void SyncToCurrentValues();

        /// <summary>Capture current undo state into a new dictionary</summary>
        public abstract Godot.Collections.Dictionary CaptureUndoState();

        /// <summary>Apply undo state from dictionary</summary>
        public abstract void ApplyUndoState(Godot.Collections.Dictionary state);

        /// <summary>Export config data for JSON output (optional, returns null by default)</summary>
        public virtual Godot.Collections.Dictionary ExportConfigData() => null;

        #region Shared Helpers — slider row creation
        protected (HSlider slider, Label valueLabel) CreateSliderRow(
            Container parent, string label, float min, float max, float def, float step = -1f)
        {
            float actualStep = step > 0 ? step : (max <= 1.0f ? 0.05f : 1f);
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(new Label { Text = label + ":", CustomMinimumSize = new Vector2(80, 0) });

            var slider = new HSlider
            {
                MinValue = min, MaxValue = max, Value = def,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 20), Step = actualStep,
                FocusMode = Control.FocusModeEnum.Click,
                Scrollable = false
            };
            row.AddChild(slider);

            string initialText = actualStep < 1.0f ? def.ToString("F1") : ((int)def).ToString();
            var valLbl = new Label { Text = initialText, CustomMinimumSize = new Vector2(36, 0) };
            row.AddChild(valLbl);

            slider.ValueChanged += (v) => valLbl.Text = (actualStep < 1.0f ? v.ToString("F1") : ((int)v).ToString());
            parent.AddChild(row);
            return (slider, valLbl);
        }

        protected (HSlider slider, Label valueLabel) CreateMonsterSliderRow(
            Container parent, string label, float min, float max, float def, float? step = null)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(new Label { Text = label + ":", CustomMinimumSize = new Vector2(80, 0) });

            var slider = new HSlider
            {
                MinValue = min, MaxValue = max, Value = def,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 20),
                Step = step ?? (max <= 1 ? 0.05f : 1f),
                FocusMode = Control.FocusModeEnum.Click,
                Scrollable = false
            };
            row.AddChild(slider);

            var valLbl = new Label { Text = def.ToString("F1"), CustomMinimumSize = new Vector2(36, 0) };
            row.AddChild(valLbl);

            slider.ValueChanged += (v) => valLbl.Text = (max <= 1 ? v.ToString("F2") : ((int)v).ToString());
            parent.AddChild(row);
            return (slider, valLbl);
        }

        /// <summary>
        /// 创建血条/MP条样式的滑条行（标签:滑条:值），值标签可点击编辑。
        /// formatValue 为 null 时默认用 F3 格式。
        /// centerCheck 非 null 时会在值标签后添加居中按钮，并绑定联动逻辑。
        /// </summary>
        protected (HSlider slider, Label valueLabel) CreateBarSliderRow(
            Container parent, string label, double min, double max, double def,
            double? step = null, Func<double, string> formatValue = null, int labelMinWidth = 60,
            CheckButton centerCheck = null)
        {
            if (formatValue == null)
                formatValue = v => v.ToString(DebugPanelLengthScalePolicy.FormatStr);

            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(labelMinWidth, 0) });

            var slider = new HSlider
            {
                MinValue = min, MaxValue = max, Value = def,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 20),
                Step = step ?? DebugPanelLengthScalePolicy.StepF,
                FocusMode = Control.FocusModeEnum.Click,
                Scrollable = false
            };
            row.AddChild(slider);

            var valLbl = new Label { Text = formatValue(def), CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            row.AddChild(valLbl);

            if (centerCheck != null)
            {
                row.AddChild(centerCheck);
                centerCheck.Toggled += (centered) =>
                {
                    slider.Editable = !centered;
                    slider.Modulate = centered ? new Color(0.5f, 0.5f, 0.5f, 1) : new Color(1, 1, 1, 1);
                    if (centered)
                    {
                        slider.SetBlockSignals(true);
                        slider.Value = 0;
                        slider.SetBlockSignals(false);
                        UpdateAttachedValue(slider, "0");
                    }
                };
                // 初始状态同步
                if (centerCheck.ButtonPressed)
                {
                    slider.Editable = false;
                    slider.Modulate = new Color(0.5f, 0.5f, 0.5f, 1);
                }
            }

            AttachValueLineEdit(slider, valLbl, formatValue);
            parent.AddChild(row);
            return (slider, valLbl);
        }

        /// <summary>
        /// 创建只读值行（标签:值），用于显示计算结果如长度/高度。
        /// </summary>
        protected Label CreateBarReadOnlyRow(Container parent, string label, string initialValue)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(60, 0) });
            var valLbl = new Label { Text = initialValue, CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            row.AddChild(valLbl);
            parent.AddChild(row);
            return valLbl;
        }

        protected SpinBox CreateSpinBox(double minV, double maxV, double step, double value, int width)
        {
            var spin = new SpinBox
            {
                MinValue = minV, MaxValue = maxV, Step = step, Value = value,
                CustomMinimumSize = new Vector2(width, 0)
            };
            return spin;
        }
        #endregion

        #region Value Line Edit — click value label to type precise number
        /// <summary>
        /// 单击值标签时，将标签替换为 LineEdit，允许直接输入精确数值。
        /// 回车确认后同步回 slider；Escape 或失焦取消。
        /// </summary>
        internal void AttachValueLineEdit(HSlider slider, Label valueLabel, Func<double, string> formatValue = null)
        {
            if (formatValue == null)
                formatValue = v => v.ToString(DebugPanelLengthScalePolicy.FormatStr);

            // 创建一个可点击的 Button 覆盖在 Label 位置
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

            // 同步滑条值到 Button
            slider.ValueChanged += (v) =>
            {
                clickBtn.Text = formatValue(v);
            };

            // 替换 Label 为 Button（同位置同索引）
            var parent = valueLabel.GetParent();
            if (parent == null) return;
            int labelIndex = valueLabel.GetIndex();
            parent.RemoveChild(valueLabel);
            parent.AddChild(clickBtn);
            parent.MoveChild(clickBtn, labelIndex);

            _attachedValueButtons[slider] = clickBtn;

            LineEdit edit = null;
            bool applying = false;

            clickBtn.Pressed += () =>
            {
                if (edit != null) return;

                edit = new LineEdit
                {
                    Text = clickBtn.Text,
                    CustomMinimumSize = new Vector2(50, 0),
                    SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
                    PlaceholderText = clickBtn.Text,
                    FocusMode = Control.FocusModeEnum.Click
                };

                // 替换 Button 为 LineEdit
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

                    // 恢复 Button，用三位小数格式化显示
                    int editIndex = edit.GetIndex();
                    parent.RemoveChild(edit);
                    parent.AddChild(clickBtn);
                    parent.MoveChild(clickBtn, editIndex);
                    clickBtn.Text = formatValue(slider.Value);
                    edit = null;
                    applying = false;
                }

                edit.TextSubmitted += (txt) => ApplyValue();
                edit.FocusExited += () => ApplyValue();

                // 注册全局输入回调：点击 LineEdit 外部时取消编辑
                Owner._inputCallback = (evt) =>
                {
                    if (edit == null || applying) return;
                    if (evt is InputEventMouseButton mb && mb.Pressed)
                    {
                        var editRect = edit.GetGlobalRect();
                        if (!editRect.HasPoint(mb.GlobalPosition))
                        {
                            Owner._inputCallback = null;
                            ApplyValue();
                        }
                    }
                };
            };
        }

        /// <summary>
        /// 更新 AttachValueLineEdit 创建的 Button 文本。
        /// 用于外部代码（如联动回调）需要手动更新显示值的场景。
        /// </summary>
        internal void UpdateAttachedValue(HSlider slider, string text)
        {
            if (_attachedValueButtons.TryGetValue(slider, out var btn))
                btn.Text = text;
        }
        #endregion
    }
}