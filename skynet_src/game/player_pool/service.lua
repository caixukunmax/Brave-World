-- 玩家池: 处理分配到本池的玩家操作
-- 由 player_mgr 按 account_id % POOL_COUNT 路由

local pool_id = tonumber(...) or 0

local skynet = require "skynet"
local common = require "common"
local platform = common.platform
local MessageId = common.MessageId
local ErrorCode = common.ErrorCode
local protos = common.protos

--------------------------------------------------------------------------------
-- 在线玩家数据（本池）
--------------------------------------------------------------------------------
local onlinePlayers = {}

--------------------------------------------------------------------------------
-- 辅助：解析 "1001:1,1002:10" 格式的初始物品
--------------------------------------------------------------------------------
local function parseInitItems(str)
    local items = {}
    if not str or str == "" then return items end
    for pair in str:gmatch("([^,]+)") do
        local id, count = pair:match("^(%d+):(%d+)$")
        if id and count then
            items[#items + 1] = { item_id = tonumber(id), count = tonumber(count) }
        end
    end
    return items
end

-- 辅助：从 DB 查询结果构建 proto items 数组
local function buildItemsProto(roleId)
    local dbItems = platform.serviceCall("game/db", "getInventory", roleId)
    local items = {}
    for _, row in ipairs(dbItems) do
        items[#items + 1] = { item_id = row.item_id, count = row.count }
    end
    return items
end

-- 辅助：构建地图宝箱列表（按地图过滤，含开启状态）
local function buildChestsProto(roleId, mapId)
    local mapChestData = common.getMapChests(mapId, common.EMapEntityType.CHEST)
    if not mapChestData or #mapChestData == 0 then return {} end
    
    local opened = platform.serviceCall("game/db", "getOpenedChests", roleId)
    local chests = {}
    for _, instance in ipairs(mapChestData) do
        chests[#chests + 1] = {
            chest_id = instance.chest_id,
            x = instance.x,
            y = instance.y,
            opened = opened[instance.chest_id] or false,
        }
    end
    return chests
end

-- 辅助：构建地图怪物列表
local function buildMonstersProto(mapId)
    local mapMonsterData = common.getMapMonsters(mapId)
    if not mapMonsterData or #mapMonsterData == 0 then return {} end
    
    local monsters = {}
    for _, m in ipairs(mapMonsterData) do
        local attrs = {}
        for _, attr in ipairs(m.attrs or {}) do
            attrs[#attrs + 1] = {
                attr_key = attr.attr_key,
                attr_value = attr.attr_value,
            }
        end
        monsters[#monsters + 1] = {
            instance_id = m.instance_id,
            monster_id = m.monster_id,
            x = m.x,
            y = m.y,
            name = m.name,
            level = m.level,
            attrs = attrs,
        }
    end
    return monsters
end

-- 辅助：推送地图信息同步（宝箱+怪物）
local function sendMapInfoSyncNotify(accountId, serverId, mapName, mapId, roleId)
    roleId = roleId or 0
    local chests = buildChestsProto(roleId, mapId)
    local monsters = buildMonstersProto(mapId)
    local notifyData = protos.game.MapInfoSyncNotify.encode({
        map_name = mapName,
        chests = chests,
        monsters = monsters,
    })
    platform.serviceSend("game/gateway", "sendToAccount", accountId, serverId, MessageId.GAME_MAP_INFO_SYNC_NOTIFY, notifyData)
    platform.log("info", string.format("MapInfoSyncNotify: account=%s map=%s chests=%d monsters=%d",
        tostring(accountId), mapName, #chests, #monsters))
end

--------------------------------------------------------------------------------
-- handlers: 由 player_mgr 调用
--------------------------------------------------------------------------------
local handlers = {}

function handlers.createRole(msg, claims)
    -- claims 已由 player_mgr 验证，直接使用
    local req = protos.game.CreateRoleRequest.decode(msg.data)
    if not req then
        return common.makeError(MessageId.GAME_CREATE_ROLE_RSP, ErrorCode.INVALID_REQUEST)
    end

    local roleName = req.role_name or ""

    if #roleName < 2 then
        return common.makeError(MessageId.GAME_CREATE_ROLE_RSP, ErrorCode.ROLE_NAME_TOO_SHORT)
    end
    if #roleName > 12 then
        return common.makeError(MessageId.GAME_CREATE_ROLE_RSP, ErrorCode.ROLE_NAME_TOO_LONG)
    end

    local nameExists = platform.serviceCall("game/db", "checkRoleNameExists", claims.server_id, roleName)
    if nameExists then
        return common.makeError(MessageId.GAME_CREATE_ROLE_RSP, ErrorCode.ROLE_NAME_EXISTS)
    end

    local roleCount = platform.serviceCall("game/db", "countRolesByAccountAndServer", claims.account_id, claims.server_id)
    if roleCount >= 3 then
        return common.makeError(MessageId.GAME_CREATE_ROLE_RSP, ErrorCode.ROLE_COUNT_LIMIT)
    end

    local roleId = platform.serviceCall("game/db", "getNextRoleId")
    local now = math.floor(skynet.time())
    local startMap = common.getFirstMap()

    local roleData = {
        role_id         = roleId,
        account_id      = claims.account_id,
        server_id       = claims.server_id,
        role_name       = roleName,
        level           = 1,
        exp             = 0,
        avatar_id       = 0,
        gold            = 10000,
        diamond         = 100,
        total_power     = 100,
        vip_level       = 0,
        create_time     = now,
        last_login_time = now,
        job             = "无",
        title           = "新手",
        status          = "在线",
        current_map     = startMap.map_name,
        grid_x          = startMap.spawn_x,
        grid_y          = startMap.spawn_y,
    }
    platform.serviceCall("game/db", "createRole", roleData)

    -- 初始化背包：从 RoleInitConfig 读取初始物品
    local initConfig = common.queryTable("TbRoleInitConfig")
    local initItems = {}
    if initConfig then
        for _, cfg in pairs(initConfig) do
            initItems = parseInitItems(cfg.init_items or "")
            break
        end
    end
    for _, item in ipairs(initItems) do
        platform.serviceCall("game/db", "addItem", roleId, item.item_id, item.count)
    end

    local response = {
        code    = ErrorCode.SUCCESS,
        message = "",
        role_info = {
            role_id         = roleId,
            role_name       = roleName,
            level           = 1,
            exp             = 0,
            avatar_id       = 0,
            gold            = 10000,
            diamond         = 100,
            total_power     = 100,
            vip_level       = 0,
            create_time     = now,
            last_login_time = now,
            job             = "无",
            title           = "新手",
            status          = "在线",
            current_map     = startMap.map_name,
            grid_x          = startMap.spawn_x,
            grid_y          = startMap.spawn_y,
        },
        items       = initItems,
        tasks       = {},
        server_time = now,
        chests      = buildChestsProto(roleId, startMap.id),
    }
    local rspData = protos.game.CreateRoleResponse.encode(response)
    platform.log("info", "CreateRole: " .. roleName .. " roleId=" .. tostring(roleId))

    -- 推送地图信息同步（宝箱+怪物）
    sendMapInfoSyncNotify(claims.account_id, claims.server_id, startMap.map_name, startMap.id, roleId)

    return { msg_id = MessageId.GAME_CREATE_ROLE_RSP, data = rspData }
end

function handlers.enterGame(msg, claims)
    -- claims 已由 player_mgr 验证
    local req = protos.game.EnterGameRequest.decode(msg.data)
    if not req then
        return common.makeError(MessageId.GAME_ENTER_GAME_RSP, ErrorCode.INVALID_REQUEST)
    end

    local roleId = req.role_id or 0
    if roleId == 0 then
        return common.makeError(MessageId.GAME_ENTER_GAME_RSP, ErrorCode.INVALID_REQUEST)
    end

    local role = platform.serviceCall("game/db", "findRoleById", roleId)
    if not role then
        return common.makeError(MessageId.GAME_ENTER_GAME_RSP, ErrorCode.ROLE_NOT_FOUND)
    end

    if role.account_id ~= claims.account_id or role.server_id ~= claims.server_id then
        return common.makeError(MessageId.GAME_ENTER_GAME_RSP, ErrorCode.FORBIDDEN)
    end

    local now = math.floor(skynet.time())
    platform.serviceSend("game/db", "updateRole", roleId, { last_login_time = now })
    onlinePlayers[claims.account_id] = role

    local items = buildItemsProto(roleId)

    local response = {
        code    = ErrorCode.SUCCESS,
        message = "",
        role_info = {
            role_id         = role.role_id or 0,
            role_name       = role.role_name or "",
            level           = role.level or 1,
            exp             = role.exp or 0,
            avatar_id       = role.avatar_id or 0,
            gold            = role.gold or 0,
            diamond         = role.diamond or 0,
            total_power     = role.total_power or 0,
            vip_level       = role.vip_level or 0,
            create_time     = role.create_time or 0,
            last_login_time = now,
            job             = role.job or "无",
            title           = role.title or "新手",
            status          = role.status or "在线",
            current_map     = role.current_map or "xinshoucun",
            grid_x          = role.grid_x or 25,
            grid_y          = role.grid_y or 25,
        },
        items       = items,
        tasks       = {},
        server_time = now,
        chests      = buildChestsProto(roleId, common.getMapIdByName(role.current_map or "xinshoucun")),
    }
    local rspData = protos.game.EnterGameResponse.encode(response)
    platform.log("info", "EnterGame: roleId=" .. tostring(roleId) .. " name=" .. (role.role_name or "?"))

    -- 推送地图信息同步（宝箱+怪物）
    local mapName = role.current_map or "xinshoucun"
    local mapId = common.getMapIdByName(mapName)
    sendMapInfoSyncNotify(claims.account_id, claims.server_id, mapName, mapId, roleId)

    return { msg_id = MessageId.GAME_ENTER_GAME_RSP, data = rspData }
end

function handlers.move(msg, claims)
    local req = protos.game.MoveRequest.decode(msg.data)
    if not req then
        return common.makeError(MessageId.GAME_MOVE_RSP, ErrorCode.INVALID_REQUEST)
    end

    local fromX, fromY = req.from_x or 0, req.from_y or 0
    local toX, toY = req.to_x or 0, req.to_y or 0
    local mapName = req.map_name or ""

    -- 校验移动距离（只允许相邻格）
    local dx = math.abs(toX - fromX)
    local dy = math.abs(toY - fromY)
    if dx + dy ~= 1 then
        local rspData = protos.game.MoveResponse.encode({
            code = ErrorCode.INVALID_REQUEST,
            message = "invalid distance",
            x = fromX,
            y = fromY,
        })
        return { msg_id = MessageId.GAME_MOVE_RSP, data = rspData }
    end

    -- 校验地图数据
    if mapName == "" or not common.isWalkable(mapName, toX, toY) then
        local rspData = protos.game.MoveResponse.encode({
            code = ErrorCode.FORBIDDEN,
            message = "target not walkable",
            x = fromX,
            y = fromY,
        })
        return { msg_id = MessageId.GAME_MOVE_RSP, data = rspData }
    end

    -- 校验目标格是否有怪物阻挡
    if common.isBlockedByMonster(mapName, toX, toY) then
        local rspData = protos.game.MoveResponse.encode({
            code = ErrorCode.FORBIDDEN,
            message = "blocked by monster",
            x = fromX,
            y = fromY,
        })
        return { msg_id = MessageId.GAME_MOVE_RSP, data = rspData }
    end

    -- 校验目标格是否有未开的宝箱阻挡
    local mapId = common.getMapIdByName(mapName)
    local mapChests = common.getMapChests(mapId, common.EMapEntityType.CHEST)
    for _, instance in ipairs(mapChests) do
        if instance.x == toX and instance.y == toY then
            local opened = platform.serviceCall("game/db", "isChestOpened", onlinePlayers[claims.account_id].role_id, instance.chest_id)
            if not opened then
                local rspData = protos.game.MoveResponse.encode({
                    code = ErrorCode.FORBIDDEN,
                    message = "blocked by chest",
                    x = fromX,
                    y = fromY,
                })
                return { msg_id = MessageId.GAME_MOVE_RSP, data = rspData }
            end
        end
    end

    -- 更新在线玩家位置
    local player = onlinePlayers[claims.account_id]
    if player then
        player.grid_x = toX
        player.grid_y = toY
    end

    local rspData = protos.game.MoveResponse.encode({
        code = ErrorCode.SUCCESS,
        message = "",
        x = toX,
        y = toY,
    })
    return { msg_id = MessageId.GAME_MOVE_RSP, data = rspData }
end

function handlers.useItem(msg, claims)
    local req = protos.game.UseItemRequest.decode(msg.data)
    if not req then
        return common.makeError(MessageId.GAME_USE_ITEM_RSP, ErrorCode.INVALID_REQUEST)
    end

    local player = onlinePlayers[claims.account_id]
    if not player then
        return common.makeError(MessageId.GAME_USE_ITEM_RSP, ErrorCode.UNAUTHORIZED)
    end

    local itemId = req.item_id or 0
    local count = req.count or 0
    if itemId == 0 or count == 0 then
        return common.makeError(MessageId.GAME_USE_ITEM_RSP, ErrorCode.INVALID_REQUEST)
    end

    local ok = platform.serviceCall("game/db", "removeItem", player.role_id, itemId, count)
    if not ok then
        local rspData = protos.game.UseItemResponse.encode({
            code = ErrorCode.NOT_FOUND,
            message = "item not enough",
            items = buildItemsProto(player.role_id),
        })
        return { msg_id = MessageId.GAME_USE_ITEM_RSP, data = rspData }
    end

    platform.log("info", "UseItem: roleId=" .. tostring(player.role_id) .. " itemId=" .. itemId .. " count=" .. count)
    local rspData = protos.game.UseItemResponse.encode({
        code = ErrorCode.SUCCESS,
        message = "",
        items = buildItemsProto(player.role_id),
    })
    return { msg_id = MessageId.GAME_USE_ITEM_RSP, data = rspData }
end

function handlers.dropItem(msg, claims)
    local req = protos.game.DropItemRequest.decode(msg.data)
    if not req then
        return common.makeError(MessageId.GAME_DROP_ITEM_RSP, ErrorCode.INVALID_REQUEST)
    end

    local player = onlinePlayers[claims.account_id]
    if not player then
        return common.makeError(MessageId.GAME_DROP_ITEM_RSP, ErrorCode.UNAUTHORIZED)
    end

    local itemId = req.item_id or 0
    local count = req.count or 0
    if itemId == 0 or count == 0 then
        return common.makeError(MessageId.GAME_DROP_ITEM_RSP, ErrorCode.INVALID_REQUEST)
    end

    local ok = platform.serviceCall("game/db", "removeItem", player.role_id, itemId, count)
    if not ok then
        local rspData = protos.game.DropItemResponse.encode({
            code = ErrorCode.NOT_FOUND,
            message = "item not enough",
            items = buildItemsProto(player.role_id),
        })
        return { msg_id = MessageId.GAME_DROP_ITEM_RSP, data = rspData }
    end

    platform.log("info", "DropItem: roleId=" .. tostring(player.role_id) .. " itemId=" .. itemId .. " count=" .. count)
    local rspData = protos.game.DropItemResponse.encode({
        code = ErrorCode.SUCCESS,
        message = "",
        items = buildItemsProto(player.role_id),
    })
    return { msg_id = MessageId.GAME_DROP_ITEM_RSP, data = rspData }
end

function handlers.gmCommand(msg, claims)
    local req = protos.game.GmCommandRequest.decode(msg.data)
    if not req then
        local rspData = protos.game.GmCommandResponse.encode({
            code = ErrorCode.INVALID_REQUEST,
            message = "invalid request",
        })
        return { msg_id = MessageId.GAME_GM_RSP, data = rspData }
    end

    -- 命令格式: "additem,itemId,count" 整串放在 command 字段
    local cmdLine = req.command or ""
    local parts = {}
    for p in cmdLine:gmatch("[^,]+") do
        parts[#parts + 1] = p
    end
    local cmd = parts[1] or ""

    local player = onlinePlayers[claims.account_id]
    if not player then
        local rspData = protos.game.GmCommandResponse.encode({
            code = ErrorCode.UNAUTHORIZED,
            message = "player not online",
        })
        return { msg_id = MessageId.GAME_GM_RSP, data = rspData }
    end

    if cmd == "additem" then
        -- 格式: additem,itemId,count
        local itemId = tonumber(parts[2]) or 0
        local count = tonumber(parts[3]) or 1
        if itemId == 0 then
            local rspData = protos.game.GmCommandResponse.encode({
                code = ErrorCode.INVALID_REQUEST,
                message = "usage: additem,itemId,count",
            })
            return { msg_id = MessageId.GAME_GM_RSP, data = rspData }
        end
        if count <= 0 then count = 1 end

        -- 校验物品是否存在
        local itemTable = common.queryTable("TbItem")
        if not itemTable or not itemTable[itemId] then
            local rspData = protos.game.GmCommandResponse.encode({
                code = ErrorCode.NOT_FOUND,
                message = "item not found: " .. tostring(itemId),
            })
            return { msg_id = MessageId.GAME_GM_RSP, data = rspData }
        end

        platform.serviceCall("game/db", "addItem", player.role_id, itemId, count)
        platform.log("info", "GM additem: roleId=" .. tostring(player.role_id) .. " item=" .. itemId .. " count=" .. count)

        local rspData = protos.game.GmCommandResponse.encode({
            code = ErrorCode.SUCCESS,
            message = "added " .. count .. "x " .. (itemTable[itemId].name or tostring(itemId)),
            items = buildItemsProto(player.role_id),
        })
        return { msg_id = MessageId.GAME_GM_RSP, data = rspData }
    end

    if cmd == "teleport" then
        -- 格式: teleport,x,y
        local tx = tonumber(parts[2]) or 0
        local ty = tonumber(parts[3]) or 0
        if tx == 0 and ty == 0 then
            local rspData = protos.game.GmCommandResponse.encode({
                code = ErrorCode.INVALID_REQUEST,
                message = "usage: teleport,x,y",
            })
            return { msg_id = MessageId.GAME_GM_RSP, data = rspData }
        end

        local mapName = player.current_map or "xinshoucun"
        local finalX, finalY = common.findNearestWalkable(mapName, tx, ty, 10)

        if not finalX then
            local rspData = protos.game.GmCommandResponse.encode({
                code = ErrorCode.FORBIDDEN,
                message = "no walkable cell near (" .. tx .. "," .. ty .. ")",
            })
            return { msg_id = MessageId.GAME_GM_RSP, data = rspData }
        end

        player.grid_x = finalX
        player.grid_y = finalY
        platform.log("info", "GM teleport: roleId=" .. tostring(player.role_id) .. " to (" .. finalX .. "," .. finalY .. ")")

        -- 返回实际坐标，客户端解析 TELEPORT:x:y 格式
        local rspData = protos.game.GmCommandResponse.encode({
            code = ErrorCode.SUCCESS,
            message = "TELEPORT:" .. finalX .. ":" .. finalY,
        })
        return { msg_id = MessageId.GAME_GM_RSP, data = rspData }
    end

    if cmd == "addchest" then
        -- 格式: addchest,chest_config_id,x,y
        local chestCfgId = tonumber(parts[2]) or 0
        local cx = tonumber(parts[3]) or -1
        local cy = tonumber(parts[4]) or -1
        
        if chestCfgId <= 0 or cx < 0 or cy < 0 then
            local rspData = protos.game.GmCommandResponse.encode({
                code = ErrorCode.INVALID_REQUEST,
                message = "usage: addchest,chest_config_id,x,y",
            })
            return { msg_id = MessageId.GAME_GM_RSP, data = rspData }
        end

        local chestType = common.getChestType(chestCfgId)
        if not chestType then
            local rspData = protos.game.GmCommandResponse.encode({
                code = ErrorCode.NOT_FOUND,
                message = "chest config not found: " .. chestCfgId,
            })
            return { msg_id = MessageId.GAME_GM_RSP, data = rspData }
        end
        
        local rewards = chestType.rewards or ""
        local mapName = player.current_map or "xinshoucun"
        local mapId = common.getMapIdByName(mapName)
        
        local seq = platform.serviceCall("game/db", "allocateEntitySeq", mapId, common.EMapEntityType.CHEST)
        local newId = common.makeInstanceId(mapId, common.EMapEntityType.CHEST, seq)

        platform.serviceCall("game/db", "addGmChest", newId, mapId, common.EMapEntityType.CHEST, chestCfgId, cx, cy, rewards)

        platform.log("info", "GM addchest: config=" .. chestCfgId .. " instance=" .. newId .. " (" .. cx .. "," .. cy .. ")")

        -- 广播宝箱更新给同地图所有在线玩家
        local notifyData = protos.game.ChestUpdateNotify.encode({
            chests = {
                { chest_id = newId, x = cx, y = cy, opened = false }
            }
        })
        platform.log("info", string.format("Broadcasting chest update: map=%s server_id=%s players=%d", mapName, tostring(claims.server_id), #onlinePlayers))
        for accountId, p in pairs(onlinePlayers) do
            if p.current_map == mapName then
                platform.log("info", string.format("Sending notify to account=%s server_id=%s", tostring(accountId), tostring(claims.server_id)))
                platform.serviceSend("game/gateway", "sendToAccount", accountId, claims.server_id, MessageId.GAME_CHEST_UPDATE_NOTIFY, notifyData)
            end
        end

        local rspData = protos.game.GmCommandResponse.encode({
            code = ErrorCode.SUCCESS,
            message = "ADDCH:" .. newId .. ":" .. cx .. ":" .. cy .. ":" .. chestType.name,
        })
        return { msg_id = MessageId.GAME_GM_RSP, data = rspData }
    end

    local rspData = protos.game.GmCommandResponse.encode({
        code = ErrorCode.INVALID_REQUEST,
        message = "unknown command: " .. cmd,
    })
    return { msg_id = MessageId.GAME_GM_RSP, data = rspData }
end

function handlers.openChest(msg, claims)
    local req = protos.game.OpenChestRequest.decode(msg.data)
    if not req then
        return common.makeError(MessageId.GAME_OPEN_CHEST_RSP, ErrorCode.INVALID_REQUEST)
    end

    local player = onlinePlayers[claims.account_id]
    if not player then
        return common.makeError(MessageId.GAME_OPEN_CHEST_RSP, ErrorCode.UNAUTHORIZED)
    end

    local instanceId = req.chest_id or 0
    if instanceId == 0 then
        return common.makeError(MessageId.GAME_OPEN_CHEST_RSP, ErrorCode.INVALID_REQUEST)
    end

    local parsed = common.parseInstanceId(instanceId)
    local mapId = parsed.mapId
    local entityType = parsed.entityType
    
    if entityType ~= common.EMapEntityType.CHEST then
        return common.makeError(MessageId.GAME_OPEN_CHEST_RSP, ErrorCode.INVALID_REQUEST)
    end
    
    local mapChests = common.getMapChests(mapId, entityType)
    local chestX, chestY
    local chestTypeId
    local found = false
    
    for _, instance in ipairs(mapChests) do
        if instance.chest_id == instanceId then
            chestX, chestY = instance.x, instance.y
            chestTypeId = instance.chest_type_id
            found = true
            break
        end
    end
    
    if not found then
        return common.makeError(MessageId.GAME_OPEN_CHEST_RSP, ErrorCode.NOT_FOUND)
    end

    -- 校验玩家在宝箱旁边（相邻格）
    local px = player.grid_x or 0
    local py = player.grid_y or 0
    local dx = math.abs(px - chestX)
    local dy = math.abs(py - chestY)
    platform.log("info", string.format("OpenChest check: player=(%d,%d) chest=(%d,%d) dx+dy=%d id=%d", px, py, chestX, chestY, dx+dy, instanceId))
    if dx + dy > 1 then
        return common.makeError(MessageId.GAME_OPEN_CHEST_RSP, ErrorCode.FORBIDDEN)
    end

    -- 检查是否已开（使用实例ID存储）
    local roleId = player.role_id
    local alreadyOpened = platform.serviceCall("game/db", "isChestOpened", roleId, instanceId)
    if alreadyOpened then
        local rspData = protos.game.OpenChestResponse.encode({
            code = ErrorCode.INVALID_REQUEST,
            message = "chest already opened",
        })
        return { msg_id = MessageId.GAME_OPEN_CHEST_RSP, data = rspData }
    end

    -- 发放奖励
    local rewards = common.getChestRewards(chestTypeId)
    for _, item in ipairs(rewards) do
        platform.serviceCall("game/db", "addItem", roleId, item.item_id, item.count)
    end

    -- 标记已开（使用实例ID）
    platform.serviceCall("game/db", "markChestOpened", roleId, instanceId)
    platform.log("info", "OpenChest: roleId=" .. tostring(roleId) .. " instanceId=" .. tostring(instanceId) .. " type=" .. tostring(chestTypeId) .. " items=" .. #rewards)

    local rspData = protos.game.OpenChestResponse.encode({
        code = ErrorCode.SUCCESS,
        message = "",
        items = rewards,
    })
    return { msg_id = MessageId.GAME_OPEN_CHEST_RSP, data = rspData }
end

function handlers.login(msg)
    platform.log("info", "Player login (pool " .. pool_id .. ")", msg.userId)
    local player = platform.serviceCall("game/db", "queryPlayer", msg.userId)
    if not player then
        return { success = false, error = "Player not found" }
    end
    onlinePlayers[msg.userId] = player
    return { success = true, sessionId = "s_" .. msg.userId }
end

function handlers.kick(userId)
    onlinePlayers[userId] = nil
    platform.serviceSend("game/gateway", "kick", userId)
    platform.log("info", "Player kicked (pool " .. pool_id .. ")", userId)
end

function handlers.getOnlineCount()
    local count = 0
    for _ in pairs(onlinePlayers) do
        count = count + 1
    end
    return count
end

-- 供 monster_pool 查询同地图在线玩家
function handlers.getOnlinePlayersByMap(mapName)
    local result = {}
    for accountId, p in pairs(onlinePlayers) do
        if p.current_map == mapName then
            result[accountId] = {
                account_id = accountId,
                role_id = p.role_id,
                grid_x = p.grid_x or 0,
                grid_y = p.grid_y or 0,
                server_id = p.server_id,
            }
        end
    end
    return result
end

--------------------------------------------------------------------------------
-- 服务注册（纯命令处理，不注册 Gateway 路由）
--------------------------------------------------------------------------------
common.defineService("player_pool_" .. pool_id, handlers, {
    init = function()
        skynet.error("player_pool_" .. pool_id .. " started")
    end,
})
