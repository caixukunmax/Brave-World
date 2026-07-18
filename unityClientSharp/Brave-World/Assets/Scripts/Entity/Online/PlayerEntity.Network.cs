using Protocol;
using UnityEngine;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// 玩家移动 — 网络收发与服务器响应。
    /// 移植自 Godot Player.Movement.Network.cs：324 起步 / 326 检查点确认 / 327 到达 / 329 碰撞，
    /// OnMoveResponse 三分支（attack bump / 失败回滚 / 成功计时+检查点）。
    /// </summary>
    public partial class PlayerEntity
    {
        private Coroutine _checkRoutine;

        // ============ 上行 ============

        private bool SendMoveStartRequest(Vector2Int from, Vector2Int to)
        {
            var nm = Net.NetworkManager.Instance;
            if (nm == null || !nm.IsServerConnected() || string.IsNullOrEmpty(nm.GatewayToken))
                return false;

            return nm.SendPacket(MessageId.GameMoveReq, new Game.MoveRequest
            {
                FromX = from.x,
                FromY = from.y,
                ToX = to.x,
                ToY = to.y,
                MapName = nm.CurrentMapName ?? "xinshoucun",
            });
        }

        private void SendMoveConfirmRequest(Vector2Int target)
        {
            var nm = Net.NetworkManager.Instance;
            if (nm == null || !nm.IsServerConnected()) return;
            nm.SendPacket(MessageId.GameMoveConfirmReq, new Game.MoveConfirmRequest
            {
                TargetX = target.x,
                TargetY = target.y,
            });
        }

        private void SendMoveCompleteRequest(Vector2Int target)
        {
            var nm = Net.NetworkManager.Instance;
            if (nm == null || !nm.IsServerConnected()) return;
            nm.SendPacket(MessageId.GameMoveCompleteReq, new Game.MoveCompleteRequest
            {
                TargetX = target.x,
                TargetY = target.y,
            });
        }

        private void SendMoveCollisionNotify(Vector2Int target)
        {
            var nm = Net.NetworkManager.Instance;
            if (nm == null || !nm.IsServerConnected()) return;
            nm.SendPacket(MessageId.GameMoveCollisionNotify, new Game.MoveCollisionNotify
            {
                TargetX = target.x,
                TargetY = target.y,
            });
        }

        private void SendOpenChestRequest(Vector2Int chestPos)
        {
            var nm = Net.NetworkManager.Instance;
            if (nm == null || !nm.IsServerConnected()) return;
            uint chestId = Walkability.Chests != null ? Walkability.Chests.GetChestIdAt(chestPos) : 0;
            if (chestId == 0) return;
            nm.SendPacket(MessageId.GameOpenChestReq, new Game.OpenChestRequest { ChestId = chestId });
        }

        // ============ 响应处理 ============

        private void OnMoveResponse(Game.MoveResponse rsp)
        {
            if (_moveSentCount > 0)
                _moveSentCount--;
            _lastMoveActivityTime = Time.time; // 看门狗锚点：响应流动即重置

            if (rsp.Message == "attack")
            {
                // 服务器认定这一格是打怪：顿挫动画，不做其它状态重置
                var originPos = new Vector2Int((int)rsp.X, (int)rsp.Y);
                GridPos = originPos;
                PlayBumpAnimation(originPos, _moveTargetPos);
                return;
            }

            if (rsp.Code != Common.ErrorCode.Success || rsp.DurationMs <= 0)
            {
                var rollbackPos = new Vector2Int((int)rsp.X, (int)rsp.Y);
                if (rollbackPos.x == 0 && rollbackPos.y == 0)
                    rollbackPos = _moveFromPos;
                RollbackTo(rollbackPos);
                return;
            }

            // 成功：以响应为准更新移动计时（不重建当前 tween，中途重建会跳变）
            ApplyServerMoveTiming(rsp);
            StartMoveCheckpointTimer();

            // 动画已完成则立即尝试下一格
            if (!IsMoving && !_bouncingBack && string.IsNullOrEmpty(CastingSkill))
                TryStartHeldDirectionMove();
        }

        private void ApplyServerMoveTiming(Game.MoveResponse rsp)
        {
            _moveDurationMs = rsp.DurationMs;
            _moveCheckRatio = rsp.CheckRatio;
            _moveDualStartRatio = rsp.DualStartRatio; // 只存不用（对齐 Godot）
            _moveDualEndRatio = rsp.DualEndRatio;
            MoveDuration = rsp.DurationMs / 1000f; // 供下一格用
        }

        // ============ 移动检查点（checkRatio% 时刻复查）============

        private void StartMoveCheckpointTimer()
        {
            StopMoveCheckpoint();
            float waitSec = _moveDurationMs * _moveCheckRatio / 100f / 1000f;
            _checkRoutine = StartCoroutine(CheckpointRoutine(waitSec));
        }

        private void StopMoveCheckpoint()
        {
            if (_checkRoutine != null)
            {
                StopCoroutine(_checkRoutine);
                _checkRoutine = null;
            }
        }

        private System.Collections.IEnumerator CheckpointRoutine(float waitSec)
        {
            yield return new WaitForSeconds(waitSec);
            _checkRoutine = null;
            OnMoveCheckPoint();
        }

        private void OnMoveCheckPoint()
        {
            if (_collisionMove)
            {
                // 碰撞移动：怪物仍在目标格 → 弹回 + 发 329；怪物走了 → 发 326
                if (Walkability.Monsters != null && Walkability.Monsters.IsBlockedByMonster(_moveTargetPos))
                {
                    PlayBounceBack(_moveFromPos);
                    SendMoveCollisionNotify(_moveTargetPos);
                }
                else
                {
                    SendMoveConfirmRequest(_moveTargetPos);
                }
                return;
            }

            // 普通移动：目标格变得不可走 → 回滚；否则发 326
            if (!Walkability.IsWalkable(_moveTargetPos))
                RollbackTo(_moveFromPos);
            else
                SendMoveConfirmRequest(_moveTargetPos);
        }

        // ============ 服务器取消（328）============

        private void OnMoveCancelReceived(Game.MoveCancelNotify notify)
        {
            // 对齐服务器语义：EntityId == AccountId（Godot 用节点 InstanceId 比是 bug）
            var nm = Net.NetworkManager.Instance;
            if (nm == null || notify.EntityId != (ulong)nm.AccountId)
                return;
            _lastMoveActivityTime = Time.time; // 取消通知也是响应流动

            var rollbackPos = new Vector2Int(notify.RollbackX, notify.RollbackY);
            if (_bouncingBack)
            {
                GridPos = rollbackPos; // 弹回中只改逻辑格
            }
            else if (!IsMoving)
            {
                GridPos = rollbackPos;
                transform.position = PositionForGridPos(rollbackPos);
            }
            else
            {
                RollbackTo(rollbackPos);
            }
        }

        // ============ 死亡重生（392）============

        private void OnPlayerDeath(Game.PlayerDeathNotify notify)
        {
            ClearPendingServerGridCorrection();
            KillMoveTween();
            StopMoveCheckpoint();
            IsMoving = false;
            _bouncingBack = false;
            _collisionMove = false;
            _moveSentCount = 0;

            var spawnPos = new Vector2Int((int)notify.SpawnX, (int)notify.SpawnY);
            GridPos = spawnPos;
            _moveFromPos = spawnPos;
            transform.position = PositionForGridPos(spawnPos);

            // 血满 + 回填 HP/MP（直接字段，对齐 Godot Player.NetworkSync.OnPlayerDeath）
            SetHpFill(1f);
            SetHpFill((float)notify.Hp / Mathf.Max(1, notify.MaxHp));
            SetMpFill((float)notify.Mp / Mathf.Max(1, notify.MaxMp));

            CastingSkill = "";
        }
    }
}
