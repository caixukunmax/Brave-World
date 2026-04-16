using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 飘字效果：屏幕空间文字向上飘起并淡出
    /// </summary>
    public partial class FloatingText : Label
    {
        private double _lifetime = 0;
        private double _duration = 1.5;
        private Color _baseColor = Colors.Yellow;
        private float _speed = 30f;

        public void Setup(string text, Color color, int fontSize = 16, double duration = 1.5)
        {
            Text = text;
            _baseColor = color;
            _duration = duration;
            AddThemeFontSizeOverride("font_size", fontSize);
            AddThemeColorOverride("font_color", color);
            HorizontalAlignment = HorizontalAlignment.Center;
        }

        public override void _Process(double delta)
        {
            _lifetime += delta;
            Position += new Vector2(0, (float)(-_speed * delta));

            float alpha = 1.0f - (float)(_lifetime / _duration);
            if (alpha <= 0)
            {
                QueueFree();
                return;
            }
            var c = _baseColor;
            c.A = alpha;
            AddThemeColorOverride("font_color", c);
        }
    }
}
