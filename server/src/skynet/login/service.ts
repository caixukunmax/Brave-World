import { defineService } from "../service";
import { platform } from "../platform";
import { LoginLogic } from "./logic";

const logic = new LoginLogic(platform);

defineService({
    accountLogin(msg: any): any {
        return logic.accountLogin(msg);
    },
    selectServer(msg: any): any {
        return logic.selectServer(msg);
    },
}, function() {
    logic.init();
    // 向 Gateway 注册路由
    const gatewayAddr = skynet.queryservice("gateway_service");
    skynet.send(gatewayAddr, "lua", "register", {
        service_name: "login",
        service_addr: skynet.self(),
        routes: {
            [210]: "accountLogin",   // LOGIN_ACCOUNT_LOGIN_REQ
            [212]: "selectServer",   // LOGIN_SELECT_SERVER_REQ
        } as Record<number, string>,
    });
    platform.log("info", "login_service started");
});
