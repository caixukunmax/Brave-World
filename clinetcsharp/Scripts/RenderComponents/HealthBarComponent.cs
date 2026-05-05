using Godot;

namespace ClinetCSharp.RenderComponents
{
    /// <summary>
    /// 血条渲染组件
    /// DrawOrder = 100
    /// </summary>
    public class HealthBarComponent : IRenderComponent
    {
        private EntityBase _entity = null!;

        public int DrawOrder => 100;

        public void OnAttach(EntityBase entity) => _entity = entity;
        public void OnDetach(EntityBase entity) => _entity = null!;

        public void Draw()
        {
            EntityDrawUtils.DrawHealthBar(_entity, _entity.HealthBarOffset, _entity.HealthBarLength,
                _entity.HealthBarHeight, _entity.HealthBarFillPercent,
                _entity.HealthBarBgColor, _entity.HealthBarColor, _entity.HealthBarVisible);
        }
    }
}
