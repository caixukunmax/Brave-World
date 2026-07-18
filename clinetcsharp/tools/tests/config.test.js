const { describe, it } = require('node:test');
const assert = require('node:assert');

describe('map-gen-config', () => {
  it('loads and has required sections', () => {
    const config = require('../map-gen-config.json');
    assert(config.terrains.water.id === 1);
    assert(config.styles.forest.water === 0.15);
    assert(config.decorationDensity.medium === 0.06);
    assert(config.decorations.tree.id === 100000);
    assert(config.decorations.grass.id === 110000);
    assert(config.sizeLevels.medium.min === 30);
  });
});
