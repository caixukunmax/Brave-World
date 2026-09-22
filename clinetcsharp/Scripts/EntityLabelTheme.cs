using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 实体标签字体来源 —— 游戏运行时 RichTextLabel 实际解析到的字体的唯一真相源。
    ///
    /// 背景：实体标签（RichTextLabel）挂在 Node2D 下，Control 主题链在 Node2D 处中断，
    /// 主题解析回落到「所在 Viewport 的父 Viewport（根窗口）主题」：
    ///   - 游戏里根窗口主题 = 项目主题 ?? 引擎默认主题（字体 = 引擎默认字体/FallbackFont）；
    ///   - 编辑器插件预览里实体在 SubViewport 内，根窗口是编辑器窗口，可能解析到
    ///     **编辑器主题字体**（字形宽度与游戏默认字体不同），导致预览文字宽度与
    ///     按 ThemeDB.FallbackFont 预量的布局尺寸不一致（量/绘错配）。
    ///
    /// 因此标签创建时必须用本类把「游戏主题项」（字体 + normal StyleBox + 行距常量）
    /// 显式 override 到 RichTextLabel 上：游戏里 override 与主题解析结果同源，零视觉变化；
    /// 编辑器预览里则强制回到游戏主题，保证预览与运行时 1:1 一致，
    /// 同时根除「按 FallbackFont 量尺寸、按主题字体渲染」的量/绘不一致。
    /// </summary>
    public static class EntityLabelTheme
    {
        /// <summary>游戏运行时解析到的主题：项目主题 ?? 引擎默认主题。</summary>
        public static Theme GetGameTheme()
        {
            return ThemeDB.Singleton.GetProjectTheme() ?? ThemeDB.Singleton.GetDefaultTheme();
        }

        /// <summary>游戏运行时 RichTextLabel 某字体槽的实际解析结果
        /// （主题未定义该字体项时回落到 ThemeDB.FallbackFont，与引擎主题解析行为一致）。</summary>
        public static Font ResolveGameFont(string themeItem = "normal_font")
        {
            var theme = GetGameTheme();
            if (theme != null && theme.HasFont(themeItem, "RichTextLabel"))
                return theme.GetFont(themeItem, "RichTextLabel");
            return ThemeDB.FallbackFont;
        }

        /// <summary>把游戏解析到的主题项固定到标签上，标签创建时必须调用一次，
        /// 保证编辑器预览与游戏运行时渲染完全一致：
        /// 1) 字体（normal/bold/italics/bold_italics/mono）——编辑器主题字体宽度不同，
        ///    会与按 FallbackFont 预量的布局尺寸错配；
        /// 2) normal StyleBox——游戏解析到的是空 StyleBox（透明、无内边距），编辑器主题是
        ///    带内边距的深色面板：既在文字背后画出深色底，又把 FitContent 尺寸撑大
        ///    （实测 +16 宽/+21 高），导致文字偏离按文字尺寸计算的布局中心、探出铭牌边框；
        /// 3) line_separation 等常量——多行/表格行距与游戏对齐。</summary>
        public static void ApplyGameTheme(RichTextLabel label)
        {
            var theme = GetGameTheme();

            label.AddThemeFontOverride("normal_font", ResolveGameFont("normal_font"));
            label.AddThemeFontOverride("bold_font", ResolveGameFont("bold_font"));
            label.AddThemeFontOverride("italics_font", ResolveGameFont("italics_font"));
            label.AddThemeFontOverride("bold_italics_font", ResolveGameFont("bold_italics_font"));
            label.AddThemeFontOverride("mono_font", ResolveGameFont("mono_font"));

            label.AddThemeStyleboxOverride("normal",
                theme != null && theme.HasStylebox("normal", "RichTextLabel")
                    ? theme.GetStylebox("normal", "RichTextLabel")
                    : new StyleBoxEmpty());

            label.AddThemeConstantOverride("line_separation",
                theme != null && theme.HasConstant("line_separation", "RichTextLabel")
                    ? theme.GetConstant("line_separation", "RichTextLabel")
                    : 0);
        }
    }
}
