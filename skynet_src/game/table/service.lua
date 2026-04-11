-- Table 共享加载服务: 启动时将所有配置表加载到 skynet sharetable
-- 职责: 集中加载、支持热更新、内存共享（所有服务共享同一份数据）

local skynet = require "skynet"
local sharetable = require "skynet.sharetable"
local schema = require "tables.schema"

--------------------------------------------------------------------------------
-- 命令处理
--------------------------------------------------------------------------------
local CMD = {}

--- 查询所有已加载的表名（调试用）
function CMD.info(source)
    local names = {}
    for _, tbl in ipairs(schema.tables) do
        names[#names + 1] = tbl.name .. " (" .. tbl.file .. ")"
    end
    return table.concat(names, "\n")
end

--- 热更新指定表（重新 loadfile）
-- @param source skynet.address
-- @param tableNames string[] 要更新的表名列表，nil 表示全部更新
function CMD.reload(source, tableNames)
    local toReload = {}
    if tableNames then
        for _, name in ipairs(tableNames) do
            for _, tbl in ipairs(schema.tables) do
                if tbl.name == name or tbl.file == name then
                    toReload[#toReload + 1] = tbl
                    break
                end
            end
        end
    else
        toReload = schema.tables
    end

    local files = {}
    for _, tbl in ipairs(toReload) do
        sharetable.loadfile("tables/data/" .. tbl.file .. ".lua")
        files[#files + 1] = tbl.file
        skynet.error("[Table] Reloaded: " .. tbl.name .. " ← " .. tbl.file)
    end

    return files
end

--------------------------------------------------------------------------------
-- 服务启动: 根据 schema.lua 加载所有配置表到 sharetable
--------------------------------------------------------------------------------
skynet.start(function()
    local count = 0
    for _, tbl in ipairs(schema.tables) do
        sharetable.loadfile("tables/data/" .. tbl.file .. ".lua")
        count = count + 1
    end

    skynet.error("======== Table Service Started (" .. count .. " tables loaded) ========")

    skynet.dispatch("lua", function(session, address, cmd, ...)
        local f = CMD[cmd]
        if not f then
            skynet.error("[Table] Unknown command: " .. tostring(cmd))
            return
        end
        local ok, result = pcall(f, address, ...)
        if not ok then
            skynet.error("[Table] Error handling " .. cmd .. ": " .. tostring(result))
            return
        end
        if session > 0 and result ~= nil then
            skynet.retpack(result)
        end
    end)
end)
