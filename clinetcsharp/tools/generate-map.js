#!/usr/bin/env node
const fs = require('fs');
const path = require('path');
const { createMapData, saveMapJson } = require('./lib/map-core');
const { generateTerrain, ensureConnectivity, placeSpawn, placeDecorations } = require('./lib/generator');
const { applyBlueprint } = require('./lib/blueprint');
const { resolveMapName } = require('./lib/naming');
const { writeForm } = require('./lib/form');
const { syncToServer } = require('./lib/sync');

const DEFAULT_WIDTH = 30;
const DEFAULT_HEIGHT = 30;
const DEFAULT_STYLE = 'mixed';
const DEFAULT_DECORATION = 'medium';

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

function parseSize(value, defaultValue) {
  const n = parseInt(value, 10);
  if (!Number.isFinite(n) || n <= 0) return defaultValue;
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
    description
  } = options;

  const mapsFolder = path.join(outputDir, 'maps');
  const finalName = force ? requestedName : resolveMapName(requestedName, mapsFolder);
  const mapDir = path.join(mapsFolder, finalName);

  const mapData = createMapData(width, height, finalName);

  if (blueprintPath && fs.existsSync(blueprintPath)) {
    const blueprint = JSON.parse(fs.readFileSync(blueprintPath, 'utf8'));
    applyBlueprint(mapData, blueprint);
  }

  generateTerrain(mapData, { style, water, obstacle, seed });
  const largestRegion = ensureConnectivity(mapData);
  placeSpawn(mapData, largestRegion);
  placeDecorations(mapData, decoration, style, seed);

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
    mode: '生成新地图',
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

  return { finalName, mapDir };
}

function main() {
  const args = parseArgs(process.argv);

  if (!args.name) {
    console.error('Usage: node generate-map.js --name <name> [--description <desc>] [--count <n>] [--width <w>] [--height <h>] [--seed <n>] [--style <style>] [--water <ratio>] [--obstacle <ratio>] [--decoration <low|medium|high>] [--force] [--output-dir <dir>] [--blueprint <path>] [--no-sync]');
    process.exit(1);
  }

  const width = parseSize(args.width, DEFAULT_WIDTH);
  const height = parseSize(args.height, DEFAULT_HEIGHT);

  const seed = parseInt(args.seed, 10) || Math.floor(Math.random() * 1000000);
  const style = args.style || DEFAULT_STYLE;
  const water = args.water !== undefined ? parseFloat(args.water) : undefined;
  const obstacle = args.obstacle !== undefined ? parseFloat(args.obstacle) : undefined;
  const decoration = args.decoration || DEFAULT_DECORATION;
  const outputDir = args['output-dir'] || path.join(__dirname, '..');
  const force = args.force === true || args.force === 'true';
  const noSync = args['no-sync'] === true || args['no-sync'] === 'true';
  const count = Math.max(1, parseInt(args.count, 10) || 1);

  const blueprintPath = args.blueprint;
  if (blueprintPath && !fs.existsSync(blueprintPath)) {
    console.error(`Error: blueprint file not found: ${blueprintPath}`);
    process.exit(1);
  }

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
      description: args.description
    });
    results.push(result);
  }

  for (const result of results) {
    console.log(`Generated map: ${result.finalName} (${width}x${height}) at ${result.mapDir}`);
  }
}

if (require.main === module) {
  main();
}

module.exports = { parseArgs, generateSingleMap };
