using UnityEngine;
using UnityClientSharp.Entity;

namespace UnityClientSharp.DebugPanel
{
    /// <summary>
    /// 地图 Tab（配置编辑，移植自 Godot DebugPanelMapTab）。
    /// 网格 overlay / 相机校准参数写入解耦的 DebugPanelSettings（无运行时消费者也持久化）。
    /// 注：GridManager / MapCameraController 可视化与 MapEditor 联动留 TODO（当前仅存配置）。
    /// </summary>
    public class DebugPanelMapTab : DebugPanelTab
    {
        public override string TabName => "地图";
        private readonly DebugPanel _host;

        public DebugPanelMapTab(DebugPanel host) { _host = host; }

        protected override void BuildContent(RectTransform root)
        {
            var grid = DebugPanelUI.Section(root, "网格 overlay");
            float op = DebugPanelSettings.GetValue("map_tab", "grid_opacity", 0.3f);
            DebugPanelUI.AddSlider(grid, "网格不透明度", 0f, 1f, op, "F2", v =>
            {
                DebugPanelSettings.SetValue("map_tab", "grid_opacity", v);
                _host.ScheduleSave();
            });
            DebugPanelUI.AddColorField(grid, "网格颜色",
                ReadColor("map_tab", "grid_color", new Color(0.3f, 0.8f, 0.4f)), c =>
                {
                    WriteColor("map_tab", "grid_color", c);
                    _host.ScheduleSave();
                });

            var cam = DebugPanelUI.Section(root, "相机校准");
            float zoom = DebugPanelSettings.GetValue("map_tab", "camera_zoom", 1f);
            DebugPanelUI.AddSlider(cam, "缩放", 0.3f, 3f, zoom, "F2", v =>
            {
                DebugPanelSettings.SetValue("map_tab", "camera_zoom", v);
                _host.ScheduleSave();
            });
            float pad = DebugPanelSettings.GetValue("map_tab", "camera_padding", 0.1f);
            DebugPanelUI.AddSlider(cam, "边缘留白", 0f, 0.5f, pad, "F2", v =>
            {
                DebugPanelSettings.SetValue("map_tab", "camera_padding", v);
                _host.ScheduleSave();
            });

            var note = DebugPanelUI.Section(root, "说明");
            DebugPanelUI.Label(note, "网格/相机 overlay 的实际可视化依赖 GridManager / MapCameraController；MapEditor 联动留 TODO", 11);
        }

        private static Color ReadColor(string s, string k, Color def) => new Color(
            DebugPanelSettings.GetValue(s, k + "_r", def.r),
            DebugPanelSettings.GetValue(s, k + "_g", def.g),
            DebugPanelSettings.GetValue(s, k + "_b", def.b),
            DebugPanelSettings.GetValue(s, k + "_a", def.a));

        private static void WriteColor(string s, string k, Color c)
        {
            DebugPanelSettings.SetValue(s, k + "_r", c.r);
            DebugPanelSettings.SetValue(s, k + "_g", c.g);
            DebugPanelSettings.SetValue(s, k + "_b", c.b);
            DebugPanelSettings.SetValue(s, k + "_a", c.a);
        }
    }
}
