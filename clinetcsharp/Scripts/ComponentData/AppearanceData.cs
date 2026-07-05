using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 外观组件数据 — 大小/比例/边框/圆角/背景/颜色
    /// </summary>
    public class AppearanceData : IComponentData
    {
        public float VisualSizeScale = 1.0f;
        public float BorderWidthScale = 3.0f / 111.0f;
        public float CornerRadius = 12.0f;
        public float BgOpacity = 0.9f;
        public int FontSize = 0; // 0 = 自动
        public int SizeX = 1;    // 占地宽度（格子数）
        public int SizeY = 1;    // 占地高度（格子数）

        public Color BorderColor = Colors.White;
        public Color BgColor = Colors.White;
        public Color TextColor = Colors.Black;

        public IComponentData Clone()
        {
            return new AppearanceData
            {
                VisualSizeScale = VisualSizeScale,
                BorderWidthScale = BorderWidthScale,
                CornerRadius = CornerRadius,
                BgOpacity = BgOpacity,
                FontSize = FontSize,
                SizeX = SizeX,
                SizeY = SizeY,
                BorderColor = BorderColor,
                BgColor = BgColor,
                TextColor = TextColor,
            };
        }
    }
}
