import { contextBridge, ipcRenderer } from 'electron';

const electronAPI = {
  server: {
    start: (profileId: string) => ipcRenderer.invoke('server:start', profileId),
    stop: () => ipcRenderer.invoke('server:stop'),
    restart: (profileId: string) => ipcRenderer.invoke('server:restart', profileId),
    getStatus: () => ipcRenderer.invoke('server:getStatus'),
    sendCommand: (cmd: string) => ipcRenderer.invoke('server:sendCommand', cmd),
    onOutput: (callback: (line: string) => void) => {
      const handler = (_: unknown, line: string) => callback(line);
      ipcRenderer.on('server:output', handler);
      return () => ipcRenderer.removeListener('server:output', handler);
    },
    onStatusChange: (callback: (status: unknown) => void) => {
      const handler = (_: unknown, status: unknown) => callback(status);
      ipcRenderer.on('server:statusChange', handler);
      return () => ipcRenderer.removeListener('server:statusChange', handler);
    },
  },

  build: {
    tables: () => ipcRenderer.invoke('build:tables'),
    proto: () => ipcRenderer.invoke('build:proto'),
    server: () => ipcRenderer.invoke('build:server'),
    all: () => ipcRenderer.invoke('build:all'),
    onOutput: (callback: (line: string) => void) => {
      const handler = (_: unknown, line: string) => callback(line);
      ipcRenderer.on('build:output', handler);
      return () => ipcRenderer.removeListener('build:output', handler);
    },
  },

  config: {
    listTables: () => ipcRenderer.invoke('config:listTables'),
    readTable: (name: string) => ipcRenderer.invoke('config:readTable', name),
    writeTable: (name: string, data: unknown) => ipcRenderer.invoke('config:writeTable', name, data),
    diffTable: (name: string) => ipcRenderer.invoke('config:diffTable', name),
    rollbackTable: (name: string) => ipcRenderer.invoke('config:rollbackTable', name),
    validateTable: (name: string) => ipcRenderer.invoke('config:validateTable', name),
    readAppSettings: () => ipcRenderer.invoke('config:readAppSettings'),
    writeAppSettings: (data: unknown) => ipcRenderer.invoke('config:writeAppSettings', data),
    readMapRegistry: () => ipcRenderer.invoke('config:readMapRegistry'),
    writeMapRegistry: (data: unknown) => ipcRenderer.invoke('config:writeMapRegistry', data),
    readBuildings: () => ipcRenderer.invoke('config:readBuildings'),
    writeBuildings: (data: unknown) => ipcRenderer.invoke('config:writeBuildings', data),
    getXlsxSyncStatus: () => ipcRenderer.invoke('config:getXlsxSyncStatus'),
  },

  database: {
    listAccounts: (filter?: unknown) => ipcRenderer.invoke('database:listAccounts', filter),
    getAccount: (id: number) => ipcRenderer.invoke('database:getAccount', id),
    createAccount: (data: unknown) => ipcRenderer.invoke('database:createAccount', data),
    updateAccount: (id: number, data: unknown) => ipcRenderer.invoke('database:updateAccount', id, data),
    banAccount: (id: number) => ipcRenderer.invoke('database:banAccount', id),
    unbanAccount: (id: number) => ipcRenderer.invoke('database:unbanAccount', id),
    resetPassword: (id: number, newPassword: string) => ipcRenderer.invoke('database:resetPassword', id, newPassword),
    listRoles: (filter?: unknown) => ipcRenderer.invoke('database:listRoles', filter),
    getRole: (id: number) => ipcRenderer.invoke('database:getRole', id),
    updateRole: (id: number, data: unknown) => ipcRenderer.invoke('database:updateRole', id, data),
    getInventory: (roleId: number) => ipcRenderer.invoke('database:getInventory', roleId),
    addItem: (roleId: number, itemId: number, count: number) => ipcRenderer.invoke('database:addItem', roleId, itemId, count),
    removeItem: (roleId: number, itemId: number, count: number) => ipcRenderer.invoke('database:removeItem', roleId, itemId, count),
    listChests: () => ipcRenderer.invoke('database:listChests'),
    createChest: (data: unknown) => ipcRenderer.invoke('database:createChest', data),
    deleteChest: (id: number) => ipcRenderer.invoke('database:deleteChest', id),
    getStats: () => ipcRenderer.invoke('database:getStats'),
  },

  logs: {
    startStreaming: (filter?: unknown) => ipcRenderer.invoke('logs:startStreaming', filter),
    stopStreaming: () => ipcRenderer.invoke('logs:stopStreaming'),
    getHistory: (lines?: number) => ipcRenderer.invoke('logs:getHistory', lines),
    onLogLine: (callback: (line: unknown) => void) => {
      const handler = (_: unknown, line: unknown) => callback(line);
      ipcRenderer.on('logs:newLine', handler);
      return () => ipcRenderer.removeListener('logs:newLine', handler);
    },
  },

  monitoring: {
    getPerformance: () => ipcRenderer.invoke('monitoring:getPerformance'),
    getOnlinePlayers: () => ipcRenderer.invoke('monitoring:getOnlinePlayers'),
    getMapEntities: () => ipcRenderer.invoke('monitoring:getMapEntities'),
    kickPlayer: (accountId: number) => ipcRenderer.invoke('monitoring:kickPlayer', accountId),
    onMetricsUpdate: (callback: (metrics: unknown) => void) => {
      const handler = (_: unknown, metrics: unknown) => callback(metrics);
      ipcRenderer.on('monitoring:metricsUpdate', handler);
      return () => ipcRenderer.removeListener('monitoring:metricsUpdate', handler);
    },
  },

  gm: {
    sendCommand: (command: string, targetPlayerId?: number) => ipcRenderer.invoke('gm:sendCommand', command, targetPlayerId),
    broadcast: (message: string) => ipcRenderer.invoke('gm:broadcast', message),
    listSchedules: () => ipcRenderer.invoke('gm:listSchedules'),
    createSchedule: (task: unknown) => ipcRenderer.invoke('gm:createSchedule', task),
    updateSchedule: (id: string, task: unknown) => ipcRenderer.invoke('gm:updateSchedule', id, task),
    deleteSchedule: (id: string) => ipcRenderer.invoke('gm:deleteSchedule', id),
  },

  maps: {
    listMaps: () => ipcRenderer.invoke('maps:listMaps'),
    readMap: (mapName: string) => ipcRenderer.invoke('maps:readMap', mapName),
    writeMap: (mapName: string, data: unknown) => ipcRenderer.invoke('maps:writeMap', mapName, data),
    generateMap: (params: unknown) => ipcRenderer.invoke('maps:generateMap', params),
    getPreviewImage: (mapName: string) => ipcRenderer.invoke('maps:getPreviewImage', mapName),
    readSpawnPoints: (mapName: string) => ipcRenderer.invoke('maps:readSpawnPoints', mapName),
    writeSpawnPoints: (mapName: string, points: unknown) => ipcRenderer.invoke('maps:writeSpawnPoints', mapName, points),
    readNpcPlacements: (mapName: string) => ipcRenderer.invoke('maps:readNpcPlacements', mapName),
    writeNpcPlacements: (mapName: string, npcs: unknown) => ipcRenderer.invoke('maps:writeNpcPlacements', mapName, npcs),
  },

  proto: {
    listMessages: () => ipcRenderer.invoke('proto:listMessages'),
    getMessageSchema: (msgName: string) => ipcRenderer.invoke('proto:getMessageSchema', msgName),
    sendRequest: (msgId: number, data: Uint8Array) => ipcRenderer.invoke('proto:sendRequest', msgId, data),
    generateDoc: () => ipcRenderer.invoke('proto:generateDoc'),
  },

  testData: {
    generateAccounts: (count: number) => ipcRenderer.invoke('testData:generateAccounts', count),
    generateRoles: (count: number) => ipcRenderer.invoke('testData:generateRoles', count),
    generateItems: (count: number) => ipcRenderer.invoke('testData:generateItems', count),
  },

  profiles: {
    list: () => ipcRenderer.invoke('profiles:list'),
    get: (id: string) => ipcRenderer.invoke('profiles:get', id),
    create: (profile: unknown) => ipcRenderer.invoke('profiles:create', profile),
    update: (id: string, profile: unknown) => ipcRenderer.invoke('profiles:update', id, profile),
    delete: (id: string) => ipcRenderer.invoke('profiles:delete', id),
    setActive: (id: string) => ipcRenderer.invoke('profiles:setActive', id),
    getActive: () => ipcRenderer.invoke('profiles:getActive'),
  },

  app: {
    getVersion: () => ipcRenderer.invoke('app:getVersion'),
    getPaths: () => ipcRenderer.invoke('app:getPaths'),
    openFileDialog: (options: unknown) => ipcRenderer.invoke('app:openFileDialog', options),
    saveFileDialog: (options: unknown) => ipcRenderer.invoke('app:saveFileDialog', options),
  },
};

contextBridge.exposeInMainWorld('electronAPI', electronAPI);

export type ElectronAPI = typeof electronAPI;