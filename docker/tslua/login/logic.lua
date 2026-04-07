local ____lualib = require("lualib_bundle")
local __TS__Class = ____lualib.__TS__Class
local __TS__SourceMapTraceBack = ____lualib.__TS__SourceMapTraceBack
__TS__SourceMapTraceBack(debug.getinfo(1).short_src, {["6"] = 4,["7"] = 4,["8"] = 4,["9"] = 7,["10"] = 8,["11"] = 7,["12"] = 11,["13"] = 12,["14"] = 11,["15"] = 20,["16"] = 22,["17"] = 23,["18"] = 24,["20"] = 27,["21"] = 28,["22"] = 30,["23"] = 31,["25"] = 33,["26"] = 34,["28"] = 38,["29"] = 40,["30"] = 42,["31"] = 43,["32"] = 43,["33"] = 43,["34"] = 43,["36"] = 46,["37"] = 48,["38"] = 50,["39"] = 51,["40"] = 53,["41"] = 56,["42"] = 57,["43"] = 58,["45"] = 61,["46"] = 62,["48"] = 65,["49"] = 66,["52"] = 71,["53"] = 74,["54"] = 77,["55"] = 78,["56"] = 79,["57"] = 81,["58"] = 82,["59"] = 84,["60"] = 84,["61"] = 84,["62"] = 84,["63"] = 84,["64"] = 84,["65"] = 84,["66"] = 84,["67"] = 84,["68"] = 84,["69"] = 84,["70"] = 84,["73"] = 99,["74"] = 99,["75"] = 99,["76"] = 99,["77"] = 99,["78"] = 99,["79"] = 99,["80"] = 99,["81"] = 99,["82"] = 109,["83"] = 110,["84"] = 110,["85"] = 110,["86"] = 110,["87"] = 112,["88"] = 20,["89"] = 119,["90"] = 121,["91"] = 122,["92"] = 123,["94"] = 126,["95"] = 127,["96"] = 130,["97"] = 131,["98"] = 132,["100"] = 136,["101"] = 137,["102"] = 138,["104"] = 140,["105"] = 141,["107"] = 145,["108"] = 147,["109"] = 148,["110"] = 149,["111"] = 150,["112"] = 151,["113"] = 152,["114"] = 152,["115"] = 152,["116"] = 152,["117"] = 152,["118"] = 152,["119"] = 152,["120"] = 152,["121"] = 160,["122"] = 161,["123"] = 162,["127"] = 168,["128"] = 171,["129"] = 171,["130"] = 171,["131"] = 171,["132"] = 171,["133"] = 171,["134"] = 171,["135"] = 174,["136"] = 174,["137"] = 174,["138"] = 174,["139"] = 174,["140"] = 174,["141"] = 174,["142"] = 174,["143"] = 183,["144"] = 184,["145"] = 184,["146"] = 184,["147"] = 184,["148"] = 186,["149"] = 186,["150"] = 186,["151"] = 186,["152"] = 186,["153"] = 186,["154"] = 186,["155"] = 119,["156"] = 195,["157"] = 196,["158"] = 197,["159"] = 195});
local ____exports = {}
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
    local req = pb_decode("login.AccountLoginRequest", msg.data)
    if not req then
        return self:makeError(211, 2, "无效的请求格式")
    end
    local username = req.username or ""
    local password = req.password or ""
    if not username or #username < 2 then
        return self:makeError(211, 104, "用户名格式错误")
    end
    if not password or #password < 1 then
        return self:makeError(211, 105, "密码格式错误")
    end
    local account = self.platform.serviceCall("db_service", "findAccountByUsername", username)
    if not account then
        account = self.platform.serviceCall("db_service", "createAccount", username, password)
        self.platform.log(
            "info",
            (("Auto-registered account: " .. username) .. " id=") .. tostring(account.account_id)
        )
    else
        local passwordValid = false
        if password_verify(password, account.password) then
            passwordValid = true
        elseif account.password == password then
            passwordValid = true
            local hashedPassword = password_hash(password)
            self.platform.serviceSend("db_service", "updateAccountPassword", account.account_id, hashedPassword)
            self.platform.log("info", "Upgraded password to hash format for account: " .. username)
        end
        if not passwordValid then
            return self:makeError(211, 101, "密码错误")
        end
        if account.status == 1 then
            return self:makeError(211, 102, "账号已被封禁")
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
        code = 0,
        message = "",
        account_token = accountToken,
        account_id = account.account_id,
        servers = serverList,
        last_server_id = account.last_server_id or 0,
        last_role_name = account.last_role_name or ""
    }
    local rspData = pb_encode("login.AccountLoginResponse", response)
    self.platform.log(
        "info",
        (("Login success: " .. username) .. " accountId=") .. tostring(account.account_id)
    )
    return {msg_id = 211, data = rspData}
end
function LoginLogic.prototype.selectServer(self, msg)
    local req = pb_decode("login.SelectServerRequest", msg.data)
    if not req then
        return self:makeError(213, 2, "无效的请求格式")
    end
    local accountToken = req.account_token or ""
    local serverId = req.server_id or 0
    local claims = token_validate_account(accountToken)
    if not claims then
        return self:makeError(213, 3, "Token无效或已过期")
    end
    local server = self.platform.serviceCall("db_service", "findServerById", serverId)
    if not server then
        return self:makeError(213, 300, "区服不存在")
    end
    if server.status == 0 then
        return self:makeError(213, 301, "区服维护中")
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
        code = 0,
        message = "",
        gateway_token = gatewayToken,
        roles = roleList,
        max_role_count = 3,
        server_time = math.floor(skynet.time())
    }
    local rspData = pb_encode("login.SelectServerResponse", response)
    self.platform.log(
        "info",
        (("SelectServer: accountId=" .. tostring(claims.account_id)) .. " serverId=") .. tostring(serverId)
    )
    return {
        msg_id = 213,
        data = rspData,
        bind_token = gatewayToken,
        account_id = claims.account_id,
        server_id = serverId
    }
end
function LoginLogic.prototype.makeError(self, msgId, code, message)
    local rspData = pb_encode("common.Response", {code = code, message = message, data = ""})
    return {msg_id = msgId, data = rspData}
end
return ____exports
