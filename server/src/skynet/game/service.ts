import { defineService } from "../service";
import { platform } from "../platform";
import { GameLogic } from "./logic";
import { MessageId } from "../protos/message_id";

const logic = new GameLogic(platform);

defineService("game", {
    [MessageId.GAME_ENTER_GAME_REQ]: (msg: any) => logic.enterGame(msg),
    [MessageId.GAME_CREATE_ROLE_REQ]: (msg: any) => logic.createRole(msg),
}, {
    init() {
        logic.init();
        platform.log("info", "game_service started");
    },
});
