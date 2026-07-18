using UnityEngine;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// 玩家位置服务器矫正 — 移植自 Godot Player.Movement.Authority.cs。
    /// 触发源：ApplyRoleInfo（每次 RoleAttrNotify）。延迟条件：IsMoving/_moveSentCount>0/_bouncingBack。
    /// 本地预测起步即丢弃挂起矫正。
    /// </summary>
    public partial class PlayerEntity
    {
        private Vector2Int? _pendingServerGridPos;
        private Coroutine _correctionRoutine;
        private bool _correctionActive;

        public void QueueServerGridCorrection(Vector2Int serverGridPos)
        {
            _pendingServerGridPos = serverGridPos;
            TryStartPendingServerGridCorrection();
        }

        /// <summary>每帧 Update 第一行调用（对齐 Godot）。</summary>
        private void TryStartPendingServerGridCorrection()
        {
            if (!_pendingServerGridPos.HasValue)
                return;
            // 延迟策略：移动中/在途/弹回中一律等（对齐 PlayerRoleInfoPositionSyncPolicy）
            if (IsMoving || _moveSentCount > 0 || _bouncingBack)
                return;
            if (_correctionActive)
                return;

            var pos = _pendingServerGridPos.Value;
            _pendingServerGridPos = null;

            // 逻辑先行：_gridPos 与 _moveFromPos 立即改为服务器格
            GridPos = pos;
            _moveFromPos = pos;

            var targetWorld = PositionForGridPos(pos);
            float dist = Vector3.Distance(transform.position, targetWorld);
            if (dist <= 0.5f)
            {
                transform.position = targetWorld; // 直接吸附
                TryStartPendingServerGridCorrection();
                return;
            }

            // Sine/Out 滑过去，时长 clamp(0.04 + dist/gridSize*0.04, 0.04, 0.12)
            float duration = Mathf.Clamp(0.04f + (dist / _gridSize) * 0.04f, 0.04f, 0.12f);
            _correctionActive = true;
            _correctionRoutine = StartCoroutine(CorrectionRoutine(targetWorld, duration));
        }

        private System.Collections.IEnumerator CorrectionRoutine(Vector3 targetWorld, float duration)
        {
            Vector3 from = transform.position;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                transform.position = Vector3.LerpUnclamped(from, targetWorld,
                    SimpleTween.Evaluate(SimpleTween.Ease.SineOut, t / duration));
                yield return null;
            }
            transform.position = targetWorld;
            _correctionRoutine = null;
            _correctionActive = false;
            TryStartPendingServerGridCorrection(); // 递归尝试下一个挂起
        }

        public bool IsServerGridCorrectionActive() => _correctionActive;

        /// <summary>丢弃挂起+进行中的矫正（BeginPredictedMove/弹回/回滚/死亡/传送调用）。</summary>
        public void ClearPendingServerGridCorrection()
        {
            _pendingServerGridPos = null;
            if (_correctionRoutine != null)
            {
                StopCoroutine(_correctionRoutine);
                _correctionRoutine = null;
            }
            _correctionActive = false;
        }
    }
}
