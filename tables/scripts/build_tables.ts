#!/usr/bin/env tsx
/**
 * =============================================================================
 * Luban 配置表编译脚本
 * 功能：从 Excel 生成 JSON 数据（C# 服务器端）
 * =============================================================================
 */

import path from 'path';
import fs from 'fs';
import { spawnSync } from 'child_process';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);

// =============================================================================
// 类型定义
// =============================================================================
interface LubanConfig {
  luban_dll: string;
  input: {
    data_dir: string;
    define_dir: string;
    config_file: string;
  };
  luban_args: {
    l10n_text_provider_file: string;
  };
}

interface PathsConfig {
  tables: {
    json_data_dir: string;
  };
  maps: {
    source_dir: string;
    cs_output_dir: string;
    client_output_dir: string;
    cs_registry_path: string;
  };
}

// =============================================================================
// 配置加载
// =============================================================================
const BASE_DIR = path.resolve(path.dirname(__filename), '..');
const CONFIG_FILE = path.join(BASE_DIR, 'luban.config.json');

function loadConfig(): LubanConfig {
  if (!fs.existsSync(CONFIG_FILE)) {
    console.error(`配置文件不存在：${CONFIG_FILE}`);
    process.exit(1);
  }
  return JSON.parse(fs.readFileSync(CONFIG_FILE, 'utf-8'));
}

function loadPaths(): PathsConfig {
  const pathsFile = path.join(BASE_DIR, '..', 'paths.json');
  if (!fs.existsSync(pathsFile)) {
    console.error(`路径配置不存在：${pathsFile}`);
    process.exit(1);
  }
  return JSON.parse(fs.readFileSync(pathsFile, 'utf-8'));
}

// =============================================================================
// 工具函数
// =============================================================================
const colors = {
  reset: '\x1b[0m',
  blue: '\x1b[34m',
  green: '\x1b[32m',
  yellow: '\x1b[33m',
  red: '\x1b[31m',
};

function info(msg: string): void {
  console.log(`${colors.blue}[INFO]${colors.reset} ${msg}`);
}

function success(msg: string): void {
  console.log(`${colors.green}[SUCCESS]${colors.reset} ${msg}`);
}

function warn(msg: string): void {
  console.log(`${colors.yellow}[WARN]${colors.reset} ${msg}`);
}

function error(msg: string): void {
  console.error(`${colors.red}[ERROR]${colors.reset} ${msg}`);
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

function removeDirSync(dir: string): void {
  if (fs.existsSync(dir)) {
    fs.rmSync(dir, { recursive: true, force: true });
  }
}

function runLuban(
  dotnetCmd: string,
  lubanDll: string,
  configPath: string,
  extraArgs: string[],
  cwd: string
): number {
  const args = [lubanDll, '--conf', configPath, ...extraArgs];
  const result = spawnSync(dotnetCmd, args, {
    stdio: 'inherit',
    cwd,
  });
  return result.status ?? 0;
}

// =============================================================================
// 主逻辑
// =============================================================================
function main(): void {
  const config = loadConfig();
  const paths = loadPaths();
  info(`Loaded config: ${CONFIG_FILE}`);

  console.log('');
  console.log('========================================');
  console.log('  Compiling Luban Configuration Tables');
  console.log('========================================');
  console.log('');

  // 解析路径（从 paths.json 统一配置读取，路径相对于项目根目录）
  const ROOT_DIR = path.resolve(BASE_DIR, '..');
  const LUBAN_DLL = path.join(BASE_DIR, config.luban_dll);
  const CONFIG_PATH = path.join(BASE_DIR, config.input.config_file);
  const JSON_DATA_DIR = path.resolve(ROOT_DIR, paths.tables.json_data_dir);

  // 临时目录
  const TMP_DIR = path.join(BASE_DIR, '.tmp');
  const TMP_JSON_DATA = path.join(TMP_DIR, 'json_data');

  console.log(`Luban DLL:       ${LUBAN_DLL}`);
  console.log(`JSON data dir:   ${JSON_DATA_DIR}`);
  console.log('');

  // 检查 Luban DLL
  if (!fs.existsSync(LUBAN_DLL)) {
    error(`Luban.dll not found: ${LUBAN_DLL}`);
    console.log('请确保 Luban 工具已下载到 tables/tools/luban/Luban/ 目录');
    process.exit(1);
  }

  // 检查 dotnet
  let dotnetCmd: string;
  try {
    const result = spawnSync('dotnet', ['--version'], { stdio: 'ignore' });
    if (result.status === 0) {
      dotnetCmd = 'dotnet';
    } else {
      throw new Error('dotnet not available');
    }
  } catch {
    error('dotnet not found. Luban 需要 .NET SDK 8.0+');
    process.exit(1);
  }

  // 检查 luban.conf
  if (!fs.existsSync(CONFIG_PATH)) {
    error(`luban.conf not found: ${CONFIG_PATH}`);
    process.exit(1);
  }

  // 清理临时目录
  removeDirSync(TMP_DIR);
  fs.mkdirSync(TMP_JSON_DATA, { recursive: true });

  // 创建输出目录
  fs.mkdirSync(JSON_DATA_DIR, { recursive: true });

  // --------------------------------------------------------------------------
  // Pass 1: 生成 JSON data（C# 服务器端）
  // --------------------------------------------------------------------------
  console.log('----------------------------------------');
  info('Generating JSON data (C# server)...');
  console.log('----------------------------------------');

  const TMP_JSON_ONLY = path.join(TMP_DIR, 'json_only');

  const jsonArgs = [
    '-t', 'json',
    '-d', 'json',
    '-f',
    '-x', `outputDataDir=${TMP_JSON_ONLY}`,
    '-x', `l10n.textProviderFile=${config.luban_args.l10n_text_provider_file}`,
  ];

  let code = runLuban(dotnetCmd, LUBAN_DLL, CONFIG_PATH, jsonArgs, BASE_DIR);
  if (code !== 0) {
    error('Failed to generate JSON data');
    process.exit(1);
  }

  // 先把 JSON 数据拷出来，再跑 Lua code（Lua 会清理输出目录）
  fs.mkdirSync(JSON_DATA_DIR, { recursive: true });
  if (fs.existsSync(TMP_JSON_ONLY)) {
    copyDirSync(TMP_JSON_ONLY, JSON_DATA_DIR);
  }
  success(`JSON data → ${paths.tables.json_data_dir}`);

  // 将技能配置 JSON 复制到客户端 data 目录
  const CLIENT_DATA_DIR = path.resolve(ROOT_DIR, 'clinetcsharp/data');
  const SKILL_JSON_SRC = path.join(JSON_DATA_DIR, 'common_tbskill.json');
  const SKILL_JSON_DST = path.join(CLIENT_DATA_DIR, 'skill_config.json');
  if (fs.existsSync(SKILL_JSON_SRC)) {
    fs.mkdirSync(CLIENT_DATA_DIR, { recursive: true });
    fs.copyFileSync(SKILL_JSON_SRC, SKILL_JSON_DST);
    success('Skill config → clinetcsharp/data/skill_config.json');
  }

  // 将 Buff 配置 JSON 复制到客户端 data 目录
  const BUFF_JSON_SRC = path.join(JSON_DATA_DIR, 'common_tbbuff.json');
  const BUFF_JSON_DST = path.join(CLIENT_DATA_DIR, 'buff_config.json');
  if (fs.existsSync(BUFF_JSON_SRC)) {
    fs.mkdirSync(CLIENT_DATA_DIR, { recursive: true });
    fs.copyFileSync(BUFF_JSON_SRC, BUFF_JSON_DST);
    success('Buff config → clinetcsharp/data/buff_config.json');
  }

  // 将道具配置 JSON 复制到客户端 data 目录
  const ITEM_JSON_SRC = path.join(JSON_DATA_DIR, 'item_tbitem.json');
  const ITEM_JSON_DST = path.join(CLIENT_DATA_DIR, 'item_config.json');
  if (fs.existsSync(ITEM_JSON_SRC)) {
    fs.mkdirSync(CLIENT_DATA_DIR, { recursive: true });
    fs.copyFileSync(ITEM_JSON_SRC, ITEM_JSON_DST);
    success('Item config → clinetcsharp/data/item_config.json');
  }

  // 将地形配置 JSON 复制到客户端 data 目录
  const TERRAIN_JSON_SRC = path.join(JSON_DATA_DIR, 'common_tbterrainconfig.json');
  const TERRAIN_JSON_DST = path.join(CLIENT_DATA_DIR, 'terrain_config.json');
  if (fs.existsSync(TERRAIN_JSON_SRC)) {
    fs.mkdirSync(CLIENT_DATA_DIR, { recursive: true });
    fs.copyFileSync(TERRAIN_JSON_SRC, TERRAIN_JSON_DST);
    success('Terrain config → clinetcsharp/data/terrain_config.json');
  }

  // 将 GM 命令说明 JSON 复制到客户端 data 目录
  const GM_COMMAND_DESC_JSON_SRC = path.join(JSON_DATA_DIR, 'common_tbgmcommanddesc.json');
  const GM_COMMAND_DESC_JSON_DST = path.join(CLIENT_DATA_DIR, 'gm_command_desc.json');
  if (fs.existsSync(GM_COMMAND_DESC_JSON_SRC)) {
    fs.mkdirSync(CLIENT_DATA_DIR, { recursive: true });
    fs.copyFileSync(GM_COMMAND_DESC_JSON_SRC, GM_COMMAND_DESC_JSON_DST);
    success('GM command desc config → clinetcsharp/data/gm_command_desc.json');
  }

  // 将生成的 JSON 同步到服务端 bin 目录（运行时读取位置）
  const BIN_DATA_DIR = path.resolve(ROOT_DIR, 'servercsharp/src/GameServer/bin/Debug/net8.0/data/tables');
  if (fs.existsSync(BIN_DATA_DIR)) {
    copyDirSync(JSON_DATA_DIR, BIN_DATA_DIR);
    success('JSON data → bin/Debug/net8.0/data/tables (runtime sync)');
  }

  // --------------------------------------------------------------------------
  // Pass 2: 同步地图 CSV 到客户端和服务端
  // --------------------------------------------------------------------------
  console.log('----------------------------------------');
  info('Syncing map CSV files...');
  console.log('----------------------------------------');

  const MAP_SOURCE_DIR = path.resolve(ROOT_DIR, paths.maps.source_dir);
  const MAP_CLIENT_DIR = path.resolve(ROOT_DIR, paths.maps.client_output_dir);
  const MAP_SERVER_DIR = path.resolve(ROOT_DIR, paths.maps.cs_output_dir);
  const MAP_REGISTRY_PATH = path.resolve(ROOT_DIR, paths.maps.cs_registry_path);

  function stripBom(content: string): string {
    return content.charCodeAt(0) === 0xFEFF ? content.slice(1) : content;
  }

  // 读取地图注册表，获取 display_name -> map_name 映射
  const mapNameMap = new Map<string, string>(); // display_name -> map_name
  if (fs.existsSync(MAP_REGISTRY_PATH)) {
    const registry = JSON.parse(stripBom(fs.readFileSync(MAP_REGISTRY_PATH, 'utf-8')));
    for (const entry of registry) {
      if (entry.display_name && entry.map_name) {
        mapNameMap.set(entry.display_name, entry.map_name);
      }
    }
  }

  if (fs.existsSync(MAP_SOURCE_DIR)) {
    const mapDirs = fs.readdirSync(MAP_SOURCE_DIR, { withFileTypes: true })
      .filter(e => e.isDirectory());
    const sourceMapNames = new Set<string>();

    for (const dir of mapDirs) {
      const displayName = dir.name;
      const serverMapName = mapNameMap.get(displayName) || displayName;
      sourceMapNames.add(displayName);
      sourceMapNames.add(serverMapName);

      const srcDir = path.join(MAP_SOURCE_DIR, displayName);
      const clientDir = path.join(MAP_CLIENT_DIR, displayName);
      const serverDir = path.join(MAP_SERVER_DIR, serverMapName);

      // 同步到客户端
      copyDirSync(srcDir, clientDir);
      success(`Map '${displayName}' → ${paths.maps.client_output_dir}`);

      // 同步到服务端
      copyDirSync(srcDir, serverDir);
      success(`Map '${displayName}' → ${paths.maps.cs_output_dir}/${serverMapName}`);
    }

    // 清理目标目录中已不存在的地图
    for (const outDir of [MAP_CLIENT_DIR, MAP_SERVER_DIR]) {
      if (!fs.existsSync(outDir)) continue;
      for (const entry of fs.readdirSync(outDir, { withFileTypes: true })) {
        if (!entry.isDirectory()) continue;
        if (sourceMapNames.has(entry.name)) continue;
        const staleDir = path.join(outDir, entry.name);
        removeDirSync(staleDir);
        info(`Removed stale map dir: ${staleDir}`);
      }
    }

    // 同时同步到服务端 bin 目录（运行时读取位置）
    const BIN_DEBUG_DIR = path.resolve(ROOT_DIR, 'servercsharp/src/GameServer/bin/Debug/net8.0/data/maps');
    const BIN_RELEASE_DIR = path.resolve(ROOT_DIR, 'servercsharp/src/GameServer/bin/Release/net8.0/data/maps');
    for (const dir of mapDirs) {
      const displayName = dir.name;
      const serverMapName = mapNameMap.get(displayName) || displayName;
      const srcDir = path.join(MAP_SOURCE_DIR, displayName);

      if (fs.existsSync(BIN_DEBUG_DIR)) {
        const binDir = path.join(BIN_DEBUG_DIR, serverMapName);
        copyDirSync(srcDir, binDir);
      }
      if (fs.existsSync(BIN_RELEASE_DIR)) {
        const binDir = path.join(BIN_RELEASE_DIR, serverMapName);
        copyDirSync(srcDir, binDir);
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
        info(`Removed stale bin map dir: ${staleDir}`);
      }
    }
    if (fs.existsSync(BIN_DEBUG_DIR) || fs.existsSync(BIN_RELEASE_DIR)) {
      success('Map CSV → bin/Debug|Release/net8.0/data/maps (runtime sync)');
    }

    // 重新生成服务端地图注册表，确保已删除的地图不会残留
    function readJsonFile(jsonPath: string): any {
      return JSON.parse(stripBom(fs.readFileSync(jsonPath, 'utf-8')));
    }

    const registry = mapDirs.map(dir => {
      const displayName = dir.name;
      const serverMapName = mapNameMap.get(displayName) || displayName;
      const jsonPath = path.join(MAP_SOURCE_DIR, displayName, 'map.json');
      const mapJson = readJsonFile(jsonPath);
      const bounds = mapJson.bounds || { x: 0, y: 0, w: 50, h: 50 };
      const spawn = mapJson.spawn || { x: Math.floor(bounds.w / 2), y: Math.floor(bounds.h / 2) };
      return {
        map_name: serverMapName,
        display_name: mapJson.display_name || displayName,
        width: bounds.w,
        height: bounds.h,
        spawn_x: spawn.x,
        spawn_y: spawn.y,
      };
    });
    fs.writeFileSync(MAP_REGISTRY_PATH, JSON.stringify(registry, null, 2), 'utf-8');
    success(`Map registry → ${paths.maps.cs_registry_path} (${registry.length} maps)`);

    // 同步注册表到 bin 运行目录
    for (const binDataDir of [path.dirname(BIN_DEBUG_DIR), path.dirname(BIN_RELEASE_DIR)]) {
      const binRegistryPath = path.join(binDataDir, 'map_registry.json');
      if (fs.existsSync(path.dirname(binRegistryPath))) {
        fs.writeFileSync(binRegistryPath, JSON.stringify(registry, null, 2), 'utf-8');
      }
    }
  } else {
    warn(`Map source dir not found: ${MAP_SOURCE_DIR}`);
  }

  // 清理临时目录
  removeDirSync(TMP_DIR);

  console.log('');
  console.log('========================================');
  success('Luban compilation complete!');
  console.log('========================================');
  console.log('');
}

main();
