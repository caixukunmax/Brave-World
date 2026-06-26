/**
 * 一次性迁移脚本：将旧 CSV/config.cfg 地图数据迁移到 v2 JSON 格式。
 * 支持旧格式：
 *   - terrain;height;custom
 *   - uid;terrain;height;custom
 * 迁移后删除原 map.csv 和 config.cfg。
 */

import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const ROOT = path.resolve(__dirname, '..', '..');
const SOURCE_DIR = path.join(ROOT, 'tables', 'datas', 'maps');

interface MapCell {
  terrain: number;
  height: number;
  custom: string;
}

interface MapBounds {
  x: number;
  y: number;
  w: number;
  h: number;
}

interface MapJson {
  version: number;
  display_name: string;
  bounds: MapBounds;
  spawn: { x: number; y: number };
  cells: Record<string, MapCell>;
}

function parseUid(uid: string): { x: number; y: number } | null {
  const parts = uid.split('_');
  if (parts.length !== 2) return null;
  const x = parseInt(parts[0], 10);
  const y = parseInt(parts[1], 10);
  if (Number.isNaN(x) || Number.isNaN(y)) return null;
  return { x, y };
}

function loadConfig(configPath: string) {
  const config: Record<string, string> = {};
  if (!fs.existsSync(configPath)) return config;

  const content = fs.readFileSync(configPath, 'utf-8');
  for (const line of content.split(/\r?\n/)) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith('#') || trimmed.startsWith('[')) continue;
    const eqIdx = trimmed.indexOf('=');
    if (eqIdx < 0) continue;
    const key = trimmed.substring(0, eqIdx).trim();
    const val = trimmed.substring(eqIdx + 1).trim();
    config[key] = val;
  }
  return config;
}

function parseCsv(csvPath: string): { cells: Record<string, MapCell>; bounds: MapBounds } | null {
  const content = fs.readFileSync(csvPath, 'utf-8');
  const lines = content.split(/\r?\n/).filter(l => {
    const t = l.trim();
    return t && !t.startsWith('#');
  });

  if (lines.length === 0) return null;

  const cells: Record<string, MapCell> = {};
  let minX = Number.MAX_SAFE_INTEGER;
  let minY = Number.MAX_SAFE_INTEGER;
  let maxX = Number.MIN_SAFE_INTEGER;
  let maxY = Number.MIN_SAFE_INTEGER;

  for (let rowIndex = 0; rowIndex < lines.length; rowIndex++) {
    const cols = lines[rowIndex].split(',');
    for (let colIndex = 0; colIndex < cols.length; colIndex++) {
      const val = cols[colIndex].trim();
      if (!val) continue;

      let terrain = 0;
      let height = 0;
      let custom = '';
      let uid = `${colIndex}_${rowIndex}`;

      if (val.includes(';')) {
        const parts = val.split(';');
        if (parts.length >= 4) {
          // uid;terrain;height;custom
          uid = parts[0];
          terrain = parseInt(parts[1], 10);
          height = parseInt(parts[2], 10);
          custom = parts[3] ?? '';
        } else if (parts.length >= 2) {
          // terrain;height;custom
          terrain = parseInt(parts[0], 10);
          height = parseInt(parts[1], 10);
          custom = parts[2] ?? '';
        }
      } else {
        // 单个 terrain 数字
        terrain = parseInt(val, 10);
      }

      // 旧格式使用 terrain=-1 表示缺失格子，新格式直接跳过
      if (terrain < 0) continue;

      const pos = parseUid(uid);
      if (pos) {
        uid = `${pos.x}_${pos.y}`;
        minX = Math.min(minX, pos.x);
        minY = Math.min(minY, pos.y);
        maxX = Math.max(maxX, pos.x);
        maxY = Math.max(maxY, pos.y);
      } else {
        minX = Math.min(minX, colIndex);
        minY = Math.min(minY, rowIndex);
        maxX = Math.max(maxX, colIndex);
        maxY = Math.max(maxY, rowIndex);
      }

      cells[uid] = { terrain, height, custom };
    }
  }

  if (Object.keys(cells).length === 0) return null;

  const bounds: MapBounds = {
    x: minX,
    y: minY,
    w: maxX - minX + 1,
    h: maxY - minY + 1,
  };

  return { cells, bounds };
}

function migrateMap(mapName: string, mapDir: string): boolean {
  const csvPath = path.join(mapDir, 'map.csv');
  const configPath = path.join(mapDir, 'config.cfg');
  const jsonPath = path.join(mapDir, 'map.json');

  if (!fs.existsSync(csvPath)) {
    console.log(`[Migrate] 跳过 ${mapName}: 没有 map.csv`);
    return false;
  }

  if (fs.existsSync(jsonPath)) {
    console.log(`[Migrate] 跳过 ${mapName}: map.json 已存在`);
    return false;
  }

  const parsed = parseCsv(csvPath);
  if (!parsed) {
    console.log(`[Migrate] 跳过 ${mapName}: CSV 解析失败`);
    return false;
  }

  const config = loadConfig(configPath);

  let bounds = parsed.bounds;
  if (config.bounds_x !== undefined) bounds.x = parseInt(config.bounds_x, 10);
  if (config.bounds_y !== undefined) bounds.y = parseInt(config.bounds_y, 10);
  if (config.bounds_w !== undefined) bounds.w = parseInt(config.bounds_w, 10);
  if (config.bounds_h !== undefined) bounds.h = parseInt(config.bounds_h, 10);

  // 兼容旧 config 的 width/height/origin_x/origin_y
  if (config.origin_x !== undefined) bounds.x = parseInt(config.origin_x, 10);
  if (config.origin_y !== undefined) bounds.y = parseInt(config.origin_y, 10);
  if (config.width !== undefined) bounds.w = parseInt(config.width, 10);
  if (config.height !== undefined) bounds.h = parseInt(config.height, 10);

  let spawnX = Math.floor(bounds.x + bounds.w / 2);
  let spawnY = Math.floor(bounds.y + bounds.h / 2);
  if (config.spawn_x !== undefined) spawnX = parseInt(config.spawn_x, 10);
  if (config.spawn_y !== undefined) spawnY = parseInt(config.spawn_y, 10);

  const displayName = config.display_name || mapName;

  const mapJson: MapJson = {
    version: 2,
    display_name: displayName,
    bounds,
    spawn: { x: spawnX, y: spawnY },
    cells: parsed.cells,
  };

  fs.writeFileSync(jsonPath, JSON.stringify(mapJson, null, 2), 'utf-8');

  // 删除旧文件
  fs.unlinkSync(csvPath);
  if (fs.existsSync(configPath)) fs.unlinkSync(configPath);
  // 删除可能存在的 .import 文件
  const importPath = csvPath + '.import';
  if (fs.existsSync(importPath)) fs.unlinkSync(importPath);

  console.log(`[Migrate] ${mapName}: ${Object.keys(parsed.cells).length} 个格子 → map.json, bounds=${JSON.stringify(bounds)}`);
  return true;
}

function main() {
  if (!fs.existsSync(SOURCE_DIR)) {
    console.log('[Migrate] 地图源目录不存在');
    return;
  }

  const entries = fs.readdirSync(SOURCE_DIR, { withFileTypes: true });
  let migrated = 0;

  for (const entry of entries) {
    if (!entry.isDirectory()) continue;
    const mapDir = path.join(SOURCE_DIR, entry.name);
    if (migrateMap(entry.name, mapDir)) migrated++;
  }

  console.log(`[Migrate] 完成，迁移 ${migrated} 张地图`);
}

main();
