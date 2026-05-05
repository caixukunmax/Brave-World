using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 等级徽章组件数据
    /// </summary>
    public class LevelBadgeData : IComponentData
    {
        public bool Visible = true;
        public float FontSize = 12f;
        public Color TextColor = Colors.Yellow;
        public string Text = "Lv.{level}";
        public float OffsetX = -35f;
        public float OffsetY = -35f;
        public bool CenterX = false;

        public IComponentData Clone()
        {
            return new LevelBadgeData
            {
                Visible = Visible,
                FontSize = FontSize,
                TextColor = TextColor,
                Text = Text,
                OffsetX = OffsetX,
                OffsetY = OffsetY,
                CenterX = CenterX,
            };
        }
    }
}
