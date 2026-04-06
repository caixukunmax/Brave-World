import { IPlatform } from "./types";

// 服务地址缓存，避免每次 queryservice
const serviceCache: Map<string, any> = new Map();

// 定时器管理
let timerIdCounter = 1000;
const pendingTimers: Map<number, boolean> = new Map();

function generateTimerId(): number {
    return ++timerIdCounter;
}

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
        const id = generateTimerId();
        pendingTimers.set(id, true);
        skynet.timeout(msToTicks(delayMs), () => {
            if (pendingTimers.get(id)) {
                pendingTimers.delete(id);
                callback();
            }
        });
        return id;
    },

    clearInterval(id) {
        pendingTimers.delete(id);
    },

    serviceCall(serviceName, method, ...args) {
        try {
            return skynet.call(getServiceAddr(serviceName), "lua", method, ...args);
        } catch (e) {
            // 清除可能失效的服务缓存
            serviceCache.delete(serviceName);
            throw e;
        }
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
