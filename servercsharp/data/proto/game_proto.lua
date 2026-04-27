--------------------------------------------------------------------------------
-- 消息编解码（由 build_proto 自动生成，请勿手动修改）
-- Source: game.proto  Package: game
--------------------------------------------------------------------------------
local pb = require "pb"

local M = {}

-- Fields: pos_x(float) pos_y(float) width(float) height(float)
M.UpdateUIPanelPosRequest = {
    encode = function(data) return pb.encode("game.UpdateUIPanelPosRequest", data) end,
    decode = function(data) return pb.decode("game.UpdateUIPanelPosRequest", data) end,
}

-- Fields: code(common.ErrorCode) message(string)
M.UpdateUIPanelPosResponse = {
    encode = function(data) return pb.encode("game.UpdateUIPanelPosResponse", data) end,
    decode = function(data) return pb.decode("game.UpdateUIPanelPosResponse", data) end,
}

-- Fields: log_type(CombatLogType) -- 日志类型 timestamp(uint64) -- 服务器时间戳（秒） actor_name(string) -- 行为发起者名称 target_name(string) -- 目标名称（可选） skill_name(string) -- 技能名称（可选） value(int32) -- 数值（伤害/治疗量，可选） extra(string) -- 额外文本（可选）
M.CombatLogEntry = {
    encode = function(data) return pb.encode("game.CombatLogEntry", data) end,
    decode = function(data) return pb.decode("game.CombatLogEntry", data) end,
}

-- Fields: entries(CombatLogEntry[])
M.CombatLogNotify = {
    encode = function(data) return pb.encode("game.CombatLogNotify", data) end,
    decode = function(data) return pb.decode("game.CombatLogNotify", data) end,
}

-- Fields: skill_id(uint32) remaining_cd(float) -- 剩余CD（秒） total_cd(float) -- 总CD时长（秒，用于遮罩百分比）
M.CombatStateNotify = {
    encode = function(data) return pb.encode("game.CombatStateNotify", data) end,
    decode = function(data) return pb.decode("game.CombatStateNotify", data) end,
}

-- Fields: entity_id(uint64) -- account_id 或 instance_id entity_name(string) -- 显示名称 atb(float) -- 0.0 ~ 100.0 is_player(bool) -- true=玩家, false=怪物 hp(int32) -- 当前血量 max_hp(int32) -- 最大血量 casting_skill(string) -- 正在蓄力的技能名（空=不在蓄力） cast_progress(float) -- 蓄力进度 0.0~1.0 skill_cds(SkillCdEntry[]) -- 当前正在CD中的技能 mp(int32) -- 当前魔法 max_mp(int32) -- 最大魔法
M.CombatUnit = {
    encode = function(data) return pb.encode("game.CombatUnit", data) end,
    decode = function(data) return pb.decode("game.CombatUnit", data) end,
}

-- Fields: role_id(uint64) -- 角色ID role_name(string) -- 角色名 level(uint32) -- 等级 exp(uint64) -- 经验值 avatar_id(uint32) -- 头像ID gold(uint64) -- 金币 diamond(uint64) -- 钻石 total_power(uint64) -- 总战力 vip_level(uint32) -- VIP等级 create_time(uint64) -- 创建时间 last_login_time(uint64) -- 上次登录时间 job(string) -- 职业 title(string) -- 称号 status(string) -- 状态 current_map(string) -- 当前地图 grid_x(int32) -- 格子X grid_y(int32) -- 格子Y ui_panel_pos_x(float) -- 综合面板位置X ui_panel_pos_y(float) -- 综合面板位置Y ui_panel_width(float) -- 综合面板宽度 ui_panel_height(float) -- 综合面板高度 attrs(AttrItem[]) -- 战斗属性集合（key-value，接 luban 枚举） learned_skills(uint32[]) -- 已学会的技能ID列表 equipped_skills(uint32[]) -- 已装备的技能ID列表
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

-- Fields: code(common.ErrorCode) message(string) x(int32) -- 服务器确认的位置 X（失败时为合法位置） y(int32) -- 服务器确认的位置 Y duration_ms(int32) -- 移动总时长（毫秒） check_ratio(int32) -- 检查点比例 0-100 dual_start_ratio(int32) -- 双格开始比例 0-100 dual_end_ratio(int32) -- 双格结束比例 0-100
M.MoveResponse = {
    encode = function(data) return pb.encode("game.MoveResponse", data) end,
    decode = function(data) return pb.decode("game.MoveResponse", data) end,
}

-- Fields: target_x(int32) target_y(int32)
M.MoveConfirmRequest = {
    encode = function(data) return pb.encode("game.MoveConfirmRequest", data) end,
    decode = function(data) return pb.decode("game.MoveConfirmRequest", data) end,
}

-- Fields: target_x(int32) target_y(int32)
M.MoveCompleteRequest = {
    encode = function(data) return pb.encode("game.MoveCompleteRequest", data) end,
    decode = function(data) return pb.decode("game.MoveCompleteRequest", data) end,
}

-- Fields: entity_id(uint64) rollback_x(int32) rollback_y(int32)
M.MoveCancelNotify = {
    encode = function(data) return pb.encode("game.MoveCancelNotify", data) end,
    decode = function(data) return pb.decode("game.MoveCancelNotify", data) end,
}

-- Fields: target_x(int32) -- 碰撞目标格 X target_y(int32) -- 碰撞目标格 Y
M.MoveCollisionNotify = {
    encode = function(data) return pb.encode("game.MoveCollisionNotify", data) end,
    decode = function(data) return pb.decode("game.MoveCollisionNotify", data) end,
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

-- Fields: code(common.ErrorCode) message(string) items(ItemInfo[]) -- 如果影响背包，返回更新后的背包 learned_skills(uint32[]) -- 已学会的技能ID列表 equipped_skills(uint32[]) -- 已装备的技能ID列表
M.GmCommandResponse = {
    encode = function(data) return pb.encode("game.GmCommandResponse", data) end,
    decode = function(data) return pb.decode("game.GmCommandResponse", data) end,
}

-- Fields: key(uint32) value(int32)
M.AttrItem = {
    encode = function(data) return pb.encode("game.AttrItem", data) end,
    decode = function(data) return pb.decode("game.AttrItem", data) end,
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

-- Fields: instance_id(uint32) -- 怪物实例ID from_x(int32) -- 起点 X from_y(int32) -- 起点 Y to_x(int32) -- 终点 X to_y(int32) -- 终点 Y state(string) -- 状态：idle | patrol | chase | return duration_ms(int32) -- 移动耗时（毫秒），客户端据此播放动画
M.MonsterMoveNotify = {
    encode = function(data) return pb.encode("game.MonsterMoveNotify", data) end,
    decode = function(data) return pb.decode("game.MonsterMoveNotify", data) end,
}

-- Fields: monsters(MonsterMoveNotify[])
M.MonsterStateBatchNotify = {
    encode = function(data) return pb.encode("game.MonsterStateBatchNotify", data) end,
    decode = function(data) return pb.decode("game.MonsterStateBatchNotify", data) end,
}

-- Fields: npc_instance_id(uint64) npc_name(string) npc_type(int32) -- NPC类型：1=转职大师 x(int32) y(int32)
M.NpcInfo = {
    encode = function(data) return pb.encode("game.NpcInfo", data) end,
    decode = function(data) return pb.decode("game.NpcInfo", data) end,
}

-- Fields: npc_instance_id(uint64) npc_name(string) npc_type(int32)
M.NpcInteractNotify = {
    encode = function(data) return pb.encode("game.NpcInteractNotify", data) end,
    decode = function(data) return pb.decode("game.NpcInteractNotify", data) end,
}

-- Fields: target_job(string)
M.ChangeJobRequest = {
    encode = function(data) return pb.encode("game.ChangeJobRequest", data) end,
    decode = function(data) return pb.decode("game.ChangeJobRequest", data) end,
}

-- Fields: code(common.ErrorCode) message(string) current_job(string) learned_skills(uint32[]) equipped_skills(uint32[])
M.ChangeJobResponse = {
    encode = function(data) return pb.encode("game.ChangeJobResponse", data) end,
    decode = function(data) return pb.decode("game.ChangeJobResponse", data) end,
}

-- Fields: npc_instance_id(uint64) -- NPC实例ID
M.NpcCombatRequest = {
    encode = function(data) return pb.encode("game.NpcCombatRequest", data) end,
    decode = function(data) return pb.decode("game.NpcCombatRequest", data) end,
}

-- Fields: code(common.ErrorCode) npc_instance_id(uint64) -- NPC实例ID
M.NpcCombatResponse = {
    encode = function(data) return pb.encode("game.NpcCombatResponse", data) end,
    decode = function(data) return pb.decode("game.NpcCombatResponse", data) end,
}

-- Fields: skill_id(uint32) slot_index(uint32) -- 装备到哪个槽位
M.EquipSkillRequest = {
    encode = function(data) return pb.encode("game.EquipSkillRequest", data) end,
    decode = function(data) return pb.decode("game.EquipSkillRequest", data) end,
}

-- Fields: code(common.ErrorCode) message(string) equipped_skills(uint32[])
M.EquipSkillResponse = {
    encode = function(data) return pb.encode("game.EquipSkillResponse", data) end,
    decode = function(data) return pb.decode("game.EquipSkillResponse", data) end,
}

-- Fields: skill_id(uint32) slot_index(uint32) -- 从哪个槽位卸下
M.UnequipSkillRequest = {
    encode = function(data) return pb.encode("game.UnequipSkillRequest", data) end,
    decode = function(data) return pb.decode("game.UnequipSkillRequest", data) end,
}

-- Fields: code(common.ErrorCode) message(string) equipped_skills(uint32[])
M.UnequipSkillResponse = {
    encode = function(data) return pb.encode("game.UnequipSkillResponse", data) end,
    decode = function(data) return pb.decode("game.UnequipSkillResponse", data) end,
}

-- Fields: old_level(int32) new_level(int32) max_hp(int32) max_mp(int32) hp(int32) mp(int32) patk(int32) matk(int32) pdef(int32) mdef(int32) agility(int32)
M.LevelUpNotify = {
    encode = function(data) return pb.encode("game.LevelUpNotify", data) end,
    decode = function(data) return pb.decode("game.LevelUpNotify", data) end,
}

-- Fields: map_name(string) -- 地图名 chests(ChestInfo[]) -- 地图宝箱列表 monsters(MonsterInfo[]) -- 地图怪物列表 npcs(NpcInfo[]) -- 地图NPC列表
M.MapInfoSyncNotify = {
    encode = function(data) return pb.encode("game.MapInfoSyncNotify", data) end,
    decode = function(data) return pb.decode("game.MapInfoSyncNotify", data) end,
}

-- Fields: target_map(string) -- 目标地图名
M.ChangeMapRequest = {
    encode = function(data) return pb.encode("game.ChangeMapRequest", data) end,
    decode = function(data) return pb.decode("game.ChangeMapRequest", data) end,
}

-- Fields: code(common.ErrorCode) message(string) map_name(string) -- 当前地图名 spawn_x(int32) -- 出生点 X spawn_y(int32) -- 出生点 Y
M.ChangeMapResponse = {
    encode = function(data) return pb.encode("game.ChangeMapResponse", data) end,
    decode = function(data) return pb.decode("game.ChangeMapResponse", data) end,
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

-- Fields: spawn_x(int32) -- 重生点 X spawn_y(int32) -- 重生点 Y hp(int32) -- 恢复后的 HP max_hp(int32) -- 最大 HP mp(int32) -- 恢复后的 MP max_mp(int32) -- 最大 MP
M.PlayerDeathNotify = {
    encode = function(data) return pb.encode("game.PlayerDeathNotify", data) end,
    decode = function(data) return pb.decode("game.PlayerDeathNotify", data) end,
}

-- Fields: entity_ids(uint64[])
M.CombatStartNotify = {
    encode = function(data) return pb.encode("game.CombatStartNotify", data) end,
    decode = function(data) return pb.decode("game.CombatStartNotify", data) end,
}

-- Fields: reason(CombatEndReason) entity_ids(uint64[])
M.CombatEndNotify = {
    encode = function(data) return pb.encode("game.CombatEndNotify", data) end,
    decode = function(data) return pb.decode("game.CombatEndNotify", data) end,
}

-- Fields: instance_id(uint32) rollback_x(int32) rollback_y(int32)
M.MonsterMoveCancelNotify = {
    encode = function(data) return pb.encode("game.MonsterMoveCancelNotify", data) end,
    decode = function(data) return pb.decode("game.MonsterMoveCancelNotify", data) end,
}

return M
