using Godot;
using System.Collections.Generic;
using System.Linq;

namespace ClinetCSharp
{
    /// <summary>
    /// EntityProfileManager partial — 旧格式兼容加载
    /// 检测 monster_*/npc_*/player/labels/actionbar/levelbadge section 自动转换
    /// </summary>
    public partial class EntityProfileManager
    {
        private void LoadLegacyFormat(ConfigFile config)
        {
            _profiles.Clear();

            // ---- Player ----
            if (config.HasSection("player"))
            {
                var profile = EntityProfile.CreatePlayerDefault(1);
                var app = profile.GetData<AppearanceData>("appearance");

                app.VisualSizeScale = (float)(double)config.GetValue("player", "visual_size_scale", 1.0);
                app.BorderWidthScale = (float)(double)config.GetValue("player", "border_width_scale", 3.0 / 111.0);
                app.CornerRadius = (float)(double)config.GetValue("player", "corner_radius", 12.0);
                app.BgOpacity = (float)(double)config.GetValue("player", "bg_opacity", 0.1);
                app.FontSize = (int)(double)config.GetValue("player", "font_size", 0);

                float bcR = (float)(double)config.GetValue("player", "border_color_r", 1.0);
                float bcG = (float)(double)config.GetValue("player", "border_color_g", 1.0);
                float bcB = (float)(double)config.GetValue("player", "border_color_b", 1.0);
                app.BorderColor = new Color(bcR, bcG, bcB);

                float bgcR = (float)(double)config.GetValue("player", "bg_color_r", 1.0);
                float bgcG = (float)(double)config.GetValue("player", "bg_color_g", 1.0);
                float bgcB = (float)(double)config.GetValue("player", "bg_color_b", 1.0);
                app.BgColor = new Color(bgcR, bgcG, bgcB);

                float tcR = (float)(double)config.GetValue("player", "text_color_r", 0.0);
                float tcG = (float)(double)config.GetValue("player", "text_color_g", 0.0);
                float tcB = (float)(double)config.GetValue("player", "text_color_b", 0.0);
                app.TextColor = new Color(tcR, tcG, tcB);

                profile.SetData("appearance", app);

                // Labels (from [labels] section)
                if (config.HasSection("labels"))
                {
                    var labels = profile.GetData<LabelGroupData>("labels");
                    if (labels != null)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            string prefix = $"label_{i}";
                            labels.Visible[i] = (bool)config.GetValue("labels", $"{prefix}_visible", true);
                            labels.Names[i] = (string)config.GetValue("labels", $"{prefix}_name", "");
                            labels.ContentPreview[i] = (string)config.GetValue("labels", $"{prefix}_text", "");
                            labels.FontSizes[i] = (int)(double)config.GetValue("labels", $"{prefix}_font_size", 0);
                            labels.XOffset[i] = (float)(double)config.GetValue("labels", $"{prefix}_offset_x", 0);
                            labels.YOffset[i] = (float)(double)config.GetValue("labels", $"{prefix}_offset_y", 0);

                            float lr = (float)(double)config.GetValue("labels", $"{prefix}_color_r", 0.0);
                            float lg = (float)(double)config.GetValue("labels", $"{prefix}_color_g", 0.0);
                            float lb = (float)(double)config.GetValue("labels", $"{prefix}_color_b", 0.0);
                            float la = (float)(double)config.GetValue("labels", $"{prefix}_color_a", 1.0);
                            labels.ColorPreview[i] = new Color(lr, lg, lb, la);
                        }
                        profile.SetData("labels", labels);
                    }
                }

                // Action bar
                if (config.HasSection("actionbar"))
                {
                    var action = profile.GetData<ActionBarData>("actionbar");
                    if (action != null)
                    {
                        action.TextYOffset = (float)(double)config.GetValue("actionbar", "text_y_offset", 0);
                        action.ProgressHeight = (float)(double)config.GetValue("actionbar", "progress_height", 4);
                        profile.SetData("actionbar", action);
                    }
                }

                // Level badge
                if (config.HasSection("levelbadge"))
                {
                    var badge = profile.GetData<LevelBadgeData>("levelbadge");
                    if (badge != null)
                    {
                        badge.Visible = (bool)config.GetValue("levelbadge", "visible", true);
                        badge.FontSize = (float)(double)config.GetValue("levelbadge", "font_size", 12);
                        badge.Text = (string)config.GetValue("levelbadge", "text", "Lv.{level}");
                        badge.OffsetX = (float)(double)config.GetValue("levelbadge", "offset_x", -35);
                        badge.OffsetY = (float)(double)config.GetValue("levelbadge", "offset_y", -35);
                        badge.CenterX = (bool)config.GetValue("levelbadge", "center_x", false);
                        float tr = (float)(double)config.GetValue("levelbadge", "txt_r", 1.0);
                        float tg = (float)(double)config.GetValue("levelbadge", "txt_g", 1.0);
                        float tb = (float)(double)config.GetValue("levelbadge", "txt_b", 0.0);
                        badge.TextColor = new Color(tr, tg, tb);
                        profile.SetData("levelbadge", badge);
                    }
                }

                _profiles[1] = profile;
            }

            // ---- Monster (monster_* sections) ----
            foreach (string section in config.GetSections())
            {
                if (!section.StartsWith("monster_") && section != "monster")
                    continue;

                int id = 2;
                if (section.StartsWith("monster_"))
                {
                    string idPart = section.Substring("monster_".Length);
                    int.TryParse(idPart, out id);
                    if (id < 2) id = 2;
                }

                var cfg = EntityStyleConfig.CreateMonsterDefault();
                MonsterManager.LoadStyleConfigFromSection(config, section, cfg);

                string profileName = section == "monster" ? "怪物" : $"怪物_{id}";
                var profile = EntityProfile.FromStyleConfig(id, cfg, profileName, "monster");

                // Monster AI data
                var monsterConfigMgr = GetTree()
                    .GetFirstNodeInGroup("monster_config_manager") as MonsterConfigManager;
                if (monsterConfigMgr != null)
                {
                    profile.SetData("monster_ai", new MonsterAiData
                    {
                        MoveSpeedMs = monsterConfigMgr.GetMoveSpeedMs(),
                        PatrolRange = 3f,
                        AggroRange = 5f,
                        MoveIntervalMs = 2000,
                    });
                }
                else
                {
                    profile.SetData("monster_ai", new MonsterAiData());
                }

                _profiles[id] = profile;
            }

            // ---- NPC (npc_* sections) ----
            foreach (string section in config.GetSections())
            {
                if (!section.StartsWith("npc_") && section != "npc")
                    continue;

                int id = 3;
                if (section.StartsWith("npc_"))
                {
                    string idPart = section.Substring("npc_".Length);
                    int.TryParse(idPart, out id);
                    if (id < 3) id = 3;
                }

                var cfg = EntityStyleConfig.CreateNpcDefault();
                MonsterManager.LoadStyleConfigFromSection(config, section, cfg);

                string profileName = section == "npc" ? "NPC" : $"NPC_{id}";
                var profile = EntityProfile.FromStyleConfig(id, cfg, profileName, "npc");

                _profiles[id] = profile;
            }

            EnsureDefaultProfiles();
            GD.Print($"[EntityProfileManager] Loaded {_profiles.Count} profiles (legacy format migration)");
        }
    }
}
