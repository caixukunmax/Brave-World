local ____lualib = require("lualib_bundle")
local __TS__New = ____lualib.__TS__New
local __TS__SourceMapTraceBack = ____lualib.__TS__SourceMapTraceBack
__TS__SourceMapTraceBack(debug.getinfo(1).short_src, {["6"] = 1,["7"] = 1,["8"] = 2,["9"] = 2,["10"] = 3,["11"] = 3,["12"] = 5,["13"] = 7,["14"] = 7,["15"] = 8,["16"] = 9,["17"] = 7,["18"] = 11,["19"] = 12,["20"] = 7,["21"] = 7,["22"] = 14,["23"] = 15,["24"] = 17,["25"] = 18,["26"] = 18,["27"] = 18,["28"] = 18,["29"] = 18,["30"] = 18,["31"] = 18,["32"] = 18,["33"] = 18,["34"] = 18,["35"] = 26,["36"] = 7,["37"] = 7});
local ____exports = {}
local ____service = require("service")
local defineService = ____service.defineService
local ____platform = require("platform")
local platform = ____platform.platform
local ____logic = require("login.logic")
local LoginLogic = ____logic.LoginLogic
local logic = __TS__New(LoginLogic, platform)
defineService(
    {
        accountLogin = function(self, msg)
            return logic:accountLogin(msg)
        end,
        selectServer = function(self, msg)
            return logic:selectServer(msg)
        end
    },
    function()
        logic:init()
        local gatewayAddr = skynet.queryservice("gateway_service")
        skynet.send(
            gatewayAddr,
            "lua",
            "register",
            {
                service_name = "login",
                service_addr = skynet.self(),
                routes = {[210] = "accountLogin", [212] = "selectServer"}
            }
        )
        platform.log("info", "login_service started")
    end
)
return ____exports
