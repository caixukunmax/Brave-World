using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 共享绘制工具 — Player / Monster 共用的绘制逻辑
    /// </summary>
    public static class EntityDrawUtils
    {
        /// <summary>绘制角色方块（圆角矩形）</summary>
        public static void DrawBody(Node2D node, int drawSize, Color bgColor, float bgOpacity,
            Color borderColor, float borderWidth, float cornerRadius)
        {
            float halfDraw = drawSize / 2.0f;
            var rect = new Rect2(new Vector2(-halfDraw, -halfDraw), new Vector2(drawSize, drawSize));
            var actualBg = new Color(bgColor.R, bgColor.G, bgColor.B, bgOpacity);

            if (cornerRadius > 0)
            {
                float maxR = halfDraw - borderWidth;
                float r = Mathf.Min(cornerRadius, Mathf.Max(maxR, 0));
                node.DrawRoundedRect(rect, actualBg, true, r);
                node.DrawRoundedRect(rect, borderColor, false, r, borderWidth);
            }
            else
            {
                node.DrawRect(rect, actualBg, true);
                node.DrawRect(rect, borderColor, false, borderWidth);
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
