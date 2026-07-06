const { describe, it } = require('node:test');
const assert = require('node:assert');
const { createMapData } = require('../lib/map-core');
const { applyBlueprint } = require('../lib/blueprint');

describe('blueprint', () => {
  it('places a central lake', () => {
    const map = createMapData(40, 40, 'BP');
    applyBlueprint(map, { regions: [{ type: 'water', anchor: 'center', size: 'large' }] });
    const center = map.cells['20_20'];
    assert.strictEqual(center.terrain, 1);
  });

  it('places snow in the north', () => {
    const map = createMapData(40, 40, 'BP');
    applyBlueprint(map, { regions: [{ type: 'snow', anchor: 'north', size: 'medium' }] });
    const north = map.cells['20_5'];
    assert.strictEqual(north.terrain, 5);
  });
});
