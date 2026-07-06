const fs = require('fs');
const path = require('path');

const SAFE_NAME_RE = /^(?!.*\.\.)[a-zA-Z0-9\u4e00-\u9fff\u3040-\u309f\u30a0-\u30ff\uac00-\ud7af._-]+$/;

function validateMapName(name) {
  if (!name || typeof name !== 'string') {
    throw new Error('Map name is required.');
  }
  if (name === '.' || name === '..') {
    throw new Error(`Invalid map name: "${name}". Names may not be "." or "..".`);
  }
  if (!SAFE_NAME_RE.test(name)) {
    throw new Error(
      `Invalid map name: "${name}". Names may contain letters, digits, underscore, hyphen, dot, and CJK characters, ` +
      'and must not contain path separators or "..".'
    );
  }
}

function assertContained(childPath, parentPath, label) {
  const resolvedChildRaw = path.resolve(childPath);
  const resolvedParentRaw = path.resolve(parentPath);
  const [resolvedChild, resolvedParent] = process.platform === 'win32'
    ? [resolvedChildRaw.toLowerCase(), resolvedParentRaw.toLowerCase()]
    : [resolvedChildRaw, resolvedParentRaw];
  const prefix = resolvedParent.endsWith(path.sep) ? resolvedParent : resolvedParent + path.sep;
  if (resolvedChild !== resolvedParent && !resolvedChild.startsWith(prefix)) {
    throw new Error(`${label} "${resolvedChildRaw}" escapes the allowed folder "${resolvedParentRaw}".`);
  }
}

function createCell(x, y, terrain = 0) {
  return {
    uid: `${x}_${y}`,
    terrain,
    height: 0,
    custom: ''
  };
}

function createMapData(width, height, displayName) {
  const cells = {};
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const cell = createCell(x, y, 0);
      cells[cell.uid] = cell;
    }
  }
  return {
    version: 3,
    display_name: displayName,
    bounds: { x: 0, y: 0, w: width, h: height },
    spawn: { x: Math.floor(width / 2), y: Math.floor(height / 2) },
    cells
  };
}

function saveMapJson(mapData, filePath) {
  const dir = path.dirname(filePath);
  if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
  fs.writeFileSync(filePath, JSON.stringify(mapData, null, '  '), 'utf8');
}

function loadMapJson(filePath) {
  const text = fs.readFileSync(filePath, 'utf8');
  return JSON.parse(text);
}

function calculateBounds(cells) {
  const keys = Object.keys(cells);
  if (keys.length === 0) return { x: 0, y: 0, w: 50, h: 50 };
  let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
  for (const uid of keys) {
    const [x, y] = uid.split('_').map(Number);
    minX = Math.min(minX, x);
    minY = Math.min(minY, y);
    maxX = Math.max(maxX, x);
    maxY = Math.max(maxY, y);
  }
  return { x: minX, y: minY, w: maxX - minX + 1, h: maxY - minY + 1 };
}

module.exports = { createCell, createMapData, saveMapJson, loadMapJson, calculateBounds, validateMapName, assertContained };
