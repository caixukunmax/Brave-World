import { IPlatform } from "../types";

export class GameLogic {
    private platform: IPlatform;

    constructor(platform: IPlatform) {
        this.platform = platform;
    }

    init(): void {
        this.platform.log("info", "game_logic init");
    }

    /**
     * 创建角色
     */
    createRole(msg: { conn_id: number; session: number; token: string; data: string }): any {
        // 1. 验证 GatewayToken
        if (!msg.token) {
            return this.makeError(323, 3, "未授权，请先选服");  // UNAUTHORIZED
        }
        const claims = token_validate_gateway(msg.token);
        if (!claims) {
            return this.makeError(323, 3, "Token无效或已过期");  // UNAUTHORIZED
        }

        // 2. 解码请求
        const req = pb_decode("game.CreateRoleRequest", msg.data);
        if (!req) {
            return this.makeError(323, 2, "无效的请求格式");
        }

        const roleName: string = req.role_name || "";

        // 3. 校验角色名
        if (roleName.length < 2) {
            return this.makeError(323, 204, "角色名太短");  // ROLE_NAME_TOO_SHORT
        }
        if (roleName.length > 12) {
            return this.makeError(323, 205, "角色名太长");  // ROLE_NAME_TOO_LONG
        }

        // 4. 检查角色名是否已存在
        const nameExists = this.platform.serviceCall("db_service", "checkRoleNameExists", claims.server_id, roleName) as boolean;
        if (nameExists) {
            return this.makeError(323, 201, "角色名已存在");  // ROLE_NAME_EXISTS
        }

        // 5. 检查角色数量上限
        const roleCount = this.platform.serviceCall("db_service", "countRolesByAccountAndServer", claims.account_id, claims.server_id) as number;
        if (roleCount >= 3) {
            return this.makeError(323, 202, "角色数量已达上限");  // ROLE_COUNT_LIMIT
        }

        // 6. 创建角色
        const roleId = Math.floor(skynet.now() / 100) * 10000 + Math.floor(Math.random() * 10000) + 1;
        const now = Math.floor(skynet.time());

        const roleData = {
            role_id: roleId,
            account_id: claims.account_id,
            server_id: claims.server_id,
            role_name: roleName,
            level: 1,
            exp: 0,
            avatar_id: 0,
            gold: 10000,
            diamond: 100,
            total_power: 100,
            vip_level: 0,
            create_time: now,
            last_login_time: now,
        };

        this.platform.serviceCall("db_service", "createRole", roleData);

        // 7. 构造响应
        const response = {
            code: 0,
            message: "",
            role_info: {
                role_id: roleId,
                role_name: roleName,
                level: 1,
                exp: 0,
                avatar_id: 0,
                gold: 10000,
                diamond: 100,
                total_power: 100,
                vip_level: 0,
                create_time: now,
                last_login_time: now,
            },
            items: [],  // TODO: 初始道具
            tasks: [],  // TODO: 初始任务
            server_time: now,
        };

        const rspData = pb_encode("game.CreateRoleResponse", response);
        this.platform.log("info", "CreateRole: " + roleName + " roleId=" + roleId);

        return { msg_id: 323, data: rspData };
    }

    /**
     * 进入游戏
     */
    enterGame(msg: { conn_id: number; session: number; token: string; data: string }): any {
        // 1. 验证 GatewayToken
        if (!msg.token) {
            return this.makeError(321, 3, "未授权，请先选服");
        }
        const claims = token_validate_gateway(msg.token);
        if (!claims) {
            return this.makeError(321, 3, "Token无效或已过期");
        }

        // 2. 解码请求
        const req = pb_decode("game.EnterGameRequest", msg.data);
        if (!req) {
            return this.makeError(321, 2, "无效的请求格式");
        }

        const roleId: number = req.role_id || 0;
        if (roleId === 0) {
            return this.makeError(321, 2, "角色ID不能为空");
        }

        // 3. 查询角色
        const role = this.platform.serviceCall("db_service", "findRoleById", roleId) as any;
        if (!role) {
            return this.makeError(321, 200, "角色不存在");  // ROLE_NOT_FOUND
        }

        // 验证角色属于该账号
        if (role.account_id !== claims.account_id || role.server_id !== claims.server_id) {
            return this.makeError(321, 4, "无权操作此角色");  // FORBIDDEN
        }

        // 4. 更新最后登录时间
        const now = Math.floor(skynet.time());
        this.platform.serviceSend("db_service", "updateRole", roleId, { last_login_time: now });

        // 5. 构造响应
        const response = {
            code: 0,
            message: "",
            role_info: {
                role_id: role.role_id || 0,
                role_name: role.role_name || "",
                level: role.level || 1,
                exp: role.exp || 0,
                avatar_id: role.avatar_id || 0,
                gold: role.gold || 0,
                diamond: role.diamond || 0,
                total_power: role.total_power || 0,
                vip_level: role.vip_level || 0,
                create_time: role.create_time || 0,
                last_login_time: now,
            },
            items: [],  // TODO: 加载背包
            tasks: [],  // TODO: 加载任务
            server_time: now,
        };

        const rspData = pb_encode("game.EnterGameResponse", response);
        this.platform.log("info", "EnterGame: roleId=" + roleId + " name=" + (role.role_name || "?"));

        return { msg_id: 321, data: rspData };
    }

    private makeError(msgId: number, code: number, message: string): any {
        const rspData = pb_encode("common.Response", { code: code, message: message, data: "" });
        return { msg_id: msgId, data: rspData };
    }
}
