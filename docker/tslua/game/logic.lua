local ____lualib = require("lualib_bundle")
local __TS__Class = ____lualib.__TS__Class
local __TS__SourceMapTraceBack = ____lualib.__TS__SourceMapTraceBack
__TS__SourceMapTraceBack(debug.getinfo(1).short_src, {["6"] = 4,["7"] = 4,["8"] = 4,["9"] = 7,["10"] = 8,["11"] = 7,["12"] = 11,["13"] = 12,["14"] = 11,["15"] = 18,["16"] = 20,["17"] = 21,["19"] = 23,["20"] = 24,["21"] = 25,["23"] = 29,["24"] = 30,["25"] = 31,["27"] = 34,["28"] = 37,["29"] = 38,["31"] = 40,["32"] = 41,["34"] = 45,["35"] = 46,["36"] = 47,["38"] = 51,["39"] = 52,["40"] = 53,["42"] = 57,["43"] = 58,["44"] = 60,["45"] = 60,["46"] = 60,["47"] = 60,["48"] = 60,["49"] = 60,["50"] = 60,["51"] = 60,["52"] = 60,["53"] = 60,["54"] = 60,["55"] = 60,["56"] = 60,["57"] = 60,["58"] = 60,["59"] = 76,["60"] = 79,["61"] = 79,["62"] = 79,["63"] = 82,["64"] = 82,["65"] = 82,["66"] = 82,["67"] = 82,["68"] = 82,["69"] = 82,["70"] = 82,["71"] = 82,["72"] = 82,["73"] = 82,["74"] = 82,["75"] = 79,["76"] = 79,["77"] = 79,["78"] = 79,["79"] = 79,["80"] = 100,["81"] = 101,["82"] = 101,["83"] = 101,["84"] = 101,["85"] = 103,["86"] = 18,["87"] = 109,["88"] = 111,["89"] = 112,["91"] = 114,["92"] = 115,["93"] = 116,["95"] = 120,["96"] = 121,["97"] = 122,["99"] = 125,["100"] = 126,["101"] = 127,["103"] = 131,["104"] = 132,["105"] = 133,["107"] = 137,["108"] = 138,["110"] = 142,["111"] = 143,["112"] = 146,["113"] = 146,["114"] = 146,["115"] = 149,["116"] = 149,["117"] = 149,["118"] = 149,["119"] = 149,["120"] = 149,["121"] = 149,["122"] = 149,["123"] = 149,["124"] = 149,["125"] = 149,["126"] = 149,["127"] = 146,["128"] = 146,["129"] = 146,["130"] = 146,["131"] = 146,["132"] = 167,["133"] = 168,["134"] = 168,["135"] = 168,["136"] = 168,["137"] = 170,["138"] = 109,["139"] = 173,["140"] = 174,["141"] = 175,["142"] = 173});
local ____exports = {}
____exports.GameLogic = __TS__Class()
local GameLogic = ____exports.GameLogic
GameLogic.name = "GameLogic"
function GameLogic.prototype.____constructor(self, platform)
    self.platform = platform
end
function GameLogic.prototype.init(self)
    self.platform.log("info", "game_logic init")
end
function GameLogic.prototype.createRole(self, msg)
    if not msg.token then
        return self:makeError(323, 3, "未授权，请先选服")
    end
    local claims = token_validate_gateway(msg.token)
    if not claims then
        return self:makeError(323, 3, "Token无效或已过期")
    end
    local req = pb_decode("game.CreateRoleRequest", msg.data)
    if not req then
        return self:makeError(323, 2, "无效的请求格式")
    end
    local roleName = req.role_name or ""
    if #roleName < 2 then
        return self:makeError(323, 204, "角色名太短")
    end
    if #roleName > 12 then
        return self:makeError(323, 205, "角色名太长")
    end
    local nameExists = self.platform.serviceCall("db_service", "checkRoleNameExists", claims.server_id, roleName)
    if nameExists then
        return self:makeError(323, 201, "角色名已存在")
    end
    local roleCount = self.platform.serviceCall("db_service", "countRolesByAccountAndServer", claims.account_id, claims.server_id)
    if roleCount >= 3 then
        return self:makeError(323, 202, "角色数量已达上限")
    end
    local roleId = self.platform.serviceCall("db_service", "getNextRoleId")
    local now = math.floor(skynet.time())
    local roleData = {
        role_id = roleId,
        account_id = claims.account_id,
        server_id = claims.server_id,
        role_name = roleName,
        level = 1,
        exp = 0,
        avatar_id = 0,
        gold = 10000,
        diamond = 100,
        total_power = 100,
        vip_level = 0,
        create_time = now,
        last_login_time = now
    }
    self.platform.serviceCall("db_service", "createRole", roleData)
    local response = {
        code = 0,
        message = "",
        role_info = {
            role_id = roleId,
            role_name = roleName,
            level = 1,
            exp = 0,
            avatar_id = 0,
            gold = 10000,
            diamond = 100,
            total_power = 100,
            vip_level = 0,
            create_time = now,
            last_login_time = now
        },
        items = {},
        tasks = {},
        server_time = now
    }
    local rspData = pb_encode("game.CreateRoleResponse", response)
    self.platform.log(
        "info",
        (("CreateRole: " .. roleName) .. " roleId=") .. tostring(roleId)
    )
    return {msg_id = 323, data = rspData}
end
function GameLogic.prototype.enterGame(self, msg)
    if not msg.token then
        return self:makeError(321, 3, "未授权，请先选服")
    end
    local claims = token_validate_gateway(msg.token)
    if not claims then
        return self:makeError(321, 3, "Token无效或已过期")
    end
    local req = pb_decode("game.EnterGameRequest", msg.data)
    if not req then
        return self:makeError(321, 2, "无效的请求格式")
    end
    local roleId = req.role_id or 0
    if roleId == 0 then
        return self:makeError(321, 2, "角色ID不能为空")
    end
    local role = self.platform.serviceCall("db_service", "findRoleById", roleId)
    if not role then
        return self:makeError(321, 200, "角色不存在")
    end
    if role.account_id ~= claims.account_id or role.server_id ~= claims.server_id then
        return self:makeError(321, 4, "无权操作此角色")
    end
    local now = math.floor(skynet.time())
    self.platform.serviceSend("db_service", "updateRole", roleId, {last_login_time = now})
    local response = {
        code = 0,
        message = "",
        role_info = {
            role_id = role.role_id or 0,
            role_name = role.role_name or "",
            level = role.level or 1,
            exp = role.exp or 0,
            avatar_id = role.avatar_id or 0,
            gold = role.gold or 0,
            diamond = role.diamond or 0,
            total_power = role.total_power or 0,
            vip_level = role.vip_level or 0,
            create_time = role.create_time or 0,
            last_login_time = now
        },
        items = {},
        tasks = {},
        server_time = now
    }
    local rspData = pb_encode("game.EnterGameResponse", response)
    self.platform.log(
        "info",
        (("EnterGame: roleId=" .. tostring(roleId)) .. " name=") .. tostring(role.role_name or "?")
    )
    return {msg_id = 321, data = rspData}
end
function GameLogic.prototype.makeError(self, msgId, code, message)
    local rspData = pb_encode("common.Response", {code = code, message = message, data = ""})
    return {msg_id = msgId, data = rspData}
end
return ____exports
