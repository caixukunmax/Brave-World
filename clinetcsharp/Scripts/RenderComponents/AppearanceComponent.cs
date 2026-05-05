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
            var drawSize = _entity.VisualSize;
            if (drawSize < 10) drawSize = 10;
            EntityDrawUtils.DrawBody(_entity, drawSize, _entity.BgColor, _entity.BgOpacity,
                _entity.BorderColor, _entity.BorderWidth, _entity.CornerRadius);
        }
    }
}
