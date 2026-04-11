-- Login 服务: 账号登录、选服
-- 路由 msg_id 210 (LOGIN_ACCOUNT_LOGIN_REQ), 212 (LOGIN_SELECT_SERVER_REQ)

local skynet = require "skynet"
local common = require "common"
local platform = common.platform
local MessageId = common.MessageId
local ErrorCode = common.ErrorCode
local protos = common.protos

--------------------------------------------------------------------------------
-- 账号登录
-- 自动注册：账号不存在则创建
--------------------------------------------------------------------------------
local function accountLogin(msg)
    -- 1. 解码请求
    local req = protos.login.AccountLoginRequest.decode(msg.data)
    if not req then
        return common.makeError(MessageId.LOGIN_ACCOUNT_LOGIN_RSP, ErrorCode.INVALID_REQUEST)
    end

    local username = req.username or ""
    local password = req.password or ""

    -- 2. 校验格式
    if not username or #username < 2 then
        return common.makeError(MessageId.LOGIN_ACCOUNT_LOGIN_RSP, ErrorCode.INVALID_ACCOUNT_FORMAT)
    end
    if not password or #password < 1 then
        return common.makeError(MessageId.LOGIN_ACCOUNT_LOGIN_RSP, ErrorCode.INVALID_PASSWORD_FORMAT)
    end

    -- 3. 查询账号
    local account = platform.serviceCall("game/db", "findAccountByUsername", username)

    if not account then
        -- 4. 自动注册
        account = platform.serviceCall("game/db", "createAccount", username, password)
        platform.log("info", "Auto-registered account: " .. username .. " id=" .. tostring(account.account_id))
    else
        -- 5. 验证密码
        if not common.password_verify(password, account.password) then
            return common.makeError(MessageId.LOGIN_ACCOUNT_LOGIN_RSP, ErrorCode.PASSWORD_ERROR)
        end
        if account.status == 1 then
            return common.makeError(MessageId.LOGIN_ACCOUNT_LOGIN_RSP, ErrorCode.ACCOUNT_BANNED)
        end
    end

    -- 6. 获取区服列表 + 批量查询该账号所有角色
    local servers = platform.serviceCall("game/db", "getServers")
    local allRoles = platform.serviceCall("game/db", "findAllRolesByAccount", account.account_id)

    -- 按 server_id 分组角色
    local rolesByServer = {}
    for _, r in ipairs(allRoles) do
        local sid = r.server_id
        if not rolesByServer[sid] then rolesByServer[sid] = {} end
        rolesByServer[sid][#rolesByServer[sid] + 1] = r
    end

    -- 7. 生成 AccountToken
    local accountToken = common.token_generate_account(account.account_id, username)

    -- 8. 构造区服列表（从本地分组取角色，无需逐服查询）
    local serverList = {}
    if servers and #servers > 0 then
        for _, s in ipairs(servers) do
            local roles = rolesByServer[s.server_id] or {}
            local hasRole = #roles > 0
            serverList[#serverList + 1] = {
                server_id    = s.server_id or 0,
                server_name  = s.server_name or "",
                host         = s.host or "",
                port         = s.port or 0,
                status       = s.status or 0,
                online_count = s.online_count or 0,
                is_new       = s.is_new or false,
                is_recommend = s.is_recommend or false,
                has_role     = hasRole,
                role_count   = #roles,
            }
        end
    end

    -- 9. 构造响应
    local response = {
        code           = ErrorCode.SUCCESS,
        message        = "",
        account_token  = accountToken,
        account_id     = account.account_id,
        servers        = serverList,
        last_server_id = account.last_server_id or 0,
        last_role_name = account.last_role_name or "",
    }

    local rspData = protos.login.AccountLoginResponse.encode(response)
    platform.log("info", "Login success: " .. username .. " accountId=" .. tostring(account.account_id))

    -- 发布玩家登录事件
    common.event.emit(common.EventType.PLAYER_LOGIN, { account_id = account.account_id, username = username })

    return { msg_id = MessageId.LOGIN_ACCOUNT_LOGIN_RSP, data = rspData }
end

--------------------------------------------------------------------------------
-- 选服
-- 验证 AccountToken，获取角色列表，生成 GatewayToken
--------------------------------------------------------------------------------
local function selectServer(msg)
    -- 1. 解码请求
    local req = protos.login.SelectServerRequest.decode(msg.data)
    if not req then
        return common.makeError(MessageId.LOGIN_SELECT_SERVER_RSP, ErrorCode.INVALID_REQUEST)
    end

    local accountToken = req.account_token or ""
    local serverId = req.server_id or 0

    -- 2. 验证 AccountToken
    local claims = common.token_validate_account(accountToken)
    if not claims then
        return common.makeError(MessageId.LOGIN_SELECT_SERVER_RSP, ErrorCode.UNAUTHORIZED)
    end

    -- 3. 检查区服是否存在
    local server = platform.serviceCall("game/db", "findServerById", serverId)
    if not server then
        return common.makeError(MessageId.LOGIN_SELECT_SERVER_RSP, ErrorCode.SERVER_NOT_FOUND)
    end
    if server.status == 0 then
        return common.makeError(MessageId.LOGIN_SELECT_SERVER_RSP, ErrorCode.SERVER_MAINTENANCE)
    end

    -- 4. 查询该账号在该服的角色列表
    local roles = platform.serviceCall("game/db", "findRolesByAccountAndServer", claims.account_id, serverId)

    local roleList = {}
    local lastRoleName = ""
    local lastLoginTime = 0
    if roles and #roles > 0 then
        for _, r in ipairs(roles) do
            roleList[#roleList + 1] = {
                role_id     = r.role_id or 0,
                role_name   = r.role_name or "",
                level       = r.level or 1,
                avatar_id   = r.avatar_id or 0,
                last_login  = r.last_login_time or 0,
                total_power = r.total_power or 0,
            }
            if (r.last_login_time or 0) > lastLoginTime then
                lastLoginTime = r.last_login_time
                lastRoleName = r.role_name or ""
            end
        end
    end

    -- 5. 生成 GatewayToken
    local gatewayToken = common.token_generate_gateway(claims.account_id, serverId)

    -- 6. 更新最后登录区服
    platform.serviceSend("game/db", "updateAccountLastServer", claims.account_id, serverId, lastRoleName)

    -- 7. 构造响应
    local response = {
        code           = ErrorCode.SUCCESS,
        message        = "",
        gateway_token  = gatewayToken,
        roles          = roleList,
        max_role_count = 3,
        server_time    = math.floor(skynet.time()),
    }

    local rspData = protos.login.SelectServerResponse.encode(response)
    platform.log("info", "SelectServer: accountId=" .. tostring(claims.account_id) .. " serverId=" .. tostring(serverId))

    return {
        msg_id     = MessageId.LOGIN_SELECT_SERVER_RSP,
        data       = rspData,
        bind_token = gatewayToken,
        account_id = claims.account_id,
        server_id  = serverId,
    }
end

--------------------------------------------------------------------------------
-- 服务注册
--------------------------------------------------------------------------------
common.defineService("login", {
    [MessageId.LOGIN_ACCOUNT_LOGIN_REQ]  = function(msg) return accountLogin(msg) end,
    [MessageId.LOGIN_SELECT_SERVER_REQ]  = function(msg) return selectServer(msg) end,
}, {
    init = function()
        platform.log("info", "login_service started")
    end,
})
