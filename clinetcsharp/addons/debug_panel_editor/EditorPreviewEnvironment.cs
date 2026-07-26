using Godot;

namespace ClinetCSharp.Editor
{
    /// <summary>
    /// 编辑器预览宿主环境对齐工具（通用）——让编辑器插件内嵌的 SubViewport 预览
    /// 在「宿主渲染环境」上与游戏根窗口保持一致，适用于一切编辑器内游戏画面预览
    /// （实体配置/地图/演出等）。实体与渲染组件代码保持与游戏完全同一份，只校准宿主。
    ///
    /// 根因：游戏内画面由根窗口按物理像素 1:1 输出，主题解析到项目默认主题；
    /// 编辑器插件预览渲染在 SubViewport 纹理里，宿主环境存在两处系统性偏差：
    ///   1) 像素密度：编辑器 UI 按「编辑器显示缩放」（hiDPI 下常见 1.25~2.0）把整张
    ///      SubViewport 纹理双线性放大到物理屏——几何、血条、文字一起被二次采样发虚。
    ///      游戏里没有这一步后处理放大，所以游戏内是清晰的。
    ///   2) 主题：SubViewport 内 Control（如 RichTextLabel 铭牌）的主题解析会穿过
    ///      SubViewport 落到编辑器主题（编辑器字体/字号/行高），与游戏解析到的
    ///      项目默认主题不同——字号/间距/字形随之漂移。
    ///
    /// 注意（实测 Godot 4.6 引擎行为，见 subviewport_container.cpp）：
    /// Stretch=true 时 SubViewport.Size 完全由引擎托管，强制设为 容器尺寸/StretchShrink，
    /// 手动 set Size 会被引擎拒绝（打印警告且不生效）；且容器的 resized 信号先于引擎的
    /// 尺寸同步发出，信号处理器里读到的是旧尺寸——尺寸相关的相机校准必须延迟到帧末做。
    /// 因此不存在「N× 渲染 / 1/N 显示」的超采样效果：引擎实际是 1/N 渲染 + N× 放大显示。
    /// </summary>
    public static class EditorPreviewEnvironment
    {
        /// <summary>
        /// 预览渲染缩放倍率（≥1）：编辑器显示缩放 100% → 1；125%/150%/175% → 2。
        /// 仅作为 SubViewportContainer.StretchShrink 使用（引擎语义：视口按 容器/N 渲染、
        /// 再放大 N 倍显示，见类注释——并非超采样，保留仅为维持现有视觉尺寸换算不变）。
        /// </summary>
        public static int GetRenderScaleFactor()
        {
            float scale = 1.0f;
            if (Engine.IsEditorHint())
            {
                try
                {
                    scale = EditorInterface.Singleton?.GetEditorScale() ?? 1.0f;
                }
                catch (System.Exception)
                {
                    // 拿不到编辑器接口时退回 1（与旧行为一致，不会因插件环境异常而崩）
                    scale = 1.0f;
                }
            }
            return Mathf.Max(1, Mathf.CeilToInt(scale));
        }

        /// <summary>
        /// 一次性把预览容器/视口对齐到游戏渲染环境：Stretch/StretchShrink + 游戏主题。
        /// 返回渲染缩放倍率（仅用作 StretchShrink）。
        /// 注意：不要手动 set SubViewport.Size——Stretch=true 时尺寸由引擎托管
        /// （容器尺寸/StretchShrink），手动设置会被拒绝；容器 resized 信号先于引擎的
        /// 尺寸同步发出，依赖视口尺寸的校准必须延迟到帧末（见 EditorProfilePanel）。
        /// </summary>
        public static int ApplyTo(SubViewportContainer container, SubViewport viewport)
        {
            int factor = GetRenderScaleFactor();
            if (container == null || viewport == null) return factor;
            container.Stretch = true;
            container.StretchShrink = factor;
            container.Theme = GetGameTheme();
            return factor;
        }

        /// <summary>
        /// 游戏运行时实际解析到的主题：项目主题（gui/theme/custom，未配置时 GetProjectTheme
        /// 返回 null）→ 引擎默认主题。把它赋给 SubViewportContainer（SubViewport 内 Control 的
        /// 主题解析会穿过 SubViewport 找到最近的带 Theme 的 Control 祖先），铭牌等 Control 的
        /// 字体/字号即与游戏 1:1 一致；同时引擎默认主题的字体与实体排版用的
        /// ThemeDB.FallbackFont 同源，「按 FallbackFont 量尺寸 + 按主题字体渲染」不再错配。
        /// </summary>
        public static Theme GetGameTheme()
        {
            return ThemeDB.Singleton.GetProjectTheme() ?? ThemeDB.Singleton.GetDefaultTheme();
        }
    }
}
