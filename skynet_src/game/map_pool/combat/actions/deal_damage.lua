-- 造成伤害 Action
local skynet = require "skynet"
local common = require "common"

local DealDamage = {}

-- 从 maps 中读取 entity 属性
local function getEntityAttr(entityId, attrName, maps)
    for mapName, map in pairs(maps) do
        if map.players and map.players[entityId] then
            return map.players[entityId][attrName]
        end
        if map.monsters and map.monsters[entityId] then
            return map.monsters[entityId][attrName]
        end
    end
    return nil
end

--- 伤害公式（第一版简化）
local function calcDamage(casterId, targetId, params, maps)
    local damageType = params.damageType or "physical"
    local coefficient = params.coefficient or 1.0
    
    local baseDamage = 10
    local targetDef = 0
    
    if damageType == "physical" then
        local patk = getEntityAttr(casterId, "patk", maps) or 10
        local pdef = getEntityAttr(targetId, "pdef", maps) or 5
        baseDamage = patk
        targetDef = pdef
    else
        local matk = getEntityAttr(casterId, "matk", maps) or 10
        local mdef = getEntityAttr(targetId, "mdef", maps) or 5
        baseDamage = matk
        targetDef = mdef
    end
    
    -- 简化减伤：防御减免 50% 伤害（后续可换成更复杂的公式）
    local damage = math.floor((baseDamage * coefficient) * (1 - targetDef * 0.01))
    if damage < 1 then damage = 1 end
    
    return damage, damageType
end

function DealDamage:execute(casterId, targets, context)
    local params = context.actionParams or {}
    local results = {}
    local maps = context.maps or {}
    
    for _, target in ipairs(targets) do
        local targetId = target
        -- 如果 target 是坐标 table（AOE 定点），需要扫描范围内目标
        if type(target) == "table" and target.x and target.y then
            -- AOE 扫描由上层或 SpawnArea/SpawnProjectile 处理，这里跳过
            skynet.error("[DealDamage] AOE point target should be resolved by SpawnArea/Projectile")
            goto continue
        end
        
        local damage, damageType = calcDamage(casterId, targetId, params, maps)
        
        -- 通知 CombatManager 应用伤害（由 manager 统一处理血量扣减和死亡判定）
        if context.combatManager then
            context.combatManager:applyDamage(casterId, targetId, damage, damageType, maps)
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
