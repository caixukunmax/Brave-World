import { ipcMain } from 'electron';
import { BuildRunner } from '../services/build-runner';

const buildRunner = new BuildRunner();

export function registerBuildIpc() {
  ipcMain.handle('build:tables', () => buildRunner.buildTables());
  ipcMain.handle('build:proto', () => buildRunner.buildProto());
  ipcMain.handle('build:server', () => buildRunner.buildServer());
  ipcMain.handle('build:all', () => buildRunner.buildAll());
}