using System.IO;
using UnityClientSharp.Entity;
using UnityEngine;

namespace UnityClientSharp.DebugPanel
{
    /// <summary>
    /// DebugPanel 通用设置存储（供 MapTab / SystemTab / UITab 这类「配置编辑但暂无运行时消费者」的面板）。
    /// 与 entity_profiles.json 解耦，单独落在 debug_panel_settings.json。
    /// 按「配置编辑解耦」原则：即使当前无运行时消费者，配置也照常读写持久化（对应子节加 TODO 注明）。
    /// 底层复用逻辑层 JsonProfileStore（Newtonsoft JSON）。
    /// </summary>
    public static class DebugPanelSettings
    {
        private const string FileName = "debug_panel_settings.json";
        private static JsonProfileStore _store;
        private static string FullPath => Path.Combine(Application.persistentDataPath, FileName);

        private static JsonProfileStore Store => _store ??= Load();

        private static JsonProfileStore Load()
        {
            var s = new JsonProfileStore();
            s.Load(FullPath);
            return s;
        }

        public static void SetValue(string section, string key, object value) => Store.SetValue(section, key, value);
        public static T GetValue<T>(string section, string key, T defaultValue) => Store.GetValue(section, key, defaultValue);
        public static bool HasSection(string section) => Store.HasSection(section);
        public static void Save() => Store.Save(FullPath);
    }
}
