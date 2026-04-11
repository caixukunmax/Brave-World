-- Skynet preload: inject globals for tstl-generated code
skynet = require "skynet"
mongo = require "skynet.db.mongo"

-- Lua 5.4 兼容: unpack 已移至 table.unpack
unpack = table.unpack

-- 配置表已由 game/table 服务统一加载到 sharetable，各服务通过 common.queryTable 查询
-- preload 不再全局 require 配置表

-- tstl 冒号调用辅助：tstl 无法对任意对象生成 Lua 冒号语法
mongo_findOne = function(col, query) return col:findOne(query) end
mongo_insert = function(col, doc) col:insert(doc) end
mongo_update = function(col, query, update, upsert, multi) col:update(query, update, upsert, multi) end
mongo_delete = function(col, query, single) col:delete(query, single) end

-- 确保索引（tstl 包装）
mongo_ensureIndex = function(col, spec) 
    -- spec 格式: { key = { field: 1 }, unique = true, name = "idx_name" }
    if spec and spec.key then
        pcall(function() col:ensureIndex(spec.key, spec.unique or false, spec.name or nil) end)
    end
end

-- findAndModify 原子操作（tstl 包装）
mongo_findAndModify = function(col, options)
    -- options: { query, update, upsert, new }
    return col:findAndModify(options)
end

-- 查询多条记录，返回数组
mongo_findArray = function(col, query)
    local results = {}
    local cursor = col:find(query)
    while cursor:hasNext() do
        local doc = cursor:next()
        table.insert(results, doc)
    end
    cursor:close()
    return results
end

-- 计数
mongo_count = function(col, query)
    local count = 0
    local cursor = col:find(query)
    while cursor:hasNext() do
        count = count + 1
        cursor:next()
    end
    cursor:close()
    return count
end

-- === lua-protobuf ===
local pb = require "pb"

-- 加载所有 .desc 文件
local desc_dir = "protos/"
pcall(pb.loadfile, desc_dir .. "common_pb.desc")
pcall(pb.loadfile, desc_dir .. "login_pb.desc")
pcall(pb.loadfile, desc_dir .. "game_pb.desc")
pcall(pb.loadfile, desc_dir .. "server_pb.desc")
pcall(pb.loadfile, desc_dir .. "gateway_pb.desc")
pcall(pb.loadfile, desc_dir .. "message_id_pb.desc")

-- tstl 冒号调用包装
pb_decode = function(msg_type, data) return pb.decode(msg_type, data) end
pb_encode = function(msg_type, data) return pb.encode(msg_type, data) end

-- === 密码加密工具 ===
local md5 = require "md5.core"

-- 生成随机盐值
local function generate_salt()
    local bytes = {}
    for i = 1, 16 do
        table.insert(bytes, string.char(math.random(0, 255)))
    end
    return table.concat(bytes)
end

-- base64 编解码 (纯 Lua 实现)
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

-- 密码哈希: 返回 "salt:hash"
password_hash = function(password)
    local salt = generate_salt()
    local hash = md5.sumhexa(salt .. password)
    return base64encode(salt) .. ":" .. hash
end

-- 密码验证
password_verify = function(password, stored_hash)
    local salt_b64, hash_hex = stored_hash:match("^([^:]+):([^:]+)$")
    if not salt_b64 then return false end
    local salt = base64decode(salt_b64)
    if not salt then return false end
    local expected = md5.sumhexa(salt .. password)
    return hash_hex == expected
end

-- === Token 系统 ===
local TOKEN_SECRET = skynet.getenv("TOKEN_SECRET") or "tslua2_game_secret_2024"

-- AccountToken: base64(payload|md5hex(secret+payload))
token_generate_account = function(account_id, username)
    local ts = tostring(os.time())
    local payload = tostring(account_id) .. ":" .. username .. ":" .. ts
    local sig = md5.sumhexa(TOKEN_SECRET .. payload)
    return base64encode(payload .. "|" .. sig)
end

token_validate_account = function(token)
    if not token or token == "" then return nil end
    local ok, decoded = pcall(base64decode, token)
    if not ok or not decoded then return nil end
    local payload, sig = decoded:match("^(.+)|(.+)$")
    if not payload then return nil end
    local expected = md5.sumhexa(TOKEN_SECRET .. payload)
    if sig ~= expected then return nil end
    local aid, uname, ts = payload:match("^(%d+):([^:]+):(%d+)$")
    if not aid then return nil end
    if os.time() - tonumber(ts) > 300 then return nil end
    return { account_id = tonumber(aid), username = uname }
end

-- GatewayToken: base64(payload|md5hex(secret+payload))
token_generate_gateway = function(account_id, server_id)
    local ts = tostring(os.time())
    local payload = tostring(account_id) .. ":" .. tostring(server_id) .. ":" .. ts
    local sig = md5.sumhexa(TOKEN_SECRET .. payload)
    return base64encode(payload .. "|" .. sig)
end

token_validate_gateway = function(token)
    if not token or token == "" then return nil end
    local ok, decoded = pcall(base64decode, token)
    if not ok or not decoded then return nil end
    local payload, sig = decoded:match("^(.+)|(.+)$")
    if not payload then return nil end
    local expected = md5.sumhexa(TOKEN_SECRET .. payload)
    if sig ~= expected then return nil end
    local aid, sid, ts = payload:match("^(%d+):(%d+):(%d+)$")
    if not aid then return nil end
    if os.time() - tonumber(ts) > 604800 then return nil end
    return { account_id = tonumber(aid), server_id = tonumber(sid) }
end
