export const APP_NAME = 'TSLua2 Admin';
export const APP_VERSION = '1.0.0';

export const CONFIG_TABLES = [
  'common_tbaccountconfig',
  'common_tbai',
  'common_tbbuff',
  'common_tbchestconfig',
  'common_tbcombatnarration',
  'common_tbdropgroup',
  'common_tbgmcommanddesc',
  'common_tbglobalconfig',
  'common_tbjob',
  'common_tbmapconfig',
  'common_tbmapmonster',
  'common_tbmapnpc',
  'common_tbmonster',
  'common_tbnpc',
  'common_tbplayerattr',
  'common_tbroleinitconfig',
  'common_tbskill',
  'common_tbterrainconfig',
  'item_tbitem',
  'tbserverconfig',
] as const;

export const CONFIG_TABLE_LABELS: Record<string, string> = {
  common_tbaccountconfig: 'Account Config',
  common_tbai: 'AI Config',
  common_tbbuff: 'Buffs',
  common_tbchestconfig: 'Chest Config',
  common_tbcombatnarration: 'Combat Narration',
  common_tbdropgroup: 'Drop Groups',
  common_tbgmcommanddesc: 'GM Command Descriptions',
  common_tbglobalconfig: 'Global Config',
  common_tbjob: 'Jobs',
  common_tbmapconfig: 'Map Config',
  common_tbmapmonster: 'Map Monsters',
  common_tbmapnpc: 'Map NPCs',
  common_tbmonster: 'Monsters',
  common_tbnpc: 'NPCs',
  common_tbplayerattr: 'Player Attributes',
  common_tbroleinitconfig: 'Role Init Config',
  common_tbskill: 'Skills',
  common_tbterrainconfig: 'Terrain Config',
  item_tbitem: 'Items',
  tbserverconfig: 'Server Config',
};

export const LOG_LEVELS = ['FTL', 'ERR', 'WRN', 'INF', 'DBG'] as const;

export const LOG_LEVEL_COLORS: Record<string, string> = {
  FTL: 'red',
  ERR: 'red',
  WRN: 'yellow',
  INF: 'gray',
  DBG: 'dimmed',
};