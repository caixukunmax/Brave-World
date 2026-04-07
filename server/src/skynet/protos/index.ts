/**
 * Protocol Buffers TypeScript 定义
 * 由 ts-proto 自动生成
 * 源文件: protocols/proto/*.proto
 * 生成命令: npm run build:proto
 */

export { ServerStatus, ErrorCode } from './common';
export type { Packet, Response } from './common';
export type { FullRoleInfo, ItemInfo, TaskInfo, EnterGameRequest, EnterGameResponse, CreateRoleRequest, CreateRoleResponse } from './game';
export { MessageType } from './gateway';
export type { HeartbeatRequest, HeartbeatResponse, ClientInfo, ConnectRequest, ConnectResponse, DisconnectNotify } from './gateway';
export type { AccountLoginRequest, AccountLoginResponse, SelectServerRequest, RoleBrief, SelectServerResponse } from './login';
export { MessageId } from './message_id';
export type { ServerInfo, GetServerListRequest, GetServerListResponse, ServerStatusUpdate } from './server';

import { ServerStatus, ErrorCode } from './common';
import type { Packet, Response } from './common';
import type { FullRoleInfo, ItemInfo, TaskInfo, EnterGameRequest, EnterGameResponse, CreateRoleRequest, CreateRoleResponse } from './game';
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
    FullRoleInfo: {
      create: (init?: Partial<FullRoleInfo>): FullRoleInfo =>
        createMessage({ role_id: 0, role_name: '', level: 0, exp: 0, avatar_id: 0, gold: 0, diamond: 0, total_power: 0, vip_level: 0, create_time: 0, last_login_time: 0 }, init),
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
        createMessage({ code: ErrorCode.SUCCESS, message: '', items: [], tasks: [], server_time: 0 }, init),
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
        createMessage({ code: ErrorCode.SUCCESS, message: '', items: [], tasks: [], server_time: 0 }, init),
      decode: (data: string): CreateRoleResponse =>
        pb_decode("game.CreateRoleResponse", data) as CreateRoleResponse,
      encode: (msg: CreateRoleResponse): string =>
        pb_encode("game.CreateRoleResponse", msg),
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
