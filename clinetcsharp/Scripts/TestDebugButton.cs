using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 调试面板切换按钮 - 简化为直接控制 DebugPanel 的可见性
    /// </summary>
    public partial class TestDebugButton : Button
    {
        private CanvasLayer _debugPanel;
        private bool _isPanelVisible = false;

        public override void _Ready()
        {
            GD.Print("========================================");
            GD.Print("[DebugButton] _Ready() START");
            GD.Print($"[DebugButton] Name: {Name}");
            GD.Print($"[DebugButton] Parent: {GetParent()?.Name}");
            GD.Print($"[DebugButton] Position: {Position}, GlobalPosition: {GlobalPosition}");
            GD.Print($"[DebugButton] Size: {Size}");
            GD.Print($"[DebugButton] Visible: {Visible}");
            GD.Print($"[DebugButton] Modulate: {Modulate}");
            GD.Print($"[DebugButton] ZIndex: {ZIndex}");
            GD.Print($"[DebugButton] Text: '{Text}'");
            
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
            
            // 直接通过节点路径获取 DebugPanel (按钮现在是在 DebugButtonCanvas 下)
            _debugPanel = GetNodeOrNull<CanvasLayer>("../../DebugPanel");
            GD.Print($"[DebugButton] Looking for DebugPanel at ../../DebugPanel, found: {_debugPanel != null}");
            
            if (_debugPanel != null)
            {
                GD.Print("[DebugButton] Found DebugPanel");
                // 初始隐藏面板 - 只隐藏Panel，不隐藏整个CanvasLayer
                var panel = _debugPanel.GetNodeOrNull<Control>("Control/Panel");
                if (panel != null)
                {
                    panel.Visible = false;
                    GD.Print("[DebugButton] Panel hidden initially");
                }
            }
            else
            {
                GD.PushError("[DebugButton] DebugPanel not found!");
            }
            
            // 连接点击事件
            Pressed += OnButtonPressed;
            
            GD.Print("[DebugButton] _Ready() END");
            GD.Print("========================================");
        }

        private void OnButtonPressed()
        {
            GD.Print("[DebugButton] Clicked!");
            
            if (_debugPanel == null)
            {
                // 重新尝试获取 (路径更新)
                _debugPanel = GetNodeOrNull<CanvasLayer>("../../DebugPanel");
                if (_debugPanel == null)
                {
                    GD.PushError("[DebugButton] DebugPanel still not found!");
                    return;
                }
            }
            
            // 切换面板可见性 - 直接控制 Control/Panel 的可见性
            var panel = _debugPanel.GetNodeOrNull<Control>("Control/Panel");
            if (panel != null)
            {
                _isPanelVisible = !_isPanelVisible;
                panel.Visible = _isPanelVisible;
                Text = _isPanelVisible ? "✕ 关闭" : "⚙ 调试";
                GD.Print($"[DebugButton] Panel visibility: {_isPanelVisible}");
            }
            else
            {
                GD.PushError("[DebugButton] Panel not found in DebugPanel");
            }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.F1)
            {
                GD.Print("[DebugButton] F1 pressed!");
                OnButtonPressed();
            }
        }
    }
}
