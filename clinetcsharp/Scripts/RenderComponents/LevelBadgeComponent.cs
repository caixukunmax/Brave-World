using Godot;

namespace ClinetCSharp.RenderComponents
{
    /// <summary>
    /// 等级徽章渲染组件（Player 专用）
    /// DrawOrder = 400
    /// </summary>
    public class LevelBadgeComponent : IRenderComponent
    {
        private Player _player = null!;

        public int DrawOrder => 400;

        public void OnAttach(EntityBase entity)
        {
            _player = entity as Player;
        }

        public void OnDetach(EntityBase entity) => _player = null!;

        public void Draw()
        {
            if (_player == null || !_player.LevelBadgeVisible) return;

            var levelText = _player.LevelBadgeText
                .Replace("{level}", _player.Level.ToString())
                .Replace("{name}", _player.CharacterName)
                .Replace("{job}", _player.Job)
                .Replace("{title}", _player.Title)
                .Replace("{status}", _player.Status);

            var font = ThemeDB.FallbackFont;
            int fontSize = Mathf.Max((int)_player.LevelBadgeFontSize, 6);
            var textSize = font.GetStringSize(levelText, HorizontalAlignment.Center, -1, fontSize);
            var textPos = _player.LevelBadgeOffset - textSize / 2.0f + new Vector2(0, fontSize * 0.15f);
            _player.DrawString(font, textPos, levelText, HorizontalAlignment.Center, -1, fontSize, _player.LevelBadgeTextColor);
        }
    }
}
