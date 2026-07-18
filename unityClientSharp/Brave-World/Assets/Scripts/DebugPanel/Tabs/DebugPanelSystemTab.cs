using UnityEngine;
using UnityClientSharp.Entity;

namespace UnityClientSharp.DebugPanel
{
    /// <summary>
    /// 系统 Tab（配置编辑，移植自 Godot DebugPanelSystemTab）。
    /// 背包布局子节对应 Godot 端 InventoryUI；Unity 端 InventoryUI 未实现，
    /// 按「配置编辑解耦」原则仍写出配置 UI + 持久化到 DebugPanelSettings，加 TODO 注明暂无消费者。
    /// </summary>
    public class DebugPanelSystemTab : DebugPanelTab
    {
        public override string TabName => "系统";
        private readonly DebugPanel _host;

        public DebugPanelSystemTab(DebugPanel host) { _host = host; }

        protected override void BuildContent(RectTransform root)
        {
            var inv = DebugPanelUI.Section(root, "背包布局(InventoryUI)");
            DebugPanelUI.Label(inv, "TODO: Unity 端无 InventoryUI 消费者，仅写配置", 11);
            AddInt(inv, "每行格数", "inv_cols", 5, 1, 12);
            AddInt(inv, "格子尺寸", "inv_cell_size", 64, 16, 128);
            AddInt(inv, "格子间距", "inv_cell_spacing", 4, 0, 32);
            AddInt(inv, "背包内边距", "inv_padding", 8, 0, 32);

            var perf = DebugPanelUI.Section(root, "系统参数");
            float scale = DebugPanelSettings.GetValue("system_tab", "ui_scale", 1f);
            DebugPanelUI.AddSlider(perf, "UI 缩放", 0.5f, 2f, scale, "F2", v =>
            {
                DebugPanelSettings.SetValue("system_tab", "ui_scale", v);
                _host.ScheduleSave();
            });
            bool vsync = DebugPanelSettings.GetValue("system_tab", "vsync", true);
            DebugPanelUI.AddToggle(perf, "垂直同步", vsync, v =>
            {
                DebugPanelSettings.SetValue("system_tab", "vsync", v);
                _host.ScheduleSave();
            });

            var note = DebugPanelUI.Section(root, "说明");
            DebugPanelUI.Label(note, "系统参数为配置编辑；具体消费依赖各运行时模块，部分留 TODO", 11);
        }

        private void AddInt(RectTransform parent, string label, string key, int def, int min, int max)
        {
            int v = DebugPanelSettings.GetValue("system_tab", key, def);
            DebugPanelUI.AddSlider(parent, label, min, max, v, "int", x =>
            {
                DebugPanelSettings.SetValue("system_tab", key, (int)x);
                _host.ScheduleSave();
            });
        }
    }
}
