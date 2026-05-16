using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 共享绘制工具 — Player / Monster 共用的绘制逻辑
    /// </summary>
    public static class EntityDrawUtils
    {
        /// <summary>绘制角色方块（圆角矩形）</summary>
        public static void DrawBody(Node2D node, int outerSize, int innerSize, Color bgColor, float bgOpacity,
            Color borderColor, float borderWidth, float cornerRadius)
        {
            float halfOuter = outerSize / 2.0f;
            var outerRect = new Rect2(new Vector2(-halfOuter, -halfOuter), new Vector2(outerSize, outerSize));
            float clampedOpacity = Mathf.Clamp(bgOpacity, 0.0f, 1.0f);
            // The inner fill should represent the final visible color instead of
            // blending with the border underlay, otherwise low-opacity black gets
            // tinted by a colored border and looks maroon instead of black.
            var actualBg = new Color(
                bgColor.R * clampedOpacity,
                bgColor.G * clampedOpacity,
                bgColor.B * clampedOpacity,
                1.0f);
            int snappedBorder = Mathf.Max(0, Mathf.RoundToInt(borderWidth));
            int snappedCorner = Mathf.Max(0, Mathf.RoundToInt(cornerRadius));

            if (snappedCorner > 0)
            {
                float outerRadius = Mathf.Min(snappedCorner, outerSize / 2.0f);
                node.DrawRoundedRect(outerRect, borderColor, true, outerRadius);

                if (innerSize > 0)
                {
                    float halfInner = innerSize / 2.0f;
                    var innerRect = new Rect2(new Vector2(-halfInner, -halfInner), new Vector2(innerSize, innerSize));
                    float innerRadius = Mathf.Max(0.0f, outerRadius - snappedBorder);
                    node.DrawRoundedRect(innerRect, actualBg, true, innerRadius);
                }
            }
            else
            {
                node.DrawRect(outerRect, borderColor, true);
                if (innerSize > 0)
                {
                    float halfInner = innerSize / 2.0f;
                    var innerRect = new Rect2(new Vector2(-halfInner, -halfInner), new Vector2(innerSize, innerSize));
                    node.DrawRect(innerRect, actualBg, true);
                }
            }
        }

        /// <summary>绘制血条</summary>
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

        /// <summary>绘制动作栏（角色下方）</summary>
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
                node.DrawRect(new Rect2(pbX, pbY, fillW, progressHeight), new Color("#FFD700"), true);
        }
    }
}
