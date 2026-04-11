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
    }
    platform.serviceCall("game/db", "createRole", roleData)

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
        },
        items       = {},
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
        },
        items       = {},
        tasks       = {},
        server_time = now,
    }
    local rspData = protos.game.EnterGameResponse.encode(response)
    platform.log("info", "EnterGame: roleId=" .. tostring(roleId) .. " name=" .. (role.role_name or "?"))

    return { msg_id = MessageId.GAME_ENTER_GAME_RSP, data = rspData }
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
