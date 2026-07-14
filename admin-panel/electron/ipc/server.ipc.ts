import { ipcMain } from 'electron';
import { ServerManager } from '../services/server-manager';
import { ProfileManager } from '../services/profile-manager';

const serverManager = new ServerManager();
const profileManager = new ProfileManager();

export function registerServerIpc() {
  ipcMain.handle('server:start', async (_, profileId: string) => {
    const profile = await profileManager.get(profileId);
    if (!profile) throw new Error(`Profile ${profileId} not found`);
    return serverManager.start(profile);
  });

  ipcMain.handle('server:stop', async () => {
    return serverManager.stop();
  });

  ipcMain.handle('server:restart', async (_, profileId: string) => {
    const profile = await profileManager.get(profileId);
    if (!profile) throw new Error(`Profile ${profileId} not found`);
    return serverManager.restart(profile);
  });

  ipcMain.handle('server:getStatus', () => {
    return serverManager.getStatus();
  });

  ipcMain.handle('server:sendCommand', (_, cmd: string) => {
    return serverManager.sendCommand(cmd);
  });
}