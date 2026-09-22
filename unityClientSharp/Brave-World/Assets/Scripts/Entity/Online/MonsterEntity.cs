using UnityEngine;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// 怪物实体（显示 + 移动）。
    /// 移动部分移植自 Godot Monster.cs：记 pending 目标格、按向量改朝向、Quad/Out 插值、
    /// 落定后推进逻辑格并回调管理器清预约。
    /// </summary>
    public class MonsterEntity : EntityVisualBase
    {
        public uint InstanceId { get; private set; }
        public int MonsterId { get; private set; }
        public string MonsterName { get; private set; } = "";

        /// <summary>移动中的目标格（落定前逻辑格仍在原地）</summary>
        public Vector2Int? PendingGridPos { get; private set; }

        /// <summary>落定回调（MonsterManager 用来推进逻辑位置并清预约）。</summary>
        public System.Action<MonsterEntity> MoveVisualCompleted;

        /// <summary>怪物状态中文映射（对齐 Godot Monster.cs 状态表）。</summary>
        public static string StateToText(string state) => state switch
        {
            "idle" => "待机",
            "patrol" => "巡逻",
            "chase" => "追击",
            "return" => "回撤",
            "combat" => "接战",
            "combat_hold" => "对峙",
            "combat_chase" => "追杀",
            "combat_ranged_hold" => "远程瞄准",
            "combat_ranged_chase" => "远程追击",
            "combat_cast_hold" => "蓄法",
            "combat_cast_chase" => "施法追击",
            "dead" => "死亡",
            _ => "待机",
        };

        public void SetupFromInfo(uint instanceId, int monsterId, string name, int level, Vector2Int gridPos,
            int gridSize, int sizeX, int sizeY, int direction)
        {
            InstanceId = instanceId;
            MonsterId = monsterId;
            MonsterName = name ?? "";

            Setup(2, gridPos, gridSize, sizeX, sizeY, sortingOrder: 4);
            if (direction >= 0) Direction = direction;

            SetLabel(0, $"LV.{level} {(string.IsNullOrEmpty(MonsterName) ? "未知野怪" : MonsterName)}");
            SetLabel(1, MonsterConfigManager.GetQuality(monsterId));
            SetLabel(3, StateToText("idle"));
            // 血量平时无字段推送，血条常满（对齐 Godot 显示行为）
            SetHpFill(1f);
            SetMpFill(1f);
        }

        /// <summary>服务器驱动的插值移动（对齐 Godot Monster.MoveTo）。</summary>
        public void MoveTo(Vector2Int targetPos, float durationSec, int direction)
        {
            if (direction >= 0) Direction = direction;
            PendingGridPos = targetPos;
            base.MoveTo(targetPos, durationSec);
        }

        protected override void OnMovementSettled(Vector2Int gridPos)
        {
            if (PendingGridPos.HasValue && gridPos == PendingGridPos.Value)
            {
                PendingGridPos = null;
                MoveVisualCompleted?.Invoke(this);
            }
        }

        /// <summary>服务器取消移动：移动中弹回（带 pending 方向前冲），静止直接吸附。</summary>
        public void CancelMove(Vector2Int rollbackPos)
        {
            if (IsMoving && PendingGridPos.HasValue)
                PlayBounceBack(rollbackPos, PendingGridPos.Value);
            else
                RollbackTo(rollbackPos);
            PendingGridPos = null;
        }

        protected override void FinishBounceBack(Vector2Int originPos)
        {
            PendingGridPos = null;
            base.FinishBounceBack(originPos);
        }
    }
}
