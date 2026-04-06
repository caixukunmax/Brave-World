import { IPlatform, LoginResult, PlayerInfo } from "../types";

export class PlayerLogic {
    private platform: IPlatform;
    private players: Map<string, PlayerInfo> = new Map();

    constructor(platform: IPlatform) {
        this.platform = platform;
    }

    login(userId: string, token: string): LoginResult {
        this.platform.log("info", "Player login", { userId: userId });
        const player = this.platform.serviceCall("db_service", "queryPlayer", userId) as PlayerInfo | null;
        if (!player) {
            return { success: false, error: "Player not found" };
        }
        this.players.set(userId, player);
        return { success: true, sessionId: "s_" + userId };
    }

    getOnlineCount(): number {
        return this.players.size;
    }

    kick(userId: string): void {
        this.players.delete(userId);
        this.platform.serviceSend("gateway", "kickUser", userId);
        this.platform.log("info", "Player kicked", { userId: userId });
    }
}
