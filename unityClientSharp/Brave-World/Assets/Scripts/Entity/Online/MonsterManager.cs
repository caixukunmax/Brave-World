using System.Collections.Generic;
using UnityClientSharp.Net;
using UnityEngine;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// 怪物管理器（显示 + 移动）。
    /// 移植自 Godot MonsterManager：生成/死亡/重生 + 服务器移动通知（footprint 预约、
    /// 状态标签、插值时长取服务器 durationMs）+ 移动取消弹回 + 行走阻挡查询。
    /// 裁剪：战斗状态、巡逻覆盖层。
    /// </summary>
    public class MonsterManager : MonoBehaviour
    {
        public static MonsterManager Instance { get; private set; }

        private readonly Dictionary<uint, MonsterEntity> _monsterById = new();
        /// <summary>怪物逻辑位置（tween 落定才推进，对齐 Godot _monsterPositions）</summary>
        private readonly Dictionary<uint, Vector2Int> _monsterPositions = new();
        /// <summary>移动目标 footprint 预约集合（tween 途中目标格即视为阻挡，对齐 Godot）</summary>
        private readonly HashSet<Vector2Int> _monsterReservedPositions = new();
        private int _gridSize = 111;

        public int GridSize { get => _gridSize; set => _gridSize = value; }

        private void Awake()
        {
            Instance = this;
            Walkability.Monsters = this;
        }

        private void Start()
        {
            if (NetworkManager.Instance == null) return;
            NetworkManager.Instance.MonsterDeathNotify += OnMonsterDeath;
            NetworkManager.Instance.MonsterRespawnNotify += OnMonsterRespawn;
            NetworkManager.Instance.MonsterMoveNotify += OnMonsterMove;
            NetworkManager.Instance.MonsterMoveCancelNotify += OnMonsterMoveCancel;
            NetworkManager.Instance.DirectionNotify += OnDirectionNotify;
            NetworkManager.Instance.CombatStateNotify += OnCombatState;
            NetworkManager.Instance.CombatEndNotify += OnCombatEnd;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (Walkability.Monsters == this) Walkability.Monsters = null;
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.MonsterDeathNotify -= OnMonsterDeath;
                NetworkManager.Instance.MonsterRespawnNotify -= OnMonsterRespawn;
                NetworkManager.Instance.MonsterMoveNotify -= OnMonsterMove;
                NetworkManager.Instance.MonsterMoveCancelNotify -= OnMonsterMoveCancel;
                NetworkManager.Instance.DirectionNotify -= OnDirectionNotify;
                NetworkManager.Instance.CombatStateNotify -= OnCombatState;
                NetworkManager.Instance.CombatEndNotify -= OnCombatEnd;
            }
        }

        // ============ 战斗状态（对齐 Godot MonsterManager 的 CombatStateNotify 处理）============

        private void OnCombatState(Game.CombatStateNotify notify)
        {
            if (notify.Units.Count == 0)
            {
                // 空 units = 全员脱战复位
                foreach (var m in _monsterById.Values)
                    ResetMonsterCombatVisual(m);
                return;
            }

            var updated = new HashSet<uint>();
            foreach (var unit in notify.Units)
            {
                // 怪物/NPC unit 的 IsPlayer 可能为 true（NPC 与玩家同段），按 InstanceId 匹配最可靠
                if (!_monsterById.TryGetValue((uint)unit.EntityId, out var m) || m == null)
                    continue;
                updated.Add((uint)unit.EntityId);

                if (m.SetHpFill((float)unit.Hp / Mathf.Max(1, unit.MaxHp)))
                    m.PlayHitEffect();
                m.SetMpFill((float)unit.Mp / Mathf.Max(1, unit.MaxMp));
                m.SetCombatAuraVisible(unit.InCombat);

                // 显示状态（对齐 ResolveCombatDisplayState 简化版）：施法中→技能名；战斗中→对峙；否则待机
                if (!string.IsNullOrEmpty(unit.CastingSkill))
                    m.SetLabel(3, unit.CastingSkill);
                else if (unit.InCombat)
                    m.SetLabel(3, MonsterEntity.StateToText("combat_hold"));
                else
                    m.SetLabel(3, MonsterEntity.StateToText("idle"));
            }

            // 不在本次同步中的怪物 → 复位
            foreach (var kv in _monsterById)
            {
                if (!updated.Contains(kv.Key))
                    ResetMonsterCombatVisual(kv.Value);
            }
        }

        private void ResetMonsterCombatVisual(MonsterEntity m)
        {
            if (m == null) return;
            m.SetCombatAuraVisible(false);
            m.SetHpFill(1f);
            m.SetMpFill(1f);
            m.SetLabel(3, MonsterEntity.StateToText("idle"));
        }

        private void OnCombatEnd(Game.CombatEndNotify notify)
        {
            foreach (var id in notify.EntityIds)
            {
                if (_monsterById.TryGetValue((uint)id, out var m))
                    ResetMonsterCombatVisual(m);
            }
        }

        // ============ 生成/清理 ============

        /// <summary>从缓存全量生成（先清旧怪）。</summary>
        public void SpawnMonsters(IEnumerable<Game.MonsterInfo> monsters)
        {
            ClearMonsters();
            if (monsters == null) return;

            foreach (var info in monsters)
                SpawnOne(info.InstanceId, (int)info.MonsterId, info.Name, (int)info.Level,
                    new Vector2Int(info.X, info.Y), info.SizeX, info.SizeY, info.Direction);

            Debug.Log($"[MonsterManager] 生成 {_monsterById.Count} 只怪物");
        }

        private MonsterEntity SpawnOne(uint instanceId, int monsterId, string name, int level, Vector2Int pos,
            int sizeX, int sizeY, int direction)
        {
            var go = new GameObject($"Monster_{instanceId}_{name}");
            go.transform.SetParent(transform, false);
            var m = go.AddComponent<MonsterEntity>();
            m.SetupFromInfo(instanceId, monsterId, name, level, pos, _gridSize,
                sizeX > 0 ? sizeX : 1, sizeY > 0 ? sizeY : 1, direction);
            m.MoveVisualCompleted = OnMonsterMoveVisualCompleted;
            _monsterById[instanceId] = m;
            _monsterPositions[instanceId] = pos;
            return m;
        }

        public IEnumerable<MonsterEntity> GetMonsters() => _monsterById.Values;

        public void ClearMonsters()
        {
            foreach (var m in _monsterById.Values)
                if (m != null) Destroy(m.gameObject);
            _monsterById.Clear();
            _monsterPositions.Clear();
            _monsterReservedPositions.Clear();
        }

        // ============ 阻挡查询（供 Walkability 门面）============

        /// <summary>pos 被任一怪物当前 footprint 或移动预约 footprint 覆盖。</summary>
        public bool IsBlockedByMonster(Vector2Int pos)
        {
            if (_monsterReservedPositions.Contains(pos))
                return true;
            foreach (var kv in _monsterById)
            {
                var m = kv.Value;
                if (m == null) continue;
                if (!_monsterPositions.TryGetValue(kv.Key, out var center))
                    center = m.GridPos;
                var anchor = new Vector2Int(
                    center.x - (Mathf.Max(1, m.SizeX) - 1) / 2,
                    center.y - (Mathf.Max(1, m.SizeY) - 1) / 2);
                if (pos.x >= anchor.x && pos.x < anchor.x + Mathf.Max(1, m.SizeX) &&
                    pos.y >= anchor.y && pos.y < anchor.y + Mathf.Max(1, m.SizeY))
                    return true;
            }
            return false;
        }

        private void AddReservedFootprint(Vector2Int center, int sizeX, int sizeY)
        {
            var anchor = new Vector2Int(
                center.x - (Mathf.Max(1, sizeX) - 1) / 2,
                center.y - (Mathf.Max(1, sizeY) - 1) / 2);
            for (int dx = 0; dx < Mathf.Max(1, sizeX); dx++)
                for (int dy = 0; dy < Mathf.Max(1, sizeY); dy++)
                    _monsterReservedPositions.Add(new Vector2Int(anchor.x + dx, anchor.y + dy));
        }

        private void RemoveReservedFootprint(Vector2Int center, int sizeX, int sizeY)
        {
            var anchor = new Vector2Int(
                center.x - (Mathf.Max(1, sizeX) - 1) / 2,
                center.y - (Mathf.Max(1, sizeY) - 1) / 2);
            for (int dx = 0; dx < Mathf.Max(1, sizeX); dx++)
                for (int dy = 0; dy < Mathf.Max(1, sizeY); dy++)
                    _monsterReservedPositions.Remove(new Vector2Int(anchor.x + dx, anchor.y + dy));
        }

        // ============ 网络事件 ============

        private void OnMonsterDeath(Game.MonsterDeathNotify notify)
        {
            if (!_monsterById.TryGetValue(notify.InstanceId, out var m)) return;
            _monsterById.Remove(notify.InstanceId);
            _monsterPositions.Remove(notify.InstanceId);
            if (m != null)
            {
                if (m.PendingGridPos.HasValue)
                    RemoveReservedFootprint(m.PendingGridPos.Value, m.SizeX, m.SizeY);
                m.PlayDeathFade(0.5f); // DeathEffectMode 1：淡出+缩放到 0
            }
        }

        private void OnMonsterRespawn(Game.MonsterRespawnNotify notify)
        {
            if (_monsterById.TryGetValue(notify.InstanceId, out var old))
            {
                _monsterById.Remove(notify.InstanceId);
                _monsterPositions.Remove(notify.InstanceId);
                if (old != null)
                {
                    if (old.PendingGridPos.HasValue)
                        RemoveReservedFootprint(old.PendingGridPos.Value, old.SizeX, old.SizeY);
                    Destroy(old.gameObject);
                }
            }
            SpawnOne(notify.InstanceId, (int)notify.MonsterId, notify.Name, (int)notify.Level,
                new Vector2Int(notify.X, notify.Y), notify.SizeX, notify.SizeY, -1);
        }

        /// <summary>怪物移动通知（370）：预约目标 footprint + 状态标签 + 服务器时长插值。</summary>
        private void OnMonsterMove(Game.MonsterMoveNotify notify)
        {
            if (!_monsterById.TryGetValue(notify.InstanceId, out var m) || m == null) return;

            // 改目标时先清旧预约，防残留
            if (m.PendingGridPos.HasValue)
                RemoveReservedFootprint(m.PendingGridPos.Value, m.SizeX, m.SizeY);

            var from = new Vector2Int(notify.FromX, notify.FromY);
            var to = new Vector2Int(notify.ToX, notify.ToY);
            _monsterPositions[notify.InstanceId] = from; // 逻辑位置重置到 from
            AddReservedFootprint(to, m.SizeX, m.SizeY);

            m.SetLabel(3, MonsterEntity.StateToText(notify.State ?? "idle"));
            float durationSec = notify.DurationMs > 0 ? notify.DurationMs / 1000f : 0.15f;
            m.MoveTo(to, durationSec, notify.Direction);
        }

        private void OnMonsterMoveVisualCompleted(MonsterEntity m)
        {
            _monsterPositions[m.InstanceId] = m.GridPos;
            RemoveReservedFootprint(m.GridPos, m.SizeX, m.SizeY);
        }

        private void OnMonsterMoveCancel(Game.MonsterMoveCancelNotify notify)
        {
            if (!_monsterById.TryGetValue(notify.InstanceId, out var m) || m == null) return;
            if (m.PendingGridPos.HasValue)
                RemoveReservedFootprint(m.PendingGridPos.Value, m.SizeX, m.SizeY);

            var rollbackPos = new Vector2Int(notify.RollbackX, notify.RollbackY);
            _monsterPositions[notify.InstanceId] = rollbackPos;
            m.CancelMove(rollbackPos);
        }

        /// <summary>朝向广播（432）：仅喂给怪物（对齐 Godot MapManager.OnDirectionNotify）。</summary>
        private void OnDirectionNotify(Game.DirectionNotify notify)
        {
            if (_monsterById.TryGetValue((uint)notify.EntityId, out var m) && m != null)
                m.Direction = (int)notify.Direction;
        }
    }
}
