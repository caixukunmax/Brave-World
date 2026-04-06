local ____lualib = require("lualib_bundle")
local __TS__New = ____lualib.__TS__New
local __TS__SourceMapTraceBack = ____lualib.__TS__SourceMapTraceBack
__TS__SourceMapTraceBack(debug.getinfo(1).short_src, {["6"] = 1,["7"] = 1,["8"] = 2,["9"] = 2,["10"] = 3,["11"] = 3,["12"] = 5,["13"] = 7,["14"] = 7,["15"] = 8,["16"] = 9,["17"] = 7,["18"] = 12,["19"] = 13,["20"] = 7,["21"] = 16,["22"] = 17,["23"] = 7,["24"] = 7,["25"] = 19,["26"] = 20,["27"] = 7,["28"] = 7});
local ____exports = {}
local ____service = require("service")
local defineService = ____service.defineService
local ____platform = require("platform")
local platform = ____platform.platform
local ____logic = require("player.logic")
local PlayerLogic = ____logic.PlayerLogic
local logic = __TS__New(PlayerLogic, platform)
defineService(
    {
        login = function(self, msg)
            return logic:login(msg.userId, msg.token)
        end,
        kick = function(self, userId)
            logic:kick(userId)
        end,
        getOnlineCount = function(self)
            return logic:getOnlineCount()
        end
    },
    function()
        platform.log("info", "player_service started")
    end
)
return ____exports
