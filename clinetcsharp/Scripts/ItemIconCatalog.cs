using Godot;

namespace ClinetCSharp
{
    public static class ItemIconCatalog
    {
        private static readonly Color CommonColor = new(1f, 1f, 1f);       // 白
        private static readonly Color UncommonColor = new(0.1f, 1f, 0.1f); // 绿
        private static readonly Color RareColor = new(0.1f, 0.5f, 1f);     // 蓝
        private static readonly Color EpicColor = new(0.8f, 0.2f, 1f);     // 紫
        private static readonly Color LegendaryColor = new(1f, 0.5f, 0f);  // 橙

        public static Color GetQualityColor(int quality)
        {
            return quality switch
            {
                0 => CommonColor,
                1 => UncommonColor,
                2 => RareColor,
                3 => EpicColor,
                >= 4 => LegendaryColor,
                _ => CommonColor,
            };
        }
    }
}
