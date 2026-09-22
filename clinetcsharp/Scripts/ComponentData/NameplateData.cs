using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 铭牌背景组件数据 —— 为实体标签提供三层填充色块背景板。
    /// 顶栏与底栏共用高度/颜色，中块宽度按实体视觉外框比例居中缩放。
    /// 不渲染文字，文字仍由 labels 组件负责。
    /// </summary>
    public class NameplateData : IComponentData
    {
        public bool Visible = false;
        public float YOffset = -80f;
        public float Spacing = 4f;

        public float BarHeight = 6f;
        public Color BarColor = new Color(0.1f, 0.1f, 0.1f, 0.7f);

        public float CenterBoxHeight = 24f;
        public float CenterBoxWidthScale = 0.6f;
        public Color CenterBoxColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);

        public IComponentData Clone()
        {
            return new NameplateData
            {
                Visible = Visible,
                YOffset = YOffset,
                Spacing = Spacing,
                BarHeight = BarHeight,
                BarColor = BarColor,
                CenterBoxHeight = CenterBoxHeight,
                CenterBoxWidthScale = CenterBoxWidthScale,
                CenterBoxColor = CenterBoxColor,
            };
        }
    }
}
