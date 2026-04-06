/** @noSelf */
// 平台抽象接口 — 翻译部分和 Node.js 端各自实现
export interface IPlatform {
    setTimeout(delayMs: number, callback: () => void): number;
    clearInterval(id: number): void;
    serviceCall(serviceName: string, method: string, ...args: any[]): any;
    serviceSend(serviceName: string, method: string, ...args: any[]): void;
    log(level: string, msg: string, data?: any): void;
}

// 全局函数声明（由 preload.lua 注入）
declare function password_hash(password: string): string;
declare function password_verify(password: string, stored_hash: string): boolean;
declare function pb_decode(msg_type: string, data: string): any;
declare function pb_encode(msg_type: string, data: any): string;
declare function token_generate_account(account_id: number, username: string): string;
declare function token_validate_account(token: string): any;
declare function token_generate_gateway(account_id: number, server_id: number): string;
declare function token_validate_gateway(token: string): any;
declare function mongo_findOne(col: any, query: any): any;
declare function mongo_findArray(col: any, query: any): any[];
declare function mongo_insert(col: any, doc: any): void;
declare function mongo_update(col: any, query: any, update: any, upsert?: boolean, multi?: boolean): void;
declare function mongo_count(col: any, query: any): number;

// 消息协议类型
export interface LoginRequest {
    userId: string;
    token: string;
}

export interface LoginResult {
    success: boolean;
    sessionId?: string;
    error?: string;
}

export interface PlayerInfo {
    userId: string;
    name: string;
    level: number;
    gold: number;
}
