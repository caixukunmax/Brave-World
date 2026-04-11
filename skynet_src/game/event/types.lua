-- 事件类型定义
-- 所有跨服务事件名集中管理，避免字符串拼写错误

return {
    -- 玩家事件
    PLAYER_LOGIN       = "player_login",        -- 玩家登录
    PLAYER_LOGOUT      = "player_logout",       -- 玩家登出
    PLAYER_KICK        = "player_kick",         -- 玩家被踢

    -- 角色事件
    ROLE_CREATE        = "role_create",         -- 创建角色
    ROLE_LEVELUP       = "role_levelup",        -- 角色升级

    -- 连接事件
    CONNECTION_OPEN    = "connection_open",     -- 新连接建立
    CONNECTION_CLOSE   = "connection_close",    -- 连接断开
}
