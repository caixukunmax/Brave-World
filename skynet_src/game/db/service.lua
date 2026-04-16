-- DB 服务: MongoDB 数据访问层
-- 职责: 账号、角色、区服、玩家的 CRUD 操作

local skynet = require "skynet"
local mongo  = require "skynet.db.mongo"
local common = require "common"
local platform = common.platform

--------------------------------------------------------------------------------
-- 集合引用
--------------------------------------------------------------------------------
local playersCol
local accountsCol
local rolesCol
local serversCol
local countersCol
local inventoriesCol
local chestsCol
local gmChestsCol
local entitySeqMetaCol

--------------------------------------------------------------------------------
-- 内部: 获取自增 ID
--------------------------------------------------------------------------------
local function getNextId(counterName)
    countersCol:update({ _id = counterName }, { ["$inc"] = { seq = 1 } }, true, false)
    local doc = countersCol:findOne({ _id = counterName })
    if not doc then return 1000 end
    if doc.seq < 1000 then
        countersCol:update({ _id = counterName }, { ["$set"] = { seq = 1000 } }, false, false)
        return 1000
    end
    return doc.seq
end

--------------------------------------------------------------------------------
-- 内部: 确保索引
--------------------------------------------------------------------------------
local function ensureIndexes()
    pcall(function()
        accountsCol:ensureIndex({ username = 1 }, true, "username_idx")
        rolesCol:ensureIndex({ server_id = 1, role_name = 1 }, true, "server_role_name_idx")
    end)
end

--------------------------------------------------------------------------------
-- 内部: 查询多条记录为数组
--------------------------------------------------------------------------------
local function findArray(col, query)
    local results = {}
    local cursor = col:find(query)
    while cursor:hasNext() do
        results[#results + 1] = cursor:next()
    end
    cursor:close()
    return results
end

--------------------------------------------------------------------------------
-- 内部: 计数
--------------------------------------------------------------------------------
local function countDocs(col, query)
    local n = 0
    local cursor = col:find(query)
    while cursor:hasNext() do
        n = n + 1
        cursor:next()
    end
    cursor:close()
    return n
end

--------------------------------------------------------------------------------
-- 内部: 种子区服数据
--------------------------------------------------------------------------------
local function seedServers()
    local existing = countDocs(serversCol, {})
    if existing > 0 then return end

    local data = common.queryTable("TbServerConfig")
    if not data then return end

    local now = math.floor(skynet.time())
    local count = 0
    for _, row in pairs(data) do
        serversCol:insert({
            server_id    = row.id,
            server_name  = row.server_name,
            host         = row.host,
            port         = row.port,
            status       = row.status,
            online_count = 0,
            is_new       = row.is_new,
            is_recommend = row.is_recommend,
            created_at   = now,
        })
        count = count + 1
    end
    platform.log("info", "Seeded " .. count .. " servers from config table")
end

--------------------------------------------------------------------------------
-- 命令处理
--------------------------------------------------------------------------------
local CMD = {}

-- ========== players ==========

function CMD.queryPlayer(userId)
    local doc = playersCol:findOne({ user_id = userId })
    if not doc then return nil end
    return { userId = doc.user_id, name = doc.name, level = doc.level, gold = doc.gold }
end

function CMD.savePlayer(player)
    playersCol:update(
        { user_id = player.userId },
        { ["$set"] = { name = player.name, level = player.level, gold = player.gold } },
        true)
end

function CMD.createPlayer(player)
    playersCol:insert({
        user_id = player.userId,
        name    = player.name,
        level   = player.level,
        gold    = player.gold,
    })
end

-- ========== accounts ==========

function CMD.findAccountByUsername(username)
    return accountsCol:findOne({ username = username })
end

function CMD.findAccountById(accountId)
    return accountsCol:findOne({ account_id = accountId })
end

function CMD.createAccount(username, password)
    local accountId = getNextId("account_id")
    local hashedPassword = common.password_hash(password)
    local doc = {
        account_id     = accountId,
        username       = username,
        password       = hashedPassword,
        status         = 0,
        last_server_id = 0,
        last_role_name = "",
        created_at     = skynet.time(),
    }
    accountsCol:insert(doc)
    return doc
end

function CMD.updateAccountLastServer(accountId, serverId, roleName)
    accountsCol:update(
        { account_id = accountId },
        { ["$set"] = { last_server_id = serverId, last_role_name = roleName } },
        false)
end

-- ========== roles ==========

function CMD.findRolesByAccountAndServer(accountId, serverId)
    return findArray(rolesCol, { account_id = accountId, server_id = serverId })
end

function CMD.findAllRolesByAccount(accountId)
    return findArray(rolesCol, { account_id = accountId })
end

function CMD.findRoleById(roleId)
    return rolesCol:findOne({ role_id = roleId })
end

function CMD.createRole(roleData)
    rolesCol:insert(roleData)
end

function CMD.updateRole(roleId, updates)
    rolesCol:update(
        { role_id = roleId },
        { ["$set"] = updates },
        false)
end

function CMD.checkRoleNameExists(serverId, roleName)
    local doc = rolesCol:findOne({ server_id = serverId, role_name = roleName })
    return doc ~= nil
end

function CMD.countRolesByAccountAndServer(accountId, serverId)
    return countDocs(rolesCol, { account_id = accountId, server_id = serverId })
end

function CMD.getNextRoleId()
    return getNextId("role_id")
end

-- ========== inventories ==========

function CMD.getInventory(roleId)
    return findArray(inventoriesCol, { role_id = roleId })
end

function CMD.addItem(roleId, itemId, count)
    local existing = inventoriesCol:findOne({ role_id = roleId, item_id = itemId })
    if existing then
        local newCount = (existing.count or 0) + count
        inventoriesCol:update(
            { role_id = roleId, item_id = itemId },
            { ["$set"] = { count = newCount } },
            false)
    else
        inventoriesCol:insert({ role_id = roleId, item_id = itemId, count = count })
    end
end

function CMD.removeItem(roleId, itemId, count)
    local existing = inventoriesCol:findOne({ role_id = roleId, item_id = itemId })
    if not existing then return false end
    local newCount = (existing.count or 0) - count
    if newCount <= 0 then
        inventoriesCol:delete({ role_id = roleId, item_id = itemId })
    else
        inventoriesCol:update(
            { role_id = roleId, item_id = itemId },
            { ["$set"] = { count = newCount } },
            false)
    end
    return true
end

-- ========== chests (宝箱开启状态) ==========

function CMD.isChestOpened(roleId, chestId)
    return chestsCol:findOne({ role_id = roleId, chest_id = chestId }) ~= nil
end

function CMD.markChestOpened(roleId, chestId)
    chestsCol:safe_insert({ role_id = roleId, chest_id = chestId })
end

function CMD.getOpenedChests(roleId)
    local docs = findArray(chestsCol, { role_id = roleId })
    local ids = {}
    for _, doc in ipairs(docs) do
        ids[doc.chest_id] = true
    end
    return ids
end

-- ========== GM dynamic chests ==========

function CMD.addGmChest(id, mapId, entityType, chestTypeId, x, y, rewards)
    gmChestsCol:safe_insert({
        id = id,
        map_id = mapId,
        entity_type = entityType,
        chest_type_id = chestTypeId,
        x = x,
        y = y,
        rewards = rewards,
    })
end

function CMD.getGmChestsByMapAndType(mapId, entityType)
    return findArray(gmChestsCol, { map_id = mapId, entity_type = entityType })
end

function CMD.allocateEntitySeq(mapId, entityType)
    local metaId = tostring(mapId) .. "_" .. tostring(entityType)
    
    local meta = entitySeqMetaCol:findOne({ _id = metaId })
    if not meta then
        meta = { _id = metaId, map_id = mapId, entity_type = entityType, last_seq = 99, active_count = 0 }
        entitySeqMetaCol:safe_insert(meta)
    end
    
    -- 满员检查 (可用槽位 100~9999 共 9900 个)
    if meta.active_count >= 9900 then
        error("entity seq overflow: map=" .. mapId .. " type=" .. entityType)
    end
    
    -- 查询已占用的 seq（包括 GM 宝箱和配置宝箱）
    local occupied = {}
    local cursor = gmChestsCol:find({ map_id = mapId, entity_type = entityType })
    while cursor:hasNext() do
        local doc = cursor:next()
        occupied[doc.id % 10000] = true
    end
    cursor:close()
    
    -- 同时标记配置宝箱占用的 seq
    local mapChests = common.queryTable("TbMapChest")
    if mapChests then
        for _, c in ipairs(mapChests) do
            if c.map_id == mapId and c.entity_type == entityType then
                occupied[c.id % 10000] = true
            end
        end
    end
    
    -- 从 last_seq + 1 开始扫描，范围 100~9999，绕回 100
    local startSeq = math.max(100, (meta.last_seq + 1) % 10000)
    if startSeq < 100 then startSeq = 100 end
    local seq = startSeq
    while occupied[seq] do
        seq = seq + 1
        if seq > 9999 then seq = 100 end
        if seq == startSeq then
            error("entity seq overflow: map=" .. mapId .. " type=" .. entityType)
        end
    end
    
    entitySeqMetaCol:update(
        { _id = metaId },
        { ["$set"] = { last_seq = seq }, ["$inc"] = { active_count = 1 } },
        true,
        false
    )
    
    return seq
end

function CMD.deallocateEntitySeq(mapId, entityType)
    local metaId = tostring(mapId) .. "_" .. tostring(entityType)
    entitySeqMetaCol:update(
        { _id = metaId },
        { ["$inc"] = { active_count = -1 } },
        true,
        false
    )
end

-- ========== servers ==========

function CMD.getServers()
    return findArray(serversCol, {})
end

function CMD.findServerById(serverId)
    return serversCol:findOne({ server_id = serverId })
end

function CMD.updateServerOnlineCount(serverId, count)
    serversCol:update(
        { server_id = serverId },
        { ["$set"] = { online_count = count } },
        false)
end

--------------------------------------------------------------------------------
-- 启动
--------------------------------------------------------------------------------
common.defineService("db", CMD, {
    init = function()
        local host = os.getenv("MONGO_HOST") or skynet.getenv("MONGO_HOST") or "127.0.0.1"
        local port = tonumber(os.getenv("MONGO_PORT") or skynet.getenv("MONGO_PORT") or "27017")
        platform.log("info", "Connecting to MongoDB", host .. ":" .. port)

        local client = mongo.client({ host = host, port = port })
        local db = client["tslua2"]
        playersCol  = db["players"]
        accountsCol = db["accounts"]
        rolesCol    = db["roles"]
        serversCol  = db["servers"]
        countersCol = db["counters"]
        inventoriesCol = db["inventories"]
        chestsCol = db["chests"]
        gmChestsCol = db["gm_chests"]
        entitySeqMetaCol = db["entity_seq_meta"]

        -- 清理旧格式 gm_chests（id >= 20000 且无 entity_type 字段）
        pcall(function()
            gmChestsCol:delete({ id = { ["$gte"] = 20000 }, entity_type = { ["$exists"] = false } })
        end)

        -- 清理与配置宝箱 ID 冲突的 GM 宝箱
        pcall(function()
            local mapChests = common.queryTable("TbMapChest")
            if mapChests then
                for _, c in ipairs(mapChests) do
                    gmChestsCol:delete({ id = c.id })
                end
            end
        end)

        platform.log("info", "MongoDB connected")
        ensureIndexes()
        seedServers()
        platform.log("info", "db_service started")
    end,
})
