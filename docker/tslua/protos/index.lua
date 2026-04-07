local ____lualib = require("lualib_bundle")
local __TS__ObjectAssign = ____lualib.__TS__ObjectAssign
local __TS__New = ____lualib.__TS__New
local __TS__SourceMapTraceBack = ____lualib.__TS__SourceMapTraceBack
__TS__SourceMapTraceBack(debug.getinfo(1).short_src, {["7"] = 17,["8"] = 17,["9"] = 17,["10"] = 20,["11"] = 20,["12"] = 23,["13"] = 23,["15"] = 8,["16"] = 8,["17"] = 8,["20"] = 11,["21"] = 11,["24"] = 14,["25"] = 14,["27"] = 27,["28"] = 28,["29"] = 27,["30"] = 32,["31"] = 33,["32"] = 33,["33"] = 33,["34"] = 36,["35"] = 38,["36"] = 38,["37"] = 38,["38"] = 38,["39"] = 38,["40"] = 38,["41"] = 38,["42"] = 33,["43"] = 40,["44"] = 42,["45"] = 42,["46"] = 42,["47"] = 42,["48"] = 42,["49"] = 42,["50"] = 33,["51"] = 32,["52"] = 45,["53"] = 46,["54"] = 48,["55"] = 48,["56"] = 48,["57"] = 48,["58"] = 48,["59"] = 48,["60"] = 48,["61"] = 48,["62"] = 48,["63"] = 48,["64"] = 48,["65"] = 45,["66"] = 45,["67"] = 45,["68"] = 45,["69"] = 62,["70"] = 64,["71"] = 64,["72"] = 64,["73"] = 64,["74"] = 64,["75"] = 45,["76"] = 45,["77"] = 70,["78"] = 72,["79"] = 72,["80"] = 72,["81"] = 72,["82"] = 72,["83"] = 45,["84"] = 32,["85"] = 75,["86"] = 75,["87"] = 75,["88"] = 75,["89"] = 85,["90"] = 87,["91"] = 87,["92"] = 87,["93"] = 87,["94"] = 87,["95"] = 75,["96"] = 75,["97"] = 75,["98"] = 75,["99"] = 32,["100"] = 102,["101"] = 103,["102"] = 105,["103"] = 105,["104"] = 105,["105"] = 105,["106"] = 105,["107"] = 102,["108"] = 107,["109"] = 109,["110"] = 109,["111"] = 109,["112"] = 109,["113"] = 109,["114"] = 109,["115"] = 109,["116"] = 102,["117"] = 102,["118"] = 115,["119"] = 117,["120"] = 117,["121"] = 117,["122"] = 117,["123"] = 117,["124"] = 117,["125"] = 102,["126"] = 119,["127"] = 121,["128"] = 121,["129"] = 121,["130"] = 121,["131"] = 121,["132"] = 121,["133"] = 102,["134"] = 32,["135"] = 32,["136"] = 127,["137"] = 128,["138"] = 130,["139"] = 130,["140"] = 130,["141"] = 130,["142"] = 130,["143"] = 130,["144"] = 130,["145"] = 130,["146"] = 130,["147"] = 130,["148"] = 127,["149"] = 127,["150"] = 127,["151"] = 127,["152"] = 32,["153"] = 32,["154"] = 147});
local ____exports = {}
local ____common = require("protos.common")
local ServerStatus = ____common.ServerStatus
local ErrorCode = ____common.ErrorCode
local ____gateway = require("protos.gateway")
local MessageType = ____gateway.MessageType
local ____message_id = require("protos.message_id")
local MessageId = ____message_id.MessageId
do
    local ____common = require("protos.common")
    ____exports.ServerStatus = ____common.ServerStatus
    ____exports.ErrorCode = ____common.ErrorCode
end
do
    local ____gateway = require("protos.gateway")
    ____exports.MessageType = ____gateway.MessageType
end
do
    local ____message_id = require("protos.message_id")
    ____exports.MessageId = ____message_id.MessageId
end
local function createMessage(defaults, init)
    return __TS__ObjectAssign({}, defaults, init)
end
____exports.proto = {
    common = {
        ServerStatus = ServerStatus,
        ErrorCode = ErrorCode,
        Packet = {create = function(init) return createMessage(
            {
                msg_id = 0,
                session = 0,
                data = __TS__New(Uint8Array, 0),
                timestamp = 0
            },
            init
        ) end},
        Response = {create = function(init) return createMessage(
            {
                code = ErrorCode.SUCCESS,
                message = "",
                data = __TS__New(Uint8Array, 0)
            },
            init
        ) end}
    },
    game = {
        FullRoleInfo = {create = function(init) return createMessage({
            role_id = 0,
            role_name = "",
            level = 0,
            exp = 0,
            avatar_id = 0,
            gold = 0,
            diamond = 0,
            total_power = 0,
            vip_level = 0,
            create_time = 0,
            last_login_time = 0
        }, init) end},
        ItemInfo = {create = function(init) return createMessage({item_id = 0, count = 0}, init) end},
        TaskInfo = {create = function(init) return createMessage({task_id = 0, status = 0, progress = 0}, init) end},
        EnterGameRequest = {create = function(init) return createMessage({role_id = 0}, init) end},
        EnterGameResponse = {create = function(init) return createMessage({
            code = ErrorCode.SUCCESS,
            message = "",
            items = {},
            tasks = {},
            server_time = 0
        }, init) end},
        CreateRoleRequest = {create = function(init) return createMessage({role_name = ""}, init) end},
        CreateRoleResponse = {create = function(init) return createMessage({
            code = ErrorCode.SUCCESS,
            message = "",
            items = {},
            tasks = {},
            server_time = 0
        }, init) end}
    },
    gateway = {
        MessageType = MessageType,
        HeartbeatRequest = {create = function(init) return createMessage({client_time = 0}, init) end},
        HeartbeatResponse = {create = function(init) return createMessage({server_time = 0, online_count = 0}, init) end},
        ClientInfo = {create = function(init) return createMessage({
            ip = "",
            port = 0,
            version = "",
            platform = "",
            device_id = ""
        }, init) end},
        ConnectRequest = {create = function(init) return createMessage({token = ""}, init) end},
        ConnectResponse = {create = function(init) return createMessage({code = ErrorCode.SUCCESS, message = "", conn_id = 0, server_time = 0}, init) end},
        DisconnectNotify = {create = function(init) return createMessage({conn_id = 0, reason = ""}, init) end}
    },
    login = {
        AccountLoginRequest = {create = function(init) return createMessage({
            username = "",
            password = "",
            platform = "",
            device_id = "",
            client_version = ""
        }, init) end},
        AccountLoginResponse = {create = function(init) return createMessage({
            code = ErrorCode.SUCCESS,
            message = "",
            account_token = "",
            account_id = 0,
            servers = {},
            last_server_id = 0,
            last_role_name = ""
        }, init) end},
        SelectServerRequest = {create = function(init) return createMessage({account_token = "", server_id = 0}, init) end},
        RoleBrief = {create = function(init) return createMessage({
            role_id = 0,
            role_name = "",
            level = 0,
            avatar_id = 0,
            last_login = 0,
            total_power = 0
        }, init) end},
        SelectServerResponse = {create = function(init) return createMessage({
            code = ErrorCode.SUCCESS,
            message = "",
            gateway_token = "",
            roles = {},
            max_role_count = 0,
            server_time = 0
        }, init) end}
    },
    message_id = {MessageId = MessageId},
    server = {
        ServerInfo = {create = function(init) return createMessage({
            server_id = 0,
            server_name = "",
            host = "",
            port = 0,
            status = nil,
            online_count = 0,
            is_new = false,
            is_recommend = false,
            has_role = false,
            role_count = 0
        }, init) end},
        GetServerListRequest = {create = function(init) return createMessage({account_token = ""}, init) end},
        GetServerListResponse = {create = function(init) return createMessage({code = ErrorCode.SUCCESS, message = "", servers = {}, last_server_id = 0}, init) end},
        ServerStatusUpdate = {create = function(init) return createMessage({server_id = 0, status = nil, online_count = 0}, init) end}
    }
}
____exports.default = ____exports.proto
return ____exports
