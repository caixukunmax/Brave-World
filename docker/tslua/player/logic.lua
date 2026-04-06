local ____lualib = require("lualib_bundle")
local __TS__Class = ____lualib.__TS__Class
local Map = ____lualib.Map
local __TS__New = ____lualib.__TS__New
local __TS__SourceMapTraceBack = ____lualib.__TS__SourceMapTraceBack
__TS__SourceMapTraceBack(debug.getinfo(1).short_src, {["8"] = 3,["9"] = 3,["10"] = 3,["11"] = 7,["12"] = 5,["13"] = 8,["14"] = 7,["15"] = 11,["16"] = 12,["17"] = 13,["18"] = 14,["19"] = 15,["21"] = 17,["22"] = 18,["23"] = 11,["24"] = 21,["25"] = 22,["26"] = 21,["27"] = 25,["28"] = 26,["29"] = 27,["30"] = 28,["31"] = 25});
local ____exports = {}
____exports.PlayerLogic = __TS__Class()
local PlayerLogic = ____exports.PlayerLogic
PlayerLogic.name = "PlayerLogic"
function PlayerLogic.prototype.____constructor(self, platform)
    self.players = __TS__New(Map)
    self.platform = platform
end
function PlayerLogic.prototype.login(self, userId, token)
    self.platform.log("info", "Player login", {userId = userId})
    local player = self.platform.serviceCall("db_service", "queryPlayer", userId)
    if not player then
        return {success = false, error = "Player not found"}
    end
    self.players:set(userId, player)
    return {success = true, sessionId = "s_" .. userId}
end
function PlayerLogic.prototype.getOnlineCount(self)
    return self.players.size
end
function PlayerLogic.prototype.kick(self, userId)
    self.players:delete(userId)
    self.platform.serviceSend("gateway", "kickUser", userId)
    self.platform.log("info", "Player kicked", {userId = userId})
end
return ____exports
