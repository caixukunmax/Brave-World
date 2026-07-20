using UnityEditor;
using UnityEngine;

namespace BraveWorld.Editor
{
    /// <summary>
    /// 面板设置 Tab：网格 overlay / 相机校准 / 系统参数 / 背包布局 / 功能按钮栏 / HUD。
    /// 对应已删除 UGUI 版的「地图」「系统」「UI」三个 Tab（它们本就全是纯配置项）。
    /// 键名/分节与旧版一致（debug_panel_settings.json）。TODO 标注：暂无运行时消费者。
    /// </summary>
    public class SettingsTab
    {
        private readonly DebugPanelWindow _host;
        private Vector2 _scroll;

        public SettingsTab(DebugPanelWindow host) { _host = host; }

        public void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("网格 overlay（TODO：暂无运行时消费者）", EditorStyles.boldLabel);
            SetF("map_tab", "grid_opacity", EditorGUILayout.Slider("网格不透明度",
                GetF("map_tab", "grid_opacity", 0.3f), 0f, 1f));
            WriteColor("map_tab", "grid_color", EditorGUILayout.ColorField("网格颜色",
                ReadColor("map_tab", "grid_color", new Color(0.3f, 0.8f, 0.4f))));

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("相机校准（TODO：暂无运行时消费者）", EditorStyles.boldLabel);
            SetF("map_tab", "camera_zoom", EditorGUILayout.Slider("缩放",
                GetF("map_tab", "camera_zoom", 1f), 0.3f, 3f));
            SetF("map_tab", "camera_padding", EditorGUILayout.Slider("边缘留白",
                GetF("map_tab", "camera_padding", 0.1f), 0f, 0.5f));

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("系统参数", EditorStyles.boldLabel);
            SetF("system_tab", "ui_scale", EditorGUILayout.Slider("UI 缩放",
                GetF("system_tab", "ui_scale", 1f), 0.5f, 2f));
            SetB("system_tab", "vsync", EditorGUILayout.Toggle("垂直同步",
                GetB("system_tab", "vsync", true)));

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("背包布局(InventoryUI)（TODO：暂无运行时消费者）", EditorStyles.boldLabel);
            SetI("system_tab", "inv_cols", EditorGUILayout.IntSlider("每行格数",
                GetI("system_tab", "inv_cols", 5), 1, 12));
            SetI("system_tab", "inv_cell_size", EditorGUILayout.IntSlider("格子尺寸",
                GetI("system_tab", "inv_cell_size", 64), 16, 128));
            SetI("system_tab", "inv_cell_spacing", EditorGUILayout.IntSlider("格子间距",
                GetI("system_tab", "inv_cell_spacing", 4), 0, 32));
            SetI("system_tab", "inv_padding", EditorGUILayout.IntSlider("背包内边距",
                GetI("system_tab", "inv_padding", 8), 0, 32));

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("功能按钮栏(FunctionButtonBar)（TODO：暂无运行时消费者）", EditorStyles.boldLabel);
            SetI("ui_tab", "func_btn_size", EditorGUILayout.IntSlider("按钮尺寸",
                GetI("ui_tab", "func_btn_size", 48), 16, 128));
            SetI("ui_tab", "func_btn_spacing", EditorGUILayout.IntSlider("按钮间距",
                GetI("ui_tab", "func_btn_spacing", 8), 0, 64));
            SetI("ui_tab", "func_bar_padding", EditorGUILayout.IntSlider("栏内边距",
                GetI("ui_tab", "func_bar_padding", 8), 0, 64));

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("HUD", EditorStyles.boldLabel);
            SetB("ui_tab", "show_status_bars", EditorGUILayout.Toggle("显示状态条",
                GetB("ui_tab", "show_status_bars", true)));

            if (EditorGUI.EndChangeCheck()) _host.MarkDirty();
            EditorGUILayout.EndScrollView();
        }

        private static float GetF(string s, string k, float def) => EditorDebugSettings.Get(s, k, def);
        private static void SetF(string s, string k, float v) => EditorDebugSettings.Set(s, k, v);
        private static int GetI(string s, string k, int def) => EditorDebugSettings.Get(s, k, def);
        private static void SetI(string s, string k, int v) => EditorDebugSettings.Set(s, k, v);
        private static bool GetB(string s, string k, bool def) => EditorDebugSettings.Get(s, k, def);
        private static void SetB(string s, string k, bool v) => EditorDebugSettings.Set(s, k, v);

        // 颜色按 r/g/b/a 四个 float 键存储（与旧版一致）
        private static Color ReadColor(string s, string k, Color def) => new Color(
            EditorDebugSettings.Get(s, k + "_r", def.r),
            EditorDebugSettings.Get(s, k + "_g", def.g),
            EditorDebugSettings.Get(s, k + "_b", def.b),
            EditorDebugSettings.Get(s, k + "_a", def.a));

        private static void WriteColor(string s, string k, Color c)
        {
            EditorDebugSettings.Set(s, k + "_r", c.r);
            EditorDebugSettings.Set(s, k + "_g", c.g);
            EditorDebugSettings.Set(s, k + "_b", c.b);
            EditorDebugSettings.Set(s, k + "_a", c.a);
        }
    }
}
