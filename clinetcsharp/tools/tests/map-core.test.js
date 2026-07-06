const { describe, it } = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const path = require('path');
const { createMapData, saveMapJson, loadMapJson, calculateBounds, validateMapName, assertContained } = require('../lib/map-core');

describe('map-core', () => {
  it('creates default map data', () => {
    const map = createMapData(10, 8, 'Test');
    assert.strictEqual(map.version, 3);
    assert.strictEqual(map.display_name, 'Test');
    assert.strictEqual(map.bounds.w, 10);
    assert.strictEqual(map.bounds.h, 8);
    assert.strictEqual(Object.keys(map.cells).length, 80);
    assert.strictEqual(map.cells['5_5'].terrain, 0);
  });

  it('saves and loads map json', () => {
    const tmp = path.join(__dirname, 'tmp-map.json');
    const map = createMapData(5, 5, 'Roundtrip');
    saveMapJson(map, tmp);
    const loaded = loadMapJson(tmp);
    assert.strictEqual(loaded.display_name, 'Roundtrip');
    assert.strictEqual(loaded.bounds.w, 5);
    fs.unlinkSync(tmp);
  });

  it('calculates bounds from cells', () => {
    const cells = { '2_3': { terrain: 0 }, '5_7': { terrain: 0 } };
    const b = calculateBounds(cells);
    assert.deepStrictEqual(b, { x: 2, y: 3, w: 4, h: 5 });
  });

  it('rejects . and .. as map names', () => {
    assert.throws(() => validateMapName('.'), /Invalid map name/);
    assert.throws(() => validateMapName('..'), /Invalid map name/);
  });

  it('accepts map names that contain dots', () => {
    assert.doesNotThrow(() => validateMapName('v1.2'));
    assert.doesNotThrow(() => validateMapName('map.test'));
  });

  it('detects paths that escape a parent directory', () => {
    assert.throws(() => assertContained('/tmp/foo', '/tmp/bar', 'Path'), /escapes/);
  });

  it('accepts paths contained within a parent directory', () => {
    assert.doesNotThrow(() => assertContained('/tmp/foo/bar', '/tmp/foo', 'Path'));
  });

  if (process.platform === 'win32') {
    it('accepts Windows paths that differ only in case', () => {
      assert.doesNotThrow(() => assertContained('C:\\Users\\Foo', 'c:\\users\\foo', 'Path'));
    });
  }
});
