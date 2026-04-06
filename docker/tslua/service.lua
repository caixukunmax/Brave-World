local ____lualib = require("lualib_bundle")
local __TS__SourceMapTraceBack = ____lualib.__TS__SourceMapTraceBack
__TS__SourceMapTraceBack(debug.getinfo(1).short_src, {["5"] = 1,["6"] = 5,["7"] = 6,["8"] = 6,["9"] = 6,["10"] = 7,["11"] = 8,["12"] = 9,["15"] = 12,["16"] = 15,["17"] = 16,["19"] = 6,["20"] = 6,["21"] = 20,["22"] = 21,["24"] = 5,["25"] = 1});
local ____exports = {}
function ____exports.defineService(commands, init)
    skynet.start(function()
        skynet.dispatch(
            "lua",
            function(session, address, cmd, ...)
                local handler = commands[cmd]
                if not handler then
                    skynet.error("Unknown command: " .. cmd)
                    return
                end
                local result = handler(...)
                if session > 0 and result ~= nil then
                    skynet.retpack(result)
                end
            end
        )
        if init then
            init()
        end
    end)
end
return ____exports
