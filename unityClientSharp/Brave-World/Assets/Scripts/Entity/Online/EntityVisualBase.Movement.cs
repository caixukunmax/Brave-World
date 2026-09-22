using System.Collections;
using UnityEngine;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// 实体移动/回弹/顿挫动画 — 移植自 Godot EntityBase 的移动 tween 部分。
    /// 铁律②：所有改 Position 的动画共用一个 handle（<see cref="_moveRoutine"/>），新动画必须先杀旧。
    /// </summary>
    public partial class EntityVisualBase
    {
        // 回弹常量（对齐 Godot EntityBase.cs:15-17）
        public const float BounceBackDuration = 0.06f;
        public const float BounceBackOvershootRatio = 0.08f;
        public const float BounceBackOvershootThreshold = 0.40f;

        public bool IsMoving { get; protected set; }

        protected Coroutine _moveRoutine;

        /// <summary>杀掉当前移动/回弹/矫正动画（对齐 _currentTween.Kill 语义）。</summary>
        protected void KillMoveTween()
        {
            if (_moveRoutine != null)
            {
                StopCoroutine(_moveRoutine);
                _moveRoutine = null;
            }
        }

        protected void StartMoveTween(Vector3 targetWorld, float duration, SimpleTween.Ease ease, System.Action onComplete)
        {
            KillMoveTween();
            if (duration <= 0f)
            {
                transform.position = targetWorld;
                onComplete?.Invoke();
                return;
            }
            IsMoving = true;
            _moveRoutine = StartCoroutine(MoveTweenRoutine(targetWorld, duration, ease, onComplete));
        }

        private IEnumerator MoveTweenRoutine(Vector3 targetWorld, float duration, SimpleTween.Ease ease, System.Action onComplete)
        {
            Vector3 from = transform.position;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = SimpleTween.Evaluate(ease, t / duration);
                transform.position = Vector3.LerpUnclamped(from, targetWorld, k);
                yield return null;
            }
            transform.position = targetWorld; // 精确落位
            _moveRoutine = null;
            onComplete?.Invoke();
        }

        /// <summary>普通插值移动（Quad/Out，杀旧 tween，落定推进逻辑格）。</summary>
        public virtual void MoveTo(Vector2Int gridPos, float durationSec)
        {
            var targetWorld = PositionForGridPos(gridPos);
            StartMoveTween(targetWorld, durationSec, SimpleTween.Ease.QuadOut, () =>
            {
                IsMoving = false;
                GridPos = gridPos;
                OnMovementSettled(gridPos);
            });
        }

        /// <summary>移动落定回调（怪物清预约、玩家补发 327 等）。</summary>
        protected virtual void OnMovementSettled(Vector2Int gridPos) { }

        /// <summary>
        /// 回滚到指定格（Quad/In，时长 clamp(dist/800, 0.05, 0.12)；偏差 ≤1px 直接吸附）。
        /// 移植自 Godot EntityBase.RollbackTo。
        /// </summary>
        public virtual void RollbackTo(Vector2Int gridPos)
        {
            KillMoveTween();
            var originWorld = PositionForGridPos(gridPos);
            float dist = Vector3.Distance(transform.position, originWorld);
            if (dist <= 1.0f)
            {
                transform.position = originWorld;
                IsMoving = false;
                GridPos = gridPos;
                OnMovementSettled(gridPos);
                return;
            }
            float duration = Mathf.Clamp(dist / 800f, 0.05f, 0.12f);
            StartMoveTween(originWorld, duration, SimpleTween.Ease.QuadIn, () =>
            {
                IsMoving = false;
                GridPos = gridPos;
                OnMovementSettled(gridPos);
            });
        }

        /// <summary>
        /// 弹回动画（overshoot 版）：已走 &lt;0.40 格先沿目标方向前冲 8% 格（0.35d Sine/Out）再弹回（0.65d Cubic/In）；
        /// ≥0.40 或方向退化时单段直回（d Cubic/In）。移植自 Godot EntityBase.PlayBounceBack。
        /// </summary>
        public virtual void PlayBounceBack(Vector2Int originPos, Vector2Int? overshootTarget = null, float durationSec = -1f)
        {
            KillMoveTween();
            float d = durationSec >= 0f ? durationSec : BounceBackDuration;
            var originWorld = PositionForGridPos(originPos);
            float ratio = Vector3.Distance(transform.position, originWorld) / Mathf.Max(1, _gridSize);

            Vector3? frontWorld = null;
            if (ratio < BounceBackOvershootThreshold && overshootTarget.HasValue)
            {
                var dir = (PositionForGridPos(overshootTarget.Value) - transform.position);
                if (dir.magnitude > 0.001f)
                    frontWorld = transform.position + dir.normalized * (_gridSize * BounceBackOvershootRatio);
            }

            if (frontWorld.HasValue)
                _moveRoutine = StartCoroutine(BounceBackTwoPhase(frontWorld.Value, originWorld, d * 0.35f, d * 0.65f, originPos));
            else
                StartMoveTween(originWorld, d, SimpleTween.Ease.CubicIn, () => FinishBounceBack(originPos));
            IsMoving = true;
        }

        private IEnumerator BounceBackTwoPhase(Vector3 frontWorld, Vector3 originWorld, float d1, float d2, Vector2Int originPos)
        {
            Vector3 from = transform.position;
            float t = 0f;
            while (t < d1)
            {
                t += Time.deltaTime;
                transform.position = Vector3.LerpUnclamped(from, frontWorld, SimpleTween.Evaluate(SimpleTween.Ease.SineOut, t / d1));
                yield return null;
            }
            from = transform.position;
            t = 0f;
            while (t < d2)
            {
                t += Time.deltaTime;
                transform.position = Vector3.LerpUnclamped(from, originWorld, SimpleTween.Evaluate(SimpleTween.Ease.CubicIn, t / d2));
                yield return null;
            }
            transform.position = originWorld;
            _moveRoutine = null;
            FinishBounceBack(originPos);
        }

        protected virtual void FinishBounceBack(Vector2Int originPos)
        {
            IsMoving = false;
            transform.position = PositionForGridPos(originPos);
            GridPos = originPos;
            OnMovementSettled(originPos);
        }

        /// <summary>
        /// 撞怪顿挫（attack bump）：从 from 向 to 方向冲 30% 格距（0.08s Sine/Out）再弹回（0.07s Sine/In）。
        /// 移植自 Godot Player.PlayBumpAnimation。
        /// </summary>
        public virtual void PlayBumpAnimation(Vector2Int fromPos, Vector2Int toPos)
        {
            KillMoveTween();
            var fromWorld = PositionForGridPos(fromPos);
            var bumpWorld = fromWorld + (PositionForGridPos(toPos) - fromWorld) * 0.3f;
            _moveRoutine = StartCoroutine(BumpRoutine(fromWorld, bumpWorld, fromPos));
            IsMoving = true;
        }

        private IEnumerator BumpRoutine(Vector3 fromWorld, Vector3 bumpWorld, Vector2Int fromPos)
        {
            float t = 0f;
            const float d1 = 0.08f, d2 = 0.07f;
            while (t < d1)
            {
                t += Time.deltaTime;
                transform.position = Vector3.LerpUnclamped(fromWorld, bumpWorld, SimpleTween.Evaluate(SimpleTween.Ease.SineOut, t / d1));
                yield return null;
            }
            t = 0f;
            while (t < d2)
            {
                t += Time.deltaTime;
                transform.position = Vector3.LerpUnclamped(bumpWorld, fromWorld, SimpleTween.Evaluate(SimpleTween.Ease.SineIn, t / d2));
                yield return null;
            }
            transform.position = fromWorld;
            _moveRoutine = null;
            IsMoving = false;
            GridPos = fromPos;
            OnMovementSettled(fromPos);
        }
    }
}
