import fs from 'fs';
import path from 'path';

export class MapReader {
  private dataDir: string;

  constructor(dataDir: string) {
    this.dataDir = dataDir;
  }

  async listMaps(): Promise<any[]> {
    const registryPath = path.join(this.dataDir, 'map_registry.json');
    if (!fs.existsSync(registryPath)) return [];
    return JSON.parse(fs.readFileSync(registryPath, 'utf-8'));
  }

  async readMap(mapName: string): Promise<any> {
    const mapPath = path.join(this.dataDir, 'maps', mapName, 'map.json');
    if (!fs.existsSync(mapPath)) throw new Error(`Map ${mapName} not found`);
    let raw = fs.readFileSync(mapPath, 'utf-8');
    // Strip UTF-8 BOM
    if (raw.charCodeAt(0) === 0xFEFF) raw = raw.slice(1);
    return JSON.parse(raw);
  }

  async writeMap(mapName: string, data: unknown): Promise<void> {
    const mapDir = path.join(this.dataDir, 'maps', mapName);
    if (!fs.existsSync(mapDir)) fs.mkdirSync(mapDir, { recursive: true });
    const mapPath = path.join(mapDir, 'map.json');
    if (fs.existsSync(mapPath)) {
      const ts = new Date().toISOString().replace(/[:.]/g, '_').slice(0, 19);
      fs.copyFileSync(mapPath, mapPath + `.${ts}.bak`);
    }
    fs.writeFileSync(mapPath, JSON.stringify(data, null, 2), 'utf-8');
  }

  async deleteMap(mapName: string): Promise<void> {
    // 1. 从注册表删除
    const registryPath = path.join(this.dataDir, 'map_registry.json');
    if (fs.existsSync(registryPath)) {
      const registry: any[] = JSON.parse(fs.readFileSync(registryPath, 'utf-8'));
      const filtered = registry.filter(m => m.map_name !== mapName);
      if (filtered.length === registry.length) {
        throw new Error(`Map "${mapName}" not found in registry`);
      }
      // 备份注册表
      const ts = new Date().toISOString().replace(/[:.]/g, '_').slice(0, 19);
      fs.copyFileSync(registryPath, registryPath + `.${ts}.bak`);
      fs.writeFileSync(registryPath, JSON.stringify(filtered, null, 2), 'utf-8');
    }

    // 2. 删除地图文件
    const mapDir = path.join(this.dataDir, 'maps', mapName);
    if (fs.existsSync(mapDir)) {
      fs.rmSync(mapDir, { recursive: true, force: true });
    }
  }

  async getMapDetail(mapName: string): Promise<any> {
    const registry = await this.listMaps();
    const meta = registry.find((m: any) => m.map_name === mapName);
    if (!meta) throw new Error(`Map "${mapName}" not found in registry`);

    // 读取地图数据统计
    let cellCount = 0;
    let terrainStats: Record<string, number> = {};
    let fileSize = 0;
    try {
      const mapData = await this.readMap(mapName);
      const cells = (mapData as any).cells || {};
      cellCount = Object.keys(cells).length;
      for (const key of Object.keys(cells)) {
        const t = String(cells[key].terrain || '0');
        terrainStats[t] = (terrainStats[t] || 0) + 1;
      }
      const mapPath = path.join(this.dataDir, 'maps', mapName, 'map.json');
      fileSize = fs.statSync(mapPath).size;
    } catch { /* map file may not exist */ }

    // 读取该地图的怪物刷新点
    const monsters = this.readTableMatching('common_tbmapmonster', 'map_id', (row: any) => {
      // map_id 需要匹配 map_name，通过 map_registry 找到 map_id
      const mapConfigs = this.readTable('common_tbmapconfig');
      const mapConfig = mapConfigs.find((m: any) => m.map_name === mapName);
      return mapConfig ? row.map_id === mapConfig.id : false;
    });

    // 读取该地图的 NPC
    const npcs = this.readTableMatching('common_tbmapnpc', 'map_id', (row: any) => {
      const mapConfigs = this.readTable('common_tbmapconfig');
      const mapConfig = mapConfigs.find((m: any) => m.map_name === mapName);
      return mapConfig ? row.map_id === mapConfig.id : false;
    });

    return {
      ...meta,
      cellCount,
      fileSize,
      fileSizeFormatted: fileSize > 1024 * 1024 ? `${(fileSize / 1024 / 1024).toFixed(1)} MB`
        : fileSize > 1024 ? `${(fileSize / 1024).toFixed(1)} KB` : `${fileSize} B`,
      terrainStats,
      monsters,
      npcs,
    };
  }

  private readTable(name: string): any[] {
    const filePath = path.join(this.dataDir, 'tables', `${name}.json`);
    if (!fs.existsSync(filePath)) return [];
    return JSON.parse(fs.readFileSync(filePath, 'utf-8'));
  }

  private readTableMatching(name: string, _field: string, predicate: (row: any) => boolean): any[] {
    const data = this.readTable(name);
    return data.filter(predicate);
  }
}