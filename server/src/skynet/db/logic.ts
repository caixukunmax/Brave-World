/// <reference path="../types.ts" />
import { IPlatform, PlayerInfo } from "../types";

export class DbLogic {
    private platform: IPlatform;
    private playersCol: any;
    private accountsCol: any;
    private rolesCol: any;
    private serversCol: any;
    private countersCol: any;

    constructor(platform: IPlatform) {
        this.platform = platform;
        this.playersCol = null as any;
        this.accountsCol = null as any;
        this.rolesCol = null as any;
        this.serversCol = null as any;
    }

    init(): void {
        // Use os.getenv for system environment variables (Docker)
        const host = (os as any).getenv("MONGO_HOST") || skynet.getenv("MONGO_HOST") || "127.0.0.1";
        const port = Number((os as any).getenv("MONGO_PORT") || skynet.getenv("MONGO_PORT") || "27017");
        this.platform.log("info", "Connecting to MongoDB", host + ":" + port);

        const client: any = mongo.client({ host, port });
        const db: any = client["tslua2"];
        this.playersCol = db["players"];
        this.accountsCol = db["accounts"];
        this.rolesCol = db["roles"];
        this.serversCol = db["servers"];
        this.countersCol = db["counters"];

        this.platform.log("info", "MongoDB connected");

        // 确保 username 唯一索引
        this.ensureIndexes();
    }

    private ensureIndexes(): void {
        try {
            // 确保 username 唯一索引
            mongo_ensureIndex(this.accountsCol, { key: { username: 1 }, unique: true, name: "username_idx" });
            // 确保角色名在服务器内唯一
            mongo_ensureIndex(this.rolesCol, { key: { server_id: 1, role_name: 1 }, unique: true, name: "server_role_name_idx" });
        } catch (_e) {
            // ignore
        }
    }

    // 获取自增 ID（原子操作）
    private getNextId(counterName: string): number {
        // 使用 findAndModify 实现原子自增
        const result = mongo_findAndModify(this.countersCol, {
            query: { _id: counterName },
            update: { ["$inc"]: { seq: 1 } },
            upsert: true,
            new: true  // 返回更新后的文档
        });
        
        // 如果是新创建的文档，seq 从 1000 开始
        if (!result || result.seq === 1) {
            // 第一次创建，设置为 1000
            mongo_update(
                this.countersCol,
                { _id: counterName },
                { ["$set"]: { seq: 1000 } }
            );
            return 1000;
        }
        
        return result.seq;
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
        // 使用自增 ID
        const accountId = this.getNextId("account_id");
        
        // 密码加密
        const hashedPassword = password_hash(password);

        const doc = {
            account_id: accountId,
            username: username,
            password: hashedPassword,
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

    updateAccountPassword(accountId: number, hashedPassword: string): void {
        mongo_update(
            this.accountsCol,
            { account_id: accountId },
            { ["$set"]: { password: hashedPassword } },
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

    getNextRoleId(): number {
        return this.getNextId("role_id");
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
