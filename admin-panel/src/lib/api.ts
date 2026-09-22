const BASE = '/api';

async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  const options: RequestInit = {
    method,
    headers: { 'Content-Type': 'application/json' },
  };
  if (body) options.body = JSON.stringify(body);
  const res = await fetch(`${BASE}${url}`, options);
  if (!res.ok) {
    const err = await res.json().catch(() => ({ error: res.statusText }));
    throw new Error(err.error || res.statusText);
  }
  return res.json();
}

export const api = {
  // Server
  server: {
    statuses: () => request<any[]>('GET', '/server/statuses'),
    status: (profileId?: string) => request<any>('GET', `/server/status${profileId ? `?profileId=${profileId}` : ''}`),
    output: (profileId?: string) => request<{ lines: string[] }>('GET', `/server/output${profileId ? `?profileId=${profileId}` : ''}`),
    start: (profileId?: string) => request<any>('POST', '/server/start', { profileId }),
    stop: (profileId?: string) => request<any>('POST', '/server/stop', { profileId }),
    restart: (profileId?: string) => request<any>('POST', '/server/restart', { profileId }),
    command: (cmd: string, profileId?: string) => request<any>('POST', '/server/command', { command: cmd, profileId }),
  },
  // Build
  build: {
    run: (type: string) => request<any>('POST', `/build/${type}`),
    restart: (profileId?: string) => request<any>('POST', '/build/restart', { profileId }),
  },
  // Config
  config: {
    listTables: () => request<any[]>('GET', '/config/tables'),
    readTable: (name: string) => request<any>('GET', `/config/tables/${name}`),
    writeTable: (name: string, data: any) => request<any>('PUT', `/config/tables/${name}`, data),
    diffTable: (name: string) => request<any>('GET', `/config/tables/${name}/diff`),
    rollbackTable: (name: string) => request<any>('POST', `/config/tables/${name}/rollback`),
    validateTable: (name: string) => request<any>('GET', `/config/tables/${name}/validate`),
    mapRegistry: () => request<any>('GET', '/config/map-registry'),
    updateMapRegistry: (data: any) => request<any>('PUT', '/config/map-registry', data),
    buildings: () => request<any>('GET', '/config/buildings'),
    updateBuildings: (data: any) => request<any>('PUT', '/config/buildings', data),
    appSettings: () => request<any>('GET', '/config/appsettings'),
    updateAppSettings: (data: any) => request<any>('PUT', '/config/appsettings', data),
  },
  // Database
  db: {
    accounts: (search?: string) => request<any[]>('GET', `/db/accounts${search ? `?search=${search}` : ''}`),
    account: (id: number) => request<any>('GET', `/db/accounts/${id}`),
    createAccount: (data: any) => request<any>('POST', '/db/accounts', data),
    updateAccount: (id: number, data: any) => request<any>('PUT', `/db/accounts/${id}`, data),
    banAccount: (id: number) => request<any>('POST', `/db/accounts/${id}/ban`),
    unbanAccount: (id: number) => request<any>('POST', `/db/accounts/${id}/unban`),
    resetPassword: (id: number, password: string) => request<any>('POST', `/db/accounts/${id}/reset-password`, { password }),
    roles: (search?: string) => request<any[]>('GET', `/db/roles${search ? `?search=${search}` : ''}`),
    role: (id: number) => request<any>('GET', `/db/roles/${id}`),
    updateRole: (id: number, data: any) => request<any>('PUT', `/db/roles/${id}`, data),
    inventory: (roleId: number) => request<any[]>('GET', `/db/inventory/${roleId}`),
    addItem: (roleId: number, itemId: number, count: number) => request<any>('POST', `/db/inventory/${roleId}/add`, { itemId, count }),
    removeItem: (roleId: number, itemId: number, count: number) => request<any>('POST', `/db/inventory/${roleId}/remove`, { itemId, count }),
    chests: () => request<any[]>('GET', '/db/chests'),
    createChest: (data: any) => request<any>('POST', '/db/chests', data),
    deleteChest: (id: number) => request<any>('DELETE', `/db/chests/${id}`),
    stats: () => request<any>('GET', '/db/stats'),
  },
  // Monitoring
  monitoring: {
    status: () => request<any>('GET', '/monitoring/status'),
    players: () => request<any[]>('GET', '/monitoring/players'),
    maps: () => request<any[]>('GET', '/monitoring/maps'),
  },
  // GM
  gm: {
    command: (cmd: string, targetPlayerId?: number) => request<any>('POST', '/gm/command', { command: cmd, targetPlayerId }),
    broadcast: (message: string) => request<any>('POST', '/gm/broadcast', { message }),
    kick: (accountId: number) => request<any>('POST', '/gm/kick', { accountId }),
  },
  // Tasks
  tasks: {
    list: () => request<any[]>('GET', '/tasks'),
    create: (data: any) => request<any>('POST', '/tasks', data),
    update: (id: string, data: any) => request<any>('PUT', `/tasks/${id}`, data),
    delete: (id: string) => request<any>('DELETE', `/tasks/${id}`),
  },
  // Maps
  maps: {
    list: () => request<any[]>('GET', '/maps'),
    read: (name: string) => request<any>('GET', `/maps/${name}`),
    detail: (name: string) => request<any>('GET', `/maps/${name}/detail`),
    write: (name: string, data: any) => request<any>('PUT', `/maps/${name}`, data),
    delete: (name: string) => request<any>('DELETE', `/maps/${name}`),
  },
  // Profiles
  profiles: {
    list: () => request<any[]>('GET', '/profiles'),
    get: (id: string) => request<any>('GET', `/profiles/${id}`),
    create: (data: any) => request<any>('POST', '/profiles', data),
    update: (id: string, data: any) => request<any>('PUT', `/profiles/${id}`, data),
    delete: (id: string) => request<any>('DELETE', `/profiles/${id}`),
  },
};