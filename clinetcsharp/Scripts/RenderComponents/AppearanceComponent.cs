using Godot;

namespace ClinetCSharp.RenderComponents
{
    /// <summary>
    /// 外观渲染组件 — DrawBody（方块 + 边框 + 背景）
    /// DrawOrder = 0（最底层）
    /// </summary>
    public class AppearanceComponent : IRenderComponent
    {
        private EntityBase _entity = null!;

        public int DrawOrder => 0;

        public void OnAttach(EntityBase entity) => _entity = entity;
        public void OnDetach(EntityBase entity) => _entity = null!;

        public void Draw()
        {
            // 多格建筑使用矩形尺寸按 footprint 填充；1x1 实体 X/Y 相等，退化为旧正方形逻辑。
            EntityDrawUtils.DrawBody(_entity, _entity.VisualOuterSizeX, _entity.VisualOuterSizeY,
                _entity.VisualSizeX, _entity.VisualSizeY, _entity.BgColor, _entity.BgOpacity,
                _entity.BorderColor, _entity.BorderWidth, _entity.CornerRadius);
        }
    }
}
