using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// EntityProfileManager partial — 组件数据序列化（Write/Read）
    /// </summary>
    public partial class EntityProfileManager
    {
        #region Component Data Serialization — Write

        private void WriteComponentData(ConfigFile config, string section,
            string compName, IComponentData data)
        {
            switch (data)
            {
                case AppearanceData app:
                    config.SetValue(section, "visual_size_scale", (double)app.VisualSizeScale);
                    config.SetValue(section, "border_width_scale", (double)app.BorderWidthScale);
                    config.SetValue(section, "corner_radius", (double)app.CornerRadius);
                    config.SetValue(section, "bg_opacity", (double)app.BgOpacity);
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
                    config.SetValue(section, "font_name", labels.FontName);
                    config.SetValue(section, "default_font_size", labels.DefaultFontSize);
                    config.SetValue(section, "default_color_r", (double)labels.DefaultColor.R);
                    config.SetValue(section, "default_color_g", (double)labels.DefaultColor.G);
                    config.SetValue(section, "default_color_b", (double)labels.DefaultColor.B);
                    config.SetValue(section, "bold", labels.Bold);
                    config.SetValue(section, "italic", labels.Italic);
                    config.SetValue(section, "shadow", labels.Shadow);
                    for (int i = 0; i < 4; i++)
                    {
                        string prefix = $"label_{i}";
                        config.SetValue(section, $"{prefix}_visible", labels.Visible[i]);
                        config.SetValue(section, $"{prefix}_name", labels.Names[i] ?? "");
                        config.SetValue(section, $"{prefix}_content", labels.ContentPreview[i] ?? "");
                        config.SetValue(section, $"{prefix}_font_size", labels.FontSizes[i]);
                        config.SetValue(section, $"{prefix}_color_r", (double)labels.ColorPreview[i].R);
                        config.SetValue(section, $"{prefix}_color_g", (double)labels.ColorPreview[i].G);
                        config.SetValue(section, $"{prefix}_color_b", (double)labels.ColorPreview[i].B);
                        config.SetValue(section, $"{prefix}_x_offset", (double)labels.XOffset[i]);
                        config.SetValue(section, $"{prefix}_center_x", labels.CenterX[i]);
                        config.SetValue(section, $"{prefix}_y_offset", (double)labels.YOffset[i]);
                    }
                    break;

                case BarData bar:
                    config.SetValue(section, "visible", bar.Visible);
                    config.SetValue(section, "length_scale", (double)bar.LengthScale);
                    config.SetValue(section, "height_scale", (double)bar.HeightScale);
                    config.SetValue(section, "fill_percent", (double)bar.FillPercent);
                    config.SetValue(section, "center_x", bar.CenterX);
                    config.SetValue(section, "offset_x", (double)bar.OffsetX);
                    config.SetValue(section, "offset_y", (double)bar.OffsetY);
                    config.SetValue(section, "color_r", (double)bar.Color.R);
                    config.SetValue(section, "color_g", (double)bar.Color.G);
                    config.SetValue(section, "color_b", (double)bar.Color.B);
                    break;

                case CastBarData cast:
                    config.SetValue(section, "visible", cast.Visible);
                    config.SetValue(section, "length_scale", (double)cast.LengthScale);
                    config.SetValue(section, "height_scale", (double)cast.HeightScale);
                    config.SetValue(section, "fill_percent", (double)cast.FillPercent);
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
                        VisualSizeScale = (float)(double)config.GetValue(section, "visual_size_scale", 1.0),
                        BorderWidthScale = (float)(double)config.GetValue(section, "border_width_scale", 3.0 / 111.0),
                        CornerRadius = (float)(double)config.GetValue(section, "corner_radius", 12.0),
                        BgOpacity = (float)(double)config.GetValue(section, "bg_opacity", 0.9),
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
                        FontName = (string)config.GetValue(section, "font_name", ""),
                        DefaultFontSize = (int)(double)config.GetValue(section, "default_font_size", 0),
                        DefaultColor = new Color(
                            (float)(double)config.GetValue(section, "default_color_r", 0.0),
                            (float)(double)config.GetValue(section, "default_color_g", 0.0),
                            (float)(double)config.GetValue(section, "default_color_b", 0.0)),
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
                        labels.ColorPreview[i] = new Color(
                            (float)(double)config.GetValue(section, $"{prefix}_color_r", 0.0),
                            (float)(double)config.GetValue(section, $"{prefix}_color_g", 0.0),
                            (float)(double)config.GetValue(section, $"{prefix}_color_b", 0.0));
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
                        LengthScale = (float)(double)config.GetValue(section, "length_scale", 102.0 / 111.0),
                        HeightScale = (float)(double)config.GetValue(section, "height_scale", 6.0 / 111.0),
                        FillPercent = (float)(double)config.GetValue(section, "fill_percent", 1.0),
                        CenterX = (bool)config.GetValue(section, "center_x", true),
                        OffsetX = (float)(double)config.GetValue(section, "offset_x", 0),
                        OffsetY = (float)(double)config.GetValue(section, "offset_y", -70),
                        Color = new Color(
                            (float)(double)config.GetValue(section, "color_r", 0.0),
                            (float)(double)config.GetValue(section, "color_g", 0.8),
                            (float)(double)config.GetValue(section, "color_b", 0.0)),
                    };

                case "castbar":
                    return new CastBarData
                    {
                        Visible = (bool)config.GetValue(section, "visible", true),
                        LengthScale = (float)(double)config.GetValue(section, "length_scale", 60.0 / 111.0),
                        HeightScale = (float)(double)config.GetValue(section, "height_scale", 4.0 / 111.0),
                        FillPercent = (float)(double)config.GetValue(section, "fill_percent", 0.0),
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

                default:
                    GD.PushWarning($"[EntityProfileManager] Unknown component type: {compName}");
                    return null;
            }
        }

        #endregion
    }
}
