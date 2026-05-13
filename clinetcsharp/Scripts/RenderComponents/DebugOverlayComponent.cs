using Godot;

namespace ClinetCSharp.RenderComponents
{
    /// <summary>
    /// Shared diagnostic overlay for player / monster / npc alignment checks.
    /// DrawOrder = 500 to stay above body, bars, and labels.
    /// </summary>
    public class DebugOverlayComponent : IRenderComponent
    {
        private EntityBase _entity = null!;

        public int DrawOrder => 500;

        public void OnAttach(EntityBase entity) => _entity = entity;
        public void OnDetach(EntityBase entity) => _entity = null!;

        public void Draw()
        {
            if (!EntityBase.GlobalDebugOverlayVisible)
                return;

            float gridHalf = _entity.GridSize / 2.0f;
            var gridRect = new Rect2(new Vector2(-gridHalf, -gridHalf), new Vector2(_entity.GridSize, _entity.GridSize));
            _entity.DrawRect(gridRect, new Color(1f, 0f, 0f, 0.45f), false, 1.0f);

            float outerHalf = _entity.VisualOuterSize / 2.0f;
            var outerRect = new Rect2(new Vector2(-outerHalf, -outerHalf), new Vector2(_entity.VisualOuterSize, _entity.VisualOuterSize));
            _entity.DrawRect(outerRect, new Color(1f, 0.85f, 0.1f, 0.85f), false, 1.2f);

            float innerHalf = _entity.VisualSize / 2.0f;
            var innerRect = new Rect2(new Vector2(-innerHalf, -innerHalf), new Vector2(_entity.VisualSize, _entity.VisualSize));
            _entity.DrawRect(innerRect, new Color(0f, 1f, 0.25f, 0.75f), false, 1.2f);

            float crossSize = 8.0f;
            var crossColor = new Color(0.1f, 0.9f, 1.0f, 0.9f);
            _entity.DrawLine(new Vector2(-crossSize, 0), new Vector2(crossSize, 0), crossColor, 1.0f);
            _entity.DrawLine(new Vector2(0, -crossSize), new Vector2(0, crossSize), crossColor, 1.0f);
        }
    }
}
