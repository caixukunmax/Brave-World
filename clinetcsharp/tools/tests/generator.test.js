const { describe, it } = require('node:test');
const assert = require('node:assert');
const { createMapData } = require('../lib/map-core');
const { generateTerrain, countTerrain, isWalkable, ensureConnectivity, placeSpawn, placeDecorations } = require('../lib/generator');
const { applyBlueprint } = require('../lib/blueprint');
const config = require('../map-gen-config.json');

function makeCell(terrain) {
  return { terrain };
}

describe('generator terrain', () => {
  it('generates water according to ratio', () => {
    const map = createMapData(50, 50, 'Test');
    generateTerrain(map, { style: 'swamp', water: 0.35, obstacle: 0.10, seed: 1 });
    const counts = countTerrain(map);
    const waterRatio = counts.water / (50 * 50);
    assert(waterRatio >= 0.25 && waterRatio <= 0.45, `water ratio ${waterRatio}`);
  });

  it('uses seed deterministically', () => {
    const m1 = createMapData(30, 30, 'A');
    const m2 = createMapData(30, 30, 'A');
    generateTerrain(m1, { style: 'forest', water: 0.2, obstacle: 0.1, seed: 99 });
    generateTerrain(m2, { style: 'forest', water: 0.2, obstacle: 0.1, seed: 99 });
    assert.deepStrictEqual(m1.cells, m2.cells);
  });

  it('preserves blueprint regions during terrain generation', () => {
    const map = createMapData(30, 30, 'Blueprint');
    const blueprint = {
      regions: [
        { type: 'water', anchor: 'center', size: 'large' },
        { type: 'rock', anchor: 'northwest', size: 'medium' }
      ]
    };
    applyBlueprint(map, blueprint);
    const blueprintCells = Object.entries(map.cells)
      .filter(([_, c]) => c.terrain !== 0)
      .map(([k, c]) => [k, c.terrain]);
    assert(blueprintCells.length > 0, 'blueprint should set some cells');
    generateTerrain(map, { style: 'forest', water: 0.2, obstacle: 0.1, seed: 42 });
    for (const [key, terrain] of blueprintCells) {
      assert.strictEqual(map.cells[key].terrain, terrain, `cell ${key} should preserve blueprint terrain ${terrain}`);
    }
  });
});

describe('generator connectivity', () => {
  it('places spawn on a walkable cell', () => {
    const map = createMapData(40, 40, 'Conn');
    generateTerrain(map, { style: 'forest', water: 0.3, obstacle: 0.15, seed: 7 });
    ensureConnectivity(map);
    placeSpawn(map);
    const spawnCell = map.cells[`${map.spawn.x}_${map.spawn.y}`];
    assert(isWalkable(spawnCell));
  });

  it('has a large connected walkable region', () => {
    const map = createMapData(40, 40, 'Conn');
    generateTerrain(map, { style: 'forest', water: 0.3, obstacle: 0.15, seed: 7 });
    ensureConnectivity(map);
    placeSpawn(map);
    const reachable = floodReachable(map, map.spawn.x, map.spawn.y);
    const totalWalkable = Object.values(map.cells).filter(isWalkable).length;
    assert(reachable / totalWalkable >= 0.7, `reachable ratio ${reachable / totalWalkable}`);
  });

  it('reuses the region returned by ensureConnectivity when placing spawn', () => {
    const map = createMapData(40, 40, 'Conn');
    generateTerrain(map, { style: 'forest', water: 0.3, obstacle: 0.15, seed: 7 });
    const region = ensureConnectivity(map);
    placeSpawn(map, region);
    const spawnCell = map.cells[`${map.spawn.x}_${map.spawn.y}`];
    assert(isWalkable(spawnCell));
    assert(region.includes(`${map.spawn.x}_${map.spawn.y}`));
  });
});

describe('generator walkability', () => {
  it('treats configured walkable terrain as walkable', () => {
    assert.strictEqual(isWalkable(makeCell(config.terrains.grass.id)), true);
  });

  it('treats configured unwalkable terrain as unwalkable', () => {
    assert.strictEqual(isWalkable(makeCell(config.terrains.water.id)), false);
    assert.strictEqual(isWalkable(makeCell(config.terrains.rock.id)), false);
  });

  it('treats unknown terrain ids as unwalkable', () => {
    assert.strictEqual(isWalkable(makeCell(99999)), false);
  });
});

describe('generator decorations', () => {
  it('places decorations according to density', () => {
    const map = createMapData(50, 50, 'Dec');
    generateTerrain(map, { style: 'forest', water: 0.15, obstacle: 0.10, seed: 11 });
    ensureConnectivity(map);
    placeSpawn(map);
    placeDecorations(map, 'high', 'forest', 11);
    const decCount = Object.values(map.cells).filter(c => c.decoration).length;
    const total = 50 * 50;
    const ratio = decCount / total;
    assert(ratio >= 0.08 && ratio <= 0.16, `decoration ratio ${ratio}`);
  });

  it('does not place a decoration on the spawn cell', () => {
    const map = createMapData(50, 50, 'SpawnDec');
    generateTerrain(map, { style: 'forest', water: 0.15, obstacle: 0.10, seed: 12 });
    ensureConnectivity(map);
    placeSpawn(map);
    placeDecorations(map, 'high', 'forest', 12);
    const spawnCell = map.cells[`${map.spawn.x}_${map.spawn.y}`];
    assert.strictEqual(spawnCell.decoration, 0, 'spawn cell should remain undecorated');
  });

  it('does not let multi-cell decorations overlap', () => {
    // 临时注入一个 2x2 测试装饰物，验证 placeDecorations 的占地检测
    const testId = 99999;
    const testSize = { x: 2, y: 2 };
    config.decorations['__test_2x2'] = { id: testId, walkable: false, biomes: ['forest'], size: testSize };

    try {
      for (let seed = 1; seed <= 20; seed++) {
        const map = createMapData(80, 80, 'NoOverlap');
        generateTerrain(map, { style: 'forest', water: 0.10, obstacle: 0.05, seed });
        ensureConnectivity(map);
        placeSpawn(map);
        placeDecorations(map, 'high', 'forest', seed);

        const placed = Object.entries(map.cells)
          .filter(([_, c]) => c.decoration === testId)
          .map(([k, _]) => k.split('_').map(Number));

        const occupied = new Set();
        for (const [ax, ay] of placed) {
          for (let dy = 0; dy < testSize.y; dy++) {
            for (let dx = 0; dx < testSize.x; dx++) {
              const key = `${ax + dx}_${ay + dy}`;
              assert(!occupied.has(key), `2x2 decoration at ${ax},${ay} overlaps another at ${key}`);
              occupied.add(key);
            }
          }
        }
      }
    } finally {
      delete config.decorations['__test_2x2'];
    }
  });
});

function floodReachable(map, sx, sy) {
  const key = `${sx}_${sy}`;
  const visited = new Set();
  const stack = [key];
  while (stack.length) {
    const k = stack.pop();
    if (visited.has(k)) continue;
    visited.add(k);
    const [x, y] = k.split('_').map(Number);
    for (const [dx, dy] of [[0, 1], [0, -1], [1, 0], [-1, 0]]) {
      const nk = `${x + dx}_${y + dy}`;
      if (map.cells[nk] && isWalkable(map.cells[nk]) && !visited.has(nk)) stack.push(nk);
    }
  }
  return visited.size;
}
