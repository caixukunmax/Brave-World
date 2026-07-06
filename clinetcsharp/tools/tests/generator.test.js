const { describe, it } = require('node:test');
const assert = require('node:assert');
const { createMapData } = require('../lib/map-core');
const { generateTerrain, countTerrain } = require('../lib/generator');

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
});
