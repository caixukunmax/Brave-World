using Godot;

namespace ClinetCSharp
{
    public partial class Player
    {
        internal void UpdateLabelFontSize()
        {
            var globalFontSize = EntityLabelLayout.ResolveBaseFontSize(FontSizeOverride, VisualSize);

            const int minFontSize = 8;
            if (globalFontSize < minFontSize)
                globalFontSize = minFontSize;

            for (int i = 0; i < LabelCount; i++)
            {
                if (_labels[i] == null)
                    continue;

                var fontSize = _labelFontSizes[i] > 0 ? _labelFontSizes[i] : globalFontSize;
                if (fontSize < minFontSize)
                    fontSize = minFontSize;

                _labels[i].AddThemeFontSizeOverride("normal_font_size", fontSize);
                ApplyLabelTextStyle(_labels[i]);
                ApplyLabelShadow(_labels[i]);
                _labels[i].AddThemeConstantOverride("character_spacing", (int)LetterSpacing);
            }

            UpdateAllLabelPositions();
        }

        private void ApplyLabelTextStyle(RichTextLabel label)
        {
            var originalText = label.GetParsedText();
            var bbcodeText = "";
            if (FontBold)
                bbcodeText += "[b]";
            if (FontItalic)
                bbcodeText += "[i]";
            bbcodeText += originalText;
            if (FontItalic)
                bbcodeText += "[/i]";
            if (FontBold)
                bbcodeText += "[/b]";
            label.Text = bbcodeText;
        }

        private void ApplyLabelShadow(RichTextLabel label)
        {
            if (FontShadow)
            {
                label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.5f));
                label.AddThemeConstantOverride("shadow_offset_x", 2);
                label.AddThemeConstantOverride("shadow_offset_y", 2);
            }
            else
            {
                label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0));
            }
        }
    }
}
