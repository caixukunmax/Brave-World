using Godot;

namespace ClinetCSharp.RenderComponents
{
    /// <summary>
    /// MP条渲染组件
    /// DrawOrder = 100
    /// </summary>
    public class MpBarComponent : IRenderComponent
    {
        private EntityBase _entity = null!;

        public int DrawOrder => 100;

        public void OnAttach(EntityBase entity) => _entity = entity;
        public void OnDetach(EntityBase entity) => _entity = null!;

        public void Draw()
        {
            EntityDrawUtils.DrawHealthBar(_entity, _entity.MpBarOffset, _entity.MpBarLength,
                _entity.MpBarHeight, _entity.MpBarFillPercent,
                _entity.MpBarBgColor, _entity.MpBarColor, _entity.MpBarVisible);
        }
    }
}
