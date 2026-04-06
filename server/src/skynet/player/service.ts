import { defineService } from "../service";
import { platform } from "../platform";
import { PlayerLogic } from "./logic";

const logic = new PlayerLogic(platform);

defineService({
    login(msg: { userId: string; token: string }) {
        return logic.login(msg.userId, msg.token);
    },

    kick(userId: string) {
        logic.kick(userId);
    },

    getOnlineCount(): number {
        return logic.getOnlineCount();
    }
}, function() {
    platform.log("info", "player_service started");
});
