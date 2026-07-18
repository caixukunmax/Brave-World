using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// EntityProfileManager partial — 配置保存/加载（移植自 Godot 端 EntityProfileManager.Config.cs）。
    /// Stage 0 / Path B：把 Godot ConfigFile 替换为 <see cref="IProfileStore"/>（JSON），
    /// 去除定点整数与 v3→v4 定点迁移；ConfigVersion 从 1 起新基线。
    /// </summary>
    public static partial class EntityProfileManager
    {
        #region Save / Load

        public static void SaveConfig()
        {
            EnsureInitialized();
            var store = new JsonProfileStore();
            // 载入旧文件以保留非 profile_ 的段（如 meta / 未来样式覆盖），再覆盖 profile_ 段。
            store.Load(ConfigPath);
            store.SetValue("meta", "config_version", ConfigVersion);
            WriteProfileConfig(store);
            if (store.Save(ConfigPath))
                Debug.Log("[EntityProfileManager] Config saved");
            else
                Debug.LogError("[EntityProfileManager] Failed to save config");
        }

        /// <summary>将 Profile 数据写入指定存储（供 DebugPanel 统一保存时调用）。</summary>
        public static void WriteProfileConfig(IProfileStore store)
        {
            // 清除旧的 profile_ section（含子 section profile_X.comp）
            foreach (string section in store.GetSections())
            {
                if (section.StartsWith("profile_"))
                    store.EraseSection(section);
            }

            foreach (var profile in _profiles.Values)
            {
                string section = $"profile_{profile.Id}";
                store.SetValue(section, "name", profile.Name);
                store.SetValue(section, "entity_type", profile.EntityType);
                store.SetValue(section, "components", string.Join(",", profile.ComponentNames));

                var disabled = profile.ComponentNames.Where(c => profile.IsComponentDisabled(c)).ToList();
                store.SetValue(section, "disabled_components",
                    disabled.Count > 0 ? string.Join(",", disabled) : "");

                foreach (string compName in profile.ComponentNames)
                {
                    var data = profile.GetData(compName);
                    string compSection = $"{section}.{compName}";
                    WriteComponentData(store, compSection, compName, data);
                }
            }
        }

        public static void LoadConfig()
        {
            var store = new JsonProfileStore();
            if (!store.Load(ConfigPath))
            {
                Debug.Log("[EntityProfileManager] Config file not found, using defaults");
                return;
            }

            bool hasProfileSections = store.GetSections().Any(s => s.StartsWith("profile_"));
            if (hasProfileSections)
                LoadNewFormat(store);
            // 无 profile_ 段：Unity 端无 Godot 旧散键格式，直接保持默认（Path B 不移植 LoadLegacyFormat）。

            _nextId = _profiles.Count > 0 ? _profiles.Keys.Max() + 1 : 1;
        }

        private static void LoadNewFormat(IProfileStore store)
        {
            _profiles.Clear();

            foreach (string section in store.GetSections())
            {
                if (!section.StartsWith("profile_"))
                    continue;

                // 跳过子 section（profile_X.compname）— 只有顶级 section 才没有 '.'
                string afterPrefix = section.Substring("profile_".Length);
                if (afterPrefix.Contains('.'))
                    continue;

                if (!int.TryParse(afterPrefix, out int id))
                    continue;

                string name = store.GetValue(section, "name", "");
                string entityType = store.GetValue(section, "entity_type", "");
                string componentsStr = store.GetValue(section, "components", "");

                var profile = new EntityProfile
                {
                    Id = id,
                    Name = name,
                    EntityType = entityType,
                };

                if (!string.IsNullOrEmpty(componentsStr))
                {
                    foreach (string compName in componentsStr.Split(','))
                    {
                        string trimmed = compName.Trim();
                        if (string.IsNullOrEmpty(trimmed)) continue;

                        string compSection = $"{section}.{trimmed}";
                        var data = ReadComponentData(store, compSection, trimmed);
                        if (data != null)
                            profile.SetData(trimmed, data);
                    }
                }

                string disabledStr = store.GetValue(section, "disabled_components", "");
                if (!string.IsNullOrEmpty(disabledStr))
                {
                    foreach (string compName in disabledStr.Split(','))
                    {
                        string trimmed = compName.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                            profile.SetComponentDisabled(trimmed, true);
                    }
                }

                _profiles[id] = profile;
            }

            // TODO(Path A / 旧地图迁移): Godot 端此处有 MigrateBuildingProfileIds（旧默认 ID 10/11/12 → 新配置 ID），
            // Unity 端从未使用旧 ID，暂不移植；若未来需兼容旧存档再补。
            EnsureDefaultProfiles();
            Debug.Log($"[EntityProfileManager] Loaded {_profiles.Count} profiles (new format)");
        }

        #endregion
    }
}
