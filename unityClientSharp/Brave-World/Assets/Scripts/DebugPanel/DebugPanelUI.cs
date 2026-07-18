using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UnityClientSharp.DebugPanel
{
    /// <summary>
    /// DebugPanel UGUI 构建助手：标签、滑块、开关、输入框、下拉、颜色、分组。
    /// 复用 DraggablePanel 同款中文字体逻辑（FontUtil），避免中文方块。
    /// </summary>
    public static class DebugPanelUI
    {
        public const float LabelWidth = 100f;

        public static RectTransform Row(RectTransform parent)
        {
            var go = new GameObject("Row", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4;
            hlg.padding = new RectOffset(2, 2, 1, 1);
            hlg.childControlWidth = true;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(0, 22);
            return rt;
        }

        public static TMP_Text Label(RectTransform parent, string text, float fontSize = 12)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(LabelWidth, 22);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = new Color(0.85f, 0.85f, 0.85f);
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.raycastTarget = false;
            UnityClientSharp.Map.Rendering.FontUtil.ApplyCjkFont(tmp);
            return tmp;
        }

        /// <summary>滑块绑定：支持静默赋值（SetSilent），避免 SyncFromData 时回环触发 onChanged。</summary>
        public class SliderBinding
        {
            public Slider Slider;
            public TMP_Text ValueText;
            public string Format;

            public void SetSilent(float v)
            {
                Slider.SetValueWithoutNotify(v);
                if (ValueText != null) ValueText.text = FormatValue(v);
            }

            public string FormatValue(float v)
            {
                if (Format == "int") return ((int)v).ToString();
                if (string.IsNullOrEmpty(Format)) return v.ToString("F2");
                return v.ToString(Format);
            }
        }

        public static SliderBinding AddSlider(RectTransform parent, string label, float min, float max,
            float value, string format, System.Action<float> onChanged)
        {
            var row = Row(parent);
            Label(row, label);

            var sliderGo = new GameObject("Slider", typeof(RectTransform));
            sliderGo.transform.SetParent(row, false);
            var srt = (RectTransform)sliderGo.transform;
            srt.sizeDelta = new Vector2(0, 20);
            var slider = sliderGo.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            if (format == "int") slider.wholeNumbers = true;
            SetupSliderVisual(slider);

            TMP_Text vt = null;
            if (onChanged != null || true)
            {
                var vGo = new GameObject("Val", typeof(RectTransform));
                vGo.transform.SetParent(row, false);
                var vrt = (RectTransform)vGo.transform;
                vrt.sizeDelta = new Vector2(48, 20);
                vt = vGo.AddComponent<TextMeshProUGUI>();
                vt.fontSize = 11;
                vt.color = new Color(0.9f, 0.9f, 0.7f);
                vt.alignment = TextAlignmentOptions.Right;
                vt.raycastTarget = false;
                UnityClientSharp.Map.Rendering.FontUtil.ApplyCjkFont(vt);
            }

            var binding = new SliderBinding { Slider = slider, ValueText = vt, Format = format };
            if (vt != null) vt.text = binding.FormatValue(value);
            if (onChanged != null)
                slider.onValueChanged.AddListener(v =>
                {
                    if (vt != null) vt.text = binding.FormatValue(v);
                    onChanged(v);
                });
            return binding;
        }

        private static void SetupSliderVisual(Slider slider)
        {
            var bg = new GameObject("Bg", typeof(RectTransform));
            bg.transform.SetParent(slider.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.25f, 0.25f, 0.3f);
            var bgRt = (RectTransform)bg.transform;
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
            slider.targetGraphic = bgImg;

            var fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(slider.transform, false);
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.3f, 0.6f, 0.9f);
            slider.fillRect = (RectTransform)fill.transform;

            var handle = new GameObject("Handle", typeof(RectTransform));
            handle.transform.SetParent(slider.transform, false);
            var handleImg = handle.AddComponent<Image>();
            handleImg.color = Color.white;
            slider.handleRect = (RectTransform)handle.transform;
        }

        public static Toggle AddToggle(RectTransform parent, string label, bool value, System.Action<bool> onChanged)
        {
            var row = Row(parent);
            Label(row, label);
            var go = new GameObject("Toggle", typeof(RectTransform));
            go.transform.SetParent(row, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(20, 20);
            var toggle = go.AddComponent<Toggle>();
            toggle.isOn = value;
            if (onChanged != null) toggle.onValueChanged.AddListener(v => onChanged(v));
            return toggle;
        }

        public static TMP_InputField AddInput(RectTransform parent, string label, string value, System.Action<string> onChanged)
        {
            var row = Row(parent);
            Label(row, label);
            var go = new GameObject("Input", typeof(RectTransform));
            go.transform.SetParent(row, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(0, 22);
            var input = go.AddComponent<TMP_InputField>();
            input.text = value ?? "";
            var txtGo = new GameObject("Text", typeof(RectTransform));
            txtGo.transform.SetParent(go.transform, false);
            var txtRt = (RectTransform)txtGo.transform;
            txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = new Vector2(4, 2); txtRt.offsetMax = new Vector2(-4, -2);
            var txt = txtGo.AddComponent<TextMeshProUGUI>();
            txt.fontSize = 12; txt.color = Color.white;
            UnityClientSharp.Map.Rendering.FontUtil.ApplyCjkFont(txt);
            input.textComponent = txt;
            if (onChanged != null) input.onEndEdit.AddListener(v => onChanged(v));
            return input;
        }

        public static TMP_Dropdown AddDropdown(RectTransform parent, string label, string[] options, int value, System.Action<int> onChanged)
        {
            var row = Row(parent);
            Label(row, label);
            var go = new GameObject("Dropdown", typeof(RectTransform));
            go.transform.SetParent(row, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(0, 22);
            var dd = go.AddComponent<TMP_Dropdown>();
            dd.options = new System.Collections.Generic.List<TMP_Dropdown.OptionData>();
            foreach (var o in options) dd.options.Add(new TMP_Dropdown.OptionData(o));
            dd.value = value;
            if (onChanged != null) dd.onValueChanged.AddListener(v => onChanged(v));
            return dd;
        }

        /// <summary>颜色字段绑定：含 SetSilent 以便 SyncFromData 还原（不触发 onChanged）。</summary>
        public class ColorBinding
        {
            private readonly SliderBinding[] _sliders = new SliderBinding[4];
            private readonly float[] _ch = new float[4];
            private System.Action<Color> _onChange;

            public void Attach(int idx, SliderBinding b, System.Action<Color> onChange)
            {
                _sliders[idx] = b;
                _onChange = onChange;
            }

            public void SetSilent(Color c)
            {
                _ch[0] = c.r; _ch[1] = c.g; _ch[2] = c.b; _ch[3] = c.a;
                _sliders[0].SetSilent(c.r);
                _sliders[1].SetSilent(c.g);
                _sliders[2].SetSilent(c.b);
                _sliders[3].SetSilent(c.a);
            }

            public Color GetValue() => new Color(_ch[0], _ch[1], _ch[2], _ch[3]);

            public void Notify(int idx, float v)
            {
                _ch[idx] = v;
                _onChange?.Invoke(GetValue());
            }
        }

        /// <summary>颜色字段：用 R/G/B/A 四个滑块表示（SDF 资源无编辑器预烘焙，运行时动态）。返回绑定以支持静默回填。</summary>
        public static ColorBinding AddColorField(RectTransform parent, string label, Color value, System.Action<Color> onChange)
        {
            var section = Section(parent, label);
            var binding = new ColorBinding();
            float[] ch = { value.r, value.g, value.b, value.a };
            string[] names = { "R", "G", "B", "A" };
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var sb = AddSlider(section, names[idx], 0, 1, ch[idx], "F2", v => binding.Notify(idx, v));
                binding.Attach(idx, sb, onChange);
            }
            return binding;
        }

        /// <summary>分组容器（垂直布局），可带标题；返回其 RectTransform 供继续 Add*。</summary>
        public static RectTransform Section(RectTransform parent, string title)
        {
            var wrap = new GameObject("Section:" + (title ?? ""), typeof(RectTransform));
            wrap.transform.SetParent(parent, false);
            var vlg = wrap.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2;
            vlg.padding = new RectOffset(4, 4, 2, 2);
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var rt = (RectTransform)wrap.transform;
            rt.sizeDelta = new Vector2(0, 0);
            if (!string.IsNullOrEmpty(title))
            {
                var h = Label(rt, title, 12);
                h.color = new Color(0.9f, 0.8f, 0.5f);
            }
            return rt;
        }

        /// <summary>按钮行。</summary>
        public static Button AddButton(RectTransform parent, string text, System.Action onClick)
        {
            var row = Row(parent);
            var go = new GameObject("Btn", typeof(RectTransform));
            go.transform.SetParent(row, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(0, 22);
            var btn = go.AddComponent<Button>();
            var img = go.AddComponent<Image>();
            img.color = new Color(0.3f, 0.4f, 0.55f);
            btn.targetGraphic = img;
            var tGo = new GameObject("Text", typeof(RectTransform));
            tGo.transform.SetParent(go.transform, false);
            var tRt = (RectTransform)tGo.transform;
            tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
            tRt.offsetMin = tRt.offsetMax = Vector2.zero;
            var tmp = tGo.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = 12; tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            UnityClientSharp.Map.Rendering.FontUtil.ApplyCjkFont(tmp);
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }
    }
}
