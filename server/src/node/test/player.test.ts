import { PlayerLogic } from "../../skynet/player/logic";
import { platform, registerService, resetServices } from "../platform";

describe("PlayerLogic", () => {
    let logic: PlayerLogic;

    beforeEach(() => {
        resetServices();
        registerService("db_service", {
            queryPlayer(userId: string) {
                if (userId === "123") {
                    return { userId: "123", name: "TestPlayer", level: 10, gold: 1000 };
                }
                return null;
            }
        });
        registerService("gateway", {
            kickUser(userId: string) {
                // mock: do nothing
            }
        });
        logic = new PlayerLogic(platform);
    });

    test("login success", () => {
        const result = logic.login("123", "token");
        expect(result.success).toBe(true);
        expect(result.sessionId).toBe("s_123");
    });

    test("login fail - player not found", () => {
        const result = logic.login("999", "token");
        expect(result.success).toBe(false);
        expect(result.error).toBe("Player not found");
    });

    test("kick player", () => {
        logic.login("123", "token");
        expect(logic.getOnlineCount()).toBe(1);
        logic.kick("123");
        expect(logic.getOnlineCount()).toBe(0);
    });
});
