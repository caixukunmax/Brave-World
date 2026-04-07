local ____lualib = require("lualib_bundle")
local __TS__ObjectAssign = ____lualib.__TS__ObjectAssign
local __TS__New = ____lualib.__TS__New
local __TS__SourceMapTraceBack = ____lualib.__TS__SourceMapTraceBack
__TS__SourceMapTraceBack(debug.getinfo(1).short_src, {["7"] = 17,["8"] = 17,["9"] = 17,["10"] = 20,["11"] = 20,["12"] = 23,["13"] = 23,["15"] = 8,["16"] = 8,["17"] = 8,["20"] = 11,["21"] = 11,["24"] = 14,["25"] = 14,["27"] = 27,["28"] = 28,["29"] = 27,["30"] = 32,["31"] = 33,["32"] = 33,["33"] = 33,["34"] = 36,["35"] = 37,["36"] = 38,["37"] = 38,["38"] = 38,["39"] = 38,["40"] = 38,["41"] = 38,["42"] = 38,["43"] = 36,["44"] = 36,["45"] = 36,["46"] = 33,["47"] = 44,["48"] = 45,["49"] = 46,["50"] = 46,["51"] = 46,["52"] = 46,["53"] = 46,["54"] = 46,["55"] = 44,["56"] = 44,["57"] = 44,["58"] = 33,["59"] = 32,["60"] = 53,["61"] = 54,["62"] = 55,["63"] = 56,["64"] = 56,["65"] = 56,["66"] = 56,["67"] = 56,["68"] = 56,["69"] = 56,["70"] = 56,["71"] = 56,["72"] = 56,["73"] = 56,["74"] = 54,["75"] = 54,["76"] = 54,["77"] = 53,["78"] = 62,["79"] = 62,["80"] = 62,["81"] = 62,["82"] = 53,["83"] = 70,["84"] = 70,["85"] = 70,["86"] = 70,["87"] = 53,["88"] = 78,["89"] = 78,["90"] = 78,["91"] = 78,["92"] = 53,["93"] = 86,["94"] = 87,["95"] = 88,["96"] = 88,["97"] = 88,["98"] = 88,["99"] = 88,["100"] = 86,["101"] = 86,["102"] = 86,["103"] = 53,["104"] = 94,["105"] = 94,["106"] = 94,["107"] = 94,["108"] = 53,["109"] = 102,["110"] = 103,["111"] = 104,["112"] = 104,["113"] = 104,["114"] = 104,["115"] = 104,["116"] = 102,["117"] = 102,["118"] = 102,["119"] = 53,["120"] = 32,["121"] = 111,["122"] = 111,["123"] = 113,["124"] = 113,["125"] = 113,["126"] = 113,["127"] = 111,["128"] = 121,["129"] = 121,["130"] = 121,["131"] = 121,["132"] = 111,["133"] = 129,["134"] = 130,["135"] = 131,["136"] = 131,["137"] = 131,["138"] = 131,["139"] = 131,["140"] = 129,["141"] = 129,["142"] = 129,["143"] = 111,["144"] = 137,["145"] = 137,["146"] = 137,["147"] = 137,["148"] = 111,["149"] = 145,["150"] = 145,["151"] = 145,["152"] = 145,["153"] = 111,["154"] = 153,["155"] = 153,["156"] = 153,["157"] = 153,["158"] = 111,["159"] = 32,["160"] = 162,["161"] = 163,["162"] = 164,["163"] = 165,["164"] = 165,["165"] = 165,["166"] = 165,["167"] = 165,["168"] = 163,["169"] = 163,["170"] = 163,["171"] = 162,["172"] = 171,["173"] = 172,["174"] = 173,["175"] = 173,["176"] = 173,["177"] = 173,["178"] = 173,["179"] = 173,["180"] = 173,["181"] = 171,["182"] = 171,["183"] = 171,["184"] = 162,["185"] = 179,["186"] = 179,["187"] = 179,["188"] = 179,["189"] = 162,["190"] = 187,["191"] = 188,["192"] = 189,["193"] = 189,["194"] = 189,["195"] = 189,["196"] = 189,["197"] = 189,["198"] = 187,["199"] = 187,["200"] = 187,["201"] = 162,["202"] = 195,["203"] = 196,["204"] = 197,["205"] = 197,["206"] = 197,["207"] = 197,["208"] = 197,["209"] = 197,["210"] = 195,["211"] = 195,["212"] = 195,["213"] = 162,["214"] = 32,["215"] = 32,["216"] = 207,["217"] = 208,["218"] = 209,["219"] = 210,["220"] = 210,["221"] = 210,["222"] = 210,["223"] = 210,["224"] = 210,["225"] = 210,["226"] = 210,["227"] = 210,["228"] = 210,["229"] = 208,["230"] = 208,["231"] = 208,["232"] = 207,["233"] = 216,["234"] = 216,["235"] = 216,["236"] = 216,["237"] = 207,["238"] = 224,["239"] = 224,["240"] = 224,["241"] = 224,["242"] = 207,["243"] = 232,["244"] = 232,["245"] = 232,["246"] = 232,["247"] = 207,["248"] = 32,["249"] = 32,["250"] = 243});
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
        Packet = {
            create = function(init) return createMessage(
                {
                    msg_id = 0,
                    session = 0,
                    data = __TS__New(Uint8Array, 0),
                    timestamp = 0
                },
                init
            ) end,
            decode = function(data) return pb_decode("common.Packet", data) end,
            encode = function(msg) return pb_encode("common.Packet", msg) end
        },
        Response = {
            create = function(init) return createMessage(
                {
                    code = ErrorCode.SUCCESS,
                    message = "",
                    data = __TS__New(Uint8Array, 0)
                },
                init
            ) end,
            decode = function(data) return pb_decode("common.Response", data) end,
            encode = function(msg) return pb_encode("common.Response", msg) end
        }
    },
    game = {
        FullRoleInfo = {
            create = function(init) return createMessage({
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
            }, init) end,
            decode = function(data) return pb_decode("game.FullRoleInfo", data) end,
            encode = function(msg) return pb_encode("game.FullRoleInfo", msg) end
        },
        ItemInfo = {
            create = function(init) return createMessage({item_id = 0, count = 0}, init) end,
            decode = function(data) return pb_decode("game.ItemInfo", data) end,
            encode = function(msg) return pb_encode("game.ItemInfo", msg) end
        },
        TaskInfo = {
            create = function(init) return createMessage({task_id = 0, status = 0, progress = 0}, init) end,
            decode = function(data) return pb_decode("game.TaskInfo", data) end,
            encode = function(msg) return pb_encode("game.TaskInfo", msg) end
        },
        EnterGameRequest = {
            create = function(init) return createMessage({role_id = 0}, init) end,
            decode = function(data) return pb_decode("game.EnterGameRequest", data) end,
            encode = function(msg) return pb_encode("game.EnterGameRequest", msg) end
        },
        EnterGameResponse = {
            create = function(init) return createMessage({
                code = ErrorCode.SUCCESS,
                message = "",
                items = {},
                tasks = {},
                server_time = 0
            }, init) end,
            decode = function(data) return pb_decode("game.EnterGameResponse", data) end,
            encode = function(msg) return pb_encode("game.EnterGameResponse", msg) end
        },
        CreateRoleRequest = {
            create = function(init) return createMessage({role_name = ""}, init) end,
            decode = function(data) return pb_decode("game.CreateRoleRequest", data) end,
            encode = function(msg) return pb_encode("game.CreateRoleRequest", msg) end
        },
        CreateRoleResponse = {
            create = function(init) return createMessage({
                code = ErrorCode.SUCCESS,
                message = "",
                items = {},
                tasks = {},
                server_time = 0
            }, init) end,
            decode = function(data) return pb_decode("game.CreateRoleResponse", data) end,
            encode = function(msg) return pb_encode("game.CreateRoleResponse", msg) end
        }
    },
    gateway = {
        MessageType = MessageType,
        HeartbeatRequest = {
            create = function(init) return createMessage({client_time = 0}, init) end,
            decode = function(data) return pb_decode("gateway.HeartbeatRequest", data) end,
            encode = function(msg) return pb_encode("gateway.HeartbeatRequest", msg) end
        },
        HeartbeatResponse = {
            create = function(init) return createMessage({server_time = 0, online_count = 0}, init) end,
            decode = function(data) return pb_decode("gateway.HeartbeatResponse", data) end,
            encode = function(msg) return pb_encode("gateway.HeartbeatResponse", msg) end
        },
        ClientInfo = {
            create = function(init) return createMessage({
                ip = "",
                port = 0,
                version = "",
                platform = "",
                device_id = ""
            }, init) end,
            decode = function(data) return pb_decode("gateway.ClientInfo", data) end,
            encode = function(msg) return pb_encode("gateway.ClientInfo", msg) end
        },
        ConnectRequest = {
            create = function(init) return createMessage({token = ""}, init) end,
            decode = function(data) return pb_decode("gateway.ConnectRequest", data) end,
            encode = function(msg) return pb_encode("gateway.ConnectRequest", msg) end
        },
        ConnectResponse = {
            create = function(init) return createMessage({code = ErrorCode.SUCCESS, message = "", conn_id = 0, server_time = 0}, init) end,
            decode = function(data) return pb_decode("gateway.ConnectResponse", data) end,
            encode = function(msg) return pb_encode("gateway.ConnectResponse", msg) end
        },
        DisconnectNotify = {
            create = function(init) return createMessage({conn_id = 0, reason = ""}, init) end,
            decode = function(data) return pb_decode("gateway.DisconnectNotify", data) end,
            encode = function(msg) return pb_encode("gateway.DisconnectNotify", msg) end
        }
    },
    login = {
        AccountLoginRequest = {
            create = function(init) return createMessage({
                username = "",
                password = "",
                platform = "",
                device_id = "",
                client_version = ""
            }, init) end,
            decode = function(data) return pb_decode("login.AccountLoginRequest", data) end,
            encode = function(msg) return pb_encode("login.AccountLoginRequest", msg) end
        },
        AccountLoginResponse = {
            create = function(init) return createMessage({
                code = ErrorCode.SUCCESS,
                message = "",
                account_token = "",
                account_id = 0,
                servers = {},
                last_server_id = 0,
                last_role_name = ""
            }, init) end,
            decode = function(data) return pb_decode("login.AccountLoginResponse", data) end,
            encode = function(msg) return pb_encode("login.AccountLoginResponse", msg) end
        },
        SelectServerRequest = {
            create = function(init) return createMessage({account_token = "", server_id = 0}, init) end,
            decode = function(data) return pb_decode("login.SelectServerRequest", data) end,
            encode = function(msg) return pb_encode("login.SelectServerRequest", msg) end
        },
        RoleBrief = {
            create = function(init) return createMessage({
                role_id = 0,
                role_name = "",
                level = 0,
                avatar_id = 0,
                last_login = 0,
                total_power = 0
            }, init) end,
            decode = function(data) return pb_decode("login.RoleBrief", data) end,
            encode = function(msg) return pb_encode("login.RoleBrief", msg) end
        },
        SelectServerResponse = {
            create = function(init) return createMessage({
                code = ErrorCode.SUCCESS,
                message = "",
                gateway_token = "",
                roles = {},
                max_role_count = 0,
                server_time = 0
            }, init) end,
            decode = function(data) return pb_decode("login.SelectServerResponse", data) end,
            encode = function(msg) return pb_encode("login.SelectServerResponse", msg) end
        }
    },
    message_id = {MessageId = MessageId},
    server = {
        ServerInfo = {
            create = function(init) return createMessage({
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
            }, init) end,
            decode = function(data) return pb_decode("server.ServerInfo", data) end,
            encode = function(msg) return pb_encode("server.ServerInfo", msg) end
        },
        GetServerListRequest = {
            create = function(init) return createMessage({account_token = ""}, init) end,
            decode = function(data) return pb_decode("server.GetServerListRequest", data) end,
            encode = function(msg) return pb_encode("server.GetServerListRequest", msg) end
        },
        GetServerListResponse = {
            create = function(init) return createMessage({code = ErrorCode.SUCCESS, message = "", servers = {}, last_server_id = 0}, init) end,
            decode = function(data) return pb_decode("server.GetServerListResponse", data) end,
            encode = function(msg) return pb_encode("server.GetServerListResponse", msg) end
        },
        ServerStatusUpdate = {
            create = function(init) return createMessage({server_id = 0, status = nil, online_count = 0}, init) end,
            decode = function(data) return pb_decode("server.ServerStatusUpdate", data) end,
            encode = function(msg) return pb_encode("server.ServerStatusUpdate", msg) end
        }
    }
}
____exports.default = ____exports.proto
return ____exports
