--------------------------------------------------------------------------------
-- 枚举常量（由 build_proto 自动生成，请勿手动修改）
-- Source: gateway.proto
--------------------------------------------------------------------------------

local MessageType = {
    HEARTBEAT = 0,  -- 心跳
    CONNECT = 1,  -- 连接
    DISCONNECT = 2,  -- 断开
    FORWARD = 3,  -- 转发
    BROADCAST = 4,  -- 广播
    KICK = 5,  -- 踢出
}

return {
    MessageType = MessageType,
}
