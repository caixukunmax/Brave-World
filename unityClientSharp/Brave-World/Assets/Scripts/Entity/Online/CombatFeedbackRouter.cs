using UnityEngine;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// 战斗反馈路由 — 移植自 Godot NetworkManager.Dispatch.Combat 的攻击者抖动逻辑：
    /// CombatLogNotify 的 DAMAGE 条目按**名字**找攻击者实体（协议无 ID 字段），触发攻击前冲。
    /// </summary>
    public class CombatFeedbackRouter : MonoBehaviour
    {
        private void Start()
        {
            if (Net.NetworkManager.Instance != null)
                Net.NetworkManager.Instance.CombatLogNotify += OnCombatLog;
        }

        private void OnDestroy()
        {
            if (Net.NetworkManager.Instance != null)
                Net.NetworkManager.Instance.CombatLogNotify -= OnCombatLog;
        }

        private void OnCombatLog(Game.CombatLogNotify notify)
        {
            foreach (var entry in notify.Entries)
            {
                if (entry.LogType != Game.CombatLogType.CombatLogDamage)
                    continue;

                var attacker = FindEntityByName(entry.ActorName);
                if (attacker == null) continue;

                var target = FindEntityByName(entry.TargetName);
                attacker.PlayAttackShake(target != null ? target.GridPos : (Vector2Int?)null);
            }
        }

        /// <summary>按名字查找实体（玩家按 CharacterName、怪物按 MonsterName，对齐 Godot）。</summary>
        private EntityVisualBase FindEntityByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            var player = PlayerEntity.Instance;
            if (player != null && player.CharacterName == name)
                return player;

            if (MonsterManager.Instance != null)
            {
                foreach (var m in MonsterManager.Instance.GetMonsters())
                    if (m.MonsterName == name)
                        return m;
            }
            return null;
        }
    }
}
