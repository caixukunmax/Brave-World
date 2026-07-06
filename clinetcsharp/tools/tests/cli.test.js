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
});
