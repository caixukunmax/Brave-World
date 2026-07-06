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

  it('rolls back only the files written when the map directory already existed', () => {
    const mapDir = path.join(tmpDir, 'maps', 'rollback');
    fs.mkdirSync(mapDir, { recursive: true });
    fs.mkdirSync(path.join(mapDir, 'map-gen-form.md'), { recursive: true });
    fs.writeFileSync(path.join(mapDir, 'extra-file.txt'), 'keep me');

    assert.throws(() => {
      execSync(`node "${toolPath}" --name rollback --width 10 --height 10 --seed 1 --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });
    }, /Error:/);

    assert(fs.existsSync(mapDir), 'pre-existing map directory should remain');
    assert(!fs.existsSync(path.join(mapDir, 'map.json')), 'map.json should be removed on rollback');
    assert(fs.existsSync(path.join(mapDir, 'extra-file.txt')), 'unrelated pre-existing files should not be removed');
  });

  it('accepts string-typed bounds in adjust mode', () => {
    fs.mkdirSync(path.join(tmpDir, 'maps', 'strbounds'), { recursive: true });
    fs.writeFileSync(path.join(tmpDir, 'maps', 'strbounds', 'map.json'), JSON.stringify({
      version: 3,
      display_name: 'strbounds',
      bounds: { x: 0, y: 0, w: '20', h: '20' },
      spawn: { x: 10, y: 10 },
      cells: Object.fromEntries(Array.from({ length: 20 * 20 }, (_, i) => {
        const x = i % 20, y = Math.floor(i / 20);
        return [`${x}_${y}`, { terrain: 0, height: 0, custom: '' }];
      }))
    }, null, 2));

    const out = execSync(`node "${toolPath}" --name strbounds --adjust --width 20 --height 20 --seed 1 --style snow --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });
    assert(out.includes('strbounds_1'));
    assert(fs.existsSync(path.join(tmpDir, 'maps', 'strbounds_1', 'map.json')));
  });

  it('rejects water or obstacle ratios outside [0, 1]', () => {
    assert.throws(() => {
      execSync(`node "${toolPath}" --name badwater --width 10 --height 10 --water 1.5 --seed 1 --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });
    }, /Invalid water/);
    assert.throws(() => {
      execSync(`node "${toolPath}" --name badobs --width 10 --height 10 --obstacle -0.1 --seed 1 --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });
    }, /Invalid obstacle/);
    assert.throws(() => {
      execSync(`node "${toolPath}" --name badwater2 --width 10 --height 10 --water abc --seed 1 --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });
    }, /Invalid water/);
  });

  it('populates size reason and layout summary in the form', () => {
    fs.mkdirSync(path.join(tmpDir, 'maps', 'layoutbase'), { recursive: true });
    const blueprintPath = path.join(tmpDir, 'maps', 'layoutbase', 'map-blueprint.json');
    fs.writeFileSync(blueprintPath, JSON.stringify({
      regions: [
        { anchor: 'center', type: 'water', size: 'medium' },
        { anchor: 'north', type: 'snow', size: 'small' }
      ]
    }));

    execSync(`node "${toolPath}" --name layoutbase --description "forest with lake" --width 25 --height 25 --seed 1 --blueprint "${blueprintPath}" --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });

    const formPath = path.join(tmpDir, 'maps', 'layoutbase', 'map-gen-form.md');
    assert(fs.existsSync(formPath));
    const form = fs.readFileSync(formPath, 'utf8');
    assert(form.includes('User specified 25×25'), 'form should include explicit size reason');
    assert(form.includes('center water (medium)'), 'form should include layout summary');
    assert(form.includes('north snow (small)'), 'form should include all blueprint regions');
  });

  it('reports inferred size reason from description when size is not explicit', () => {
    execSync(`node "${toolPath}" --name inferred --description "small forest" --seed 1 --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });

    const formPath = path.join(tmpDir, 'maps', 'inferred', 'map-gen-form.md');
    assert(fs.existsSync(formPath));
    const form = fs.readFileSync(formPath, 'utf8');
    assert(form.includes("Inferred 'small' from description:"), 'form should include inferred size reason');
  });

  it('reports only the explicitly provided dimension in size reason', () => {
    execSync(`node "${toolPath}" --name widthexplicit --width 40 --seed 1 --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });

    const formPath = path.join(tmpDir, 'maps', 'widthexplicit', 'map-gen-form.md');
    assert(fs.existsSync(formPath));
    const form = fs.readFileSync(formPath, 'utf8');
    assert(form.includes('User specified width 40'), 'form should report only width as explicit');
    assert(!form.includes('User specified 40×30'), 'form should not report default height as explicit');
  });

  it('rejects path traversal in map name', () => {
    assert.throws(() => {
      execSync(`node "${toolPath}" --name "../evil-target" --width 10 --height 10 --seed 1 --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });
    }, /Invalid map name/);
    assert(!fs.existsSync(path.join(tmpDir, 'evil-target')), 'path traversal should not create files outside maps folder');
  });

  it('allows leading zeros in size arguments', () => {
    execSync(`node "${toolPath}" --name leadingzeros --width 08 --height 08 --seed 1 --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });
    const map = JSON.parse(fs.readFileSync(path.join(tmpDir, 'maps', 'leadingzeros', 'map.json'), 'utf8'));
    assert.strictEqual(map.bounds.w, 8);
    assert.strictEqual(map.bounds.h, 8);
  });

  it('rejects invalid style and decoration values', () => {
    assert.throws(() => {
      execSync(`node "${toolPath}" --name badstyle --style pirate --width 10 --height 10 --seed 1 --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });
    }, /Invalid style/);
    assert.throws(() => {
      execSync(`node "${toolPath}" --name baddecoration --decoration extreme --width 10 --height 10 --seed 1 --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });
    }, /Invalid decoration/);
  });

  it('restores pre-existing map.json from backup when force generation fails', () => {
    const mapDir = path.join(tmpDir, 'maps', 'rollbackforce');
    fs.mkdirSync(mapDir, { recursive: true });
    const originalContent = JSON.stringify({ version: 3, display_name: 'rollbackforce', original: true });
    fs.writeFileSync(path.join(mapDir, 'map.json'), originalContent);
    // Cause writeForm to fail after map.json has been overwritten.
    fs.mkdirSync(path.join(mapDir, 'map-gen-form.md'), { recursive: true });

    assert.throws(() => {
      execSync(`node "${toolPath}" --name rollbackforce --width 10 --height 10 --seed 1 --force --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });
    }, /Error:/);

    assert(fs.existsSync(mapDir), 'map directory should remain');
    const restored = fs.readFileSync(path.join(mapDir, 'map.json'), 'utf8');
    assert.strictEqual(restored, originalContent, 'original map.json should be restored from backup');
    for (const file of ['map.json.bak', 'map-gen-form.md.bak', 'map-blueprint.json.bak']) {
      assert(!fs.existsSync(path.join(mapDir, file)), `backup ${file} should be cleaned up`);
    }
  });

  it('rejects adjust mode when the base map is oversized without --allow-oversize', () => {
    fs.mkdirSync(path.join(tmpDir, 'maps', 'oversizedbase'), { recursive: true });
    fs.writeFileSync(path.join(tmpDir, 'maps', 'oversizedbase', 'map.json'), JSON.stringify({
      version: 3,
      display_name: 'oversizedbase',
      bounds: { x: 0, y: 0, w: 60, h: 60 },
      spawn: { x: 30, y: 30 },
      cells: Object.fromEntries(Array.from({ length: 60 * 60 }, (_, i) => {
        const x = i % 60, y = Math.floor(i / 60);
        return [`${x}_${y}`, { terrain: 0, height: 0, custom: '' }];
      }))
    }, null, 2));

    assert.throws(() => {
      execSync(`node "${toolPath}" --name oversizedbase --adjust --seed 1 --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });
    }, /exceeds the default maximum size/);
  });

  it('rejects map names "." and ".."', () => {
    assert.throws(() => {
      execSync(`node "${toolPath}" --name "." --width 10 --height 10 --seed 1 --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });
    }, /Invalid map name/);
    assert.throws(() => {
      execSync(`node "${toolPath}" --name ".." --width 10 --height 10 --seed 1 --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });
    }, /Invalid map name/);
  });

  it('does not clobber unrelated .bak files in the map directory', () => {
    const mapDir = path.join(tmpDir, 'maps', 'backupclobber');
    fs.mkdirSync(mapDir, { recursive: true });
    const originalMap = JSON.stringify({ version: 3, display_name: 'backupclobber', original: true });
    fs.writeFileSync(path.join(mapDir, 'map.json'), originalMap);
    const sentinel = 'unrelated backup content';
    fs.writeFileSync(path.join(mapDir, 'map.json.bak'), sentinel);

    execSync(`node "${toolPath}" --name backupclobber --width 10 --height 10 --seed 1 --force --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });

    const currentMap = JSON.parse(fs.readFileSync(path.join(mapDir, 'map.json'), 'utf8'));
    assert(!currentMap.original, 'map.json should be overwritten in force mode');
    assert.strictEqual(fs.readFileSync(path.join(mapDir, 'map.json.bak'), 'utf8'), sentinel, 'unrelated .bak file should not be touched');
  });

  it('rejects an output-dir outside the project root', () => {
    const escapedDir = path.resolve(tmpDir, '..', '..', '..', '..', '..', 'escaped-cli-output');
    assert.throws(() => {
      execSync(`node "${toolPath}" --name escaped --width 10 --height 10 --seed 1 --output-dir "${escapedDir}" --no-sync`, { encoding: 'utf8' });
    }, /Output directory .* escapes/);
    assert(!fs.existsSync(escapedDir), 'no files should be created outside the project');
  });

  it('accepts decimal literals .5 and 1. for ratios', () => {
    execSync(`node "${toolPath}" --name ratioliterals --width 10 --height 10 --water .5 --obstacle 1. --seed 1 --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });
    assert(fs.existsSync(path.join(tmpDir, 'maps', 'ratioliterals', 'map.json')));
  });

  it('copies the provided blueprint into the map directory', () => {
    fs.mkdirSync(path.join(tmpDir, 'maps', 'blueprintcopy'), { recursive: true });
    const sourceBlueprint = path.join(tmpDir, 'maps', 'blueprintcopy', 'source-blueprint.json');
    const blueprint = { regions: [{ anchor: 'center', type: 'water', size: 'small' }] };
    fs.writeFileSync(sourceBlueprint, JSON.stringify(blueprint));

    execSync(`node "${toolPath}" --name blueprintcopy --width 15 --height 15 --seed 1 --blueprint "${sourceBlueprint}" --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });

    const copiedPath = path.join(tmpDir, 'maps', 'blueprintcopy', 'map-blueprint.json');
    assert(fs.existsSync(copiedPath), 'map-blueprint.json should be copied when --blueprint is provided');
    assert.deepStrictEqual(JSON.parse(fs.readFileSync(copiedPath, 'utf8')), blueprint);
  });

  it('does not create map-blueprint.json when no blueprint is provided', () => {
    execSync(`node "${toolPath}" --name noblueprint --width 10 --height 10 --seed 1 --output-dir "${tmpDir}" --no-sync`, { encoding: 'utf8' });
    assert(!fs.existsSync(path.join(tmpDir, 'maps', 'noblueprint', 'map-blueprint.json')));
  });
});
