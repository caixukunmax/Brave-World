using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 全局视觉配置（[system_tab] 段）应用 —— 运行时与编辑器插件共用的唯一入口。
    ///
    /// 实体渲染依赖的全局静态字段（方向箭头样式/大小/颜色/透明度/四方向偏移/角度）
    /// 持久化在 debug_panel_config.cfg 的 [system_tab] 段。游戏启动时由
    /// DebugPanelSystemTab.LoadConfig 应用；编辑器插件是独立进程，静态字段各自独立，
    /// 不经过这里应用就会回落到代码默认值，导致预览与游戏渲染不一致
    /// （典型症状：游戏里几乎透明的方向箭头，在编辑器预览里又大又亮地贴在实体上）。
    ///
    /// 任何在编辑器内渲染实体的入口（实体显示配置面板等）在加载 Profile 前
    /// 都必须先经 ApplyFromFile 应用全局配置，禁止各自解析 cfg 或只用默认值。
    /// </summary>
    public static class EntityGlobalVisualConfig
    {
        /// <summary>从已加载的 ConfigFile 把 [system_tab] 全局视觉配置写入 EntityBase 静态字段。</summary>
        public static void ApplyFromConfig(ConfigFile cfg)
        {
            if (cfg == null || !cfg.HasSection("system_tab")) return;

            // 方向箭头配置（解析逻辑与历史运行时实现逐字一致，勿改默认值）
            EntityBase.DirectionArrowStyle = (int)(double)cfg.GetValue("system_tab", "direction_arrow_style", 0.0);
            EntityBase.DirectionArrowSize = (float)(double)cfg.GetValue("system_tab", "direction_arrow_size", 0.35);
            EntityBase.DirectionArrowColor = (Color)cfg.GetValue("system_tab", "direction_arrow_color", new Color(1f, 0.9f, 0.2f, 0.9f));
            EntityBase.DirectionArrowAlpha = (float)(double)cfg.GetValue("system_tab", "direction_arrow_alpha", 0.9);
            Vector2[] defaultOffsets = { new(15, 0), new(0, 15), new(-15, 0), new(0, -15) };
            float[] defaultAngles = { 0f, 90f, 180f, 270f };
            string[] dirKeys = { "right", "down", "left", "up" };
            for (int d = 0; d < 4; d++)
            {
                float ox = (float)(double)cfg.GetValue("system_tab", $"direction_arrow_offset_x_{dirKeys[d]}", (double)defaultOffsets[d].X);
                float oy = (float)(double)cfg.GetValue("system_tab", $"direction_arrow_offset_y_{dirKeys[d]}", (double)defaultOffsets[d].Y);
                float ang = (float)(double)cfg.GetValue("system_tab", $"direction_arrow_angle_{dirKeys[d]}", (double)defaultAngles[d]);
                EntityBase.DirectionArrowOffsets[d] = new Vector2(ox, oy);
                EntityBase.DirectionArrowAngles[d] = ang;
            }
        }

        /// <summary>直接从配置文件加载并应用（编辑器插件等没有运行时 ConfigFile 的入口用）。
        /// 读取失败保持代码默认值，不阻塞 UI。</summary>
        public static void ApplyFromFile(string path = ProfileConfigIO.ConfigPath)
        {
            try
            {
                var cfg = new ConfigFile();
                if (cfg.Load(path) == Error.Ok)
                    ApplyFromConfig(cfg);
            }
            catch (System.Exception) { /* 读取失败则保持代码默认值 */ }
        }
    }
}
