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

        private void MigrateBuildingProfileIds()
        {
            // 旧默认建筑 ID 10/11/12 → 新建筑配置 ID
            var migrationMap = new Dictionary<int, int>
            {
                [10] = BuildingType.GetConfigBaseId(BuildingType.House) + 1,  // 房舍
                [11] = BuildingType.GetConfigBaseId(BuildingType.Shop) + 1,   // 商店子配置
                [12] = BuildingType.GetConfigBaseId(BuildingType.Shop) + 2,   // 商店子配置
            };

            foreach (var (oldId, newId) in migrationMap)
            {
                if (!_profiles.ContainsKey(oldId))
                    continue;

                var profile = _profiles[oldId];
                _profiles.Remove(oldId);

                if (_profiles.ContainsKey(newId))
                    continue;

                profile.Id = newId;
                _profiles[newId] = profile;
                GD.Print($"[EntityProfileManager] Migrated decoration profile {oldId} → {newId}");
            }
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
                        {
                            profile.SetData(trimmed, data);
                        }
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

            MigrateBuildingProfileIds();
            EnsureDefaultProfiles();
            GD.Print($"[EntityProfileManager] Loaded {_profiles.Count} profiles (new format)");
        }

        private void MigrateConfig(ConfigFile config, int fromVersion)
        {
            // v0-v2 → v3: 旧格式自动转换在新格式加载逻辑中处理

            // v3 → v4: 比例类字段从 double 转为定点整数
            if (fromVersion < 4)
            {
                var scaleKeys = new HashSet<string>
                {
                    "visual_size_scale", "border_width_scale", "bg_opacity",
                    "length_scale", "height_scale", "fill_percent",
                    "hp_bar_length_scale", "hp_bar_height_scale", "hp_bar_fill_percent",
                    "mp_bar_length_scale", "mp_bar_height_scale", "mp_bar_fill_percent",
                };
                foreach (string section in config.GetSections())
                {
                    foreach (string key in config.GetSectionKeys(section))
                    {
                        if (!scaleKeys.Contains(key)) continue;
                        var v = config.GetValue(section, key, 0);
                        if (v.VariantType == Variant.Type.Float)
                            config.SetValue(section, key, ToFpD((double)v));
                    }
                }
                GD.Print("[EntityProfileManager] Migrated scale values to fixed-point format (v3→v4)");
            }
        }

        #endregion
    }
}
