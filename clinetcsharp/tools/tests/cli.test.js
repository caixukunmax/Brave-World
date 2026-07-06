const { describe, it, beforeEach, afterEach } = require('node:test');
const assert = require('node:assert');
const { execSync } = require('child_process');
const fs = require('fs');
const path = require('path');

describe('cli', () => {
  const tmpDir = path.join(__dirname, 'tmp-cli');
  const toolPath = path.join(__dirname, '..', 'generate-map.js');

  beforeEach(() => {
    if (fs.existsSync(tmpDir)) fs.rmSync(tmpDir, { recursive: true });
    fs.mkdirSync(tmpDir, { recursive: true });
  });

  afterEach(() => {
    if (fs.existsSync(tmpDir)) fs.rmSync(tmpDir, { recursive: true });
  });

  it('generates a map via CLI', () => {
    const out = execSync(`node "${toolPath}" --name testcli --description "small forest" --width 20 --height 20 --seed 1 --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });
    assert(out.includes('Generated'));
    const mapPath = path.join(tmpDir, 'maps', 'testcli', 'map.json');
    assert(fs.existsSync(mapPath));
    const map = JSON.parse(fs.readFileSync(mapPath, 'utf8'));
    assert.strictEqual(map.version, 3);
    assert.strictEqual(map.display_name, 'testcli');
    assert.strictEqual(map.bounds.w, 20);
    assert.strictEqual(map.bounds.h, 20);
    assert.strictEqual(typeof map.cells, 'object');
    assert.strictEqual(Object.keys(map.cells).length, 400);
    assert(map.spawn.x >= 0 && map.spawn.x < 20);
    assert(map.spawn.y >= 0 && map.spawn.y < 20);

    const formPath = path.join(tmpDir, 'maps', 'testcli', 'map-gen-form.md');
    assert(fs.existsSync(formPath));
    const form = fs.readFileSync(formPath, 'utf8');
    assert(form.includes('testcli'));
    assert(form.includes('20'));
  });

  it('generates multiple maps via --count', () => {
    execSync(`node "${toolPath}" --name batch --description "batch" --width 10 --height 10 --seed 2 --count 3 --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });
    assert(fs.existsSync(path.join(tmpDir, 'maps', 'batch', 'map.json')));
    assert(fs.existsSync(path.join(tmpDir, 'maps', 'batch_1', 'map.json')));
    assert(fs.existsSync(path.join(tmpDir, 'maps', 'batch_2', 'map.json')));
  });

  it('resolves naming conflicts by picking next available index', () => {
    fs.mkdirSync(path.join(tmpDir, 'maps', 'dup'), { recursive: true });
    fs.writeFileSync(path.join(tmpDir, 'maps', 'dup', 'map.json'), '{}');
    fs.mkdirSync(path.join(tmpDir, 'maps', 'dup_1'), { recursive: true });
    fs.writeFileSync(path.join(tmpDir, 'maps', 'dup_1', 'map.json'), '{}');

    execSync(`node "${toolPath}" --name dup --description "dup" --width 10 --height 10 --seed 3 --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });
    assert(fs.existsSync(path.join(tmpDir, 'maps', 'dup_2', 'map.json')));
  });

  it('defaults width and height to 30 when not provided', () => {
    execSync(`node "${toolPath}" --name defaults --description "defaults" --seed 4 --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });
    const map = JSON.parse(fs.readFileSync(path.join(tmpDir, 'maps', 'defaults', 'map.json'), 'utf8'));
    assert.strictEqual(map.bounds.w, 30);
    assert.strictEqual(map.bounds.h, 30);
  });

  it('rejects width or height above 50 without --allow-oversize', () => {
    assert.throws(() => {
      execSync(`node "${toolPath}" --name big --width 51 --height 30 --seed 1 --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });
    }, /--allow-oversize/);
    assert.throws(() => {
      execSync(`node "${toolPath}" --name big --width 30 --height 51 --seed 1 --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });
    }, /--allow-oversize/);
  });

  it('accepts width or height above 50 with --allow-oversize', () => {
    execSync(`node "${toolPath}" --name oversized --width 100 --height 100 --seed 1 --allow-oversize --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });
    const map = JSON.parse(fs.readFileSync(path.join(tmpDir, 'maps', 'oversized', 'map.json'), 'utf8'));
    assert.strictEqual(map.bounds.w, 100);
    assert.strictEqual(map.bounds.h, 100);
  });

  it('rejects malformed numeric size strings', () => {
    assert.throws(() => {
      execSync(`node "${toolPath}" --name bad --width 20abc --height 20 --seed 1 --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });
    }, /Invalid width/);
  });

  it('uses seed 0 as a deterministic seed', () => {
    const firstRun = path.join(tmpDir, 'first');
    const secondRun = path.join(tmpDir, 'second');
    fs.mkdirSync(firstRun, { recursive: true });
    fs.mkdirSync(secondRun, { recursive: true });

    execSync(`node "${toolPath}" --name seedzero --width 10 --height 10 --seed 0 --output-dir "${firstRun}" --no-sync`, { encoding: 'utf8' });
    execSync(`node "${toolPath}" --name seedzero --width 10 --height 10 --seed 0 --output-dir "${secondRun}" --no-sync`, { encoding: 'utf8' });

    const map1 = JSON.parse(fs.readFileSync(path.join(firstRun, 'maps', 'seedzero', 'map.json'), 'utf8'));
    const map2 = JSON.parse(fs.readFileSync(path.join(secondRun, 'maps', 'seedzero', 'map.json'), 'utf8'));
    assert.deepStrictEqual(map1.cells, map2.cells);
    assert.deepStrictEqual(map1.spawn, map2.spawn);
  });

  it('rejects --force combined with --count > 1', () => {
    assert.throws(() => {
      execSync(`node "${toolPath}" --name conflict --width 10 --height 10 --seed 1 --count 2 --force --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });
    }, /--force cannot be used with --count/);
  });

  it('rejects adjust mode with dimensions different from the base map', () => {
    fs.mkdirSync(path.join(tmpDir, 'maps', 'adjustbase'), { recursive: true });
    fs.writeFileSync(path.join(tmpDir, 'maps', 'adjustbase', 'map.json'), JSON.stringify({
      version: 3,
      display_name: 'adjustbase',
      bounds: { x: 0, y: 0, w: 20, h: 20 },
      spawn: { x: 10, y: 10 },
      cells: {}
    }));

    assert.throws(() => {
      execSync(`node "${toolPath}" --name adjustbase --adjust --width 25 --height 20 --seed 1 --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });
    }, /Adjust mode cannot change map dimensions/);
    assert.throws(() => {
      execSync(`node "${toolPath}" --name adjustbase --adjust --width 20 --height 25 --seed 1 --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });
    }, /Adjust mode cannot change map dimensions/);
  });

  it('rolls back the map directory if form write fails after save', () => {
    const mapDir = path.join(tmpDir, 'maps', 'rollback');
    fs.mkdirSync(mapDir, { recursive: true });
    fs.mkdirSync(path.join(mapDir, 'map-gen-form.md'), { recursive: true });

    assert.throws(() => {
      execSync(`node "${toolPath}" --name rollback --width 10 --height 10 --seed 1 --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });
    }, /Error:/);

    assert(!fs.existsSync(mapDir), 'map directory should be removed on rollback');
  });
});
