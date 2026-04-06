-- tslua2 启动引导
local skynet = require "skynet"

skynet.start(function()
    skynet.error("======== tslua2 Server Starting ========")

    -- 调试控制台（Skynet 内置 C 服务，接受端口号）
    local DEBUG_PORT = tonumber(skynet.getenv "DEBUG_PORT") or 8000
    skynet.newservice("debug_console", DEBUG_PORT)
    skynet.error("Debug console on 127.0.0.1:" .. DEBUG_PORT)

    -- 1. 数据库服务（其他服务依赖）
    skynet.uniqueservice("db/service")
    skynet.error("db_service started")

    -- 2. 网关服务（TCP监听，业务服务启动后注册路由）
    skynet.uniqueservice("gateway_service")
    skynet.error("gateway_service started")

    -- 3. Login 服务（启动时向 Gateway 注册 msg_id 210,212 路由）
    skynet.uniqueservice("login/service")
    skynet.error("login_service started")

    -- 4. Game 服务（启动时向 Gateway 注册 msg_id 320,322 路由）
    skynet.uniqueservice("game/service")
    skynet.error("game_service started")

    -- 保留 player 服务（原有）
    skynet.uniqueservice("player/service")

    skynet.error("======== tslua2 Server Ready ========")
    -- 不退出，保持主服务存活以响应查询
end)
