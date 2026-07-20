using System.IO;
using UnityClientSharp.Entity;
using UnityEngine;

namespace BraveWorld.Editor
{
    /// <summary>
    /// 面板设置存储（对应已删除运行时版的 DebugPanelSettings）：
    /// 复用 Runtime 的 JsonProfileStore，落 persistentDataPath/debug_panel_settings.json。
    /// 按「配置编辑解耦」原则：暂无运行时消费者的配置也照常读写持久化（键名与旧版一致）。
    /// </summary>
    public static class EditorDebugSettings
    {
        private const string FileName = "debug_panel_settings.json";
        private static JsonProfileStore _store;
        private static string FullPath => Path.Combine(Application.persistentDataPath, FileName);

        private static JsonProfileStore Store
        {
            get
            {
                if (_store == null)
                {
                    _store = new JsonProfileStore();
                    _store.Load(FullPath);
                }
                return _store;
            }
        }

        public static T Get<T>(string section, string key, T defaultValue) => Store.GetValue(section, key, defaultValue);
        public static void Set<T>(string section, string key, T value) => Store.SetValue(section, key, value);
        public static void Save() => Store.Save(FullPath);

        /// <summary>丢弃内存缓存，下次访问时从磁盘重读（配合窗口「从磁盘重载」按钮）。</summary>
        public static void Reload() => _store = null;
    }
}
