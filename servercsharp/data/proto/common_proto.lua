--------------------------------------------------------------------------------
-- 消息编解码（由 build_proto 自动生成，请勿手动修改）
-- Source: common.proto  Package: common
--------------------------------------------------------------------------------
local pb = require "pb"

local M = {}

-- Fields: msg_id(uint32) -- 消息ID session(uint32) -- 会话ID（用于请求响应匹配） data(bytes) -- 消息体（具体消息的序列化数据） timestamp(uint64) -- 时间戳
M.Packet = {
    encode = function(data) return pb.encode("common.Packet", data) end,
    decode = function(data) return pb.decode("common.Packet", data) end,
}

-- Fields: code(ErrorCode) -- 错误码 message(string) -- 错误信息 data(bytes) -- 响应数据
M.Response = {
    encode = function(data) return pb.encode("common.Response", data) end,
    decode = function(data) return pb.decode("common.Response", data) end,
}

return M
