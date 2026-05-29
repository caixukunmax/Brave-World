using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 模板预览专用实体 — 只做渲染，不参与游戏逻辑、网络同步或 AI
    /// </summary>
    public partial class EntityPreview : EntityBase
    {
        public override void _Ready()
        {
            EnsureRenderComponents();
            QueueRedraw();
        }

        public override void _Process(double delta) { }
        public override void _Input(InputEvent @event) { }

        public override void _Draw()
        {
            foreach (var comp in _renderComponents)
                comp.Draw();
        }

        private void EnsureRenderComponents()
        {
            if (_renderComponents.Count > 0) return;

            AddRenderComponent(new RenderComponents.CombatAuraComponent());
            AddRenderComponent(new RenderComponents.AppearanceComponent());
            AddRenderComponent(new RenderComponents.HealthBarComponent());
            AddRenderComponent(new RenderComponents.MpBarComponent());
            AddRenderComponent(new RenderComponents.CastBarComponent());
            AddRenderComponent(new RenderComponents.ActionBarComponent());
            AddRenderComponent(new RenderComponents.RichLabelComponent());
            AddRenderComponent(new RenderComponents.LevelBadgeComponent());
        }
    }
}
