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

local maps = {} -- mapName -> { map_id, players={accountId -> snapshot} }

local handlers = {}

-- 初始化所有地图配置
local function initMaps()
    local registry = common.getMapRegistry()
    if registry then
        for mapId, cfg in pairs(registry) do
            maps[cfg.map_name] = {
                map_id = mapId,
                players = {},
            }
        end
    end
    if not maps["xinshoucun"] then
        maps["xinshoucun"] = { map_id = 1, players = {} }
    end
end

-- 玩家进入地图（enterGame / createRole 后调用）
function handlers.playerEnter(snapshot)
    local mapName = snapshot.current_map or "xinshoucun"
    local map = maps[mapName]
    if not map then
        map = { map_id = common.getMapIdByName(mapName), players = {} }
        maps[mapName] = map
    end
    map.players[snapshot.account_id] = {
        account_id = snapshot.account_id,
        role_id    = snapshot.role_id,
        role_name  = snapshot.role_name,
        server_id  = snapshot.server_id,
        grid_x     = snapshot.grid_x or 0,
        grid_y     = snapshot.grid_y or 0,
        level      = snapshot.level or 1,
    }
    skynet.error(string.format("[map_pool_%d] playerEnter: account=%d map=%s pos=(%d,%d)",
        pool_id, snapshot.account_id, mapName, snapshot.grid_x or 0, snapshot.grid_y or 0))
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
    skynet.error(string.format("[map_pool_%d] playerLeave: account=%d map=%s", pool_id, accountId, mapName))
end

-- 供 monster_pool 查询同地图玩家
function handlers.getPlayersOnMap(mapName)
    local map = maps[mapName]
    return map and map.players or {}
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
    end,
})
