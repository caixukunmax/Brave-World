const fs = require('fs');
const path = require('path');

function escapeRegExp(string) {
  return string.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function listExistingMaps(mapsFolder) {
  if (!fs.existsSync(mapsFolder)) return [];
  return fs.readdirSync(mapsFolder)
    .filter(name => fs.existsSync(path.join(mapsFolder, name, 'map.json')));
}

function resolveMapName(requestedName, mapsFolder) {
  const existing = new Set(listExistingMaps(mapsFolder));
  if (!existing.has(requestedName)) return requestedName;
  let maxIndex = 0;
  const re = new RegExp(`^${escapeRegExp(requestedName)}_(\\d+)$`);
  for (const name of existing) {
    const m = name.match(re);
    if (m) maxIndex = Math.max(maxIndex, parseInt(m[1], 10));
  }
  return `${requestedName}_${maxIndex + 1}`;
}

module.exports = { escapeRegExp, listExistingMaps, resolveMapName };
