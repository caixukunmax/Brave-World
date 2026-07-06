const config = require('../map-gen-config.json');

const SIZES = { small: 4, medium: 8, large: 14 };

function getAnchorPoint(mapData, anchor) {
  const { x, y, w, h } = mapData.bounds;
  const cx = x + Math.floor(w / 2);
  const cy = y + Math.floor(h / 2);
  switch (anchor) {
    case 'center': return { x: cx, y: cy };
    case 'north': return { x: cx, y: y + Math.floor(h * 0.15) };
    case 'south': return { x: cx, y: y + Math.floor(h * 0.85) };
    case 'east': return { x: x + Math.floor(w * 0.85), y: cy };
    case 'west': return { x: x + Math.floor(w * 0.15), y: cy };
    case 'northeast': return { x: x + Math.floor(w * 0.75), y: y + Math.floor(h * 0.15) };
    case 'northwest': return { x: x + Math.floor(w * 0.25), y: y + Math.floor(h * 0.15) };
    case 'southeast': return { x: x + Math.floor(w * 0.75), y: y + Math.floor(h * 0.85) };
    case 'southwest': return { x: x + Math.floor(w * 0.25), y: y + Math.floor(h * 0.85) };
    default: return { x: cx, y: cy };
  }
}

function paintRegion(mapData, center, radius, terrainId) {
  for (let dy = -radius; dy <= radius; dy++) {
    for (let dx = -radius; dx <= radius; dx++) {
      if (dx * dx + dy * dy > radius * radius) continue;
      const x = center.x + dx;
      const y = center.y + dy;
      const key = `${x}_${y}`;
      if (mapData.cells[key]) {
        mapData.cells[key].terrain = terrainId;
      }
    }
  }
}

function applyBlueprint(mapData, blueprint) {
  if (!blueprint || !blueprint.regions) return;
  for (const region of blueprint.regions) {
    const terrainId = config.terrains[region.type]?.id;
    if (terrainId === undefined) continue;
    const center = getAnchorPoint(mapData, region.anchor);
    const radius = SIZES[region.size] || SIZES.medium;
    paintRegion(mapData, center, radius, terrainId);
  }
}

module.exports = { applyBlueprint, getAnchorPoint };
