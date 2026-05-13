using Godot;
using Protocol;

namespace ClinetCSharp
{
    public partial class Player
    {
        public override void PlayBounceBack(Vector2I originPos, float duration = -1f)
        {
            ClearPendingServerGridCorrection();
            StopMoveCheckpointTimer();
            _currentTween?.Kill();

            var originWorld = UiUtils.GridToWorld(originPos, GridSize);

            _bouncingBack = true;
            _currentTween = CreateTween();

            float d = duration < 0 ? BounceBackDuration : duration;

            // 计算当前已走的距离比例
            float distFromOrigin = Position.DistanceTo(originWorld);
            float ratio = distFromOrigin / Mathf.Max(1f, GridSize);

            if (ratio < BounceBackOvershootThreshold)
            {
                // 走得不多时：轻微 overshoot 然后弹回（更有"撞到东西"的感觉）
                var targetWorld = UiUtils.GridToWorld(_moveTargetPos, GridSize);
                var dir = targetWorld - originWorld;
                if (dir.Length() > 0.001f)
                {
                    dir = dir.Normalized();
                    var overshootWorld = Position + dir * GridSize * BounceBackOvershootRatio;

                    _currentTween.SetTrans(Tween.TransitionType.Sine);
                    _currentTween.SetEase(Tween.EaseType.Out);
                    _currentTween.TweenProperty(this, "position", overshootWorld, d * 0.35f);

                    _currentTween.SetTrans(Tween.TransitionType.Cubic);
                    _currentTween.SetEase(Tween.EaseType.In);
                    _currentTween.TweenProperty(this, "position", originWorld, d * 0.65f);
                }
                else
                {
                    _currentTween.SetTrans(Tween.TransitionType.Cubic);
                    _currentTween.SetEase(Tween.EaseType.In);
                    _currentTween.TweenProperty(this, "position", originWorld, d);
                }
            }
            else
            {
                // 走得较远时（如贴脸碰撞，已走约 50%）：直接快速弹回，不再前冲
                // 避免"走了很远还继续冲一段"的突兀感
                _currentTween.SetTrans(Tween.TransitionType.Cubic);
                _currentTween.SetEase(Tween.EaseType.In);
                _currentTween.TweenProperty(this, "position", originWorld, d);
            }

            _currentTween.Finished += () =>
            {
                IsMoving = false;
                _bouncingBack = false;
                Position = originWorld;
                _gridPos = originPos;
                _collisionMove = false;
            };
        }

        private void OnMoveCancelReceived(Game.MoveCancelNotify notify)
        {
            if (notify.EntityId != (ulong)GetInstanceId())
                return;

            GD.Print($"[Player] Server cancelled move, rollback to ({notify.RollbackX}, {notify.RollbackY})");
            _collisionMove = false;

            var rollbackPos = new Vector2I(notify.RollbackX, notify.RollbackY);
            if (_bouncingBack)
            {
                _gridPos = rollbackPos;
                return;
            }

            if (!IsMoving)
            {
                _gridPos = rollbackPos;
                Position = UiUtils.GridToWorld(rollbackPos, GridSize);
                return;
            }

            RollbackTo(rollbackPos);
        }

        private void PlayBumpAnimation(Vector2I fromPos, Vector2I targetPos)
        {
            StopMoveCheckpointTimer();
            _currentTween?.Kill();

            var fromWorld = UiUtils.GridToWorld(fromPos, GridSize);
            var toWorld = UiUtils.GridToWorld(targetPos, GridSize);
            var bumpPos = fromWorld + ((toWorld - fromWorld) * 0.3f);

            IsMoving = true;
            const float bumpDuration = 0.08f;
            const float returnDuration = 0.07f;

            _currentTween = CreateTween();
            _currentTween.SetTrans(Tween.TransitionType.Sine);
            _currentTween.SetEase(Tween.EaseType.Out);
            _currentTween.TweenProperty(this, "position", bumpPos, bumpDuration);
            _currentTween.SetTrans(Tween.TransitionType.Sine);
            _currentTween.SetEase(Tween.EaseType.In);
            _currentTween.TweenProperty(this, "position", fromWorld, returnDuration);
            _currentTween.Finished += () =>
            {
                IsMoving = false;
                Position = fromWorld;
                _gridPos = fromPos;
            };
        }

        public override void RollbackTo(Vector2I pos)
        {
            ClearPendingServerGridCorrection();
            _currentTween?.Kill();
            _currentTween = null;
            StopMoveCheckpointTimer();

            var targetWorld = UiUtils.GridToWorld(pos, GridSize);
            var dist = Position.DistanceTo(targetWorld);

            if (dist > 1.0f)
            {
                var duration = Mathf.Clamp(dist / 800f, 0.05f, 0.12f);
                _bouncingBack = true;
                _currentTween = CreateTween();
                _currentTween.SetTrans(Tween.TransitionType.Quad);
                _currentTween.SetEase(Tween.EaseType.In);
                _currentTween.TweenProperty(this, "position", targetWorld, duration);
                _currentTween.Finished += () =>
                {
                    IsMoving = false;
                    _bouncingBack = false;
                    _gridPos = pos;
                    Position = UiUtils.GridToWorld(pos, GridSize);
                    _currentTween = null;
                };
                IsMoving = true;
            }
            else
            {
                IsMoving = false;
                _bouncingBack = false;
                _gridPos = pos;
                Position = targetWorld;
            }

            _moveSentCount = 0;
            _collisionMove = false;
        }

        private void StopMoveCheckpointTimer()
        {
            _checkTimer?.Stop();
            _checkTimer?.QueueFree();
            _checkTimer = null;
        }
    }
}
