using Godot;
using Protocol;

namespace ClinetCSharp
{
    public partial class Player
    {
        private void SendMoveStartRequest(Vector2I from, Vector2I to)
        {
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm == null || !nm.IsServerConnected() || string.IsNullOrEmpty(nm.GatewayToken))
                return;

            var req = new Game.MoveRequest
            {
                FromX = from.X,
                FromY = from.Y,
                ToX = to.X,
                ToY = to.Y,
                MapName = GetMapName(),
            };
            nm.SendPacket(MessageId.GameMoveReq, req);
        }

        private string GetMapName()
        {
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            return nm?.CurrentMapName ?? "xinshoucun";
        }

        private void OnMoveResponse(Game.MoveResponse rsp)
        {
            if (_moveSentCount > 0)
                _moveSentCount--;

            if (rsp.Message == "attack")
            {
                GD.Print($"[Player] Server acknowledged attack at ({rsp.X}, {rsp.Y})");
                var originPos = new Vector2I((int)rsp.X, (int)rsp.Y);
                _gridPos = originPos;
                PlayBumpAnimation(originPos, _moveTargetPos);
                return;
            }

            if (rsp.Code != Common.ErrorCode.Success || rsp.DurationMs <= 0)
            {
                var rollbackPos = new Vector2I((int)rsp.X, (int)rsp.Y);
                if (rollbackPos.X == 0 && rollbackPos.Y == 0)
                    rollbackPos = _moveFromPos;

                GD.Print($"[Player] Move rejected by server code={rsp.Code} message='{rsp.Message}' from=({_moveFromPos.X}, {_moveFromPos.Y}) target=({_moveTargetPos.X}, {_moveTargetPos.Y}) rollback=({rollbackPos.X}, {rollbackPos.Y})");
                RollbackTo(rollbackPos);
                return;
            }

            ApplyServerMoveTiming(rsp);
            StartMoveCheckpointTimer();

            // 服务器响应已到达，如果动画也已完成，立即尝试下一格
            if (!IsMoving && !_bouncingBack && string.IsNullOrEmpty(CastingSkill))
                TryStartHeldDirectionMove();
        }

        private void ApplyServerMoveTiming(Game.MoveResponse rsp)
        {
            _moveDurationMs = rsp.DurationMs;
            _moveCheckRatio = rsp.CheckRatio;
            _moveDualStartRatio = rsp.DualStartRatio;
            _moveDualEndRatio = rsp.DualEndRatio;

            float durationSec = _moveDurationMs / 1000.0f;
            MoveDuration = durationSec; // 更新供下一格使用

            // 不再杀掉当前 tween 重建——连续移动时中途重建会导致角色跳动
            // 如果当前 tween 还在运行，让它自然完成；下一格会使用新的 MoveDuration
        }

        private void StartMoveCheckpointTimer()
        {
            var checkDelay = _moveDurationMs * _moveCheckRatio / 100.0f / 1000.0f;
            _checkTimer = new Godot.Timer();
            _checkTimer.WaitTime = checkDelay;
            _checkTimer.OneShot = true;
            _checkTimer.Timeout += OnMoveCheckPoint;
            AddChild(_checkTimer);
            _checkTimer.Start();
        }

        private void OnMoveCheckPoint()
        {
            if (_collisionMove)
            {
                HandleCollisionMoveCheckpoint();
                return;
            }

            var gridManager = GetParent()?.GetNode<GridManager>("GridManager");
            if (gridManager != null && !gridManager.IsWalkable(_moveTargetPos))
            {
                GD.Print($"[Player] Checkpoint: target {_moveTargetPos} not walkable, rolling back");
                RollbackTo(_moveFromPos);
                return;
            }

            SendMoveConfirmRequest();
        }

        private void HandleCollisionMoveCheckpoint()
        {
            var mm = GetTree()?.GetFirstNodeInGroup("monster_manager") as MonsterManager;
            if (mm != null && mm.IsBlockedByMonster(_moveTargetPos))
            {
                GD.Print($"[Player] Checkpoint: enemy detected at {_moveTargetPos}, bouncing back + collision notify");
                PlayBounceBack(_moveFromPos);
                SendMoveCollisionNotify(_moveTargetPos);
                return;
            }

            SendMoveConfirmRequest();
        }

        private void SendMoveConfirmRequest()
        {
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm == null || !nm.IsServerConnected())
                return;

            var req = new Game.MoveConfirmRequest
            {
                TargetX = _moveTargetPos.X,
                TargetY = _moveTargetPos.Y,
            };
            nm.SendPacket(MessageId.GameMoveConfirmReq, req);
        }

        private void SendMoveCollisionNotify(Vector2I targetPos)
        {
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm == null || !nm.IsServerConnected())
                return;

            var notify = new Game.MoveCollisionNotify
            {
                TargetX = targetPos.X,
                TargetY = targetPos.Y,
            };
            nm.SendPacket(MessageId.GameMoveCollisionNotify, notify);
        }

        private void SendMoveCompleteRequest() => SendMoveCompleteRequest(_moveTargetPos);

        private void SendMoveCompleteRequest(Vector2I targetPos)
        {
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm == null || !nm.IsServerConnected())
                return;

            var req = new Game.MoveCompleteRequest
            {
                TargetX = targetPos.X,
                TargetY = targetPos.Y,
            };
            nm.SendPacket(MessageId.GameMoveCompleteReq, req);
        }
    }
}
