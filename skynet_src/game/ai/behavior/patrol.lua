-- 巡逻行为：在出生点附近随机游走
local skynet = require "skynet"
local common = require "common"

local patrol = {}

function patrol.run(m, mapId, players)
    local now = skynet.now() * 10  -- skynet.now() 单位是 1/100 秒，转为毫秒
    if m.lastMoveTime and (now - m.lastMoveTime) < (m.aiConfig.move_interval_ms or 2000) then
        return nil
    end

    local mapName = common.getMapRegistry()[mapId] and common.getMapRegistry()[mapId].map_name or "xinshoucun"
    local dirs = { {0, -1}, {0, 1}, {-1, 0}, {1, 0} }

    -- 随机打乱方向
    for i = #dirs, 2, -1 do
        local j = math.random(i)
        dirs[i], dirs[j] = dirs[j], dirs[i]
    end

    local range = m.aiConfig.patrol_range or 3
    for _, d in ipairs(dirs) do
        local nx, ny = m.x + d[1], m.y + d[2]
        -- 必须在巡逻范围内且可行走
        if math.abs(nx - m.spawnX) <= range and math.abs(ny - m.spawnY) <= range then
            if common.isWalkable(mapName, nx, ny) then
                m.state = "patrol"
                m.lastMoveTime = now
                return nx, ny
            end
        end
    end

    return nil
end

return patrol
