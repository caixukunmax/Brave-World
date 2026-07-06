#!/usr/bin/env node
const fs = require('fs');
const path = require('path');
const { createMapData, saveMapJson, loadMapJson } = require('./lib/map-core');
const { generateTerrain, ensureConnectivity, placeSpawn, placeDecorations } = require('./lib/generator');
const { applyBlueprint } = require('./lib/blueprint');
const { resolveMapName } = require('./lib/naming');
const { writeForm } = require('./lib/form');
const { syncToServer } = require('./lib/sync');

const DEFAULT_WIDTH = 30;
const DEFAULT_HEIGHT = 30;
const DEFAULT_STYLE = 'mixed';
const DEFAULT_DECORATION = 'medium';

function cloneCells(cells) {
  const out = {};
  for (const [k, v] of Object.entries(cells)) {
    out[k] = { uid: k, terrain: 0, height: 0, custom: '', decoration: 0, ...v };
  }
  return out;
}

function parseArgs(argv) {
  const args = {};
  for (let i = 2; i < argv.length; i++) {
    const arg = argv[i];
    if (!arg.startsWith('--')) continue;
    const key = arg.slice(2);
    const next = argv[i + 1];
    if (next !== undefined && !next.startsWith('--')) {
      args[key] = next;
      i++;
    } else {
      args[key] = true;
    }
  }
  return args;
}

function parseSize(value, defaultValue, label, allowOversize) {
  if (value === undefined) return defaultValue;
  const trimmed = value.trim();
  const n = parseInt(trimmed, 10);
  if (String(n) !== trimmed || !Number.isFinite(n) || n <= 0) {
    throw new Error(`Invalid ${label}: "${value}". Must be a positive integer.`);
  }
  if (n > 50 && !allowOversize) {
    throw new Error(
      `Map ${label} ${n} exceeds the default maximum size of 50. ` +
      `To generate a map larger than 50, confirm with the user and pass --allow-oversize.`
    );
  }
  return n;
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
    baseMapData
  } = options;

  const mapsFolder = path.join(outputDir, 'maps');
  const finalName = force ? requestedName : resolveMapName(requestedName, mapsFolder);
  const mapDir = path.join(mapsFolder, finalName);

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
  if (blueprintPath && fs.existsSync(blueprintPath)) {
    const blueprint = JSON.parse(fs.readFileSync(blueprintPath, 'utf8'));
    applyBlueprint(mapData, blueprint);
  }
  generateTerrain(mapData, { style, water, obstacle, seed });

  const largestRegion = ensureConnectivity(mapData);
  placeSpawn(mapData, largestRegion);
  placeDecorations(mapData, decoration, style, seed);

  try {
    saveMapJson(mapData, path.join(mapDir, 'map.json'));

    let syncResult = { skipped: true, reason: 'disabled via --no-sync' };
    if (!noSync) {
      const serverRoot = path.join(__dirname, '..', '..', 'servercsharp', 'data');
      syncResult = syncToServer(finalName, path.join(mapDir, 'map.json'), serverRoot);
      if (syncResult.skipped) console.log(`[sync] skipped: ${syncResult.reason}`);
    }

    const formOptions = {
      requestedName,
      finalName,
      mode: isAdjust ? '调整已有地图' : '生成新地图',
      baseMap: isAdjust ? requestedName : undefined,
      width,
      height,
      seed,
      description: description || '-',
      style,
      water: water ?? 'auto',
      obstacle: obstacle ?? 'auto',
      decoration,
      force: !!force,
      outputPath: mapDir,
      syncServer: !syncResult.skipped
    };
    writeForm(path.join(mapDir, 'map-gen-form.md'), formOptions);
  } catch (err) {
    fs.rmSync(mapDir, { recursive: true, force: true });
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

  const isAdjust = args.adjust === true || args.adjust === 'true';
  const outputDir = args['output-dir'] || path.join(__dirname, '..');
  const allowOversize = args['allow-oversize'] === true || args['allow-oversize'] === 'true';

  let baseMapData = null;
  if (isAdjust) {
    const basePath = path.join(outputDir, 'maps', args.name, 'map.json');
    if (!fs.existsSync(basePath)) {
      console.error(`Error: Base map not found: ${basePath}`);
      process.exit(1);
    }
    baseMapData = loadMapJson(basePath);
  }

  let width, height;
  try {
    const defaultWidth = baseMapData ? baseMapData.bounds.w : DEFAULT_WIDTH;
    const defaultHeight = baseMapData ? baseMapData.bounds.h : DEFAULT_HEIGHT;
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
  const water = args.water !== undefined ? parseFloat(args.water) : undefined;
  const obstacle = args.obstacle !== undefined ? parseFloat(args.obstacle) : undefined;
  const decoration = args.decoration || DEFAULT_DECORATION;
  const force = args.force === true || args.force === 'true';
  const noSync = args['no-sync'] === true || args['no-sync'] === 'true';

  let count;
  if (args.count !== undefined) {
    const trimmed = args.count.trim();
    const parsed = parseInt(trimmed, 10);
    if (String(parsed) !== trimmed || !Number.isFinite(parsed) || parsed < 1) {
      console.error(`Error: Invalid count: "${args.count}". Must be a positive integer.`);
      process.exit(1);
    }
    count = parsed;
  } else {
    count = 1;
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
        baseMapData
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
