-- 怪物池逻辑：AI tick、状态管理、移动广播
local skynet = require "skynet"
local common = require "common"
local platform = common.platform
local MessageId = common.MessageId
local protos = common.protos

local logic = {}

-- AI 行为分发器
local AI_HANDLERS = {
    patrol = require "game.ai.behavior.patrol",
    patrol_chase = require "game.ai.behavior.patrol_chase",
    guard = require "game.ai.behavior.patrol_chase", -- guard 复用 patrol_chase，但配置里 patrol_range=0
}

function logic.createMonsterState(mapMonster)
    local aiConfig = common.queryTable("TbAi")[mapMonster.ai_id]
    local aiType = aiConfig and aiConfig.ai_type or "patrol"
    local monsterConfigs = common.queryTable("TbMonster")
    local monsterCfg = monsterConfigs and monsterConfigs[mapMonster.monster_id]
    local maxHp = 100
    if monsterCfg and monsterCfg.attrs then
        for _, attr in ipairs(monsterCfg.attrs) do
            if attr.attr_key == "hp" or attr.attr_key == "max_hp" then
                maxHp = attr.attr_value or 100
                break
            end
        end
    end
    return {
        instanceId   = mapMonster.id, -- 与 MapInfoSyncNotify / buildMonstersProto 保持一致
        monsterId    = mapMonster.monster_id,
        x            = mapMonster.x,
        y            = mapMonster.y,
        spawnX       = mapMonster.x,
        spawnY       = mapMonster.y,
        aiId         = mapMonster.ai_id or 0,
        aiType       = aiType,
        aiConfig     = aiConfig or {},
        state        = "idle",
        targetId     = nil,
        lastMoveTime = 0,
        hp           = maxHp,
        maxHp        = maxHp,
    }
end

function logic.initMonsters(mapId)
    local monsters = {}
    local mapMonsters = common.queryTable("TbMapMonster")
    if mapMonsters then
        for _, m in ipairs(mapMonsters) do
            if m.map_id == mapId and m.is_active then
                local state = logic.createMonsterState(m)
                monsters[state.instanceId] = state
            end
        end
    end
    return monsters
end

-- 获取同地图在线玩家列表
-- 通过 map_pool 查询（player_pool 已同步玩家状态到 map_pool）
function logic.getOnlinePlayersOnMap(mapId, mapName)
    local serviceName = common.getMapPoolName(mapId)
    local ok, players = pcall(platform.serviceCall, serviceName, "getPlayersOnMap", mapName)
    if not ok then
        skynet.error(string.format("[monster_pool] getOnlinePlayersOnMap failed: service=%s err=%s", serviceName, tostring(players)))
    end
    if ok and players then
        return players
    end
    return {}
end

function logic.tick(monsters, mapId)
    local mapName = common.getMapRegistry()[mapId] and common.getMapRegistry()[mapId].map_name or "xinshoucun"
    local players = logic.getOnlinePlayersOnMap(mapId, mapName)

    local movedMonsters = {}

    for instanceId, m in pairs(monsters) do
        local handler = AI_HANDLERS[m.aiType]
        if handler then
            local nx, ny = handler.run(m, mapId, players)
            if nx and ny and (nx ~= m.x or ny ~= m.y) then
                -- 记录移动
                local fromX, fromY = m.x, m.y
                m.x, m.y = nx, ny
                movedMonsters[#movedMonsters + 1] = {
                    instance_id = instanceId,
                    from_x = fromX,
                    from_y = fromY,
                    to_x = nx,
                    to_y = ny,
                    state = m.state,
                }
                -- 同步坐标到 map_pool
                platform.serviceSend(common.getMapPoolName(mapId), "monsterMove", instanceId, nx, ny)
            end
        end
    end

    -- 批量广播移动通知
    if #movedMonsters > 0 then
        logic.broadcastMonsterMoves(movedMonsters, mapName)
    end
end

function logic.broadcastMonsterMoves(moves, mapName)
    local notify = {}
    for _, m in ipairs(moves) do
        notify[#notify + 1] = {
            instance_id = m.instance_id,
            from_x = m.from_x,
            from_y = m.from_y,
            to_x = m.to_x,
            to_y = m.to_y,
            state = m.state,
        }
    end

    -- 使用 map_pool 广播给同地图所有在线玩家
    for _, m in ipairs(notify) do
        local data = protos.game.MonsterMoveNotify.encode(m)
        pcall(platform.serviceCall, common.getMapPoolName(common.getMapIdByName(mapName)), "broadcastToMap", mapName, MessageId.GAME_MONSTER_MOVE_NOTIFY, data)
    end
end

-- 检查某个格子是否被怪物占据
function logic.isOccupied(monsters, x, y)
    for _, m in pairs(monsters) do
        if m.x == x and m.y == y then
            return true
        end
    end
    return false
end

return logic
