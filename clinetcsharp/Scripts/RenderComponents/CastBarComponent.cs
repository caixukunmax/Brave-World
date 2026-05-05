using Godot;

namespace ClinetCSharp.RenderComponents
{
    /// <summary>
    /// 施法条渲染组件
    /// DrawOrder = 200
    /// </summary>
    public class CastBarComponent : IRenderComponent
    {
        private EntityBase _entity = null!;

        public int DrawOrder => 200;

        public void OnAttach(EntityBase entity) => _entity = entity;
        public void OnDetach(EntityBase entity) => _entity = null!;

        public void Draw()
        {
            if (!_entity.CastBarVisible) return;
            float cHalfLen = _entity.CastBarLength / 2.0f;
            float cHalfH = _entity.CastBarHeight / 2.0f;
            var cBgRect = new Rect2(
                _entity.CastBarOffset.X - cHalfLen,
                _entity.CastBarOffset.Y - cHalfH,
                _entity.CastBarLength,
                _entity.CastBarHeight);
            _entity.DrawRect(cBgRect, _entity.CastBarBgColor, true);

            float cFillWidth = _entity.CastBarLength * Mathf.Clamp(_entity.CastBarFillPercent, 0, 1);
            if (cFillWidth > 0)
            {
                var cFillRect = new Rect2(
                    _entity.CastBarOffset.X - cHalfLen,
                    _entity.CastBarOffset.Y - cHalfH,
                    cFillWidth,
                    _entity.CastBarHeight);
                _entity.DrawRect(cFillRect, _entity.CastBarColor, true);
            }
        }
    }
}
