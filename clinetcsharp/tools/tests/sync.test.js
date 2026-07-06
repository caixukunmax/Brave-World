const { describe, it, beforeEach } = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const path = require('path');
const { syncToServer } = require('../lib/sync');

describe('sync', () => {
  const tmpDir = path.join(__dirname, 'tmp-sync');
  const serverRoot = path.join(tmpDir, 'servercsharp', 'data');

  beforeEach(() => {
    if (fs.existsSync(tmpDir)) fs.rmSync(tmpDir, { recursive: true });
    fs.mkdirSync(path.join(tmpDir, 'maps', '新手村'), { recursive: true });
    fs.writeFileSync(
      path.join(tmpDir, 'maps', '新手村', 'map.json'),
      JSON.stringify({
        version: 3,
        display_name: '新手村',
        bounds: { width: 30, height: 20 },
        spawn: { x: 5, y: 10 },
        cells: []
      })
    );
    fs.mkdirSync(path.join(serverRoot, 'maps'), { recursive: true });
    fs.writeFileSync(path.join(serverRoot, 'map_registry.json'), JSON.stringify([]));
  });

  it('copies map to server and updates registry with actual bounds and spawn', () => {
    const result = syncToServer(
      '新手村',
      path.join(tmpDir, 'maps', '新手村', 'map.json'),
      serverRoot
    );
    assert.strictEqual(result.skipped, false);
    assert(fs.existsSync(path.join(serverRoot, 'maps', '新手村', 'map.json')));
    const registry = JSON.parse(fs.readFileSync(path.join(serverRoot, 'map_registry.json'), 'utf8'));
    const entry = registry.find(e => e.map_name === '新手村');
    assert(entry);
    assert.strictEqual(entry.display_name, '新手村');
    assert.strictEqual(entry.width, 30);
    assert.strictEqual(entry.height, 20);
    assert.strictEqual(entry.spawn_x, 5);
    assert.strictEqual(entry.spawn_y, 10);
  });

  it('skips when server root does not exist', () => {
    const result = syncToServer(
      '新手村',
      path.join(tmpDir, 'maps', '新手村', 'map.json'),
      path.join(tmpDir, 'missing')
    );
    assert.strictEqual(result.skipped, true);
    assert(!fs.existsSync(path.join(tmpDir, 'missing')));
  });

  it('falls back to defaults when map JSON lacks bounds or spawn', () => {
    fs.writeFileSync(
      path.join(tmpDir, 'maps', '新手村', 'map.json'),
      JSON.stringify({ version: 3 })
    );
    const result = syncToServer(
      '新手村',
      path.join(tmpDir, 'maps', '新手村', 'map.json'),
      serverRoot
    );
    assert.strictEqual(result.skipped, false);
    const registry = JSON.parse(fs.readFileSync(path.join(serverRoot, 'map_registry.json'), 'utf8'));
    const entry = registry.find(e => e.map_name === '新手村');
    assert.strictEqual(entry.width, 50);
    assert.strictEqual(entry.height, 50);
    assert.strictEqual(entry.spawn_x, 25);
    assert.strictEqual(entry.spawn_y, 25);
  });

  it('does not duplicate registry entries', () => {
    syncToServer('新手村', path.join(tmpDir, 'maps', '新手村', 'map.json'), serverRoot);
    syncToServer('新手村', path.join(tmpDir, 'maps', '新手村', 'map.json'), serverRoot);
    const registry = JSON.parse(fs.readFileSync(path.join(serverRoot, 'map_registry.json'), 'utf8'));
    assert.strictEqual(registry.filter(e => e.map_name === '新手村').length, 1);
  });
});
