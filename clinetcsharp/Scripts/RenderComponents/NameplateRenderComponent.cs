using Godot;

namespace ClinetCSharp.RenderComponents
{
    /// <summary>
    /// 铭牌背景渲染组件 —— 绘制三层填充色块（顶栏/中块/底栏）。
    /// DrawOrder = 290，位于血条/施法条（200）之后、文字标签（300）之前，作为文字背景板。
    /// </summary>
    public class NameplateRenderComponent : IRenderComponent
    {
        private EntityBase _entity = null!;

        public int DrawOrder => 290;

        public void OnAttach(EntityBase entity) => _entity = entity;
        public void OnDetach(EntityBase entity) => _entity = null!;

        public void Draw()
        {
            if (!_entity.NameplateVisible)
                return;

            // 使用内框宽度（外框减去左右边框），让铭牌顶/底栏内缩到实体边框内侧，
            // 不再与实体边框同宽而压住边框。
            float innerWidth = _entity.GridSizeX > 1 ? _entity.VisualSizeX : _entity.VisualSize;
            float halfInnerWidth = innerWidth / 2.0f;

            float barHeight = _entity.NameplateBarHeight;
            float centerHeight = _entity.NameplateCenterBoxHeight;
            float spacing = _entity.NameplateSpacing;
            float totalHeight = barHeight + spacing + centerHeight + spacing + barHeight;
            float startY = _entity.NameplateYOffset - totalHeight / 2.0f;

            // 顶栏
            float topY = startY;
            _entity.DrawRect(
                new Rect2(-halfInnerWidth, topY, innerWidth, barHeight),
                _entity.NameplateBarColor,
                filled: true);

            // 中块（居中，宽度按实体内框比例缩放，上限不超过内框宽度）
            float centerWidth = Mathf.Clamp(innerWidth * _entity.NameplateCenterBoxWidthScale, 1f, innerWidth);
            float centerY = startY + barHeight + spacing;
            _entity.DrawRect(
                new Rect2(-centerWidth / 2.0f, centerY, centerWidth, centerHeight),
                _entity.NameplateCenterBoxColor,
                filled: true);

            // 底栏
            float bottomY = centerY + centerHeight + spacing;
            _entity.DrawRect(
                new Rect2(-halfInnerWidth, bottomY, innerWidth, barHeight),
                _entity.NameplateBarColor,
                filled: true);
        }
    }
}
