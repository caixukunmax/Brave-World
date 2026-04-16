-- 怪物池服务：单张地图的怪物 AI 与状态管理
-- 当前为简化版，一个服务管理所有地图的怪物（后续可按 map_id 分片）

local skynet = require "skynet"
local common = require "common"
local platform = common.platform
local logic = require "game.monster_pool.logic"

local monsters = {}     -- instanceId -> state
local mapId = 1         -- 默认新手村

local handlers = {}

-- 查询格子是否被怪物占据（供 player_pool 调用）
function handlers.isOccupied(x, y)
    return logic.isOccupied(monsters, x, y)
end

function handlers.getMonsters()
    return monsters
end

-- 战斗伤害回调（由 map_pool/combat 调用）
function handlers.onCombatDamage(instanceId, attackerId, damage)
    local m = monsters[instanceId]
    if not m then return end
    m.hp = (m.hp or 100) - damage
    skynet.error(string.format("[monster_pool] monster damaged: id=%d dmg=%d hp=%d", instanceId, damage, m.hp))
    if m.hp <= 0 then
        m.hp = 0
        skynet.error(string.format("[monster_pool] monster died: id=%d", instanceId))
        -- TODO: 死亡处理（掉落、经验分配、移除等）
    end
end

-- 战斗回血回调（脱战后由 map_pool/combat 调用）
function handlers.onCombatRegen(instanceId, regen)
    local m = monsters[instanceId]
    if not m then return end
    m.hp = math.min(m.maxHp or 100, (m.hp or 100) + regen)
end

common.defineService("game/monster_pool", handlers, {
    init = function()
        skynet.error("[monster_pool] initializing...")

        -- 加载默认地图（新手村）怪物
        local firstMap = common.getFirstMap()
        mapId = common.getMapIdByName(firstMap.map_name)
        monsters = logic.initMonsters(mapId)
        local count = 0
        for _ in pairs(monsters) do count = count + 1 end
        skynet.error(string.format("[monster_pool] map=%s id=%d monsters=%d", firstMap.map_name, mapId, count))

        -- 打印第一个怪物状态用于诊断
        for id, m in pairs(monsters) do
            skynet.error(string.format("[monster_pool] monster init: id=%d pos=(%d,%d) ai=%s ai_id=%d",
                id, m.x, m.y, m.aiType, m.aiId))
            break
        end

        -- 同步怪物到 map_pool
        local mapName = firstMap.map_name
        for instanceId, m in pairs(monsters) do
            platform.serviceSend(common.getMapPoolName(mapId), "monsterEnter", {
                instance_id = instanceId,
                monster_id  = m.monsterId,
                map_name    = mapName,
                x           = m.x,
                y           = m.y,
                hp          = m.hp or 100,
                max_hp      = m.maxHp or 100,
                level       = 1,
            })
        end

        -- 启动 AI tick 循环（每 500ms）
        skynet.fork(function()
            while true do
                skynet.sleep(50) -- 50 * 10ms = 500ms
                local ok, err = pcall(logic.tick, monsters, mapId)
                if not ok then
                    skynet.error("[monster_pool] tick error: " .. tostring(err))
                end
            end
        end)

        skynet.error("[monster_pool] tick loop started")
    end,
})
