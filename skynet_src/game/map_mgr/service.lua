-- 地图管理器：启动并管理多个 map_pool 实例
-- 分片策略：按 map_id % MAP_POOL_COUNT 路由

local skynet = require "skynet"
local common = require "common"

local handlers = {}

function handlers.getPoolCount()
    return common.MAP_POOL_COUNT
end

common.defineService("map_mgr", handlers, {
    init = function()
        for i = 0, common.MAP_POOL_COUNT - 1 do
            skynet.newservice("game/map_pool", i)
        end
        skynet.error("map_mgr started with " .. common.MAP_POOL_COUNT .. " pools")
    end,
})
