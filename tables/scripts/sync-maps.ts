/**
 * 地图配置同步脚本
 * 扫描 tables/datas/maps/ 下的 JSON 地图，部署到多端：
 *   1. C# 服务器源目录:  servercsharp/data/maps/{name}/map.json + map_registry.json
 *   2. 客户端源目录:     clinetcsharp/maps/{name}/map.json
 *   3. 服务端 bin 运行目录: servercsharp/src/GameServer/bin/Debug|Release/net8.0/data/maps/{name}/map.json
 *
 * 本脚本是地图同步的单一入口，确保 tables/datas/maps/ 作为唯一数据源，
 * 所有消费端（客户端、服务端源目录、服务端运行时目录）保持一致。
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

function copyDirSync(src: string, dest: string): void {
  if (!fs.existsSync(dest)) {
    fs.mkdirSync(dest, { recursive: true });
  }
  const entries = fs.readdirSync(src, { withFileTypes: true });
  for (const entry of entries) {
    const srcPath = path.join(src, entry.name);
    const destPath = path.join(dest, entry.name);
    if (entry.isDirectory()) {
      copyDirSync(srcPath, destPath);
    } else {
      fs.copyFileSync(srcPath, destPath);
    }
  }
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

function parseUid(uid: string): { x: number; y: number } | null {
  const parts = uid.split('_');
  if (parts.length !== 2) return null;
  const x = parseInt(parts[0], 10);
  const y = parseInt(parts[1], 10);
  if (Number.isNaN(x) || Number.isNaN(y)) return null;
  return { x, y };
}

function loadMapConfig(json: MapJson, mapName: string, width: number, height: number): MapConfig {
  const bounds = json.bounds ?? { x: 0, y: 0, w: width, h: height };

  // 优先从地图中的出生点建筑（60000）推导 spawn
  let spawn = json.spawn ?? { x: Math.floor(width / 2), y: Math.floor(height / 2) };
  const spawnPointDecorationId = 60000;
  if (json.cells) {
    for (const [uid, cell] of Object.entries(json.cells)) {
      if (cell.decoration === spawnPointDecorationId) {
        const pos = parseUid(uid);
        if (pos) {
          spawn = pos;
          break;
        }
      }
    }
  }

  return {
    map_name: toPinyin(mapName),
    display_name: json.display_name || mapName,
    width: bounds.w,
    height: bounds.h,
    spawn_x: spawn.x,
    spawn_y: spawn.y,
  };
}

function removeDirSync(dir: string) {
  if (fs.existsSync(dir)) {
    fs.rmSync(dir, { recursive: true, force: true });
  }
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

  // 收集源目录中存在的地图名
  const sourceMapNames = new Set<string>();
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
    sourceMapNames.add(luaName);
    sourceMapNames.add(mapName);
    const mapConfig = loadMapConfig(data.json, mapName, data.width, data.height);

    // 让 map.json 的 spawn 字段与出生点建筑位置保持一致
    data.json.spawn = { x: mapConfig.spawn_x, y: mapConfig.spawn_y };
    const updatedJsonContent = JSON.stringify(data.json, null, 2);

    // 1. C# 服务器 — JSON
    const csMapDir = path.join(CS_DIR, luaName);
    fs.mkdirSync(csMapDir, { recursive: true });
    fs.writeFileSync(path.join(csMapDir, 'map.json'), updatedJsonContent, 'utf-8');

    // 2. 客户端 — JSON
    const clientMapDir = path.join(CLIENT_DIR, mapName);
    fs.mkdirSync(clientMapDir, { recursive: true });
    fs.writeFileSync(path.join(clientMapDir, 'map.json'), updatedJsonContent, 'utf-8');

    // 3. 回写 tables 源目录，确保单一数据源一致
    fs.writeFileSync(jsonPath, updatedJsonContent, 'utf-8');

    registry.push(mapConfig);
    console.log(`[SyncMaps] ${mapName} (${data.width}x${data.height}) spawn=(${mapConfig.spawn_x},${mapConfig.spawn_y}) → cs:${luaName} + client:${mapName}`);
    syncCount++;
  }

  // 3. 清理目标目录中源目录已不存在的地图
  for (const outDir of [CS_DIR, CLIENT_DIR]) {
    if (!fs.existsSync(outDir)) continue;
    for (const entry of fs.readdirSync(outDir, { withFileTypes: true })) {
      if (!entry.isDirectory()) continue;
      if (sourceMapNames.has(entry.name)) continue;
      const staleDir = path.join(outDir, entry.name);
      removeDirSync(staleDir);
      console.log(`[SyncMaps] 清理已删除地图: ${staleDir}`);
    }
  }

  // 4. 同步到服务端 bin 运行目录（避免直接运行 GameServer.exe 时读到旧地图）
  const BIN_DEBUG_DIR = path.resolve(ROOT, 'servercsharp/src/GameServer/bin/Debug/net8.0/data/maps');
  const BIN_RELEASE_DIR = path.resolve(ROOT, 'servercsharp/src/GameServer/bin/Release/net8.0/data/maps');
  for (const entry of entries) {
    if (!entry.isDirectory()) continue;

    const mapName = entry.name;
    const luaName = toPinyin(mapName);
    const srcDir = path.join(SOURCE_DIR, mapName);

    if (fs.existsSync(BIN_DEBUG_DIR)) {
      copyDirSync(srcDir, path.join(BIN_DEBUG_DIR, luaName));
    }
    if (fs.existsSync(BIN_RELEASE_DIR)) {
      copyDirSync(srcDir, path.join(BIN_RELEASE_DIR, luaName));
    }
  }
  // 清理 bin 目录中的过期地图
  for (const binDir of [BIN_DEBUG_DIR, BIN_RELEASE_DIR]) {
    if (!fs.existsSync(binDir)) continue;
    for (const entry of fs.readdirSync(binDir, { withFileTypes: true })) {
      if (!entry.isDirectory()) continue;
      if (sourceMapNames.has(entry.name)) continue;
      const staleDir = path.join(binDir, entry.name);
      removeDirSync(staleDir);
      console.log(`[SyncMaps] 清理过期 bin 地图: ${staleDir}`);
    }
  }
  if (fs.existsSync(BIN_DEBUG_DIR) || fs.existsSync(BIN_RELEASE_DIR)) {
    console.log(`[SyncMaps] 已同步到 bin 运行目录`);
  }

  // 5. 生成 C# 地图注册表 JSON
  fs.mkdirSync(path.dirname(REGISTRY_PATH), { recursive: true });
  fs.writeFileSync(REGISTRY_PATH, JSON.stringify(registry, null, 2), 'utf-8');
  console.log(`[SyncMaps] 注册表: ${REGISTRY_PATH} (${registry.length} 张地图)`);

  // 6. 同步注册表到 bin 运行目录
  for (const binDataDir of [path.dirname(BIN_DEBUG_DIR), path.dirname(BIN_RELEASE_DIR)]) {
    const binRegistryPath = path.join(binDataDir, 'map_registry.json');
    if (fs.existsSync(path.dirname(binRegistryPath))) {
      fs.writeFileSync(binRegistryPath, JSON.stringify(registry, null, 2), 'utf-8');
    }
  }

  console.log(`[SyncMaps] 完成，共同步 ${syncCount} 张地图`);
}

syncMaps();
