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
        },
        items       = initItems,
        tasks       = {},
        server_time = now,
    }
    local rspData = protos.game.CreateRoleResponse.encode(response)
    platform.log("info", "CreateRole: " .. roleName .. " roleId=" .. tostring(roleId))

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
        },
        items       = items,
        tasks       = {},
        server_time = now,
    }
    local rspData = protos.game.EnterGameResponse.encode(response)
    platform.log("info", "EnterGame: roleId=" .. tostring(roleId) .. " name=" .. (role.role_name or "?"))

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

    local cmd = req.command or ""
    local args = req.args or ""

    local player = onlinePlayers[claims.account_id]
    if not player then
        local rspData = protos.game.GmCommandResponse.encode({
            code = ErrorCode.UNAUTHORIZED,
            message = "player not online",
        })
        return { msg_id = MessageId.GAME_GM_RSP, data = rspData }
    end

    if cmd == "additem" then
        -- 格式: "itemId:count" 或 "itemId"（默认1个）
        local itemIdStr, countStr = args:match("^(%d+):?(%d*)$")
        if not itemIdStr then
            local rspData = protos.game.GmCommandResponse.encode({
                code = ErrorCode.INVALID_REQUEST,
                message = "usage: additem itemId:count",
            })
            return { msg_id = MessageId.GAME_GM_RSP, data = rspData }
        end
        local itemId = tonumber(itemIdStr)
        local count = tonumber(countStr) or 1
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
            message = "added " .. count .. "x " .. (itemTable[itemId].name or itemId),
            items = buildItemsProto(player.role_id),
        })
        return { msg_id = MessageId.GAME_GM_RSP, data = rspData }
    end

    local rspData = protos.game.GmCommandResponse.encode({
        code = ErrorCode.INVALID_REQUEST,
        message = "unknown command: " .. cmd,
    })
    return { msg_id = MessageId.GAME_GM_RSP, data = rspData }
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

--------------------------------------------------------------------------------
-- 服务注册（纯命令处理，不注册 Gateway 路由）
--------------------------------------------------------------------------------
common.defineService("player_pool_" .. pool_id, handlers, {
    init = function()
        skynet.error("player_pool_" .. pool_id .. " started")
    end,
})
