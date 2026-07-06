const { describe, it, beforeEach, afterEach } = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');

describe('adjust mode', () => {
  const tmpDir = path.join(__dirname, 'tmp-adjust');
  const toolPath = path.join(__dirname, '..', 'generate-map.js');

  beforeEach(() => {
    if (fs.existsSync(tmpDir)) fs.rmSync(tmpDir, { recursive: true });
    fs.mkdirSync(path.join(tmpDir, 'maps', 'base'), { recursive: true });
    fs.writeFileSync(path.join(tmpDir, 'maps', 'base', 'map.json'), JSON.stringify({
      version: 3,
      display_name: 'base',
      bounds: { x: 0, y: 0, w: 30, h: 30 },
      spawn: { x: 15, y: 15 },
      cells: Object.fromEntries(Array.from({ length: 30 * 30 }, (_, i) => {
        const x = i % 30, y = Math.floor(i / 30);
        return [`${x}_${y}`, { terrain: 0, height: 0, custom: '' }];
      }))
    }, null, 2));
  });

  afterEach(() => {
    if (fs.existsSync(tmpDir)) fs.rmSync(tmpDir, { recursive: true });
  });

  it('adjusts an existing map into a new version', () => {
    const out = execSync(`node "${toolPath}" --name base --adjust --description "north snow" --width 30 --height 30 --seed 2 --style snow --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });
    assert(out.includes('base_1'));
    const mapPath = path.join(tmpDir, 'maps', 'base_1', 'map.json');
    assert(fs.existsSync(mapPath));
    const map = JSON.parse(fs.readFileSync(mapPath, 'utf8'));
    assert.strictEqual(map.display_name, 'base_1');
    assert.strictEqual(map.bounds.w, 30);
    assert.strictEqual(map.bounds.h, 30);

    const formPath = path.join(tmpDir, 'maps', 'base_1', 'map-gen-form.md');
    assert(fs.existsSync(formPath));
    const form = fs.readFileSync(formPath, 'utf8');
    assert(form.includes('调整已有地图'), 'form should show adjust mode');
    assert(form.includes('base'), 'form should reference base map');
  });

  it('exits with an error when the base map does not exist', () => {
    assert.throws(() => {
      execSync(`node "${toolPath}" --name missing --adjust --description "x" --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });
    }, /not found|Base map/);
  });
});
