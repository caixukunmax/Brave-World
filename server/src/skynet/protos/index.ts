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
    },
    Response: {
      create: (init?: Partial<Response>): Response =>
        createMessage({ code: ErrorCode.SUCCESS, message: '', data: new Uint8Array(0) }, init),
    },
  },
  game: {
    FullRoleInfo: {
      create: (init?: Partial<FullRoleInfo>): FullRoleInfo =>
        createMessage({ role_id: 0, role_name: '', level: 0, exp: 0, avatar_id: 0, gold: 0, diamond: 0, total_power: 0, vip_level: 0, create_time: 0, last_login_time: 0 }, init),
    },
    ItemInfo: {
      create: (init?: Partial<ItemInfo>): ItemInfo =>
        createMessage({ item_id: 0, count: 0 }, init),
    },
    TaskInfo: {
      create: (init?: Partial<TaskInfo>): TaskInfo =>
        createMessage({ task_id: 0, status: 0, progress: 0 }, init),
    },
    EnterGameRequest: {
      create: (init?: Partial<EnterGameRequest>): EnterGameRequest =>
        createMessage({ role_id: 0 }, init),
    },
    EnterGameResponse: {
      create: (init?: Partial<EnterGameResponse>): EnterGameResponse =>
        createMessage({ code: ErrorCode.SUCCESS, message: '', items: [], tasks: [], server_time: 0 }, init),
    },
    CreateRoleRequest: {
      create: (init?: Partial<CreateRoleRequest>): CreateRoleRequest =>
        createMessage({ role_name: '' }, init),
    },
    CreateRoleResponse: {
      create: (init?: Partial<CreateRoleResponse>): CreateRoleResponse =>
        createMessage({ code: ErrorCode.SUCCESS, message: '', items: [], tasks: [], server_time: 0 }, init),
    },
  },
  gateway: {
    MessageType,
    HeartbeatRequest: {
      create: (init?: Partial<HeartbeatRequest>): HeartbeatRequest =>
        createMessage({ client_time: 0 }, init),
    },
    HeartbeatResponse: {
      create: (init?: Partial<HeartbeatResponse>): HeartbeatResponse =>
        createMessage({ server_time: 0, online_count: 0 }, init),
    },
    ClientInfo: {
      create: (init?: Partial<ClientInfo>): ClientInfo =>
        createMessage({ ip: '', port: 0, version: '', platform: '', device_id: '' }, init),
    },
    ConnectRequest: {
      create: (init?: Partial<ConnectRequest>): ConnectRequest =>
        createMessage({ token: '' }, init),
    },
    ConnectResponse: {
      create: (init?: Partial<ConnectResponse>): ConnectResponse =>
        createMessage({ code: ErrorCode.SUCCESS, message: '', conn_id: 0, server_time: 0 }, init),
    },
    DisconnectNotify: {
      create: (init?: Partial<DisconnectNotify>): DisconnectNotify =>
        createMessage({ conn_id: 0, reason: '' }, init),
    },
  },
  login: {
    AccountLoginRequest: {
      create: (init?: Partial<AccountLoginRequest>): AccountLoginRequest =>
        createMessage({ username: '', password: '', platform: '', device_id: '', client_version: '' }, init),
    },
    AccountLoginResponse: {
      create: (init?: Partial<AccountLoginResponse>): AccountLoginResponse =>
        createMessage({ code: ErrorCode.SUCCESS, message: '', account_token: '', account_id: 0, servers: [], last_server_id: 0, last_role_name: '' }, init),
    },
    SelectServerRequest: {
      create: (init?: Partial<SelectServerRequest>): SelectServerRequest =>
        createMessage({ account_token: '', server_id: 0 }, init),
    },
    RoleBrief: {
      create: (init?: Partial<RoleBrief>): RoleBrief =>
        createMessage({ role_id: 0, role_name: '', level: 0, avatar_id: 0, last_login: 0, total_power: 0 }, init),
    },
    SelectServerResponse: {
      create: (init?: Partial<SelectServerResponse>): SelectServerResponse =>
        createMessage({ code: ErrorCode.SUCCESS, message: '', gateway_token: '', roles: [], max_role_count: 0, server_time: 0 }, init),
    },
  },
  message_id: {
    MessageId,
  },
  server: {
    ServerInfo: {
      create: (init?: Partial<ServerInfo>): ServerInfo =>
        createMessage({ server_id: 0, server_name: '', host: '', port: 0, status: undefined, online_count: 0, is_new: false, is_recommend: false, has_role: false, role_count: 0 }, init),
    },
    GetServerListRequest: {
      create: (init?: Partial<GetServerListRequest>): GetServerListRequest =>
        createMessage({ account_token: '' }, init),
    },
    GetServerListResponse: {
      create: (init?: Partial<GetServerListResponse>): GetServerListResponse =>
        createMessage({ code: ErrorCode.SUCCESS, message: '', servers: [], last_server_id: 0 }, init),
    },
    ServerStatusUpdate: {
      create: (init?: Partial<ServerStatusUpdate>): ServerStatusUpdate =>
        createMessage({ server_id: 0, status: undefined, online_count: 0 }, init),
    },
  },
};

export default proto;
