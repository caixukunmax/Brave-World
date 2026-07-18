using UnityEngine;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// 玩家移动 — 输入与本地预测起步。
    /// 移植自 Godot Player.Movement.cs：Update 固定序（矫正→吸附→20% 预加载→输入）、
    /// WASD 四向（上>下>左>右）、MaxMoveQueue=3 在途、逻辑先行视觉追赶。
    /// </summary>
    public partial class PlayerEntity
    {
        private Vector2Int _moveFromPos;
        private Vector2Int _moveTargetPos;
        private int _moveDurationMs;
        private int _moveCheckRatio = 30;
        private int _moveDualStartRatio; // 服务器概念，客户端只存不用（对齐 Godot）
        private int _moveDualEndRatio;
        private int _moveSentCount;           // 已发送但未收到响应的请求数
        private const int MaxMoveQueue = 3;    // 最多预发 3 格，不被网络阻塞
        private bool _collisionMove;
        private bool _bouncingBack;
        private Vector2Int _lastMoveTargetPos;
        private float _lastMoveActivityTime;  // 看门狗：最近一次 起步发送/收到响应 的时刻

        /// <summary>每格移动时长（秒），ApplyRoleInfo 按移速属性更新</summary>
        public float MoveDuration = 0.18f;

        /// <summary>蓄力中的技能名（战斗阶段接入；非空时禁止移动）</summary>
        public string CastingSkill { get; set; } = "";

        private void Update()
        {
            // 固定顺序（对齐 Godot Player._Process）
            TryStartPendingServerGridCorrection();

            // 静止吸附：非矫正、非移动时兜住 tween 之外的漂移
            if (!IsServerGridCorrectionActive() && !IsMoving)
            {
                var targetPos = PositionForGridPos(GridPos);
                if (Vector3.Distance(transform.position, targetPos) > 0.5f)
                    transform.position = targetPos;
            }

            // 20% 预加载：距离当前目标 <20% 格时提前起步下一格，实现两格无缝衔接
            if (IsMoving && _moveSentCount < MaxMoveQueue && !_bouncingBack && string.IsNullOrEmpty(CastingSkill))
            {
                var currentTargetWorld = PositionForGridPos(_moveTargetPos);
                if (Vector3.Distance(transform.position, currentTargetWorld) < _gridSize * 0.20f)
                    TryStartHeldDirectionMove();
            }

            UpdateMoveWatchdog();
            HandleInput();
        }

        private void HandleInput()
        {
            if (IsMoving || _moveSentCount >= MaxMoveQueue || IsServerGridCorrectionActive())
                return;
            if (!string.IsNullOrEmpty(CastingSkill))
                return;
            // TODO(UI 阶段): GUI 文本输入聚焦时跳过（UiUtils.IsGuiTextInputFocused）

            TryStartHeldDirectionMove();
        }

        /// <summary>按当前按住的 WASD 尝试起步（上>下>左>右，仅 4 向）。</summary>
        private void TryStartHeldDirectionMove()
        {
            // 对齐 Godot 输入映射：仅 WASD，if-else 链优先级 上>下>左>右
            if (Input.GetKey(KeyCode.W)) MoveTo(GridPos + new Vector2Int(0, -1));
            else if (Input.GetKey(KeyCode.S)) MoveTo(GridPos + new Vector2Int(0, 1));
            else if (Input.GetKey(KeyCode.A)) MoveTo(GridPos + new Vector2Int(-1, 0));
            else if (Input.GetKey(KeyCode.D)) MoveTo(GridPos + new Vector2Int(1, 0));
        }

        /// <summary>目标格移动入口：阻挡分支（怪物→碰撞移动；未开宝箱→开箱请求；其它→原地不动）。</summary>
        private void MoveTo(Vector2Int targetPos)
        {
            if (!Walkability.IsWalkable(targetPos))
            {
                if (Walkability.Monsters != null && Walkability.Monsters.IsBlockedByMonster(targetPos))
                {
                    TryStartMonsterCollisionMove(targetPos);
                    return;
                }
                if (Walkability.Chests != null && Walkability.Chests.IsBlockedByChest(targetPos))
                {
                    TryOpenBlockedChest(targetPos);
                    return;
                }
                return; // 地形/装饰阻挡：原地不动
            }
            BeginPredictedMove(targetPos);
        }

        /// <summary>本地预测起步：逻辑格先行、计数+1、丢弃挂起矫正、发 324、Linear tween。</summary>
        private void BeginPredictedMove(Vector2Int targetPos)
        {
            // 旧 tween 被预加载覆盖时，立即为旧格补发 327，避免丢到达确认
            bool wasMoving = IsMoving;
            var oldTarget = _lastMoveTargetPos;
            KillMoveTween();
            if (wasMoving)
            {
                SendMoveCompleteRequest(oldTarget);
                CheckAdjacentNpc();
            }

            _moveFromPos = GridPos;
            _moveTargetPos = targetPos;
            ClearPendingServerGridCorrection(); // 本地预测一旦起步，未落地的服务器矫正直接作废
            Direction = DirectionFromVector(targetPos - GridPos);

            GridPos = targetPos; // 逻辑先行，视觉 tween 追赶
            // 计数只记"真正发出去了"的请求——离线/未过网关时不发也不计，看门狗自然不触发
            if (SendMoveStartRequest(_moveFromPos, targetPos))
            {
                if (_moveSentCount == 0)
                    _lastMoveActivityTime = Time.time;
                _moveSentCount++;
            }

            var targetWorld = PositionForGridPos(targetPos);
            float dist = Vector3.Distance(transform.position, targetWorld);
            float duration = MoveDuration * Mathf.Max(0.5f, dist / _gridSize);

            IsMoving = true;
            _lastMoveTargetPos = targetPos;
            StartMoveTween(targetWorld, duration, SimpleTween.Ease.Linear, OnMoveFinished);
        }

        /// <summary>碰撞移动：目标格被怪物占据但其它可走——照预测起步，标记 _collisionMove。</summary>
        private void TryStartMonsterCollisionMove(Vector2Int targetPos)
        {
            Direction = DirectionFromVector(targetPos - GridPos);
            BeginPredictedMove(targetPos);
            _collisionMove = true;
        }

        /// <summary>撞未开宝箱：直接发开箱请求，人不动。</summary>
        private void TryOpenBlockedChest(Vector2Int targetPos)
        {
            SendOpenChestRequest(targetPos);
        }

        private void OnMoveFinished()
        {
            IsMoving = false;
            SendMoveCompleteRequest(_moveTargetPos);
            CheckAdjacentNpc();

            // 碰撞格特判：落定逻辑格（对齐 Godot）
            if (_collisionMove)
            {
                GridPos = _moveTargetPos;
                _collisionMove = false;
            }

            if (!_bouncingBack && string.IsNullOrEmpty(CastingSkill))
                TryStartHeldDirectionMove();
        }

        /// <summary>移动完成时检查四邻格 NPC：邻格即弹交互菜单、走开即关（对齐 Godot Player.Movement.CheckAdjacentNpc）。</summary>
        private void CheckAdjacentNpc()
        {
            if (NpcManager.Instance == null) return;
            var npc = NpcManager.Instance.GetAdjacentNpc(GridPos);
            if (npc != null) NpcManager.Instance.ShowInteractMenu(npc, GridPos);
            else NpcManager.Instance.CloseInteractMenu();
        }

        /// <summary>响应超时兜底（修复 Godot 已知软锁：响应彻底停滞时 _moveSentCount/IsMoving 滞留）。
        /// 计时锚点是"最近一次起步发送或收到响应"——持续移动时在途计数不归零是正常态，不能用最老请求计时。</summary>
        private void UpdateMoveWatchdog()
        {
            if (_moveSentCount <= 0) return;
            // 离线时纯本地预测，无响应可等（对齐 Godot 无看门狗行为）
            var nm = Net.NetworkManager.Instance;
            if (nm == null || !nm.IsServerConnected()) return;
            if (Time.time - _lastMoveActivityTime > MoveDuration * 2f + 1f)
            {
                Debug.LogWarning("[Player] MoveResponse 超时，强制回滚");
                RollbackTo(_moveFromPos);
            }
        }
    }
}
