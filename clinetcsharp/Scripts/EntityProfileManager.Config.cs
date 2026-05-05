using Godot;
using System.Collections.Generic;
using System.Linq;

namespace ClinetCSharp
{
    /// <summary>
    /// EntityProfileManager partial — 配置保存/加载/序列化
    /// </summary>
    public partial class EntityProfileManager
    {
        #region Save/Load Config

        public void SaveConfig()
        {
            var config = new ConfigFile();
            Error loadErr = config.Load(ConfigPath);
            WriteProfileConfig(config);
            Error err = config.Save(ConfigPath);
            if (err == Error.Ok)
                GD.Print("[EntityProfileManager] Config saved");
            else
                GD.PushError($"[EntityProfileManager] Failed to save config: {err}");
        }

        /// <summary>
        /// 将 Profile 数据写入指定的 ConfigFile（供 DebugPanel 统一保存时调用）
        /// </summary>
        public void WriteProfileConfig(ConfigFile config)
        {
            // 清除旧的 Profile section
            foreach (string section in config.GetSections())
            {
                if (section.StartsWith("profile_"))
                    config.EraseSection(section);
            }

            foreach (var profile in _profiles.Values)
            {
                string section = $"profile_{profile.Id}";
                config.SetValue(section, "name", profile.Name);
                config.SetValue(section, "entity_type", profile.EntityType);
                config.SetValue(section, "components",
                    string.Join(",", profile.ComponentNames));

                // 保存停用组件列表
                var disabled = profile.ComponentNames.Where(c => profile.IsComponentDisabled(c)).ToList();
                config.SetValue(section, "disabled_components",
                    disabled.Count > 0 ? string.Join(",", disabled) : "");

                foreach (string compName in profile.ComponentNames)
                {
                    var data = profile.GetData(compName);
                    string compSection = $"{section}.{compName}";
                    WriteComponentData(config, compSection, compName, data);
                }
            }
        }

        public void LoadConfig()
        {
            var config = new ConfigFile();
            Error err = config.Load(ConfigPath);

            if (err == Error.FileNotFound)
            {
                GD.Print("[EntityProfileManager] Config file not found, using defaults");
                return;
            }

            if (err != Error.Ok)
            {
                GD.PushError($"[EntityProfileManager] Failed to load config: {err}");
                return;
            }

            int savedVersion = (int)config.GetValue("meta", "config_version", 0);

            if (savedVersion < ConfigVersion)
            {
                GD.Print($"[EntityProfileManager] Migrating config from v{savedVersion} to v{ConfigVersion}");
                MigrateConfig(config, savedVersion);
            }

            bool hasProfileSections = config.GetSections()
                .Any(s => s.StartsWith("profile_"));

            if (hasProfileSections)
            {
                LoadNewFormat(config);
            }
            else
            {
                LoadLegacyFormat(config);
            }

            _nextId = _profiles.Count > 0 ? _profiles.Keys.Max() + 1 : 1;
        }

        private void LoadNewFormat(ConfigFile config)
        {
            _profiles.Clear();

            foreach (string section in config.GetSections())
            {
                if (!section.StartsWith("profile_"))
                    continue;

                // 跳过子 section（profile_X.compname）— 只有一个点的是顶级 section
                string afterPrefix = section.Substring("profile_".Length);
                if (afterPrefix.Contains('.'))
                    continue;

                if (!int.TryParse(afterPrefix, out int id))
                    continue;

                string name = (string)config.GetValue(section, "name", "");
                string entityType = (string)config.GetValue(section, "entity_type", "");
                string componentsStr = (string)config.GetValue(section, "components", "");

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
                        var data = ReadComponentData(config, compSection, trimmed);
                        if (data != null)
                            profile.SetData(trimmed, data);
                    }
                }

                // 读取停用组件列表
                string disabledStr = (string)config.GetValue(section, "disabled_components", "");
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

            EnsureDefaultProfiles();
            GD.Print($"[EntityProfileManager] Loaded {_profiles.Count} profiles (new format)");
        }

        private void MigrateConfig(ConfigFile config, int fromVersion)
        {
            // v0-v2 → v3: 旧格式自动转换在新格式加载逻辑中处理
            // 这里只更新版本号
        }

        #endregion
    }
}
