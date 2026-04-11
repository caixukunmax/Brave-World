/**
 * 地图配置同步脚本
 * 扫描 clinetcsharp/maps/ 下的 CSV 地图，转换为 Lua table 格式
 * 输出到 skynet_src/tables/data/map_{name}.lua
 */

import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// 项目根目录
const ROOT = path.resolve(__dirname, '..', '..');
const MAPS_DIR = path.join(ROOT, 'clinetcsharp', 'maps');
const OUTPUT_DIR = path.join(ROOT, 'skynet_src', 'tables', 'data');

function toPinyin(name: string): string {
  // 简单映射：中文名 → 拼音标识符
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

  // 展平为一维数组 (index = y * width + x)，每个元素是 cell 的原始字符串
  const cells: string[] = [];
  for (let y = 0; y < height; y++) {
    const cols = dataLines[y].split(',');
    for (let x = 0; x < width; x++) {
      cells.push(cols[x] ? cols[x].trim() : '0;0;0;0;0;');
    }
  }

  return { width, height, cells };
}

function generateLua(mapName: string, data: { width: number; height: number; cells: string[] }): string {
  const lines: string[] = [];
  lines.push('-- 地图数据 (由 sync-maps.ts 自动生成，勿手动编辑)');
  lines.push(`-- 地图名: ${mapName}`);
  lines.push(`return {`);
  lines.push(`    width = ${data.width},`);
  lines.push(`    height = ${data.height},`);
  lines.push(`    cells = {`);

  for (let y = 0; y < data.height; y++) {
    const rowCells: string[] = [];
    for (let x = 0; x < data.width; x++) {
      const idx = y * data.width + x;
      rowCells.push(`[${idx}] = "${data.cells[idx]}"`);
    }
    lines.push(`        ${rowCells.join(', ')}`);
  }

  lines.push(`    }`);
  lines.push(`}`);
  lines.push('');

  return lines.join('\n');
}

function syncMaps() {
  console.log('[SyncMaps] 开始同步地图数据...');
  console.log(`[SyncMaps] 地图目录: ${MAPS_DIR}`);
  console.log(`[SyncMaps] 输出目录: ${OUTPUT_DIR}`);

  if (!fs.existsSync(MAPS_DIR)) {
    console.log('[SyncMaps] 地图目录不存在，跳过');
    return;
  }

  // 确保输出目录存在
  if (!fs.existsSync(OUTPUT_DIR)) {
    fs.mkdirSync(OUTPUT_DIR, { recursive: true });
  }

  // 扫描地图子目录
  const entries = fs.readdirSync(MAPS_DIR, { withFileTypes: true });
  let syncCount = 0;

  for (const entry of entries) {
    if (!entry.isDirectory()) continue;

    const mapName = entry.name;
    const csvPath = path.join(MAPS_DIR, mapName, 'map.csv');

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
    const luaContent = generateLua(mapName, data);
    const outputPath = path.join(OUTPUT_DIR, `map_${luaName}.lua`);

    fs.writeFileSync(outputPath, luaContent, 'utf-8');
    console.log(`[SyncMaps] 同步: ${mapName} (${data.width}x${data.height}) → map_${luaName}.lua`);
    syncCount++;
  }

  console.log(`[SyncMaps] 完成，共同步 ${syncCount} 张地图`);
}

syncMaps();
