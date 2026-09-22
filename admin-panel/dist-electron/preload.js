"use strict";
const electron = require("electron");
const electronAPI = {
  server: {
    start: (profileId) => electron.ipcRenderer.invoke("server:start", profileId),
    stop: () => electron.ipcRenderer.invoke("server:stop"),
    restart: (profileId) => electron.ipcRenderer.invoke("server:restart", profileId),
    getStatus: () => electron.ipcRenderer.invoke("server:getStatus"),
    sendCommand: (cmd) => electron.ipcRenderer.invoke("server:sendCommand", cmd),
    onOutput: (callback) => {
      const handler = (_, line) => callback(line);
      electron.ipcRenderer.on("server:output", handler);
      return () => electron.ipcRenderer.removeListener("server:output", handler);
    },
    onStatusChange: (callback) => {
      const handler = (_, status) => callback(status);
      electron.ipcRenderer.on("server:statusChange", handler);
      return () => electron.ipcRenderer.removeListener("server:statusChange", handler);
    }
  },
  build: {
    tables: () => electron.ipcRenderer.invoke("build:tables"),
    proto: () => electron.ipcRenderer.invoke("build:proto"),
    server: () => electron.ipcRenderer.invoke("build:server"),
    all: () => electron.ipcRenderer.invoke("build:all"),
    onOutput: (callback) => {
      const handler = (_, line) => callback(line);
      electron.ipcRenderer.on("build:output", handler);
      return () => electron.ipcRenderer.removeListener("build:output", handler);
    }
  },
  config: {
    listTables: () => electron.ipcRenderer.invoke("config:listTables"),
    readTable: (name) => electron.ipcRenderer.invoke("config:readTable", name),
    writeTable: (name, data) => electron.ipcRenderer.invoke("config:writeTable", name, data),
    diffTable: (name) => electron.ipcRenderer.invoke("config:diffTable", name),
    rollbackTable: (name) => electron.ipcRenderer.invoke("config:rollbackTable", name),
    validateTable: (name) => electron.ipcRenderer.invoke("config:validateTable", name),
    readAppSettings: () => electron.ipcRenderer.invoke("config:readAppSettings"),
    writeAppSettings: (data) => electron.ipcRenderer.invoke("config:writeAppSettings", data),
    readMapRegistry: () => electron.ipcRenderer.invoke("config:readMapRegistry"),
    writeMapRegistry: (data) => electron.ipcRenderer.invoke("config:writeMapRegistry", data),
    readBuildings: () => electron.ipcRenderer.invoke("config:readBuildings"),
    writeBuildings: (data) => electron.ipcRenderer.invoke("config:writeBuildings", data),
    getXlsxSyncStatus: () => electron.ipcRenderer.invoke("config:getXlsxSyncStatus")
  },
  database: {
    listAccounts: (filter) => electron.ipcRenderer.invoke("database:listAccounts", filter),
    getAccount: (id) => electron.ipcRenderer.invoke("database:getAccount", id),
    createAccount: (data) => electron.ipcRenderer.invoke("database:createAccount", data),
    updateAccount: (id, data) => electron.ipcRenderer.invoke("database:updateAccount", id, data),
    banAccount: (id) => electron.ipcRenderer.invoke("database:banAccount", id),
    unbanAccount: (id) => electron.ipcRenderer.invoke("database:unbanAccount", id),
    resetPassword: (id, newPassword) => electron.ipcRenderer.invoke("database:resetPassword", id, newPassword),
    listRoles: (filter) => electron.ipcRenderer.invoke("database:listRoles", filter),
    getRole: (id) => electron.ipcRenderer.invoke("database:getRole", id),
    updateRole: (id, data) => electron.ipcRenderer.invoke("database:updateRole", id, data),
    getInventory: (roleId) => electron.ipcRenderer.invoke("database:getInventory", roleId),
    addItem: (roleId, itemId, count) => electron.ipcRenderer.invoke("database:addItem", roleId, itemId, count),
    removeItem: (roleId, itemId, count) => electron.ipcRenderer.invoke("database:removeItem", roleId, itemId, count),
    listChests: () => electron.ipcRenderer.invoke("database:listChests"),
    createChest: (data) => electron.ipcRenderer.invoke("database:createChest", data),
    deleteChest: (id) => electron.ipcRenderer.invoke("database:deleteChest", id),
    getStats: () => electron.ipcRenderer.invoke("database:getStats")
  },
  logs: {
    startStreaming: (filter) => electron.ipcRenderer.invoke("logs:startStreaming", filter),
    stopStreaming: () => electron.ipcRenderer.invoke("logs:stopStreaming"),
    getHistory: (lines) => electron.ipcRenderer.invoke("logs:getHistory", lines),
    onLogLine: (callback) => {
      const handler = (_, line) => callback(line);
      electron.ipcRenderer.on("logs:newLine", handler);
      return () => electron.ipcRenderer.removeListener("logs:newLine", handler);
    }
  },
  monitoring: {
    getPerformance: () => electron.ipcRenderer.invoke("monitoring:getPerformance"),
    getOnlinePlayers: () => electron.ipcRenderer.invoke("monitoring:getOnlinePlayers"),
    getMapEntities: () => electron.ipcRenderer.invoke("monitoring:getMapEntities"),
    kickPlayer: (accountId) => electron.ipcRenderer.invoke("monitoring:kickPlayer", accountId),
    onMetricsUpdate: (callback) => {
      const handler = (_, metrics) => callback(metrics);
      electron.ipcRenderer.on("monitoring:metricsUpdate", handler);
      return () => electron.ipcRenderer.removeListener("monitoring:metricsUpdate", handler);
    }
  },
  gm: {
    sendCommand: (command, targetPlayerId) => electron.ipcRenderer.invoke("gm:sendCommand", command, targetPlayerId),
    broadcast: (message) => electron.ipcRenderer.invoke("gm:broadcast", message),
    listSchedules: () => electron.ipcRenderer.invoke("gm:listSchedules"),
    createSchedule: (task) => electron.ipcRenderer.invoke("gm:createSchedule", task),
    updateSchedule: (id, task) => electron.ipcRenderer.invoke("gm:updateSchedule", id, task),
    deleteSchedule: (id) => electron.ipcRenderer.invoke("gm:deleteSchedule", id)
  },
  maps: {
    listMaps: () => electron.ipcRenderer.invoke("maps:listMaps"),
    readMap: (mapName) => electron.ipcRenderer.invoke("maps:readMap", mapName),
    writeMap: (mapName, data) => electron.ipcRenderer.invoke("maps:writeMap", mapName, data),
    generateMap: (params) => electron.ipcRenderer.invoke("maps:generateMap", params),
    getPreviewImage: (mapName) => electron.ipcRenderer.invoke("maps:getPreviewImage", mapName),
    readSpawnPoints: (mapName) => electron.ipcRenderer.invoke("maps:readSpawnPoints", mapName),
    writeSpawnPoints: (mapName, points) => electron.ipcRenderer.invoke("maps:writeSpawnPoints", mapName, points),
    readNpcPlacements: (mapName) => electron.ipcRenderer.invoke("maps:readNpcPlacements", mapName),
    writeNpcPlacements: (mapName, npcs) => electron.ipcRenderer.invoke("maps:writeNpcPlacements", mapName, npcs)
  },
  proto: {
    listMessages: () => electron.ipcRenderer.invoke("proto:listMessages"),
    getMessageSchema: (msgName) => electron.ipcRenderer.invoke("proto:getMessageSchema", msgName),
    sendRequest: (msgId, data) => electron.ipcRenderer.invoke("proto:sendRequest", msgId, data),
    generateDoc: () => electron.ipcRenderer.invoke("proto:generateDoc")
  },
  testData: {
    generateAccounts: (count) => electron.ipcRenderer.invoke("testData:generateAccounts", count),
    generateRoles: (count) => electron.ipcRenderer.invoke("testData:generateRoles", count),
    generateItems: (count) => electron.ipcRenderer.invoke("testData:generateItems", count)
  },
  profiles: {
    list: () => electron.ipcRenderer.invoke("profiles:list"),
    get: (id) => electron.ipcRenderer.invoke("profiles:get", id),
    create: (profile) => electron.ipcRenderer.invoke("profiles:create", profile),
    update: (id, profile) => electron.ipcRenderer.invoke("profiles:update", id, profile),
    delete: (id) => electron.ipcRenderer.invoke("profiles:delete", id),
    setActive: (id) => electron.ipcRenderer.invoke("profiles:setActive", id),
    getActive: () => electron.ipcRenderer.invoke("profiles:getActive")
  },
  app: {
    getVersion: () => electron.ipcRenderer.invoke("app:getVersion"),
    getPaths: () => electron.ipcRenderer.invoke("app:getPaths"),
    openFileDialog: (options) => electron.ipcRenderer.invoke("app:openFileDialog", options),
    saveFileDialog: (options) => electron.ipcRenderer.invoke("app:saveFileDialog", options)
  }
};
electron.contextBridge.exposeInMainWorld("electronAPI", electronAPI);
