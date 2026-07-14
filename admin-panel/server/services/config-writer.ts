import fs from 'fs';
import path from 'path';

export class ConfigWriter {
  private tablesDir: string;

  constructor(dataDir: string) {
    this.tablesDir = path.join(dataDir, 'tables');
  }

  private createBackup(filePath: string): string {
    const ts = new Date().toISOString().replace(/[:.]/g, '_').replace('T', '_').slice(0, 19);
    const backupPath = filePath + `.${ts}.bak`;
    fs.copyFileSync(filePath, backupPath);

    const dir = path.dirname(filePath);
    const base = path.basename(filePath);
    const backups = fs.readdirSync(dir)
      .filter(f => f.startsWith(base) && f.endsWith('.bak'))
      .sort().reverse();

    for (let i = 5; i < backups.length; i++) {
      fs.unlinkSync(path.join(dir, backups[i]));
    }
    return backupPath;
  }

  async writeTable(name: string, data: unknown): Promise<void> {
    const filePath = path.join(this.tablesDir, `${name}.json`);
    if (fs.existsSync(filePath)) this.createBackup(filePath);
    fs.writeFileSync(filePath, JSON.stringify(data, null, 2), 'utf-8');
  }

  async rollbackTable(name: string): Promise<void> {
    const filePath = path.join(this.tablesDir, `${name}.json`);
    const dir = path.dirname(filePath);
    const base = path.basename(filePath);
    const backups = fs.readdirSync(dir)
      .filter(f => f.startsWith(base) && f.endsWith('.bak'))
      .sort().reverse();

    if (backups.length === 0) throw new Error(`No backups found for ${name}`);
    fs.copyFileSync(path.join(dir, backups[0]), filePath);
  }

  async writeAppSettings(data: unknown): Promise<void> {
    const filePath = path.join(this.tablesDir, '..', '..', 'src', 'GameServer', 'appsettings.json');
    if (fs.existsSync(filePath)) this.createBackup(filePath);
    fs.writeFileSync(filePath, JSON.stringify(data, null, 2), 'utf-8');
  }

  async writeMapRegistry(data: unknown): Promise<void> {
    const filePath = path.join(this.tablesDir, '..', 'map_registry.json');
    if (fs.existsSync(filePath)) this.createBackup(filePath);
    fs.writeFileSync(filePath, JSON.stringify(data, null, 2), 'utf-8');
  }

  async writeBuildings(data: unknown): Promise<void> {
    const filePath = path.join(this.tablesDir, '..', 'buildings.json');
    if (fs.existsSync(filePath)) this.createBackup(filePath);
    fs.writeFileSync(filePath, JSON.stringify(data, null, 2), 'utf-8');
  }
}