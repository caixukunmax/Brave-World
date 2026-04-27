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
