using Godot;

namespace ClinetCSharp
{
    public static partial class EntityDrawUtils
    {
        public static void DrawBody(Node2D node, int outerSize, int innerSize, Color bgColor, float bgOpacity,
            Color borderColor, float borderWidth, float cornerRadius)
        {
            DrawBody(node, outerSize, outerSize, innerSize, innerSize, bgColor, bgOpacity, borderColor, borderWidth, cornerRadius);
        }

        public static void DrawBody(Node2D node, int outerWidth, int outerHeight, int innerWidth, int innerHeight,
            Color bgColor, float bgOpacity, Color borderColor, float borderWidth, float cornerRadius)
        {
            float halfOuterW = outerWidth / 2.0f;
            float halfOuterH = outerHeight / 2.0f;
            float clampedOpacity = Mathf.Clamp(bgOpacity, 0.0f, 1.0f);
            var actualBg = new Color(bgColor.R, bgColor.G, bgColor.B, clampedOpacity);
            int snappedBorder = Mathf.Max(0, Mathf.RoundToInt(borderWidth));
            int snappedCorner = Mathf.Max(0, Mathf.RoundToInt(cornerRadius));

            if (snappedBorder > 0)
            {
                float strokeHalfW = halfOuterW - snappedBorder / 2.0f;
                float strokeHalfH = halfOuterH - snappedBorder / 2.0f;
                var strokeRect = new Rect2(new Vector2(-strokeHalfW, -strokeHalfH), new Vector2(outerWidth - snappedBorder, outerHeight - snappedBorder));
                float strokeRadius = snappedCorner > 0
                    ? Mathf.Max(0.0f, Mathf.Min(snappedCorner, Mathf.Min(outerWidth, outerHeight) / 2.0f) - snappedBorder / 2.0f)
                    : 0;
                node.DrawRoundedRect(strokeRect, borderColor, false, strokeRadius, snappedBorder);
            }

            if (innerWidth > 0 && innerHeight > 0)
            {
                float halfInnerW = innerWidth / 2.0f;
                float halfInnerH = innerHeight / 2.0f;
                var innerRect = new Rect2(new Vector2(-halfInnerW, -halfInnerH), new Vector2(innerWidth, innerHeight));
                if (snappedCorner > 0)
                {
                    float outerRadius = Mathf.Min(snappedCorner, Mathf.Min(outerWidth, outerHeight) / 2.0f);
                    float innerRadius = Mathf.Max(0.0f, outerRadius - snappedBorder);
                    node.DrawRoundedRect(innerRect, actualBg, true, innerRadius);
                }
                else
                {
                    node.DrawRect(innerRect, actualBg, true);
                }
            }
        }

        public static void DrawHealthBar(Node2D node, Vector2 offset, float length, float height,
            float fillPercent, Color bgColor, Color fillColor, bool visible)
        {
            if (!visible) return;
            float halfLen = length / 2.0f;
            float halfH = height / 2.0f;
            float x = offset.X - halfLen;
            float y = offset.Y - halfH;
            node.DrawRect(new Rect2(x, y, length, height), bgColor, true);
            float fillW = length * Mathf.Clamp(fillPercent, 0, 1);
            if (fillW > 0)
                node.DrawRect(new Rect2(x, y, fillW, height), fillColor, true);
        }

        public static void DrawActionBar(Node2D node, float drawSize, string skillName,
            float castProgress, float textYOffset, float progressHeight)
        {
            if (string.IsNullOrEmpty(skillName)) return;
            float halfDraw = drawSize / 2.0f;
            float gap = 2f;
            float barWidth = drawSize;
            float barX = -barWidth / 2f;
            float barY = halfDraw + gap;
            var font = ThemeDB.FallbackFont;
            int fontSize = Mathf.Max((int)(drawSize / 5.0f * 0.7f), 8);
            float lineH = fontSize * 1.3f;
            float pad = 2f;
            float totalH = lineH + progressHeight + pad * 3;
            node.DrawRect(new Rect2(barX, barY, barWidth, totalH), new Color(0.1f, 0.1f, 0.1f, 0.7f), true);
            var textSize = font.GetStringSize(skillName, HorizontalAlignment.Left, -1, fontSize);
            float textX = -textSize.X / 2f;
            float textY = barY + pad + (font.GetAscent(fontSize) - font.GetDescent(fontSize)) * 0.5f + textYOffset;
            node.DrawString(font, new Vector2(textX, textY), skillName, HorizontalAlignment.Left, -1, fontSize, Colors.White);
            float pbX = barX + pad;
            float pbY = barY + pad + lineH + pad;
            float pbW = barWidth - pad * 2;
            node.DrawRect(new Rect2(pbX, pbY, pbW, progressHeight), new Color(0.3f, 0.3f, 0.3f, 0.8f), true);
            float fillW = pbW * Mathf.Clamp(castProgress, 0, 1);
            if (fillW > 0)
                node.DrawRect(new Rect2(pbX, pbY, fillW, progressHeight), new Color(0.99f, 0.84f, 0.0f), true);
        }
    }
}
