using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 调试面板切换按钮 - 简化为直接控制 DebugPanel 的可见性
    /// </summary>
    public partial class TestDebugButton : Button
    {
        private DebugPanel _debugPanel;
        private bool _isPanelVisible = false;

        public override void _Ready()
        {
            // 确保按钮可见且颜色对比度高
            Visible = true;
            Modulate = new Color(1, 1, 1, 1);

            // 设置明显的背景色
            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(0.2f, 0.5f, 0.9f); // 蓝色背景
            styleBox.BorderColor = new Color(1, 1, 1);
            styleBox.BorderWidthBottom = 2;
            styleBox.BorderWidthLeft = 2;
            styleBox.BorderWidthRight = 2;
            styleBox.BorderWidthTop = 2;
            styleBox.CornerRadiusBottomLeft = 5;
            styleBox.CornerRadiusBottomRight = 5;
            styleBox.CornerRadiusTopLeft = 5;
            styleBox.CornerRadiusTopRight = 5;
            AddThemeStyleboxOverride("normal", styleBox);

            // 字体颜色白色
            AddThemeColorOverride("font_color", new Color(1, 1, 1));
            AddThemeFontSizeOverride("font_size", 20);

            // 通过 group 获取 DebugPanel
            _debugPanel = GetTree()?.GetFirstNodeInGroup("debug_panel") as DebugPanel;

            if (_debugPanel != null)
            {
                var panel = _debugPanel.GetNodeOrNull<Control>("Control/Panel");
                if (panel != null)
                    panel.Visible = false;
            }

            // 连接点击事件
            Pressed += OnButtonPressed;
        }

        private void OnButtonPressed()
        {
            if (_debugPanel == null)
            {
                _debugPanel = GetTree()?.GetFirstNodeInGroup("debug_panel") as DebugPanel;
                if (_debugPanel == null)
                    return;
            }

            var panel = _debugPanel.GetNodeOrNull<Control>("Control/Panel");
            if (panel != null)
            {
                _isPanelVisible = !_isPanelVisible;
                panel.Visible = _isPanelVisible;
                Text = _isPanelVisible ? "✕ 关闭" : "⚙ 调试";
            }
        }
    }
}
