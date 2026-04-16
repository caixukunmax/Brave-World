-- 造成伤害 Action
local skynet = require "skynet"
local common = require "common"

local DealDamage = {}

--- 伤害公式（第一版简化）
local function calcDamage(casterId, targetId, params)
    local damageType = params.damageType or "physical"
    local coefficient = params.coefficient or 1.0
    
    -- 这里后续接入完整的属性系统
    -- 简化版：固定伤害 * 系数
    local baseDamage = 10
    if casterId >= 1000000 then
        -- 怪物，读取怪物配置
        -- TODO: 接入怪物属性表
        baseDamage = 5
    else
        -- 玩家，读取玩家属性
        -- TODO: 接入玩家属性表
        baseDamage = 10
    end
    
    local damage = math.floor(baseDamage * coefficient)
    return damage, damageType
end

function DealDamage:execute(casterId, targets, context)
    local params = context.actionParams or {}
    local results = {}
    
    for _, target in ipairs(targets) do
        local targetId = target
        -- 如果 target 是坐标 table（AOE 定点），需要扫描范围内目标
        if type(target) == "table" and target.x and target.y then
            -- AOE 扫描由上层或 SpawnArea/SpawnProjectile 处理，这里跳过
            skynet.error("[DealDamage] AOE point target should be resolved by SpawnArea/Projectile")
            goto continue
        end
        
        local damage, damageType = calcDamage(casterId, targetId, params)
        
        -- 通知 CombatManager 应用伤害（由 manager 统一处理血量扣减和死亡判定）
        if context.combatManager then
            context.combatManager:applyDamage(casterId, targetId, damage, damageType)
        else
            skynet.error("[DealDamage] combatManager not found in context")
        end
        
        results[#results + 1] = {
            target_id = targetId,
            damage = damage,
            damage_type = damageType,
        }
        
        ::continue::
    end
    
    return { success = true, results = results }
end

return DealDamage
