using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 共享 UI 样式常量 — 所有 DraggablePanel 子类共用的面板/标题栏样式。
    /// </summary>
    public static class UiStyles
    {
        public static readonly Color PanelBgColor = new(0, 0, 0, 0.85f);
        public static readonly Color PanelBorderColor = new(0.2f, 0.2f, 0.2f);
        public static readonly Color TitleBarBgColor = new(0.1f, 0.1f, 0.1f, 0.9f);

        // 语义颜色
        public static readonly Color AccentColor = new(0.4f, 0.6f, 1f);
        public static readonly Color TextColor = new(0.85f, 0.85f, 0.85f);
        public static readonly Color TextDimColor = new(0.5f, 0.5f, 0.5f);
        public static readonly Color DangerColor = new(1f, 0.3f, 0.3f);
        public static readonly Color SuccessColor = new(0.3f, 1f, 0.3f);
        public static readonly Color GoldColor = new Color("#FFD700");

        public static StyleBoxFlat CreatePanelStyle()
        {
            return new StyleBoxFlat
            {
                BgColor = PanelBgColor,
                BorderColor = PanelBorderColor,
                BorderWidthBottom = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                BorderWidthTop = 1,
            };
        }

        public static StyleBoxFlat CreateTitleBarStyle()
        {
            return new StyleBoxFlat { BgColor = TitleBarBgColor };
        }

        /// <summary>技能栏槽位背景样式</summary>
        public static StyleBoxFlat CreateSlotStyle()
        {
            return new StyleBoxFlat
            {
                BgColor = new Color(0.1f, 0.1f, 0.1f, 0.7f),
                BorderColor = new Color(0.3f, 0.3f, 0.3f),
                BorderWidthBottom = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                BorderWidthTop = 1,
            };
        }

        /// <summary>选中高亮边框样式</summary>
        public static StyleBoxFlat CreateHighlightStyle()
        {
            return new StyleBoxFlat
            {
                BgColor = new Color(0, 0, 0, 0),
                BorderColor = GoldColor,
                BorderWidthBottom = 2,
                BorderWidthLeft = 2,
                BorderWidthRight = 2,
                BorderWidthTop = 2,
            };
        }

        /// <summary>蓄力条背景样式</summary>
        public static StyleBoxFlat CreateCastBarBgStyle()
        {
            return new StyleBoxFlat
            {
                BgColor = new Color(TextDimColor, 0.8f),
            };
        }

        /// <summary>
        /// 通用 StyleBoxFlat 工厂方法 — 替代散布各处的 new StyleBoxFlat { ... } 模式。
        /// 只设置非默认值的边框宽度；所有边框宽度默认为 0（无边框）。
        /// </summary>
        public static StyleBoxFlat CreateStyleBox(Color bgColor, Color? borderColor = null,
            int borderWidth = 0, int cornerRadius = 0)
        {
            var style = new StyleBoxFlat { BgColor = bgColor };
            if (borderColor != null)
            {
                style.BorderColor = borderColor.Value;
                if (borderWidth > 0)
                {
                    style.BorderWidthBottom = borderWidth;
                    style.BorderWidthLeft = borderWidth;
                    style.BorderWidthRight = borderWidth;
                    style.BorderWidthTop = borderWidth;
                }
            }
            if (cornerRadius > 0)
            {
                style.CornerRadiusBottomLeft = cornerRadius;
                style.CornerRadiusBottomRight = cornerRadius;
                style.CornerRadiusTopLeft = cornerRadius;
                style.CornerRadiusTopRight = cornerRadius;
            }
            return style;
        }

        /// <summary>
        /// 带圆角的面板样式快捷方法。
        /// </summary>
        public static StyleBoxFlat CreatePanelStyle(Color bgColor, Color borderColor, int borderWidth = 1, int cornerRadius = 0)
        {
            return CreateStyleBox(bgColor, borderColor, borderWidth, cornerRadius);
        }

        /// <summary>为面板应用标准深色主题样式</summary>
        public static void ApplyDarkTheme(DraggablePanel panel)
        {
            panel.AddThemeStyleboxOverride("panel", CreatePanelStyle());
            var titleBar = panel.GetNodeOrNull<PanelContainer>("VBoxContainer/TitleBar");
            if (titleBar != null)
                titleBar.AddThemeStyleboxOverride("panel", CreateTitleBarStyle());
        }
    }
}
