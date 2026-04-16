-- skynet_src 启动引导
local skynet = require "skynet"

skynet.start(function()
    skynet.error("======== skynet_src Server Starting ========")

    -- 调试控制台
    local DEBUG_PORT = tonumber(skynet.getenv "DEBUG_PORT") or 8000
    skynet.newservice("debug_console", DEBUG_PORT)
    skynet.error("Debug console on 127.0.0.1:" .. DEBUG_PORT)

    -- 1. 配置表服务（sharetable 共享加载，其他服务依赖）
    skynet.uniqueservice("game/table")
    skynet.error("table_service started")

    -- 2. 数据库服务（其他服务依赖）
    skynet.uniqueservice("game/db")
    skynet.error("db_service started")

    -- 3. 事件中心（其他服务可能依赖）
    skynet.uniqueservice("game/event")
    skynet.error("event_service started")

    -- 4. 网关服务（TCP监听）
    skynet.uniqueservice("game/gateway")
    skynet.error("gateway_service started")

    -- 5. Login 服务（路由 msg_id 210, 212）
    skynet.uniqueservice("game/login")
    skynet.error("login_service started")

    -- 6. 玩家管理服务（管理器 + 4 个玩家池）
    skynet.uniqueservice("game/player_mgr")
    skynet.error("player_mgr_service started")

    -- 7. 怪物 AI 服务
    skynet.uniqueservice("game/monster_pool")
    skynet.error("monster_pool_service started")

    skynet.error("======== skynet_src Server Ready ========")
end)
