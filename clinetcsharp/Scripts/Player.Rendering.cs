using Godot;

namespace ClinetCSharp
{
    public partial class Player
    {
        public override void _Draw()
        {
            foreach (var comp in _renderComponents)
                comp.Draw();
        }

        /// <summary>确保渲染组件已添加（幂等，只添加一次）</summary>
        private void EnsureRenderComponents()
        {
            if (_renderComponents.Count > 0)
                return;

            AddRenderComponent(new RenderComponents.AppearanceComponent());
            AddRenderComponent(new RenderComponents.HealthBarComponent());
            AddRenderComponent(new RenderComponents.MpBarComponent());
            AddRenderComponent(new RenderComponents.CastBarComponent());
            AddRenderComponent(new RenderComponents.ActionBarComponent());
            AddRenderComponent(new RenderComponents.RichLabelComponent());
            AddRenderComponent(new RenderComponents.LevelBadgeComponent());
            AddRenderComponent(new RenderComponents.DebugOverlayComponent());
        }
    }
}
