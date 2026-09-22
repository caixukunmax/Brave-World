using System.Collections.Generic;
using UnityEngine;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// 技能图标目录（简化版）— 从 Godot SkillIconCatalog 抽取调色板部分（background/secondary/accent/outline），
    /// 不含 24 种矢量 glyph（UI 阶段补）。本期图标 = bg 底 + accent 斜条点缀的色块。
    /// </summary>
    public static class SkillIconCatalog
    {
        public readonly struct SkillIconSpec
        {
            public SkillIconSpec(Color background, Color secondary, Color accent, Color outline)
            {
                Background = background;
                Secondary = secondary;
                Accent = accent;
                Outline = outline;
            }

            public Color Background { get; }
            public Color Secondary { get; }
            public Color Accent { get; }
            public Color Outline { get; }
        }

        private static readonly SkillIconSpec DefaultSpec = new(
            HexColor("#253046"), HexColor("#35507A"), HexColor("#DDE9FF"), HexColor("#91A7D8"));

        public static SkillIconSpec Get(uint skillId)
        {
            return skillId switch
            {
                1 => Make("#4A2218", "#8A3A2A", "#FFE0C8", "#FFAA7A"),
                2 => Make("#6A1C18", "#A43324", "#FFD8C2", "#FFAD85"),
                3 => Make("#3A2948", "#67438A", "#E8D9FF", "#C89BFF"),
                4 => Make("#4D2018", "#A14B21", "#FFE5B4", "#FFC27A"),
                5 => Make("#5B1D12", "#C94B20", "#FFE2B3", "#FFAA5C"),
                6 => Make("#123A5B", "#2C77B8", "#E3F6FF", "#8ED2FF"),
                7 => Make("#2A215A", "#5C46C7", "#F0E8FF", "#BCA8FF"),
                9 => Make("#1A5C3A", "#2DA26D", "#E6FFE9", "#99F0B6"),
                10 => Make("#6D5416", "#B89426", "#FFF5D0", "#FFDC72"),
                11 => Make("#5A2D19", "#AA6032", "#FFE9D7", "#FFBE93"),
                12 => Make("#4F2418", "#973722", "#FFE1CB", "#FFB28E"),
                13 => Make("#284D1B", "#4F9C35", "#EBFFD7", "#B7F48B"),
                14 => Make("#4A3030", "#8A5151", "#FFF0E8", "#F7C2A6"),
                20 => Make("#6A261B", "#B9412B", "#FFF0DA", "#FFC37B"),
                21 => Make("#4A4332", "#7B6A4D", "#F5EFDE", "#D9C89E"),
                22 => Make("#314719", "#597E2A", "#EBFFC6", "#C7F076"),
                23 => Make("#184563", "#317AB0", "#E4F7FF", "#9EDFFF"),
                24 => Make("#514215", "#A98C31", "#FFF9DA", "#FFE17A"),
                25 => Make("#6A5516", "#B99828", "#FFF4C7", "#FFE47B"),
                26 => Make("#395117", "#5E8B22", "#F2FFD2", "#C5F27C"),
                27 => Make("#1B4A56", "#2F8096", "#E4FBFF", "#A3EAFF"),
                30 => Make("#5A231F", "#A23E34", "#FFE2DA", "#FFAA99"),
                31 => Make("#5E1E14", "#CC542E", "#FFE0C9", "#FFB16C"),
                32 => Make("#24594A", "#3B9C82", "#E6FFF8", "#A0F0D9"),
                33 => Make("#5A1829", "#A5274A", "#FFE0EA", "#FF9CB8"),
                _ => DefaultSpec,
            };
        }

        private static SkillIconSpec Make(string bg, string secondary, string accent, string outline)
            => new SkillIconSpec(HexColor(bg), HexColor(secondary), HexColor(accent), HexColor(outline));

        private static Color HexColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        // ---- 色块图标纹理（bg 底 + accent 斜条点缀，44px 基准，按 skillId 缓存）----

        private static readonly Dictionary<uint, Sprite> _spriteCache = new();

        public static Sprite GetSprite(uint skillId)
        {
            if (_spriteCache.TryGetValue(skillId, out var cached) && cached != null)
                return cached;

            var spec = Get(skillId);
            const int size = 44;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"SkillIcon_{skillId}",
            };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color c = spec.Background;
                    // 左下到右上的 accent 斜条带（两条）
                    int diag = x + y;
                    if (Mathf.Abs(diag - size) <= 3 || Mathf.Abs(diag - size * 3 / 2) <= 2)
                        c = spec.Accent;
                    else if (diag < size)
                        c = spec.Secondary;
                    // 1px outline 边框
                    if (x < 1 || y < 1 || x >= size - 1 || y >= size - 1)
                        c = spec.Outline;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            _spriteCache[skillId] = sprite;
            return sprite;
        }
    }
}
