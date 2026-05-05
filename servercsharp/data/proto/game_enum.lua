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
    COMBAT_LOG_BUFF_APPLY = 8,  -- 施加 Buff
    COMBAT_LOG_BUFF_REMOVE = 9,  -- Buff 消失
    COMBAT_LOG_BUFF_TICK = 10,  -- Buff tick 效果（DOT 等）
    COMBAT_LOG_SHIELD_ABSORB = 11,  -- 护盾吸收
}

local EntityType = {
    ENTITY_TYPE_PLAYER = 0,
    ENTITY_TYPE_MONSTER = 1,
    ENTITY_TYPE_NPC = 2,
    ENTITY_TYPE_BOSS = 3,
    ENTITY_TYPE_PET = 4,
    ENTITY_TYPE_GUARD = 5,
    ENTITY_TYPE_GATHER_NODE = 6,
    ENTITY_TYPE_PORTAL = 7,
    ENTITY_TYPE_CHEST = 8,
}

local ComponentType = {
    COMPONENT_COMBAT = 0,
    COMPONENT_MOVE = 1,
    COMPONENT_CAST = 2,
    COMPONENT_AI = 3,
    COMPONENT_INTERACT = 4,
    COMPONENT_GATHER = 5,
}

local CombatEndReason = {
    COMBAT_END_DISENGAGE = 0,  -- 脱战
    COMBAT_END_DEATH = 1,  -- 死亡
}

return {
    CombatLogType = CombatLogType,
    EntityType = EntityType,
    ComponentType = ComponentType,
    CombatEndReason = CombatEndReason,
}
