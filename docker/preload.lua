-- Skynet preload: inject globals for tstl-generated code
skynet = require "skynet"
mongo = require "skynet.db.mongo"

-- tstl 冒号调用辅助：tstl 无法对任意对象生成 Lua 冒号语法
mongo_findOne = function(col, query) return col:findOne(query) end
mongo_insert = function(col, doc) col:insert(doc) end
mongo_update = function(col, query, update, upsert, multi) col:update(query, update, upsert, multi) end
mongo_delete = function(col, query, single) col:delete(query, single) end

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
local desc_dir = "lualib/tslua/protos/"
pcall(pb.loadfile, desc_dir .. "common_pb.desc")
pcall(pb.loadfile, desc_dir .. "login_pb.desc")
pcall(pb.loadfile, desc_dir .. "game_pb.desc")
pcall(pb.loadfile, desc_dir .. "server_pb.desc")
pcall(pb.loadfile, desc_dir .. "gateway_pb.desc")
pcall(pb.loadfile, desc_dir .. "message_id_pb.desc")

-- tstl 冒号调用包装
pb_decode = function(msg_type, data) return pb.decode(msg_type, data) end
pb_encode = function(msg_type, data) return pb.encode(msg_type, data) end

-- === 密码加密工具 (SHA256 + Salt) ===
local crypt = require "skynet.crypt"

-- 生成随机盐值
local function generate_salt()
    local bytes = {}
    for i = 1, 16 do
        table.insert(bytes, string.char(math.random(0, 255)))
    end
    return table.concat(bytes)
end

-- 密码哈希: 返回 "salt:hash"
password_hash = function(password)
    local salt = generate_salt()
    local hash = crypt.sha256(salt .. password)
    return crypt.base64encode(salt) .. ":" .. crypt.base64encode(hash)
end

-- 密码验证
password_verify = function(password, stored_hash)
    local salt_b64, hash_b64 = stored_hash:match("^([^:]+):([^:]+)$")
    if not salt_b64 then return false end
    
    local ok, salt = pcall(crypt.base64decode, salt_b64)
    if not ok or not salt then return false end
    
    local expected_hash = crypt.sha256(salt .. password)
    local expected_b64 = crypt.base64encode(expected_hash)
    return hash_b64 == expected_b64
end

-- === Token 系统 (HMAC-SHA256 via skynet.crypt) ===
local TOKEN_SECRET = skynet.getenv("TOKEN_SECRET") or "tslua2_game_secret_2024_change_in_production"

-- AccountToken: payload=accountId:username:timestamp  签名=hmac  编码=base64(payload|sig)
token_generate_account = function(account_id, username)
    local ts = tostring(os.time())
    local payload = tostring(account_id) .. ":" .. username .. ":" .. ts
    local sig = crypt.hmac_sha256(TOKEN_SECRET, payload)
    return crypt.base64encode(payload .. "|" .. sig)
end

token_validate_account = function(token)
    if not token or token == "" then return nil end
    local ok, decoded = pcall(crypt.base64decode, token)
    if not ok or not decoded then return nil end
    local payload, sig = decoded:match("^(.+)|(.+)$")
    if not payload then return nil end
    local expected = crypt.hmac_sha256(TOKEN_SECRET, payload)
    if sig ~= expected then return nil end
    local aid, uname, ts = payload:match("^(%d+):([^:]+):(%d+)$")
    if not aid then return nil end
    if os.time() - tonumber(ts) > 300 then return nil end
    return { account_id = tonumber(aid), username = uname }
end

-- GatewayToken: payload=accountId:serverId:timestamp  签名=hmac  编码=base64(payload|sig)
token_generate_gateway = function(account_id, server_id)
    local ts = tostring(os.time())
    local payload = tostring(account_id) .. ":" .. tostring(server_id) .. ":" .. ts
    local sig = crypt.hmac_sha256(TOKEN_SECRET, payload)
    return crypt.base64encode(payload .. "|" .. sig)
end

token_validate_gateway = function(token)
    if not token or token == "" then return nil end
    local ok, decoded = pcall(crypt.base64decode, token)
    if not ok or not decoded then return nil end
    local payload, sig = decoded:match("^(.+)|(.+)$")
    if not payload then return nil end
    local expected = crypt.hmac_sha256(TOKEN_SECRET, payload)
    if sig ~= expected then return nil end
    local aid, sid, ts = payload:match("^(%d+):(%d+):(%d+)$")
    if not aid then return nil end
    if os.time() - tonumber(ts) > 604800 then return nil end
    return { account_id = tonumber(aid), server_id = tonumber(sid) }
end
