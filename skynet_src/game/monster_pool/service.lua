-- 怪物池服务：单张地图的怪物 AI 与状态管理
-- 当前为简化版，一个服务管理所有地图的怪物（后续可按 map_id 分片）

local skynet = require "skynet"
local common = require "common"
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
