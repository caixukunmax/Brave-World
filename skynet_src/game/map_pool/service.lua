-- 地图池服务：聚合单张地图上的玩家快照，供怪物 AI 查询和广播使用
-- 当前为简化版，一个服务管理所有地图（后续可按 map_id 分片为多个实例）

local skynet = require "skynet"
require "skynet.manager"
local common = require "common"
local platform = common.platform
local MessageId = common.MessageId

local pool_id = 0
local arg = ...
if arg ~= nil then
    pool_id = tonumber(arg) or 0
end

local CombatManager = require "game.map_pool.combat.manager"

local maps = {} -- mapName -> { map_id, players={}, monsters={} }

local handlers = {}

-- 初始化所有地图配置
local function initMaps()
    local registry = common.getMapRegistry()
    if registry then
        for mapId, cfg in pairs(registry) do
            maps[cfg.map_name] = {
                map_id = mapId,
                players = {},
                monsters = {},
            }
        end
    end
    if not maps["xinshoucun"] then
        maps["xinshoucun"] = { map_id = 1, players = {}, monsters = {} }
    end
end

-- 玩家进入地图（enterGame / createRole 后调用）
function handlers.playerEnter(snapshot)
    local mapName = snapshot.current_map or "xinshoucun"
    local map = maps[mapName]
    if not map then
        map = { map_id = common.getMapIdByName(mapName), players = {}, monsters = {} }
        maps[mapName] = map
    end
    local x = snapshot.grid_x or 0
    local y = snapshot.grid_y or 0
    map.players[snapshot.account_id] = {
        account_id = snapshot.account_id,
        role_id    = snapshot.role_id,
        role_name  = snapshot.role_name,
        server_id  = snapshot.server_id,
        grid_x     = x,
        grid_y     = y,
        level      = snapshot.level or 1,
        hp         = snapshot.hp or 100,
        max_hp     = snapshot.max_hp or 100,
        mp         = snapshot.mp or 50,
        max_mp     = snapshot.max_mp or 50,
        agility    = snapshot.agility or 100,
        patk       = snapshot.patk or 10,
        matk       = snapshot.matk or 10,
        pdef       = snapshot.pdef or 5,
        mdef       = snapshot.mdef or 5,
    }
    skynet.error(string.format("[map_pool_%d] playerEnter: account=%d map=%s pos=(%d,%d)",
        pool_id, snapshot.account_id, mapName, x, y))
end

-- 碰撞检测辅助：怪物移动后与玩家相邻时触发战斗
local function checkCollisionMonsterVsPlayer(instanceId, mapName, x, y)
    local map = maps[mapName]
    if not map then return end
    for accountId, p in pairs(map.players or {}) do
        local dist = math.abs(p.grid_x - x) + math.abs(p.grid_y - y)
        if dist == 1 then
            CombatManager:onCollision(instanceId, accountId, maps)
        end
    end
end

-- 玩家移动后更新坐标
function handlers.playerMove(accountId, mapName, x, y)
    local map = maps[mapName]
    if map and map.players[accountId] then
        map.players[accountId].grid_x = x
        map.players[accountId].grid_y = y
    end
end

-- 玩家离开地图（下线或切图）
function handlers.playerLeave(accountId, mapName)
    local map = maps[mapName]
    if map then
        map.players[accountId] = nil
    end
    -- 通知 combat 模块清除该玩家的战斗关系
    CombatManager:onEntityRemoved(accountId)
    skynet.error(string.format("[map_pool_%d] playerLeave: account=%d map=%s", pool_id, accountId, mapName))
end

-- 供 monster_pool 查询同地图玩家
function handlers.getPlayersOnMap(mapName)
    local map = maps[mapName]
    return map and map.players or {}
end

-- 怪物进入地图（由 monster_pool 同步）
function handlers.monsterEnter(snapshot)
    local mapName = snapshot.map_name or "xinshoucun"
    local map = maps[mapName]
    if not map then
        map = { map_id = common.getMapIdByName(mapName), players = {}, monsters = {} }
        maps[mapName] = map
    end
    map.monsters[snapshot.instance_id] = {
        instance_id = snapshot.instance_id,
        monster_id  = snapshot.monster_id,
        x           = snapshot.x or 0,
        y           = snapshot.y or 0,
        hp          = snapshot.hp or 100,
        max_hp      = snapshot.max_hp or 100,
        level       = snapshot.level or 1,
    }
    skynet.error(string.format("[map_pool_%d] monsterEnter: instance=%d map=%s pos=(%d,%d)",
        pool_id, snapshot.instance_id, mapName, snapshot.x or 0, snapshot.y or 0))
end

-- 玩家主动攻击：移动被怪物阻挡后转为进攻
function handlers.playerAttack(accountId, mapName, x, y)
    local map = maps[mapName]
    if not map then return end
    for instanceId, m in pairs(map.monsters or {}) do
        if m.x == x and m.y == y then
            CombatManager:onCollision(accountId, instanceId, maps)
            break
        end
    end
end

-- 怪物移动后更新坐标
function handlers.monsterMove(instanceId, x, y)
    for mapName, map in pairs(maps) do
        map.monsters = map.monsters or {}
        if map.monsters[instanceId] then
            map.monsters[instanceId].x = x
            map.monsters[instanceId].y = y
            checkCollisionMonsterVsPlayer(instanceId, mapName, x, y)
            break
        end
    end
end

-- 怪物离开地图
function handlers.monsterLeave(instanceId)
    for mapName, map in pairs(maps) do
        map.monsters = map.monsters or {}
        if map.monsters[instanceId] then
            map.monsters[instanceId] = nil
            CombatManager:onEntityRemoved(instanceId)
            skynet.error(string.format("[map_pool_%d] monsterLeave: instance=%d map=%s", pool_id, instanceId, mapName))
            break
        end
    end
end

-- 查询格子是否被怪物占据（供 player_pool / AI 路径规划使用）
function handlers.isOccupied(x, y)
    for mapName, map in pairs(maps) do
        for instanceId, m in pairs(map.monsters or {}) do
            if m.x == x and m.y == y then
                return true
            end
        end
    end
    return false
end

-- 向地图内所有在线玩家广播 Gateway 消息
function handlers.broadcastToMap(mapName, msgId, data)
    local map = maps[mapName]
    if not map then return end
    for accountId, p in pairs(map.players) do
        platform.serviceSend("game/gateway", "sendToAccount", accountId, p.server_id, msgId, data)
    end
end

common.defineService("game/map_pool_" .. pool_id, handlers, {
    init = function()
        skynet.name(".map_pool_" .. pool_id, skynet.self())
        initMaps()
        local count = 0
        for _ in pairs(maps) do count = count + 1 end
        skynet.error(string.format("[map_pool_%d] initialized with %d maps", pool_id, count))
        
        -- 初始化战斗管理器
        CombatManager:init()
        skynet.error(string.format("[map_pool_%d] combat manager initialized", pool_id))
        
        -- 启动战斗 tick（每 100ms = 10 tick/秒）
        skynet.fork(function()
            while true do
                skynet.sleep(10) -- 10 * 10ms = 100ms
                local ok, err = pcall(CombatManager.tick, CombatManager, 0.1, maps)
                if not ok then
                    skynet.error("[map_pool_" .. pool_id .. "] combat tick error: " .. tostring(err))
                end
            end
        end)
    end,
})
