using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 实体样式配置 — 存储一套完整的视觉属性
    /// Monster 按 MonsterId 绑定配置，NPC 按 NpcType 绑定配置
    /// 设计原则：只存 Scale，Length/Height 是计算值（GridSize × Scale）
    /// </summary>
    public class EntityStyleConfig
    {
        // 视觉大小
        public float VisualSizeScale = 1.0f;
        public float BorderWidthScale = 3.0f / 111.0f;
        public float CornerRadius = 12.0f;
        public float BgOpacity = 0.9f;
        public int FontSize = 0; // 0 = 自动

        // 颜色
        public Color BorderColor;
        public Color BgColor;
        public Color TextColor;

        // 4 行文字
        public string[] LabelTexts = new string[4] { "", "", "", "" };
        public int[] LabelFontSizes = new int[4] { 0, 0, 0, 0 };
        public float[] LabelXOffsets = new float[4] { 0, 0, 0, 0 };
        public bool[] LabelCenterX = new bool[4] { true, true, true, true };
        public float[] LabelYOffsets = new float[4] { 0, 0, 0, 0 };

        // 血条（只存 Scale，Length/Height 由 GridSize × Scale 计算）
        public bool HpBarVisible = true;
        public float HpBarLengthScale = 102.0f / 111.0f;
        public float HpBarHeightScale = 6.0f / 111.0f;
        public float HpBarFillPercent = 1.0f;
        public float HpBarOffsetX = 0;
        public float HpBarOffsetY = -70;
        public Color HpBarColor = new Color(0, 0.8f, 0, 1);

        // MP 条（同上）
        public bool MpBarVisible = true;
        public float MpBarLengthScale = 80.0f / 111.0f;
        public float MpBarHeightScale = 4.0f / 111.0f;
        public float MpBarFillPercent = 1.0f;
        public float MpBarOffsetX = 0;
        public float MpBarOffsetY = -62;
        public Color MpBarColor = new Color(0.2f, 0.4f, 1.0f, 1);

        // NPC 交互面板偏移（Monster 忽略）
        public float InteractMenuOffsetAX = 60f;
        public float InteractMenuOffsetAY = -20f;
        public float InteractMenuOffsetBX = -60f;
        public float InteractMenuOffsetBY = -20f;

        public int ComputeVisualSize(int gridSize) =>
            Mathf.Clamp((int)(gridSize * VisualSizeScale), 10, gridSize);

        public float ComputeBorderWidth(int gridSize) =>
            Mathf.Clamp(gridSize * BorderWidthScale, 1.0f, 20.0f);

        public EntityStyleConfig Clone()
        {
            var c = new EntityStyleConfig
            {
                VisualSizeScale = VisualSizeScale,
                BorderWidthScale = BorderWidthScale,
                CornerRadius = CornerRadius,
                BgOpacity = BgOpacity,
                FontSize = FontSize,
                BorderColor = BorderColor,
                BgColor = BgColor,
                TextColor = TextColor,
                InteractMenuOffsetAX = InteractMenuOffsetAX,
                InteractMenuOffsetAY = InteractMenuOffsetAY,
                InteractMenuOffsetBX = InteractMenuOffsetBX,
                InteractMenuOffsetBY = InteractMenuOffsetBY,
                HpBarVisible = HpBarVisible,
                HpBarLengthScale = HpBarLengthScale,
                HpBarHeightScale = HpBarHeightScale,
                HpBarFillPercent = HpBarFillPercent,
                HpBarOffsetX = HpBarOffsetX,
                HpBarOffsetY = HpBarOffsetY,
                HpBarColor = HpBarColor,
                MpBarVisible = MpBarVisible,
                MpBarLengthScale = MpBarLengthScale,
                MpBarHeightScale = MpBarHeightScale,
                MpBarFillPercent = MpBarFillPercent,
                MpBarOffsetX = MpBarOffsetX,
                MpBarOffsetY = MpBarOffsetY,
                MpBarColor = MpBarColor,
            };
            for (int i = 0; i < 4; i++)
            {
                c.LabelTexts[i] = LabelTexts[i];
                c.LabelFontSizes[i] = LabelFontSizes[i];
                c.LabelXOffsets[i] = LabelXOffsets[i];
                c.LabelCenterX[i] = LabelCenterX[i];
                c.LabelYOffsets[i] = LabelYOffsets[i];
            }
            return c;
        }

        /// <summary>怪物默认配置（红色系）</summary>
        public static EntityStyleConfig CreateMonsterDefault() => new EntityStyleConfig
        {
            BorderColor = new Color(0.9f, 0.3f, 0.3f),
            BgColor = new Color(0.8f, 0.2f, 0.2f),
            TextColor = new Color(1, 0.95f, 0.95f),
        };

        /// <summary>NPC 默认配置（蓝色系）</summary>
        public static EntityStyleConfig CreateNpcDefault() => new EntityStyleConfig
        {
            BorderColor = new Color(0.3f, 0.5f, 0.9f),
            BgColor = new Color(0.2f, 0.4f, 0.8f),
            TextColor = new Color(0.95f, 0.97f, 1.0f),
        };
    }
}
