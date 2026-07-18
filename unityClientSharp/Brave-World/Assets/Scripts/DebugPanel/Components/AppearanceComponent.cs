using UnityEngine;
using UnityEngine.UI;
using UnityClientSharp.DebugPanel;
using UnityClientSharp.Entity;

namespace UnityClientSharp.DebugPanel.Components
{
    /// <summary>外观组件控件（UGUI 版，移植自 Godot AppearanceComponent）。</summary>
    public class AppearanceComponent : IEntityTabComponent
    {
        public string ComponentName => "appearance";
        public string DisplayName => "外观";
        public System.Type DataType => typeof(AppearanceData);

        private RectTransform _root;
        private DebugPanelUI.SliderBinding _sizeScale, _corner, _bgOp, _font, _sx, _sy, _borderWS;
        private DebugPanelUI.ColorBinding _border, _bg, _text;
        private System.Action _onChanged;
        private bool _suppress;

        public void BuildUI(Transform parent)
        {
            _root = DebugPanelUI.Section((RectTransform)parent, "外观");
            _sizeScale = DebugPanelUI.AddSlider(_root, "角色比例", 0.1f, 1f, 1f, "F2", _ => Changed());
            _borderWS = DebugPanelUI.AddSlider(_root, "边框比例", 0f, 0.2f, 3f / 111f, "F3", _ => Changed());
            _corner = DebugPanelUI.AddSlider(_root, "圆角半径", 0f, 60f, 12f, "int", _ => Changed());
            _bgOp = DebugPanelUI.AddSlider(_root, "背景不透明度", 0f, 1f, 0.9f, "F2", _ => Changed());
            _font = DebugPanelUI.AddSlider(_root, "字体大小", 0, 48, 0, "int", _ => Changed());
            _sx = DebugPanelUI.AddSlider(_root, "占地宽度", 1, 4, 1, "int", _ => Changed());
            _sy = DebugPanelUI.AddSlider(_root, "占地高度", 1, 4, 1, "int", _ => Changed());
            _border = DebugPanelUI.AddColorField(_root, "边框颜色", Color.white, _ => Changed());
            _bg = DebugPanelUI.AddColorField(_root, "背景颜色", Color.white, _ => Changed());
            _text = DebugPanelUI.AddColorField(_root, "文字颜色", Color.black, _ => Changed());
        }

        private void Changed() { if (!_suppress) _onChanged?.Invoke(); }

        public void SyncFromData(IComponentData data)
        {
            if (data is AppearanceData d)
            {
                _suppress = true;
                _sizeScale.SetSilent(d.VisualSizeScale);
                _borderWS.SetSilent(d.BorderWidthScale);
                _corner.SetSilent(d.CornerRadius);
                _bgOp.SetSilent(d.BgOpacity);
                _font.SetSilent(d.FontSize);
                _sx.SetSilent(d.SizeX);
                _sy.SetSilent(d.SizeY);
                _border.SetSilent(d.BorderColor);
                _bg.SetSilent(d.BgColor);
                _text.SetSilent(d.TextColor);
                _suppress = false;
            }
        }

        public IComponentData SyncToData() => new AppearanceData
        {
            VisualSizeScale = _sizeScale.Slider.value,
            BorderWidthScale = _borderWS.Slider.value,
            CornerRadius = _corner.Slider.value,
            BgOpacity = _bgOp.Slider.value,
            FontSize = (int)_font.Slider.value,
            SizeX = Mathf.Max(1, (int)_sx.Slider.value),
            SizeY = Mathf.Max(1, (int)_sy.Slider.value),
            BorderColor = _border.GetValue(),
            BgColor = _bg.GetValue(),
            TextColor = _text.GetValue(),
        };

        public void ConnectSignals(System.Action onChanged) => _onChanged = onChanged;
        public void DisconnectSignals() => _onChanged = null;
        public void SetPropertyLocked(string p, bool l) { }
        public void SetCollapsed(bool c) { }
        public void Dispose() => DisconnectSignals();
    }
}
