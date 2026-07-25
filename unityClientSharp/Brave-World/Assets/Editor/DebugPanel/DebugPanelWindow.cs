using UnityClientSharp.Entity;
using UnityEditor;
using UnityEngine;

namespace BraveWorld.Editor
{
    /// <summary>
    /// 调试面板（编辑器版，取代已删除的运行时 UGUI 版 DebugPanel）。
    /// - Edit 模式：纯配置编写（实体 Profile / 面板设置），落盘 persistentDataPath/*.json；
    /// - Play 模式：改配置后点「应用并刷新场景实体」手动同步（实时非必须，见用户决策）。
    /// - 解耦：本面板全在 Editor 程序集，不进包；玩法侧仅提供通用刷新 API（ProfileRefreshUtil）。
    /// 菜单：Window > BraveWorld > 调试面板
    /// </summary>
    public class DebugPanelWindow : EditorWindow
    {
        [MenuItem("Window/BraveWorld/调试面板")]
        public static void Open() => GetWindow<DebugPanelWindow>("调试面板");

        private static readonly string[] TabNames = { "实体配置", "面板设置" };

        private int _tab;
        private EntityProfileTab _profileTab;
        private SettingsTab _settingsTab;
        private double _dirtyAt = -1;

        private void OnEnable()
        {
            _profileTab = new EntityProfileTab(this);
            _settingsTab = new SettingsTab(this);
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            // 关窗前有未落盘的改动则补存
            if (_dirtyAt > 0) { _dirtyAt = -1; SaveAll(); }
        }

        private void OnPlayModeChanged(PlayModeStateChange _)
        {
            // Play 切换伴随域重载，Profile 引用全部失效，强制重拉
            _profileTab.Invalidate();
            Repaint();
        }

        private void OnFocus() => _profileTab?.RefreshIfNeeded();

        /// <summary>配置已改：0.5s 防抖自动落盘（拖 slider 时不会每帧写盘）；同时立即重绘窗口让预览实时刷新。</summary>
        public void MarkDirty()
        {
            _dirtyAt = EditorApplication.timeSinceStartup;
            Repaint();
        }

        private void Update()
        {
            if (_dirtyAt > 0 && EditorApplication.timeSinceStartup - _dirtyAt > 0.5)
            {
                _dirtyAt = -1;
                SaveAll();
            }
        }

        private void SaveAll()
        {
            EntityProfileManager.SaveConfig();
            EditorDebugSettings.Save();
            RefreshMapEditorSession();
        }

        /// <summary>
        /// 配置变更后同步重建地图编辑器会话里的装饰摆件（字号/样式/停用立即生效），
        /// 否则地图编辑器里看到的还是进入会话时的旧配置，与调试面板预览不一致。
        /// </summary>
        private static void RefreshMapEditorSession()
        {
            var mapEditor = MapEditing.MapEditorWindow.Active;
            if (mapEditor != null && mapEditor.Session.IsActive)
                mapEditor.Session.RefreshAll();
        }

        private void OnGUI()
        {
            _tab = GUILayout.Toolbar(_tab, TabNames);
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandHeight(true)))
            {
                if (_tab == 0) _profileTab.OnGUI();
                else _settingsTab.OnGUI();
            }
            DrawFooter();
        }

        private void DrawFooter()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(Application.isPlaying ? "运行中" : "编辑中", EditorStyles.boldLabel, GUILayout.Width(50));
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("保存", GUILayout.Width(60)))
                {
                    _dirtyAt = -1;
                    SaveAll();
                }
                if (GUILayout.Button("从磁盘重载", GUILayout.Width(80)))
                {
                    EntityProfileManager.LoadConfig();
                    EditorDebugSettings.Reload();
                    _profileTab.Invalidate();
                    RefreshMapEditorSession();
                }
                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    if (GUILayout.Button("应用并刷新场景实体", GUILayout.Width(140)))
                    {
                        _dirtyAt = -1;
                        SaveAll();
                        ProfileRefreshUtil.RefreshAll();
                    }
                }
            }
            string hint = Application.isPlaying
                ? "改配置 → 点「应用并刷新场景实体」同步到场景（新刷出的实体自动吃新配置）。"
                : "Edit 模式的修改在下次 Play 时自动生效；「应用并刷新」仅 Play 可用。";
            EditorGUILayout.LabelField(hint, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("配置路径: " +
                System.IO.Path.Combine(Application.persistentDataPath, "entity_profiles.json"), EditorStyles.miniLabel);
        }
    }
}
