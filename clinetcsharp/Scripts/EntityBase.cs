using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 实体基类 — 玩家、怪物、NPC 共享的外观、标签、血条/MP条
    /// 设计原则：Scale 是唯一真相源，绝对值是计算属性，不可能不一致
    /// </summary>
    public partial class EntityBase : Node2D
    {
        // ========== 外观 ==========
        public float VisualSizeScale { get; set; } = 1.0f;
        public float BorderWidthScale { get; set; } = 3.0f / 111.0f;
        public Color BorderColor { get; set; } = Colors.White;
        public Color BgColor { get; set; } = new Color(1, 1, 1, 0.1f);
        public Color TextColor { get; set; } = Colors.Black;
        public float CornerRadius { get; set; } = 12.0f;
        public float BgOpacity { get; set; } = 0.1f;
        public int FontSize { get; set; } = 0; // 0 = 自动

        // ========== 外观 — 计算属性（只读） ==========
        public int VisualSize => Mathf.Clamp((int)(GridSize * VisualSizeScale), 10, GridSize);
        public float BorderWidth => Mathf.Clamp(GridSize * BorderWidthScale, 1.0f, 20.0f);

        // ========== 血条 ==========
        public Vector2 HealthBarOffset { get; set; } = new Vector2(0, -70);
        public float HealthBarLengthScale { get; set; } = 102.0f / 111.0f;
        public float HealthBarHeightScale { get; set; } = 6.0f / 111.0f;
        public Color HealthBarColor { get; set; } = new Color(0, 0.8f, 0, 1);
        public Color HealthBarBgColor { get; set; } = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        public bool HealthBarVisible { get; set; } = true;
        public float HealthBarFillPercent { get; set; } = 1.0f;

        // ========== 血条 — 计算属性（只读） ==========
        public float HealthBarLength => Mathf.Clamp(GridSize * HealthBarLengthScale, 10.0f, GridSize * 2.0f);
        public float HealthBarHeight => Mathf.Clamp(GridSize * HealthBarHeightScale, 2.0f, GridSize);

        // ========== MP条 ==========
        public Vector2 MpBarOffset { get; set; } = new Vector2(0, -62);
        public float MpBarLengthScale { get; set; } = 80.0f / 111.0f;
        public float MpBarHeightScale { get; set; } = 4.0f / 111.0f;
        public Color MpBarColor { get; set; } = new Color(0.2f, 0.4f, 1.0f, 1);
        public Color MpBarBgColor { get; set; } = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        public bool MpBarVisible { get; set; } = true;
        public float MpBarFillPercent { get; set; } = 1.0f;

        // ========== MP条 — 计算属性（只读） ==========
        public float MpBarLength => Mathf.Clamp(GridSize * MpBarLengthScale, 10.0f, GridSize * 2.0f);
        public float MpBarHeight => Mathf.Clamp(GridSize * MpBarHeightScale, 2.0f, GridSize);

        // ========== 4 行文字标签 ==========
        public string[] LabelTexts = new string[4] { "", "", "", "" };
        public int[] LabelFontSizes = new int[4] { 0, 0, 0, 0 };
        public float[] LabelXOffsets = new float[4] { 0, 0, 0, 0 };
        public bool[] LabelCenterX = new bool[4] { true, true, true, true };
        public float[] LabelYOffsets = new float[4] { 0, 0, 0, 0 };

        // ========== GridSize（子类可 override） ==========
        private int _gridSize = 111;
        public virtual int GridSize { get => _gridSize; set => _gridSize = value; }

        // ========== 外观 setter ==========
        public void SetVisualSizeScale(float scale) { VisualSizeScale = scale; QueueRedraw(); }
        public void SetBorderWidthScale(float scale) { BorderWidthScale = scale; QueueRedraw(); }
        public void SetBorderColor(Color color) { BorderColor = color; QueueRedraw(); }
        public void SetBgColor(Color color) { BgColor = color; QueueRedraw(); }
        public void SetTextColor(Color color) { TextColor = color; QueueRedraw(); }
        public void SetCornerRadius(float radius) { CornerRadius = radius; QueueRedraw(); }
        public void SetBgOpacity(float opacity) { BgOpacity = opacity; QueueRedraw(); }
        public void SetFontSize(int size) { FontSize = size; QueueRedraw(); }

        // ========== 血条 setter ==========
        public Vector2 GetHealthBarOffset() => HealthBarOffset;
        public void SetHealthBarOffset(Vector2 offset) { HealthBarOffset = offset; QueueRedraw(); }
        public void SetHealthBarLengthScale(float scale) { HealthBarLengthScale = scale; QueueRedraw(); }
        public void SetHealthBarHeightScale(float scale) { HealthBarHeightScale = scale; QueueRedraw(); }
        public void SetHealthBarColor(Color color) { HealthBarColor = color; QueueRedraw(); }
        public void SetHealthBarBgColor(Color color) { HealthBarBgColor = color; QueueRedraw(); }
        public void SetHealthBarFillPercent(float percent) { HealthBarFillPercent = Mathf.Clamp(percent, 0, 1); QueueRedraw(); }
        public void SetHealthBarVisible(bool visible) { HealthBarVisible = visible; QueueRedraw(); }

        // ========== MP条 setter ==========
        public Vector2 GetMpBarOffset() => MpBarOffset;
        public void SetMpBarOffset(Vector2 offset) { MpBarOffset = offset; QueueRedraw(); }
        public void SetMpBarLengthScale(float scale) { MpBarLengthScale = scale; QueueRedraw(); }
        public void SetMpBarHeightScale(float scale) { MpBarHeightScale = scale; QueueRedraw(); }
        public void SetMpBarColor(Color color) { MpBarColor = color; QueueRedraw(); }
        public void SetMpBarBgColor(Color color) { MpBarBgColor = color; QueueRedraw(); }
        public void SetMpBarFillPercent(float percent) { MpBarFillPercent = Mathf.Clamp(percent, 0, 1); QueueRedraw(); }
        public void SetMpBarVisible(bool visible) { MpBarVisible = visible; QueueRedraw(); }

        // ========== 标签 setter ==========
        public void SetLabelText(int index, string text)
        {
            if (index < 0 || index >= 4) return;
            LabelTexts[index] = text;
            QueueRedraw();
        }
        public void SetLabelFontSize(int index, int size)
        {
            if (index < 0 || index >= 4) return;
            LabelFontSizes[index] = size;
            QueueRedraw();
        }
        public void SetLabelXOffset(int index, float offset)
        {
            if (index < 0 || index >= 4) return;
            LabelXOffsets[index] = offset;
            QueueRedraw();
        }
        public void SetLabelCenterX(int index, bool center)
        {
            if (index < 0 || index >= 4) return;
            LabelCenterX[index] = center;
            QueueRedraw();
        }
        public void SetLabelYOffset(int index, float offset)
        {
            if (index < 0 || index >= 4) return;
            LabelYOffsets[index] = offset;
            QueueRedraw();
        }

        // ========== 绘制 ==========
        protected void DrawBars()
        {
            EntityDrawUtils.DrawHealthBar(this, HealthBarOffset, HealthBarLength, HealthBarHeight,
                HealthBarFillPercent, HealthBarBgColor, HealthBarColor, HealthBarVisible);

            EntityDrawUtils.DrawHealthBar(this, MpBarOffset, MpBarLength, MpBarHeight,
                MpBarFillPercent, MpBarBgColor, MpBarColor, MpBarVisible);
        }

        protected void DrawLabels()
        {
            var font = ThemeDB.FallbackFont;
            int baseFs = FontSize > 0 ? FontSize : Mathf.Max((int)(VisualSize / 4.0f * 0.7f), 8);
            float baseLineHeight = baseFs * 1.1f;
            float totalHeight = baseLineHeight * 4;
            float startY = -(totalHeight / 2.0f) + baseLineHeight * 0.5f;

            for (int i = 0; i < 4; i++)
            {
                if (string.IsNullOrEmpty(LabelTexts[i])) continue;
                int fs = LabelFontSizes[i] > 0 ? LabelFontSizes[i] : baseFs;
                float posY = startY + i * baseLineHeight + LabelYOffsets[i];
                float posX = LabelCenterX[i] ? 0 : LabelXOffsets[i];

                var textSize = font.GetStringSize(LabelTexts[i], HorizontalAlignment.Left, -1, fs);
                float drawX = posX - textSize.X / 2f;
                float baselineY = posY + (font.GetAscent(fs) - font.GetDescent(fs)) * 0.5f;
                DrawString(font, new Vector2(drawX, baselineY), LabelTexts[i], HorizontalAlignment.Left, -1, fs, TextColor);
            }
        }
    }
}
