import fs from 'fs';
import path from 'path';

export class ConfigWriter {
  private tablesDir = 'servercsharp/data/tables';
  private dataDir = 'servercsharp/data';

  private resolveTablesDir(): string {
    const dir = path.resolve(this.tablesDir);
    return fs.existsSync(dir) ? dir : path.resolve(this.dataDir, 'tables');
  }

  private resetDataDir(): string {
    return path.resolve(this.dataDir);
  }

  private createBackup(filePath: string): string {
    const timestamp = new Date().toISOString().replace(/[:.]/g, '_').replace('T', '_').slice(0, 19);
    const backupPath = filePath + `.${timestamp}.bak`;
    fs.copyFileSync(filePath, backupPath);

    const dir = path.dirname(filePath);
    const baseName = path.basename(filePath);
    const backups = fs.readdirSync(dir)
      .filter(f => f.startsWith(baseName) && f.endsWith('.bak'))
      .sort()
      .reverse();

    const maxBackups = 5;
    for (let i = maxBackups; i < backups.length; i++) {
      fs.unlinkSync(path.join(dir, backups[i]));
    }

    return backupPath;
  }

  async writeTable(name: string, data: unknown): Promise<void> {
    const tablesDir = this.resolveTablesDir();
    const filePath = path.join(tablesDir, `${name}.json`);
    if (fs.existsSync(filePath)) {
      this.createBackup(filePath);
    }
    const content = JSON.stringify(data, null, 2);
    fs.writeFileSync(filePath, content, 'utf-8');
  }

  async rollbackTable(name: string): Promise<void> {
    const tablesDir = this.resolveTablesDir();
    const filePath = path.join(tablesDir, `${name}.json`);
    const dir = path.dirname(filePath);
    const baseName = path.basename(filePath);
    const backups = fs.readdirSync(dir)
      .filter(f => f.startsWith(baseName) && f.endsWith('.bak'))
      .sort()
      .reverse();

    if (backups.length === 0) {
      throw new Error(`No backups found for ${name}`);
    }

    const latestBackup = path.join(dir, backups[0]);
    fs.copyFileSync(latestBackup, filePath);
  }

  async writeAppSettings(data: unknown): Promise<void> {
    const filePath = path.resolve('servercsharp/src/GameServer/appsettings.json');
    if (fs.existsSync(filePath)) {
      this.createBackup(filePath);
    }
    fs.writeFileSync(filePath, JSON.stringify(data, null, 2), 'utf-8');
  }

  async writeMapRegistry(data: unknown): Promise<void> {
    const filePath = path.join(this.resetDataDir(), 'map_registry.json');
    if (fs.existsSync(filePath)) {
      this.createBackup(filePath);
    }
    fs.writeFileSync(filePath, JSON.stringify(data, null, 2), 'utf-8');
  }

  async writeBuildings(data: unknown): Promise<void> {
    const filePath = path.join(this.resetDataDir(), 'buildings.json');
    if (fs.existsSync(filePath)) {
      this.createBackup(filePath);
    }
    fs.writeFileSync(filePath, JSON.stringify(data, null, 2), 'utf-8');
  }
}