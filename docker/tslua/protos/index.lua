local ____lualib = require("lualib_bundle")
local __TS__ObjectAssign = ____lualib.__TS__ObjectAssign
local __TS__New = ____lualib.__TS__New
local __TS__SourceMapTraceBack = ____lualib.__TS__SourceMapTraceBack
__TS__SourceMapTraceBack(debug.getinfo(1).short_src, {["7"] = 17,["8"] = 17,["9"] = 17,["10"] = 20,["11"] = 20,["12"] = 23,["13"] = 23,["15"] = 8,["16"] = 8,["17"] = 8,["20"] = 11,["21"] = 11,["24"] = 14,["25"] = 14,["27"] = 27,["28"] = 28,["29"] = 27,["30"] = 32,["31"] = 33,["32"] = 33,["33"] = 33,["34"] = 36,["35"] = 38,["36"] = 38,["37"] = 38,["38"] = 38,["39"] = 38,["40"] = 38,["41"] = 38,["42"] = 33,["43"] = 40,["44"] = 42,["45"] = 42,["46"] = 42,["47"] = 42,["48"] = 42,["49"] = 42,["50"] = 33,["51"] = 32,["52"] = 45,["53"] = 46,["54"] = 48,["55"] = 48,["56"] = 48,["57"] = 48,["58"] = 48,["59"] = 48,["60"] = 48,["61"] = 48,["62"] = 48,["63"] = 48,["64"] = 48,["65"] = 45,["66"] = 45,["67"] = 45,["68"] = 45,["69"] = 62,["70"] = 64,["71"] = 64,["72"] = 64,["73"] = 64,["74"] = 64,["75"] = 45,["76"] = 45,["77"] = 70,["78"] = 72,["79"] = 72,["80"] = 72,["81"] = 72,["82"] = 72,["83"] = 45,["84"] = 74,["85"] = 76,["86"] = 76,["87"] = 76,["88"] = 76,["89"] = 76,["90"] = 45,["91"] = 45,["92"] = 45,["93"] = 45,["94"] = 45,["95"] = 45,["96"] = 45,["97"] = 45,["98"] = 45,["99"] = 45,["100"] = 45,["101"] = 45,["102"] = 45,["103"] = 45,["104"] = 45,["105"] = 32,["106"] = 135,["107"] = 135,["108"] = 135,["109"] = 135,["110"] = 145,["111"] = 147,["112"] = 147,["113"] = 147,["114"] = 147,["115"] = 147,["116"] = 135,["117"] = 135,["118"] = 135,["119"] = 135,["120"] = 32,["121"] = 162,["122"] = 163,["123"] = 165,["124"] = 165,["125"] = 165,["126"] = 165,["127"] = 165,["128"] = 162,["129"] = 167,["130"] = 169,["131"] = 169,["132"] = 169,["133"] = 169,["134"] = 169,["135"] = 169,["136"] = 169,["137"] = 162,["138"] = 162,["139"] = 175,["140"] = 177,["141"] = 177,["142"] = 177,["143"] = 177,["144"] = 177,["145"] = 177,["146"] = 162,["147"] = 179,["148"] = 181,["149"] = 181,["150"] = 181,["151"] = 181,["152"] = 181,["153"] = 181,["154"] = 162,["155"] = 162,["156"] = 187,["157"] = 189,["158"] = 189,["159"] = 189,["160"] = 189,["161"] = 189,["162"] = 162,["163"] = 162,["164"] = 162,["165"] = 162,["166"] = 162,["167"] = 162,["168"] = 162,["169"] = 162,["170"] = 32,["171"] = 32,["172"] = 223,["173"] = 224,["174"] = 226,["175"] = 226,["176"] = 226,["177"] = 226,["178"] = 226,["179"] = 226,["180"] = 226,["181"] = 226,["182"] = 226,["183"] = 226,["184"] = 223,["185"] = 223,["186"] = 223,["187"] = 223,["188"] = 32,["189"] = 32,["190"] = 243});
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
        }, init) end},
        PlayerInfo = {create = function(init) return createMessage({
            user_id = 0,
            level = 0,
            exp = 0,
            gold = 0,
            enter_time = 0
        }, init) end},
        OldEnterGameRequest = {create = function(init) return createMessage({user_id = 0, token = ""}, init) end},
        LeaveGameRequest = {create = function(init) return createMessage({user_id = 0}, init) end},
        LeaveGameResponse = {create = function(init) return createMessage({code = ErrorCode.SUCCESS, message = ""}, init) end},
        GetPlayerInfoRequest = {create = function(init) return createMessage({user_id = 0}, init) end},
        GetPlayerInfoResponse = {create = function(init) return createMessage({code = ErrorCode.SUCCESS, message = "", player = nil}, init) end},
        PlayerUpdate = {create = function(init) return createMessage({level = 0, exp = 0, gold = 0}, init) end},
        UpdatePlayerRequest = {create = function(init) return createMessage({user_id = 0, update = nil}, init) end},
        UpdatePlayerResponse = {create = function(init) return createMessage({code = ErrorCode.SUCCESS, message = "", player = nil}, init) end},
        GetOnlinePlayersRequest = {create = function(init) return createMessage({}, init) end},
        GetOnlinePlayersResponse = {create = function(init) return createMessage({count = 0, players = {}}, init) end},
        AddExpRequest = {create = function(init) return createMessage({user_id = 0, exp_amount = 0}, init) end},
        AddExpResponse = {create = function(init) return createMessage({code = ErrorCode.SUCCESS, message = "", level_up = false}, init) end},
        AddGoldRequest = {create = function(init) return createMessage({user_id = 0, gold_amount = 0}, init) end},
        AddGoldResponse = {create = function(init) return createMessage({code = ErrorCode.SUCCESS, message = "", player = nil}, init) end}
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
        }, init) end},
        LoginRequest = {create = function(init) return createMessage({username = "", password = "", device_id = "", platform = ""}, init) end},
        UserInfo = {create = function(init) return createMessage({
            user_id = 0,
            username = "",
            login_time = 0,
            level = 0,
            exp = 0
        }, init) end},
        LoginResponse = {create = function(init) return createMessage({code = ErrorCode.SUCCESS, message = "", token = ""}, init) end},
        LogoutRequest = {create = function(init) return createMessage({user_id = 0}, init) end},
        LogoutResponse = {create = function(init) return createMessage({code = ErrorCode.SUCCESS, message = ""}, init) end},
        ValidateTokenRequest = {create = function(init) return createMessage({token = ""}, init) end},
        ValidateTokenResponse = {create = function(init) return createMessage({code = ErrorCode.SUCCESS, message = "", user_id = 0, valid = false}, init) end},
        GetOnlineCountRequest = {create = function(init) return createMessage({}, init) end},
        GetOnlineCountResponse = {create = function(init) return createMessage({count = 0}, init) end}
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
