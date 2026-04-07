local ____lualib = require("lualib_bundle")
local __TS__New = ____lualib.__TS__New
local __TS__SourceMapTraceBack = ____lualib.__TS__SourceMapTraceBack
__TS__SourceMapTraceBack(debug.getinfo(1).short_src, {["6"] = 1,["7"] = 1,["8"] = 2,["9"] = 2,["10"] = 3,["11"] = 3,["12"] = 4,["13"] = 4,["14"] = 6,["15"] = 8,["16"] = 8,["17"] = 8,["18"] = 8,["19"] = 8,["20"] = 8,["21"] = 11,["22"] = 13,["23"] = 14,["24"] = 8,["25"] = 8});
local ____exports = {}
local ____service = require("service")
local defineService = ____service.defineService
local ____platform = require("platform")
local platform = ____platform.platform
local ____logic = require("game.logic")
local GameLogic = ____logic.GameLogic
local ____message_id = require("protos.message_id")
local MessageId = ____message_id.MessageId
local logic = __TS__New(GameLogic, platform)
defineService(
    "game",
    {
        [MessageId.GAME_ENTER_GAME_REQ] = function(msg) return logic:enterGame(msg) end,
        [MessageId.GAME_CREATE_ROLE_REQ] = function(msg) return logic:createRole(msg) end
    },
    {init = function(self)
        logic:init()
        platform.log("info", "game_service started")
    end}
)
return ____exports
