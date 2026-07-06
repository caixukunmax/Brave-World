const { describe, it, beforeEach, afterEach } = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const path = require('path');
const { resolveMapName, listExistingMaps } = require('../lib/naming');

describe('naming', () => {
  const tmpDir = path.join(__dirname, 'tmp-maps');

  beforeEach(() => {
    if (fs.existsSync(tmpDir)) fs.rmSync(tmpDir, { recursive: true });
    fs.mkdirSync(path.join(tmpDir, 'test'), { recursive: true });
    fs.mkdirSync(path.join(tmpDir, 'test_1'), { recursive: true });
    fs.mkdirSync(path.join(tmpDir, 'test_2'), { recursive: true });
    fs.writeFileSync(path.join(tmpDir, 'test', 'map.json'), '{}');
    fs.writeFileSync(path.join(tmpDir, 'test_1', 'map.json'), '{}');
    fs.writeFileSync(path.join(tmpDir, 'test_2', 'map.json'), '{}');
  });

  afterEach(() => {
    if (fs.existsSync(tmpDir)) fs.rmSync(tmpDir, { recursive: true });
  });

  it('resolves to next available index', () => {
    const name = resolveMapName('test', tmpDir);
    assert.strictEqual(name, 'test_3');
  });

  it('returns original name if not exists', () => {
    const name = resolveMapName('newmap', tmpDir);
    assert.strictEqual(name, 'newmap');
  });
});
