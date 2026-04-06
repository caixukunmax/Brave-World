using Godot;

namespace ClinetCSharp
{
    [GlobalClass]
    public partial class ItemDropAnimation : Node2D
    {
        [Export] public float DropHeight { get; set; } = 200.0f;
        [Export] public float BounceHeightRatio { get; set; } = 0.5f;
        [Export] public int BounceCount { get; set; } = 3;
        [Export] public float DropDuration { get; set; } = 0.5f;
        [Export] public float BounceDurationRatio { get; set; } = 0.7f;
        [Export] public float ArcOffset { get; set; } = 50.0f;

        private Vector2 _targetPosition = Vector2.Zero;
        private Vector2 _startPosition = Vector2.Zero;

        [Signal]
        public delegate void AnimationFinishedEventHandler();

        public void StartDrop(Vector2 fromPos, Vector2 toPos)
        {
            _startPosition = fromPos;
            _targetPosition = toPos;
            GlobalPosition = fromPos;
            CreateDropTween();
        }

        private void CreateDropTween()
        {
            var tween = CreateTween();
            tween.SetTrans(Tween.TransitionType.Quad);
            tween.SetEase(Tween.EaseType.In);

            var controlPos = new Vector2(
                Mathf.Lerp(_startPosition.X, _targetPosition.X, 0.5f) + ArcOffset,
                Mathf.Min(_startPosition.Y, _targetPosition.Y) - DropHeight
            );

            tween.TweenMethod(Callable.From<float>(progress =>
            {
                GlobalPosition = QuadraticBezier(_startPosition, controlPos, _targetPosition, progress);
                Rotation = progress * Mathf.Pi * 2;
            }), 0.0f, 1.0f, DropDuration);

            var currentBounceHeight = DropHeight * BounceHeightRatio;
            var currentDuration = DropDuration * BounceDurationRatio;
            var bouncePos = _targetPosition;

            for (int i = 0; i < BounceCount; i++)
            {
                if (currentBounceHeight < 5)
                    break;

                var upPos = bouncePos + new Vector2(0, -currentBounceHeight);
                tween.Chain().TweenProperty(this, "global_position", upPos, currentDuration / 2);
                tween.Parallel().TweenProperty(this, "rotation", Rotation - Mathf.Pi, currentDuration / 2);
                tween.Chain().TweenProperty(this, "global_position", bouncePos, currentDuration / 2);
                tween.Parallel().TweenProperty(this, "rotation", Rotation + Mathf.Pi, currentDuration / 2);

                currentBounceHeight *= BounceHeightRatio;
                currentDuration *= BounceDurationRatio;
            }

            tween.Finished += () =>
            {
                Rotation = 0;
                EmitSignal(SignalName.AnimationFinished);
                GD.Print("[ItemDrop] Drop animation completed");
            };
        }

        private Vector2 QuadraticBezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            float u = 1.0f - t;
            return u * u * p0 + 2.0f * u * t * p1 + t * t * p2;
        }

    }
}
