const fs = require('fs');
const path = require('path');
const { createMapData, saveMapJson, validateMapName } = require('../clinetcsharp/tools/lib/map-core');
const { generateTerrain, ensureConnectivity, isWalkable } = require('../clinetcsharp/tools/lib/generator');
const { createNoise2D } = require('../clinetcsharp/tools/lib/noise');

const ROOT = path.resolve(__dirname, '..');
const MAP_NAME = '野狼谷';
const OUTPUT_DIR = path.join(ROOT, 'tables', 'datas', 'maps', MAP_NAME);
const OUTPUT_FILE = path.join(OUTPUT_DIR, 'map.json');

const WIDTH = 80;
const HEIGHT = 80;
const SPAWN_X = 40;
const SPAWN_Y = 40;
const PORTAL_X = 42;
const PORTAL_Y = 40;

const TERRAIN_WATER = 1;
const TERRAIN_GRASS = 2;
const TERRAIN_ROCK = 4;

const DECO_TREE = 10000;
const DECO_ROCK = 10002;
const DECO_SPAWN = 60000;
const DECO_PORTAL = 70000;

function key(x, y) { return `${x}_${y}`; }

function inBounds(x, y) {
  return x >= 0 && x < WIDTH && y >= 0 && y < HEIGHT;
}

function countTerrain(mapData) {
  const counts = {};
  for (const cell of Object.values(mapData.cells)) {
    counts[cell.terrain] = (counts[cell.terrain] || 0) + 1;
  }
  return counts;
}

function carveRiver(mapData, seed) {
  // 在地图西侧/北侧生成 1-2 条蜿蜒河流，增加生态感
  const noise = createNoise2D(seed + 1000);
  const riverCount = 1 + Math.floor(Math.abs(noise(0, 0)) * 2); // 1-2 条

  for (let r = 0; r < riverCount; r++) {
    let x = Math.floor(WIDTH * 0.15 + r * WIDTH * 0.35);
    let y = 0;
    while (y < HEIGHT) {
      // 主河道
      for (let dx = -1; dx <= 1; dx++) {
        const cx = x + dx;
        const cy = y;
        if (inBounds(cx, cy)) {
          mapData.cells[key(cx, cy)].terrain = TERRAIN_WATER;
        }
      }
      // 偶尔加宽到 2 格
      if (Math.abs(noise(x * 0.2, y * 0.2)) > 0.6) {
        const wx = x + 2;
        if (inBounds(wx, y)) {
          mapData.cells[key(wx, y)].terrain = TERRAIN_WATER;
        }
      }
      const turn = noise(x * 0.1, y * 0.1);
      if (turn > 0.4) x += 1;
      else if (turn < -0.4) x -= 1;
      y += 1;
      if (x < 2) x = 2;
      if (x >= WIDTH - 2) x = WIDTH - 3;
    }
  }
}

function placeSpawnAndPortal(mapData) {
  // 确保出生点与传送门区域可行走且为草地，但保留彼此的建筑锚点
  const points = [
    { x: SPAWN_X, y: SPAWN_Y, deco: DECO_SPAWN },
    { x: PORTAL_X, y: PORTAL_Y, deco: DECO_PORTAL },
  ];

  // 第一步：清出可行走的草地，但不碰其他点的锚点
  for (const p of points) {
    for (let dy = -2; dy <= 2; dy++) {
      for (let dx = -2; dx <= 2; dx++) {
        const cx = p.x + dx;
        const cy = p.y + dy;
        if (!inBounds(cx, cy)) continue;
        const k = key(cx, cy);
        // 不要清除另一个出生点/传送门的锚点
        const isOtherAnchor = points.some(op => op.x === cx && op.y === cy);
        if (isOtherAnchor) continue;
        mapData.cells[k].terrain = TERRAIN_GRASS;
        mapData.cells[k].decoration = 0;
      }
    }
  }

  // 第二步：放置建筑锚点
  for (const p of points) {
    mapData.cells[key(p.x, p.y)].terrain = TERRAIN_GRASS;
    mapData.cells[key(p.x, p.y)].decoration = p.deco;
  }

  mapData.spawn = { x: SPAWN_X, y: SPAWN_Y };
}

function placeDecorationsCustom(mapData, seed) {
  const noise = createNoise2D(seed + 2000);
  const grassCells = [];
  const rockCells = [];

  const protectedCells = new Set([
    key(SPAWN_X, SPAWN_Y),
    key(PORTAL_X, PORTAL_Y),
  ]);

  for (let y = 0; y < HEIGHT; y++) {
    for (let x = 0; x < WIDTH; x++) {
      const k = key(x, y);
      const cell = mapData.cells[k];
      if (protectedCells.has(k)) continue;
      if (cell.decoration) continue;
      if (cell.terrain === TERRAIN_GRASS) grassCells.push({ x, y, k });
      if (cell.terrain === TERRAIN_ROCK) rockCells.push({ x, y, k });
    }
  }

  // 树木密度：约 15% 的草地
  const targetTrees = Math.floor(grassCells.length * 0.15);
  grassCells.sort((a, b) => noise(a.x, a.y) - noise(b.x, b.y));
  for (let i = 0; i < targetTrees && i < grassCells.length; i++) {
    const { x, y } = grassCells[i];
    mapData.cells[key(x, y)].decoration = DECO_TREE;
  }

  // 岩石装饰密度：约 15% 的岩石地形
  const targetRocks = Math.floor(rockCells.length * 0.15);
  rockCells.sort((a, b) => noise(a.x + 1000, a.y + 1000) - noise(b.x + 1000, b.y + 1000));
  for (let i = 0; i < targetRocks && i < rockCells.length; i++) {
    const { x, y } = rockCells[i];
    mapData.cells[key(x, y)].decoration = DECO_ROCK;
  }
}

function main() {
  validateMapName(MAP_NAME);

  const seed = Math.floor(Math.random() * 1000000);
  console.log(`[Generate ${MAP_NAME}] seed=${seed}, size=${WIDTH}x${HEIGHT}`);

  const mapData = createMapData(WIDTH, HEIGHT, MAP_NAME);

  // 1. 生成基础地形：密林偏草地 + 岩石 + 少量水
  generateTerrain(mapData, {
    style: 'forest',
    water: 0.05,
    obstacle: 0.10,
    seed,
  });

  // 2. 雕刻河流（替代部分噪点水域，形成更自然的河流）
  carveRiver(mapData, seed);

  // 3. 将普通平地转为草地，营造浓密森林感（仅保留少量林间空地）
  const noiseGrass = createNoise2D(seed + 3000);
  for (let y = 0; y < HEIGHT; y++) {
    for (let x = 0; x < WIDTH; x++) {
      const k = key(x, y);
      const cell = mapData.cells[k];
      if (cell.terrain === 0 && Math.abs(noiseGrass(x * 0.15, y * 0.15)) < 0.75) {
        cell.terrain = TERRAIN_GRASS;
      }
    }
  }

  // 4. 保证连通性，避免玩家被水域/岩石困住
  ensureConnectivity(mapData);

  // 4. 放置出生点与传送门
  placeSpawnAndPortal(mapData);

  // 5. 放置密集树木与岩石装饰
  placeDecorationsCustom(mapData, seed);

  // 6. 保存
  saveMapJson(mapData, OUTPUT_FILE);

  const counts = countTerrain(mapData);
  console.log(`[Generate ${MAP_NAME}] terrain counts:`, counts);
  console.log(`[Generate ${MAP_NAME}] saved to ${OUTPUT_FILE}`);
}

main();
