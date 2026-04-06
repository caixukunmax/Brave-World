local ____lualib = require("lualib_bundle")
local Map = ____lualib.Map
local __TS__New = ____lualib.__TS__New
local __TS__SourceMapTraceBack = ____lualib.__TS__SourceMapTraceBack
__TS__SourceMapTraceBack(debug.getinfo(1).short_src, {["7"] = 4,["8"] = 6,["9"] = 7,["10"] = 8,["11"] = 9,["12"] = 10,["14"] = 12,["15"] = 6,["16"] = 16,["17"] = 17,["18"] = 17,["19"] = 17,["20"] = 17,["21"] = 16,["22"] = 20,["23"] = 21,["24"] = 22,["25"] = 22,["26"] = 22,["27"] = 22,["28"] = 20,["29"] = 25,["30"] = 20,["31"] = 29,["32"] = 30,["33"] = 30,["34"] = 30,["35"] = 30,["36"] = 30,["37"] = 30,["38"] = 20,["39"] = 33,["40"] = 34,["41"] = 34,["42"] = 34,["43"] = 34,["44"] = 34,["45"] = 34,["46"] = 20,["47"] = 37,["48"] = 38,["49"] = 39,["51"] = 41,["53"] = 20,["54"] = 20});
local ____exports = {}
local serviceCache = __TS__New(Map)
local function getServiceAddr(name)
    local addr = serviceCache:get(name)
    if addr == nil then
        addr = skynet.queryservice(name)
        serviceCache:set(name, addr)
    end
    return addr
end
local function msToTicks(ms)
    return math.max(
        1,
        math.floor(ms / 10)
    )
end
____exports.platform = {
    setTimeout = function(delayMs, callback)
        return skynet.timeout(
            msToTicks(delayMs),
            callback
        )
    end,
    clearInterval = function(id)
    end,
    serviceCall = function(serviceName, method, ...)
        return skynet.call(
            getServiceAddr(serviceName),
            "lua",
            method,
            ...
        )
    end,
    serviceSend = function(serviceName, method, ...)
        skynet.send(
            getServiceAddr(serviceName),
            "lua",
            method,
            ...
        )
    end,
    log = function(level, msg, data)
        if data ~= nil then
            skynet.error((((("[" .. level) .. "] ") .. msg) .. " ") .. tostring(data))
        else
            skynet.error((("[" .. level) .. "] ") .. msg)
        end
    end
}
return ____exports
