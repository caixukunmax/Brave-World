-- 战斗管理器（CombatManager）
-- 运行在 map_pool 服务内部，负责 ATB、脱战、碰撞、技能管线调度

local skynet = require "skynet"
local common = require "common"
local platform = common.platform
local SkillPipeline = require "game.map_pool.combat.pipeline"

local CombatManager = {
    relations = {},      -- [relationId] = CombatRelation
    contexts = {},       -- [entityId]   = CombatContext
    nextRelationId = 1,
}

-- 常量配置（TODO: 后续可提取到全局配置表）
local BASE_ATB_RATE = 10          -- 每秒 10 点
local ATB_BOOST_PER_MISS = 0.25   -- 每次打空 +25%
local ATB_BOOST_MAX = 0.50        -- 上限 50%
local ATB_BOOST_STACKS_MAX = 2

local DISENGAGE_DISTANCE_A = 15
local DISENGAGE_NO_DAMAGE_TIME_T1 = 3
local DISENGAGE_TIME_S1 = 5
local DISENGAGE_DISTANCE_B = 30
local DISENGAGE_TIME_S2 = 3

local MONSTER_REGEN_PERCENT_PER_SEC = 0.05

--------------------------------------------------------------------------------
-- 内部辅助
--------------------------------------------------------------------------------
local function distance(posA, posB)
    if not posA or not posB then return math.huge end
    return math.abs(posA.x - posB.x) + math.abs(posA.y - posB.y)
end

-- 在 maps 中查找 entity 坐标
local function findEntityPosition(entityId, maps)
    for mapName, map in pairs(maps) do
        if map.players then
            for accountId, p in pairs(map.players) do
                if accountId == entityId then
                    return mapName, { x = p.grid_x, y = p.grid_y }
                end
            end
        end
        if map.monsters then
            for instanceId, m in pairs(map.monsters) do
                if instanceId == entityId then
                    return mapName, { x = m.x, y = m.y }
                end
            end
        end
    end
    return nil, nil
end

-- 迅捷系数（简化版）
local function getAgilityCoefficient(entityId)
    -- TODO: 接入真实属性查询
    return 1.0
end

--------------------------------------------------------------------------------
-- 战斗上下文管理
--------------------------------------------------------------------------------
function CombatManager:getOrCreateContext(entityId)
    if not self.contexts[entityId] then
        self.contexts[entityId] = {
            entityId = entityId,
            state = "IDLE",
            subState = "NONE",
            atbValue = 0,
            atbBoost = 0,
            atbBoostStacks = 0,
            castSkillId = nil,
            castEndTime = nil,
            postCastEndTime = nil,
            relationIds = {},
            skillCooldowns = {},
        }
    end
    return self.contexts[entityId]
end

function CombatManager:setState(entityId, state)
    local ctx = self.contexts[entityId]
    if not ctx then return end
    ctx.state = state
    if state == "IDLE" then
        ctx.subState = "NONE"
        ctx.atbValue = 0
        ctx.castSkillId = nil
        ctx.castEndTime = nil
        ctx.postCastEndTime = nil
    end
end

--------------------------------------------------------------------------------
-- 战斗关系管理
--------------------------------------------------------------------------------
function CombatManager:createRelation(attackerId, targetId)
    -- 检查是否已存在
    for relationId, rel in pairs(self.relations) do
        if rel.attackerId == attackerId and rel.targetId == targetId and rel.isActive then
            return relationId
        end
    end
    
    local relationId = self.nextRelationId
    self.nextRelationId = self.nextRelationId + 1
    
    local now = skynet.time()
    local rel = {
        relationId = relationId,
        attackerId = attackerId,
        targetId = targetId,
        startTime = now,
        lastDamageTime = now,
        lastDistance = 0,
        isActive = true,
        disengageTimer1 = 0,
        disengageTimer2 = 0,
    }
    
    self.relations[relationId] = rel
    
    local ctxA = self:getOrCreateContext(attackerId)
    local ctxB = self:getOrCreateContext(targetId)
    ctxA.relationIds[relationId] = true
    ctxB.relationIds[relationId] = true
    
    return relationId
end

function CombatManager:removeRelation(relationId)
    local rel = self.relations[relationId]
    if not rel or not rel.isActive then return end
    rel.isActive = false
    
    local ctxA = self.contexts[rel.attackerId]
    local ctxB = self.contexts[rel.targetId]
    if ctxA then ctxA.relationIds[relationId] = nil end
    if ctxB then ctxB.relationIds[relationId] = nil end
    
    -- 检查是否回退到 IDLE
    if ctxA and next(ctxA.relationIds) == nil then
        self:setState(rel.attackerId, "IDLE")
    end
    if ctxB and next(ctxB.relationIds) == nil then
        self:setState(rel.targetId, "IDLE")
    end
end

function CombatManager:onEntityRemoved(entityId)
    local ctx = self.contexts[entityId]
    if not ctx then return end
    
    -- 清除该 entity 作为 attacker 或 target 的所有关系
    local toRemove = {}
    for relationId, _ in pairs(ctx.relationIds) do
        table.insert(toRemove, relationId)
    end
    for _, relationId in ipairs(toRemove) do
        self:removeRelation(relationId)
    end
    
    self.contexts[entityId] = nil
end

--------------------------------------------------------------------------------
-- 碰撞与先手攻击
--------------------------------------------------------------------------------
function CombatManager:onCollision(entityA, entityB, maps)
    -- TODO: 后续接入阵营/安全区判定
    local canCombat = true
    if not canCombat then return end
    
    -- 建立双向战斗关系
    self:createRelation(entityA, entityB)
    self:createRelation(entityB, entityA)
    
    local ctxA = self.contexts[entityA]
    local ctxB = self.contexts[entityB]
    
    -- 若至少一方原先是 IDLE，先手方执行简化普攻
    if (not ctxA or ctxA.state == "IDLE") or (not ctxB or ctxB.state == "IDLE") then
        -- 简化：双方互相先手一次（后续可改为只有主动碰撞方先手）
        self:executeFirstStrike(entityA, entityB, maps)
        self:executeFirstStrike(entityB, entityA, maps)
    end
    
    self:setState(entityA, "COMBAT")
    self:setState(entityB, "COMBAT")
end

function CombatManager:executeFirstStrike(attackerId, targetId, maps)
    local _, attackerPos = findEntityPosition(attackerId, maps)
    local _, targetPos = findEntityPosition(targetId, maps)
    if not attackerPos or not targetPos then return end
    if distance(attackerPos, targetPos) > 1 then return end
    
    -- 简化普攻：直接应用伤害
    self:applyDamage(attackerId, targetId, 5, "physical")
    
    -- 更新 lastDamageTime
    for relationId, rel in pairs(self.relations) do
        if rel.isActive and ((rel.attackerId == attackerId and rel.targetId == targetId) or
                             (rel.attackerId == targetId and rel.targetId == attackerId)) then
            rel.lastDamageTime = skynet.time()
        end
    end
end

--------------------------------------------------------------------------------
-- 伤害与死亡
--------------------------------------------------------------------------------
function CombatManager:applyDamage(attackerId, targetId, damage, damageType)
    -- 这里只记录/通知，真实血量由 player_pool / monster_pool 维护
    -- TODO: 接入真实属性系统后，可以在 map_pool 本地维护一份战斗用 HP 快照
    
    skynet.error(string.format("[Combat] damage: attacker=%d target=%d dmg=%d type=%s",
        attackerId, targetId, damage, damageType or "physical"))
    
    -- 通知 player_pool / monster_pool
    if targetId < 1000000 then
        -- 玩家
        -- platform.serviceSend("game/player_pool_" .. (targetId % 4), "onCombatDamage", targetId, attackerId, damage)
    else
        -- 怪物
        platform.serviceSend("game/monster_pool", "onCombatDamage", targetId, attackerId, damage)
    end
    
    -- 更新关系 lastDamageTime
    for relationId, rel in pairs(self.relations) do
        if rel.isActive and rel.attackerId == attackerId and rel.targetId == targetId then
            rel.lastDamageTime = skynet.time()
        end
    end
end

function CombatManager:onDeath(entityId)
    -- 清除所有关系
    local ctx = self.contexts[entityId]
    if ctx then
        local toRemove = {}
        for relationId, _ in pairs(ctx.relationIds) do
            table.insert(toRemove, relationId)
        end
        for _, relationId in ipairs(toRemove) do
            self:removeRelation(relationId)
        end
    end
    
    -- 广播死亡
    skynet.error(string.format("[Combat] death: entity=%d", entityId))
end

--------------------------------------------------------------------------------
-- ATB Tick
--------------------------------------------------------------------------------
function CombatManager:tickATB(dt, maps)
    for entityId, ctx in pairs(self.contexts) do
        if ctx.state == "COMBAT" and ctx.subState ~= "CASTING" then
            -- 检查后摇是否结束
            if ctx.subState == "POST_CAST" and ctx.postCastEndTime and skynet.now() < ctx.postCastEndTime then
                -- 后摇中，ATB 继续积累
            elseif ctx.subState == "POST_CAST" then
                ctx.subState = "NONE"
                ctx.postCastEndTime = nil
            end
            
            local agilityCoef = getAgilityCoefficient(entityId)
            local delta = BASE_ATB_RATE * agilityCoef * (1 + ctx.atbBoost) * dt
            ctx.atbValue = math.min(100, ctx.atbValue + delta)
            
            if ctx.atbValue >= 100 then
                if ctx.subState == "POST_CAST" and ctx.postCastEndTime and skynet.now() < ctx.postCastEndTime then
                    ctx.atbValue = 100
                else
                    ctx.atbValue = 0
                    ctx.atbBoost = 0
                    ctx.atbBoostStacks = 0
                    self:requestCast(entityId, maps)
                end
            end
        elseif ctx.state == "COMBAT" and ctx.subState == "CASTING" then
            -- 处理读条续接
            local result = SkillPipeline:resumeCast(entityId, maps)
            if result == "MISS" then
                ctx.atbBoost = math.min(ctx.atbBoost + ATB_BOOST_PER_MISS, ATB_BOOST_MAX)
                ctx.atbBoostStacks = math.min(ctx.atbBoostStacks + 1, ATB_BOOST_STACKS_MAX)
            elseif result == "SUCCESS" or result == "FAILURE" then
                -- 正常结束或失败，ATB 已在 pipeline 中处理
            elseif result == "PENDING" then
                -- 仍在读条中
            end
        end
    end
end

function CombatManager:requestCast(entityId, maps)
    local ctx = self.contexts[entityId]
    if not ctx then return end
    
    -- AI 选技能：简化版，始终尝试普攻（skillId = 1）
    -- TODO: 接入 AI 技能选择逻辑
    local skillId = 1
    
    local result = SkillPipeline:cast(skillId, entityId, maps)
    
    if result == "PENDING" then
        -- 进入读条，由 tickATB 续接
    elseif result == "MISS" then
        ctx.atbBoost = math.min(ctx.atbBoost + ATB_BOOST_PER_MISS, ATB_BOOST_MAX)
        ctx.atbBoostStacks = math.min(ctx.atbBoostStacks + 1, ATB_BOOST_STACKS_MAX)
    elseif result == "FAILURE" then
        -- 失败给予最小补偿
        ctx.atbBoost = 0.15
        ctx.atbBoostStacks = 1
    end
end

--------------------------------------------------------------------------------
-- 脱战 Tick
--------------------------------------------------------------------------------
function CombatManager:tickDisengage(dt, maps)
    local now = skynet.time()
    local toRemove = {}
    
    for relationId, rel in pairs(self.relations) do
        if not rel.isActive then goto continue end
        
        local _, posA = findEntityPosition(rel.attackerId, maps)
        local _, posB = findEntityPosition(rel.targetId, maps)
        local dist = distance(posA, posB)
        rel.lastDistance = dist
        
        -- 条件 2：彻底逃离
        if dist > DISENGAGE_DISTANCE_B then
            rel.disengageTimer2 = (rel.disengageTimer2 or 0) + dt
            if rel.disengageTimer2 >= DISENGAGE_TIME_S2 then
                table.insert(toRemove, relationId)
                goto continue
            end
        else
            rel.disengageTimer2 = 0
        end
        
        -- 条件 1：安全脱离
        if dist > DISENGAGE_DISTANCE_A then
            local noDamage = (now - (rel.lastDamageTime or 0)) >= DISENGAGE_NO_DAMAGE_TIME_T1
            if noDamage then
                rel.disengageTimer1 = (rel.disengageTimer1 or 0) + dt
                if rel.disengageTimer1 >= DISENGAGE_TIME_S1 then
                    table.insert(toRemove, relationId)
                    goto continue
                end
            else
                rel.disengageTimer1 = 0
            end
        else
            rel.disengageTimer1 = 0
        end
        
        ::continue::
    end
    
    for _, relationId in ipairs(toRemove) do
        self:removeRelation(relationId)
    end
end

--------------------------------------------------------------------------------
-- 怪物回血 Tick
--------------------------------------------------------------------------------
function CombatManager:tickMonsterRegen(dt, maps)
    for entityId, ctx in pairs(self.contexts) do
        if ctx.state == "IDLE" and entityId >= 1000000 then
            -- 怪物且不在战斗中
            -- 通知 monster_pool 回血
            local maxHp = 100 -- TODO: 接入真实 maxHp
            local regen = maxHp * MONSTER_REGEN_PERCENT_PER_SEC * dt
            platform.serviceSend("game/monster_pool", "onCombatRegen", entityId, regen)
        end
    end
end

--------------------------------------------------------------------------------
-- 主 Tick（由 map_pool 的定时器调用）
--------------------------------------------------------------------------------
function CombatManager:tick(dt, maps)
    self:tickDisengage(dt, maps)
    self:tickATB(dt, maps)
    self:tickMonsterRegen(dt, maps)
end

--------------------------------------------------------------------------------
-- 初始化
--------------------------------------------------------------------------------
function CombatManager:init()
    -- 绑定 SkillPipeline 的反向引用
    SkillPipeline.combatManager = self
end

return CombatManager
