--------------------------------------------------------------------------------
-- 消息编解码（由 build_proto 自动生成，请勿手动修改）
-- Source: server.proto  Package: server
--------------------------------------------------------------------------------
local pb = require "pb"

local M = {}

-- Fields: server_id(uint32) -- 区服ID server_name(string) -- 区服名称 host(string) -- 网关地址 port(uint32) -- 网关端口 status(common.ServerStatus) -- 区服状态 online_count(uint32) -- 在线人数 is_new(bool) -- 是否新服 is_recommend(bool) -- 是否推荐 has_role(bool) -- 当前账号在此服是否有角色 role_count(uint32) -- 当前账号在此服的角色数量
M.ServerInfo = {
    encode = function(data) return pb.encode("server.ServerInfo", data) end,
    decode = function(data) return pb.decode("server.ServerInfo", data) end,
}

-- Fields: account_token(string) -- 账号Token（可选，用于返回has_role）
M.GetServerListRequest = {
    encode = function(data) return pb.encode("server.GetServerListRequest", data) end,
    decode = function(data) return pb.decode("server.GetServerListRequest", data) end,
}

-- Fields: code(common.ErrorCode) message(string) servers(ServerInfo[]) -- 区服列表 last_server_id(uint32) -- 最近登录的区服
M.GetServerListResponse = {
    encode = function(data) return pb.encode("server.GetServerListResponse", data) end,
    decode = function(data) return pb.decode("server.GetServerListResponse", data) end,
}

-- Fields: server_id(uint32) status(common.ServerStatus) online_count(uint32)
M.ServerStatusUpdate = {
    encode = function(data) return pb.encode("server.ServerStatusUpdate", data) end,
    decode = function(data) return pb.decode("server.ServerStatusUpdate", data) end,
}

return M
