export function defineService<T extends Record<string, (...args: any[]) => any>>(
    commands: T,
    init?: () => void
): void {
    skynet.start(function() {
        skynet.dispatch("lua", function(session: number, address: number, cmd: string, ...args: any[]) {
            const handler = commands[cmd];
            if (!handler) {
                skynet.error("Unknown command: " + cmd);
                return;
            }
            const result = handler(...args);
            // session > 0: skynet.call 发来，需要响应
            // result !== undefined: 同步返回值，自动 retpack
            if (session > 0 && result !== undefined) {
                skynet.retpack(result);
            }
        });

        if (init) {
            init();
        }
    });
}
