import { defineService } from "../service";
import { platform } from "../platform";
import { LoginLogic } from "./logic";
import { MessageId } from "../protos/message_id";

const logic = new LoginLogic(platform);

defineService("login", {
    [MessageId.LOGIN_ACCOUNT_LOGIN_REQ]: (msg: any) => logic.accountLogin(msg),
    [MessageId.LOGIN_SELECT_SERVER_REQ]: (msg: any) => logic.selectServer(msg),
}, {
    init() {
        logic.init();
        platform.log("info", "login_service started");
    },
});
