#!/usr/bin/env node
const fs = require('fs');
const os = require('os');
const path = require('path');
const { execSync } = require('child_process');
const { createMapData, saveMapJson, loadMapJson, validateMapName, assertContained } = require('./lib/map-core');
const { generateTerrain, convertTerrainToDecorations, ensureConnectivity, placeSpawn, placeDecorations } = require('./lib/generator');
const { applyBlueprint } = require('./lib/blueprint');
const { resolveMapName } = require('./lib/naming');
const { writeForm } = require('./lib/form');
const config = require('./map-gen-config.json');

const DEFAULT_WIDTH = 30;
const DEFAULT_HEIGHT = 30;
const DEFAULT_STYLE = 'mixed';
const DEFAULT_DECORATION = 'medium';

const SIZE_KEYWORDS = {
  small: ['房间', '小村庄', '密室', '小岛', 'room', 'small', 'tiny'],
  medium: ['小镇', '森林', '山谷', '港口', 'town', 'forest', 'valley', 'harbor', 'medium'],
  large: ['大陆', '广袤', '王国', '平原', 'continent', 'vast', 'kingdom', 'plain', 'large']
};

function inferSizeFromDescription(description, sizeLevels) {
  if (!description) return null;
  const lower = description.toLowerCase();
  for (const [level, words] of Object.entries(SIZE_KEYWORDS)) {
    if (words.some(w => lower.includes(w))) {
      const range = sizeLevels && sizeLevels[level];
      if (range) {
        const width = Math.floor(Math.random() * (range.max - range.min + 1)) + range.min;
        const height = Math.floor(Math.random() * (range.max - range.min + 1)) + range.min;
        return { width, height, level };
      }
    }
  }
  return null;
}

const BACKUP_FILE_NAMES = ['map.json', 'map-gen-form.md', 'map-blueprint.json'];

function cloneCells(cells) {
  const out = {};
  for (const [k, v] of Object.entries(cells)) {
    out[k] = { terrain: 0, height: 0, custom: '', decoration: 0, ...v };
    delete out[k].uid;
  }
  return out;
}

const BOOLEAN_FLAGS = new Set(['force', 'adjust', 'no-sync', 'allow-oversize']);

function parseArgs(argv) {
  const args = {};
  for (let i = 2; i < argv.length; i++) {
    const arg = argv[i];
    if (!arg.startsWith('--')) continue;
    const key = arg.slice(2);
    const next = argv[i + 1];
    if (BOOLEAN_FLAGS.has(key)) {
      if (next === 'true' || next === 'false') {
        args[key] = next === 'true';
        i++;
      } else {
        args[key] = true;
      }
    } else if (next !== undefined && !next.startsWith('--')) {
      args[key] = next;
      i++;
    } else {
      args[key] = true;
    }
  }
  return args;
}

function parseSize(value, defaultValue, label, allowOversize) {
  const raw = value === undefined ? String(defaultValue) : value.trim();
  const n = parseInt(raw, 10);
  if (!/^\d+$/.test(raw) || !Number.isFinite(n) || n <= 0) {
    throw new Error(`Invalid ${label}: "${value !== undefined ? value : defaultValue}". Must be a positive integer.`);
  }
  const maxSize = config.sizeLevels?.large?.max ?? 50;
  if (n > maxSize && !allowOversize) {
    throw new Error(
      `Map ${label} ${n} exceeds the default maximum size of ${maxSize}. ` +
      `To generate a map larger than ${maxSize}, confirm with the user and pass --allow-oversize.`
    );
  }
  return n;
}

function parseRatio(value, label) {
  if (value === undefined) return undefined;
  const trimmed = value.trim();
  if (trimmed.toLowerCase() === 'auto') return undefined;
  const n = Number(trimmed);
  if (!/^(\d+\.?\d*|\.\d+)$/.test(trimmed) || !Number.isFinite(n) || n < 0 || n > 1) {
    throw new Error(`Invalid ${label}: "${value}". Must be a number between 0 and 1, or 'auto'.`);
  }
  return n;
}

function buildSizeReason(width, height, description, explicitWidth, explicitHeight) {
  if (explicitWidth && explicitHeight) {
    return `User specified ${width}×${height}`;
  }
  if (explicitWidth) {
    return `User specified width ${width}`;
  }
  if (explicitHeight) {
    return `User specified height ${height}`;
  }
  if (description) {
    const lower = description.toLowerCase();
    for (const [level, words] of Object.entries(SIZE_KEYWORDS)) {
      if (words.some(w => lower.includes(w))) {
        return `Inferred '${level}' from description: '${description}'`;
      }
    }
  }
  return `Default size ${width}×${height}`;
}

function summarizeBlueprint(blueprint) {
  if (!blueprint || !Array.isArray(blueprint.regions) || blueprint.regions.length === 0) {
    return '-';
  }
  const parts = blueprint.regions.map(r => {
    const size = r.size || 'medium';
    return `${r.anchor || 'center'} ${r.type || 'unknown'} (${size})`;
  });
  return parts.join(', ');
}

function createBackups(mapDir, files) {
  const backups = [];
  let backupDir = null;
  for (const file of files) {
    const filePath = path.join(mapDir, file);
    try {
      const stat = fs.statSync(filePath);
      if (stat.isFile()) {
        if (!backupDir) {
          backupDir = fs.mkdtempSync(path.join(os.tmpdir(), 'mapgen-backup-'));
        }
        const backupPath = path.join(backupDir, file);
        fs.copyFileSync(filePath, backupPath);
        backups.push({ file, filePath, backupPath });
      }
    } catch {
      // File does not exist or is not readable; nothing to back up.
    }
  }
  return backups;
}

function removeBackups(backups) {
  const dirs = new Set();
  for (const { backupPath } of backups) {
    dirs.add(path.dirname(backupPath));
  }
  for (const dir of dirs) {
    try {
      fs.rmSync(dir, { recursive: true, force: true });
    } catch {
      // Ignore missing or already-removed backups.
    }
  }
}

function rollbackMapDir(mapDir, existedBefore, backups) {
  if (!existedBefore) {
    fs.rmSync(mapDir, { recursive: true, force: true });
    return;
  }

  // Restore any pre-existing files that were backed up at the start of generation.
  for (const { filePath, backupPath } of backups) {
    try {
      fs.copyFileSync(backupPath, filePath);
    } catch {
      // Backup may be missing; ignore.
    }
  }
  removeBackups(backups);

  // Remove generated files that did not have a pre-existing backup.
  const backedUpFiles = new Set(backups.map(b => b.file));
  for (const file of BACKUP_FILE_NAMES) {
    if (backedUpFiles.has(file)) continue;
    const filePath = path.join(mapDir, file);
    try {
      const stat = fs.statSync(filePath);
      if (stat.isFile()) {
        fs.rmSync(filePath);
      }
    } catch {
      // Ignore files that do not exist or are not regular files.
    }
  }
}

function generateSingleMap(options) {
  const {
    requestedName,
    outputDir,
    width,
    height,
    seed,
    style,
    water,
    obstacle,
    decoration,
    blueprintPath,
    force,
    noSync,
    description,
    isAdjust,
    baseMapData,
    explicitWidth,
    explicitHeight
  } = options;

  const mapsFolder = path.join(outputDir, 'maps');
  const finalName = force ? requestedName : resolveMapName(requestedName, mapsFolder);
  const mapDir = path.join(mapsFolder, finalName);

  // Defense in depth: ensure the computed map directory stays inside the maps folder.
  assertContained(mapDir, mapsFolder, 'Map directory');

  // Track whether the directory existed before this run so rollback does not
  // destroy unrelated files in a pre-existing map folder.
  const mapDirExisted = fs.existsSync(mapDir);

  // Back up any pre-existing files before overwriting them.
  const backups = createBackups(mapDir, BACKUP_FILE_NAMES);

  let mapData;
  if (isAdjust && baseMapData) {
    mapData = {
      version: 3,
      display_name: finalName,
      bounds: { ...baseMapData.bounds },
      spawn: { ...baseMapData.spawn },
      cells: cloneCells(baseMapData.cells)
    };
  } else {
    mapData = createMapData(width, height, finalName);
  }

  // Apply blueprint before terrain generation in both modes so regions are preserved.
  let blueprint = null;
  if (blueprintPath && fs.existsSync(blueprintPath)) {
    blueprint = JSON.parse(fs.readFileSync(blueprintPath, 'utf8'));
    applyBlueprint(mapData, blueprint);
  }
  generateTerrain(mapData, { style, water, obstacle, seed });

  const largestRegion = ensureConnectivity(mapData);
  placeSpawn(mapData, largestRegion);
  placeDecorations(mapData, decoration, style, seed);

  // 生成阶段用水/岩石 terrain 保证连通性，保存前统一转换为建筑装饰
  convertTerrainToDecorations(mapData);

  const sizeReason = buildSizeReason(width, height, description, explicitWidth, explicitHeight);
  const layoutSummary = summarizeBlueprint(blueprint);

  try {
    saveMapJson(mapData, path.join(mapDir, 'map.json'));

    let syncResult = { skipped: true, reason: 'disabled via --no-sync' };
    if (!noSync) {
      // 统一走 tables/datas/maps/ 作为数据源，再同步到 client/server/bin
      const projectRoot = path.join(__dirname, '..', '..');
      const tablesMapDir = path.join(projectRoot, 'tables', 'datas', 'maps', finalName);
      fs.mkdirSync(tablesMapDir, { recursive: true });
      fs.copyFileSync(path.join(mapDir, 'map.json'), path.join(tablesMapDir, 'map.json'));
      try {
        execSync('npx tsx tables/scripts/sync-maps.ts', { stdio: 'inherit', cwd: projectRoot });
        syncResult = { skipped: false };
      } catch (err) {
        console.error(`[sync] failed: ${err.message}`);
        syncResult = { skipped: true, reason: err.message };
      }
    }

    const formOptions = {
      requestedName,
      finalName,
      mode: isAdjust ? '调整已有地图' : '生成新地图',
      baseMap: isAdjust ? requestedName : undefined,
      width,
      height,
      sizeReason,
      seed,
      description: description || '-',
      style,
      water: water ?? 'auto',
      obstacle: obstacle ?? 'auto',
      decoration,
      layoutSummary,
      force: !!force,
      outputPath: mapDir,
      syncServer: !syncResult.skipped
    };
    writeForm(path.join(mapDir, 'map-gen-form.md'), formOptions);

    // Persist the source blueprint alongside the generated map when one was provided.
    if (blueprintPath) {
      fs.copyFileSync(blueprintPath, path.join(mapDir, 'map-blueprint.json'));
    }

    // Generation succeeded; discard the backups.
    removeBackups(backups);
  } catch (err) {
    rollbackMapDir(mapDir, mapDirExisted, backups);
    throw err;
  }

  return { finalName, mapDir };
}

function main() {
  const args = parseArgs(process.argv);

  if (!args.name) {
    console.error('Usage: node generate-map.js --name <name> [--description <desc>] [--count <n>] [--width <w>] [--height <h>] [--seed <n>] [--style <style>] [--water <ratio>] [--obstacle <ratio>] [--decoration <low|medium|high>] [--force] [--output-dir <dir>] [--blueprint <path>] [--no-sync] [--adjust] [--allow-oversize]');
    process.exit(1);
  }

  try {
    validateMapName(args.name);
  } catch (err) {
    console.error(`Error: ${err.message}`);
    process.exit(1);
  }

  const isAdjust = args.adjust === true || args.adjust === 'true';
  const outputDir = args['output-dir'] || path.join(__dirname, '..');
  const projectRoot = path.join(__dirname, '..', '..');
  try {
    assertContained(path.resolve(outputDir), path.resolve(projectRoot), 'Output directory');
  } catch (err) {
    console.error(`Error: ${err.message}`);
    process.exit(1);
  }
  const allowOversize = args['allow-oversize'] === true || args['allow-oversize'] === 'true';

  let baseMapData = null;
  if (isAdjust) {
    const basePath = path.join(outputDir, 'maps', args.name, 'map.json');
    if (!fs.existsSync(basePath)) {
      console.error(`Error: Base map not found: ${basePath}`);
      process.exit(1);
    }
    baseMapData = loadMapJson(basePath);
    // Normalize bounds to numbers so string-typed JSON values do not break comparisons.
    if (baseMapData && baseMapData.bounds) {
      baseMapData.bounds.w = Number(baseMapData.bounds.w);
      baseMapData.bounds.h = Number(baseMapData.bounds.h);
    }
  }

  const explicitWidth = args.width !== undefined;
  const explicitHeight = args.height !== undefined;

  const inferredSize = !baseMapData ? inferSizeFromDescription(args.description, config.sizeLevels) : null;

  let width, height;
  try {
    const defaultWidth = baseMapData ? Number(baseMapData.bounds.w) : (inferredSize ? inferredSize.width : DEFAULT_WIDTH);
    const defaultHeight = baseMapData ? Number(baseMapData.bounds.h) : (inferredSize ? inferredSize.height : DEFAULT_HEIGHT);
    width = parseSize(args.width, defaultWidth, 'width', allowOversize);
    height = parseSize(args.height, defaultHeight, 'height', allowOversize);
  } catch (err) {
    console.error(`Error: ${err.message}`);
    process.exit(1);
  }

  if (isAdjust && (width !== baseMapData.bounds.w || height !== baseMapData.bounds.h)) {
    console.error(`Error: Adjust mode cannot change map dimensions. Omit --width/--height or use the base map size (${baseMapData.bounds.w}\u00d7${baseMapData.bounds.h}).`);
    process.exit(1);
  }

  let seed;
  if (args.seed !== undefined) {
    const trimmed = args.seed.trim();
    const parsed = parseInt(trimmed, 10);
    if (String(parsed) !== trimmed || !Number.isFinite(parsed)) {
      console.error(`Error: Invalid seed: "${args.seed}". Must be an integer.`);
      process.exit(1);
    }
    seed = parsed;
  } else {
    seed = Math.floor(Math.random() * 1000000);
  }

  const style = args.style || DEFAULT_STYLE;
  const water = parseRatio(args.water, 'water');
  const obstacle = parseRatio(args.obstacle, 'obstacle');
  const decoration = args.decoration || DEFAULT_DECORATION;

  const validStyles = Object.keys(config.styles);
  const validDecorations = Object.keys(config.decorationDensity);
  if (!validStyles.includes(style)) {
    console.error(`Error: Invalid style: "${style}". Valid styles: ${validStyles.join(', ')}.`);
    process.exit(1);
  }
  if (!validDecorations.includes(decoration)) {
    console.error(`Error: Invalid decoration density: "${decoration}". Valid densities: ${validDecorations.join(', ')}.`);
    process.exit(1);
  }

  const force = args.force === true || args.force === 'true';
  if (isAdjust && force) {
    console.error('Error: --force cannot be used with --adjust.');
    process.exit(1);
  }
  const noSync = args['no-sync'] === true || args['no-sync'] === 'true';

  let count;
  if (force && args.count === undefined) {
    // Force 覆盖单个地图，默认只生成 1 个变体
    count = 1;
  } else if (args.count !== undefined) {
    const trimmed = args.count.trim();
    const parsed = parseInt(trimmed, 10);
    if (String(parsed) !== trimmed || !Number.isFinite(parsed) || parsed < 1) {
      console.error(`Error: Invalid count: "${args.count}". Must be a positive integer.`);
      process.exit(1);
    }
    count = parsed;
  } else {
    count = Math.floor(Math.random() * 3) + 1; // default 1-3 variants
  }

  if (count > 1 && force) {
    console.error('Error: --force cannot be used with --count > 1 because each generated map would overwrite the same folder.');
    process.exit(1);
  }

  const blueprintPath = args.blueprint;
  if (blueprintPath && !fs.existsSync(blueprintPath)) {
    console.error(`Error: blueprint file not found: ${blueprintPath}`);
    process.exit(1);
  }

  try {
    const results = [];
    for (let i = 0; i < count; i++) {
      const result = generateSingleMap({
        requestedName: args.name,
        outputDir,
        width,
        height,
        seed: seed + i,
        style,
        water,
        obstacle,
        decoration,
        blueprintPath,
        force,
        noSync,
        description: args.description,
        isAdjust,
        baseMapData,
        explicitWidth,
        explicitHeight
      });
      results.push(result);
    }

    const actionLabel = isAdjust ? 'Adjusted' : 'Generated';
    for (const result of results) {
      console.log(`${actionLabel} map: ${result.finalName} (${width}x${height}) at ${result.mapDir}`);
    }
  } catch (err) {
    console.error(`Error: ${err.message}`);
    process.exit(1);
  }
}

if (require.main === module) {
  main();
}

module.exports = { parseArgs, generateSingleMap };
