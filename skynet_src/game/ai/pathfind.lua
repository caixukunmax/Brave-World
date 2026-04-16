-- 轻量寻路模块（BFS）
local skynet = require "skynet"
local common = require "common"

local pathfind = {}

function pathfind.manhattan(ax, ay, bx, by)
    return math.abs(ax - bx) + math.abs(ay - by)
end

-- BFS 寻找从 (startX, startY) 到 (goalX, goalY) 的下一步方向
-- 返回: nextX, nextY（只返回第一步），若不可达返回 nil, nil
-- 参数: mapName 用于 isWalkable 检查
function pathfind.bfsNextStep(startX, startY, goalX, goalY, mapName)
    if startX == goalX and startY == goalY then
        return startX, startY
    end

    local visited = {}
    local queue = {}
    local cameFrom = {}

    local function key(x, y)
        return x .. "," .. y
    end

    visited[key(startX, startY)] = true
    queue[#queue + 1] = { x = startX, y = startY }

    local dirs = { {0, -1}, {0, 1}, {-1, 0}, {1, 0} }

    local head = 1
    while head <= #queue do
        local cur = queue[head]
        head = head + 1

        for _, d in ipairs(dirs) do
            local nx, ny = cur.x + d[1], cur.y + d[2]
            local k = key(nx, ny)
            if not visited[k] and common.isWalkable(mapName, nx, ny) then
                visited[k] = true
                cameFrom[k] = { x = cur.x, y = cur.y }
                queue[#queue + 1] = { x = nx, y = ny }

                if nx == goalX and ny == goalY then
                    -- 回溯找到第一步
                    local step = { x = nx, y = ny }
                    while true do
                        local prev = cameFrom[key(step.x, step.y)]
                        if not prev then
                            return step.x, step.y
                        end
                        if prev.x == startX and prev.y == startY then
                            return step.x, step.y
                        end
                        step = prev
                    end
                end
            end
        end
    end

    return nil, nil
end

return pathfind
