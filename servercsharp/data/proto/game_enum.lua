--------------------------------------------------------------------------------
-- 枚举常量（由 build_proto 自动生成，请勿手动修改）
-- Source: game.proto
--------------------------------------------------------------------------------

local CombatLogType = {
    COMBAT_LOG_START = 0,  -- 战斗开始
    COMBAT_LOG_SKILL = 1,  -- 释放技能
    COMBAT_LOG_DAMAGE = 2,  -- 造成伤害
    COMBAT_LOG_HEAL = 3,  -- 治疗/回血
    COMBAT_LOG_BUFF = 4,  -- 获得 Buff/Debuff
    COMBAT_LOG_DODGE = 5,  -- 闪避/打空
    COMBAT_LOG_DEATH = 6,  -- 死亡
    COMBAT_LOG_END = 7,  -- 战斗结束/脱战
}

local CombatEndReason = {
    COMBAT_END_DISENGAGE = 0,  -- 脱战
    COMBAT_END_DEATH = 1,  -- 死亡
}

return {
    CombatLogType = CombatLogType,
    CombatEndReason = CombatEndReason,
}
