local ____lualib = require("lualib_bundle")
local __TS__New = ____lualib.__TS__New
local __TS__SourceMapTraceBack = ____lualib.__TS__SourceMapTraceBack
__TS__SourceMapTraceBack(debug.getinfo(1).short_src, {["6"] = 1,["7"] = 1,["8"] = 2,["9"] = 2,["10"] = 3,["11"] = 3,["12"] = 5,["13"] = 7,["14"] = 7,["15"] = 9,["16"] = 10,["17"] = 7,["18"] = 12,["19"] = 13,["20"] = 7,["21"] = 15,["22"] = 16,["23"] = 7,["24"] = 20,["25"] = 21,["26"] = 7,["27"] = 23,["28"] = 24,["29"] = 7,["30"] = 26,["31"] = 27,["32"] = 7,["33"] = 29,["34"] = 30,["35"] = 7,["36"] = 34,["37"] = 35,["38"] = 7,["39"] = 37,["40"] = 38,["41"] = 7,["42"] = 40,["43"] = 41,["44"] = 7,["45"] = 43,["46"] = 44,["47"] = 7,["48"] = 46,["49"] = 47,["50"] = 7,["51"] = 49,["52"] = 50,["53"] = 7,["54"] = 52,["55"] = 53,["56"] = 7,["57"] = 57,["58"] = 58,["59"] = 7,["60"] = 60,["61"] = 61,["62"] = 7,["63"] = 63,["64"] = 64,["65"] = 7,["66"] = 7,["67"] = 66,["68"] = 67,["69"] = 68,["70"] = 7,["71"] = 7});
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
        end,
        findAccountByUsername = function(self, username)
            return logic:findAccountByUsername(username)
        end,
        findAccountById = function(self, accountId)
            return logic:findAccountById(accountId)
        end,
        createAccount = function(self, username, password)
            return logic:createAccount(username, password)
        end,
        updateAccountLastServer = function(self, accountId, serverId, roleName)
            logic:updateAccountLastServer(accountId, serverId, roleName)
        end,
        findRolesByAccountAndServer = function(self, accountId, serverId)
            return logic:findRolesByAccountAndServer(accountId, serverId)
        end,
        findRoleById = function(self, roleId)
            return logic:findRoleById(roleId)
        end,
        createRole = function(self, roleData)
            logic:createRole(roleData)
        end,
        updateRole = function(self, roleId, updates)
            logic:updateRole(roleId, updates)
        end,
        checkRoleNameExists = function(self, serverId, roleName)
            return logic:checkRoleNameExists(serverId, roleName)
        end,
        countRolesByAccountAndServer = function(self, accountId, serverId)
            return logic:countRolesByAccountAndServer(accountId, serverId)
        end,
        getNextRoleId = function(self)
            return logic:getNextRoleId()
        end,
        getServers = function(self)
            return logic:getServers()
        end,
        findServerById = function(self, serverId)
            return logic:findServerById(serverId)
        end,
        updateServerOnlineCount = function(self, serverId, count)
            logic:updateServerOnlineCount(serverId, count)
        end
    },
    function()
        logic:init()
        platform.log("info", "db_service started")
    end
)
return ____exports
