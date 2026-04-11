-- 玩家管理器: 启动 4 个玩家池，路由请求到对应池
-- 路由策略: account_id % POOL_COUNT

local skynet = require "skynet"
local common = require "common"
local MessageId = common.MessageId
local ErrorCode = common.ErrorCode

local POOL_COUNT = 4
local pools = {} -- pools[1..4] = skynet address

local function getPool(accountId)
    return pools[(accountId % POOL_COUNT) + 1]
end

--------------------------------------------------------------------------------
-- handlers: 命令 + Gateway 路由
--------------------------------------------------------------------------------
local handlers = {}

-- Gateway 路由: 验证 token 后转发到对应池（claims 传给池，避免重复验证）
handlers[MessageId.GAME_CREATE_ROLE_REQ] = function(msg)
    if not msg.token or msg.token == "" then
        return common.makeError(MessageId.GAME_CREATE_ROLE_RSP, ErrorCode.UNAUTHORIZED)
    end
    local claims = common.token_validate_gateway(msg.token)
    if not claims then
        return common.makeError(MessageId.GAME_CREATE_ROLE_RSP, ErrorCode.UNAUTHORIZED)
    end
    return skynet.call(getPool(claims.account_id), "lua", "createRole", msg, claims)
end

handlers[MessageId.GAME_ENTER_GAME_REQ] = function(msg)
    if not msg.token or msg.token == "" then
        return common.makeError(MessageId.GAME_ENTER_GAME_RSP, ErrorCode.UNAUTHORIZED)
    end
    local claims = common.token_validate_gateway(msg.token)
    if not claims then
        return common.makeError(MessageId.GAME_ENTER_GAME_RSP, ErrorCode.UNAUTHORIZED)
    end
    return skynet.call(getPool(claims.account_id), "lua", "enterGame", msg, claims)
end

-- 命令路由
function handlers.login(msg)
    return skynet.call(getPool(msg.userId), "lua", "login", msg)
end

function handlers.kick(userId)
    skynet.send(getPool(userId), "lua", "kick", userId)
end

function handlers.getOnlineCount()
    local total = 0
    for _, pool in ipairs(pools) do
        total = total + skynet.call(pool, "lua", "getOnlineCount")
    end
    return total
end

--------------------------------------------------------------------------------
-- 服务注册
--------------------------------------------------------------------------------
common.defineService("player_mgr", handlers, {
    init = function()
        for i = 0, POOL_COUNT - 1 do
            pools[i + 1] = skynet.newservice("game/player_pool", i)
        end
        skynet.error("player_mgr started with " .. POOL_COUNT .. " pools")
    end,
})
