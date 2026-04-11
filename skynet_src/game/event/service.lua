-- Event Channel 管理服务: 为每个事件类型创建/管理 multicast channel
-- 职责: 启动时根据 event/types.lua 预创建所有 channel，维护映射表

local skynet = require "skynet"
local mc = require "skynet.multicast"
local eventTypes = require "event.types"

--------------------------------------------------------------------------------
-- channel 映射表: { eventName → channel_id }
--------------------------------------------------------------------------------
local channels = {}

--------------------------------------------------------------------------------
-- 命令处理（纯查询，channel 已在启动时全部预创建）
--------------------------------------------------------------------------------
local CMD = {}

--- 获取事件的 channel ID
function CMD.getChannel(source, eventName)
    return channels[eventName]
end

--- 批量获取多个事件的 channel ID
function CMD.getChannels(source, eventNames)
    local result = {}
    for _, eventName in ipairs(eventNames) do
        result[eventName] = channels[eventName]
    end
    return result
end

--- 查询所有 channel 信息（调试用）
function CMD.info(source)
    local info = {}
    for eventName, channelId in pairs(channels) do
        info[#info + 1] = string.format("  %s → channel %d", eventName, channelId)
    end
    if #info == 0 then
        return "No channels"
    end
    return table.concat(info, "\n")
end

--------------------------------------------------------------------------------
-- 服务启动: 遍历 event/types.lua 预创建所有 multicast channel
--------------------------------------------------------------------------------
skynet.start(function()
    local count = 0
    for _, eventName in pairs(eventTypes) do
        local ch = mc.new()
        channels[eventName] = ch.channel
        count = count + 1
        skynet.error("[Event] Channel created: " .. eventName .. " → " .. tostring(ch.channel))
        ch:delete()
    end

    skynet.error("======== Event Channel Manager Started (" .. count .. " channels) ========")

    skynet.dispatch("lua", function(session, address, cmd, ...)
        local f = CMD[cmd]
        if not f then
            skynet.error("[Event] Unknown command: " .. tostring(cmd))
            return
        end
        local ok, result = pcall(f, address, ...)
        if not ok then
            skynet.error("[Event] Error handling " .. cmd .. ": " .. tostring(result))
            return
        end
        if session > 0 and result ~= nil then
            skynet.retpack(result)
        end
    end)
end)
