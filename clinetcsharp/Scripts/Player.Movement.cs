using Godot;

namespace ClinetCSharp
{
    public partial class Player
    {
        private Vector2I _moveFromPos;
        private Vector2I _moveTargetPos;
        private int _moveDurationMs;
        private int _moveCheckRatio;
        private int _moveDualStartRatio;
        private int _moveDualEndRatio;
        private Godot.Timer? _checkTimer;
        private int _moveSentCount;           // 已发送但未收到响应的请求数
        private const int MaxMoveQueue = 3;    // 最多预发 3 格，不被网络阻塞
        private bool _collisionMove;
        private bool _bouncingBack;
        private Vector2I _lastMoveTargetPos;

        public override void _Process(double delta)
        {
            TryStartPendingServerGridCorrection();

            if (!IsServerGridCorrectionActive() && !IsMoving)
            {
                var targetPos = UiUtils.GridToWorld(_gridPos, GridSize);
                if (Position.DistanceTo(targetPos) > 0.5f)
                    Position = targetPos;
            }

            // 视觉预加载：距离当前目标还有 15% 时，提前开始下一格 tween
            // 新 tween 覆盖旧 tween，实现两格之间的视觉无缝衔接
            if (IsMoving && _moveSentCount < MaxMoveQueue && !_bouncingBack && string.IsNullOrEmpty(CastingSkill))
            {
                var currentTargetWorld = UiUtils.GridToWorld(_moveTargetPos, GridSize);
                float remainingDist = Position.DistanceTo(currentTargetWorld);
                if (remainingDist < GridSize * 0.20f)
                    TryStartHeldDirectionMove();
            }

            HandleInput();
        }

        private void HandleInput()
        {
            if (IsMoving || _moveSentCount >= MaxMoveQueue || IsServerGridCorrectionActive())
                return;

            // 蓄力期间禁止本地移动输入，避免服务器拒绝后产生回弹
            if (!string.IsNullOrEmpty(CastingSkill))
                return;

            TryStartHeldDirectionMove();
        }

        private void TryStartHeldDirectionMove()
        {
            var direction = Vector2I.Zero;
            if (Input.IsActionPressed("move_up"))
                direction.Y = -1;
            else if (Input.IsActionPressed("move_down"))
                direction.Y = 1;
            else if (Input.IsActionPressed("move_left"))
                direction.X = -1;
            else if (Input.IsActionPressed("move_right"))
                direction.X = 1;

            if (direction != Vector2I.Zero)
                MoveTo(_gridPos + direction);
        }

        private void MoveTo(Vector2I targetGridPos)
        {
            var gridManager = GetParent()?.GetNode<GridManager>("GridManager");
            if (gridManager != null && !gridManager.IsWalkable(targetGridPos))
            {
                if (TryStartMonsterCollisionMove(targetGridPos))
                    return;

                TryOpenBlockedChest(gridManager, targetGridPos);
                return;
            }

            BeginPredictedMove(targetGridPos);
        }

        private bool TryStartMonsterCollisionMove(Vector2I targetGridPos)
        {
            var monsterManager = GetTree()?.GetFirstNodeInGroup("monster_manager") as MonsterManager;
            if (monsterManager == null || !monsterManager.IsBlockedByMonster(targetGridPos))
                return false;

            _moveFromPos = _gridPos;
            _moveTargetPos = targetGridPos;
            _collisionMove = true;
            _moveSentCount++;

            var targetWorldPos = UiUtils.GridToWorld(targetGridPos, GridSize);
            StartMoveTween(targetWorldPos, MoveDuration);
            SendMoveStartRequest(_moveFromPos, targetGridPos);
            return true;
        }

        private void TryOpenBlockedChest(GridManager gridManager, Vector2I targetGridPos)
        {
            if (!gridManager.IsBlockedByChest(targetGridPos))
                return;

            var chestMgr = GetTree()?.GetFirstNodeInGroup("chest_manager") as ChestManager;
            chestMgr?.TryOpenChestAt(targetGridPos);
        }

        private void BeginPredictedMove(Vector2I targetGridPos)
        {
            // === 视觉预加载：旧 tween 还在运行时，先完成它再启动新 tween ===
            // 否则两个 Tween 同时竞争 "position" 属性 → 每帧抖动
            if (_currentTween != null && GodotObject.IsInstanceValid(_currentTween) && _currentTween.IsRunning())
            {
                var oldTarget = _lastMoveTargetPos;

                _currentTween.Kill();
                _currentTween = null;

                if (_collisionMove)
                    _collisionMove = false;

                SendMoveCompleteRequest(oldTarget);
                CheckAdjacentNpc();
            }

            _moveFromPos = _gridPos;
            _moveTargetPos = targetGridPos;
            _collisionMove = false;
            _moveSentCount++;
            ClearPendingServerGridCorrection();
            _gridPos = targetGridPos;

            var targetWorldPos = UiUtils.GridToWorld(_gridPos, GridSize);
            float actualDist = Position.DistanceTo(targetWorldPos);
            float adjustedDuration = MoveDuration * Mathf.Max(0.5f, actualDist / GridSize);
            StartMoveTween(targetWorldPos, adjustedDuration);
            SendMoveStartRequest(_moveFromPos, targetGridPos);
        }

        private void StartMoveTween(Vector2 targetWorldPos, float duration)
        {
            IsMoving = true;
            _lastMoveTargetPos = _moveTargetPos;
            _currentTween = CreateTween();
            _currentTween.SetTrans(Tween.TransitionType.Linear);
            _currentTween.TweenProperty(this, "position", targetWorldPos, duration);
            _currentTween.Finished += OnMoveFinished;
        }

        private void OnMoveFinished()
        {
            var completedTarget = _lastMoveTargetPos;
            bool hasNewTweenRunning = _currentTween != null && GodotObject.IsInstanceValid(_currentTween) && _currentTween.IsRunning();

            // 如果已有新 tween 在运行（视觉预加载），只发送完成请求，不做任何位置干预
            if (hasNewTweenRunning)
            {
                if (_collisionMove)
                    _collisionMove = false;
                SendMoveCompleteRequest(completedTarget);
                CheckAdjacentNpc();
                return;
            }

            if (_collisionMove)
            {
                _gridPos = completedTarget;
                _collisionMove = false;
            }

            Position = UiUtils.GridToWorld(_gridPos, GridSize);
            SendMoveCompleteRequest(completedTarget);
            CheckAdjacentNpc();

            // 尝试开始下一格
            if (_moveSentCount < MaxMoveQueue && !_bouncingBack && string.IsNullOrEmpty(CastingSkill))
            {
                TryStartHeldDirectionMove();
                if (_moveSentCount > 0)
                    return;
            }

            IsMoving = false;
        }

        private void CheckAdjacentNpc()
        {
            var npcMgr = GetTree()?.GetFirstNodeInGroup("npc_manager") as NpcManager;
            if (npcMgr == null)
                return;

            var npc = npcMgr.GetAdjacentNpc(_gridPos);
            if (npc != null)
                npcMgr.ShowInteractMenu(npc, npc.NpcType, _gridPos);
            else
                npcMgr.CloseInteractMenu();
        }
    }
}
