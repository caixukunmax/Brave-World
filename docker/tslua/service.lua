local ____lualib = require("lualib_bundle")
local __TS__SourceMapTraceBack = ____lualib.__TS__SourceMapTraceBack
__TS__SourceMapTraceBack(debug.getinfo(1).short_src, {["9"] = 11,["10"] = 16,["11"] = 18,["12"] = 19,["13"] = 20,["14"] = 21,["15"] = 22,["16"] = 23,["19"] = 27,["20"] = 27,["21"] = 27,["22"] = 28,["23"] = 29,["24"] = 30,["25"] = 31,["28"] = 34,["29"] = 35,["30"] = 36,["32"] = 27,["33"] = 27,["34"] = 41,["35"] = 42,["36"] = 43,["37"] = 43,["38"] = 43,["39"] = 43,["40"] = 43,["41"] = 43,["42"] = 43,["43"] = 43,["44"] = 43,["45"] = 43,["47"] = 50,["48"] = 51,["50"] = 16,["51"] = 11});
local ____exports = {}
--- 定义 Skynet 服务
-- handlers 的 key 可以是:
--   - number (MessageId 枚举): 自动注册为 Gateway 路由
--   - string: 内部命令，其他服务通过 skynet.call 调用
function ____exports.defineService(name, handlers, options)
    skynet.start(function()
        local routeCount = 0
        local routeMsgIds = {}
        for key in pairs(handlers) do
            if type(key) == "number" then
                table.insert(routeMsgIds, key)
                routeCount = routeCount + 1
            end
        end
        skynet.dispatch(
            "lua",
            function(session, address, cmd, ...)
                skynet.error((((("[Service:" .. name) .. "] dispatch cmd=") .. tostring(cmd)) .. " session=") .. tostring(session))
                local handler = handlers[cmd]
                if not handler then
                    skynet.error("Unknown command: " .. tostring(cmd))
                    return
                end
                local result = handler(...)
                if session > 0 and result ~= nil then
                    skynet.retpack(result)
                end
            end
        )
        if routeCount > 0 then
            local gatewayAddr = skynet.queryservice("gateway_service")
            skynet.send(
                gatewayAddr,
                "lua",
                "register",
                {
                    service_name = name,
                    service_addr = skynet.self(),
                    routes = routeMsgIds
                }
            )
        end
        if options and options.init then
            options:init()
        end
    end)
end
return ____exports
