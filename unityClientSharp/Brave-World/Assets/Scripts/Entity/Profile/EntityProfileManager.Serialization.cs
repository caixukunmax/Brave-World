using UnityEngine;
using UnityClientSharp.Map.Core;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// EntityProfileManager partial — 组件数据序列化（Write/Read），移植自 Godot 端 EntityProfileManager.Serialization.cs。
    /// Path B 持久化底座（JSON + 停用组件功能）+ 第二轮决策补齐的注册表层：
    /// 覆盖全部 12 类组件（appearance/labels/obstacle/healthbar/mpbar/castbar→BarData/
    /// category/building_type/actionbar/nameplate/levelbadge/monster_ai/npc_interact）。
    /// 定点整数（ToFp/FromFp）已废弃（JSON 保真 float，直接读写）。
    /// </summary>
    public static partial class EntityProfileManager
    {
        #region Component Data Serialization — Write

        private static void WriteComponentData(IProfileStore store, string section,
            string compName, IComponentData data)
        {
            switch (data)
            {
                case AppearanceData app:
                    store.SetValue(section, "visual_size_scale", app.VisualSizeScale);
                    store.SetValue(section, "size_x", app.SizeX);
                    store.SetValue(section, "size_y", app.SizeY);
                    store.SetValue(section, "border_width_scale", app.BorderWidthScale);
                    store.SetValue(section, "corner_radius", app.CornerRadius);
                    store.SetValue(section, "bg_opacity", app.BgOpacity);
                    store.SetValue(section, "font_size", app.FontSize);
                    store.SetValue(section, "border_color_r", app.BorderColor.r);
                    store.SetValue(section, "border_color_g", app.BorderColor.g);
                    store.SetValue(section, "border_color_b", app.BorderColor.b);
                    store.SetValue(section, "bg_color_r", app.BgColor.r);
                    store.SetValue(section, "bg_color_g", app.BgColor.g);
                    store.SetValue(section, "bg_color_b", app.BgColor.b);
                    store.SetValue(section, "text_color_r", app.TextColor.r);
                    store.SetValue(section, "text_color_g", app.TextColor.g);
                    store.SetValue(section, "text_color_b", app.TextColor.b);
                    break;

                case LabelGroupData labels:
                    store.SetValue(section, "default_font_size", labels.DefaultFontSize);
                    store.SetValue(section, "bold", labels.Bold);
                    store.SetValue(section, "italic", labels.Italic);
                    store.SetValue(section, "shadow", labels.Shadow);
                    for (int i = 0; i < LabelGroupData.LabelCount; i++)
                    {
                        string prefix = $"label_{i}";
                        store.SetValue(section, $"{prefix}_visible", labels.Visible[i]);
                        store.SetValue(section, $"{prefix}_name", labels.Names[i] ?? "");
                        store.SetValue(section, $"{prefix}_content", labels.ContentPreview[i] ?? "");
                        store.SetValue(section, $"{prefix}_use_global_font_size", labels.UseGlobalFontSize[i]);
                        store.SetValue(section, $"{prefix}_font_size", labels.FontSizes[i]);
                        store.SetValue(section, $"{prefix}_x_offset", labels.XOffset[i]);
                        store.SetValue(section, $"{prefix}_center_x", labels.CenterX[i]);
                        store.SetValue(section, $"{prefix}_y_offset", labels.YOffset[i]);
                    }
                    break;

                // healthbar / mpbar / castbar 在 Unity 端统一用 BarData（castbar 折叠进 BarData）
                case BarData bar:
                    store.SetValue(section, "visible", bar.Visible);
                    store.SetValue(section, "length_scale", bar.LengthScale);
                    store.SetValue(section, "height_scale", bar.HeightScale);
                    store.SetValue(section, "fill_percent", bar.FillPercent);
                    store.SetValue(section, "center_x", bar.CenterX);
                    store.SetValue(section, "offset_x", bar.OffsetX);
                    store.SetValue(section, "offset_y", bar.OffsetY);
                    store.SetValue(section, "color_r", bar.Color.r);
                    store.SetValue(section, "color_g", bar.Color.g);
                    store.SetValue(section, "color_b", bar.Color.b);
                    break;

                case ObstacleData obstacle:
                    store.SetValue(section, "block_movement", obstacle.BlockMovement);
                    break;

                case CategoryData cat:
                    store.SetValue(section, "category", cat.Category ?? "");
                    break;

                case BuildingTypeData buildingType:
                    store.SetValue(section, "building_type", buildingType.Type);
                    break;

                // —— 第二轮决策（2026-07-18）：注册表层补齐，原先顺延到战斗/交互阶段的 5 类一并落地 ——
                case ActionBarData ab:
                    store.SetValue(section, "force_show", ab.ForceShow);
                    store.SetValue(section, "text_y_offset", ab.TextYOffset);
                    store.SetValue(section, "progress_height", ab.ProgressHeight);
                    break;

                case NameplateData np:
                    store.SetValue(section, "visible", np.Visible);
                    store.SetValue(section, "y_offset", np.YOffset);
                    store.SetValue(section, "spacing", np.Spacing);
                    store.SetValue(section, "bar_height", np.BarHeight);
                    store.SetValue(section, "bar_color_r", np.BarColor.r);
                    store.SetValue(section, "bar_color_g", np.BarColor.g);
                    store.SetValue(section, "bar_color_b", np.BarColor.b);
                    store.SetValue(section, "bar_color_a", np.BarColor.a);
                    store.SetValue(section, "center_box_height", np.CenterBoxHeight);
                    store.SetValue(section, "center_box_width_scale", np.CenterBoxWidthScale);
                    store.SetValue(section, "center_box_color_r", np.CenterBoxColor.r);
                    store.SetValue(section, "center_box_color_g", np.CenterBoxColor.g);
                    store.SetValue(section, "center_box_color_b", np.CenterBoxColor.b);
                    store.SetValue(section, "center_box_color_a", np.CenterBoxColor.a);
                    break;

                case LevelBadgeData lb:
                    store.SetValue(section, "visible", lb.Visible);
                    store.SetValue(section, "font_size", lb.FontSize);
                    store.SetValue(section, "text_color_r", lb.TextColor.r);
                    store.SetValue(section, "text_color_g", lb.TextColor.g);
                    store.SetValue(section, "text_color_b", lb.TextColor.b);
                    store.SetValue(section, "text", lb.Text ?? "Lv.{level}");
                    store.SetValue(section, "offset_x", lb.OffsetX);
                    store.SetValue(section, "offset_y", lb.OffsetY);
                    store.SetValue(section, "center_x", lb.CenterX);
                    break;

                case MonsterAiData ai:
                    store.SetValue(section, "move_speed_ms", ai.MoveSpeedMs);
                    store.SetValue(section, "patrol_range", ai.PatrolRange);
                    store.SetValue(section, "aggro_range", ai.AggroRange);
                    store.SetValue(section, "move_interval_ms", ai.MoveIntervalMs);
                    break;

                case NpcInteractData ni:
                    store.SetValue(section, "offset_a_x", ni.OffsetAX);
                    store.SetValue(section, "offset_a_y", ni.OffsetAY);
                    store.SetValue(section, "offset_b_x", ni.OffsetBX);
                    store.SetValue(section, "offset_b_y", ni.OffsetBY);
                    break;
            }
        }

        #endregion

        #region Component Data Serialization — Read

        private static IComponentData ReadComponentData(IProfileStore store, string section, string compName)
        {
            if (!store.HasSection(section))
                return null;

            switch (compName)
            {
                case "appearance":
                    return new AppearanceData
                    {
                        VisualSizeScale = store.GetValue(section, "visual_size_scale", 1.0f),
                        SizeX = store.GetValue(section, "size_x", 1),
                        SizeY = store.GetValue(section, "size_y", 1),
                        BorderWidthScale = store.GetValue(section, "border_width_scale", 3.0f / 111.0f),
                        CornerRadius = store.GetValue(section, "corner_radius", 12.0f),
                        BgOpacity = store.GetValue(section, "bg_opacity", 0.9f),
                        FontSize = store.GetValue(section, "font_size", 0),
                        BorderColor = new Color(
                            store.GetValue(section, "border_color_r", 1.0f),
                            store.GetValue(section, "border_color_g", 1.0f),
                            store.GetValue(section, "border_color_b", 1.0f)),
                        BgColor = new Color(
                            store.GetValue(section, "bg_color_r", 1.0f),
                            store.GetValue(section, "bg_color_g", 1.0f),
                            store.GetValue(section, "bg_color_b", 1.0f)),
                        TextColor = new Color(
                            store.GetValue(section, "text_color_r", 0.0f),
                            store.GetValue(section, "text_color_g", 0.0f),
                            store.GetValue(section, "text_color_b", 0.0f)),
                    };

                case "labels":
                    var labels = new LabelGroupData
                    {
                        DefaultFontSize = store.GetValue(section, "default_font_size", 0),
                        Bold = store.GetValue(section, "bold", false),
                        Italic = store.GetValue(section, "italic", false),
                        Shadow = store.GetValue(section, "shadow", false),
                    };
                    for (int i = 0; i < LabelGroupData.LabelCount; i++)
                    {
                        string prefix = $"label_{i}";
                        labels.Visible[i] = store.GetValue(section, $"{prefix}_visible", true);
                        labels.Names[i] = store.GetValue(section, $"{prefix}_name", "");
                        labels.ContentPreview[i] = store.GetValue(section, $"{prefix}_content", "");
                        labels.FontSizes[i] = store.GetValue(section, $"{prefix}_font_size", 0);
                        labels.UseGlobalFontSize[i] = store.GetValue(section, $"{prefix}_use_global_font_size", labels.FontSizes[i] <= 0);
                        labels.XOffset[i] = store.GetValue(section, $"{prefix}_x_offset", 0f);
                        labels.CenterX[i] = store.GetValue(section, $"{prefix}_center_x", true);
                        labels.YOffset[i] = store.GetValue(section, $"{prefix}_y_offset", 0f);
                    }
                    return labels;

                case "healthbar":
                case "mpbar":
                    return new BarData
                    {
                        Visible = store.GetValue(section, "visible", true),
                        LengthScale = store.GetValue(section, "length_scale", compName == "mpbar" ? 80f / 111f : 102f / 111f),
                        HeightScale = store.GetValue(section, "height_scale", compName == "mpbar" ? 4f / 111f : 6f / 111f),
                        FillPercent = store.GetValue(section, "fill_percent", 1.0f),
                        CenterX = store.GetValue(section, "center_x", true),
                        OffsetX = store.GetValue(section, "offset_x", 0f),
                        OffsetY = store.GetValue(section, "offset_y", compName == "mpbar" ? -62f : -70f),
                        Color = new Color(
                            store.GetValue(section, "color_r", 0.0f),
                            store.GetValue(section, "color_g", compName == "mpbar" ? 0.4f : 0.8f),
                            store.GetValue(section, "color_b", compName == "mpbar" ? 1.0f : 0.0f)),
                    };

                case "castbar":
                    return new BarData
                    {
                        Visible = store.GetValue(section, "visible", true),
                        LengthScale = store.GetValue(section, "length_scale", 60f / 111f),
                        HeightScale = store.GetValue(section, "height_scale", 4f / 111f),
                        FillPercent = store.GetValue(section, "fill_percent", 0f),
                        CenterX = store.GetValue(section, "center_x", true),
                        OffsetX = store.GetValue(section, "offset_x", 0f),
                        OffsetY = store.GetValue(section, "offset_y", -80f),
                        Color = new Color(
                            store.GetValue(section, "color_r", 0.3f),
                            store.GetValue(section, "color_g", 0.5f),
                            store.GetValue(section, "color_b", 1.0f)),
                    };

                case "obstacle":
                    return new ObstacleData
                    {
                        BlockMovement = store.GetValue(section, "block_movement", true),
                    };

                case "category":
                    return new CategoryData
                    {
                        Category = store.GetValue(section, "category", ""),
                    };

                case "building_type":
                {
                    int type = store.GetValue(section, "building_type", BuildingType.House);
                    if (!BuildingType.IsValid(type)) type = BuildingType.House;
                    return new BuildingTypeData { Type = type };
                }

                // —— 第二轮决策（2026-07-18）：注册表层补齐，原先顺延到战斗/交互阶段的 5 类一并落地 ——
                case "actionbar":
                    return new ActionBarData
                    {
                        ForceShow = store.GetValue(section, "force_show", false),
                        TextYOffset = store.GetValue(section, "text_y_offset", 0f),
                        ProgressHeight = store.GetValue(section, "progress_height", 4f),
                    };

                case "nameplate":
                    return new NameplateData
                    {
                        Visible = store.GetValue(section, "visible", false),
                        YOffset = store.GetValue(section, "y_offset", -80f),
                        Spacing = store.GetValue(section, "spacing", 4f),
                        BarHeight = store.GetValue(section, "bar_height", 6f),
                        BarColor = new Color(
                            store.GetValue(section, "bar_color_r", 0.1f),
                            store.GetValue(section, "bar_color_g", 0.1f),
                            store.GetValue(section, "bar_color_b", 0.1f),
                            store.GetValue(section, "bar_color_a", 0.7f)),
                        CenterBoxHeight = store.GetValue(section, "center_box_height", 24f),
                        CenterBoxWidthScale = store.GetValue(section, "center_box_width_scale", 0.6f),
                        CenterBoxColor = new Color(
                            store.GetValue(section, "center_box_color_r", 0.1f),
                            store.GetValue(section, "center_box_color_g", 0.1f),
                            store.GetValue(section, "center_box_color_b", 0.1f),
                            store.GetValue(section, "center_box_color_a", 0.85f)),
                    };

                case "levelbadge":
                    return new LevelBadgeData
                    {
                        Visible = store.GetValue(section, "visible", true),
                        FontSize = store.GetValue(section, "font_size", 12f),
                        TextColor = new Color(
                            store.GetValue(section, "text_color_r", 1.0f),
                            store.GetValue(section, "text_color_g", 1.0f),
                            store.GetValue(section, "text_color_b", 0.0f)),
                        Text = store.GetValue(section, "text", "Lv.{level}"),
                        OffsetX = store.GetValue(section, "offset_x", -35f),
                        OffsetY = store.GetValue(section, "offset_y", -35f),
                        CenterX = store.GetValue(section, "center_x", false),
                    };

                case "monster_ai":
                    return new MonsterAiData
                    {
                        MoveSpeedMs = store.GetValue(section, "move_speed_ms", 800),
                        PatrolRange = store.GetValue(section, "patrol_range", 3f),
                        AggroRange = store.GetValue(section, "aggro_range", 5f),
                        MoveIntervalMs = store.GetValue(section, "move_interval_ms", 2000),
                    };

                case "npc_interact":
                    return new NpcInteractData
                    {
                        OffsetAX = store.GetValue(section, "offset_a_x", 60f),
                        OffsetAY = store.GetValue(section, "offset_a_y", -20f),
                        OffsetBX = store.GetValue(section, "offset_b_x", -60f),
                        OffsetBY = store.GetValue(section, "offset_b_y", -20f),
                    };

                default:
                    Debug.LogWarning($"[EntityProfileManager] Unknown/unsupported component type: {compName}");
                    return null;
            }
        }

        #endregion
    }
}
