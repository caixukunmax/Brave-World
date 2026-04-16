-- 巡逻+追击行为：有玩家靠近时追击，远离出生点时返回
local skynet = require "skynet"
local common = require "common"
local pathfind = require "game.ai.pathfind"

local patrol_chase = {}

-- 查找最近的可追击玩家
local function findNearestPlayer(m, players)
    local nearest = nil
    local minDist = m.aiConfig.aggro_range or 0
    if minDist <= 0 then
        return nil
    end

    for accountId, p in pairs(players) do
        local dist = pathfind.manhattan(m.x, m.y, p.grid_x, p.grid_y)
        if dist <= minDist then
            if not nearest or dist < minDist then
                nearest = p
                minDist = dist
            end
        end
    end
    return nearest
end

function patrol_chase.run(m, mapId, players)
    local now = skynet.now() * 10
    local mapName = common.getMapRegistry()[mapId] and common.getMapRegistry()[mapId].map_name or "xinshoucun"
    local cfg = m.aiConfig
    local maxChase = cfg.max_chase_distance or 8
    local spawnDist = pathfind.manhattan(m.x, m.y, m.spawnX, m.spawnY)

    -- 1. 脱战返回判定：如果已经远离出生点，强制返回
    if spawnDist > maxChase then
        local nx, ny = pathfind.bfsNextStep(m.x, m.y, m.spawnX, m.spawnY, mapName)
        if nx then
            m.state = "return"
            m.targetId = nil
            m.lastMoveTime = now
            return nx, ny
        end
        return nil
    end

    -- 2. 追击判定
    local target = findNearestPlayer(m, players)
    if target then
        local targetDist = pathfind.manhattan(m.x, m.y, target.grid_x, target.grid_y)
        -- 检查是否在追击冷却内
        if not m.lastMoveTime or (now - m.lastMoveTime) >= (cfg.chase_interval_ms or 500) then
            if targetDist > 0 then
                local nx, ny = pathfind.bfsNextStep(m.x, m.y, target.grid_x, target.grid_y, mapName)
                if nx then
                    m.state = "chase"
                    m.targetId = target.account_id or target.role_id
                    m.lastMoveTime = now
                    return nx, ny
                end
            end
        end
        -- 有目标但在冷却中，保持 idle
        m.state = "idle"
        return nil
    end

    -- 3. 若之前在追击/返回状态但已回到出生点附近，切回 idle
    if m.state == "chase" or m.state == "return" then
        if spawnDist <= (cfg.patrol_range or 2) then
            m.state = "idle"
            m.targetId = nil
        else
            -- 继续返回出生点
            local nx, ny = pathfind.bfsNextStep(m.x, m.y, m.spawnX, m.spawnY, mapName)
            if nx then
                m.state = "return"
                m.lastMoveTime = now
                return nx, ny
            end
        end
    end

    -- 4. 巡逻
    if not m.lastMoveTime or (now - m.lastMoveTime) >= (cfg.move_interval_ms or 2000) then
        local dirs = { {0, -1}, {0, 1}, {-1, 0}, {1, 0} }
        for i = #dirs, 2, -1 do
            local j = math.random(i)
            dirs[i], dirs[j] = dirs[j], dirs[i]
        end

        local range = cfg.patrol_range or 3
        for _, d in ipairs(dirs) do
            local nx, ny = m.x + d[1], m.y + d[2]
            if math.abs(nx - m.spawnX) <= range and math.abs(ny - m.spawnY) <= range then
                if common.isWalkable(mapName, nx, ny) then
                    m.state = "patrol"
                    m.lastMoveTime = now
                    return nx, ny
                end
            end
        end
    end

    return nil
end

return patrol_chase
