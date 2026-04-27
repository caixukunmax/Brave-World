using GameServer.Tables;
using PGame = global::Game;

namespace GameServer.Services.Map.Combat;

/// <summary>
/// 战斗叙事触发器引擎 — 根据条件概率触发叙事文本
/// </summary>
public class CombatNarrationEngine
{
    private readonly LubanTableLoader _tables;
    private readonly Random _rng = new();
    
    /// <summary>实体冷却记录: entityId → (condition, lastTriggerTime)</summary>
    private readonly Dictionary<long, Dictionary<string, long>> _cooldowns = new();

    public CombatNarrationEngine(LubanTableLoader tables)
    {
        _tables = tables;
    }

    /// <summary>
    /// 检查并触发叙事文本
    /// </summary>
    /// <param name="condition">条件类型</param>
    /// <param name="actorId">行为发起者ID</param>
    /// <param name="targetId">目标ID（可选）</param>
    /// <param name="actorName">行为发起者名称</param>
    /// <param name="targetName">目标名称（可选）</param>
    /// <param name="thresholdValue">条件阈值（如HP百分比）</param>
    /// <returns>触发的叙事文本，未触发返回null</returns>
    public string? TryTrigger(string condition, long actorId, long targetId, string actorName, string targetName, double thresholdValue)
    {
        var candidates = _tables.GetNarrationsByCondition(condition);
        if (candidates.Count == 0) return null;

        long nowMs = Environment.TickCount64;

        foreach (var row in candidates)
        {
            // 检查阈值条件
            if (!CheckThreshold(condition, row.Threshold, thresholdValue))
                continue;

            // 检查冷却
            if (row.Cooldown > 0)
            {
                var entityId = condition == "damage_taken" || condition == "kill" || condition == "combat_start" ? actorId : targetId;
                if (!_cooldowns.TryGetValue(entityId, out var entityCds))
                {
                    entityCds = new Dictionary<string, long>();
                    _cooldowns[entityId] = entityCds;
                }

                string cdKey = $"{condition}_{row.Id}";
                if (entityCds.TryGetValue(cdKey, out var lastTime))
                {
                    double elapsedSec = (nowMs - lastTime) / 1000.0;
                    if (elapsedSec < row.Cooldown) continue;
                }
                entityCds[cdKey] = nowMs;
            }

            // 概率检查
            if (_rng.NextDouble() > row.Probability) continue;

            // 替换占位符
            return row.Text
                .Replace("{actor}", actorName)
                .Replace("{target}", targetName);
        }

        return null;
    }

    /// <summary>实体离开时清理冷却</summary>
    public void OnEntityRemoved(long entityId)
    {
        _cooldowns.Remove(entityId);
    }

    private static bool CheckThreshold(string condition, double configThreshold, double actualValue)
    {
        return condition switch
        {
            "hp_below_pct" => actualValue <= configThreshold,   // HP% <= 阈值
            "hp_above_pct" => actualValue >= configThreshold,   // HP% >= 阈值
            "mp_below_pct" => actualValue <= configThreshold,
            "damage_taken" => configThreshold <= 0 || actualValue >= configThreshold,  // threshold=0 表示任意伤害
            "kill" => true,
            "combat_start" => true,
            "combat_end" => true,
            "first_hit" => true,
            _ => true,
        };
    }
}
