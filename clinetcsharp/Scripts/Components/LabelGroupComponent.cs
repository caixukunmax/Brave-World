using Godot;
using System;
using System.Collections.Generic;

namespace ClinetCSharp
{
    public class LabelGroupComponent : IEntityTabComponent
    {
        private const int N = 4;
        public string ComponentName => "labels";
        public string DisplayName => "标签";
        public Type DataType => typeof(LabelGroupData);

        private Action _onChanged;

        private OptionButton _fontOption;
        private Button _loadFontBtn;
        private HSlider _fontSizeSlider;
        private Label _fontSizeValue;
        private CheckButton _boldCheck, _italicCheck, _shadowCheck;
        private ColorPickerButton _defaultColorPicker;

        private CollapsibleContainer[] _collapsibles = new CollapsibleContainer[N];
        private CheckButton[] _vis = new CheckButton[N];
        private LineEdit[] _names = new LineEdit[N];
        private LineEdit[] _texts = new LineEdit[N];
        private HSlider[] _fss = new HSlider[N];
        private Label[] _fsv = new Label[N];
        private Button[] _cols = new Button[N];
        private HSlider[] _oxs = new HSlider[N];
        private Label[] _oxv = new Label[N];
        private CheckButton[] _cxs = new CheckButton[N];
        private HSlider[] _oys = new HSlider[N];
        private Label[] _oyv = new Label[N];
        private Button[] _resets = new Button[N];
        private HashSet<string> _locked = new();

        static readonly Color[] Palette = { Colors.Black, Colors.Red, Colors.Blue, Colors.Green, Colors.Yellow, Colors.Cyan, Colors.Magenta, Colors.White };

        /// <summary>获取标签默认名称</summary>
        private static string DefaultLabelName(int i) => $"标签{i + 1}";

        /// <summary>获取标签显示名称(如果用户设了名称就用用户的,否则用默认)</summary>
        private string LabelDisplayName(int i) => string.IsNullOrEmpty(_names[i]?.Text) ? DefaultLabelName(i) : _names[i].Text;

        public void BuildUI(VBoxContainer parent)
        {
            var fr = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            fr.AddChild(new Label { Text = "字体:", CustomMinimumSize = new Vector2(40, 0) });
            _fontOption = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            fr.AddChild(_fontOption);
            _loadFontBtn = new Button { Text = "加载", CustomMinimumSize = new Vector2(60, 26) };
            fr.AddChild(_loadFontBtn);
            parent.AddChild(fr);

            (_fontSizeSlider, _fontSizeValue) = SR(parent, "默认字号", 0, 48, 0, 1);

            var cr = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            cr.AddChild(new Label { Text = "默认颜色:", CustomMinimumSize = new Vector2(70, 0) });
            _defaultColorPicker = new ColorPickerButton { CustomMinimumSize = new Vector2(60, 26) };
            cr.AddChild(_defaultColorPicker);
            parent.AddChild(cr);

            var sr = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _boldCheck = new CheckButton { Text = "加粗", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _italicCheck = new CheckButton { Text = "斜体", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _shadowCheck = new CheckButton { Text = "阴影", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            sr.AddChild(_boldCheck); sr.AddChild(_italicCheck); sr.AddChild(_shadowCheck);
            parent.AddChild(sr);
            parent.AddChild(new HSeparator());

            for (int i = 0; i < N; i++) BuildRow(parent, i);
        }

        void BuildRow(VBoxContainer c, int i)
        {
            // 每个标签用 CollapsibleContainer 包裹
            var collapsible = new CollapsibleContainer(DefaultLabelName(i), collapsed: true);
            _collapsibles[i] = collapsible;

            // 标题行：名称(左半) + 内容(右半) + 可见性 + 颜色 + 重置
            var header = collapsible.HeaderRow;
            _names[i] = new LineEdit { CustomMinimumSize = new Vector2(40, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, PlaceholderText = "名称" }; // ratio 1
            header.AddChild(_names[i]);
            _texts[i] = new LineEdit { CustomMinimumSize = new Vector2(40, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, PlaceholderText = "内容" }; // ratio 1
            header.AddChild(_texts[i]);
            _vis[i] = new CheckButton { ButtonPressed = true, TooltipText = "可见" }; 
            header.AddChild(_vis[i]);
            _cols[i] = new Button { Text = "色", CustomMinimumSize = new Vector2(30, 24) }; 
            _cols[i].Modulate = Colors.Black; 
            header.AddChild(_cols[i]);
            _resets[i] = new Button { Text = "重置", CustomMinimumSize = new Vector2(40, 24) }; 
            header.AddChild(_resets[i]);

            // 内容区（折叠后隐藏的详细设置）
            var content = collapsible.Content;

            (_fss[i], _fsv[i]) = SR(content, "字号", 0, 48, 0, 1, 35);

            var oxr = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            oxr.AddChild(new Label { Text = "X偏移", CustomMinimumSize = new Vector2(45, 0) });
            _cxs[i] = new CheckButton { Text = "居中", ButtonPressed = true };
            oxr.AddChild(_cxs[i]);
            _oxs[i] = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = -150, MaxValue = 150, Step = 1, Value = 0, Scrollable = false, FocusMode = Control.FocusModeEnum.Click };
            oxr.AddChild(_oxs[i]);
            _oxv[i] = new Label { Text = "0", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            oxr.AddChild(_oxv[i]);
            content.AddChild(oxr);

            var oyr = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            oyr.AddChild(new Label { Text = "Y偏移", CustomMinimumSize = new Vector2(45, 0) });
            _oys[i] = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = -150, MaxValue = 150, Step = 1, Value = 0, Scrollable = false, FocusMode = Control.FocusModeEnum.Click };
            oyr.AddChild(_oys[i]);
            _oyv[i] = new Label { Text = "0", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            oyr.AddChild(_oyv[i]);
            content.AddChild(oyr);

            _cxs[i].Toggled += (centered) =>
            {
                _oxs[i].Editable = !centered;
                _oxs[i].Modulate = centered ? new Color(0.5f, 0.5f, 0.5f, 1) : new Color(1, 1, 1, 1);
                if (centered) { _oxs[i].SetBlockSignals(true); _oxs[i].Value = 0; _oxs[i].SetBlockSignals(false); _oxv[i].Text = "0"; }
            };

            // 名称输入框只是注释，不影响标题

            c.AddChild(collapsible);
        }

        /// <summary>LabelDisplayName 不再用于标题，标题固定为"标签N"</summary>
        private static string FixedTitle(int i) => DefaultLabelName(i);

        public void SyncFromData(IComponentData data)
        {
            if (data is not LabelGroupData d) return;
            SS(_fontSizeSlider, d.DefaultFontSize, _fontSizeValue, d.DefaultFontSize.ToString());
            _defaultColorPicker.Color = d.DefaultColor;
            SC(_boldCheck, d.Bold); SC(_italicCheck, d.Italic); SC(_shadowCheck, d.Shadow);
            for (int i = 0; i < N; i++)
            {
                SC(_vis[i], d.Visible[i]);
                _names[i].Text = d.Names[i] ?? "";
                _texts[i].Text = d.ContentPreview[i] ?? "";
                SS(_fss[i], d.FontSizes[i], _fsv[i], d.FontSizes[i].ToString());
                _cols[i].Modulate = d.ColorPreview[i];
                SS(_oxs[i], d.XOffset[i], _oxv[i], ((int)d.XOffset[i]).ToString());
                SC(_cxs[i], d.CenterX[i]); _oxs[i].Editable = !d.CenterX[i];
                _oxs[i].Modulate = d.CenterX[i] ? new Color(0.5f, 0.5f, 0.5f, 1) : new Color(1, 1, 1, 1);
                SS(_oys[i], d.YOffset[i], _oyv[i], ((int)d.YOffset[i]).ToString());
            }
            _locked = new HashSet<string>(d.LockedProperties); ApplyLocks();
        }

        public IComponentData SyncToData()
        {
            var d = new LabelGroupData
            {
                DefaultFontSize = (int)_fontSizeSlider.Value, DefaultColor = _defaultColorPicker.Color,
                Bold = _boldCheck.ButtonPressed, Italic = _italicCheck.ButtonPressed, Shadow = _shadowCheck.ButtonPressed,
                LockedProperties = new HashSet<string>(_locked),
            };
            for (int i = 0; i < N; i++)
            {
                d.Visible[i] = _vis[i].ButtonPressed; d.Names[i] = _names[i].Text; d.ContentPreview[i] = _texts[i].Text;
                d.FontSizes[i] = (int)_fss[i].Value; d.ColorPreview[i] = _cols[i].Modulate;
                d.XOffset[i] = (float)_oxs[i].Value; d.CenterX[i] = _cxs[i].ButtonPressed;
                d.YOffset[i] = (float)_oys[i].Value;
            }
            return d;
        }

        public void ConnectSignals(Action onChanged)
        {
            _onChanged = onChanged;
            _fontSizeSlider.ValueChanged += D; _defaultColorPicker.ColorChanged += C;
            _boldCheck.Toggled += B; _italicCheck.Toggled += B; _shadowCheck.Toggled += B;
            for (int i = 0; i < N; i++)
            {
                int idx = i;
                _vis[i].Toggled += B; _names[i].TextChanged += S; _texts[i].TextChanged += S;
                _fss[i].ValueChanged += D; _cols[i].Pressed += () => ColorPressed(idx);
                _oxs[i].ValueChanged += D; _cxs[i].Toggled += B; _oys[i].ValueChanged += D;
                _resets[i].Pressed += () => ResetPressed(idx);
            }
        }

        public void DisconnectSignals()
        {
            _fontSizeSlider.ValueChanged -= D; _defaultColorPicker.ColorChanged -= C;
            _boldCheck.Toggled -= B; _italicCheck.Toggled -= B; _shadowCheck.Toggled -= B;
            for (int i = 0; i < N; i++)
            {
                _vis[i].Toggled -= B; _names[i].TextChanged -= S; _texts[i].TextChanged -= S;
                _fss[i].ValueChanged -= D; _oxs[i].ValueChanged -= D; _cxs[i].Toggled -= B; _oys[i].ValueChanged -= D;
            }
        }

        public void SyncFromEntity(EntityBase e)
        {
            if (e == null) return;
            for (int i = 0; i < N; i++)
            {
                SC(_vis[i], e.GetLabelVisible(i)); _texts[i].Text = e.LabelTexts[i] ?? "";
                SS(_fss[i], e.LabelFontSizes[i], _fsv[i], e.LabelFontSizes[i].ToString());
                SS(_oxs[i], e.LabelXOffsets[i], _oxv[i], ((int)e.LabelXOffsets[i]).ToString());
                SC(_cxs[i], e.LabelCenterX[i]); _oxs[i].Editable = !e.LabelCenterX[i];
                _oxs[i].Modulate = e.LabelCenterX[i] ? new Color(0.5f, 0.5f, 0.5f, 1) : new Color(1, 1, 1, 1);
                SS(_oys[i], e.LabelYOffsets[i], _oyv[i], ((int)e.LabelYOffsets[i]).ToString());
            }
            if (e is Player p) { SC(_boldCheck, p.FontBold); SC(_italicCheck, p.FontItalic); SC(_shadowCheck, p.FontShadow); }
            SS(_fontSizeSlider, e.FontSize, _fontSizeValue, e.FontSize.ToString());
        }

        public void SetPropertyLocked(string p, bool l) { if (l) _locked.Add(p); else _locked.Remove(p); ApplyLocks(); }
        void ApplyLocks()
        {
            for (int i = 0; i < N; i++)
            {
                bool cl = _locked.Contains($"content_{i}"), cll = _locked.Contains($"color_{i}");
                if (_texts[i] != null) { _texts[i].Editable = !cl; _texts[i].Modulate = cl ? new Color(0.5f, 0.5f, 0.5f, 1) : Colors.White; }
                if (_cols[i] != null) { _cols[i].Disabled = cll; if (cll) _cols[i].Modulate = new Color(0.5f, 0.5f, 0.5f, 1); }
            }
        }

        public void SetCollapsed(bool c) { /* CollapsibleContainer managed by DebugPanelEntityTab */ }
        public void Dispose() { DisconnectSignals(); }

        void D(double _) => _onChanged?.Invoke();
        void B(bool _) => _onChanged?.Invoke();
        void C(Color _) => _onChanged?.Invoke();
        void S(string _) => _onChanged?.Invoke();

        void ColorPressed(int i)
        {
            var cur = _cols[i].Modulate; int next = 0;
            for (int j = 0; j < Palette.Length; j++) if (cur.IsEqualApprox(Palette[j])) { next = (j + 1) % Palette.Length; break; }
            _cols[i].Modulate = Palette[next]; _onChanged?.Invoke();
        }

        void ResetPressed(int i)
        {
            _texts[i].Text = ""; _fss[i].Value = 0; _fsv[i].Text = "0";
            _oxs[i].Value = 0; _oxv[i].Text = "0"; _oys[i].Value = 0; _oyv[i].Text = "0";
            _onChanged?.Invoke();
        }

        static (HSlider, Label) SR(Container p, string lbl, double min, double max, double def, double step, int lw = 80)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(new Label { Text = lbl + ":", CustomMinimumSize = new Vector2(lw, 0) });
            var s = new HSlider { MinValue = min, MaxValue = max, Value = def, Step = step, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 20), FocusMode = Control.FocusModeEnum.Click, Scrollable = false };
            row.AddChild(s);
            Func<double, string> fmt = step < 1 ? (v => v.ToString(DebugPanelLengthScalePolicy.FormatStr)) : (v => ((int)v).ToString());
            var v = new Label { Text = fmt(def), CustomMinimumSize = new Vector2(36, 0) };
            row.AddChild(v);
            s.ValueChanged += (val) => v.Text = fmt(val);
            p.AddChild(row);
            SliderValueInput.Attach(s, v, fmt);
            return (s, v);
        }

        static void SS(HSlider s, double v, Label l, string t) { s?.SetBlockSignals(true); if (s != null) s.Value = v; s?.SetBlockSignals(false); if (l != null) l.Text = t; }
        static void SC(CheckButton c, bool v) { c?.SetBlockSignals(true); if (c != null) c.ButtonPressed = v; c?.SetBlockSignals(false); }
    }
}