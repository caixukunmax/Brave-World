const { describe, it, beforeEach, afterEach } = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');

describe('integration', () => {
  const tmpDir = path.join(__dirname, 'tmp-integration');
  const toolPath = path.join(__dirname, '..', 'generate-map.js');

  beforeEach(() => {
    if (fs.existsSync(tmpDir)) fs.rmSync(tmpDir, { recursive: true });
    fs.mkdirSync(tmpDir, { recursive: true });
  });

  afterEach(() => {
    if (fs.existsSync(tmpDir)) fs.rmSync(tmpDir, { recursive: true });
  });

  it('generates a complete map with all artifacts', () => {
    execSync(`node "${toolPath}" --name forest --description "small forest with a central lake" --width 25 --height 25 --seed 42 --style forest --water 0.2 --obstacle 0.1 --decoration medium --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });

    const mapPath = path.join(tmpDir, 'maps', 'forest', 'map.json');
    const formPath = path.join(tmpDir, 'maps', 'forest', 'map-gen-form.md');
    assert(fs.existsSync(mapPath));
    assert(fs.existsSync(formPath));

    const map = JSON.parse(fs.readFileSync(mapPath, 'utf8'));
    assert.strictEqual(map.version, 3);
    assert.strictEqual(map.display_name, 'forest');
    assert.strictEqual(map.bounds.w, 25);
    assert.strictEqual(map.bounds.h, 25);
    assert.strictEqual(typeof map.cells, 'object');
    assert.strictEqual(Object.keys(map.cells).length, 25 * 25);
    assert(map.cells['12_12']); // center cell exists
    assert(map.spawn.x >= 0 && map.spawn.x < 25);
    assert(map.spawn.y >= 0 && map.spawn.y < 25);

    const form = fs.readFileSync(formPath, 'utf8');
    assert(form.includes('forest'));
    assert(form.includes('small forest with a central lake'));
    assert(form.includes('25'));
  });

  it('generates multiple maps in one invocation via --count', () => {
    execSync(`node "${toolPath}" --name series --description "series" --width 15 --height 15 --seed 7 --count 3 --style desert --decoration low --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });

    assert(fs.existsSync(path.join(tmpDir, 'maps', 'series', 'map.json')));
    assert(fs.existsSync(path.join(tmpDir, 'maps', 'series_1', 'map.json')));
    assert(fs.existsSync(path.join(tmpDir, 'maps', 'series_2', 'map.json')));

    for (const name of ['series', 'series_1', 'series_2']) {
      const map = JSON.parse(fs.readFileSync(path.join(tmpDir, 'maps', name, 'map.json'), 'utf8'));
      assert.strictEqual(map.version, 3);
      assert.strictEqual(map.bounds.w, 15);
      assert.strictEqual(map.bounds.h, 15);
      assert(fs.existsSync(path.join(tmpDir, 'maps', name, 'map-gen-form.md')));
    }
  });

  it('adjusts an existing map into a new named version', () => {
    fs.mkdirSync(path.join(tmpDir, 'maps', 'base'), { recursive: true });
    fs.writeFileSync(path.join(tmpDir, 'maps', 'base', 'map.json'), JSON.stringify({
      version: 3,
      display_name: 'base',
      bounds: { x: 0, y: 0, w: 20, h: 20 },
      spawn: { x: 10, y: 10 },
      cells: Object.fromEntries(Array.from({ length: 20 * 20 }, (_, i) => {
        const x = i % 20, y = Math.floor(i / 20);
        return [`${x}_${y}`, { terrain: 0, height: 0, custom: '' }];
      }))
    }, null, 2));

    execSync(`node "${toolPath}" --name base --adjust --description "snowy update" --width 20 --height 20 --seed 99 --style snow --decoration high --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });

    const mapPath = path.join(tmpDir, 'maps', 'base_1', 'map.json');
    const formPath = path.join(tmpDir, 'maps', 'base_1', 'map-gen-form.md');
    assert(fs.existsSync(mapPath));
    assert(fs.existsSync(formPath));

    const map = JSON.parse(fs.readFileSync(mapPath, 'utf8'));
    assert.strictEqual(map.version, 3);
    assert.strictEqual(map.display_name, 'base_1');
    assert.strictEqual(map.bounds.w, 20);
    assert.strictEqual(map.bounds.h, 20);
    assert(map.cells['10_10']); // center cell exists

    const form = fs.readFileSync(formPath, 'utf8');
    assert(form.includes('调整已有地图'));
    assert(form.includes('base'));
  });
});
