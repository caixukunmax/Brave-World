-- Gateway 网关服务
-- 职责: TCP监听、Protobuf Packet编解码、消息路由、连接状态管理

local skynet = require "skynet"
local socket = require "skynet.socket"
local common = require "common"
local MsgId = common.MessageId
local protos = common.protos
local pb = require "pb"
local msgIdMap = require "protos.msg_id_map"

-- ========== 协议日志辅助 ==========

-- 简易 table 序列化
local function dump_table(t, depth)
    depth = depth or 0
    if depth > 2 then return "..." end
    local parts = {}
    for k, v in pairs(t) do
        local ks = type(k) == "number" and ("["..k.."]") or k
        if type(v) == "table" then
            parts[#parts + 1] = ks .. "=" .. dump_table(v, depth + 1)
        elseif type(v) == "string" then
            parts[#parts + 1] = ks .. "=" .. string.format("%q", #v > 40 and v:sub(1,40).."..." or v)
        else
            parts[#parts + 1] = ks .. "=" .. tostring(v)
        end
    end
    return "{" .. table.concat(parts, ", ") .. "}"
end

-- 解码包体并格式化
local function decode_log(msg_id, data)
    local name = msgIdMap.name[msg_id] or "?"
    local typeName = msgIdMap.type[msg_id]
    if typeName and #data > 0 then
        local ok, decoded = pcall(pb.decode, typeName, data)
        if ok and decoded then
            return string.format("%s(%d) %s", name, msg_id, dump_table(decoded))
        end
    end
    return string.format("%s(%d) [%d bytes]", name, msg_id, #data)
end

-- 状态
local connections = {}         -- fd → { fd, addr, token, account_id, server_id, last_heartbeat }
local account_connections = {} -- "account_id:server_id" → fd（用于顶号检测）
local route_table = {}         -- msg_id → { addr }
local conn_counter = 0         -- 连接ID计数器

local MAX_PACKET_SIZE = 65536  -- 64KB 最大包体
local HEARTBEAT_TIMEOUT = 3600 -- 3600秒心跳超时
local HEARTBEAT_ENABLED = true

-- ========== Protobuf 编解码辅助 ==========

-- 发送 Packet 给客户端
local function send_packet(fd, msg_id, session, data)
    local packet = protos.common.Packet.encode({
        msg_id = msg_id,
        session = session,
        data = data or "",
        timestamp = math.floor(skynet.time()),
    })
    if not packet then
        skynet.error("[Gateway] Failed to encode Packet for fd=" .. fd)
        return false
    end
    skynet.error(string.format("[Gateway] SEND fd=%d %s session=%d", fd, decode_log(msg_id, data), session))
    local header = string.pack("<I", #packet)
    return socket.write(fd, header .. packet)
end

-- 断开连接并清理
local function close_connection(fd, reason)
    local conn = connections[fd]
    if conn then
        if conn.account_id and conn.account_id > 0 and conn.server_id and conn.server_id > 0 then
            local key = tostring(conn.account_id) .. ":" .. tostring(conn.server_id)
            if account_connections[key] == fd then
                account_connections[key] = nil
            end
        end
        skynet.error(string.format("[Gateway] Connection closed: fd=%d addr=%s reason=%s",
            fd, conn.addr or "?", reason or "unknown"))
        connections[fd] = nil
    end
    socket.close(fd)
end

-- ========== Gateway 内部消息处理 ==========

local function handle_connect_req(fd, session, data)
    local req = protos.gateway.ConnectRequest.decode(data)
    conn_counter = conn_counter + 1
    local conn_id = conn_counter

    connections[fd] = {
        fd = fd,
        addr = "?",
        token = "",
        account_id = 0,
        server_id = 0,
        conn_id = conn_id,
    }

    if req and req.client_info then
        connections[fd].addr = req.client_info.ip or "?"
    end

    skynet.error(string.format("[Gateway] New connection: fd=%d connId=%d", fd, conn_id))

    local rsp_data = protos.gateway.ConnectResponse.encode({
        code = 0,
        message = "",
        conn_id = conn_id,
        server_time = math.floor(skynet.time()),
    })
    send_packet(fd, MsgId.GATEWAY_CONNECT_RSP, session, rsp_data)
end

local function handle_heartbeat_req(fd, session)
    if connections[fd] then
        connections[fd].last_heartbeat = skynet.now()
    end

    local rsp_data = protos.gateway.HeartbeatResponse.encode({
        server_time = math.floor(skynet.time()),
        online_count = 0,
    })
    send_packet(fd, MsgId.GATEWAY_HEARTBEAT_RSP, session, rsp_data)
end

-- ========== 连接读取协程 ==========

local function connection_handler(fd)
    if connections[fd] then
        connections[fd].last_heartbeat = skynet.now()
    end

    while true do
        -- 1. 读 4 字节长度前缀
        local header = socket.read(fd, 4)
        if not header or #header < 4 then
            close_connection(fd)
            return
        end

        local body_len = string.unpack("<I", header)
        if body_len <= 0 or body_len > MAX_PACKET_SIZE then
            skynet.error(string.format("[Gateway] Invalid packet size: %d from fd=%d", body_len, fd))
            close_connection(fd)
            return
        end

        -- 2. 读包体
        local body = socket.read(fd, body_len)
        if not body or #body < body_len then
            close_connection(fd)
            return
        end

        -- 3. 解码外层 Packet
        local ok, packet = pcall(protos.common.Packet.decode, body)
        if not ok or not packet then
            skynet.error(string.format("[Gateway] Failed to decode Packet from fd=%d bodyLen=%d", fd, #body))
            close_connection(fd)
            return
        end

        local msg_id = packet.msg_id
        local session = packet.session
        local data = packet.data or ""

        skynet.error(string.format("[Gateway] RECV fd=%d %s session=%d", fd, decode_log(msg_id, data), session))

        -- 任何消息都视为活跃
        if connections[fd] then
            connections[fd].last_heartbeat = skynet.now()
        end

        -- 4. 路由消息
        if msg_id == MsgId.GATEWAY_CONNECT_REQ then
            handle_connect_req(fd, session, data)
        elseif msg_id == MsgId.GATEWAY_HEARTBEAT_REQ then
            handle_heartbeat_req(fd, session)
        else
            -- 业务消息 → 查路由表转发
            local route = route_table[msg_id]
            if not route then
                skynet.error(string.format("[Gateway] No route for msg_id=%d", msg_id))
                local err_data = protos.common.Response.encode({
                    code = common.ErrorCode.SERVICE_UNAVAILABLE,
                    message = "No route for msg_id=" .. tostring(msg_id),
                })
                send_packet(fd, msg_id + 1, session, err_data)
            else
                local conn = connections[fd]
                local token = conn and conn.token or ""

                local forward_msg = {
                    conn_id = fd,
                    session = session,
                    token = token,
                    data = data,
                }

                local ok2, response = pcall(skynet.call, route.addr, "lua", msg_id, forward_msg)
                skynet.error(string.format("[Gateway] FORWARD msg_id=%d → addr=%s ok=%s", msg_id, tostring(route.addr), tostring(ok2)))
                if ok2 and response then
                    send_packet(fd, response.msg_id or (msg_id + 1), session, response.data or "")

                    -- bind_token: 选服成功后绑定 token + 顶号检测
                    if response.bind_token and conn then
                        local new_account_id = response.account_id or 0
                        local new_server_id = response.server_id or 0

                        if new_account_id > 0 and new_server_id > 0 then
                            local key = tostring(new_account_id) .. ":" .. tostring(new_server_id)
                            local old_fd = account_connections[key]
                            if old_fd and old_fd ~= fd and connections[old_fd] then
                                skynet.error(string.format("[Gateway] Kick old connection: fd=%d (new fd=%d accountId=%d serverId=%d)",
                                    old_fd, fd, new_account_id, new_server_id))
                                local notify_data = protos.gateway.DisconnectNotify.encode({
                                    reason = "account_kick",
                                })
                                send_packet(old_fd, MsgId.GATEWAY_KICK_NOTIFY, 0, notify_data)
                                skynet.fork(function()
                                    skynet.sleep(50)
                                    close_connection(old_fd, "kick_replace")
                                end)
                            end
                            account_connections[key] = fd
                        end

                        conn.token = response.bind_token
                        conn.account_id = new_account_id
                        conn.server_id = new_server_id
                    end
                elseif not ok2 then
                    skynet.error(string.format("[Gateway] Forward failed: msg_id=%d err=%s",
                        msg_id, tostring(response)))
                    local err_data = protos.common.Response.encode({
                        code = common.ErrorCode.INTERNAL_ERROR,
                        message = "Service call failed",
                    })
                    send_packet(fd, msg_id + 1, session, err_data)
                end
            end
        end
    end
end

-- ========== Skynet 服务命令 ==========

local CMD = {}

function CMD.register(source, msg)
    if not msg.routes then return end
    for _, msg_id in ipairs(msg.routes) do
        route_table[msg_id] = { addr = msg.service_addr }
        skynet.error(string.format("[Gateway] Route registered: msg_id=%d → %s", msg_id, msg.service_name))
    end
end

function CMD.unregister(source, service_name)
    local n = 0
    for msg_id, route in pairs(route_table) do
        if route.addr == service_name or route.addr == source then
            route_table[msg_id] = nil
            n = n + 1
        end
    end
    if n > 0 then
        skynet.error(string.format("[Gateway] Routes unregistered: %s (%d routes)", service_name, n))
    end
end

function CMD.bindToken(source, fd, token, account_id, server_id)
    local conn = connections[fd]
    if not conn then return end
    account_id = account_id or 0
    server_id = server_id or 0

    if account_id > 0 and server_id > 0 then
        local key = tostring(account_id) .. ":" .. tostring(server_id)
        local old_fd = account_connections[key]
        if old_fd and old_fd ~= fd and connections[old_fd] then
            send_packet(old_fd, MsgId.GATEWAY_KICK_NOTIFY, 0, protos.gateway.DisconnectNotify.encode({ reason = "account_kick" }))
            skynet.fork(function()
                skynet.sleep(50)
                close_connection(old_fd, "kick_replace")
            end)
        end
        account_connections[key] = fd
    end

    conn.token = token
    conn.account_id = account_id
    conn.server_id = server_id
end

function CMD.sendToClient(source, fd, msg_id, data)
    send_packet(fd, msg_id, 0, data)
end

function CMD.kick(source, fd)
    close_connection(fd)
end

function CMD.getOnlineCount(source)
    local count = 0
    for _ in pairs(connections) do
        count = count + 1
    end
    return count
end

-- ========== 启动 ==========

skynet.start(function()
    skynet.error("======== Gateway Service Starting ========")

    local port = tonumber(skynet.getenv("GATEWAY_PORT")) or 8889

    local listen_fd = socket.listen("0.0.0.0", port)
    skynet.error("[Gateway] Listening on 0.0.0.0:" .. port)

    socket.start(listen_fd, function(fd, addr)
        skynet.error("[Gateway] Accept connection: fd=" .. fd .. " addr=" .. addr)
        socket.start(fd)

        connections[fd] = {
            fd = fd,
            addr = addr,
            token = "",
            account_id = 0,
            server_id = 0,
            conn_id = 0,
            last_heartbeat = skynet.now(),
        }

        skynet.fork(connection_handler, fd)
    end)

    skynet.dispatch("lua", function(session, address, cmd, ...)
        local f = CMD[cmd]
        if f then
            local ret = f(address, ...)
            if session > 0 and ret ~= nil then
                skynet.retpack(ret)
            end
        else
            skynet.error("[Gateway] Unknown command: " .. tostring(cmd))
        end
    end)

    if HEARTBEAT_ENABLED then
        skynet.fork(function()
            while true do
                skynet.sleep(1000)
                local now = skynet.now()
                local timeout_ticks = HEARTBEAT_TIMEOUT * 100
                for fd, conn in pairs(connections) do
                    if conn.last_heartbeat and (now - conn.last_heartbeat) > timeout_ticks then
                        skynet.error(string.format("[Gateway] Heartbeat timeout: fd=%d addr=%s", fd, conn.addr or "?"))
                        close_connection(fd)
                    end
                end
            end
        end)
    end

    skynet.error("======== Gateway Service Ready ========")
end)
