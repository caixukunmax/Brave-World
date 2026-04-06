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
// 注意：pb_*、token_*、mongo_* 函数已在 skynet.d.ts 和 mongo.d.ts 中声明
declare function password_hash(password: string): string;
declare function password_verify(password: string, stored_hash: string): boolean;
declare function mongo_ensureIndex(col: any, spec: any): void;
declare function mongo_findAndModify(col: any, options: any): any;

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
