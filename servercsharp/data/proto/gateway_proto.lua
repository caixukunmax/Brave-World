--------------------------------------------------------------------------------
-- 消息编解码（由 build_proto 自动生成，请勿手动修改）
-- Source: gateway.proto  Package: gateway
--------------------------------------------------------------------------------
local pb = require "pb"

local M = {}

-- Fields: client_time(uint64) -- 客户端时间戳
M.HeartbeatRequest = {
    encode = function(data) return pb.encode("gateway.HeartbeatRequest", data) end,
    decode = function(data) return pb.decode("gateway.HeartbeatRequest", data) end,
}

-- Fields: server_time(uint64) -- 服务器时间戳 online_count(uint32) -- 在线人数
M.HeartbeatResponse = {
    encode = function(data) return pb.encode("gateway.HeartbeatResponse", data) end,
    decode = function(data) return pb.decode("gateway.HeartbeatResponse", data) end,
}

-- Fields: ip(string) -- IP地址 port(uint32) -- 端口 version(string) -- 客户端版本 platform(string) -- 平台（iOS/Android/Web） device_id(string) -- 设备ID
M.ClientInfo = {
    encode = function(data) return pb.encode("gateway.ClientInfo", data) end,
    decode = function(data) return pb.decode("gateway.ClientInfo", data) end,
}

-- Fields: client_info(ClientInfo) token(string) -- 登录token（可选）
M.ConnectRequest = {
    encode = function(data) return pb.encode("gateway.ConnectRequest", data) end,
    decode = function(data) return pb.decode("gateway.ConnectRequest", data) end,
}

-- Fields: code(common.ErrorCode) message(string) conn_id(uint32) -- 连接ID server_time(uint64) -- 服务器时间
M.ConnectResponse = {
    encode = function(data) return pb.encode("gateway.ConnectResponse", data) end,
    decode = function(data) return pb.decode("gateway.ConnectResponse", data) end,
}

-- Fields: conn_id(uint32) reason(string) -- 断开原因
M.DisconnectNotify = {
    encode = function(data) return pb.encode("gateway.DisconnectNotify", data) end,
    decode = function(data) return pb.decode("gateway.DisconnectNotify", data) end,
}

return M
