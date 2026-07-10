const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');

const ROOT = path.resolve(__dirname, '..');
const MAP_NAME = '落叶乡';
const CLIENT_MAPS_DIR = path.join(ROOT, 'clinetcsharp', 'maps');
const TABLES_MAPS_DIR = path.join(ROOT, 'tables', 'datas', 'maps');
const SERVER_MAPS_DIR = path.join(ROOT, 'servercsharp', 'data', 'maps');

function removeDir(dir) {
  if (fs.existsSync(dir)) {
    fs.rmSync(dir, { recursive: true, force: true });
    console.log(`[Regen] Removed: ${dir}`);
  }
}

function removeAllMaps(dir) {
  if (!fs.existsSync(dir)) return;
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (entry.isDirectory()) {
      removeDir(path.join(dir, entry.name));
    }
  }
}

// 1. 移除所有现有地图
console.log('[Regen] Step 1: Remove all existing maps...');
removeAllMaps(CLIENT_MAPS_DIR);
removeAllMaps(TABLES_MAPS_DIR);
removeAllMaps(SERVER_MAPS_DIR);

// 2. 生成 100x100 落叶乡
console.log('[Regen] Step 2: Generate 100x100 map...');
const genCmd = [
  'node', path.join(ROOT, 'clinetcsharp', 'tools', 'generate-map.js'),
  '--name', MAP_NAME,
  '--description', '秋季落叶村庄，包含出生点和传送点',
  '--width', '100',
  '--height', '100',
  '--allow-oversize',
  '--style', 'mixed',
  '--decoration', 'medium',
  '--count', '1',
  '--no-sync'
].join(' ');

execSync(genCmd, { stdio: 'inherit', cwd: ROOT });

// 3. 后处理：放置出生点和传送点
console.log('[Regen] Step 3: Place spawn and teleport...');
const mapJsonPath = path.join(CLIENT_MAPS_DIR, MAP_NAME, 'map.json');
let text = fs.readFileSync(mapJsonPath, 'utf8');
if (text.charCodeAt(0) === 0xFEFF) text = text.slice(1);
const mapData = JSON.parse(text);

// 选择地图中心偏南一点作为出生点，周围留空
const spawnX = 50;
const spawnY = 50;
const teleportX = 51;
const teleportY = 50;

// 确保出生点和传送点存在且可通行
const spawnKey = `${spawnX}_${spawnY}`;
const teleportKey = `${teleportX}_${teleportY}`;

if (!mapData.cells[spawnKey]) {
  mapData.cells[spawnKey] = { uid: spawnKey, terrain: 0, height: 0, custom: '', decoration: 0 };
}
if (!mapData.cells[teleportKey]) {
  mapData.cells[teleportKey] = { uid: teleportKey, terrain: 0, height: 0, custom: '', decoration: 0 };
}

// 强制地形为可行走（terrain 0），移除可能阻挡的装饰
mapData.cells[spawnKey].terrain = 0;
mapData.cells[spawnKey].decoration = 0;
mapData.cells[teleportKey].terrain = 0;
mapData.cells[teleportKey].decoration = 20000; // 传送点装饰

mapData.spawn = { x: spawnX, y: spawnY };

fs.writeFileSync(mapJsonPath, JSON.stringify(mapData, null, '  '), 'utf8');
console.log(`[Regen] Spawn set to (${spawnX}, ${spawnY}), teleport at (${teleportX}, ${teleportY})`);

// 4. 复制到 tables 源目录
console.log('[Regen] Step 4: Copy to tables source...');
const tablesMapDir = path.join(TABLES_MAPS_DIR, MAP_NAME);
fs.mkdirSync(tablesMapDir, { recursive: true });
fs.copyFileSync(mapJsonPath, path.join(tablesMapDir, 'map.json'));

// 5. 同步到服务端和客户端，更新注册表
console.log('[Regen] Step 5: Sync maps...');
execSync('npx tsx tables/scripts/sync-maps.ts', { stdio: 'inherit', cwd: ROOT });

console.log('[Regen] Done.');
