using UnityEngine;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// 玩家回滚/弹回/顿挫 — 移植自 Godot Player.Movement.Recovery.cs。
    /// </summary>
    public partial class PlayerEntity
    {
        /// <summary>
        /// 回滚到指定格：清矫正/停检查点/杀 tween；偏差 >1px 时 Quad/In 滑回
        /// （时长 clamp(dist/800, 0.05, 0.12)），否则吸附。无论如何清零在途计数与碰撞标记。
        /// </summary>
        public override void RollbackTo(Vector2Int gridPos)
        {
            ClearPendingServerGridCorrection();
            StopMoveCheckpoint();
            KillMoveTween();

            var originWorld = PositionForGridPos(gridPos);
            float dist = Vector3.Distance(transform.position, originWorld);
            _moveSentCount = 0;
            _collisionMove = false;

            if (dist <= 1.0f)
            {
                transform.position = originWorld;
                IsMoving = false;
                _bouncingBack = false;
                GridPos = gridPos;
                return;
            }

            _bouncingBack = true;
            float duration = Mathf.Clamp(dist / 800f, 0.05f, 0.12f);
            StartMoveTween(originWorld, duration, SimpleTween.Ease.QuadIn, () =>
            {
                IsMoving = false;
                _bouncingBack = false;
                GridPos = gridPos;
            });
        }

        /// <summary>弹回（overshoot 版）：先清矫正/停检查点，其余走基类逻辑。</summary>
        public override void PlayBounceBack(Vector2Int originPos, Vector2Int? overshootTarget = null, float durationSec = -1f)
        {
            ClearPendingServerGridCorrection();
            StopMoveCheckpoint();
            _bouncingBack = true;
            base.PlayBounceBack(originPos, overshootTarget ?? _moveTargetPos, durationSec);
        }

        protected override void FinishBounceBack(Vector2Int originPos)
        {
            _bouncingBack = false;
            _collisionMove = false;
            base.FinishBounceBack(originPos);
        }

        /// <summary>撞怪顿挫（attack bump）：停检查点后走基类 bump 动画。</summary>
        public override void PlayBumpAnimation(Vector2Int fromPos, Vector2Int toPos)
        {
            StopMoveCheckpoint();
            _collisionMove = false;
            base.PlayBumpAnimation(fromPos, toPos);
        }
    }
}
