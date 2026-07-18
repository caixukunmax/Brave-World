using UnityEngine;
using UnityClientSharp.Entity;

namespace UnityClientSharp.DebugPanel
{
    /// <summary>
    /// UI 调试 Tab（配置编辑，移植自 Godot DebugPanelUITab）。
    /// 功能按钮栏子节对应 Godot 端 FunctionButtonBar；Unity 端 FunctionButtonBar 未实现，
    /// 按「配置编辑解耦」原则仍写出配置 UI + 持久化到 DebugPanelSettings，加 TODO 注明暂无消费者。
    /// </summary>
    public class DebugPanelUITab : DebugPanelTab
    {
        public override string TabName => "UI";
        private readonly DebugPanel _host;

        public DebugPanelUITab(DebugPanel host) { _host = host; }

        protected override void BuildContent(RectTransform root)
        {
            var fbar = DebugPanelUI.Section(root, "功能按钮栏(FunctionButtonBar)");
            DebugPanelUI.Label(fbar, "TODO: Unity 端无 FunctionButtonBar 消费者，仅写配置", 11);
            AddInt(fbar, "按钮尺寸", "func_btn_size", 48);
            AddInt(fbar, "按钮间距", "func_btn_spacing", 8);
            AddInt(fbar, "栏内边距", "func_bar_padding", 8);

            var hud = DebugPanelUI.Section(root, "HUD");
            bool showBars = DebugPanelSettings.GetValue("ui_tab", "show_status_bars", true);
            DebugPanelUI.AddToggle(hud, "显示状态条", showBars, v =>
            {
                DebugPanelSettings.SetValue("ui_tab", "show_status_bars", v);
                _host.ScheduleSave();
            });
        }

        private void AddInt(RectTransform parent, string label, string key, int def)
        {
            int v = DebugPanelSettings.GetValue("ui_tab", key, def);
            DebugPanelUI.AddSlider(parent, label, 0, 200, v, "int", x =>
            {
                DebugPanelSettings.SetValue("ui_tab", key, (int)x);
                _host.ScheduleSave();
            });
        }
    }
}
