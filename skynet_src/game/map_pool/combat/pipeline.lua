-- 技能管线：6 阶段技能释放流程
-- 运行在 map_pool 服务内部

local skynet = require "skynet"
local common = require "common"
local ActionRegistry = require "game.map_pool.combat.actions.init"

local SkillPipeline = {}

--------------------------------------------------------------------------------
-- 1. 技能配置存根（后续替换为 common.queryTable("TbSkill")）
--------------------------------------------------------------------------------
local skillConfigs = {
    [1] = {
        id = 1,
        name = "普通攻击",
        cast_range = 1,
        cast_time = 0,
        interrupt_on_move = false,
        post_cast_time = 0.1,
        cooldown = 0,
        mp_cost = 0,
        target_type = "SingleEnemy",
        actions = {
            { type = "DealDamage", damageType = "physical", coefficient = 1.0 },
        },
    },
}

local function getSkillConfig(skillId)
    -- TODO: 接入配置表 common.queryTable("TbSkill")
    return skillConfigs[skillId]
end

--------------------------------------------------------------------------------
-- 2. 前置条件检查 (Pre-Check)
--------------------------------------------------------------------------------
function SkillPipeline:preCheck(skillId, casterId, ctx)
    local cfg = getSkillConfig(skillId)
    if not cfg then
        return false, "skill_not_found"
    end
    
    -- CD 检查
    if ctx.skillCooldowns[skillId] and skynet.now() < ctx.skillCooldowns[skillId] then
        return false, "cooldown"
    end
    
    -- MP 检查
    -- TODO: 接入真实 MP 查询
    local mp = 100
    if mp < cfg.mp_cost then
        return false, "not_enough_mp"
    end
    
    -- TODO: 状态检查（沉默/眩晕）
    
    return true
end

--------------------------------------------------------------------------------
-- 3. 目标选择 (Target Selection)
--------------------------------------------------------------------------------
function SkillPipeline:selectTargets(skillId, casterId, ctx, maps)
    local cfg = getSkillConfig(skillId)
    if not cfg then return nil end
    
    -- 找到施法者所在地图和坐标
    local casterMapName, casterPos = self:findEntityPosition(casterId, maps)
    if not casterMapName or not casterPos then
        return nil
    end
    
    local map = maps[casterMapName]
    if not map then return nil end
    
    if cfg.target_type == "Self" then
        return { casterId }
    end
    
    -- 辅助：计算曼哈顿距离
    local function dist(a, b)
        return math.abs(a.x - b.x) + math.abs(a.y - b.y)
    end
    
    if cfg.target_type == "SingleEnemy" then
        local candidates = {}
        -- 从战斗关系中找出敌人
        for relationId, _ in pairs(ctx.relationIds) do
            local rel = self.combatManager.relations[relationId]
            if rel and rel.isActive and rel.attackerId == casterId then
                local targetId = rel.targetId
                local _, targetPos = self:findEntityPosition(targetId, maps)
                if targetPos then
                    local d = dist(casterPos, targetPos)
                    if d <= cfg.cast_range then
                        table.insert(candidates, { id = targetId, dist = d })
                    end
                end
            end
        end
        table.sort(candidates, function(a, b) return a.dist < b.dist end)
        if #candidates > 0 then
            return { candidates[1].id }
        end
        return nil
    end
    
    if cfg.target_type == "AllEnemiesInRange" then
        local targets = {}
        for relationId, _ in pairs(ctx.relationIds) do
            local rel = self.combatManager.relations[relationId]
            if rel and rel.isActive and rel.attackerId == casterId then
                local targetId = rel.targetId
                local _, targetPos = self:findEntityPosition(targetId, maps)
                if targetPos then
                    local d = dist(casterPos, targetPos)
                    if d <= cfg.cast_range then
                        table.insert(targets, targetId)
                    end
                end
            end
        end
        return #targets > 0 and targets or nil
    end
    
    return nil
end

-- 在 maps 中查找 entity 的坐标
function SkillPipeline:findEntityPosition(entityId, maps)
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

-- 检查目标是否存活（简化版）
function SkillPipeline:isAlive(entityId)
    -- TODO: 接入真实 HP 查询
    local ctx = self.combatManager.contexts[entityId]
    if ctx then
        -- 简化：只要 context 存在就算存活，死亡时会清除 context
        return true
    end
    -- 如果玩家在 map.players 里但 context 没了，说明还没参战
    return true
end

--------------------------------------------------------------------------------
-- 4. 终局验证 (Final Validation)
--------------------------------------------------------------------------------
function SkillPipeline:finalValidation(skillId, casterId, targets, maps)
    local cfg = getSkillConfig(skillId)
    if not cfg then return false end
    
    local casterMapName, casterPos = self:findEntityPosition(casterId, maps)
    if not casterPos then return false end
    
    local function dist(a, b)
        return math.abs(a.x - b.x) + math.abs(a.y - b.y)
    end
    
    local validTargets = {}
    for _, targetId in ipairs(targets) do
        if self:isAlive(targetId) then
            local _, targetPos = self:findEntityPosition(targetId, maps)
            if targetPos and dist(casterPos, targetPos) <= cfg.cast_range then
                table.insert(validTargets, targetId)
            end
        end
    end
    
    -- AOE/范围类：如果原目标全失效，尝试重选
    if #validTargets == 0 and (cfg.target_type == "SingleEnemy" or cfg.target_type == "AllEnemiesInRange") then
        local fallback = self:selectTargets(skillId, casterId, self.combatManager.contexts[casterId], maps)
        if fallback and #fallback > 0 then
            -- 写回 targets 数组
            for i, v in ipairs(fallback) do targets[i] = v end
            for i = #fallback + 1, #targets do targets[i] = nil end
            return true
        end
    end
    
    return #validTargets > 0
end

--------------------------------------------------------------------------------
-- 5. 开始施法 / 结束施法
--------------------------------------------------------------------------------
function SkillPipeline:startCast(casterId, skillId)
    local ctx = self.combatManager.contexts[casterId]
    local cfg = getSkillConfig(skillId)
    if not ctx or not cfg then return end
    
    -- 扣 MP（简化版，TODO 接入真实 MP）
    -- consumeMP(casterId, cfg.mp_cost)
    
    ctx.subState = "CASTING"
    ctx.castSkillId = skillId
    ctx.castEndTime = skynet.now() + math.floor(cfg.cast_time * 100)
end

function SkillPipeline:endCast(casterId, skillId, isMiss)
    local ctx = self.combatManager.contexts[casterId]
    local cfg = getSkillConfig(skillId)
    if not ctx or not cfg then return end
    
    -- 进 CD（MISS 也进完整 CD）
    ctx.skillCooldowns[skillId] = skynet.now() + math.floor(cfg.cooldown * 100)
    
    ctx.subState = "POST_CAST"
    ctx.castSkillId = nil
    ctx.castEndTime = nil
    ctx.postCastEndTime = skynet.now() + math.floor(cfg.post_cast_time * 100)
    
    -- TODO: 广播施法结果
    -- broadcastCastEnd(casterId, skillId, isMiss)
end

function SkillPipeline:clearCast(casterId)
    local ctx = self.combatManager.contexts[casterId]
    if not ctx then return end
    ctx.subState = "NONE"
    ctx.castSkillId = nil
    ctx.castEndTime = nil
end

--------------------------------------------------------------------------------
-- 6. 执行 Action 序列
--------------------------------------------------------------------------------
function SkillPipeline:executeActions(skillId, casterId, targets, maps)
    local cfg = getSkillConfig(skillId)
    if not cfg then return end
    
    local context = {
        skillId = skillId,
        skillLevel = 1,
        combatManager = self.combatManager,
        maps = maps,
    }
    
    for _, actionCfg in ipairs(cfg.actions) do
        local handler = ActionRegistry:get(actionCfg.type)
        if handler then
            context.actionParams = actionCfg
            local ok, result = pcall(handler.execute, handler, casterId, targets, context)
            if not ok then
                skynet.error("[SkillPipeline] action error: " .. tostring(result))
            end
        else
            skynet.error("[SkillPipeline] unknown action: " .. tostring(actionCfg.type))
        end
    end
end

--------------------------------------------------------------------------------
-- 7. 对外主接口
--------------------------------------------------------------------------------
function SkillPipeline:cast(skillId, casterId, maps)
    local ctx = self.combatManager.contexts[casterId]
    if not ctx then
        return "FAILURE"
    end
    
    -- 阶段 1: Pre-Check
    local ok, err = self:preCheck(skillId, casterId, ctx)
    if not ok then
        return "FAILURE"
    end
    
    -- 阶段 2: Target Selection
    local targets = self:selectTargets(skillId, casterId, ctx, maps)
    if not targets or #targets == 0 then
        return "FAILURE"
    end
    
    -- 阶段 3: Cast Start
    local cfg = getSkillConfig(skillId)
    local castTime = cfg and cfg.cast_time or 0
    self:startCast(casterId, skillId)
    
    -- 读条等待
    if castTime > 0 then
        -- 由调用方（CombatManager.tick）在后续 tick 中继续处理
        -- 这里返回 PENDING，表示进入读条状态
        return "PENDING"
    end
    
    -- 阶段 4: Final Validation
    local ok2 = self:finalValidation(skillId, casterId, targets, maps)
    if not ok2 then
        self:endCast(casterId, skillId, true)
        return "MISS"
    end
    
    -- 阶段 5: Main Execution
    self:executeActions(skillId, casterId, targets, maps)
    
    -- 阶段 6: Cast End
    self:endCast(casterId, skillId, false)
    return "SUCCESS"
end

-- 读条完成后的续接处理（由 CombatManager 在每个 tick 中调用）
function SkillPipeline:resumeCast(casterId, maps)
    local ctx = self.combatManager.contexts[casterId]
    if not ctx or ctx.subState ~= "CASTING" then
        return "FAILURE"
    end
    
    local skillId = ctx.castSkillId
    if not skillId then
        self:clearCast(casterId)
        return "FAILURE"
    end
    
    -- 检查读条时间是否已到
    if skynet.now() < ctx.castEndTime then
        return "PENDING"
    end
    
    -- 重新做目标选择（用于 Final Validation 时的目标重选逻辑）
    local targets = self:selectTargets(skillId, casterId, ctx, maps)
    if not targets or #targets == 0 then
        self:endCast(casterId, skillId, true)
        return "MISS"
    end
    
    -- 阶段 4: Final Validation
    local ok2 = self:finalValidation(skillId, casterId, targets, maps)
    if not ok2 then
        self:endCast(casterId, skillId, true)
        return "MISS"
    end
    
    -- 阶段 5: Main Execution
    self:executeActions(skillId, casterId, targets, maps)
    
    -- 阶段 6: Cast End
    self:endCast(casterId, skillId, false)
    return "SUCCESS"
end

return SkillPipeline
