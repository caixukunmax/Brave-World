local ____lualib = require("lualib_bundle")
local __TS__Class = ____lualib.__TS__Class
local __TS__Number = ____lualib.__TS__Number
local __TS__SourceMapTraceBack = ____lualib.__TS__SourceMapTraceBack
__TS__SourceMapTraceBack(debug.getinfo(1).short_src, {["7"] = 4,["8"] = 4,["9"] = 4,["10"] = 12,["11"] = 13,["12"] = 14,["13"] = 15,["14"] = 16,["15"] = 17,["16"] = 12,["17"] = 20,["18"] = 22,["19"] = 23,["20"] = 24,["21"] = 24,["22"] = 24,["23"] = 24,["24"] = 24,["25"] = 26,["26"] = 27,["27"] = 28,["28"] = 29,["29"] = 30,["30"] = 31,["31"] = 32,["32"] = 34,["33"] = 37,["34"] = 20,["35"] = 40,["38"] = 43,["39"] = 45,["42"] = 40,["43"] = 52,["44"] = 54,["45"] = 62,["46"] = 64,["47"] = 69,["49"] = 72,["50"] = 52,["51"] = 77,["52"] = 78,["53"] = 79,["54"] = 80,["56"] = 82,["57"] = 77,["58"] = 90,["59"] = 91,["60"] = 90,["61"] = 99,["62"] = 100,["63"] = 99,["64"] = 110,["65"] = 111,["66"] = 110,["67"] = 114,["68"] = 115,["69"] = 114,["70"] = 118,["71"] = 120,["72"] = 123,["73"] = 125,["74"] = 125,["75"] = 125,["76"] = 125,["77"] = 125,["78"] = 125,["79"] = 125,["80"] = 125,["81"] = 125,["82"] = 134,["83"] = 135,["84"] = 118,["85"] = 138,["86"] = 139,["87"] = 138,["88"] = 147,["89"] = 148,["90"] = 147,["91"] = 158,["92"] = 161,["93"] = 158,["94"] = 164,["95"] = 165,["96"] = 164,["97"] = 168,["98"] = 169,["99"] = 168,["100"] = 172,["101"] = 173,["102"] = 172,["103"] = 181,["104"] = 182,["105"] = 183,["106"] = 181,["107"] = 186,["108"] = 187,["109"] = 186,["110"] = 190,["111"] = 191,["112"] = 190,["113"] = 196,["114"] = 197,["115"] = 196,["116"] = 200,["117"] = 201,["118"] = 200,["119"] = 204,["120"] = 205,["121"] = 204});
local ____exports = {}
____exports.DbLogic = __TS__Class()
local DbLogic = ____exports.DbLogic
DbLogic.name = "DbLogic"
function DbLogic.prototype.____constructor(self, platform)
    self.platform = platform
    self.playersCol = nil
    self.accountsCol = nil
    self.rolesCol = nil
    self.serversCol = nil
end
function DbLogic.prototype.init(self)
    local host = os.getenv("MONGO_HOST") or skynet.getenv("MONGO_HOST") or "127.0.0.1"
    local port = __TS__Number(os.getenv("MONGO_PORT") or skynet.getenv("MONGO_PORT") or "27017")
    self.platform.log(
        "info",
        "Connecting to MongoDB",
        (tostring(host) .. ":") .. tostring(port)
    )
    local client = mongo.client({host = host, port = port})
    local db = client.tslua2
    self.playersCol = db.players
    self.accountsCol = db.accounts
    self.rolesCol = db.roles
    self.serversCol = db.servers
    self.countersCol = db.counters
    self.platform.log("info", "MongoDB connected")
    self:ensureIndexes()
end
function DbLogic.prototype.ensureIndexes(self)
    do
        pcall(function()
            mongo_ensureIndex(self.accountsCol, {key = {username = 1}, unique = true, name = "username_idx"})
            mongo_ensureIndex(self.rolesCol, {key = {server_id = 1, role_name = 1}, unique = true, name = "server_role_name_idx"})
        end)
    end
end
function DbLogic.prototype.getNextId(self, counterName)
    local result = mongo_findAndModify(self.countersCol, {query = {_id = counterName}, update = {["$inc"] = {seq = 1}}, upsert = true, new = true})
    if not result or result.seq == 1 then
        mongo_update(self.countersCol, {_id = counterName}, {["$set"] = {seq = 1000}})
        return 1000
    end
    return result.seq
end
function DbLogic.prototype.queryPlayer(self, userId)
    local doc = mongo_findOne(self.playersCol, {user_id = userId})
    if not doc then
        return nil
    end
    return {userId = doc.user_id, name = doc.name, level = doc.level, gold = doc.gold}
end
function DbLogic.prototype.savePlayer(self, player)
    mongo_update(self.playersCol, {user_id = player.userId}, {["$set"] = {name = player.name, level = player.level, gold = player.gold}}, true)
end
function DbLogic.prototype.createPlayer(self, player)
    mongo_insert(self.playersCol, {user_id = player.userId, name = player.name, level = player.level, gold = player.gold})
end
function DbLogic.prototype.findAccountByUsername(self, username)
    return mongo_findOne(self.accountsCol, {username = username})
end
function DbLogic.prototype.findAccountById(self, accountId)
    return mongo_findOne(self.accountsCol, {account_id = accountId})
end
function DbLogic.prototype.createAccount(self, username, password)
    local accountId = self:getNextId("account_id")
    local hashedPassword = password_hash(password)
    local doc = {
        account_id = accountId,
        username = username,
        password = hashedPassword,
        status = 0,
        last_server_id = 0,
        last_role_name = "",
        created_at = skynet.time()
    }
    mongo_insert(self.accountsCol, doc)
    return doc
end
function DbLogic.prototype.updateAccountLastServer(self, accountId, serverId, roleName)
    mongo_update(self.accountsCol, {account_id = accountId}, {["$set"] = {last_server_id = serverId, last_role_name = roleName}}, false)
end
function DbLogic.prototype.updateAccountPassword(self, accountId, hashedPassword)
    mongo_update(self.accountsCol, {account_id = accountId}, {["$set"] = {password = hashedPassword}}, false)
end
function DbLogic.prototype.findRolesByAccountAndServer(self, accountId, serverId)
    return mongo_findArray(self.rolesCol, {account_id = accountId, server_id = serverId})
end
function DbLogic.prototype.findRoleById(self, roleId)
    return mongo_findOne(self.rolesCol, {role_id = roleId})
end
function DbLogic.prototype.createRole(self, roleData)
    mongo_insert(self.rolesCol, roleData)
end
function DbLogic.prototype.updateRole(self, roleId, updates)
    mongo_update(self.rolesCol, {role_id = roleId}, {["$set"] = updates}, false)
end
function DbLogic.prototype.checkRoleNameExists(self, serverId, roleName)
    local doc = mongo_findOne(self.rolesCol, {server_id = serverId, role_name = roleName})
    return doc ~= nil
end
function DbLogic.prototype.countRolesByAccountAndServer(self, accountId, serverId)
    return mongo_count(self.rolesCol, {account_id = accountId, server_id = serverId})
end
function DbLogic.prototype.getNextRoleId(self)
    return self:getNextId("role_id")
end
function DbLogic.prototype.getServers(self)
    return mongo_findArray(self.serversCol, {})
end
function DbLogic.prototype.findServerById(self, serverId)
    return mongo_findOne(self.serversCol, {server_id = serverId})
end
function DbLogic.prototype.updateServerOnlineCount(self, serverId, count)
    mongo_update(self.serversCol, {server_id = serverId}, {["$set"] = {online_count = count}}, false)
end
return ____exports
