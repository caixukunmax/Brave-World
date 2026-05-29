using Godot;

namespace ClinetCSharp.RenderComponents
{
    /// <summary>
    /// 战斗光环渲染组件 — 角色进入战斗时脚下显示红色光晕
    /// DrawOrder = -10（比外观更底层，确保光环在角色下方）
    /// </summary>
    public class CombatAuraComponent : IRenderComponent
    {
        private EntityBase _entity = null!;

        public int DrawOrder => -10;

        public void OnAttach(EntityBase entity) => _entity = entity;
        public void OnDetach(EntityBase entity) => _entity = null!;

        public void Draw()
        {
            if (!_entity.IsInCombat) return;

            float radius = _entity.VisualOuterSize * 0.55f;
            var color = new Color(1.0f, 0.15f, 0.15f, 0.35f);

            // 外圈光晕
            _entity.DrawCircle(Vector2.Zero, radius, color);
            // 内圈更亮的芯
            _entity.DrawCircle(Vector2.Zero, radius * 0.6f, new Color(1.0f, 0.3f, 0.2f, 0.25f));
        }
    }
}
