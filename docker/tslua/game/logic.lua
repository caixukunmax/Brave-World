local ____lualib = require("lualib_bundle")
local __TS__Class = ____lualib.__TS__Class
local __TS__New = ____lualib.__TS__New
local __TS__SourceMapTraceBack = ____lualib.__TS__SourceMapTraceBack
__TS__SourceMapTraceBack(debug.getinfo(1).short_src, {["7"] = 3,["8"] = 3,["9"] = 3,["10"] = 3,["11"] = 5,["12"] = 5,["13"] = 5,["14"] = 8,["15"] = 9,["16"] = 8,["17"] = 12,["18"] = 13,["19"] = 12,["20"] = 19,["21"] = 21,["22"] = 23,["24"] = 25,["25"] = 26,["26"] = 28,["28"] = 32,["29"] = 33,["30"] = 35,["32"] = 38,["33"] = 41,["34"] = 43,["36"] = 45,["37"] = 47,["39"] = 51,["40"] = 52,["41"] = 54,["43"] = 58,["44"] = 59,["45"] = 61,["47"] = 65,["48"] = 66,["49"] = 68,["50"] = 68,["51"] = 68,["52"] = 68,["53"] = 68,["54"] = 68,["55"] = 68,["56"] = 68,["57"] = 68,["58"] = 68,["59"] = 68,["60"] = 68,["61"] = 68,["62"] = 68,["63"] = 68,["64"] = 84,["65"] = 87,["66"] = 87,["67"] = 87,["68"] = 90,["69"] = 90,["70"] = 90,["71"] = 90,["72"] = 90,["73"] = 90,["74"] = 90,["75"] = 90,["76"] = 90,["77"] = 90,["78"] = 90,["79"] = 90,["80"] = 87,["81"] = 87,["82"] = 87,["83"] = 87,["84"] = 87,["85"] = 108,["86"] = 109,["87"] = 109,["88"] = 109,["89"] = 109,["90"] = 111,["91"] = 19,["92"] = 117,["93"] = 119,["94"] = 121,["96"] = 123,["97"] = 124,["98"] = 126,["100"] = 130,["101"] = 131,["102"] = 133,["104"] = 136,["105"] = 137,["106"] = 139,["108"] = 143,["109"] = 144,["110"] = 146,["112"] = 150,["113"] = 152,["115"] = 156,["116"] = 157,["117"] = 160,["118"] = 160,["119"] = 160,["120"] = 163,["121"] = 163,["122"] = 163,["123"] = 163,["124"] = 163,["125"] = 163,["126"] = 163,["127"] = 163,["128"] = 163,["129"] = 163,["130"] = 163,["131"] = 163,["132"] = 160,["133"] = 160,["134"] = 160,["135"] = 160,["136"] = 160,["137"] = 181,["138"] = 182,["139"] = 182,["140"] = 182,["141"] = 182,["142"] = 184,["143"] = 117,["144"] = 187,["145"] = 188,["146"] = 188,["147"] = 188,["148"] = 188,["149"] = 188,["150"] = 189,["151"] = 187});
local ____exports = {}
local ____protos = require("protos.index")
local proto = ____protos.proto
local MessageId = ____protos.MessageId
local ErrorCode = ____protos.ErrorCode
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
        return self:makeError(MessageId.GAME_CREATE_ROLE_RSP, ErrorCode.UNAUTHORIZED)
    end
    local claims = token_validate_gateway(msg.token)
    if not claims then
        return self:makeError(MessageId.GAME_CREATE_ROLE_RSP, ErrorCode.UNAUTHORIZED)
    end
    local req = proto.game.CreateRoleRequest.decode(msg.data)
    if not req then
        return self:makeError(MessageId.GAME_CREATE_ROLE_RSP, ErrorCode.INVALID_REQUEST)
    end
    local roleName = req.role_name or ""
    if #roleName < 2 then
        return self:makeError(MessageId.GAME_CREATE_ROLE_RSP, ErrorCode.ROLE_NAME_TOO_SHORT)
    end
    if #roleName > 12 then
        return self:makeError(MessageId.GAME_CREATE_ROLE_RSP, ErrorCode.ROLE_NAME_TOO_LONG)
    end
    local nameExists = self.platform.serviceCall("db_service", "checkRoleNameExists", claims.server_id, roleName)
    if nameExists then
        return self:makeError(MessageId.GAME_CREATE_ROLE_RSP, ErrorCode.ROLE_NAME_EXISTS)
    end
    local roleCount = self.platform.serviceCall("db_service", "countRolesByAccountAndServer", claims.account_id, claims.server_id)
    if roleCount >= 3 then
        return self:makeError(MessageId.GAME_CREATE_ROLE_RSP, ErrorCode.ROLE_COUNT_LIMIT)
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
    local rspData = proto.game.CreateRoleResponse.encode(response)
    self.platform.log(
        "info",
        (("CreateRole: " .. roleName) .. " roleId=") .. tostring(roleId)
    )
    return {msg_id = MessageId.GAME_CREATE_ROLE_RSP, data = rspData}
end
function GameLogic.prototype.enterGame(self, msg)
    if not msg.token then
        return self:makeError(MessageId.GAME_ENTER_GAME_RSP, ErrorCode.UNAUTHORIZED)
    end
    local claims = token_validate_gateway(msg.token)
    if not claims then
        return self:makeError(MessageId.GAME_ENTER_GAME_RSP, ErrorCode.UNAUTHORIZED)
    end
    local req = proto.game.EnterGameRequest.decode(msg.data)
    if not req then
        return self:makeError(MessageId.GAME_ENTER_GAME_RSP, ErrorCode.INVALID_REQUEST)
    end
    local roleId = req.role_id or 0
    if roleId == 0 then
        return self:makeError(MessageId.GAME_ENTER_GAME_RSP, ErrorCode.INVALID_REQUEST)
    end
    local role = self.platform.serviceCall("db_service", "findRoleById", roleId)
    if not role then
        return self:makeError(MessageId.GAME_ENTER_GAME_RSP, ErrorCode.ROLE_NOT_FOUND)
    end
    if role.account_id ~= claims.account_id or role.server_id ~= claims.server_id then
        return self:makeError(MessageId.GAME_ENTER_GAME_RSP, ErrorCode.FORBIDDEN)
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
    local rspData = proto.game.EnterGameResponse.encode(response)
    self.platform.log(
        "info",
        (("EnterGame: roleId=" .. tostring(roleId)) .. " name=") .. tostring(role.role_name or "?")
    )
    return {msg_id = MessageId.GAME_ENTER_GAME_RSP, data = rspData}
end
function GameLogic.prototype.makeError(self, msgId, code)
    local rspData = proto.common.Response.encode({
        code = code,
        message = "",
        data = __TS__New(Uint8Array, 0)
    })
    return {msg_id = msgId, data = rspData}
end
return ____exports
