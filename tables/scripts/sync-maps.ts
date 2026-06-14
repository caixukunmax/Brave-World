/**
 * 地图配置同步脚本
 * 扫描 tables/datas/maps/ 下的 JSON 地图，部署到两端：
 *   1. C# 服务器:  servercsharp/data/maps/{name}/map.json + map_registry.json
 *   2. 客户端:     clinetcsharp/maps/{name}/map.json
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
}

interface MapBounds {
  x: number;
  y: number;
  w: number;
  h: number;
}

interface MapSpawn {
  x: number;
  y: number;
}

interface MapJson {
  version: number;
  display_name?: string;
  bounds?: MapBounds;
  spawn?: MapSpawn;
  cells?: Record<string, MapCell>;
}

interface MapConfig {
  map_name: string;
  display_name: string;
  width: number;
  height: number;
  spawn_x: number;
  spawn_y: number;
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

function toPinyin(name: string): string {
  // 不再把任何中文名强制转拼音，保持原名以统一客户端/服务端地图 key。
  return name;
}

function parseJsonFile(jsonPath: string): { json: MapJson; width: number; height: number } | null {
  let content = fs.readFileSync(jsonPath, 'utf-8');
  // 去除 UTF-8 BOM
  if (content.charCodeAt(0) === 0xFEFF) {
    content = content.slice(1);
  }
  const json: MapJson = JSON.parse(content);

  if ((json.version ?? 0) < 2) {
    console.log(`[SyncMaps] 跳过: JSON 版本过低 ${jsonPath}`);
    return null;
  }

  const bounds = json.bounds;
  if (!bounds) {
    console.log(`[SyncMaps] 跳过: 缺少 bounds ${jsonPath}`);
    return null;
  }

  return { json, width: bounds.w, height: bounds.h };
}

function loadMapConfig(json: MapJson, mapName: string, width: number, height: number): MapConfig {
  const bounds = json.bounds ?? { x: 0, y: 0, w: width, h: height };
  const spawn = json.spawn ?? { x: Math.floor(width / 2), y: Math.floor(height / 2) };
  return {
    map_name: toPinyin(mapName),
    display_name: json.display_name || mapName,
    width: bounds.w,
    height: bounds.h,
    spawn_x: spawn.x,
    spawn_y: spawn.y,
  };
}

function syncMaps() {
  console.log('[SyncMaps] 开始同步地图数据...');

  const paths = loadPaths();
  const SOURCE_DIR = path.resolve(ROOT, paths.source_dir);
  const CS_DIR = path.resolve(ROOT, paths.cs_output_dir);
  const CLIENT_DIR = path.resolve(ROOT, paths.client_output_dir);
  const REGISTRY_PATH = path.resolve(ROOT, paths.cs_registry_path);

  console.log(`[SyncMaps] 地图源:   ${SOURCE_DIR}`);
  console.log(`[SyncMaps] C# 输出:  ${CS_DIR}`);
  console.log(`[SyncMaps] 客户端:   ${CLIENT_DIR}`);

  if (!fs.existsSync(SOURCE_DIR)) {
    console.log('[SyncMaps] 地图源目录不存在，跳过');
    return;
  }

  [CS_DIR, CLIENT_DIR].forEach(d => fs.mkdirSync(d, { recursive: true }));

  const entries = fs.readdirSync(SOURCE_DIR, { withFileTypes: true });
  const registry: MapConfig[] = [];
  let syncCount = 0;

  for (const entry of entries) {
    if (!entry.isDirectory()) continue;

    const mapName = entry.name;
    const mapSourceDir = path.join(SOURCE_DIR, mapName);
    const jsonPath = path.join(mapSourceDir, 'map.json');

    if (!fs.existsSync(jsonPath)) {
      console.log(`[SyncMaps] 跳过 ${mapName}: 没有 map.json`);
      continue;
    }

    const data = parseJsonFile(jsonPath);
    if (!data) {
      console.log(`[SyncMaps] 跳过 ${mapName}: JSON 解析失败`);
      continue;
    }

    const luaName = toPinyin(mapName);
    const mapConfig = loadMapConfig(data.json, mapName, data.width, data.height);

    // 1. C# 服务器 — JSON
    const csMapDir = path.join(CS_DIR, luaName);
    fs.mkdirSync(csMapDir, { recursive: true });
    fs.copyFileSync(jsonPath, path.join(csMapDir, 'map.json'));

    // 2. 客户端 — JSON
    const clientMapDir = path.join(CLIENT_DIR, mapName);
    fs.mkdirSync(clientMapDir, { recursive: true });
    fs.copyFileSync(jsonPath, path.join(clientMapDir, 'map.json'));

    registry.push(mapConfig);
    console.log(`[SyncMaps] ${mapName} (${data.width}x${data.height}) → cs:${luaName} + client:${mapName}`);
    syncCount++;
  }

  // 3. 生成 C# 地图注册表 JSON
  fs.mkdirSync(path.dirname(REGISTRY_PATH), { recursive: true });
  fs.writeFileSync(REGISTRY_PATH, JSON.stringify(registry, null, 2), 'utf-8');
  console.log(`[SyncMaps] 注册表: ${REGISTRY_PATH} (${registry.length} 张地图)`);

  console.log(`[SyncMaps] 完成，共同步 ${syncCount} 张地图`);
}

syncMaps();
