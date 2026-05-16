using Godot;

namespace ClinetCSharp
{
    public enum SkillIconGlyph
    {
        None,
        Slash,
        Shield,
        Whirlwind,
        Fireball,
        Snowflake,
        ArcaneBolt,
        Heal,
        HolyBurst,
        Smite,
        Fangs,
        Howl,
        Spear,
        Roar,
        StoneSkin,
        PoisonCloud,
        Freeze,
        Bless,
        VenomFang,
        Breath,
        Taunt,
        Flame,
        Cleanse,
        Berserk,
    }

    public readonly struct SkillIconSpec
    {
        public SkillIconSpec(Color background, Color secondary, Color accent, Color outline, SkillIconGlyph glyph)
        {
            Background = background;
            Secondary = secondary;
            Accent = accent;
            Outline = outline;
            Glyph = glyph;
        }

        public Color Background { get; }
        public Color Secondary { get; }
        public Color Accent { get; }
        public Color Outline { get; }
        public SkillIconGlyph Glyph { get; }
    }

    public static class SkillIconCatalog
    {
        private static readonly SkillIconSpec DefaultSpec = new(
            new Color("#253046"),
            new Color("#35507A"),
            new Color("#DDE9FF"),
            new Color("#91A7D8"),
            SkillIconGlyph.None);

        public static SkillIconSpec Get(uint skillId)
        {
            return skillId switch
            {
                2 => Make("#6A1C18", "#A43324", "#FFD8C2", "#FFAD85", SkillIconGlyph.Slash),
                3 => Make("#3A2948", "#67438A", "#E8D9FF", "#C89BFF", SkillIconGlyph.Shield),
                4 => Make("#4D2018", "#A14B21", "#FFE5B4", "#FFC27A", SkillIconGlyph.Whirlwind),
                5 => Make("#5B1D12", "#C94B20", "#FFE2B3", "#FFAA5C", SkillIconGlyph.Fireball),
                6 => Make("#123A5B", "#2C77B8", "#E3F6FF", "#8ED2FF", SkillIconGlyph.Snowflake),
                7 => Make("#2A215A", "#5C46C7", "#F0E8FF", "#BCA8FF", SkillIconGlyph.ArcaneBolt),
                9 => Make("#1A5C3A", "#2DA26D", "#E6FFE9", "#99F0B6", SkillIconGlyph.Heal),
                10 => Make("#6D5416", "#B89426", "#FFF5D0", "#FFDC72", SkillIconGlyph.HolyBurst),
                11 => Make("#5A2D19", "#AA6032", "#FFE9D7", "#FFBE93", SkillIconGlyph.Smite),
                12 => Make("#4F2418", "#973722", "#FFE1CB", "#FFB28E", SkillIconGlyph.Fangs),
                13 => Make("#284D1B", "#4F9C35", "#EBFFD7", "#B7F48B", SkillIconGlyph.Howl),
                14 => Make("#4A3030", "#8A5151", "#FFF0E8", "#F7C2A6", SkillIconGlyph.Spear),
                20 => Make("#6A261B", "#B9412B", "#FFF0DA", "#FFC37B", SkillIconGlyph.Roar),
                21 => Make("#4A4332", "#7B6A4D", "#F5EFDE", "#D9C89E", SkillIconGlyph.StoneSkin),
                22 => Make("#314719", "#597E2A", "#EBFFC6", "#C7F076", SkillIconGlyph.PoisonCloud),
                23 => Make("#184563", "#317AB0", "#E4F7FF", "#9EDFFF", SkillIconGlyph.Freeze),
                24 => Make("#514215", "#A98C31", "#FFF9DA", "#FFE17A", SkillIconGlyph.Shield),
                25 => Make("#6A5516", "#B99828", "#FFF4C7", "#FFE47B", SkillIconGlyph.Bless),
                26 => Make("#395117", "#5E8B22", "#F2FFD2", "#C5F27C", SkillIconGlyph.VenomFang),
                27 => Make("#1B4A56", "#2F8096", "#E4FBFF", "#A3EAFF", SkillIconGlyph.Breath),
                30 => Make("#5A231F", "#A23E34", "#FFE2DA", "#FFAA99", SkillIconGlyph.Taunt),
                31 => Make("#5E1E14", "#CC542E", "#FFE0C9", "#FFB16C", SkillIconGlyph.Flame),
                32 => Make("#24594A", "#3B9C82", "#E6FFF8", "#A0F0D9", SkillIconGlyph.Cleanse),
                33 => Make("#5A1829", "#A5274A", "#FFE0EA", "#FF9CB8", SkillIconGlyph.Berserk),
                _ => DefaultSpec,
            };
        }

        private static SkillIconSpec Make(string background, string secondary, string accent, string outline, SkillIconGlyph glyph)
        {
            return new SkillIconSpec(new Color(background), new Color(secondary), new Color(accent), new Color(outline), glyph);
        }
    }
}
