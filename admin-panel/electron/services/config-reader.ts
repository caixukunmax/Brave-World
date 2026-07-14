import fs from 'fs';
import path from 'path';
import type { ConfigTableInfo, MapRegistryEntry, BuildingConfig, XlsxSyncStatus } from '../types';

export class ConfigReader {
  private dataDir = 'servercsharp/data';
  private tablesDir = 'servercsharp/data/tables';

  private resolveTablesDir(): string {
    const candidates = [
      path.resolve(this.tablesDir),
      path.resolve(this.dataDir, 'tables'),
    ];
    for (const dir of candidates) {
      if (fs.existsSync(dir)) return dir;
    }
    return path.resolve(this.tablesDir);
  }

  private resolveDataDir(): string {
    const candidates = [
      path.resolve(this.dataDir),
      path.resolve('servercsharp', 'data'),
    ];
    for (const dir of candidates) {
      if (fs.existsSync(dir)) return dir;
    }
    return path.resolve(this.dataDir);
  }

  async listTables(): Promise<ConfigTableInfo[]> {
    const tablesDir = this.resolveTablesDir();
    if (!fs.existsSync(tablesDir)) return [];

    const files = fs.readdirSync(tablesDir).filter(f => f.endsWith('.json'));
    return files.map(f => {
      const filePath = path.join(tablesDir, f);
      const stat = fs.statSync(filePath);
      const raw = fs.readFileSync(filePath, 'utf-8');
      let rowCount = 0;
      try {
        const data = JSON.parse(raw);
        rowCount = Array.isArray(data) ? data.length : 0;
      } catch { /* ignore parse errors */ }

      return {
        name: f.replace('.json', ''),
        filePath,
        rowCount,
        lastModified: stat.mtime.toISOString(),
      };
    });
  }

  async readTable(name: string): Promise<unknown> {
    const tablesDir = this.resolveTablesDir();
    const filePath = path.join(tablesDir, `${name}.json`);
    if (!fs.existsSync(filePath)) {
      throw new Error(`Table ${name} not found at ${filePath}`);
    }
    const raw = fs.readFileSync(filePath, 'utf-8');
    return JSON.parse(raw);
  }

  async readAppSettings(): Promise<unknown> {
    const candidates = [
      path.resolve('servercsharp/src/GameServer/appsettings.json'),
      path.resolve('servercsharp/src/GameServer/appsettings.Example.json'),
    ];
    for (const filePath of candidates) {
      if (fs.existsSync(filePath)) {
        return JSON.parse(fs.readFileSync(filePath, 'utf-8'));
      }
    }
    throw new Error('appsettings.json not found');
  }

  async readMapRegistry(): Promise<MapRegistryEntry[]> {
    const filePath = path.join(this.resolveDataDir(), 'map_registry.json');
    if (!fs.existsSync(filePath)) return [];
    return JSON.parse(fs.readFileSync(filePath, 'utf-8'));
  }

  async readBuildings(): Promise<BuildingConfig> {
    const filePath = path.join(this.resolveDataDir(), 'buildings.json');
    if (!fs.existsSync(filePath)) return {};
    return JSON.parse(fs.readFileSync(filePath, 'utf-8'));
  }

  async getXlsxSyncStatus(): Promise<XlsxSyncStatus[]> {
    const tables = await this.listTables();
    return tables.map(t => ({
      tableName: t.name,
      jsonModified: t.lastModified,
      xlsxModified: null,
      inSync: true,
    }));
  }
}