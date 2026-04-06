local ____lualib = require("lualib_bundle")
local __TS__Class = ____lualib.__TS__Class
local __TS__Number = ____lualib.__TS__Number
local __TS__SourceMapTraceBack = ____lualib.__TS__SourceMapTraceBack
__TS__SourceMapTraceBack(debug.getinfo(1).short_src, {["7"] = 3,["8"] = 3,["9"] = 3,["10"] = 7,["11"] = 8,["12"] = 9,["13"] = 7,["14"] = 12,["15"] = 13,["16"] = 14,["17"] = 15,["18"] = 15,["19"] = 15,["20"] = 15,["21"] = 15,["22"] = 17,["23"] = 18,["24"] = 19,["25"] = 21,["26"] = 12,["27"] = 24,["28"] = 25,["29"] = 26,["30"] = 27,["32"] = 29,["33"] = 24,["34"] = 37,["35"] = 38,["36"] = 37,["37"] = 46,["38"] = 47,["39"] = 46});
local ____exports = {}
____exports.DbLogic = __TS__Class()
local DbLogic = ____exports.DbLogic
DbLogic.name = "DbLogic"
function DbLogic.prototype.____constructor(self, platform)
    self.platform = platform
    self.playersCol = nil
end
function DbLogic.prototype.init(self)
    local host = skynet.getenv("MONGO_HOST") or "127.0.0.1"
    local port = __TS__Number(skynet.getenv("MONGO_PORT") or "27017")
    self.platform.log(
        "info",
        "Connecting to MongoDB",
        (host .. ":") .. tostring(port)
    )
    local client = mongo.client({host = host, port = port})
    local db = client.tslua2
    self.playersCol = db.players
    self.platform.log("info", "MongoDB connected")
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
return ____exports
