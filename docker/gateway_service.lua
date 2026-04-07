-- Gateway 网关服务
-- 职责: TCP监听、Protobuf Packet编解码、消息路由、连接状态管理
-- 纯 Lua 实现（基础设施层，处理二进制协议和 Socket API 更自然）

local skynet = require "skynet"
local socket = require "skynet.socket"
local pb = require "pb"

-- 状态
local connections = {}       -- fd → { fd, addr, token, account_id, server_id, last_heartbeat }
local account_connections = {} -- "account_id:server_id" → fd（用于顶号检测）
local route_table = {}       -- msg_id → { addr, cmd }
local conn_counter = 0       -- 连接ID计数器

local MAX_PACKET_SIZE = 65536  -- 64KB 最大包体
local HEARTBEAT_TIMEOUT = 3600  -- 3600秒(1小时)心跳超时，避免客户端未实现心跳时踢掉玩家
local HEARTBEAT_ENABLED = true  -- 开启心跳检测，但超时时间很长作为安全兜底

-- ========== Protobuf 编解码辅助 ==========

-- 发送 Packet 给客户端
local function send_packet(fd, msg_id, session, data)
    local packet = pb.encode("common.Packet", {
        msg_id = msg_id,
        session = session,
        data = data or "",
        timestamp = math.floor(skynet.time()),
    })
    if not packet then
        skynet.error("[Gateway] Failed to encode Packet for fd=" .. fd)
        return false
    end
    -- 4字节 LE 长度前缀
    local header = string.pack("<I", #packet)
    return socket.write(fd, header .. packet)
end

-- 断开连接并清理
local function close_connection(fd, reason)
    local conn = connections[fd]
    if conn then
        -- 清理反向索引
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
    -- 解码 ConnectRequest
    local req = pb.decode("gateway.ConnectRequest", data)
    conn_counter = conn_counter + 1
    local conn_id = conn_counter

    -- 存储连接信息
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

    -- 返回 ConnectResponse
    local rsp_data = pb.encode("gateway.ConnectResponse", {
        code = 0,  -- SUCCESS
        message = "",
        conn_id = conn_id,
        server_time = math.floor(skynet.time()),
    })
    send_packet(fd, 103, session, rsp_data)  -- GATEWAY_CONNECT_RSP
end

local function handle_heartbeat_req(fd, session, data)
    -- 更新心跳时间
    if connections[fd] then
        connections[fd].last_heartbeat = skynet.now()
    end
    
    local rsp_data = pb.encode("gateway.HeartbeatResponse", {
        server_time = math.floor(skynet.time()),
        online_count = 0,
    })
    send_packet(fd, 101, session, rsp_data)  -- GATEWAY_HEARTBEAT_RSP
end

-- ========== 连接读取协程 ==========

local function connection_handler(fd)
    -- 初始化心跳时间
    if connections[fd] then
        connections[fd].last_heartbeat = skynet.now()
    end
    
    -- 读取循环
    while true do
        -- 1. 读 4 字节长度前缀
        local header = socket.read(fd, 4)
        if not header then
            close_connection(fd)
            return
        end
        if #header < 4 then
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
        if not body then
            close_connection(fd)
            return
        end
        if #body < body_len then
            close_connection(fd)
            return
        end

        -- 3. 解码外层 Packet
        skynet.error(string.format("[Gateway] Received packet from fd=%d, body_len=%d", fd, body_len))
        local ok, packet = pcall(pb.decode, "common.Packet", body)
        if not ok or not packet then
            skynet.error("[Gateway] Failed to decode Packet from fd=" .. fd .. ", err=" .. tostring(packet))
            close_connection(fd)
            return
        end

        local msg_id = packet.msg_id
        local session = packet.session
        local data = packet.data or ""
        skynet.error(string.format("[Gateway] Decoded packet: msg_id=%d, session=%d, data_len=%d", msg_id, session, #data))

        -- 任何消息都视为活跃，更新心跳时间
        if connections[fd] then
            connections[fd].last_heartbeat = skynet.now()
        end

        -- 4. 路由消息
        if msg_id == 102 then
            -- GATEWAY_CONNECT_REQ
            handle_connect_req(fd, session, data)
        elseif msg_id == 100 then
            -- GATEWAY_HEARTBEAT_REQ
            handle_heartbeat_req(fd, session, data)
        else
            -- 业务消息 → 查路由表转发
            local route = route_table[msg_id]
            if not route then
                skynet.error(string.format("[Gateway] No route for msg_id=%d", msg_id))
                -- 返回错误响应
                local err_data = pb.encode("common.Response", {
                    code = 8,  -- SERVICE_UNAVAILABLE
                    message = "No route for msg_id=" .. tostring(msg_id),
                })
                send_packet(fd, msg_id + 1, session, err_data)
            else
                -- 获取连接的 token
                local conn = connections[fd]
                local token = conn and conn.token or ""

                -- 转发给业务服务
                local forward_msg = {
                    conn_id = fd,
                    session = session,
                    token = token,
                    data = data,
                }

                skynet.error(string.format("[Gateway] Forwarding to service: addr=%s msg_id=%d data_len=%d",
                    tostring(route.addr), msg_id, #data))
                local ok2, response = pcall(skynet.call, route.addr, "lua", msg_id, forward_msg)
                skynet.error(string.format("[Gateway] Forward result: ok=%s response_type=%s",
                    tostring(ok2), type(response)))
                if ok2 and response then
                    -- response = { msg_id, data, conn_id? }
                    send_packet(fd, response.msg_id or (msg_id + 1), session, response.data or "")

                    -- 如果包含 bind_token 指令（选服成功后绑定 token）
                    if response.bind_token and conn then
                        local new_account_id = response.account_id or 0
                        local new_server_id = response.server_id or 0

                        -- 顶号检测：同一账号+区服只能有一个连接
                        if new_account_id > 0 and new_server_id > 0 then
                            local key = tostring(new_account_id) .. ":" .. tostring(new_server_id)
                            local old_fd = account_connections[key]
                            if old_fd and old_fd ~= fd and connections[old_fd] then
                                skynet.error(string.format("[Gateway] Kick old connection: fd=%d (new fd=%d accountId=%d serverId=%d)",
                                    old_fd, fd, new_account_id, new_server_id))
                                -- 发送顶号通知给旧连接
                                local notify_data = pb.encode("gateway.KickNotify", {
                                    reason = "account_kick",  -- 被顶号
                                })
                                send_packet(old_fd, 104, 0, notify_data)  -- GATEWAY_KICK_NOTIFY = 104
                                -- 延迟关闭旧连接，确保通知发出
                                skynet.fork(function()
                                    skynet.sleep(50)  -- 等 0.5 秒
                                    close_connection(old_fd, "kick_replace")
                                end)
                            end
                            account_connections[key] = fd
                        end

                        conn.token = response.bind_token
                        conn.account_id = new_account_id
                        conn.server_id = new_server_id
                        skynet.error(string.format("[Gateway] Token bound: fd=%d accountId=%d serverId=%d",
                            fd, conn.account_id, conn.server_id))
                    end
                elseif not ok2 then
                    skynet.error(string.format("[Gateway] Forward failed: msg_id=%d err=%s trace=%s", 
                        msg_id, tostring(response), debug.traceback()))
                    local err_data = pb.encode("common.Response", {
                        code = 7,  -- INTERNAL_ERROR
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

-- 业务服务注册路由
function CMD.register(source, msg)
    local service_name = msg.service_name
    local service_addr = msg.service_addr
    local routes = msg.routes

    if not routes then return end

    for _, msg_id in ipairs(routes) do
        route_table[msg_id] = { addr = service_addr }
        skynet.error(string.format("[Gateway] Route registered: msg_id=%d → %s", msg_id, service_name))
    end
end

-- 业务服务注销路由
function CMD.unregister(source, service_name)
    local unregistered = 0
    for msg_id, route in pairs(route_table) do
        if route.addr == service_name or route.addr == source then
            route_table[msg_id] = nil
            unregistered = unregistered + 1
        end
    end
    if unregistered > 0 then
        skynet.error(string.format("[Gateway] Routes unregistered: %s (%d routes)", service_name, unregistered))
    end
end

-- 绑定 token 到连接（Login 服务选服成功后调用）
function CMD.bindToken(source, fd, token, account_id, server_id)
    local conn = connections[fd]
    if conn then
        account_id = account_id or 0
        server_id = server_id or 0

        -- 顶号检测
        if account_id > 0 and server_id > 0 then
            local key = tostring(account_id) .. ":" .. tostring(server_id)
            local old_fd = account_connections[key]
            if old_fd and old_fd ~= fd and connections[old_fd] then
                skynet.error(string.format("[Gateway] Kick old connection: fd=%d (new fd=%d accountId=%d serverId=%d)",
                    old_fd, fd, account_id, server_id))
                local notify_data = pb.encode("gateway.KickNotify", {
                    reason = "account_kick",
                })
                send_packet(old_fd, 104, 0, notify_data)
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
        skynet.error(string.format("[Gateway] Token bound: fd=%d accountId=%d serverId=%d", fd, account_id, server_id))
    end
end

-- 向客户端发送消息（业务服务推送）
function CMD.sendToClient(source, fd, msg_id, data)
    send_packet(fd, msg_id, 0, data)
end

-- 踢下线
function CMD.kick(source, fd)
    close_connection(fd)
end

-- 查询在线数
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

    -- 监听 TCP
    local listen_fd = socket.listen("0.0.0.0", port)
    skynet.error("[Gateway] Listening on 0.0.0.0:" .. port)

    socket.start(listen_fd, function(fd, addr)
        skynet.error("[Gateway] Accept connection: fd=" .. fd .. " addr=" .. addr)
        socket.start(fd)

        -- 存储初始连接信息
        connections[fd] = {
            fd = fd,
            addr = addr,
            token = "",
            account_id = 0,
            server_id = 0,
            conn_id = 0,
            last_heartbeat = skynet.now(),
        }

        -- 为每个连接 fork 协程处理
        skynet.fork(connection_handler, fd)
    end)

    -- 注册 Lua 消息分发
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

    -- 启动心跳超时检测（仅在开启时生效）
    if HEARTBEAT_ENABLED then
        skynet.fork(function()
            while true do
                skynet.sleep(1000)  -- 每 10 秒检查一次
                local now = skynet.now()
                local timeout_ticks = HEARTBEAT_TIMEOUT * 100  -- skynet.now() 单位是 1/100 秒
                for fd, conn in pairs(connections) do
                    if conn.last_heartbeat and (now - conn.last_heartbeat) > timeout_ticks then
                        skynet.error(string.format("[Gateway] Heartbeat timeout: fd=%d addr=%s", fd, conn.addr or "?"))
                        close_connection(fd)
                    end
                end
            end
        end)
        skynet.error("[Gateway] Heartbeat timeout detection enabled (" .. HEARTBEAT_TIMEOUT .. "s)")
    else
        skynet.error("[Gateway] Heartbeat timeout detection disabled (HEARTBEAT_ENABLED=false)")
    end

    skynet.error("======== Gateway Service Ready ========")
end)
