using Godot;

namespace ClinetCSharp.RenderComponents
{
    /// <summary>
    /// 动作栏渲染组件（技能名 + 施法进度条）
    /// DrawOrder = 200
    /// </summary>
    public class ActionBarComponent : IRenderComponent
    {
        private EntityBase _entity = null!;

        public int DrawOrder => 200;

        public void OnAttach(EntityBase entity) => _entity = entity;
        public void OnDetach(EntityBase entity) => _entity = null!;

        public void Draw()
        {
            EntityDrawUtils.DrawActionBar(_entity, _entity.VisualSize,
                _entity.CastingSkill, _entity.CastProgress,
                _entity.ActionBarTextYOffset, _entity.ActionBarProgressHeight);
        }
    }
}
