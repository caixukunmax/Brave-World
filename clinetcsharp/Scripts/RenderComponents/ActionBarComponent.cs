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
            // 玩家：第一个状态由“状态标签 + 施法条”显示，动作栏留给第二个及以后的状态
            if (_entity is Player && !string.IsNullOrWhiteSpace(_entity.CastingSkill))
                return;

            string skillName = _entity.CastingSkill;
            float castProgress = _entity.CastProgress;

            if (_entity.ActionBarForceShow && string.IsNullOrWhiteSpace(skillName))
            {
                skillName = "动作栏预览";
                castProgress = 0.6f;
            }

            EntityDrawUtils.DrawActionBar(_entity, _entity.VisualSize,
                skillName, castProgress,
                _entity.ActionBarTextYOffset, _entity.ActionBarProgressHeight);
        }
    }
}
