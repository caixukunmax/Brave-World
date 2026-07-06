const fs = require('fs');
const path = require('path');

function listExistingMaps(mapsFolder) {
  if (!fs.existsSync(mapsFolder)) return [];
  return fs.readdirSync(mapsFolder)
    .filter(name => fs.existsSync(path.join(mapsFolder, name, 'map.json')));
}

function resolveMapName(requestedName, mapsFolder) {
  const existing = new Set(listExistingMaps(mapsFolder));
  if (!existing.has(requestedName)) return requestedName;
  let maxIndex = 0;
  const re = new RegExp(`^${requestedName}_(\\d+)$`);
  for (const name of existing) {
    const m = name.match(re);
    if (m) maxIndex = Math.max(maxIndex, parseInt(m[1], 10));
  }
  return `${requestedName}_${maxIndex + 1}`;
}

module.exports = { listExistingMaps, resolveMapName };
