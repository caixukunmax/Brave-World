using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinetCSharp
{
    /// <summary>
    /// 实体配置档案纯数据 I/O —— 不依赖 Node / GetTree / 运行场景。
    /// EntityProfileManager（运行时 Autoload）与编辑器插件（EditorPlugin）共用，消除双份真相。
    /// 仅负责新格式（profile_* section）的读/写、版本迁移、默认 Profile 补齐。
    /// 旧格式（无 profile_ section，依赖 GetTree 的 monster_*/npc_* 迁移）仍由 EntityProfileManager.LoadLegacyFormat 处理。
    /// </summary>
    public static class ProfileConfigIO
    {
        public const string ConfigPath = "res://debug_panel_config.cfg";
        public const int ConfigVersion = 4; // v4 = 定点整数序列化

        #region Fixed-Point Helpers

        /// <summary>定点整数精度因子：万分之一</summary>
        private const int FpScale = 10000;

        /// <summary>float → 定点整数（用于写入 cfg）</summary>
        internal static int ToFp(float v) => Mathf.RoundToInt(v * FpScale);

        /// <summary>double → 定点整数（用于迁移旧 double 值）</summary>
        internal static int ToFpD(double v) => (int)Math.Round(v * FpScale);

        /// <summary>定点整数 → float（用于从 cfg 读取）</summary>
        internal static float FromFp(int v) => v / (float)FpScale;

        /// <summary>安全读取定点整数值：兼容旧格式(double)和新格式(int)。
        /// 类型既不是 int 也不是 float 时打印警告并回退到默认值，避免静默出错。</summary>
        internal static int ReadFp(ConfigFile config, string section, string key, int defaultFp)
        {
            var v = config.GetValue(section, key, defaultFp);
            if (v.VariantType == Variant.Type.Int) return (int)v;
            if (v.VariantType == Variant.Type.Float) return ToFpD((double)v);
            GD.PushWarning(
                $"[ProfileConfigIO] [{section}] {key} 类型为 {v.VariantType}，" +
                $"期望 int/float，回退到默认值 {defaultFp}（对应浮点 {FromFp(defaultFp):F4}）");
            return defaultFp;
        }

        #endregion

        #region Public Load / Save

        /// <summary>从磁盘加载；文件不存在则返回仅含默认 Profile 的集合。</summary>
        public static Dictionary<int, EntityProfile> LoadFromFile(string path = ConfigPath)
        {
            var config = new ConfigFile();
            Error err = config.Load(path);

            if (err == Error.FileNotFound)
            {
                GD.Print("[ProfileConfigIO] Config file not found, using defaults");
                var def = new Dictionary<int, EntityProfile>();
                EnsureDefaultProfiles(def);
                return def;
            }

            if (err != Error.Ok)
            {
                GD.PushError($"[ProfileConfigIO] Failed to load config: {err}");
                return new Dictionary<int, EntityProfile>();
            }

            bool hasProfileSections = config.GetSections().Any(s => s.StartsWith("profile_"));
            if (!hasProfileSections)
            {
                // 旧格式（无 profile_ section）由运行时 EntityProfileManager.LoadLegacyFormat 处理；
                // 编辑器内不会出现此类旧配置，这里回退到默认 Profile 集合。
                GD.PushWarning("[ProfileConfigIO] 配置文件缺少 profile_ section，将仅用默认 Profiles");
                var def = new Dictionary<int, EntityProfile>();
                EnsureDefaultProfiles(def);
                return def;
            }

            var profiles = new Dictionary<int, EntityProfile>();
            LoadInto(config, profiles);
            return profiles;
        }

        /// <summary>把 profiles 写入磁盘（先加载已有配置，保留非 profile_* 的其他 section）。</summary>
        public static void SaveToFile(Dictionary<int, EntityProfile> profiles, string path = ConfigPath)
        {
            var config = new ConfigFile();
            config.Load(path); // 保留无关 section（如 meta 的其他字段）
            Write(config, profiles);
            Error err = config.Save(path);
            if (err == Error.Ok)
                GD.Print("[ProfileConfigIO] Config saved");
            else
                GD.PushError($"[ProfileConfigIO] Failed to save config: {err}");
        }

        /// <summary>将 profiles 写入给定 ConfigFile（覆盖所有 profile_* section，保留其它）。</summary>
        public static void Write(ConfigFile config, Dictionary<int, EntityProfile> profiles)
        {
            // 清除旧的 Profile section
            foreach (string section in config.GetSections())
                if (section.StartsWith("profile_")) config.EraseSection(section);

            foreach (var profile in profiles.Values)
            {
                string section = $"profile_{profile.Id}";
                config.SetValue(section, "name", profile.Name);
                config.SetValue(section, "entity_type", profile.EntityType);
                config.SetValue(section, "components", string.Join(",", profile.ComponentNames));

                var disabled = profile.ComponentNames.Where(c => profile.IsComponentDisabled(c)).ToList();
                config.SetValue(section, "disabled_components", disabled.Count > 0 ? string.Join(",", disabled) : "");

                foreach (string compName in profile.ComponentNames)
                {
                    var data = profile.GetData(compName);
                    string compSection = $"{section}.{compName}";
                    WriteComponentData(config, compSection, compName, data);
                }
            }
        }

        /// <summary>从 ConfigFile（新格式）读入 profiles（保证默认 Profile 存在），返回下一个可用 ID。</summary>
        public static int LoadInto(ConfigFile config, Dictionary<int, EntityProfile> profiles)
        {
            int savedVersion = (int)config.GetValue("meta", "config_version", 0);
            if (savedVersion < ConfigVersion)
            {
                GD.Print($"[ProfileConfigIO] Migrating config from v{savedVersion} to v{ConfigVersion}");
                MigrateConfig(config, savedVersion);
            }

            profiles.Clear();

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

                profiles[id] = profile;
            }

            MigrateBuildingProfileIds(profiles);
            EnsureDefaultProfiles(profiles);
            GD.Print($"[ProfileConfigIO] Loaded {profiles.Count} profiles (new format)");
            return profiles.Count > 0 ? profiles.Keys.Max() + 1 : 1;
        }

        #endregion

        #region Migration

        private static void MigrateConfig(ConfigFile config, int fromVersion)
        {
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
                GD.Print("[ProfileConfigIO] Migrated scale values to fixed-point format (v3→v4)");
            }
        }

        private static void MigrateBuildingProfileIds(Dictionary<int, EntityProfile> profiles)
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
                if (!profiles.ContainsKey(oldId))
                    continue;

                var profile = profiles[oldId];
                profiles.Remove(oldId);

                if (profiles.ContainsKey(newId))
                    continue;

                profile.Id = newId;
                profiles[newId] = profile;
                GD.Print($"[ProfileConfigIO] Migrated decoration profile {oldId} → {newId}");
            }
        }

        #endregion

        #region Default Profiles

        /// <summary>确保默认 Profile 存在；返回下一个可用 ID（最大 ID + 1）。</summary>
        public static int EnsureDefaultProfiles(Dictionary<int, EntityProfile> profiles)
        {
            if (!profiles.ContainsKey(1))
                profiles[1] = EntityProfile.CreatePlayerDefault(1);
            if (!profiles.ContainsKey(2))
                profiles[2] = EntityProfile.CreateMonsterDefault(2);
            if (!profiles.ContainsKey(3))
                profiles[3] = EntityProfile.CreateNpcDefault(3);

            // 默认建筑 Profiles（同 EntityProfileManager.EnsureDefaultProfiles 的规范）
            EnsureDeco(profiles, BuildingType.GetConfigBaseId(BuildingType.House), "TreeLegacy", "树(旧)", BuildingType.House,
                new Color(0.2f, 0.5f, 0.25f, 0.9f), new Color(0.1f, 0.35f, 0.15f), false, 1, 1, "Legacy");
            EnsureDeco(profiles, BuildingType.GetConfigBaseId(BuildingType.House) + 1, "House", "房舍", BuildingType.House,
                new Color(0.545f, 0.353f, 0.169f, 0.9f), new Color(0.4f, 0.2f, 0.1f), true, 2, 2, "Building");
            EnsureDeco(profiles, BuildingType.GetConfigBaseId(BuildingType.House) + 2, "RockLegacy", "岩石(旧)", BuildingType.House,
                new Color(0.53f, 0.53f, 0.53f, 0.9f), new Color(0.35f, 0.35f, 0.35f), true, 1, 1, "Legacy");
            EnsureDeco(profiles, BuildingType.GetConfigBaseId(BuildingType.House) + 3, "GrassLegacy", "草地(旧)", BuildingType.House,
                new Color(0.35f, 0.65f, 0.35f, 0.9f), new Color(0.2f, 0.45f, 0.2f), false, 1, 1, "Legacy");
            EnsureDeco(profiles, BuildingType.GetConfigBaseId(BuildingType.Shop), "Shop", "商店", BuildingType.Shop,
                new Color(0.2f, 0.4f, 0.6f, 0.9f), new Color(0.1f, 0.3f, 0.5f), true, 1, 1, "Building");
            EnsureDeco(profiles, BuildingType.GetConfigBaseId(BuildingType.Well), "Well", "水井", BuildingType.Well,
                new Color(0.5f, 0.5f, 0.55f, 0.9f), new Color(0.3f, 0.3f, 0.35f), true, 1, 1, "Building");
            EnsureDeco(profiles, BuildingType.GetConfigBaseId(BuildingType.Farm), "Farm", "农田", BuildingType.Farm,
                new Color(0.8f, 0.7f, 0.3f, 0.9f), new Color(0.5f, 0.4f, 0.1f), true, 2, 1, "Building");
            EnsureDeco(profiles, BuildingType.GetConfigBaseId(BuildingType.Tavern), "Tavern", "酒馆", BuildingType.Tavern,
                new Color(0.6f, 0.3f, 0.2f, 0.9f), new Color(0.4f, 0.15f, 0.1f), true, 2, 2, "Building");
            EnsureDeco(profiles, BuildingType.GetConfigBaseId(BuildingType.SpawnPoint), "SpawnPoint", "出生点", BuildingType.SpawnPoint,
                new Color(0.2f, 0.8f, 0.9f, 0.9f), new Color(0.1f, 0.5f, 0.6f), false, 1, 1, "Special");
            EnsureDeco(profiles, BuildingType.GetConfigBaseId(BuildingType.Portal), "Portal", "共享传送门", BuildingType.Portal,
                new Color(0.6f, 0.2f, 0.9f, 0.9f), new Color(0.4f, 0.1f, 0.7f), false, 1, 1, "Special");
            EnsureDeco(profiles, BuildingType.GetConfigBaseId(BuildingType.Water), "Water", "水", BuildingType.Water,
                new Color(0.29f, 0.56f, 0.85f, 0.9f), new Color(0.15f, 0.35f, 0.6f), true, 1, 1, "Terrain");
            EnsureDeco(profiles, BuildingType.GetConfigBaseId(BuildingType.Rock), "Rock", "岩石", BuildingType.Rock,
                new Color(0.53f, 0.53f, 0.53f, 0.9f), new Color(0.35f, 0.35f, 0.35f), true, 1, 1, "Terrain");
            EnsureDeco(profiles, BuildingType.GetConfigBaseId(BuildingType.Tree), "Tree", "树", BuildingType.Tree,
                new Color(0.2f, 0.5f, 0.25f, 0.9f), new Color(0.1f, 0.35f, 0.15f), false, 1, 1, "Terrain");
            EnsureDeco(profiles, BuildingType.GetConfigBaseId(BuildingType.Grass), "Grass", "草地", BuildingType.Grass,
                new Color(0.35f, 0.65f, 0.35f, 0.9f), new Color(0.2f, 0.45f, 0.2f), false, 1, 1, "Terrain");

            return profiles.Count > 0 ? profiles.Keys.Max() + 1 : 1;
        }

        private static void EnsureDeco(Dictionary<int, EntityProfile> profiles,
            int id, string name, string displayName, int buildingType, Color bgColor, Color borderColor, bool blockMovement,
            int sizeX, int sizeY, string category)
        {
            if (profiles.TryGetValue(id, out var profile))
            {
                // 仅补 category 组件，避免覆盖用户其他自定义
                var catData = profile.GetData<CategoryData>("category");
                if (catData == null)
                    profile.SetData("category", new CategoryData { Category = category });
                else
                    catData.Category = category;
                return;
            }

            profile = EntityProfile.CreateDecorationDefault(id, name, displayName, buildingType, bgColor, borderColor, blockMovement, sizeX, sizeY, category);
            profiles[id] = profile;
        }

        #endregion

        #region Component Data Serialization — Write

        private static void WriteComponentData(ConfigFile config, string section,
            string compName, IComponentData data)
        {
            switch (data)
            {
                case AppearanceData app:
                    config.SetValue(section, "visual_size_scale", ToFp(app.VisualSizeScale));
                    config.SetValue(section, "size_x", app.SizeX);
                    config.SetValue(section, "size_y", app.SizeY);
                    config.SetValue(section, "border_width_scale", ToFp(app.BorderWidthScale));
                    config.SetValue(section, "corner_radius", (double)app.CornerRadius);
                    config.SetValue(section, "bg_opacity", ToFp(app.BgOpacity));
                    config.SetValue(section, "font_size", app.FontSize);
                    config.SetValue(section, "border_color_r", (double)app.BorderColor.R);
                    config.SetValue(section, "border_color_g", (double)app.BorderColor.G);
                    config.SetValue(section, "border_color_b", (double)app.BorderColor.B);
                    config.SetValue(section, "bg_color_r", (double)app.BgColor.R);
                    config.SetValue(section, "bg_color_g", (double)app.BgColor.G);
                    config.SetValue(section, "bg_color_b", (double)app.BgColor.B);
                    config.SetValue(section, "text_color_r", (double)app.TextColor.R);
                    config.SetValue(section, "text_color_g", (double)app.TextColor.G);
                    config.SetValue(section, "text_color_b", (double)app.TextColor.B);
                    break;

                case LabelGroupData labels:
                    config.SetValue(section, "default_font_size", labels.DefaultFontSize);
                    config.SetValue(section, "bold", labels.Bold);
                    config.SetValue(section, "italic", labels.Italic);
                    config.SetValue(section, "shadow", labels.Shadow);
                    for (int i = 0; i < 4; i++)
                    {
                        string prefix = $"label_{i}";
                        config.SetValue(section, $"{prefix}_visible", labels.Visible[i]);
                        config.SetValue(section, $"{prefix}_name", labels.Names[i] ?? "");
                        config.SetValue(section, $"{prefix}_content", labels.ContentPreview[i] ?? "");
                        config.SetValue(section, $"{prefix}_use_global_font_size", labels.UseGlobalFontSize[i]);
                        config.SetValue(section, $"{prefix}_font_size", labels.FontSizes[i]);
                        config.SetValue(section, $"{prefix}_x_offset", (double)labels.XOffset[i]);
                        config.SetValue(section, $"{prefix}_center_x", labels.CenterX[i]);
                        config.SetValue(section, $"{prefix}_y_offset", (double)labels.YOffset[i]);
                    }
                    break;

                case BarData bar:
                    config.SetValue(section, "visible", bar.Visible);
                    config.SetValue(section, "length_scale", ToFp(bar.LengthScale));
                    config.SetValue(section, "height_scale", ToFp(bar.HeightScale));
                    config.SetValue(section, "fill_percent", ToFp(bar.FillPercent));
                    config.SetValue(section, "center_x", bar.CenterX);
                    config.SetValue(section, "offset_x", (double)bar.OffsetX);
                    config.SetValue(section, "offset_y", (double)bar.OffsetY);
                    config.SetValue(section, "color_r", (double)bar.Color.R);
                    config.SetValue(section, "color_g", (double)bar.Color.G);
                    config.SetValue(section, "color_b", (double)bar.Color.B);
                    break;

                case CastBarData cast:
                    config.SetValue(section, "visible", cast.Visible);
                    config.SetValue(section, "length_scale", ToFp(cast.LengthScale));
                    config.SetValue(section, "height_scale", ToFp(cast.HeightScale));
                    config.SetValue(section, "fill_percent", ToFp(cast.FillPercent));
                    config.SetValue(section, "center_x", cast.CenterX);
                    config.SetValue(section, "offset_x", (double)cast.OffsetX);
                    config.SetValue(section, "offset_y", (double)cast.OffsetY);
                    config.SetValue(section, "color_r", (double)cast.Color.R);
                    config.SetValue(section, "color_g", (double)cast.Color.G);
                    config.SetValue(section, "color_b", (double)cast.Color.B);
                    break;

                case ActionBarData action:
                    config.SetValue(section, "force_show", action.ForceShow);
                    config.SetValue(section, "text_y_offset", (double)action.TextYOffset);
                    config.SetValue(section, "progress_height", (double)action.ProgressHeight);
                    break;

                case NameplateData np:
                    config.SetValue(section, "visible", np.Visible);
                    config.SetValue(section, "y_offset", (double)np.YOffset);
                    config.SetValue(section, "spacing", (double)np.Spacing);
                    config.SetValue(section, "bar_height", (double)np.BarHeight);
                    // 铭牌颜色带有意义的 alpha，必须连 alpha 一起写盘
                    config.SetValue(section, "bar_color_r", (double)np.BarColor.R);
                    config.SetValue(section, "bar_color_g", (double)np.BarColor.G);
                    config.SetValue(section, "bar_color_b", (double)np.BarColor.B);
                    config.SetValue(section, "bar_color_a", (double)np.BarColor.A);
                    config.SetValue(section, "center_box_height", (double)np.CenterBoxHeight);
                    config.SetValue(section, "center_box_width_scale", ToFp(np.CenterBoxWidthScale));
                    config.SetValue(section, "center_box_color_r", (double)np.CenterBoxColor.R);
                    config.SetValue(section, "center_box_color_g", (double)np.CenterBoxColor.G);
                    config.SetValue(section, "center_box_color_b", (double)np.CenterBoxColor.B);
                    config.SetValue(section, "center_box_color_a", (double)np.CenterBoxColor.A);
                    break;

                case LevelBadgeData badge:
                    config.SetValue(section, "visible", badge.Visible);
                    config.SetValue(section, "font_size", (double)badge.FontSize);
                    config.SetValue(section, "text_color_r", (double)badge.TextColor.R);
                    config.SetValue(section, "text_color_g", (double)badge.TextColor.G);
                    config.SetValue(section, "text_color_b", (double)badge.TextColor.B);
                    config.SetValue(section, "text", badge.Text ?? "");
                    config.SetValue(section, "offset_x", (double)badge.OffsetX);
                    config.SetValue(section, "offset_y", (double)badge.OffsetY);
                    config.SetValue(section, "center_x", badge.CenterX);
                    break;

                case MonsterAiData ai:
                    config.SetValue(section, "move_speed_ms", ai.MoveSpeedMs);
                    config.SetValue(section, "patrol_range", (double)ai.PatrolRange);
                    config.SetValue(section, "aggro_range", (double)ai.AggroRange);
                    config.SetValue(section, "move_interval_ms", ai.MoveIntervalMs);
                    break;

                case NpcInteractData npc:
                    config.SetValue(section, "offset_a_x", (double)npc.OffsetAX);
                    config.SetValue(section, "offset_a_y", (double)npc.OffsetAY);
                    config.SetValue(section, "offset_b_x", (double)npc.OffsetBX);
                    config.SetValue(section, "offset_b_y", (double)npc.OffsetBY);
                    break;

                case ObstacleData obstacle:
                    config.SetValue(section, "block_movement", obstacle.BlockMovement);
                    break;

                case CategoryData cat:
                    config.SetValue(section, "category", cat.Category ?? "");
                    break;

                case BuildingTypeData buildingType:
                    config.SetValue(section, "building_type", buildingType.Type);
                    break;
            }
        }

        #endregion

        #region Component Data Serialization — Read

        private static IComponentData ReadComponentData(ConfigFile config, string section, string compName)
        {
            if (!config.HasSection(section))
                return null;

            switch (compName)
            {
                case "appearance":
                    return new AppearanceData
                    {
                        VisualSizeScale = FromFp(ReadFp(config, section, "visual_size_scale", 10000)),
                        SizeX = (int)(double)config.GetValue(section, "size_x", 1),
                        SizeY = (int)(double)config.GetValue(section, "size_y", 1),
                        BorderWidthScale = FromFp(ReadFp(config, section, "border_width_scale", 270)),
                        CornerRadius = (float)(double)config.GetValue(section, "corner_radius", 12.0),
                        BgOpacity = FromFp(ReadFp(config, section, "bg_opacity", 9000)),
                        FontSize = (int)(double)config.GetValue(section, "font_size", 0),
                        BorderColor = new Color(
                            (float)(double)config.GetValue(section, "border_color_r", 1.0),
                            (float)(double)config.GetValue(section, "border_color_g", 1.0),
                            (float)(double)config.GetValue(section, "border_color_b", 1.0)),
                        BgColor = new Color(
                            (float)(double)config.GetValue(section, "bg_color_r", 1.0),
                            (float)(double)config.GetValue(section, "bg_color_g", 1.0),
                            (float)(double)config.GetValue(section, "bg_color_b", 1.0)),
                        TextColor = new Color(
                            (float)(double)config.GetValue(section, "text_color_r", 0.0),
                            (float)(double)config.GetValue(section, "text_color_g", 0.0),
                            (float)(double)config.GetValue(section, "text_color_b", 0.0)),
                    };

                case "labels":
                    var labels = new LabelGroupData
                    {
                        DefaultFontSize = (int)(double)config.GetValue(section, "default_font_size", 0),
                        Bold = (bool)config.GetValue(section, "bold", false),
                        Italic = (bool)config.GetValue(section, "italic", false),
                        Shadow = (bool)config.GetValue(section, "shadow", false),
                    };
                    for (int i = 0; i < 4; i++)
                    {
                        string prefix = $"label_{i}";
                        labels.Visible[i] = (bool)config.GetValue(section, $"{prefix}_visible", true);
                        labels.Names[i] = (string)config.GetValue(section, $"{prefix}_name", "");
                        labels.ContentPreview[i] = (string)config.GetValue(section, $"{prefix}_content", "");
                        labels.FontSizes[i] = (int)(double)config.GetValue(section, $"{prefix}_font_size", 0);
                        labels.UseGlobalFontSize[i] = (bool)config.GetValue(section, $"{prefix}_use_global_font_size", labels.FontSizes[i] <= 0);
                        labels.XOffset[i] = (float)(double)config.GetValue(section, $"{prefix}_x_offset", 0);
                        labels.CenterX[i] = (bool)config.GetValue(section, $"{prefix}_center_x", true);
                        labels.YOffset[i] = (float)(double)config.GetValue(section, $"{prefix}_y_offset", 0);
                    }
                    return labels;

                case "healthbar":
                case "mpbar":
                    return new BarData
                    {
                        Visible = (bool)config.GetValue(section, "visible", true),
                        LengthScale = FromFp(ReadFp(config, section, "length_scale", compName == "mpbar" ? 7207 : 9189)),
                        HeightScale = FromFp(ReadFp(config, section, "height_scale", compName == "mpbar" ? 360 : 541)),
                        FillPercent = FromFp(ReadFp(config, section, "fill_percent", 10000)),
                        CenterX = (bool)config.GetValue(section, "center_x", true),
                        OffsetX = (float)(double)config.GetValue(section, "offset_x", 0),
                        OffsetY = (float)(double)config.GetValue(section, "offset_y", compName == "mpbar" ? -62 : -70),
                        Color = new Color(
                            (float)(double)config.GetValue(section, "color_r", 0.0),
                            (float)(double)config.GetValue(section, "color_g", 0.8),
                            (float)(double)config.GetValue(section, "color_b", 0.0)),
                    };

                case "castbar":
                    return new CastBarData
                    {
                        Visible = (bool)config.GetValue(section, "visible", true),
                        LengthScale = FromFp(ReadFp(config, section, "length_scale", 5405)),
                        HeightScale = FromFp(ReadFp(config, section, "height_scale", 360)),
                        FillPercent = FromFp(ReadFp(config, section, "fill_percent", 0)),
                        CenterX = (bool)config.GetValue(section, "center_x", true),
                        OffsetX = (float)(double)config.GetValue(section, "offset_x", 0),
                        OffsetY = (float)(double)config.GetValue(section, "offset_y", -80),
                        Color = new Color(
                            (float)(double)config.GetValue(section, "color_r", 0.3),
                            (float)(double)config.GetValue(section, "color_g", 0.5),
                            (float)(double)config.GetValue(section, "color_b", 1.0)),
                    };

                case "actionbar":
                    return new ActionBarData
                    {
                        ForceShow = (bool)config.GetValue(section, "force_show", false),
                        TextYOffset = (float)(double)config.GetValue(section, "text_y_offset", 0),
                        ProgressHeight = (float)(double)config.GetValue(section, "progress_height", 4),
                    };

                case "nameplate":
                    return new NameplateData
                    {
                        Visible = (bool)config.GetValue(section, "visible", false),
                        YOffset = (float)(double)config.GetValue(section, "y_offset", -80.0),
                        Spacing = (float)(double)config.GetValue(section, "spacing", 4.0),
                        BarHeight = (float)(double)config.GetValue(section, "bar_height", 6.0),
                        BarColor = new Color(
                            (float)(double)config.GetValue(section, "bar_color_r", 0.1),
                            (float)(double)config.GetValue(section, "bar_color_g", 0.1),
                            (float)(double)config.GetValue(section, "bar_color_b", 0.1),
                            (float)(double)config.GetValue(section, "bar_color_a", 0.7)),
                        CenterBoxHeight = (float)(double)config.GetValue(section, "center_box_height", 24.0),
                        CenterBoxWidthScale = FromFp(ReadFp(config, section, "center_box_width_scale", 6000)),
                        CenterBoxColor = new Color(
                            (float)(double)config.GetValue(section, "center_box_color_r", 0.1),
                            (float)(double)config.GetValue(section, "center_box_color_g", 0.1),
                            (float)(double)config.GetValue(section, "center_box_color_b", 0.1),
                            (float)(double)config.GetValue(section, "center_box_color_a", 0.85)),
                    };

                case "levelbadge":
                    return new LevelBadgeData
                    {
                        Visible = (bool)config.GetValue(section, "visible", true),
                        FontSize = (float)(double)config.GetValue(section, "font_size", 12),
                        TextColor = new Color(
                            (float)(double)config.GetValue(section, "text_color_r", 1.0),
                            (float)(double)config.GetValue(section, "text_color_g", 1.0),
                            (float)(double)config.GetValue(section, "text_color_b", 0.0)),
                        Text = (string)config.GetValue(section, "text", "Lv.{level}"),
                        OffsetX = (float)(double)config.GetValue(section, "offset_x", -35),
                        OffsetY = (float)(double)config.GetValue(section, "offset_y", -35),
                        CenterX = (bool)config.GetValue(section, "center_x", false),
                    };

                case "monster_ai":
                    return new MonsterAiData
                    {
                        MoveSpeedMs = (int)(double)config.GetValue(section, "move_speed_ms", 800),
                        PatrolRange = (float)(double)config.GetValue(section, "patrol_range", 3.0),
                        AggroRange = (float)(double)config.GetValue(section, "aggro_range", 5.0),
                        MoveIntervalMs = (int)(double)config.GetValue(section, "move_interval_ms", 2000),
                    };

                case "npc_interact":
                    return new NpcInteractData
                    {
                        OffsetAX = (float)(double)config.GetValue(section, "offset_a_x", 60),
                        OffsetAY = (float)(double)config.GetValue(section, "offset_a_y", -20),
                        OffsetBX = (float)(double)config.GetValue(section, "offset_b_x", -60),
                        OffsetBY = (float)(double)config.GetValue(section, "offset_b_y", -20),
                    };

                case "obstacle":
                    return new ObstacleData
                    {
                        BlockMovement = (bool)config.GetValue(section, "block_movement", true),
                    };

                case "category":
                    return new CategoryData
                    {
                        Category = (string)config.GetValue(section, "category", ""),
                    };

                case "building_type":
                {
                    var typeValue = config.GetValue(section, "building_type", BuildingType.House);
                    int type;
                    switch (typeValue.VariantType)
                    {
                        case Variant.Type.Int:
                            type = (int)typeValue;
                            break;
                        case Variant.Type.Float:
                            type = (int)(double)typeValue;
                            break;
                        case Variant.Type.String:
                            type = ParseLegacyBuildingType((string)typeValue);
                            break;
                        default:
                            GD.PushWarning(
                                $"[ProfileConfigIO] [{section}] building_type 类型为 " +
                                $"{typeValue.VariantType}，期望 int/float/string，回退到 House");
                            type = BuildingType.House;
                            break;
                    }
                    if (!BuildingType.IsValid(type))
                    {
                        GD.PushWarning(
                            $"[ProfileConfigIO] [{section}] building_type={type} 非法，回退到 House");
                        type = BuildingType.House;
                    }
                    return new BuildingTypeData { Type = type };
                }

                default:
                    GD.PushWarning($"[ProfileConfigIO] Unknown component type: {compName}");
                    return null;
            }
        }

        private static int ParseLegacyBuildingType(string value)
        {
            return value?.Trim() switch
            {
                "房舍" => BuildingType.House,
                "商店" => BuildingType.Shop,
                "水井" => BuildingType.Well,
                "农田" => BuildingType.Farm,
                "酒馆" => BuildingType.Tavern,
                "出生点" => BuildingType.SpawnPoint,
                "共享传送门" => BuildingType.Portal,
                "水" => BuildingType.Water,
                "岩石" => BuildingType.Rock,
                "树" => BuildingType.Tree,
                "草地" => BuildingType.Grass,
                "民居" => BuildingType.House,
                "军事" => BuildingType.Shop,
                "装饰" => BuildingType.House,
                "障碍" => BuildingType.House,
                "资源" => BuildingType.Shop,
                _ => int.TryParse(value, out int parsed) && BuildingType.IsValid(parsed) ? parsed : BuildingType.House,
            };
        }

        #endregion
    }
}
