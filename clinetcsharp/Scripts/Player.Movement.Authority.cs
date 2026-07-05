using Godot;

namespace ClinetCSharp
{
    public partial class Player
    {
        private void QueueServerGridCorrection(Vector2I serverGridPos)
        {
            _pendingServerGridPos = serverGridPos;
            TryStartPendingServerGridCorrection();
        }

        private void TryStartPendingServerGridCorrection()
        {
            if (!_pendingServerGridPos.HasValue)
                return;

            if (PlayerRoleInfoPositionSyncPolicy.ShouldDeferServerGrid(
                IsMoving,
                _moveSentCount > 0,
                _bouncingBack))
            {
                return;
            }

            if (IsServerGridCorrectionActive())
                return;

            var serverGridPos = _pendingServerGridPos.Value;
            _pendingServerGridPos = null;

            _gridPos = serverGridPos;
            _moveFromPos = serverGridPos;

            var targetWorldPos = GetWorldPositionForGridPos(serverGridPos);
            float distance = Position.DistanceTo(targetWorldPos);
            float duration = PlayerRoleInfoPositionSyncPolicy.ResolveServerGridCorrectionDuration(distance, GridSize);

            if (duration <= 0f)
            {
                Position = targetWorldPos;
                return;
            }

            _serverGridCorrectionTween?.Kill();
            _serverGridCorrectionTween = CreateTween();
            _serverGridCorrectionTween.SetTrans(Tween.TransitionType.Sine);
            _serverGridCorrectionTween.SetEase(Tween.EaseType.Out);
            _serverGridCorrectionTween.TweenProperty(this, "position", targetWorldPos, duration);
            _serverGridCorrectionTween.Finished += () =>
            {
                Position = targetWorldPos;
                _serverGridCorrectionTween = null;
                TryStartPendingServerGridCorrection();
            };
        }

        private bool IsServerGridCorrectionActive()
        {
            return _serverGridCorrectionTween != null
                && GodotObject.IsInstanceValid(_serverGridCorrectionTween)
                && _serverGridCorrectionTween.IsRunning();
        }

        private void ClearPendingServerGridCorrection()
        {
            _pendingServerGridPos = null;
            _serverGridCorrectionTween?.Kill();
            _serverGridCorrectionTween = null;
        }
    }
}