/**
 * 地图配置同步脚本
 * 扫描 tables/datas/maps/ 下的 CSV 地图，部署到两端：
 *   1. C# 服务器:  servercsharp/data/maps/{name}/map.csv + map_registry.json
 *   2. 客户端:     clinetcsharp/maps/{name}/map.csv
 */

import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const ROOT = path.resolve(__dirname, '..', '..');
const PATHS_FILE = path.join(ROOT, 'paths.json');

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
  const map: Record<string, string> = {
    '新手村': 'xinshoucun',
  };
  return map[name] || name.replace(/[^a-zA-Z0-9_]/g, '_').toLowerCase();
}

function parseCsvFile(csvPath: string): { width: number; height: number; cells: string[] } | null {
  const content = fs.readFileSync(csvPath, 'utf-8');
  const lines = content.split(/\r?\n/);

  const dataLines: string[] = [];
  for (const line of lines) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith('#')) continue;
    dataLines.push(trimmed);
  }

  if (dataLines.length === 0) return null;

  const height = dataLines.length;
  const firstCells = dataLines[0].split(',');
  const width = firstCells.length;

  const cells: string[] = [];
  for (let y = 0; y < height; y++) {
    const cols = dataLines[y].split(',');
    for (let x = 0; x < width; x++) {
      cells.push(cols[x] ? cols[x].trim() : '0;0;0;0;0;');
    }
  }

  return { width, height, cells };
}

function loadMapConfig(configPath: string, mapName: string, width: number, height: number): MapConfig {
  const config: MapConfig = {
    map_name: toPinyin(mapName),
    display_name: mapName,
    width,
    height,
    spawn_x: Math.floor(width / 2),
    spawn_y: Math.floor(height / 2),
  };

  if (fs.existsSync(configPath)) {
    const content = fs.readFileSync(configPath, 'utf-8');
    for (const line of content.split('\n')) {
      const trimmed = line.trim();
      if (trimmed.startsWith('#') || trimmed.startsWith('[') || !trimmed.includes('=')) continue;
      const eqIdx = trimmed.indexOf('=');
      const key = trimmed.substring(0, eqIdx).trim();
      const val = trimmed.substring(eqIdx + 1).trim();
      if (key === 'width') config.width = parseInt(val) || config.width;
      if (key === 'height') config.height = parseInt(val) || config.height;
      if (key === 'spawn_x') config.spawn_x = parseInt(val) || config.spawn_x;
      if (key === 'spawn_y') config.spawn_y = parseInt(val) || config.spawn_y;
      if (key === 'display_name') config.display_name = val || config.display_name;
    }
  }

  return config;
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
    const csvPath = path.join(mapSourceDir, 'map.csv');
    const configPath = path.join(mapSourceDir, 'config.cfg');

    if (!fs.existsSync(csvPath)) {
      console.log(`[SyncMaps] 跳过 ${mapName}: 没有 map.csv`);
      continue;
    }

    const data = parseCsvFile(csvPath);
    if (!data) {
      console.log(`[SyncMaps] 跳过 ${mapName}: CSV 解析失败`);
      continue;
    }

    const luaName = toPinyin(mapName);
    const mapConfig = loadMapConfig(configPath, mapName, data.width, data.height);

    // 1. C# 服务器 — CSV
    const csMapDir = path.join(CS_DIR, luaName);
    fs.mkdirSync(csMapDir, { recursive: true });
    fs.copyFileSync(csvPath, path.join(csMapDir, 'map.csv'));

    // 2. 客户端 — CSV + config.cfg
    const clientMapDir = path.join(CLIENT_DIR, mapName);
    fs.mkdirSync(clientMapDir, { recursive: true });
    fs.copyFileSync(csvPath, path.join(clientMapDir, 'map.csv'));
    if (fs.existsSync(configPath)) {
      fs.copyFileSync(configPath, path.join(clientMapDir, 'config.cfg'));
    }

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
