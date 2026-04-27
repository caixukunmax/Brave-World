--------------------------------------------------------------------------------
-- 枚举常量（由 build_proto 自动生成，请勿手动修改）
-- Source: common.proto
--------------------------------------------------------------------------------

local ServerStatus = {
    MAINTENANCE = 0,  -- 维护中
    SMOOTH = 1,  -- 流畅
    CROWDED = 2,  -- 拥挤
    FULL = 3,  -- 爆满
}

local ErrorCode = {
    SUCCESS = 0,  -- 成功
    UNKNOWN_ERROR = 1,  -- 未知错误
    INVALID_REQUEST = 2,  -- 无效请求
    UNAUTHORIZED = 3,  -- 未授权
    FORBIDDEN = 4,  -- 禁止访问
    NOT_FOUND = 5,  -- 未找到
    TIMEOUT = 6,  -- 超时
    INTERNAL_ERROR = 7,  -- 内部错误
    SERVICE_UNAVAILABLE = 8,  -- 服务不可用
    ACCOUNT_NOT_FOUND = 100,  -- 账号不存在
    PASSWORD_ERROR = 101,  -- 密码错误
    ACCOUNT_BANNED = 102,  -- 账号被封禁
    ACCOUNT_ALREADY_EXISTS = 103,  -- 账号已存在
    INVALID_ACCOUNT_FORMAT = 104,  -- 账号格式错误
    INVALID_PASSWORD_FORMAT = 105,  -- 密码格式错误
    ROLE_NOT_FOUND = 200,  -- 角色不存在
    ROLE_NAME_EXISTS = 201,  -- 角色名已存在
    ROLE_COUNT_LIMIT = 202,  -- 角色数量达到上限
    INVALID_ROLE_NAME = 203,  -- 角色名格式错误
    ROLE_NAME_TOO_SHORT = 204,  -- 角色名太短
    ROLE_NAME_TOO_LONG = 205,  -- 角色名太长
    ROLE_NAME_SENSITIVE = 206,  -- 角色名包含敏感词
    SERVER_NOT_FOUND = 300,  -- 区服不存在
    SERVER_MAINTENANCE = 301,  -- 区服维护中
    SERVER_FULL = 302,  -- 区服已满
}

return {
    ServerStatus = ServerStatus,
    ErrorCode = ErrorCode,
}
