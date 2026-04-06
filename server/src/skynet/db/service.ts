import { defineService } from "../service";
import { platform } from "../platform";
import { DbLogic } from "./logic";

const logic = new DbLogic(platform);

defineService({
    // ========== players (原有) ==========
    queryPlayer(userId: string): any {
        return logic.queryPlayer(userId);
    },
    savePlayer(player: any): void {
        logic.savePlayer(player);
    },
    createPlayer(player: any): void {
        logic.createPlayer(player);
    },

    // ========== accounts ==========
    findAccountByUsername(username: string): any {
        return logic.findAccountByUsername(username);
    },
    findAccountById(accountId: number): any {
        return logic.findAccountById(accountId);
    },
    createAccount(username: string, password: string): any {
        return logic.createAccount(username, password);
    },
    updateAccountLastServer(accountId: number, serverId: number, roleName: string): void {
        logic.updateAccountLastServer(accountId, serverId, roleName);
    },

    // ========== roles ==========
    findRolesByAccountAndServer(accountId: number, serverId: number): any {
        return logic.findRolesByAccountAndServer(accountId, serverId);
    },
    findRoleById(roleId: number): any {
        return logic.findRoleById(roleId);
    },
    createRole(roleData: any): void {
        logic.createRole(roleData);
    },
    updateRole(roleId: number, updates: any): void {
        logic.updateRole(roleId, updates);
    },
    checkRoleNameExists(serverId: number, roleName: string): boolean {
        return logic.checkRoleNameExists(serverId, roleName);
    },
    countRolesByAccountAndServer(accountId: number, serverId: number): number {
        return logic.countRolesByAccountAndServer(accountId, serverId);
    },

    // ========== servers ==========
    getServers(): any {
        return logic.getServers();
    },
    findServerById(serverId: number): any {
        return logic.findServerById(serverId);
    },
    updateServerOnlineCount(serverId: number, count: number): void {
        logic.updateServerOnlineCount(serverId, count);
    },
}, function() {
    logic.init();
    platform.log("info", "db_service started");
});
