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

  // 将生成的 JSON 同步到服务端 bin 目录（运行时读取位置）
  const BIN_DATA_DIR = path.resolve(ROOT_DIR, 'servercsharp/src/GameServer/bin/Debug/net8.0/data/tables');
  if (fs.existsSync(BIN_DATA_DIR)) {
    copyDirSync(JSON_DATA_DIR, BIN_DATA_DIR);
    success('JSON data → bin/Debug/net8.0/data/tables (runtime sync)');
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
