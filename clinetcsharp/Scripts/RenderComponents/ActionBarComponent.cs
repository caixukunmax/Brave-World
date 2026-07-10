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
            // 施法时由“状态标签 + 施法条”显示技能名和进度，动作栏不再重复绘制
            if (!string.IsNullOrWhiteSpace(_entity.CastingSkill))
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
