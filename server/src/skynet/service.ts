interface ServiceOptions {
    init?: () => void;
}

/**
 * 定义 Skynet 服务
 * handlers 的 key 可以是:
 *   - number (MessageId 枚举): 自动注册为 Gateway 路由
 *   - string: 内部命令，其他服务通过 skynet.call 调用
 */
export function defineService(
    name: string,
    handlers: Record<string | number, (...args: any[]) => any>,
    options?: ServiceOptions
): void {
    skynet.start(function() {
        // 收集数字键(msg_id)用于 Gateway 路由注册
        let routeCount = 0;
        const routeMsgIds: any = [];
        for (const key in handlers) {
            if (typeof key === "number") {
                table.insert(routeMsgIds, key);
                routeCount++;
            }
        }

        skynet.dispatch("lua", function(session: number, address: number, cmd: string | number, ...args: any[]) {
            skynet.error("[Service:" + name + "] dispatch cmd=" + tostring(cmd) + " session=" + tostring(session));
            const handler = handlers[cmd as any];
            if (!handler) {
                skynet.error("Unknown command: " + tostring(cmd));
                return;
            }
            const result = handler(...args);
            if (session > 0 && result !== undefined) {
                skynet.retpack(result);
            }
        });

        // 有数字键 → 自动注册 Gateway 路由
        if (routeCount > 0) {
            const gatewayAddr = skynet.queryservice("gateway_service");
            skynet.send(gatewayAddr, "lua", "register", {
                service_name: name,
                service_addr: skynet.self(),
                routes: routeMsgIds,
            });
        }

        if (options && options.init) {
            options.init();
        }
    });
}
