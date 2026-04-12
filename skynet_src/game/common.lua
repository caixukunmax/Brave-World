-- 公共模块: defineService、platform、常量、密码/token 工具

local skynet = require "skynet"
local pb = require "pb"
local md5 = require "md5.core"

local common = {}

--------------------------------------------------------------------------------
-- pb .desc 自动加载（扫描 protos/ 下所有 .desc 文件）
-- 新增 proto 文件只需生成 .desc 放入 protos/，无需改此代码
--------------------------------------------------------------------------------
do
    local desc_dir = "protos/"
    local cmd = package.config:sub(1,1) == "\\"
        and 'dir /b "' .. desc_dir .. '*.desc" 2>NUL'   -- Windows
        or  'ls "' .. desc_dir .. '*.desc" 2>/dev/null'  -- Linux/macOS
    local f = io.popen(cmd)
    if f then
        for name in f:lines() do
            pcall(pb.loadfile, desc_dir .. name)
        end
        f:close()
    end
end

--------------------------------------------------------------------------------
-- 枚举常量 + 消息编解码（由 build_proto 自动生成）
-- 数据源是 .proto 文件，改 proto 后重新 build 即可
--------------------------------------------------------------------------------
local protos = require "protos.index"

common.protos = protos
common.MessageId = protos.message_id.MessageId
common.ErrorCode = protos.common.ErrorCode
common.ServerStatus = protos.common.ServerStatus
common.MessageType = protos.gateway.MessageType

-- 事件类型常量
common.EventType = require "event.types"

-- 表 schema（用于 queryTable 查找 file 名）
local tableSchema = require "tables.schema"

--------------------------------------------------------------------------------
-- Table: 基于 skynet sharetable 的共享配置表
-- 用法:
--   local tbItem = common.queryTable("TbItem")
--   local item = tbItem[1001]        -- 按索引查询
--   for id, row in pairs(tbItem) do  -- 遍历
--   end
--
--   -- 热更新（GM 命令或管理接口调用）
--   skynet.call(getServiceAddr("game/table"), "lua", "reload", {"TbItem"})
--------------------------------------------------------------------------------
local sharetable = require "skynet.sharetable"
local tableCache = {} -- tableName → 共享表引用

function common.queryTable(name)
    local data = tableCache[name]
    if data then return data end

    -- 从 schema 查找 file 名
    local file
    for _, tbl in ipairs(tableSchema.tables) do
        if tbl.name == name then
            file = tbl.file
            break
        end
    end
    if not file then return nil end

    data = sharetable.query("tables/data/" .. file)
    if data then
        tableCache[name] = data
    end
    return data
end

--------------------------------------------------------------------------------
-- Map: 地图数据查询与移动合法性校验
-- 地图文件由 sync-maps.ts 从客户端 CSV 生成，存放在 tables/data/map_*.lua
--------------------------------------------------------------------------------
function common.queryMap(mapName)
    local key = "map_" .. mapName
    local data = tableCache[key]
    if data then return data end
    data = sharetable.query("tables/data/" .. key)
    if data then
        tableCache[key] = data
    end
    return data
end

function common.isWalkable(mapName, x, y)
    local mapData = common.queryMap(mapName)
    if not mapData then return false end
    if x < 0 or x >= mapData.width or y < 0 or y >= mapData.height then return false end
    -- Lua 1-based array: index = y * width + x + 1
    local cellStr = mapData.cells[y * mapData.width + x + 1]
    if not cellStr then return false end
    -- 格式: "exists;walkable;visible;terrain;height;custom"
    local walkable = cellStr:match("^%d;(%d)")
    return walkable == "1"
end

--------------------------------------------------------------------------------
-- Platform: 服务通信、定时器、日志
--------------------------------------------------------------------------------
local serviceCache = {}

local function getServiceAddr(name)
    local addr = serviceCache[name]
    if not addr then
        addr = skynet.queryservice(name)
        serviceCache[name] = addr
    end
    return addr
end

common.platform = {
    setTimeout = function(delayMs, callback)
        skynet.timeout(math.max(1, math.floor(delayMs / 10)), callback)
    end,

    serviceCall = function(serviceName, method, ...)
        local ok, result = pcall(skynet.call, getServiceAddr(serviceName), "lua", method, ...)
        if not ok then
            serviceCache[serviceName] = nil
            error(result, 0)
        end
        return result
    end,

    serviceSend = function(serviceName, method, ...)
        skynet.send(getServiceAddr(serviceName), "lua", method, ...)
    end,

    log = function(level, msg, data)
        if data ~= nil then
            skynet.error("[" .. level .. "] " .. msg .. " " .. tostring(data))
        else
            skynet.error("[" .. level .. "] " .. msg)
        end
    end,
}

--------------------------------------------------------------------------------
-- 构造标准错误响应
--------------------------------------------------------------------------------
function common.makeError(msgId, code)
    local rspData = protos.common.Response.encode({ code = code, message = "", data = "" })
    return { msg_id = msgId, data = rspData }
end

--------------------------------------------------------------------------------
-- defineService: 服务注册框架
-- handlers 的 number key → 自动注册 Gateway 路由
-- handlers 的 string key → 普通命令
--------------------------------------------------------------------------------
function common.defineService(name, handlers, options)
    skynet.start(function()
        local routeMsgIds = {}
        for key, _ in pairs(handlers) do
            if type(key) == "number" then
                routeMsgIds[#routeMsgIds + 1] = key
            end
        end

        skynet.dispatch("lua", function(session, address, cmd, ...)
            local handler = handlers[cmd]
            if not handler then
                skynet.error("[Service:" .. name .. "] Unknown command: " .. tostring(cmd))
                return
            end
            local ok, result = pcall(handler, ...)
            if not ok then
                skynet.error("[Service:" .. name .. "] handler error: " .. tostring(result))
                return
            end
            if session > 0 then
                skynet.retpack(result)
            end
        end)

        if #routeMsgIds > 0 then
            skynet.send(skynet.queryservice("game/gateway"), "lua", "register", {
                service_name = name,
                service_addr = skynet.self(),
                routes = routeMsgIds,
            })
        end

        if options and options.init then
            options.init()
        end
    end)
end

--------------------------------------------------------------------------------
-- Event: 基于 skynet multicast 的跨服务事件系统
-- 用法不变:
--   common.event.on("player_login", function(data) ... end)
--   common.event.emit("player_login", { account_id = aid })
-- 底层从集中式 event dispatcher 改为 multicast channel 直发
--------------------------------------------------------------------------------
local mc = require "skynet.multicast"

local eventHandlers = {}  -- eventName → { handler1, handler2, ... }
local channelMap = {}     -- eventName → channel_id (缓存)
local channelObjects = {} -- channel_id → channel object (本地绑定)

-- 通过事件管理服获取 channel ID（带缓存）
local function getChannelId(eventName)
    if channelMap[eventName] then
        return channelMap[eventName]
    end
    local channelId = skynet.call(getServiceAddr("game/event"), "lua", "getChannel", eventName)
    if channelId then
        channelMap[eventName] = channelId
    end
    return channelId
end

common.event = {
    --- 订阅事件（仅注册 handler，defineService 时统一 bind multicast channel）
    on = function(eventName, handler)
        if not eventHandlers[eventName] then
            eventHandlers[eventName] = {}
        end
        eventHandlers[eventName][#eventHandlers[eventName] + 1] = handler
    end,

    --- 发布事件：直接通过 multicast channel 发布，绕过中间服务
    emit = function(eventName, data)
        local channelId = channelMap[eventName]
        if not channelId then
            -- 没有本地缓存，尝试从事件管理服获取（非阻塞，可能 channel 还没创建）
            channelId = getChannelId(eventName)
        end
        if not channelId then
            skynet.error("[Event] No channel for: " .. eventName)
            return
        end
        -- 用临时 channel 对象发布（publish 不需要 subscribe）
        local ch = mc.new { channel = channelId }
        ch:publish(eventName, data)
        ch:delete()
    end,
}

-- 保存原始 defineService，注入 multicast 订阅逻辑
local _defineService = common.defineService
common.defineService = function(name, handlers, options)
    _defineService(name, handlers, {
        init = function()
            -- 批量订阅: 遍历所有已注册的事件 handler，bind 对应的 multicast channel
            local eventNames = {}
            for eventName, _ in pairs(eventHandlers) do
                eventNames[#eventNames + 1] = eventName
            end
            if #eventNames == 0 then
                if options and options.init then options.init() end
                return
            end

            -- 一次性获取所有 channel ID
            local channels = skynet.call(getServiceAddr("game/event"), "lua", "getChannels", eventNames)

            for eventName, channelId in pairs(channels) do
                if channelId then
                    channelMap[eventName] = channelId
                    local ch = mc.new {
                        channel = channelId,
                        dispatch = function(_, _, evName, evData)
                            local handlers_list = eventHandlers[evName]
                            if not handlers_list then return end
                            for _, h in ipairs(handlers_list) do
                                local ok, err = pcall(h, evData)
                                if not ok then
                                    skynet.error("[Event:" .. name .. "] handler error for " .. evName .. ": " .. tostring(err))
                                end
                            end
                        end,
                    }
                    ch:subscribe()
                    channelObjects[channelId] = ch
                    skynet.error("[Event:" .. name .. "] Subscribed: " .. eventName .. " → channel " .. tostring(channelId))
                end
            end

            if options and options.init then options.init() end
        end,
    })
end

--------------------------------------------------------------------------------
-- 密码工具
--------------------------------------------------------------------------------
local function generate_salt()
    local bytes = {}
    for i = 1, 16 do
        bytes[i] = string.char(math.random(0, 255))
    end
    return table.concat(bytes)
end

local b64chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/'
local function base64encode(data)
    return ((data:gsub('.', function(x)
        local r, b = '', x:byte()
        for i = 8, 1, -1 do r = r .. (b % 2^i - b % 2^(i-1) > 0 and '1' or '0') end
        return r
    end) .. '0000'):gsub('%d%d%d?%d?%d?%d?', function(x)
        if (#x < 6) then return '' end
        local c = 0
        for i = 1, 6 do c = c + (x:sub(i,i) == '1' and 2^(6-i) or 0) end
        return b64chars:sub(c+1, c+1)
    end) .. ({ '', '==', '=' })[#data % 3 + 1])
end
local function base64decode(data)
    data = string.gsub(data, '[^'..b64chars..'=]', '')
    return (data:gsub('.', function(x)
        if x == '=' then return '' end
        local r, f = '', (b64chars:find(x) - 1)
        for i = 6, 1, -1 do r = r .. (f % 2^i - f % 2^(i-1) > 0 and '1' or '0') end
        return r
    end):gsub('%d%d%d?%d?%d?%d?%d?%d?', function(x)
        if (#x ~= 8) then return '' end
        local c = 0
        for i = 1, 8 do c = c + (x:sub(i,i) == '1' and 2^(8-i) or 0) end
        return string.char(c)
    end))
end

function common.password_hash(password)
    local salt = generate_salt()
    local hash = md5.sumhexa(salt .. password)
    return base64encode(salt) .. ":" .. hash
end

function common.password_verify(password, stored_hash)
    local salt_b64, hash_hex = stored_hash:match("^([^:]+):([^:]+)$")
    if not salt_b64 then return false end
    local salt = base64decode(salt_b64)
    if not salt then return false end
    return md5.sumhexa(salt .. password) == hash_hex
end

--------------------------------------------------------------------------------
-- Token 系统
--------------------------------------------------------------------------------
local TOKEN_SECRET = skynet.getenv("TOKEN_SECRET") or "tslua2_game_secret_2024"

function common.token_generate_account(account_id, username)
    local ts = tostring(os.time())
    local payload = tostring(account_id) .. ":" .. username .. ":" .. ts
    local sig = md5.sumhexa(TOKEN_SECRET .. payload)
    return base64encode(payload .. "|" .. sig)
end

function common.token_validate_account(token)
    if not token or token == "" then return nil end
    local ok, decoded = pcall(base64decode, token)
    if not ok or not decoded then return nil end
    local payload, sig = decoded:match("^(.+)|(.+)$")
    if not payload then return nil end
    if md5.sumhexa(TOKEN_SECRET .. payload) ~= sig then return nil end
    local aid, uname, ts = payload:match("^(%d+):([^:]+):(%d+)$")
    if not aid then return nil end
    if os.time() - tonumber(ts) > 300 then return nil end
    return { account_id = tonumber(aid), username = uname }
end

function common.token_generate_gateway(account_id, server_id)
    local ts = tostring(os.time())
    local payload = tostring(account_id) .. ":" .. tostring(server_id) .. ":" .. ts
    local sig = md5.sumhexa(TOKEN_SECRET .. payload)
    return base64encode(payload .. "|" .. sig)
end

function common.token_validate_gateway(token)
    if not token or token == "" then return nil end
    local ok, decoded = pcall(base64decode, token)
    if not ok or not decoded then return nil end
    local payload, sig = decoded:match("^(.+)|(.+)$")
    if not payload then return nil end
    if md5.sumhexa(TOKEN_SECRET .. payload) ~= sig then return nil end
    local aid, sid, ts = payload:match("^(%d+):(%d+):(%d+)$")
    if not aid then return nil end
    if os.time() - tonumber(ts) > 604800 then return nil end
    return { account_id = tonumber(aid), server_id = tonumber(sid) }
end

return common
