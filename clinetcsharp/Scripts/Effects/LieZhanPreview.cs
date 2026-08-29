using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 烈斩特效预览场景控制器：搭一个极简网格背景 + 施法者/木桩，按空格或左键
    /// （进入场景 0.6s 后也会自动演示一次）在两者之间播放 MeleeSlashEffect，
    /// 并接住特效的震屏信号。纯用于快速调手感，不接真实战斗。
    /// </summary>
    public partial class LieZhanPreview : Node2D
    {
        private readonly Vector2 _caster = new Vector2(-80, 0);
        private readonly Vector2 _target = new Vector2(80, 0);
        private const float Tile = 40f;

        private Camera2D _camera = null!;

        public override void _Ready()
        {
            _camera = new Camera2D { Position = Vector2.Zero };
            AddChild(_camera);
            _camera.MakeCurrent();

            // 背景色
            var bg = new ColorRect
            {
                Color = new Color(0.09f, 0.10f, 0.12f),
                OffsetLeft = -2000,
                OffsetTop = -2000,
                OffsetRight = 2000,
                OffsetBottom = 2000,
                ZIndex = -10,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            AddChild(bg);

            QueueRedraw();

            // 自动演示一次
            GetTree().CreateTimer(0.6).Timeout += PlayLieZhan;
        }

        public override void _Draw()
        {
            // 网格线
            var lineCol = new Color(1, 1, 1, 0.06f);
            for (int i = -12; i <= 12; i++)
            {
                float x = i * Tile;
                DrawLine(new Vector2(x, -12 * Tile), new Vector2(x, 12 * Tile), lineCol, 1f);
                float y = i * Tile;
                DrawLine(new Vector2(-12 * Tile, y), new Vector2(12 * Tile, y), lineCol, 1f);
            }

            // 施法者（蓝）、木桩（红）
            DrawRect(new Rect2(_caster - new Vector2(Tile / 2, Tile / 2), new Vector2(Tile, Tile)),
                new Color(0.35f, 0.6f, 1f, 0.9f), true);
            DrawRect(new Rect2(_target - new Vector2(Tile / 2, Tile / 2), new Vector2(Tile, Tile)),
                new Color(1f, 0.4f, 0.35f, 0.9f), true);

            // 提示
            var font = ThemeDB.FallbackFont;
            DrawString(font, new Vector2(-12 * Tile + 8, -12 * Tile + 22),
                "烈斩预览：按 [空格] 或 [左键] 释放", HorizontalAlignment.Left, 0f, 16, new Color(1, 1, 1, 0.8f));
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey k && k.Pressed && k.Keycode == Key.Space)
                PlayLieZhan();
            else if (@event is InputEventMouseButton mb && mb.Pressed &&
                     mb.ButtonIndex == MouseButton.Left)
                PlayLieZhan();
        }

        private void PlayLieZhan()
        {
            var effect = new MeleeSlashEffect();
            AddChild(effect);
            effect.Position = _caster;
            effect.ScreenShake += OnScreenShake;

            Vector2 delta = _target - _caster;
            effect.Start(delta.Angle(), delta.Length());
        }

        private void OnScreenShake(string kind)
        {
            // 阻尼抖动相机 offset
            var tw = CreateTween();
            var amps = new[] { 10f, 6f, 4f, 2f, 0f };
            foreach (float a in amps)
            {
                var off = new Vector2(
                    (float)GD.RandRange(-a, a),
                    (float)GD.RandRange(-a, a));
                tw.TweenProperty(_camera, "offset", off, 0.03f);
            }
            tw.TweenProperty(_camera, "offset", Vector2.Zero, 0.05f);
        }
    }
}
