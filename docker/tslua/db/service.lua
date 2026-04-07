local ____lualib = require("lualib_bundle")
local __TS__New = ____lualib.__TS__New
local __TS__SourceMapTraceBack = ____lualib.__TS__SourceMapTraceBack
__TS__SourceMapTraceBack(debug.getinfo(1).short_src, {["6"] = 1,["7"] = 1,["8"] = 2,["9"] = 2,["10"] = 3,["11"] = 3,["12"] = 5,["13"] = 7,["14"] = 7,["15"] = 7,["16"] = 9,["17"] = 10,["18"] = 7,["19"] = 12,["20"] = 13,["21"] = 7,["22"] = 15,["23"] = 16,["24"] = 7,["25"] = 20,["26"] = 21,["27"] = 7,["28"] = 23,["29"] = 24,["30"] = 7,["31"] = 26,["32"] = 27,["33"] = 7,["34"] = 29,["35"] = 30,["36"] = 7,["37"] = 34,["38"] = 35,["39"] = 7,["40"] = 37,["41"] = 38,["42"] = 7,["43"] = 40,["44"] = 41,["45"] = 7,["46"] = 43,["47"] = 44,["48"] = 7,["49"] = 46,["50"] = 47,["51"] = 7,["52"] = 49,["53"] = 50,["54"] = 7,["55"] = 52,["56"] = 53,["57"] = 7,["58"] = 57,["59"] = 58,["60"] = 7,["61"] = 60,["62"] = 61,["63"] = 7,["64"] = 63,["65"] = 64,["66"] = 7,["67"] = 7,["68"] = 66,["69"] = 68,["70"] = 69,["71"] = 7,["72"] = 7});
local ____exports = {}
local ____service = require("service")
local defineService = ____service.defineService
local ____platform = require("platform")
local platform = ____platform.platform
local ____logic = require("db.logic")
local DbLogic = ____logic.DbLogic
local logic = __TS__New(DbLogic, platform)
defineService(
    "db",
    {
        queryPlayer = function(userId)
            return logic:queryPlayer(userId)
        end,
        savePlayer = function(player)
            logic:savePlayer(player)
        end,
        createPlayer = function(player)
            logic:createPlayer(player)
        end,
        findAccountByUsername = function(username)
            return logic:findAccountByUsername(username)
        end,
        findAccountById = function(accountId)
            return logic:findAccountById(accountId)
        end,
        createAccount = function(username, password)
            return logic:createAccount(username, password)
        end,
        updateAccountLastServer = function(accountId, serverId, roleName)
            logic:updateAccountLastServer(accountId, serverId, roleName)
        end,
        findRolesByAccountAndServer = function(accountId, serverId)
            return logic:findRolesByAccountAndServer(accountId, serverId)
        end,
        findRoleById = function(roleId)
            return logic:findRoleById(roleId)
        end,
        createRole = function(roleData)
            logic:createRole(roleData)
        end,
        updateRole = function(roleId, updates)
            logic:updateRole(roleId, updates)
        end,
        checkRoleNameExists = function(serverId, roleName)
            return logic:checkRoleNameExists(serverId, roleName)
        end,
        countRolesByAccountAndServer = function(accountId, serverId)
            return logic:countRolesByAccountAndServer(accountId, serverId)
        end,
        getNextRoleId = function()
            return logic:getNextRoleId()
        end,
        getServers = function()
            return logic:getServers()
        end,
        findServerById = function(serverId)
            return logic:findServerById(serverId)
        end,
        updateServerOnlineCount = function(serverId, count)
            logic:updateServerOnlineCount(serverId, count)
        end
    },
    {init = function(self)
        logic:init()
        platform.log("info", "db_service started")
    end}
)
return ____exports
