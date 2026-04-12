--------------------------------------------------------------------------------
-- 消息编解码（由 build_proto 自动生成，请勿手动修改）
-- Source: game.proto  Package: game
--------------------------------------------------------------------------------
local pb = require "pb"

local M = {}

-- Fields: role_id(uint64) -- 角色ID role_name(string) -- 角色名 level(uint32) -- 等级 exp(uint64) -- 经验值 avatar_id(uint32) -- 头像ID gold(uint64) -- 金币 diamond(uint64) -- 钻石 total_power(uint64) -- 总战力 vip_level(uint32) -- VIP等级 create_time(uint64) -- 创建时间 last_login_time(uint64) -- 上次登录时间 job(string) -- 职业 title(string) -- 称号 status(string) -- 状态
M.FullRoleInfo = {
    encode = function(data) return pb.encode("game.FullRoleInfo", data) end,
    decode = function(data) return pb.decode("game.FullRoleInfo", data) end,
}

-- Fields: item_id(uint32) -- 道具ID count(uint32) -- 数量
M.ItemInfo = {
    encode = function(data) return pb.encode("game.ItemInfo", data) end,
    decode = function(data) return pb.decode("game.ItemInfo", data) end,
}

-- Fields: task_id(uint32) -- 任务ID status(uint32) -- 状态：0未接 1进行中 2已完成 progress(uint32) -- 进度
M.TaskInfo = {
    encode = function(data) return pb.encode("game.TaskInfo", data) end,
    decode = function(data) return pb.decode("game.TaskInfo", data) end,
}

-- Fields: role_id(uint64) -- 角色ID
M.EnterGameRequest = {
    encode = function(data) return pb.encode("game.EnterGameRequest", data) end,
    decode = function(data) return pb.decode("game.EnterGameRequest", data) end,
}

-- Fields: code(common.ErrorCode) message(string) role_info(FullRoleInfo) -- 完整角色信息 items(ItemInfo[]) -- 背包道具 tasks(TaskInfo[]) -- 任务列表 server_time(uint32) -- 服务器时间
M.EnterGameResponse = {
    encode = function(data) return pb.encode("game.EnterGameResponse", data) end,
    decode = function(data) return pb.decode("game.EnterGameResponse", data) end,
}

-- Fields: role_name(string) -- 角色名
M.CreateRoleRequest = {
    encode = function(data) return pb.encode("game.CreateRoleRequest", data) end,
    decode = function(data) return pb.decode("game.CreateRoleRequest", data) end,
}

-- Fields: code(common.ErrorCode) message(string) role_info(FullRoleInfo) -- 新创建的角色信息 items(ItemInfo[]) -- 初始道具 tasks(TaskInfo[]) -- 初始任务 server_time(uint32) -- 服务器时间
M.CreateRoleResponse = {
    encode = function(data) return pb.encode("game.CreateRoleResponse", data) end,
    decode = function(data) return pb.decode("game.CreateRoleResponse", data) end,
}

-- Fields: from_x(int32) -- 起点 X from_y(int32) -- 起点 Y to_x(int32) -- 终点 X to_y(int32) -- 终点 Y map_name(string) -- 地图名
M.MoveRequest = {
    encode = function(data) return pb.encode("game.MoveRequest", data) end,
    decode = function(data) return pb.decode("game.MoveRequest", data) end,
}

-- Fields: code(common.ErrorCode) message(string) x(int32) -- 服务器确认的位置 X（失败时为合法位置） y(int32) -- 服务器确认的位置 Y
M.MoveResponse = {
    encode = function(data) return pb.encode("game.MoveResponse", data) end,
    decode = function(data) return pb.decode("game.MoveResponse", data) end,
}

-- Fields: item_id(uint32) -- 物品ID count(uint32) -- 使用数量
M.UseItemRequest = {
    encode = function(data) return pb.encode("game.UseItemRequest", data) end,
    decode = function(data) return pb.decode("game.UseItemRequest", data) end,
}

-- Fields: code(common.ErrorCode) message(string) items(ItemInfo[]) -- 更新后的完整背包
M.UseItemResponse = {
    encode = function(data) return pb.encode("game.UseItemResponse", data) end,
    decode = function(data) return pb.decode("game.UseItemResponse", data) end,
}

-- Fields: item_id(uint32) -- 物品ID count(uint32) -- 丢弃数量
M.DropItemRequest = {
    encode = function(data) return pb.encode("game.DropItemRequest", data) end,
    decode = function(data) return pb.decode("game.DropItemRequest", data) end,
}

-- Fields: code(common.ErrorCode) message(string) items(ItemInfo[]) -- 更新后的完整背包
M.DropItemResponse = {
    encode = function(data) return pb.encode("game.DropItemResponse", data) end,
    decode = function(data) return pb.decode("game.DropItemResponse", data) end,
}

return M
