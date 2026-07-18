using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// 配置存储抽象 — 对齐 Godot ConfigFile 的 section/key/value 模型。
    /// Stage 0 移植：把 EntityProfileManager 的 Godot ConfigFile 依赖替换为此接口，
    /// 底层用 JSON（Newtonsoft）实现，从而在 Tuanjie（无 Godot ConfigFile）下工作。
    /// </summary>
    public interface IProfileStore
    {
        void SetValue(string section, string key, object value);
        T GetValue<T>(string section, string key, T defaultValue);
        bool HasSection(string section);
        IEnumerable<string> GetSections();
        IEnumerable<string> GetSectionKeys(string section);
        void EraseSection(string section);
    }

    /// <summary>
    /// IProfileStore 的 JSON 实现：内部用 <see cref="JObject"/> 保存 section → { key → JToken }。
    /// - Save/Load 走 Newtonsoft，float/bool/int/string 原生保真，无需 Godot 的定点整数变通。
    /// - Color 沿用 Godot 既有约定，由调用方拆 r/g/b/a 存为多个 key（本类只负责 section/key/value）。
    /// </summary>
    public sealed class JsonProfileStore : IProfileStore
    {
        private JObject _root = new JObject();

        public void SetValue(string section, string key, object value)
        {
            if (_root[section] is not JObject sect)
            {
                sect = new JObject();
                _root[section] = sect;
            }
            sect[key] = value == null ? JValue.CreateNull() : JToken.FromObject(value);
        }

        public T GetValue<T>(string section, string key, T defaultValue)
        {
            if (_root[section] is JObject sect && sect.TryGetValue(key, out var token) && token != null && token.Type != JTokenType.Null)
            {
                try { return token.ToObject<T>(); }
                catch { return defaultValue; }
            }
            return defaultValue;
        }

        public bool HasSection(string section) => _root[section] is JObject;

        // 返回快照，避免调用方在遍历中 EraseSection 导致的修改冲突（对齐 Godot GetSections 语义）。
        public IEnumerable<string> GetSections()
            => _root.Properties().Select(p => p.Name).ToList();

        public IEnumerable<string> GetSectionKeys(string section)
            => _root[section] is JObject sect
                ? sect.Properties().Select(p => p.Name).ToList()
                : Enumerable.Empty<string>();

        public void EraseSection(string section) => _root.Remove(section);

        /// <summary>写入磁盘（缩进格式，便于人工排查）。</summary>
        public bool Save(string path)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(path, JsonConvert.SerializeObject(_root, Formatting.Indented));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[JsonProfileStore] Save failed: {e.Message}");
                return false;
            }
        }

        /// <summary>从磁盘读取；文件不存在返回 false（保持空状态）。</summary>
        public bool Load(string path)
        {
            if (!File.Exists(path))
                return false;
            try
            {
                _root = JObject.Parse(File.ReadAllText(path));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[JsonProfileStore] Load failed: {e.Message}");
                _root = new JObject();
                return false;
            }
        }
    }
}
