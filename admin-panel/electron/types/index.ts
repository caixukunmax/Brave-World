export interface ServerProfile {
  id: string;
  name: string;
  repoRoot: string;
  serverExePath: string;
  appSettingsPath: string;
  mongoHost: string;
  mongoPort: number;
  mongoDb: string;
  httpApiPort: number;
  gatewayPort: number;
  autoRestart: boolean;
  envVars: Record<string, string>;
}

export interface ServerStatus {
  status: 'stopped' | 'starting' | 'running' | 'stopping' | 'error';
  pid: number | null;
  startTime: string | null;
  uptime: number;
  cpu: number;
  memory: number;
  error: string | null;
}

export interface BuildResult {
  success: boolean;
  exitCode: number;
  output: string;
  errors: string[];
}

export interface LogLine {
  timestamp: string;
  level: string;
  message: string;
  raw: string;
}

export interface LogFilter {
  levels?: string[];
  search?: string;
  dateFrom?: string;
  dateTo?: string;
}

export interface ConfigTableInfo {
  name: string;
  filePath: string;
  rowCount: number;
  lastModified: string;
}

export interface DiffResult {
  kind: 'N' | 'D' | 'E' | 'A';
  path: string[];
  lhs?: unknown;
  rhs?: unknown;
  item?: DiffResult;
  index?: number;
}

export interface ValidationResult {
  field: string;
  rowId: number | string;
  message: string;
  severity: 'error' | 'warning';
}

export interface Account {
  account_id: number;
  username: string;
  status: number;
  last_server_id: number;
  last_role_name: string;
  created_at?: string;
  updated_at?: string;
}

export interface Role {
  role_id: number;
  account_id: number;
  role_name: string;
  level: number;
  exp: number;
  hp: number;
  mp: number;
  max_hp: number;
  max_mp: number;
  patk: number;
  matk: number;
  pdef: number;
  mdef: number;
  agility: number;
  current_map: string;
  grid_x: number;
  grid_y: number;
  job: string;
  learned_skills: number[];
  equipped_skills: number[];
}

export interface InventoryItem {
  role_id: number;
  item_id: number;
  count: number;
}

export interface GmChest {
  id: number;
  map_id: number;
  entity_type: number;
  chest_type_id: number;
  x: number;
  y: number;
  rewards: string;
}

export interface OnlinePlayer {
  accountId: number;
  roleId: number;
  roleName: string;
  level: number;
  job: string;
  currentMap: string;
  gridX: number;
  gridY: number;
}

export interface MapEntityCount {
  mapName: string;
  playerCount: number;
  monsterCount: number;
  npcCount: number;
}

export interface PerformanceMetrics {
  cpu: number;
  memory: number;
  uptime: number;
  onlinePlayers: number;
  gatewayPort: number;
  gameTickMs: number;
}

export interface MapRegistryEntry {
  map_name: string;
  display_name: string;
  width: number;
  height: number;
  spawn_x: number;
  spawn_y: number;
}

export interface MapData {
  bounds: { x: number; y: number; w: number; h: number };
  cells: Record<string, MapCell>;
}

export interface MapCell {
  terrain: number;
  height: number;
  custom: string;
  decoration?: number;
}

export interface BuildingConfig {
  [id: string]: {
    sizeX: number;
    sizeY: number;
    blockMovement: boolean;
    name?: string;
  };
}

export interface ScheduledTask {
  id: string;
  name: string;
  cronExpression: string;
  command: string;
  enabled: boolean;
  lastRun?: string;
  lastResult?: string;
}

export interface GmResult {
  success: boolean;
  message: string;
}

export interface DatabaseStats {
  totalAccounts: number;
  totalRoles: number;
  rolesByJob: Record<string, number>;
  rolesByLevel: Record<string, number>;
}

export interface ProtoMessageInfo {
  name: string;
  fields: ProtoField[];
}

export interface ProtoField {
  name: string;
  type: string;
  id: number;
  rule: string;
}

export interface ProtoResponse {
  msgId: number;
  data: Uint8Array;
  decoded: unknown;
}

export interface XlsxSyncStatus {
  tableName: string;
  jsonModified: string;
  xlsxModified: string | null;
  inSync: boolean;
}

export interface AppPaths {
  repoRoot: string;
  dataDir: string;
  tablesDir: string;
  mapsDir: string;
  logsDir: string;
  serverExe: string;
}