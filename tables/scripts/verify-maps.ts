/**
 * 地图一致性校验脚本
 * 检查 tables/datas/maps/ 作为唯一数据源，是否已与以下消费端保持一致：
 *   - clinetcsharp/maps/
 *   - servercsharp/data/maps/
 *   - servercsharp/src/GameServer/bin/Debug/net8.0/data/maps/
 *   - servercsharp/src/GameServer/bin/Release/net8.0/data/maps/
 * 同时校验每张地图的建筑 footprint 是否重叠。
 *
 * 用法：npx tsx tables/scripts/verify-maps.ts
 */

import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const ROOT = path.resolve(__dirname, '..', '..');
const PATHS_FILE = path.join(ROOT, 'paths.json');

interface MapCell {
  terrain: number;
  height: number;
  custom?: string;
  decoration?: number;
}

interface MapJson {
  version: number;
  display_name?: string;
  bounds?: { x: number; y: number; w: number; h: number };
  spawn?: { x: number; y: number };
  cells?: Record<string, MapCell>;
}

interface PathsConfig {
  maps: {
    source_dir: string;
    cs_output_dir: string;
    client_output_dir: string;
    cs_registry_path: string;
  };
}

function loadPaths() {
  const config: PathsConfig = JSON.parse(fs.readFileSync(PATHS_FILE, 'utf-8'));
  return config.maps;
}

function stripBom(content: string): string {
  return content.charCodeAt(0) === 0xFEFF ? content.slice(1) : content;
}

function readMapJson(filePath: string): MapJson | null {
  if (!fs.existsSync(filePath)) return null;
  try {
    return JSON.parse(stripBom(fs.readFileSync(filePath, 'utf-8')));
  } catch {
    return null;
  }
}

function normalizeJson(json: MapJson): string {
  return JSON.stringify(json);
}

interface BuildingConfig {
  id: number;
  size: { x: number; y: number };
}

// 与 regenerate_luoyexiang_village.js / 客户端 EntityProfileManager 保持一致的建筑占地
const BUILDING_SIZES: Record<number, { x: number; y: number }> = {
  10000: { x: 1, y: 1 }, // tree
  10001: { x: 2, y: 2 }, // house
  10002: { x: 1, y: 1 }, // rock
  10003: { x: 1, y: 1 }, // palm
  20000: { x: 1, y: 1 }, // shop
  30000: { x: 1, y: 1 }, // well
  40000: { x: 2, y: 1 }, // farm
  50000: { x: 2, y: 2 }, // tavern
};

function getDecorationSize(decorationId: number): { x: number; y: number } {
  return BUILDING_SIZES[decorationId] ?? { x: 1, y: 1 };
}

function findOverlaps(cells: Record<string, MapCell>): Array<{ anchor: string; other: string }> {
  const anchors: Array<{ key: string; x: number; y: number; sizeX: number; sizeY: number }> = [];
  for (const [key, cell] of Object.entries(cells)) {
    if (!cell.decoration) continue;
    const [x, y] = key.split('_').map(Number);
    const { x: sizeX, y: sizeY } = getDecorationSize(cell.decoration);
    anchors.push({ key, x, y, sizeX, sizeY });
  }

  const overlaps: Array<{ anchor: string; other: string }> = [];
  for (let i = 0; i < anchors.length; i++) {
    for (let j = i + 1; j < anchors.length; j++) {
      const a = anchors[i];
      const b = anchors[j];
      if (
        a.x < b.x + b.sizeX &&
        a.x + a.sizeX > b.x &&
        a.y < b.y + b.sizeY &&
        a.y + a.sizeY > b.y
      ) {
        overlaps.push({ anchor: a.key, other: b.key });
      }
    }
  }
  return overlaps;
}

function main() {
  const paths = loadPaths();
  const SOURCE_DIR = path.resolve(ROOT, paths.source_dir);
  const CS_DIR = path.resolve(ROOT, paths.cs_output_dir);
  const CLIENT_DIR = path.resolve(ROOT, paths.client_output_dir);
  const BIN_DEBUG_DIR = path.resolve(ROOT, 'servercsharp/src/GameServer/bin/Debug/net8.0/data/maps');
  const BIN_RELEASE_DIR = path.resolve(ROOT, 'servercsharp/src/GameServer/bin/Release/net8.0/data/maps');

  if (!fs.existsSync(SOURCE_DIR)) {
    console.error(`[VerifyMaps] 地图源目录不存在: ${SOURCE_DIR}`);
    process.exit(1);
  }

  let hasError = false;
  const entries = fs.readdirSync(SOURCE_DIR, { withFileTypes: true });

  for (const entry of entries) {
    if (!entry.isDirectory()) continue;
    const mapName = entry.name;
    const jsonName = 'map.json';

    const sourcePath = path.join(SOURCE_DIR, mapName, jsonName);
    const sourceJson = readMapJson(sourcePath);
    if (!sourceJson) {
      console.error(`[VerifyMaps] 无法读取源地图: ${sourcePath}`);
      hasError = true;
      continue;
    }
    const sourceNorm = normalizeJson(sourceJson);

    const destinations = [
      { label: 'client', path: path.join(CLIENT_DIR, mapName, jsonName) },
      { label: 'server/data', path: path.join(CS_DIR, mapName, jsonName) },
      { label: 'bin/Debug', path: path.join(BIN_DEBUG_DIR, mapName, jsonName) },
      { label: 'bin/Release', path: path.join(BIN_RELEASE_DIR, mapName, jsonName) },
    ];

    console.log(`[VerifyMaps] ${mapName}`);
    for (const dest of destinations) {
      const destJson = readMapJson(dest.path);
      if (!destJson) {
        console.error(`  ❌ ${dest.label}: 文件不存在或无法解析: ${dest.path}`);
        hasError = true;
        continue;
      }
      if (normalizeJson(destJson) !== sourceNorm) {
        console.error(`  ❌ ${dest.label}: 内容与源不一致`);
        hasError = true;
      } else {
        console.log(`  ✅ ${dest.label}`);
      }
    }

    const overlaps = findOverlaps(sourceJson.cells ?? {});
    if (overlaps.length > 0) {
      console.error(`  ❌ 检测到 ${overlaps.length} 处建筑 footprint 重叠:`);
      for (const o of overlaps.slice(0, 10)) {
        console.error(`     ${o.anchor} vs ${o.other}`);
      }
      hasError = true;
    } else {
      console.log(`  ✅ 建筑 footprint 无重叠`);
    }
  }

  if (hasError) {
    console.error('\n[VerifyMaps] 校验失败，请运行 npx tsx tables/scripts/sync-maps.ts 重新同步。');
    process.exit(1);
  }
  console.log('\n[VerifyMaps] 所有地图一致性校验通过。');
}

main();
