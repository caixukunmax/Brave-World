/// <reference path="../types.ts" />
import { IPlatform } from "../types";
import { proto, MessageId, ErrorCode } from "../protos";

export class LoginLogic {
    private platform: IPlatform;

    constructor(platform: IPlatform) {
        this.platform = platform;
    }

    init(): void {
        this.platform.log("info", "login_logic init");
    }

    /**
     * 账号登录
     * 自动注册：账号不存在则创建
     * 返回: { msg_id, data } 给 Gateway 转发给客户端
     */
    accountLogin(msg: { conn_id: number; session: number; token: string; data: string }): any {
        // 1. 解码请求
        const req = proto.login.AccountLoginRequest.decode(msg.data);
        if (!req) {
            // INVALID_REQUEST: 无效的请求格式
            return this.makeError(MessageId.LOGIN_ACCOUNT_LOGIN_RSP, ErrorCode.INVALID_REQUEST);
        }

        const username: string = req.username || "";
        const password: string = req.password || "";

        if (!username || username.length < 2) {
            // INVALID_ACCOUNT_FORMAT: 用户名格式错误
            return this.makeError(MessageId.LOGIN_ACCOUNT_LOGIN_RSP, ErrorCode.INVALID_ACCOUNT_FORMAT);
        }
        if (!password || password.length < 1) {
            // INVALID_PASSWORD_FORMAT: 密码格式错误
            return this.makeError(MessageId.LOGIN_ACCOUNT_LOGIN_RSP, ErrorCode.INVALID_PASSWORD_FORMAT);
        }

        // 2. 查询账号
        let account = this.platform.serviceCall("db_service", "findAccountByUsername", username) as any;

        if (!account) {
            // 3. 自动注册
            account = this.platform.serviceCall("db_service", "createAccount", username, password) as any;
            this.platform.log("info", "Auto-registered account: " + username + " id=" + account.account_id);
        } else {
            // 4. 验证密码
            if (!password_verify(password, account.password)) {
                // PASSWORD_ERROR: 密码错误
                return this.makeError(MessageId.LOGIN_ACCOUNT_LOGIN_RSP, ErrorCode.PASSWORD_ERROR);
            }

            if (account.status === 1) {
                // ACCOUNT_BANNED: 账号已被封禁
                return this.makeError(MessageId.LOGIN_ACCOUNT_LOGIN_RSP, ErrorCode.ACCOUNT_BANNED);
            }
        }

        // 5. 获取区服列表
        const servers = this.platform.serviceCall("db_service", "getServers") as any[];

        // 6. 生成 AccountToken
        const accountToken = token_generate_account(account.account_id, username);

        // 7. 构造响应
        const serverList: any[] = [];
        if (servers !== undefined && servers.length > 0) {
            for (const s of servers) {
                // 检查该账号在该服是否有角色
                const roles = this.platform.serviceCall("db_service", "findRolesByAccountAndServer", account.account_id, s.server_id) as any[];
                const hasRole = roles && roles.length > 0;

                serverList.push({
                    server_id: s.server_id || 0,
                    server_name: s.server_name || "",
                    host: s.host || "",
                    port: s.port || 0,
                    status: s.status || 0,
                    online_count: s.online_count || 0,
                    is_new: s.is_new || false,
                    is_recommend: s.is_recommend || false,
                    has_role: hasRole,
                    role_count: (roles !== undefined && roles.length > 0) ? roles.length : 0,
                });
            }
        }

        const response = {
            code: ErrorCode.SUCCESS,
            message: "",
            account_token: accountToken,
            account_id: account.account_id,
            servers: serverList,
            last_server_id: account.last_server_id || 0,
            last_role_name: account.last_role_name || "",
        };

        const rspData = proto.login.AccountLoginResponse.encode(response);
        this.platform.log("info", "Login success: " + username + " accountId=" + account.account_id);

        return { msg_id: MessageId.LOGIN_ACCOUNT_LOGIN_RSP, data: rspData };
    }

    /**
     * 选服
     * 验证 AccountToken，获取角色列表，生成 GatewayToken
     */
    selectServer(msg: { conn_id: number; session: number; token: string; data: string }): any {
        // 1. 解码请求
        const req = proto.login.SelectServerRequest.decode(msg.data);
        if (!req) {
            // INVALID_REQUEST: 无效的请求格式
            return this.makeError(MessageId.LOGIN_SELECT_SERVER_RSP, ErrorCode.INVALID_REQUEST);
        }

        const accountToken: string = req.account_token || "";
        const serverId: number = req.server_id || 0;

        // 2. 验证 AccountToken
        const claims = token_validate_account(accountToken);
        if (!claims) {
            // UNAUTHORIZED: Token无效或已过期
            return this.makeError(MessageId.LOGIN_SELECT_SERVER_RSP, ErrorCode.UNAUTHORIZED);
        }

        // 3. 检查区服是否存在
        const server = this.platform.serviceCall("db_service", "findServerById", serverId) as any;
        if (!server) {
            // SERVER_NOT_FOUND: 区服不存在
            return this.makeError(MessageId.LOGIN_SELECT_SERVER_RSP, ErrorCode.SERVER_NOT_FOUND);
        }
        if (server.status === 0) {
            // SERVER_MAINTENANCE: 区服维护中
            return this.makeError(MessageId.LOGIN_SELECT_SERVER_RSP, ErrorCode.SERVER_MAINTENANCE);
        }

        // 4. 查询该账号在该服的角色列表
        const roles = this.platform.serviceCall("db_service", "findRolesByAccountAndServer", claims.account_id, serverId) as any[];

        const roleList: any[] = [];
        let lastRoleName = "";
        let lastLoginTime = 0;
        if (roles !== undefined && roles.length > 0) {
            for (const r of roles) {
                roleList.push({
                    role_id: r.role_id || 0,
                    role_name: r.role_name || "",
                    level: r.level || 1,
                    avatar_id: r.avatar_id || 0,
                    last_login: r.last_login_time || 0,
                    total_power: r.total_power || 0,
                });
                if ((r.last_login_time || 0) > lastLoginTime) {
                    lastLoginTime = r.last_login_time;
                    lastRoleName = r.role_name || "";
                }
            }
        }

        // 5. 生成 GatewayToken
        const gatewayToken = token_generate_gateway(claims.account_id, serverId);

        // 6. 更新最后登录区服
        this.platform.serviceSend("db_service", "updateAccountLastServer", claims.account_id, serverId, lastRoleName);

        // 7. 构造响应（bind_token 让 Gateway 在发送响应前绑定 token，避免竞态）
        const response = {
            code: ErrorCode.SUCCESS,
            message: "",
            gateway_token: gatewayToken,
            roles: roleList,
            max_role_count: 3,  // TODO: 从配置表读取
            server_time: Math.floor(skynet.time()),
        };

        const rspData = proto.login.SelectServerResponse.encode(response);
        this.platform.log("info", "SelectServer: accountId=" + claims.account_id + " serverId=" + serverId);

        return {
            msg_id: MessageId.LOGIN_SELECT_SERVER_RSP,
            data: rspData,
            bind_token: gatewayToken,
            account_id: claims.account_id,
            server_id: serverId,
        };
    }

    private makeError(msgId: number, code: ErrorCode): any {
        const rspData = proto.common.Response.encode({ code: code, message: "", data: new Uint8Array(0) });
        return { msg_id: msgId, data: rspData };
    }
}
