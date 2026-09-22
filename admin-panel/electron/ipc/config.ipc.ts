import { ipcMain } from 'electron';
import { ConfigReader } from '../services/config-reader';
import { ConfigWriter } from '../services/config-writer';
import { ConfigDiff } from '../services/config-diff';
import { ConfigValidator } from '../services/config-validator';

const configReader = new ConfigReader();
const configWriter = new ConfigWriter();
const configDiff = new ConfigDiff();
const configValidator = new ConfigValidator();

export function registerConfigIpc() {
  ipcMain.handle('config:listTables', () => configReader.listTables());
  ipcMain.handle('config:readTable', (_, name: string) => configReader.readTable(name));
  ipcMain.handle('config:writeTable', (_, name: string, data: unknown) => configWriter.writeTable(name, data));
  ipcMain.handle('config:diffTable', (_, name: string) => configDiff.diffTable(name));
  ipcMain.handle('config:rollbackTable', (_, name: string) => configWriter.rollbackTable(name));
  ipcMain.handle('config:validateTable', (_, name: string) => configValidator.validateTable(name));
  ipcMain.handle('config:readAppSettings', () => configReader.readAppSettings());
  ipcMain.handle('config:writeAppSettings', (_, data: unknown) => configWriter.writeAppSettings(data));
  ipcMain.handle('config:readMapRegistry', () => configReader.readMapRegistry());
  ipcMain.handle('config:writeMapRegistry', (_, data: unknown) => configWriter.writeMapRegistry(data));
  ipcMain.handle('config:readBuildings', () => configReader.readBuildings());
  ipcMain.handle('config:writeBuildings', (_, data: unknown) => configWriter.writeBuildings(data));
  ipcMain.handle('config:getXlsxSyncStatus', () => configReader.getXlsxSyncStatus());
}