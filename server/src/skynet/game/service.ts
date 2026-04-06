import { defineService } from "../service";
import { platform } from "../platform";
import { GameLogic } from "./logic";

const logic = new GameLogic(platform);

defineService({
    createRole(msg: any): any {
        return logic.createRole(msg);
    },
    enterGame(msg: any): any {
        return logic.enterGame(msg);
    },
}, function() {
    logic.init();
    // 向 Gateway 注册路由
    const gatewayAddr = skynet.queryservice("gateway_service");
    skynet.send(gatewayAddr, "lua", "register", {
        service_name: "game",
        service_addr: skynet.self(),
        routes: {
            [320]: "enterGame",     // GAME_ENTER_GAME_REQ
            [322]: "createRole",    // GAME_CREATE_ROLE_REQ
        } as Record<number, string>,
    });
    platform.log("info", "game_service started");
});
