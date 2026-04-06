/** @noSelf */
// 平台抽象接口 — 翻译部分和 Node.js 端各自实现
export interface IPlatform {
    setTimeout(delayMs: number, callback: () => void): number;
    clearInterval(id: number): void;
    serviceCall(serviceName: string, method: string, ...args: any[]): any;
    serviceSend(serviceName: string, method: string, ...args: any[]): void;
    log(level: string, msg: string, data?: any): void;
}

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
