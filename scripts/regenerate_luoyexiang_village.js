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

function removeMapDir(dir, mapName) {
  const target = path.join(dir, mapName);
  if (fs.existsSync(target)) {
    removeDir(target);
  }
}

function validateNoOverlaps(mapData, placedBuildings) {
  const footprintCells = new Set();
  const overlaps = [];
  for (const b of placedBuildings) {
    for (let dy = 0; dy < b.sizeY; dy++) {
      for (let dx = 0; dx < b.sizeX; dx++) {
        const cx = b.x + dx;
        const cy = b.y + dy;
        const k = `${cx}_${cy}`;
        if (footprintCells.has(k)) {
          overlaps.push({ x: cx, y: cy, building: b });
        } else {
          footprintCells.add(k);
        }
      }
    }
  }
  return overlaps;
}

function stripBom(text) {
  return text.charCodeAt(0) === 0xFEFF ? text.slice(1) : text;
}

function loadMapJson(filePath) {
  const text = stripBom(fs.readFileSync(filePath, 'utf8'));
  return JSON.parse(text);
}

function saveMapJson(mapData, filePath) {
  const dir = path.dirname(filePath);
  if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
  fs.writeFileSync(filePath, JSON.stringify(mapData, null, '  '), 'utf8');
}

function key(x, y) { return `${x}_${y}`; }

function inBounds(x, y, w, h) {
  return x >= 0 && x < w && y >= 0 && y < h;
}

function isWalkable(cell) {
  // 水/岩石等地形已转换为建筑装饰，地图上只剩普通平地（terrain 0）作为可行走底图
  return cell && cell.terrain === 0;
}

// 在指定中心附近找一个可行走的格子
function findWalkable(mapData, cx, cy, radius) {
  const { w, h } = mapData.bounds;
  for (let r = 0; r <= radius; r++) {
    for (let dy = -r; dy <= r; dy++) {
      for (let dx = -r; dx <= r; dx++) {
        if (Math.abs(dx) !== r && Math.abs(dy) !== r) continue;
        const x = cx + dx;
        const y = cy + dy;
        if (!inBounds(x, y, w, h)) continue;
        const cell = mapData.cells[key(x, y)];
        if (isWalkable(cell) && !cell.decoration) return { x, y };
      }
    }
  }
  return null;
}

// 建筑配置：id、占地大小、是否阻塞移动，必须与客户端 EntityProfileManager 默认配置保持一致。
// 10000=树(1x1,不阻塞, Terrain), 10001=房舍(2x2, Building), 10003=草地(1x1, Terrain),
// 20000=商店, 30000=水井, 40000=农田, 50000=酒馆, 60000=出生点, 70000=共享传送门,
// 80000=水域(建筑装饰, Terrain), 90000=岩石(建筑装饰, Terrain)。10002 为旧岩石装饰，生成时会被迁移。
const BUILDINGS = {
  tree:        { id: 10000, size: { x: 1, y: 1 }, block: false },
  house:       { id: 10001, size: { x: 2, y: 2 }, block: true },
  grass:       { id: 10003, size: { x: 1, y: 1 }, block: false },
  shop:        { id: 20000, size: { x: 1, y: 1 }, block: true },
  well:        { id: 30000, size: { x: 1, y: 1 }, block: true },
  farm:        { id: 40000, size: { x: 2, y: 1 }, block: true },
  tavern:      { id: 50000, size: { x: 2, y: 2 }, block: true },
  spawn:       { id: 60000, size: { x: 1, y: 1 }, block: false },
  portal:      { id: 70000, size: { x: 1, y: 1 }, block: false },
  water:       { id: 80000, size: { x: 1, y: 1 }, block: true },
  rock:        { id: 90000, size: { x: 1, y: 1 }, block: true },
};

function buildingFootprint(x, y, sizeX, sizeY) {
  const cells = [];
  for (let dy = 0; dy < sizeY; dy++) {
    for (let dx = 0; dx < sizeX; dx++) {
      cells.push({ x: x + dx, y: y + dy });
    }
  }
  return cells;
}

function canPlaceBuilding(mapData, x, y, sizeX, sizeY, placedBuildings) {
  const { w, h } = mapData.bounds;
  if (!inBounds(x, y, w, h)) return false;
  if (!inBounds(x + sizeX - 1, y + sizeY - 1, w, h)) return false;

  for (const p of buildingFootprint(x, y, sizeX, sizeY)) {
    const cell = mapData.cells[key(p.x, p.y)];
    if (!cell || !isWalkable(cell) || cell.decoration) return false;
  }

  for (const b of placedBuildings) {
    const overlapX = x < b.x + b.sizeX && x + sizeX > b.x;
    const overlapY = y < b.y + b.sizeY && y + sizeY > b.y;
    if (overlapX && overlapY) return false;
  }

  return true;
}

function placeBuilding(mapData, x, y, typeKey, placedBuildings) {
  const cfg = BUILDINGS[typeKey];
  if (!canPlaceBuilding(mapData, x, y, cfg.size.x, cfg.size.y, placedBuildings)) return false;

  // 把 footprint 内所有格子都改成普通平地，避免建筑压住水/岩石等地形
  for (const p of buildingFootprint(x, y, cfg.size.x, cfg.size.y)) {
    const footprintCell = mapData.cells[key(p.x, p.y)];
    if (footprintCell) {
      footprintCell.terrain = 0;
      // footprint 非锚点格子不保存 decoration，由客户端根据 anchor 展开占地
      if (p.x === x && p.y === y) {
        footprintCell.decoration = cfg.id;
      } else {
        footprintCell.decoration = 0;
      }
    }
  }

  placedBuildings.push({ x, y, sizeX: cfg.size.x, sizeY: cfg.size.y, type: typeKey });
  return true;
}

function distSq(x1, y1, x2, y2) {
  const dx = x1 - x2;
  const dy = y1 - y2;
  return dx * dx + dy * dy;
}

// 将基础地图生成器产生的“地形”水/岩石/草地转换为同等的“建筑装饰”，
// 使地图上所有视觉/可玩内容都落在 decoration 层级。
function convertTerrainToDecorations(mapData) {
  const { w, h } = mapData.bounds;
  let waterCount = 0;
  let rockCount = 0;
  let grassCount = 0;
  let migratedOldRock = 0;

  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const k = key(x, y);
      const cell = mapData.cells[k];
      if (!cell) continue;

      // 水地形 -> 水域装饰
      if (cell.terrain === 1) {
        cell.terrain = 0;
        cell.decoration = BUILDINGS.water.id;
        waterCount++;
        continue;
      }

      // 岩石地形 -> 岩石装饰
      if (cell.terrain === 4) {
        cell.terrain = 0;
        cell.decoration = BUILDINGS.rock.id;
        rockCount++;
        continue;
      }

      // 草地地形 -> 草地装饰
      if (cell.terrain === 2) {
        cell.terrain = 0;
        cell.decoration = BUILDINGS.grass.id;
        grassCount++;
        continue;
      }

      // 其他非平地地形（沙地、雪地等）统一回普通平地
      if (cell.terrain !== 0) {
        cell.terrain = 0;
      }

      // 迁移旧岩石装饰 10002 -> 新岩石装饰 90000
      if (cell.decoration === 10002) {
        cell.decoration = BUILDINGS.rock.id;
        migratedOldRock++;
        continue;
      }

      // 旧棕榈装饰 10003 已改为草地，保留作为草地装饰
    }
  }

  console.log(`[Regen] 地形转建筑装饰: 水=${waterCount}, 岩石=${rockCount}, 草地=${grassCount}, 迁移旧岩石=${migratedOldRock}`);
  return { waterCount, rockCount, grassCount, migratedOldRock };
}

// 在地图中心生成一座城镇：中间是规整的街区，外围保持野外自然装饰
function placeTown(mapData, cx, cy, radius, rng) {
  const { w, h } = mapData.bounds;
  const placedBuildings = []; // { x, y, sizeX, sizeY, type }
  const centerClearRadius = 6; // 城镇广场/出生点留空半径

  // 1. 清理城镇区域为平地，覆盖 base 地图的野外装饰
  for (let dy = -radius; dy <= radius; dy++) {
    for (let dx = -radius; dx <= radius; dx++) {
      if (dx * dx + dy * dy > radius * radius) continue;
      const x = cx + dx;
      const y = cy + dy;
      if (!inBounds(x, y, w, h)) continue;
      const cell = mapData.cells[key(x, y)];
      if (cell) {
        cell.terrain = 0;
        cell.decoration = 0;
      }
    }
  }

  // 2. 在城镇中心附近放一座 2x2 酒馆作为地标
  const tavernOffsets = [
    { x: cx - 8, y: cy - 8 }, { x: cx + 6, y: cy - 8 },
    { x: cx - 8, y: cy + 6 }, { x: cx + 6, y: cy + 6 },
  ];
  for (const pos of tavernOffsets) {
    if (distSq(pos.x, pos.y, cx, cy) >= centerClearRadius * centerClearRadius &&
        placeBuilding(mapData, pos.x, pos.y, 'tavern', placedBuildings)) {
      break;
    }
  }

  // 3. 按 4 格街区网格放置 2x2 房舍，留出街道
  for (let y = cy - radius + 1; y <= cy + radius - 2; y += 4) {
    for (let x = cx - radius + 1; x <= cx + radius - 2; x += 4) {
      if (!inBounds(x, y, w, h)) continue;

      const d2 = distSq(x, y, cx, cy);
      // 城镇外缘留 2 格缓冲区，避免房屋压线
      if (d2 > (radius - 2) * (radius - 2)) continue;
      // 中心广场留空
      if (d2 < centerClearRadius * centerClearRadius) continue;

      if (!canPlaceBuilding(mapData, x, y, 2, 2, placedBuildings)) continue;
      if (rng() < 0.22) continue; // 22% 网格留空，形成小巷和不规则街区

      placeBuilding(mapData, x, y, 'house', placedBuildings);
    }
  }

  // 4. 在街道交汇处放 1x1 商店和水井
  const smallBuildings = ['shop', 'shop', 'shop', 'well', 'well', 'well'];
  let smallIdx = 0;
  for (let y = cy - radius + 3; y <= cy + radius - 1; y += 4) {
    for (let x = cx - radius + 3; x <= cx + radius - 1; x += 4) {
      if (!inBounds(x, y, w, h)) continue;
      const d2 = distSq(x, y, cx, cy);
      if (d2 > (radius - 3) * (radius - 3)) continue;
      if (d2 < (centerClearRadius + 1) * (centerClearRadius + 1)) continue;
      if (rng() < 0.35) continue;
      const type = smallBuildings[smallIdx % smallBuildings.length];
      if (placeBuilding(mapData, x, y, type, placedBuildings)) {
        smallIdx++;
      }
    }
  }

  // 5. 在城镇边缘（农田带）放 2x1 农田
  for (let y = cy - radius + 2; y <= cy + radius - 1; y += 3) {
    for (let x = cx - radius + 2; x <= cx + radius - 1; x += 5) {
      if (!inBounds(x, y, w, h)) continue;
      const d2 = distSq(x, y, cx, cy);
      // 只在城镇外环区域放农田
      if (d2 < (radius - 6) * (radius - 6)) continue;
      if (d2 > (radius - 1) * (radius - 1)) continue;
      if (rng() < 0.25) continue;
      placeBuilding(mapData, x, y, 'farm', placedBuildings);
    }
  }

  // 6. 在城镇外围（过渡带）稀疏补一些树木，让城镇到野外的过渡更自然
  let trees = 0;
  let attempts = 0;
  while (trees < 40 && attempts < 400) {
    attempts++;
    const angle = rng() * Math.PI * 2;
    const dist = radius + 1 + Math.floor(rng() * 7);
    const x = cx + Math.round(Math.cos(angle) * dist);
    const y = cy + Math.round(Math.sin(angle) * dist);
    if (!inBounds(x, y, w, h)) continue;
    const cell = mapData.cells[key(x, y)];
    if (!cell || !isWalkable(cell) || cell.decoration) continue;
    cell.decoration = BUILDINGS.tree.id;
    trees++;
  }

  return placedBuildings;
}

// 简单 seeded random
function createRng(seed) {
  let s = seed;
  return function () {
    s = (s * 9301 + 49297) % 233280;
    return s / 233280;
  };
}

// 1. 只删除目标地图（落叶乡），保留其他地图如新手村
console.log('[Regen] Step 1: Remove target map...');
removeMapDir(CLIENT_MAPS_DIR, MAP_NAME);
removeMapDir(TABLES_MAPS_DIR, MAP_NAME);
removeMapDir(SERVER_MAPS_DIR, MAP_NAME);

// 2. 生成 100x100 落叶乡
console.log('[Regen] Step 2: Generate base 100x100 map...');
const genCmd = [
  'node', path.join(ROOT, 'clinetcsharp', 'tools', 'generate-map.js'),
  '--name', MAP_NAME,
  '--description', '秋季落叶乡：中心城镇，边缘野外',
  '--width', '100',
  '--height', '100',
  '--allow-oversize',
  '--style', 'mixed',
  '--decoration', 'medium', // 边缘野外保持中等密度自然装饰
  '--count', '1',
  '--no-sync'
].join(' ');

execSync(genCmd, { stdio: 'inherit', cwd: ROOT });

// 3. 后处理：放置中心城镇、出生点、传送点
console.log('[Regen] Step 3: Place town, spawn and teleport...');
const mapJsonPath = path.join(CLIENT_MAPS_DIR, MAP_NAME, 'map.json');
const mapData = loadMapJson(mapJsonPath);
const rng = createRng(42);

// 先把基础生成器产生的水/岩石等地形转换为建筑装饰，确保地图上所有内容都是 decoration 级别
convertTerrainToDecorations(mapData);

// 中心城镇：半径 18 的圆形城区，内部为街区网格
const placed = placeTown(mapData, 50, 50, 18, rng);

// 出生点和传送点放在城镇中心广场
const spawn = findWalkable(mapData, 50, 50, 4);
const spawnPos = spawn || { x: 50, y: 50 };
mapData.spawn = { x: spawnPos.x, y: spawnPos.y };

const teleport = findWalkable(mapData, spawnPos.x + 2, spawnPos.y, 4);
const teleportPos = teleport || { x: spawnPos.x + 2, y: spawnPos.y };

const spawnCell = mapData.cells[key(spawnPos.x, spawnPos.y)];
if (spawnCell) {
  spawnCell.terrain = 0;
  spawnCell.decoration = BUILDINGS.spawn.id; // 60000 出生点建筑（仅编辑器可见）
}
const teleportCell = mapData.cells[key(teleportPos.x, teleportPos.y)];
if (teleportCell) {
  teleportCell.terrain = 0;
  teleportCell.decoration = BUILDINGS.portal.id; // 70000 共享传送门
}

console.log(`[Regen] Spawn: (${spawnPos.x}, ${spawnPos.y}) + 出生点建筑, Portal: (${teleportPos.x}, ${teleportPos.y})`);

// 验证所有建筑 footprint 无重叠
const overlaps = validateNoOverlaps(mapData, placed);
if (overlaps.length > 0) {
  console.error('[Regen] ERROR: 检测到建筑 footprint 重叠:');
  for (const o of overlaps.slice(0, 20)) {
    console.error(`  重叠格子: (${o.x}, ${o.y}), 建筑: ${JSON.stringify(o.building)}`);
  }
  process.exit(1);
}
console.log(`[Regen] 建筑占地验证通过: ${placed.length} 个建筑, 0 重叠`);

// 统计装饰
const counts = {};
for (const cell of Object.values(mapData.cells)) {
  if (cell.decoration) {
    counts[cell.decoration] = (counts[cell.decoration] || 0) + 1;
  }
}
const summary = Object.entries(counts)
  .map(([id, n]) => {
    const name = Object.entries(BUILDINGS).find(([_, cfg]) => cfg.id === Number(id))?.[0] || `other(${id})`;
    return `${name}=${n}`;
  })
  .join(', ');
console.log(`[Regen] Decorations: ${summary}`);

saveMapJson(mapData, mapJsonPath);

// 4. 复制到 tables 源目录
console.log('[Regen] Step 4: Copy to tables source...');
const tablesMapDir = path.join(TABLES_MAPS_DIR, MAP_NAME);
fs.mkdirSync(tablesMapDir, { recursive: true });
fs.copyFileSync(mapJsonPath, path.join(tablesMapDir, 'map.json'));

// 5. 同步到服务端和客户端，更新注册表
console.log('[Regen] Step 5: Sync maps...');
execSync('npx tsx tables/scripts/sync-maps.ts', { stdio: 'inherit', cwd: ROOT });

console.log('[Regen] Done.');
