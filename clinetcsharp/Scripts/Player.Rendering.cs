using Godot;

namespace ClinetCSharp
{
    public partial class Player
    {
        public override void _Draw()
        {
            foreach (var comp in _renderComponents)
                comp.Draw();
            EntityDrawUtils.DrawDirectionArrow(this);
        }

        /// <summary>确保渲染组件已添加（按类型幂等补齐；禁止 Count>0 早退——
        /// ApplyProfileToEntity 会先行加入 castbar/actionbar/nameplate 组件，
        /// 早退会导致 Appearance/血条/标签等基础组件丢失，见 AGENTS.md）</summary>
        protected void EnsureRenderComponents()
        {
            if (GetRenderComponent<RenderComponents.CombatAuraComponent>() == null)
                AddRenderComponent(new RenderComponents.CombatAuraComponent());
            if (GetRenderComponent<RenderComponents.AppearanceComponent>() == null)
                AddRenderComponent(new RenderComponents.AppearanceComponent());
            if (GetRenderComponent<RenderComponents.HealthBarComponent>() == null)
                AddRenderComponent(new RenderComponents.HealthBarComponent());
            if (GetRenderComponent<RenderComponents.MpBarComponent>() == null)
                AddRenderComponent(new RenderComponents.MpBarComponent());
            if (GetRenderComponent<RenderComponents.CastBarComponent>() == null)
                AddRenderComponent(new RenderComponents.CastBarComponent());
            if (GetRenderComponent<RenderComponents.ActionBarComponent>() == null)
                AddRenderComponent(new RenderComponents.ActionBarComponent());
            if (GetRenderComponent<RenderComponents.RichLabelComponent>() == null)
                AddRenderComponent(new RenderComponents.RichLabelComponent());
            if (GetRenderComponent<RenderComponents.LevelBadgeComponent>() == null)
                AddRenderComponent(new RenderComponents.LevelBadgeComponent());
            if (GetRenderComponent<RenderComponents.DebugOverlayComponent>() == null)
                AddRenderComponent(new RenderComponents.DebugOverlayComponent());
        }
    }
}
