using Godot;
using System;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// EntityProfileManager partial — 组件数据序列化（Write/Read）
    /// </summary>
    public partial class EntityProfileManager
    {
        #region Fixed-Point Serialization Helpers

        /// <summary>定点整数精度因子：万分之一</summary>
        private const int FpScale = 10000;

        /// <summary>float → 定点整数（用于写入 cfg）</summary>
        internal static int ToFp(float v) => Mathf.RoundToInt(v * FpScale);

        /// <summary>double → 定点整数（用于迁移旧 double 值）</summary>
        internal static int ToFpD(double v) => (int)Math.Round(v * FpScale);

        /// <summary>定点整数 → float（用于从 cfg 读取）</summary>
        internal static float FromFp(int v) => v / (float)FpScale;

        /// <summary>
        /// 安全读取定点整数值：兼容旧格式(double)和新格式(int)
        /// </summary>
        internal static int ReadFp(ConfigFile config, string section, string key, int defaultFp)
        {
            var v = config.GetValue(section, key, defaultFp);
            if (v.VariantType == Variant.Type.Int) return (int)v;
            if (v.VariantType == Variant.Type.Float) return ToFpD((double)v);
            return defaultFp;
        }

        #endregion

        #region Component Data Serialization — Write

        private void WriteComponentData(ConfigFile config, string section,
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

        private IComponentData ReadComponentData(ConfigFile config, string section, string compName)
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
                    int type = typeValue.VariantType switch
                    {
                        Variant.Type.Int => (int)typeValue,
                        Variant.Type.Float => (int)(double)typeValue,
                        Variant.Type.String => ParseLegacyBuildingType((string)typeValue),
                        _ => BuildingType.House,
                    };
                    return new BuildingTypeData { Type = type };
                }

                default:
                    GD.PushWarning($"[EntityProfileManager] Unknown component type: {compName}");
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
