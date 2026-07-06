const fs = require('fs');
const path = require('path');

const DEFAULT_WIDTH = 50;
const DEFAULT_HEIGHT = 50;
const DEFAULT_SPAWN_X = 25;
const DEFAULT_SPAWN_Y = 25;

function syncToServer(mapName, clientMapPath, serverRoot) {
  const serverMapsDir = path.join(serverRoot, 'maps');
  if (!fs.existsSync(serverMapsDir)) {
    return { skipped: true, reason: 'server maps directory missing' };
  }

  const targetDir = path.join(serverMapsDir, mapName);
  if (!fs.existsSync(targetDir)) fs.mkdirSync(targetDir, { recursive: true });
  const targetPath = path.join(targetDir, 'map.json');
  fs.copyFileSync(clientMapPath, targetPath);

  const mapData = readMapData(clientMapPath);
  updateRegistry(serverRoot, mapName, mapData);
  return { skipped: false };
}

function readMapData(clientMapPath) {
  try {
    return JSON.parse(fs.readFileSync(clientMapPath, 'utf8'));
  } catch {
    return {};
  }
}

function updateRegistry(serverRoot, mapName, mapData) {
  const registryPath = path.join(serverRoot, 'map_registry.json');
  if (!fs.existsSync(registryPath)) {
    return { skipped: true, reason: 'registry missing' };
  }

  const registry = JSON.parse(fs.readFileSync(registryPath, 'utf8'));
  const bounds = mapData && mapData.bounds ? mapData.bounds : {};
  const spawn = mapData && mapData.spawn ? mapData.spawn : {};

  const width = Number.isFinite(bounds.w) ? bounds.w :
                Number.isFinite(bounds.width) ? bounds.width : DEFAULT_WIDTH;
  const height = Number.isFinite(bounds.h) ? bounds.h :
                 Number.isFinite(bounds.height) ? bounds.height : DEFAULT_HEIGHT;

  const entry = {
    map_name: mapName,
    display_name: (mapData && mapData.display_name) || mapName,
    width,
    height,
    spawn_x: Number.isFinite(spawn.x) ? spawn.x : DEFAULT_SPAWN_X,
    spawn_y: Number.isFinite(spawn.y) ? spawn.y : DEFAULT_SPAWN_Y
  };

  const existingIndex = registry.findIndex(e => e.map_name === mapName);
  if (existingIndex >= 0) {
    registry[existingIndex] = entry;
  } else {
    registry.push(entry);
  }

  fs.writeFileSync(registryPath, JSON.stringify(registry, null, 2), 'utf8');
  return { skipped: false };
}

module.exports = { syncToServer };
