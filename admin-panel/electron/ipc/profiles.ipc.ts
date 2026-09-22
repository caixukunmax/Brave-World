import { ipcMain } from 'electron';
import { ProfileManager } from '../services/profile-manager';

const profileManager = new ProfileManager();

export function registerProfileIpc() {
  ipcMain.handle('profiles:list', () => profileManager.list());
  ipcMain.handle('profiles:get', (_, id: string) => profileManager.get(id));
  ipcMain.handle('profiles:create', (_, profile) => profileManager.create(profile));
  ipcMain.handle('profiles:update', (_, id, profile) => profileManager.update(id, profile));
  ipcMain.handle('profiles:delete', (_, id: string) => profileManager.delete(id));
  ipcMain.handle('profiles:setActive', (_, id: string) => profileManager.setActive(id));
  ipcMain.handle('profiles:getActive', () => profileManager.getActive());
}