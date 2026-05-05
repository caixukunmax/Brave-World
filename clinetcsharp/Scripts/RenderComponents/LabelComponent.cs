using Godot;

namespace ClinetCSharp.RenderComponents
{
    /// <summary>
    /// 文字标签渲染组件（DrawString 版，Monster/Npc 用）
    /// DrawOrder = 300
    /// </summary>
    public class LabelComponent : IRenderComponent
    {
        private EntityBase _entity = null!;

        public int DrawOrder => 300;

        public void OnAttach(EntityBase entity) => _entity = entity;
        public void OnDetach(EntityBase entity) => _entity = null!;

        public void Draw()
        {
            var font = ThemeDB.FallbackFont;
            int baseFs = _entity.FontSize > 0 ? _entity.FontSize : Mathf.Max((int)(_entity.VisualSize / 4.0f * 0.7f), 8);
            float baseLineHeight = baseFs * 1.1f;
            float totalHeight = baseLineHeight * 4;
            float startY = -(totalHeight / 2.0f) + baseLineHeight * 0.5f;

            for (int i = 0; i < 4; i++)
            {
                if (string.IsNullOrEmpty(_entity.LabelTexts[i])) continue;
                int fs = _entity.LabelFontSizes[i] > 0 ? _entity.LabelFontSizes[i] : baseFs;
                float posY = startY + i * baseLineHeight + _entity.LabelYOffsets[i];
                float posX = _entity.LabelCenterX[i] ? 0 : _entity.LabelXOffsets[i];

                var textSize = font.GetStringSize(_entity.LabelTexts[i], HorizontalAlignment.Left, -1, fs);
                float drawX = posX - textSize.X / 2f;
                float baselineY = posY + (font.GetAscent(fs) - font.GetDescent(fs)) * 0.5f;

                _entity.DrawString(font, new Vector2(drawX, baselineY), _entity.LabelTexts[i],
                    HorizontalAlignment.Left, -1, fs, _entity.TextColor);
            }
        }
    }
}
