import fs from 'fs';
import path from 'path';

export interface ConfigTableInfo {
  name: string;
  filePath: string;
  rowCount: number;
  lastModified: string;
}

export class ConfigReader {
  private tablesDir: string;

  constructor(dataDir: string) {
    this.tablesDir = path.join(dataDir, 'tables');
  }

  async listTables(): Promise<ConfigTableInfo[]> {
    if (!fs.existsSync(this.tablesDir)) return [];
    const files = fs.readdirSync(this.tablesDir).filter(f => f.endsWith('.json'));
    return files.map(f => {
      const filePath = path.join(this.tablesDir, f);
      const stat = fs.statSync(filePath);
      let rowCount = 0;
      try {
        const data = JSON.parse(fs.readFileSync(filePath, 'utf-8'));
        rowCount = Array.isArray(data) ? data.length : 0;
      } catch { /* ignore */ }
      return { name: f.replace('.json', ''), filePath, rowCount, lastModified: stat.mtime.toISOString() };
    });
  }

  async readTable(name: string): Promise<unknown> {
    const filePath = path.join(this.tablesDir, `${name}.json`);
    if (!fs.existsSync(filePath)) throw new Error(`Table ${name} not found`);
    return JSON.parse(fs.readFileSync(filePath, 'utf-8'));
  }

  async readMapRegistry(): Promise<unknown> {
    const filePath = path.join(this.tablesDir, '..', 'map_registry.json');
    if (!fs.existsSync(filePath)) return [];
    return JSON.parse(fs.readFileSync(filePath, 'utf-8'));
  }

  async readBuildings(): Promise<unknown> {
    const filePath = path.join(this.tablesDir, '..', 'buildings.json');
    if (!fs.existsSync(filePath)) return {};
    return JSON.parse(fs.readFileSync(filePath, 'utf-8'));
  }

  async readAppSettings(): Promise<unknown> {
    const candidates = [
      path.join(this.tablesDir, '..', '..', 'src', 'GameServer', 'appsettings.json'),
      path.join(this.tablesDir, '..', '..', 'src', 'GameServer', 'appsettings.Example.json'),
    ];
    for (const filePath of candidates) {
      if (fs.existsSync(filePath)) {
        return JSON.parse(fs.readFileSync(filePath, 'utf-8'));
      }
    }
    throw new Error('appsettings.json not found');
  }
}