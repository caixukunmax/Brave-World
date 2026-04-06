import { IPlatform } from "../skynet/types";

// Node.js 端模拟服务注册表
const serviceRegistry = new Map<string, Record<string, (...args: any[]) => any>>();

export function registerService(name: string, handlers: Record<string, (...args: any[]) => any>) {
    serviceRegistry.set(name, handlers);
}

export function resetServices() {
    serviceRegistry.clear();
}

export const platform: IPlatform = {
    setTimeout(delayMs, callback) {
        return globalThis.setTimeout(callback, delayMs);
    },

    clearInterval(id) {
        globalThis.clearTimeout(id);
    },

    serviceCall(serviceName, method, ...args) {
        const service = serviceRegistry.get(serviceName);
        if (!service) throw new Error("Service not found: " + serviceName);
        const handler = service[method];
        if (!handler) throw new Error("Method not found: " + method);
        return handler(...args);
    },

    serviceSend(serviceName, method, ...args) {
        this.serviceCall(serviceName, method, ...args);
    },

    log(level, msg, data) {
        console.log("[" + level + "] " + msg, data !== undefined ? data : "");
    }
};
