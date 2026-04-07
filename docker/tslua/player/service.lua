local ____lualib = require("lualib_bundle")
local __TS__New = ____lualib.__TS__New
local __TS__SourceMapTraceBack = ____lualib.__TS__SourceMapTraceBack
__TS__SourceMapTraceBack(debug.getinfo(1).short_src, {["6"] = 1,["7"] = 1,["8"] = 2,["9"] = 2,["10"] = 3,["11"] = 3,["12"] = 5,["13"] = 7,["14"] = 7,["15"] = 7,["16"] = 8,["17"] = 9,["18"] = 7,["19"] = 12,["20"] = 13,["21"] = 7,["22"] = 16,["23"] = 17,["24"] = 7,["25"] = 7,["26"] = 19,["27"] = 21,["28"] = 7,["29"] = 7});
local ____exports = {}
local ____service = require("service")
local defineService = ____service.defineService
local ____platform = require("platform")
local platform = ____platform.platform
local ____logic = require("player.logic")
local PlayerLogic = ____logic.PlayerLogic
local logic = __TS__New(PlayerLogic, platform)
defineService(
    "player",
    {
        login = function(msg)
            return logic:login(msg.userId, msg.token)
        end,
        kick = function(userId)
            logic:kick(userId)
        end,
        getOnlineCount = function()
            return logic:getOnlineCount()
        end
    },
    {init = function(self)
        platform.log("info", "player_service started")
    end}
)
return ____exports
