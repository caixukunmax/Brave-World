/// <reference types="vite/client" />

interface Window {
  electronAPI: {
    server: {
      start: (profileId: string) => Promise<{ pid: number }>;
      stop: () => Promise<void>;
      restart: (profileId: string) => Promise<void>;
      getStatus: () => Promise<{ status: string; pid: number | null; startTime: string | null; uptime: number; cpu: number; memory: number; error: string | null }>;
      sendCommand: (cmd: string) => Promise<void>;
      onOutput: (callback: (line: string) => void) => () => void;
      onStatusChange: (callback: (status: unknown) => void) => () => void;
    };
    build: {
      tables: () => Promise<{ success: boolean; exitCode: number; output: string; errors: string[] }>;
      proto: () => Promise<{ success: boolean; exitCode: number; output: string; errors: string[] }>;
      server: () => Promise<{ success: boolean; exitCode: number; output: string; errors: string[] }>;
      all: () => Promise<{ success: boolean; exitCode: number; output: string; errors: string[] }>;
      onOutput: (callback: (line: string) => void) => () => void;
    };
    config: {
      listTables: () => Promise<unknown[]>;
      readTable: (name: string) => Promise<unknown>;
      writeTable: (name: string, data: unknown) => Promise<void>;
      diffTable: (name: string) => Promise<unknown[]>;
      rollbackTable: (name: string) => Promise<void>;
      validateTable: (name: string) => Promise<unknown[]>;
      readAppSettings: () => Promise<unknown>;
      writeAppSettings: (data: unknown) => Promise<void>;
      readMapRegistry: () => Promise<unknown>;
      writeMapRegistry: (data: unknown) => Promise<void>;
      readBuildings: () => Promise<unknown>;
      writeBuildings: (data: unknown) => Promise<void>;
      getXlsxSyncStatus: () => Promise<unknown[]>;
    };
    database: {
      listAccounts: (filter?: unknown) => Promise<unknown[]>;
      getAccount: (id: number) => Promise<unknown>;
      createAccount: (data: unknown) => Promise<unknown>;
      updateAccount: (id: number, data: unknown) => Promise<void>;
      banAccount: (id: number) => Promise<void>;
      unbanAccount: (id: number) => Promise<void>;
      resetPassword: (id: number, newPassword: string) => Promise<void>;
      listRoles: (filter?: unknown) => Promise<unknown[]>;
      getRole: (id: number) => Promise<unknown>;
      updateRole: (id: number, data: unknown) => Promise<void>;
      getInventory: (roleId: number) => Promise<unknown[]>;
      addItem: (roleId: number, itemId: number, count: number) => Promise<void>;
      removeItem: (roleId: number, itemId: number, count: number) => Promise<void>;
      listChests: () => Promise<unknown[]>;
      createChest: (data: unknown) => Promise<unknown>;
      deleteChest: (id: number) => Promise<void>;
      getStats: () => Promise<unknown>;
    };
    logs: {
      startStreaming: (filter?: unknown) => Promise<void>;
      stopStreaming: () => Promise<void>;
      getHistory: (lines?: number) => Promise<unknown[]>;
      onLogLine: (callback: (line: unknown) => void) => () => void;
    };
    monitoring: {
      getPerformance: () => Promise<unknown>;
      getOnlinePlayers: () => Promise<unknown[]>;
      getMapEntities: () => Promise<unknown[]>;
      kickPlayer: (accountId: number) => Promise<void>;
      onMetricsUpdate: (callback: (metrics: unknown) => void) => () => void;
    };
    gm: {
      sendCommand: (command: string, targetPlayerId?: number) => Promise<unknown>;
      broadcast: (message: string) => Promise<void>;
      listSchedules: () => Promise<unknown[]>;
      createSchedule: (task: unknown) => Promise<unknown>;
      updateSchedule: (id: string, task: unknown) => Promise<void>;
      deleteSchedule: (id: string) => Promise<void>;
    };
    maps: {
      listMaps: () => Promise<unknown[]>;
      readMap: (mapName: string) => Promise<unknown>;
      writeMap: (mapName: string, data: unknown) => Promise<void>;
      generateMap: (params: unknown) => Promise<unknown>;
      getPreviewImage: (mapName: string) => Promise<string>;
      readSpawnPoints: (mapName: string) => Promise<unknown[]>;
      writeSpawnPoints: (mapName: string, points: unknown) => Promise<void>;
      readNpcPlacements: (mapName: string) => Promise<unknown[]>;
      writeNpcPlacements: (mapName: string, npcs: unknown) => Promise<void>;
    };
    proto: {
      listMessages: () => Promise<unknown[]>;
      getMessageSchema: (msgName: string) => Promise<unknown>;
      sendRequest: (msgId: number, data: Uint8Array) => Promise<unknown>;
      generateDoc: () => Promise<string>;
    };
    testData: {
      generateAccounts: (count: number) => Promise<void>;
      generateRoles: (count: number) => Promise<void>;
      generateItems: (count: number) => Promise<void>;
    };
    profiles: {
      list: () => Promise<unknown[]>;
      get: (id: string) => Promise<unknown>;
      create: (profile: unknown) => Promise<unknown>;
      update: (id: string, profile: unknown) => Promise<void>;
      delete: (id: string) => Promise<void>;
      setActive: (id: string) => Promise<void>;
      getActive: () => Promise<unknown | null>;
    };
    app: {
      getVersion: () => Promise<string>;
      getPaths: () => Promise<unknown>;
      openFileDialog: (options: unknown) => Promise<string | null>;
      saveFileDialog: (options: unknown) => Promise<string | null>;
    };
  };
}