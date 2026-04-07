local ____lualib = require("lualib_bundle")
local __TS__Class = ____lualib.__TS__Class
local __TS__New = ____lualib.__TS__New
local __TS__SourceMapTraceBack = ____lualib.__TS__SourceMapTraceBack
__TS__SourceMapTraceBack(debug.getinfo(1).short_src, {["7"] = 3,["8"] = 3,["9"] = 3,["10"] = 3,["11"] = 5,["12"] = 5,["13"] = 5,["14"] = 8,["15"] = 9,["16"] = 8,["17"] = 12,["18"] = 13,["19"] = 12,["20"] = 21,["21"] = 23,["22"] = 24,["23"] = 26,["25"] = 29,["26"] = 30,["27"] = 32,["28"] = 34,["30"] = 36,["31"] = 38,["33"] = 42,["34"] = 44,["35"] = 46,["36"] = 47,["37"] = 47,["38"] = 47,["39"] = 47,["41"] = 50,["42"] = 52,["44"] = 55,["45"] = 57,["48"] = 62,["49"] = 65,["50"] = 68,["51"] = 69,["52"] = 70,["53"] = 72,["54"] = 73,["55"] = 75,["56"] = 75,["57"] = 75,["58"] = 75,["59"] = 75,["60"] = 75,["61"] = 75,["62"] = 75,["63"] = 75,["64"] = 75,["65"] = 75,["66"] = 75,["69"] = 90,["70"] = 90,["71"] = 90,["72"] = 90,["73"] = 90,["74"] = 90,["75"] = 90,["76"] = 90,["77"] = 90,["78"] = 100,["79"] = 101,["80"] = 101,["81"] = 101,["82"] = 101,["83"] = 103,["84"] = 21,["85"] = 110,["86"] = 112,["87"] = 113,["88"] = 115,["90"] = 118,["91"] = 119,["92"] = 122,["93"] = 123,["94"] = 125,["96"] = 129,["97"] = 130,["98"] = 132,["100"] = 134,["101"] = 136,["103"] = 140,["104"] = 142,["105"] = 143,["106"] = 144,["107"] = 145,["108"] = 146,["109"] = 147,["110"] = 147,["111"] = 147,["112"] = 147,["113"] = 147,["114"] = 147,["115"] = 147,["116"] = 147,["117"] = 155,["118"] = 156,["119"] = 157,["123"] = 163,["124"] = 166,["125"] = 166,["126"] = 166,["127"] = 166,["128"] = 166,["129"] = 166,["130"] = 166,["131"] = 169,["132"] = 169,["133"] = 169,["134"] = 169,["135"] = 169,["136"] = 169,["137"] = 169,["138"] = 169,["139"] = 178,["140"] = 179,["141"] = 179,["142"] = 179,["143"] = 179,["144"] = 181,["145"] = 181,["146"] = 181,["147"] = 181,["148"] = 181,["149"] = 181,["150"] = 181,["151"] = 110,["152"] = 190,["153"] = 191,["154"] = 191,["155"] = 191,["156"] = 191,["157"] = 191,["158"] = 192,["159"] = 190});
local ____exports = {}
local ____protos = require("protos.index")
local proto = ____protos.proto
local MessageId = ____protos.MessageId
local ErrorCode = ____protos.ErrorCode
____exports.LoginLogic = __TS__Class()
local LoginLogic = ____exports.LoginLogic
LoginLogic.name = "LoginLogic"
function LoginLogic.prototype.____constructor(self, platform)
    self.platform = platform
end
function LoginLogic.prototype.init(self)
    self.platform.log("info", "login_logic init")
end
function LoginLogic.prototype.accountLogin(self, msg)
    local req = proto.login.AccountLoginRequest.decode(msg.data)
    if not req then
        return self:makeError(MessageId.LOGIN_ACCOUNT_LOGIN_RSP, ErrorCode.INVALID_REQUEST)
    end
    local username = req.username or ""
    local password = req.password or ""
    if not username or #username < 2 then
        return self:makeError(MessageId.LOGIN_ACCOUNT_LOGIN_RSP, ErrorCode.INVALID_ACCOUNT_FORMAT)
    end
    if not password or #password < 1 then
        return self:makeError(MessageId.LOGIN_ACCOUNT_LOGIN_RSP, ErrorCode.INVALID_PASSWORD_FORMAT)
    end
    local account = self.platform.serviceCall("db_service", "findAccountByUsername", username)
    if not account then
        account = self.platform.serviceCall("db_service", "createAccount", username, password)
        self.platform.log(
            "info",
            (("Auto-registered account: " .. username) .. " id=") .. tostring(account.account_id)
        )
    else
        if not password_verify(password, account.password) then
            return self:makeError(MessageId.LOGIN_ACCOUNT_LOGIN_RSP, ErrorCode.PASSWORD_ERROR)
        end
        if account.status == 1 then
            return self:makeError(MessageId.LOGIN_ACCOUNT_LOGIN_RSP, ErrorCode.ACCOUNT_BANNED)
        end
    end
    local servers = self.platform.serviceCall("db_service", "getServers")
    local accountToken = token_generate_account(account.account_id, username)
    local serverList = {}
    if servers ~= nil and #servers > 0 then
        for ____, s in ipairs(servers) do
            local roles = self.platform.serviceCall("db_service", "findRolesByAccountAndServer", account.account_id, s.server_id)
            local hasRole = roles and #roles > 0
            serverList[#serverList + 1] = {
                server_id = s.server_id or 0,
                server_name = s.server_name or "",
                host = s.host or "",
                port = s.port or 0,
                status = s.status or 0,
                online_count = s.online_count or 0,
                is_new = s.is_new or false,
                is_recommend = s.is_recommend or false,
                has_role = hasRole,
                role_count = roles ~= nil and #roles > 0 and #roles or 0
            }
        end
    end
    local response = {
        code = ErrorCode.SUCCESS,
        message = "",
        account_token = accountToken,
        account_id = account.account_id,
        servers = serverList,
        last_server_id = account.last_server_id or 0,
        last_role_name = account.last_role_name or ""
    }
    local rspData = proto.login.AccountLoginResponse.encode(response)
    self.platform.log(
        "info",
        (("Login success: " .. username) .. " accountId=") .. tostring(account.account_id)
    )
    return {msg_id = MessageId.LOGIN_ACCOUNT_LOGIN_RSP, data = rspData}
end
function LoginLogic.prototype.selectServer(self, msg)
    local req = proto.login.SelectServerRequest.decode(msg.data)
    if not req then
        return self:makeError(MessageId.LOGIN_SELECT_SERVER_RSP, ErrorCode.INVALID_REQUEST)
    end
    local accountToken = req.account_token or ""
    local serverId = req.server_id or 0
    local claims = token_validate_account(accountToken)
    if not claims then
        return self:makeError(MessageId.LOGIN_SELECT_SERVER_RSP, ErrorCode.UNAUTHORIZED)
    end
    local server = self.platform.serviceCall("db_service", "findServerById", serverId)
    if not server then
        return self:makeError(MessageId.LOGIN_SELECT_SERVER_RSP, ErrorCode.SERVER_NOT_FOUND)
    end
    if server.status == 0 then
        return self:makeError(MessageId.LOGIN_SELECT_SERVER_RSP, ErrorCode.SERVER_MAINTENANCE)
    end
    local roles = self.platform.serviceCall("db_service", "findRolesByAccountAndServer", claims.account_id, serverId)
    local roleList = {}
    local lastRoleName = ""
    local lastLoginTime = 0
    if roles ~= nil and #roles > 0 then
        for ____, r in ipairs(roles) do
            roleList[#roleList + 1] = {
                role_id = r.role_id or 0,
                role_name = r.role_name or "",
                level = r.level or 1,
                avatar_id = r.avatar_id or 0,
                last_login = r.last_login_time or 0,
                total_power = r.total_power or 0
            }
            if (r.last_login_time or 0) > lastLoginTime then
                lastLoginTime = r.last_login_time
                lastRoleName = r.role_name or ""
            end
        end
    end
    local gatewayToken = token_generate_gateway(claims.account_id, serverId)
    self.platform.serviceSend(
        "db_service",
        "updateAccountLastServer",
        claims.account_id,
        serverId,
        lastRoleName
    )
    local response = {
        code = ErrorCode.SUCCESS,
        message = "",
        gateway_token = gatewayToken,
        roles = roleList,
        max_role_count = 3,
        server_time = math.floor(skynet.time())
    }
    local rspData = proto.login.SelectServerResponse.encode(response)
    self.platform.log(
        "info",
        (("SelectServer: accountId=" .. tostring(claims.account_id)) .. " serverId=") .. tostring(serverId)
    )
    return {
        msg_id = MessageId.LOGIN_SELECT_SERVER_RSP,
        data = rspData,
        bind_token = gatewayToken,
        account_id = claims.account_id,
        server_id = serverId
    }
end
function LoginLogic.prototype.makeError(self, msgId, code)
    local rspData = proto.common.Response.encode({
        code = code,
        message = "",
        data = __TS__New(Uint8Array, 0)
    })
    return {msg_id = msgId, data = rspData}
end
return ____exports
