/**
 * Protocol Buffers TypeScript 定义
 * 由 ts-proto 自动生成
 * 源文件: protocols/proto/*.proto
 * 生成命令: npm run build:proto
 */

export { ServerStatus, ErrorCode } from './common';
export type { Packet, Response } from './common';
export { CombatLogType, CombatEndReason } from './game';
export type { UpdateUIPanelPosRequest, UpdateUIPanelPosResponse, CombatLogEntry, CombatLogNotify, CombatStateNotify, CombatStateNotify_SkillCdEntry, CombatStateNotify_CombatUnit, FullRoleInfo, ItemInfo, TaskInfo, EnterGameRequest, EnterGameResponse, CreateRoleRequest, CreateRoleResponse, ChestUpdateNotify, MoveRequest, MoveResponse, MoveConfirmRequest, MoveCompleteRequest, MoveCancelNotify, MoveCollisionNotify, UseItemRequest, UseItemResponse, DropItemRequest, DropItemResponse, GmCommandRequest, GmCommandResponse, AttrItem, MonsterAttr, MonsterInfo, MonsterMoveNotify, MonsterStateBatchNotify, NpcInfo, NpcInteractNotify, ChangeJobRequest, ChangeJobResponse, NpcCombatRequest, NpcCombatResponse, EquipSkillRequest, EquipSkillResponse, UnequipSkillRequest, UnequipSkillResponse, LevelUpNotify, MapInfoSyncNotify, ChangeMapRequest, ChangeMapResponse, ChestInfo, OpenChestRequest, OpenChestResponse, PlayerDeathNotify, CombatStartNotify, CombatEndNotify, MonsterMoveCancelNotify } from './game';
export { MessageType } from './gateway';
export type { HeartbeatRequest, HeartbeatResponse, ClientInfo, ConnectRequest, ConnectResponse, DisconnectNotify } from './gateway';
export type { AccountLoginRequest, AccountLoginResponse, SelectServerRequest, RoleBrief, SelectServerResponse } from './login';
export { MessageId } from './message_id';
export type { ServerInfo, GetServerListRequest, GetServerListResponse, ServerStatusUpdate } from './server';

import { ServerStatus, ErrorCode } from './common';
import type { Packet, Response } from './common';
import { CombatLogType, CombatEndReason } from './game';
import type { UpdateUIPanelPosRequest, UpdateUIPanelPosResponse, CombatLogEntry, CombatLogNotify, CombatStateNotify, CombatStateNotify_SkillCdEntry, CombatStateNotify_CombatUnit, FullRoleInfo, ItemInfo, TaskInfo, EnterGameRequest, EnterGameResponse, CreateRoleRequest, CreateRoleResponse, ChestUpdateNotify, MoveRequest, MoveResponse, MoveConfirmRequest, MoveCompleteRequest, MoveCancelNotify, MoveCollisionNotify, UseItemRequest, UseItemResponse, DropItemRequest, DropItemResponse, GmCommandRequest, GmCommandResponse, AttrItem, MonsterAttr, MonsterInfo, MonsterMoveNotify, MonsterStateBatchNotify, NpcInfo, NpcInteractNotify, ChangeJobRequest, ChangeJobResponse, NpcCombatRequest, NpcCombatResponse, EquipSkillRequest, EquipSkillResponse, UnequipSkillRequest, UnequipSkillResponse, LevelUpNotify, MapInfoSyncNotify, ChangeMapRequest, ChangeMapResponse, ChestInfo, OpenChestRequest, OpenChestResponse, PlayerDeathNotify, CombatStartNotify, CombatEndNotify, MonsterMoveCancelNotify } from './game';
import { MessageType } from './gateway';
import type { HeartbeatRequest, HeartbeatResponse, ClientInfo, ConnectRequest, ConnectResponse, DisconnectNotify } from './gateway';
import type { AccountLoginRequest, AccountLoginResponse, SelectServerRequest, RoleBrief, SelectServerResponse } from './login';
import { MessageId } from './message_id';
import type { ServerInfo, GetServerListRequest, GetServerListResponse, ServerStatusUpdate } from './server';

// 通用 create 辅助函数
function createMessage<T>(defaults: Partial<T>, init?: Partial<T>): T {
  return { ...defaults, ...init } as T;
}

// 创建 proto 对象（自动生成）
export const proto = {
  common: {
    ServerStatus,
    ErrorCode,
    Packet: {
      create: (init?: Partial<Packet>): Packet =>
        createMessage({ msg_id: 0, session: 0, data: new Uint8Array(0), timestamp: 0 }, init),
      decode: (data: string): Packet =>
        pb_decode("common.Packet", data) as Packet,
      encode: (msg: Packet): string =>
        pb_encode("common.Packet", msg),
    },
    Response: {
      create: (init?: Partial<Response>): Response =>
        createMessage({ code: ErrorCode.SUCCESS, message: '', data: new Uint8Array(0) }, init),
      decode: (data: string): Response =>
        pb_decode("common.Response", data) as Response,
      encode: (msg: Response): string =>
        pb_encode("common.Response", msg),
    },
  },
  game: {
    CombatLogType,
    CombatEndReason,
    UpdateUIPanelPosRequest: {
      create: (init?: Partial<UpdateUIPanelPosRequest>): UpdateUIPanelPosRequest =>
        createMessage({ pos_x: 0, pos_y: 0, width: 0, height: 0 }, init),
      decode: (data: string): UpdateUIPanelPosRequest =>
        pb_decode("game.UpdateUIPanelPosRequest", data) as UpdateUIPanelPosRequest,
      encode: (msg: UpdateUIPanelPosRequest): string =>
        pb_encode("game.UpdateUIPanelPosRequest", msg),
    },
    UpdateUIPanelPosResponse: {
      create: (init?: Partial<UpdateUIPanelPosResponse>): UpdateUIPanelPosResponse =>
        createMessage({ code: ErrorCode.SUCCESS, message: '' }, init),
      decode: (data: string): UpdateUIPanelPosResponse =>
        pb_decode("game.UpdateUIPanelPosResponse", data) as UpdateUIPanelPosResponse,
      encode: (msg: UpdateUIPanelPosResponse): string =>
        pb_encode("game.UpdateUIPanelPosResponse", msg),
    },
    CombatLogEntry: {
      create: (init?: Partial<CombatLogEntry>): CombatLogEntry =>
        createMessage({ log_type: undefined, timestamp: 0, actor_name: '', target_name: '', skill_name: '', value: 0, extra: '' }, init),
      decode: (data: string): CombatLogEntry =>
        pb_decode("game.CombatLogEntry", data) as CombatLogEntry,
      encode: (msg: CombatLogEntry): string =>
        pb_encode("game.CombatLogEntry", msg),
    },
    CombatLogNotify: {
      create: (init?: Partial<CombatLogNotify>): CombatLogNotify =>
        createMessage({ entries: [] }, init),
      decode: (data: string): CombatLogNotify =>
        pb_decode("game.CombatLogNotify", data) as CombatLogNotify,
      encode: (msg: CombatLogNotify): string =>
        pb_encode("game.CombatLogNotify", msg),
    },
    CombatStateNotify: {
      create: (init?: Partial<CombatStateNotify>): CombatStateNotify =>
        createMessage({ units: [] }, init),
      decode: (data: string): CombatStateNotify =>
        pb_decode("game.CombatStateNotify", data) as CombatStateNotify,
      encode: (msg: CombatStateNotify): string =>
        pb_encode("game.CombatStateNotify", msg),
    },
    CombatStateNotify_SkillCdEntry: {
      create: (init?: Partial<CombatStateNotify_SkillCdEntry>): CombatStateNotify_SkillCdEntry =>
        createMessage({ skill_id: 0, remaining_cd: 0, total_cd: 0 }, init),
      decode: (data: string): CombatStateNotify_SkillCdEntry =>
        pb_decode("game.CombatStateNotify_SkillCdEntry", data) as CombatStateNotify_SkillCdEntry,
      encode: (msg: CombatStateNotify_SkillCdEntry): string =>
        pb_encode("game.CombatStateNotify_SkillCdEntry", msg),
    },
    CombatStateNotify_CombatUnit: {
      create: (init?: Partial<CombatStateNotify_CombatUnit>): CombatStateNotify_CombatUnit =>
        createMessage({ entity_id: 0, entity_name: '', atb: 0, is_player: false, hp: 0, max_hp: 0, casting_skill: '', cast_progress: 0, skill_cds: [], mp: 0, max_mp: 0 }, init),
      decode: (data: string): CombatStateNotify_CombatUnit =>
        pb_decode("game.CombatStateNotify_CombatUnit", data) as CombatStateNotify_CombatUnit,
      encode: (msg: CombatStateNotify_CombatUnit): string =>
        pb_encode("game.CombatStateNotify_CombatUnit", msg),
    },
    FullRoleInfo: {
      create: (init?: Partial<FullRoleInfo>): FullRoleInfo =>
        createMessage({ role_id: 0, role_name: '', level: 0, exp: 0, avatar_id: 0, gold: 0, diamond: 0, total_power: 0, vip_level: 0, create_time: 0, last_login_time: 0, job: '', title: '', status: '', current_map: '', grid_x: 0, grid_y: 0, ui_panel_pos_x: 0, ui_panel_pos_y: 0, ui_panel_width: 0, ui_panel_height: 0, attrs: [], learned_skills: 0, equipped_skills: 0 }, init),
      decode: (data: string): FullRoleInfo =>
        pb_decode("game.FullRoleInfo", data) as FullRoleInfo,
      encode: (msg: FullRoleInfo): string =>
        pb_encode("game.FullRoleInfo", msg),
    },
    ItemInfo: {
      create: (init?: Partial<ItemInfo>): ItemInfo =>
        createMessage({ item_id: 0, count: 0 }, init),
      decode: (data: string): ItemInfo =>
        pb_decode("game.ItemInfo", data) as ItemInfo,
      encode: (msg: ItemInfo): string =>
        pb_encode("game.ItemInfo", msg),
    },
    TaskInfo: {
      create: (init?: Partial<TaskInfo>): TaskInfo =>
        createMessage({ task_id: 0, status: 0, progress: 0 }, init),
      decode: (data: string): TaskInfo =>
        pb_decode("game.TaskInfo", data) as TaskInfo,
      encode: (msg: TaskInfo): string =>
        pb_encode("game.TaskInfo", msg),
    },
    EnterGameRequest: {
      create: (init?: Partial<EnterGameRequest>): EnterGameRequest =>
        createMessage({ role_id: 0 }, init),
      decode: (data: string): EnterGameRequest =>
        pb_decode("game.EnterGameRequest", data) as EnterGameRequest,
      encode: (msg: EnterGameRequest): string =>
        pb_encode("game.EnterGameRequest", msg),
    },
    EnterGameResponse: {
      create: (init?: Partial<EnterGameResponse>): EnterGameResponse =>
        createMessage({ code: ErrorCode.SUCCESS, message: '', items: [], tasks: [], server_time: 0, chests: [] }, init),
      decode: (data: string): EnterGameResponse =>
        pb_decode("game.EnterGameResponse", data) as EnterGameResponse,
      encode: (msg: EnterGameResponse): string =>
        pb_encode("game.EnterGameResponse", msg),
    },
    CreateRoleRequest: {
      create: (init?: Partial<CreateRoleRequest>): CreateRoleRequest =>
        createMessage({ role_name: '' }, init),
      decode: (data: string): CreateRoleRequest =>
        pb_decode("game.CreateRoleRequest", data) as CreateRoleRequest,
      encode: (msg: CreateRoleRequest): string =>
        pb_encode("game.CreateRoleRequest", msg),
    },
    CreateRoleResponse: {
      create: (init?: Partial<CreateRoleResponse>): CreateRoleResponse =>
        createMessage({ code: ErrorCode.SUCCESS, message: '', items: [], tasks: [], server_time: 0, chests: [] }, init),
      decode: (data: string): CreateRoleResponse =>
        pb_decode("game.CreateRoleResponse", data) as CreateRoleResponse,
      encode: (msg: CreateRoleResponse): string =>
        pb_encode("game.CreateRoleResponse", msg),
    },
    ChestUpdateNotify: {
      create: (init?: Partial<ChestUpdateNotify>): ChestUpdateNotify =>
        createMessage({ chests: [] }, init),
      decode: (data: string): ChestUpdateNotify =>
        pb_decode("game.ChestUpdateNotify", data) as ChestUpdateNotify,
      encode: (msg: ChestUpdateNotify): string =>
        pb_encode("game.ChestUpdateNotify", msg),
    },
    MoveRequest: {
      create: (init?: Partial<MoveRequest>): MoveRequest =>
        createMessage({ from_x: 0, from_y: 0, to_x: 0, to_y: 0, map_name: '' }, init),
      decode: (data: string): MoveRequest =>
        pb_decode("game.MoveRequest", data) as MoveRequest,
      encode: (msg: MoveRequest): string =>
        pb_encode("game.MoveRequest", msg),
    },
    MoveResponse: {
      create: (init?: Partial<MoveResponse>): MoveResponse =>
        createMessage({ code: ErrorCode.SUCCESS, message: '', x: 0, y: 0, duration_ms: 0, check_ratio: 0, dual_start_ratio: 0, dual_end_ratio: 0 }, init),
      decode: (data: string): MoveResponse =>
        pb_decode("game.MoveResponse", data) as MoveResponse,
      encode: (msg: MoveResponse): string =>
        pb_encode("game.MoveResponse", msg),
    },
    MoveConfirmRequest: {
      create: (init?: Partial<MoveConfirmRequest>): MoveConfirmRequest =>
        createMessage({ target_x: 0, target_y: 0 }, init),
      decode: (data: string): MoveConfirmRequest =>
        pb_decode("game.MoveConfirmRequest", data) as MoveConfirmRequest,
      encode: (msg: MoveConfirmRequest): string =>
        pb_encode("game.MoveConfirmRequest", msg),
    },
    MoveCompleteRequest: {
      create: (init?: Partial<MoveCompleteRequest>): MoveCompleteRequest =>
        createMessage({ target_x: 0, target_y: 0 }, init),
      decode: (data: string): MoveCompleteRequest =>
        pb_decode("game.MoveCompleteRequest", data) as MoveCompleteRequest,
      encode: (msg: MoveCompleteRequest): string =>
        pb_encode("game.MoveCompleteRequest", msg),
    },
    MoveCancelNotify: {
      create: (init?: Partial<MoveCancelNotify>): MoveCancelNotify =>
        createMessage({ entity_id: 0, rollback_x: 0, rollback_y: 0 }, init),
      decode: (data: string): MoveCancelNotify =>
        pb_decode("game.MoveCancelNotify", data) as MoveCancelNotify,
      encode: (msg: MoveCancelNotify): string =>
        pb_encode("game.MoveCancelNotify", msg),
    },
    MoveCollisionNotify: {
      create: (init?: Partial<MoveCollisionNotify>): MoveCollisionNotify =>
        createMessage({ target_x: 0, target_y: 0 }, init),
      decode: (data: string): MoveCollisionNotify =>
        pb_decode("game.MoveCollisionNotify", data) as MoveCollisionNotify,
      encode: (msg: MoveCollisionNotify): string =>
        pb_encode("game.MoveCollisionNotify", msg),
    },
    UseItemRequest: {
      create: (init?: Partial<UseItemRequest>): UseItemRequest =>
        createMessage({ item_id: 0, count: 0 }, init),
      decode: (data: string): UseItemRequest =>
        pb_decode("game.UseItemRequest", data) as UseItemRequest,
      encode: (msg: UseItemRequest): string =>
        pb_encode("game.UseItemRequest", msg),
    },
    UseItemResponse: {
      create: (init?: Partial<UseItemResponse>): UseItemResponse =>
        createMessage({ code: ErrorCode.SUCCESS, message: '', items: [] }, init),
      decode: (data: string): UseItemResponse =>
        pb_decode("game.UseItemResponse", data) as UseItemResponse,
      encode: (msg: UseItemResponse): string =>
        pb_encode("game.UseItemResponse", msg),
    },
    DropItemRequest: {
      create: (init?: Partial<DropItemRequest>): DropItemRequest =>
        createMessage({ item_id: 0, count: 0 }, init),
      decode: (data: string): DropItemRequest =>
        pb_decode("game.DropItemRequest", data) as DropItemRequest,
      encode: (msg: DropItemRequest): string =>
        pb_encode("game.DropItemRequest", msg),
    },
    DropItemResponse: {
      create: (init?: Partial<DropItemResponse>): DropItemResponse =>
        createMessage({ code: ErrorCode.SUCCESS, message: '', items: [] }, init),
      decode: (data: string): DropItemResponse =>
        pb_decode("game.DropItemResponse", data) as DropItemResponse,
      encode: (msg: DropItemResponse): string =>
        pb_encode("game.DropItemResponse", msg),
    },
    GmCommandRequest: {
      create: (init?: Partial<GmCommandRequest>): GmCommandRequest =>
        createMessage({ command: '', args: '' }, init),
      decode: (data: string): GmCommandRequest =>
        pb_decode("game.GmCommandRequest", data) as GmCommandRequest,
      encode: (msg: GmCommandRequest): string =>
        pb_encode("game.GmCommandRequest", msg),
    },
    GmCommandResponse: {
      create: (init?: Partial<GmCommandResponse>): GmCommandResponse =>
        createMessage({ code: ErrorCode.SUCCESS, message: '', items: [], learned_skills: 0, equipped_skills: 0 }, init),
      decode: (data: string): GmCommandResponse =>
        pb_decode("game.GmCommandResponse", data) as GmCommandResponse,
      encode: (msg: GmCommandResponse): string =>
        pb_encode("game.GmCommandResponse", msg),
    },
    AttrItem: {
      create: (init?: Partial<AttrItem>): AttrItem =>
        createMessage({ key: 0, value: 0 }, init),
      decode: (data: string): AttrItem =>
        pb_decode("game.AttrItem", data) as AttrItem,
      encode: (msg: AttrItem): string =>
        pb_encode("game.AttrItem", msg),
    },
    MonsterAttr: {
      create: (init?: Partial<MonsterAttr>): MonsterAttr =>
        createMessage({ attr_key: 0, attr_value: 0 }, init),
      decode: (data: string): MonsterAttr =>
        pb_decode("game.MonsterAttr", data) as MonsterAttr,
      encode: (msg: MonsterAttr): string =>
        pb_encode("game.MonsterAttr", msg),
    },
    MonsterInfo: {
      create: (init?: Partial<MonsterInfo>): MonsterInfo =>
        createMessage({ instance_id: 0, monster_id: 0, x: 0, y: 0, name: '', level: 0, attrs: [] }, init),
      decode: (data: string): MonsterInfo =>
        pb_decode("game.MonsterInfo", data) as MonsterInfo,
      encode: (msg: MonsterInfo): string =>
        pb_encode("game.MonsterInfo", msg),
    },
    MonsterMoveNotify: {
      create: (init?: Partial<MonsterMoveNotify>): MonsterMoveNotify =>
        createMessage({ instance_id: 0, from_x: 0, from_y: 0, to_x: 0, to_y: 0, state: '', duration_ms: 0 }, init),
      decode: (data: string): MonsterMoveNotify =>
        pb_decode("game.MonsterMoveNotify", data) as MonsterMoveNotify,
      encode: (msg: MonsterMoveNotify): string =>
        pb_encode("game.MonsterMoveNotify", msg),
    },
    MonsterStateBatchNotify: {
      create: (init?: Partial<MonsterStateBatchNotify>): MonsterStateBatchNotify =>
        createMessage({ monsters: [] }, init),
      decode: (data: string): MonsterStateBatchNotify =>
        pb_decode("game.MonsterStateBatchNotify", data) as MonsterStateBatchNotify,
      encode: (msg: MonsterStateBatchNotify): string =>
        pb_encode("game.MonsterStateBatchNotify", msg),
    },
    NpcInfo: {
      create: (init?: Partial<NpcInfo>): NpcInfo =>
        createMessage({ npc_instance_id: 0, npc_name: '', npc_type: 0, x: 0, y: 0 }, init),
      decode: (data: string): NpcInfo =>
        pb_decode("game.NpcInfo", data) as NpcInfo,
      encode: (msg: NpcInfo): string =>
        pb_encode("game.NpcInfo", msg),
    },
    NpcInteractNotify: {
      create: (init?: Partial<NpcInteractNotify>): NpcInteractNotify =>
        createMessage({ npc_instance_id: 0, npc_name: '', npc_type: 0 }, init),
      decode: (data: string): NpcInteractNotify =>
        pb_decode("game.NpcInteractNotify", data) as NpcInteractNotify,
      encode: (msg: NpcInteractNotify): string =>
        pb_encode("game.NpcInteractNotify", msg),
    },
    ChangeJobRequest: {
      create: (init?: Partial<ChangeJobRequest>): ChangeJobRequest =>
        createMessage({ target_job: '' }, init),
      decode: (data: string): ChangeJobRequest =>
        pb_decode("game.ChangeJobRequest", data) as ChangeJobRequest,
      encode: (msg: ChangeJobRequest): string =>
        pb_encode("game.ChangeJobRequest", msg),
    },
    ChangeJobResponse: {
      create: (init?: Partial<ChangeJobResponse>): ChangeJobResponse =>
        createMessage({ code: ErrorCode.SUCCESS, message: '', current_job: '', learned_skills: 0, equipped_skills: 0 }, init),
      decode: (data: string): ChangeJobResponse =>
        pb_decode("game.ChangeJobResponse", data) as ChangeJobResponse,
      encode: (msg: ChangeJobResponse): string =>
        pb_encode("game.ChangeJobResponse", msg),
    },
    NpcCombatRequest: {
      create: (init?: Partial<NpcCombatRequest>): NpcCombatRequest =>
        createMessage({ npc_instance_id: 0 }, init),
      decode: (data: string): NpcCombatRequest =>
        pb_decode("game.NpcCombatRequest", data) as NpcCombatRequest,
      encode: (msg: NpcCombatRequest): string =>
        pb_encode("game.NpcCombatRequest", msg),
    },
    NpcCombatResponse: {
      create: (init?: Partial<NpcCombatResponse>): NpcCombatResponse =>
        createMessage({ code: ErrorCode.SUCCESS, npc_instance_id: 0 }, init),
      decode: (data: string): NpcCombatResponse =>
        pb_decode("game.NpcCombatResponse", data) as NpcCombatResponse,
      encode: (msg: NpcCombatResponse): string =>
        pb_encode("game.NpcCombatResponse", msg),
    },
    EquipSkillRequest: {
      create: (init?: Partial<EquipSkillRequest>): EquipSkillRequest =>
        createMessage({ skill_id: 0, slot_index: 0 }, init),
      decode: (data: string): EquipSkillRequest =>
        pb_decode("game.EquipSkillRequest", data) as EquipSkillRequest,
      encode: (msg: EquipSkillRequest): string =>
        pb_encode("game.EquipSkillRequest", msg),
    },
    EquipSkillResponse: {
      create: (init?: Partial<EquipSkillResponse>): EquipSkillResponse =>
        createMessage({ code: ErrorCode.SUCCESS, message: '', equipped_skills: 0 }, init),
      decode: (data: string): EquipSkillResponse =>
        pb_decode("game.EquipSkillResponse", data) as EquipSkillResponse,
      encode: (msg: EquipSkillResponse): string =>
        pb_encode("game.EquipSkillResponse", msg),
    },
    UnequipSkillRequest: {
      create: (init?: Partial<UnequipSkillRequest>): UnequipSkillRequest =>
        createMessage({ skill_id: 0, slot_index: 0 }, init),
      decode: (data: string): UnequipSkillRequest =>
        pb_decode("game.UnequipSkillRequest", data) as UnequipSkillRequest,
      encode: (msg: UnequipSkillRequest): string =>
        pb_encode("game.UnequipSkillRequest", msg),
    },
    UnequipSkillResponse: {
      create: (init?: Partial<UnequipSkillResponse>): UnequipSkillResponse =>
        createMessage({ code: ErrorCode.SUCCESS, message: '', equipped_skills: 0 }, init),
      decode: (data: string): UnequipSkillResponse =>
        pb_decode("game.UnequipSkillResponse", data) as UnequipSkillResponse,
      encode: (msg: UnequipSkillResponse): string =>
        pb_encode("game.UnequipSkillResponse", msg),
    },
    LevelUpNotify: {
      create: (init?: Partial<LevelUpNotify>): LevelUpNotify =>
        createMessage({ old_level: 0, new_level: 0, max_hp: 0, max_mp: 0, hp: 0, mp: 0, patk: 0, matk: 0, pdef: 0, mdef: 0, agility: 0 }, init),
      decode: (data: string): LevelUpNotify =>
        pb_decode("game.LevelUpNotify", data) as LevelUpNotify,
      encode: (msg: LevelUpNotify): string =>
        pb_encode("game.LevelUpNotify", msg),
    },
    MapInfoSyncNotify: {
      create: (init?: Partial<MapInfoSyncNotify>): MapInfoSyncNotify =>
        createMessage({ map_name: '', chests: [], monsters: [], npcs: [] }, init),
      decode: (data: string): MapInfoSyncNotify =>
        pb_decode("game.MapInfoSyncNotify", data) as MapInfoSyncNotify,
      encode: (msg: MapInfoSyncNotify): string =>
        pb_encode("game.MapInfoSyncNotify", msg),
    },
    ChangeMapRequest: {
      create: (init?: Partial<ChangeMapRequest>): ChangeMapRequest =>
        createMessage({ target_map: '' }, init),
      decode: (data: string): ChangeMapRequest =>
        pb_decode("game.ChangeMapRequest", data) as ChangeMapRequest,
      encode: (msg: ChangeMapRequest): string =>
        pb_encode("game.ChangeMapRequest", msg),
    },
    ChangeMapResponse: {
      create: (init?: Partial<ChangeMapResponse>): ChangeMapResponse =>
        createMessage({ code: ErrorCode.SUCCESS, message: '', map_name: '', spawn_x: 0, spawn_y: 0 }, init),
      decode: (data: string): ChangeMapResponse =>
        pb_decode("game.ChangeMapResponse", data) as ChangeMapResponse,
      encode: (msg: ChangeMapResponse): string =>
        pb_encode("game.ChangeMapResponse", msg),
    },
    ChestInfo: {
      create: (init?: Partial<ChestInfo>): ChestInfo =>
        createMessage({ chest_id: 0, x: 0, y: 0, opened: false }, init),
      decode: (data: string): ChestInfo =>
        pb_decode("game.ChestInfo", data) as ChestInfo,
      encode: (msg: ChestInfo): string =>
        pb_encode("game.ChestInfo", msg),
    },
    OpenChestRequest: {
      create: (init?: Partial<OpenChestRequest>): OpenChestRequest =>
        createMessage({ chest_id: 0 }, init),
      decode: (data: string): OpenChestRequest =>
        pb_decode("game.OpenChestRequest", data) as OpenChestRequest,
      encode: (msg: OpenChestRequest): string =>
        pb_encode("game.OpenChestRequest", msg),
    },
    OpenChestResponse: {
      create: (init?: Partial<OpenChestResponse>): OpenChestResponse =>
        createMessage({ code: ErrorCode.SUCCESS, message: '', items: [] }, init),
      decode: (data: string): OpenChestResponse =>
        pb_decode("game.OpenChestResponse", data) as OpenChestResponse,
      encode: (msg: OpenChestResponse): string =>
        pb_encode("game.OpenChestResponse", msg),
    },
    PlayerDeathNotify: {
      create: (init?: Partial<PlayerDeathNotify>): PlayerDeathNotify =>
        createMessage({ spawn_x: 0, spawn_y: 0, hp: 0, max_hp: 0, mp: 0, max_mp: 0 }, init),
      decode: (data: string): PlayerDeathNotify =>
        pb_decode("game.PlayerDeathNotify", data) as PlayerDeathNotify,
      encode: (msg: PlayerDeathNotify): string =>
        pb_encode("game.PlayerDeathNotify", msg),
    },
    CombatStartNotify: {
      create: (init?: Partial<CombatStartNotify>): CombatStartNotify =>
        createMessage({ entity_ids: 0 }, init),
      decode: (data: string): CombatStartNotify =>
        pb_decode("game.CombatStartNotify", data) as CombatStartNotify,
      encode: (msg: CombatStartNotify): string =>
        pb_encode("game.CombatStartNotify", msg),
    },
    CombatEndNotify: {
      create: (init?: Partial<CombatEndNotify>): CombatEndNotify =>
        createMessage({ reason: undefined, entity_ids: 0 }, init),
      decode: (data: string): CombatEndNotify =>
        pb_decode("game.CombatEndNotify", data) as CombatEndNotify,
      encode: (msg: CombatEndNotify): string =>
        pb_encode("game.CombatEndNotify", msg),
    },
    MonsterMoveCancelNotify: {
      create: (init?: Partial<MonsterMoveCancelNotify>): MonsterMoveCancelNotify =>
        createMessage({ instance_id: 0, rollback_x: 0, rollback_y: 0 }, init),
      decode: (data: string): MonsterMoveCancelNotify =>
        pb_decode("game.MonsterMoveCancelNotify", data) as MonsterMoveCancelNotify,
      encode: (msg: MonsterMoveCancelNotify): string =>
        pb_encode("game.MonsterMoveCancelNotify", msg),
    },
  },
  gateway: {
    MessageType,
    HeartbeatRequest: {
      create: (init?: Partial<HeartbeatRequest>): HeartbeatRequest =>
        createMessage({ client_time: 0 }, init),
      decode: (data: string): HeartbeatRequest =>
        pb_decode("gateway.HeartbeatRequest", data) as HeartbeatRequest,
      encode: (msg: HeartbeatRequest): string =>
        pb_encode("gateway.HeartbeatRequest", msg),
    },
    HeartbeatResponse: {
      create: (init?: Partial<HeartbeatResponse>): HeartbeatResponse =>
        createMessage({ server_time: 0, online_count: 0 }, init),
      decode: (data: string): HeartbeatResponse =>
        pb_decode("gateway.HeartbeatResponse", data) as HeartbeatResponse,
      encode: (msg: HeartbeatResponse): string =>
        pb_encode("gateway.HeartbeatResponse", msg),
    },
    ClientInfo: {
      create: (init?: Partial<ClientInfo>): ClientInfo =>
        createMessage({ ip: '', port: 0, version: '', platform: '', device_id: '' }, init),
      decode: (data: string): ClientInfo =>
        pb_decode("gateway.ClientInfo", data) as ClientInfo,
      encode: (msg: ClientInfo): string =>
        pb_encode("gateway.ClientInfo", msg),
    },
    ConnectRequest: {
      create: (init?: Partial<ConnectRequest>): ConnectRequest =>
        createMessage({ token: '' }, init),
      decode: (data: string): ConnectRequest =>
        pb_decode("gateway.ConnectRequest", data) as ConnectRequest,
      encode: (msg: ConnectRequest): string =>
        pb_encode("gateway.ConnectRequest", msg),
    },
    ConnectResponse: {
      create: (init?: Partial<ConnectResponse>): ConnectResponse =>
        createMessage({ code: ErrorCode.SUCCESS, message: '', conn_id: 0, server_time: 0 }, init),
      decode: (data: string): ConnectResponse =>
        pb_decode("gateway.ConnectResponse", data) as ConnectResponse,
      encode: (msg: ConnectResponse): string =>
        pb_encode("gateway.ConnectResponse", msg),
    },
    DisconnectNotify: {
      create: (init?: Partial<DisconnectNotify>): DisconnectNotify =>
        createMessage({ conn_id: 0, reason: '' }, init),
      decode: (data: string): DisconnectNotify =>
        pb_decode("gateway.DisconnectNotify", data) as DisconnectNotify,
      encode: (msg: DisconnectNotify): string =>
        pb_encode("gateway.DisconnectNotify", msg),
    },
  },
  login: {
    AccountLoginRequest: {
      create: (init?: Partial<AccountLoginRequest>): AccountLoginRequest =>
        createMessage({ username: '', password: '', platform: '', device_id: '', client_version: '' }, init),
      decode: (data: string): AccountLoginRequest =>
        pb_decode("login.AccountLoginRequest", data) as AccountLoginRequest,
      encode: (msg: AccountLoginRequest): string =>
        pb_encode("login.AccountLoginRequest", msg),
    },
    AccountLoginResponse: {
      create: (init?: Partial<AccountLoginResponse>): AccountLoginResponse =>
        createMessage({ code: ErrorCode.SUCCESS, message: '', account_token: '', account_id: 0, servers: [], last_server_id: 0, last_role_name: '' }, init),
      decode: (data: string): AccountLoginResponse =>
        pb_decode("login.AccountLoginResponse", data) as AccountLoginResponse,
      encode: (msg: AccountLoginResponse): string =>
        pb_encode("login.AccountLoginResponse", msg),
    },
    SelectServerRequest: {
      create: (init?: Partial<SelectServerRequest>): SelectServerRequest =>
        createMessage({ account_token: '', server_id: 0 }, init),
      decode: (data: string): SelectServerRequest =>
        pb_decode("login.SelectServerRequest", data) as SelectServerRequest,
      encode: (msg: SelectServerRequest): string =>
        pb_encode("login.SelectServerRequest", msg),
    },
    RoleBrief: {
      create: (init?: Partial<RoleBrief>): RoleBrief =>
        createMessage({ role_id: 0, role_name: '', level: 0, avatar_id: 0, last_login: 0, total_power: 0 }, init),
      decode: (data: string): RoleBrief =>
        pb_decode("login.RoleBrief", data) as RoleBrief,
      encode: (msg: RoleBrief): string =>
        pb_encode("login.RoleBrief", msg),
    },
    SelectServerResponse: {
      create: (init?: Partial<SelectServerResponse>): SelectServerResponse =>
        createMessage({ code: ErrorCode.SUCCESS, message: '', gateway_token: '', roles: [], max_role_count: 0, server_time: 0 }, init),
      decode: (data: string): SelectServerResponse =>
        pb_decode("login.SelectServerResponse", data) as SelectServerResponse,
      encode: (msg: SelectServerResponse): string =>
        pb_encode("login.SelectServerResponse", msg),
    },
  },
  message_id: {
    MessageId,
  },
  server: {
    ServerInfo: {
      create: (init?: Partial<ServerInfo>): ServerInfo =>
        createMessage({ server_id: 0, server_name: '', host: '', port: 0, status: undefined, online_count: 0, is_new: false, is_recommend: false, has_role: false, role_count: 0 }, init),
      decode: (data: string): ServerInfo =>
        pb_decode("server.ServerInfo", data) as ServerInfo,
      encode: (msg: ServerInfo): string =>
        pb_encode("server.ServerInfo", msg),
    },
    GetServerListRequest: {
      create: (init?: Partial<GetServerListRequest>): GetServerListRequest =>
        createMessage({ account_token: '' }, init),
      decode: (data: string): GetServerListRequest =>
        pb_decode("server.GetServerListRequest", data) as GetServerListRequest,
      encode: (msg: GetServerListRequest): string =>
        pb_encode("server.GetServerListRequest", msg),
    },
    GetServerListResponse: {
      create: (init?: Partial<GetServerListResponse>): GetServerListResponse =>
        createMessage({ code: ErrorCode.SUCCESS, message: '', servers: [], last_server_id: 0 }, init),
      decode: (data: string): GetServerListResponse =>
        pb_decode("server.GetServerListResponse", data) as GetServerListResponse,
      encode: (msg: GetServerListResponse): string =>
        pb_encode("server.GetServerListResponse", msg),
    },
    ServerStatusUpdate: {
      create: (init?: Partial<ServerStatusUpdate>): ServerStatusUpdate =>
        createMessage({ server_id: 0, status: undefined, online_count: 0 }, init),
      decode: (data: string): ServerStatusUpdate =>
        pb_decode("server.ServerStatusUpdate", data) as ServerStatusUpdate,
      encode: (msg: ServerStatusUpdate): string =>
        pb_encode("server.ServerStatusUpdate", msg),
    },
  },
};

export default proto;
