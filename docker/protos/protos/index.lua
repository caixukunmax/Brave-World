--------------------------------------------------------------------------------
-- 协议索引（由 build_proto 自动生成，请勿手动修改）
--------------------------------------------------------------------------------

local common_enum = require "protos.common_enum"
local gateway_enum = require "protos.gateway_enum"
local message_id_enum = require "protos.message_id_enum"
local common_proto = require "protos.common_proto"
local game_proto = require "protos.game_proto"
local gateway_proto = require "protos.gateway_proto"
local login_proto = require "protos.login_proto"
local server_proto = require "protos.server_proto"

return {
    common = setmetatable({}, {
        __index = function(_, key)
            local v = rawget(common_enum, key) or rawget(common_proto, key)
            if v ~= nil then return v end
        end,
    }),
    gateway = setmetatable({}, {
        __index = function(_, key)
            local v = rawget(gateway_enum, key) or rawget(gateway_proto, key)
            if v ~= nil then return v end
        end,
    }),
    message_id = message_id_enum,
    game = game_proto,
    login = login_proto,
    server = server_proto,
}
