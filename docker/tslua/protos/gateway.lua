local ____lualib = require("lualib_bundle")
local __TS__SourceMapTraceBack = ____lualib.__TS__SourceMapTraceBack
__TS__SourceMapTraceBack(debug.getinfo(1).short_src, {["5"] = 10,["7"] = 13,["8"] = 15,["9"] = 15,["10"] = 17,["11"] = 17,["12"] = 19,["13"] = 19,["14"] = 21,["15"] = 21,["16"] = 23,["17"] = 23,["18"] = 25,["19"] = 25,["20"] = 26,["21"] = 26});
local ____exports = {}
____exports.protobufPackage = "gateway"
--- 消息类型枚举
____exports.MessageType = ____exports.MessageType or ({})
____exports.MessageType.HEARTBEAT = 0
____exports.MessageType[____exports.MessageType.HEARTBEAT] = "HEARTBEAT"
____exports.MessageType.CONNECT = 1
____exports.MessageType[____exports.MessageType.CONNECT] = "CONNECT"
____exports.MessageType.DISCONNECT = 2
____exports.MessageType[____exports.MessageType.DISCONNECT] = "DISCONNECT"
____exports.MessageType.FORWARD = 3
____exports.MessageType[____exports.MessageType.FORWARD] = "FORWARD"
____exports.MessageType.BROADCAST = 4
____exports.MessageType[____exports.MessageType.BROADCAST] = "BROADCAST"
____exports.MessageType.KICK = 5
____exports.MessageType[____exports.MessageType.KICK] = "KICK"
____exports.MessageType.UNRECOGNIZED = -1
____exports.MessageType[____exports.MessageType.UNRECOGNIZED] = "UNRECOGNIZED"
return ____exports
