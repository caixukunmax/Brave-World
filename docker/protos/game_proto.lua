--------------------------------------------------------------------------------
-- 消息编解码（由 build_proto 自动生成，请勿手动修改）
-- Source: game.proto  Package: game
--------------------------------------------------------------------------------
local pb = require "pb"

local M = {}

-- Fields: role_id(uint64) -- 角色ID role_name(string) -- 角色名 level(uint32) -- 等级 exp(uint64) -- 经验值 avatar_id(uint32) -- 头像ID gold(uint64) -- 金币 diamond(uint64) -- 钻石 total_power(uint64) -- 总战力 vip_level(uint32) -- VIP等级 create_time(uint64) -- 创建时间 last_login_time(uint64) -- 上次登录时间 job(string) -- 职业 title(string) -- 称号 status(string) -- 状态 current_map(string) -- 当前地图 grid_x(int32) -- 格子X grid_y(int32) -- 格子Y
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

-- Fields: code(common.ErrorCode) message(string) role_info(FullRoleInfo) -- 完整角色信息 items(ItemInfo[]) -- 背包道具 tasks(TaskInfo[]) -- 任务列表 server_time(uint32) -- 服务器时间 chests(ChestInfo[]) -- 地图宝箱列表
M.EnterGameResponse = {
    encode = function(data) return pb.encode("game.EnterGameResponse", data) end,
    decode = function(data) return pb.decode("game.EnterGameResponse", data) end,
}

-- Fields: role_name(string) -- 角色名
M.CreateRoleRequest = {
    encode = function(data) return pb.encode("game.CreateRoleRequest", data) end,
    decode = function(data) return pb.decode("game.CreateRoleRequest", data) end,
}

-- Fields: code(common.ErrorCode) message(string) role_info(FullRoleInfo) -- 新创建的角色信息 items(ItemInfo[]) -- 初始道具 tasks(TaskInfo[]) -- 初始任务 server_time(uint32) -- 服务器时间 chests(ChestInfo[]) -- 地图宝箱列表
M.CreateRoleResponse = {
    encode = function(data) return pb.encode("game.CreateRoleResponse", data) end,
    decode = function(data) return pb.decode("game.CreateRoleResponse", data) end,
}

-- Fields: chests(ChestInfo[]) -- 当前地图完整宝箱列表
M.ChestUpdateNotify = {
    encode = function(data) return pb.encode("game.ChestUpdateNotify", data) end,
    decode = function(data) return pb.decode("game.ChestUpdateNotify", data) end,
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

-- Fields: command(string) -- 命令名（如 "additem"） args(string) -- 参数（如 "1001:5"）
M.GmCommandRequest = {
    encode = function(data) return pb.encode("game.GmCommandRequest", data) end,
    decode = function(data) return pb.decode("game.GmCommandRequest", data) end,
}

-- Fields: code(common.ErrorCode) message(string) items(ItemInfo[]) -- 如果影响背包，返回更新后的背包
M.GmCommandResponse = {
    encode = function(data) return pb.encode("game.GmCommandResponse", data) end,
    decode = function(data) return pb.decode("game.GmCommandResponse", data) end,
}

-- Fields: attr_key(uint32) -- 属性键（对应 EMonsterAttr） attr_value(int32) -- 属性值
M.MonsterAttr = {
    encode = function(data) return pb.encode("game.MonsterAttr", data) end,
    decode = function(data) return pb.decode("game.MonsterAttr", data) end,
}

-- Fields: instance_id(uint32) -- 实例配置ID（来自 TbMapMonster.id） monster_id(uint32) -- 怪物类型ID（来自 TbMonster.id） x(int32) -- 格子 X y(int32) -- 格子 Y name(string) -- 怪物名称 level(uint32) -- 等级 attrs(MonsterAttr[]) -- 属性列表
M.MonsterInfo = {
    encode = function(data) return pb.encode("game.MonsterInfo", data) end,
    decode = function(data) return pb.decode("game.MonsterInfo", data) end,
}

-- Fields: instance_id(uint32) -- 怪物实例ID from_x(int32) -- 起点 X from_y(int32) -- 起点 Y to_x(int32) -- 终点 X to_y(int32) -- 终点 Y state(string) -- 状态：idle | patrol | chase | return
M.MonsterMoveNotify = {
    encode = function(data) return pb.encode("game.MonsterMoveNotify", data) end,
    decode = function(data) return pb.decode("game.MonsterMoveNotify", data) end,
}

-- Fields: monsters(MonsterMoveNotify[])
M.MonsterStateBatchNotify = {
    encode = function(data) return pb.encode("game.MonsterStateBatchNotify", data) end,
    decode = function(data) return pb.decode("game.MonsterStateBatchNotify", data) end,
}

-- Fields: map_name(string) -- 地图名 chests(ChestInfo[]) -- 地图宝箱列表 monsters(MonsterInfo[]) -- 地图怪物列表
M.MapInfoSyncNotify = {
    encode = function(data) return pb.encode("game.MapInfoSyncNotify", data) end,
    decode = function(data) return pb.decode("game.MapInfoSyncNotify", data) end,
}

-- Fields: chest_id(uint32) -- 宝箱实例ID x(int32) -- 格子 X y(int32) -- 格子 Y opened(bool) -- 是否已打开
M.ChestInfo = {
    encode = function(data) return pb.encode("game.ChestInfo", data) end,
    decode = function(data) return pb.decode("game.ChestInfo", data) end,
}

-- Fields: chest_id(uint32) -- 宝箱实例ID
M.OpenChestRequest = {
    encode = function(data) return pb.encode("game.OpenChestRequest", data) end,
    decode = function(data) return pb.decode("game.OpenChestRequest", data) end,
}

-- Fields: code(common.ErrorCode) message(string) items(ItemInfo[]) -- 获得的物品
M.OpenChestResponse = {
    encode = function(data) return pb.encode("game.OpenChestResponse", data) end,
    decode = function(data) return pb.decode("game.OpenChestResponse", data) end,
}

return M
