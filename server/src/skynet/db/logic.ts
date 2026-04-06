import { IPlatform, PlayerInfo } from "../types";

export class DbLogic {
    private platform: IPlatform;
    private playersCol: any;
    private accountsCol: any;
    private rolesCol: any;
    private serversCol: any;

    constructor(platform: IPlatform) {
        this.platform = platform;
        this.playersCol = null as any;
        this.accountsCol = null as any;
        this.rolesCol = null as any;
        this.serversCol = null as any;
    }

    init(): void {
        const host = skynet.getenv("MONGO_HOST") || "127.0.0.1";
        const port = Number(skynet.getenv("MONGO_PORT") || "27017");
        this.platform.log("info", "Connecting to MongoDB", host + ":" + port);

        const client: any = mongo.client({ host, port });
        const db: any = client["tslua2"];
        this.playersCol = db["players"];
        this.accountsCol = db["accounts"];
        this.rolesCol = db["roles"];
        this.serversCol = db["servers"];

        this.platform.log("info", "MongoDB connected");

        // 确保 username 唯一索引
        this.ensureIndexes();
    }

    private ensureIndexes(): void {
        try {
            mongo_update(
                this.accountsCol,
                {},
                { ["$set"]: {} },
                false
            );
        } catch (_e) {
            // ignore
        }
    }

    // ========== 原有 players ==========

    queryPlayer(userId: string): PlayerInfo | null {
        const doc = mongo_findOne(this.playersCol, { user_id: userId });
        if (!doc) {
            return null;
        }
        return {
            userId: doc.user_id,
            name: doc.name,
            level: doc.level,
            gold: doc.gold,
        };
    }

    savePlayer(player: PlayerInfo): void {
        mongo_update(
            this.playersCol,
            { user_id: player.userId },
            { ["$set"]: { name: player.name, level: player.level, gold: player.gold } },
            true
        );
    }

    createPlayer(player: PlayerInfo): void {
        mongo_insert(this.playersCol, {
            user_id: player.userId,
            name: player.name,
            level: player.level,
            gold: player.gold,
        });
    }

    // ========== accounts 集合 ==========

    findAccountByUsername(username: string): any {
        return mongo_findOne(this.accountsCol, { username: username });
    }

    findAccountById(accountId: number): any {
        return mongo_findOne(this.accountsCol, { account_id: accountId });
    }

    createAccount(username: string, password: string): any {
        // 自增 account_id
        const lastDoc = mongo_findOne(this.accountsCol, {});
        // 简单方案：用时间戳 + 随机数生成 account_id
        const accountId = Math.floor(skynet.now() / 100) + Math.floor(Math.random() * 10000) + 1;

        const doc = {
            account_id: accountId,
            username: username,
            password: password,
            status: 0,           // 0=正常
            last_server_id: 0,
            last_role_name: "",
            created_at: skynet.time(),
        };
        mongo_insert(this.accountsCol, doc);
        return doc;
    }

    updateAccountLastServer(accountId: number, serverId: number, roleName: string): void {
        mongo_update(
            this.accountsCol,
            { account_id: accountId },
            { ["$set"]: { last_server_id: serverId, last_role_name: roleName } },
            false
        );
    }

    // ========== roles 集合 ==========

    findRolesByAccountAndServer(accountId: number, serverId: number): any[] {
        // mongo_findOne 只返回一条，需要 find
        // 由于 tstl 限制，用辅助函数
        return mongo_findArray(this.rolesCol, { account_id: accountId, server_id: serverId });
    }

    findRoleById(roleId: number): any {
        return mongo_findOne(this.rolesCol, { role_id: roleId });
    }

    createRole(roleData: any): void {
        mongo_insert(this.rolesCol, roleData);
    }

    updateRole(roleId: number, updates: any): void {
        mongo_update(
            this.rolesCol,
            { role_id: roleId },
            { ["$set"]: updates },
            false
        );
    }

    checkRoleNameExists(serverId: number, roleName: string): boolean {
        const doc = mongo_findOne(this.rolesCol, { server_id: serverId, role_name: roleName });
        return doc != null;
    }

    countRolesByAccountAndServer(accountId: number, serverId: number): number {
        return mongo_count(this.rolesCol, { account_id: accountId, server_id: serverId });
    }

    // ========== servers 集合 ==========

    getServers(): any[] {
        return mongo_findArray(this.serversCol, {});
    }

    findServerById(serverId: number): any {
        return mongo_findOne(this.serversCol, { server_id: serverId });
    }

    updateServerOnlineCount(serverId: number, count: number): void {
        mongo_update(
            this.serversCol,
            { server_id: serverId },
            { ["$set"]: { online_count: count } },
            false
        );
    }
}
