local ____lualib = require("lualib_bundle")
local Map = ____lualib.Map
local __TS__New = ____lualib.__TS__New
local __TS__SourceMapTraceBack = ____lualib.__TS__SourceMapTraceBack
__TS__SourceMapTraceBack(debug.getinfo(1).short_src, {["7"] = 4,["8"] = 7,["9"] = 8,["10"] = 10,["11"] = 11,["12"] = 11,["13"] = 10,["14"] = 14,["15"] = 15,["16"] = 16,["17"] = 17,["18"] = 18,["20"] = 20,["21"] = 14,["22"] = 24,["23"] = 25,["24"] = 25,["25"] = 25,["26"] = 25,["27"] = 24,["28"] = 28,["29"] = 29,["30"] = 30,["31"] = 31,["32"] = 32,["33"] = 32,["34"] = 32,["35"] = 33,["36"] = 34,["37"] = 35,["39"] = 32,["40"] = 32,["41"] = 38,["42"] = 28,["43"] = 41,["44"] = 42,["45"] = 28,["46"] = 45,["47"] = 45,["50"] = 50,["51"] = 51,["54"] = 47,["55"] = 47,["56"] = 47,["57"] = 47,["58"] = 47,["59"] = 47,["65"] = 46,["68"] = 28,["69"] = 55,["70"] = 56,["71"] = 56,["72"] = 56,["73"] = 56,["74"] = 56,["75"] = 56,["76"] = 28,["77"] = 59,["78"] = 60,["79"] = 61,["81"] = 63,["83"] = 28,["84"] = 28});
local ____exports = {}
local serviceCache = __TS__New(Map)
local timerIdCounter = 1000
local pendingTimers = __TS__New(Map)
local function generateTimerId()
    timerIdCounter = timerIdCounter + 1
    return timerIdCounter
end
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
        local id = generateTimerId()
        pendingTimers:set(id, true)
        skynet.timeout(
            msToTicks(delayMs),
            function()
                if pendingTimers:get(id) then
                    pendingTimers:delete(id)
                    callback()
                end
            end
        )
        return id
    end,
    clearInterval = function(id)
        pendingTimers:delete(id)
    end,
    serviceCall = function(serviceName, method, ...)
        local args = {...}
        do
            local function ____catch(e)
                serviceCache:delete(serviceName)
                error(e, 0)
            end
            local ____try, ____hasReturned, ____returnValue = pcall(function()
                return true, skynet.call(
                    getServiceAddr(serviceName),
                    "lua",
                    method,
                    unpack(args)
                )
            end)
            if not ____try then
                ____hasReturned, ____returnValue = ____catch(____hasReturned)
            end
            if ____hasReturned then
                return ____returnValue
            end
        end
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
