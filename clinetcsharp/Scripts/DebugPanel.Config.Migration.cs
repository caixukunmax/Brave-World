using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    public partial class DebugPanel
    {
        private void MigrateConfig(ConfigFile config, int fromVersion)
        {
            if (fromVersion < 1)
            {
            }

            if (fromVersion < 2)
            {
                if (config.HasSection("monster"))
                {
                    foreach (string key in config.GetSectionKeys("monster"))
                    {
                        Variant value = config.GetValue("monster", key);
                        config.SetValue("monster_1", key, value);
                    }
                    config.EraseSection("monster");
                    GD.Print("[DebugPanel] Migrated [monster] -> [monster_1]");
                }

                if (config.HasSection("npc"))
                {
                    foreach (string key in config.GetSectionKeys("npc"))
                    {
                        Variant value = config.GetValue("npc", key);
                        config.SetValue("npc_1", key, value);
                    }
                    config.EraseSection("npc");
                    GD.Print("[DebugPanel] Migrated [npc] -> [npc_1]");
                }
            }

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
                        if (!scaleKeys.Contains(key))
                            continue;

                        var v = config.GetValue(section, key, 0);
                        if (v.VariantType == Variant.Type.Float)
                            config.SetValue(section, key, EntityProfileManager.ToFpD((double)v));
                    }
                }

                GD.Print("[DebugPanel] Migrated scale values to fixed-point format (v3->v4)");
            }
        }
    }
}
