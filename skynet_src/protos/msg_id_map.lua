--------------------------------------------------------------------------------
-- msg_id 映射表（由 build_proto 自动生成，请勿手动修改）
-- Source: message_id.proto
--------------------------------------------------------------------------------
local M = {}

-- msg_id → 枚举名称
M.name = {
    [1] = "PING",
    [2] = "PONG",
    [100] = "GATEWAY_HEARTBEAT_REQ",
    [101] = "GATEWAY_HEARTBEAT_RSP",
    [102] = "GATEWAY_CONNECT_REQ",
    [103] = "GATEWAY_CONNECT_RSP",
    [104] = "GATEWAY_DISCONNECT_NOTIFY",
    [105] = "GATEWAY_KICK_NOTIFY",
    [210] = "LOGIN_ACCOUNT_LOGIN_REQ",
    [211] = "LOGIN_ACCOUNT_LOGIN_RSP",
    [212] = "LOGIN_SELECT_SERVER_REQ",
    [213] = "LOGIN_SELECT_SERVER_RSP",
    [250] = "SERVER_GET_LIST_REQ",
    [251] = "SERVER_GET_LIST_RSP",
    [252] = "SERVER_STATUS_UPDATE",
    [320] = "GAME_ENTER_GAME_REQ",
    [321] = "GAME_ENTER_GAME_RSP",
    [322] = "GAME_CREATE_ROLE_REQ",
    [323] = "GAME_CREATE_ROLE_RSP",
    [324] = "GAME_MOVE_REQ",
    [325] = "GAME_MOVE_RSP",
}

-- msg_id → protobuf 类型（用于解码包体内容）
M.type = {
    [100] = "gateway.HeartbeatRequest",
    [101] = "gateway.HeartbeatResponse",
    [102] = "gateway.ConnectRequest",
    [103] = "gateway.ConnectResponse",
    [104] = "gateway.DisconnectNotify",
    [105] = "gateway.KickNotify",
    [210] = "login.AccountLoginRequest",
    [211] = "login.AccountLoginResponse",
    [212] = "login.SelectServerRequest",
    [213] = "login.SelectServerResponse",
    [250] = "server.GetListRequest",
    [251] = "server.GetListResponse",
    [320] = "game.EnterGameRequest",
    [321] = "game.EnterGameResponse",
    [322] = "game.CreateRoleRequest",
    [323] = "game.CreateRoleResponse",
    [324] = "game.MoveRequest",
    [325] = "game.MoveResponse",
}

return M
