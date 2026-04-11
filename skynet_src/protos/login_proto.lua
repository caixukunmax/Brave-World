--------------------------------------------------------------------------------
-- 消息编解码（由 build_proto 自动生成，请勿手动修改）
-- Source: login.proto  Package: login
--------------------------------------------------------------------------------
local pb = require "pb"

local M = {}

-- Fields: username(string) -- 用户名 password(string) -- 密码（MD5 哈希） platform(string) -- 平台：ios/android/pc device_id(string) -- 设备ID client_version(string) -- 客户端版本
M.AccountLoginRequest = {
    encode = function(data) return pb.encode("login.AccountLoginRequest", data) end,
    decode = function(data) return pb.decode("login.AccountLoginRequest", data) end,
}

-- Fields: code(common.ErrorCode) message(string) account_token(string) -- 账号级Token（全区服通用，有效期5分钟） account_id(uint32) -- 账号ID servers(server.ServerInfo[]) -- 区服列表 last_server_id(uint32) -- 最近登录的区服ID last_role_name(string) -- 最近登录的角色名
M.AccountLoginResponse = {
    encode = function(data) return pb.encode("login.AccountLoginResponse", data) end,
    decode = function(data) return pb.decode("login.AccountLoginResponse", data) end,
}

-- Fields: account_token(string) -- 账号Token server_id(uint32) -- 选择的区服ID
M.SelectServerRequest = {
    encode = function(data) return pb.encode("login.SelectServerRequest", data) end,
    decode = function(data) return pb.decode("login.SelectServerRequest", data) end,
}

-- Fields: role_id(uint64) -- 角色ID role_name(string) -- 角色名 level(uint32) -- 等级 avatar_id(uint32) -- 头像ID last_login(uint64) -- 上次登录时间 total_power(uint64) -- 总战力
M.RoleBrief = {
    encode = function(data) return pb.encode("login.RoleBrief", data) end,
    decode = function(data) return pb.decode("login.RoleBrief", data) end,
}

-- Fields: code(common.ErrorCode) message(string) gateway_token(string) -- 区服级Token（区服专用，有效期7天） roles(RoleBrief[]) -- 该区服的角色列表 max_role_count(uint32) -- 最大角色数（来自配置表，默认3） server_time(uint32) -- 服务器时间（校时用）
M.SelectServerResponse = {
    encode = function(data) return pb.encode("login.SelectServerResponse", data) end,
    decode = function(data) return pb.decode("login.SelectServerResponse", data) end,
}

return M
