using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// Shared four-line label layout math for player / monster / npc.
    /// Same visual parameters should resolve to the same line anchors.
    /// </summary>
    public static class EntityLabelLayout
    {
        public const int LabelCount = 4;

        public static int ResolveBaseFontSize(int explicitFontSize, int visualSize)
        {
            return explicitFontSize > 0
                ? explicitFontSize
                : Mathf.Max((int)(visualSize / 4.0f * 0.7f), 8);
        }

        public static float ResolveLineHeight(int baseFontSize)
        {
            return baseFontSize * 1.1f;
        }

        public static float ResolveStartY(float lineHeight, int labelCount = LabelCount)
        {
            float totalHeight = lineHeight * labelCount;
            return -(totalHeight / 2.0f) + lineHeight * 0.5f;
        }

        public static Vector2 ResolveLineCenter(
            int lineIndex,
            int explicitFontSize,
            int visualSize,
            bool centerX,
            float offsetX,
            float offsetY,
            int labelCount = LabelCount)
        {
            int baseFontSize = ResolveBaseFontSize(explicitFontSize, visualSize);
            float lineHeight = ResolveLineHeight(baseFontSize);
            float startY = ResolveStartY(lineHeight, labelCount);
            float x = centerX ? 0.0f : offsetX;
            float y = startY + lineIndex * lineHeight + offsetY;
            return new Vector2(x, y);
        }
    }
}
