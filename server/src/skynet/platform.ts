import { IPlatform } from "./types";

// 服务地址缓存，避免每次 queryservice
const serviceCache: Map<string, any> = new Map();

function getServiceAddr(name: string): any {
    let addr = serviceCache.get(name);
    if (addr === undefined) {
        addr = skynet.queryservice(name);
        serviceCache.set(name, addr);
    }
    return addr;
}

// skynet.timeout 单位是 1/100 秒 (10ms)
function msToTicks(ms: number): number {
    return Math.max(1, Math.floor(ms / 10));
}

export const platform: IPlatform = {
    setTimeout(delayMs, callback) {
        return skynet.timeout(msToTicks(delayMs), callback);
    },

    clearInterval(id) {
        // skynet 无 cancelTimeout，通过标记位忽略
    },

    serviceCall(serviceName, method, ...args) {
        return skynet.call(getServiceAddr(serviceName), "lua", method, ...args);
    },

    serviceSend(serviceName, method, ...args) {
        skynet.send(getServiceAddr(serviceName), "lua", method, ...args);
    },

    log(level, msg, data) {
        if (data !== undefined) {
            skynet.error("[" + level + "] " + msg + " " + String(data));
        } else {
            skynet.error("[" + level + "] " + msg);
        }
    }
};
