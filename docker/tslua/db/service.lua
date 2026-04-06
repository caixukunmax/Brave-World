local ____lualib = require("lualib_bundle")
local __TS__New = ____lualib.__TS__New
local __TS__SourceMapTraceBack = ____lualib.__TS__SourceMapTraceBack
__TS__SourceMapTraceBack(debug.getinfo(1).short_src, {["6"] = 1,["7"] = 1,["8"] = 2,["9"] = 2,["10"] = 3,["11"] = 3,["12"] = 5,["13"] = 7,["14"] = 7,["15"] = 8,["16"] = 9,["17"] = 7,["18"] = 11,["19"] = 12,["20"] = 7,["21"] = 14,["22"] = 15,["23"] = 7,["24"] = 7,["25"] = 17,["26"] = 18,["27"] = 19,["28"] = 7,["29"] = 7});
local ____exports = {}
local ____service = require("service")
local defineService = ____service.defineService
local ____platform = require("platform")
local platform = ____platform.platform
local ____logic = require("db.logic")
local DbLogic = ____logic.DbLogic
local logic = __TS__New(DbLogic, platform)
defineService(
    {
        queryPlayer = function(self, userId)
            return logic:queryPlayer(userId)
        end,
        savePlayer = function(self, player)
            logic:savePlayer(player)
        end,
        createPlayer = function(self, player)
            logic:createPlayer(player)
        end
    },
    function()
        logic:init()
        platform.log("info", "db_service started")
    end
)
return ____exports
