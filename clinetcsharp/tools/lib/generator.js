const { createNoise2D } = require('./noise');
const config = require('../map-gen-config.json');

const terrainById = new Map(
  Object.entries(config.terrains).map(([name, t]) => [t.id, { ...t, name }])
);

function generateTerrain(mapData, options) {
  const seed = options.seed ?? Math.floor(Math.random() * 1000000);
  const noise = createNoise2D(seed);
  const style = config.styles[options.style] || config.styles.mixed;
  const waterRatio = options.water ?? style.water;
  const obstacleRatio = options.obstacle ?? style.obstacle;

  // Only operate on cells that have not been pre-set by a blueprint.
  // Blueprint regions take precedence; noise fills the remaining default cells.
  const cells = Object.values(mapData.cells).filter(c => c.terrain === 0);
  const values = cells.map(c => ({ c, v: noise(c.uid.split('_').map(Number)[0] * 0.1, c.uid.split('_').map(Number)[1] * 0.1) }));
  values.sort((a, b) => a.v - b.v);

  const waterCount = Math.floor(cells.length * waterRatio);
  const obstacleCount = Math.floor(cells.length * obstacleRatio);

  // Reset default cells so previously generated terrain does not leak through.
  cells.forEach(c => { c.terrain = 0; c.decoration = 0; });

  // Water lowest values
  for (let i = 0; i < waterCount; i++) {
    values[i].c.terrain = config.terrains.water.id;
  }

  // Obstacles next lowest values among land
  for (let i = waterCount; i < waterCount + obstacleCount && i < values.length; i++) {
    values[i].c.terrain = config.terrains.rock.id;
  }

  // Apply style bias for remaining land
  const bias = style.terrainBias;
  for (const { c } of values.slice(waterCount + obstacleCount)) {
    if (bias.length > 0) {
      const pick = bias[Math.floor(Math.abs(noise(c.uid.split('_').map(Number)[0], c.uid.split('_').map(Number)[1])) * bias.length) % bias.length];
      c.terrain = config.terrains[pick].id;
    }
  }
}

function countTerrain(mapData) {
  const counts = {};
  for (const cell of Object.values(mapData.cells)) {
    const terrain = terrainById.get(cell.terrain);
    const name = terrain?.name || 'unknown';
    counts[name] = (counts[name] || 0) + 1;
  }
  return counts;
}

function isWalkable(cell) {
  const terrain = terrainById.get(cell.terrain);
  return terrain ? terrain.walkable === true : false;
}

function findLargestConnectedRegion(mapData) {
  const visited = new Set();
  let best = [];
  for (const key of Object.keys(mapData.cells)) {
    if (!isWalkable(mapData.cells[key]) || visited.has(key)) continue;
    const region = [];
    const stack = [key];
    while (stack.length) {
      const k = stack.pop();
      if (visited.has(k)) continue;
      visited.add(k);
      region.push(k);
      const [x, y] = k.split('_').map(Number);
      for (const [dx, dy] of [[0, 1], [0, -1], [1, 0], [-1, 0]]) {
        const nk = `${x + dx}_${y + dy}`;
        if (mapData.cells[nk] && isWalkable(mapData.cells[nk]) && !visited.has(nk)) stack.push(nk);
      }
    }
    if (region.length > best.length) best = region;
  }
  return best;
}

function ensureConnectivity(mapData) {
  const largest = findLargestConnectedRegion(mapData);
  const keep = new Set(largest);
  for (const [key, cell] of Object.entries(mapData.cells)) {
    if (isWalkable(cell) && !keep.has(key)) {
      cell.terrain = config.terrains.rock.id;
    }
  }
  return largest;
}

function getDecorationSize(decId) {
  for (const d of Object.values(config.decorations)) {
    if (d.id === decId) {
      const sx = d.size?.x ?? 1;
      const sy = d.size?.y ?? 1;
      return { x: Math.max(1, sx), y: Math.max(1, sy) };
    }
  }
  return { x: 1, y: 1 };
}

function placeDecorations(mapData, density, styleName, seed) {
  const densityValue = config.decorationDensity[density] || config.decorationDensity.medium;
  const style = config.styles[styleName] || config.styles.mixed;
  const noise = createNoise2D(seed + 1);
  const { x: spawnX, y: spawnY } = mapData.spawn;
  const { x: bx, y: by, w: bw, h: bh } = mapData.bounds;

  // Filter decorations by biome compatibility
  const validDecs = Object.entries(config.decorations)
    .filter(([_, d]) => d.biomes.includes(styleName) || d.biomes.includes('mixed') || styleName === 'mixed')
    .map(([_, d]) => d.id);

  if (validDecs.length === 0) return;

  const candidates = Object.entries(mapData.cells).filter(([key, c]) => {
    const [x, y] = key.split('_').map(Number);
    return isWalkable(c) && !c.decoration && !(x === spawnX && y === spawnY);
  });
  const targetCount = Math.floor(candidates.length * densityValue);

  // Shuffle-ish via noise
  candidates.sort((a, b) => noise(a[0].split('_').map(Number)[0], a[0].split('_').map(Number)[1]) - noise(b[0].split('_').map(Number)[0], b[0].split('_').map(Number)[1]));

  const occupied = new Set();

  function occupiesSpawn(anchorX, anchorY, sizeX, sizeY) {
    return anchorX <= spawnX && spawnX < anchorX + sizeX &&
           anchorY <= spawnY && spawnY < anchorY + sizeY;
  }

  function canPlace(anchorX, anchorY, sizeX, sizeY) {
    if (anchorX < bx || anchorY < by || anchorX + sizeX > bx + bw || anchorY + sizeY > by + bh)
      return false;
    if (occupiesSpawn(anchorX, anchorY, sizeX, sizeY))
      return false;
    for (let dy = 0; dy < sizeY; dy++) {
      for (let dx = 0; dx < sizeX; dx++) {
        const x = anchorX + dx;
        const y = anchorY + dy;
        const key = `${x}_${y}`;
        const cell = mapData.cells[key];
        if (!cell || !isWalkable(cell) || cell.decoration || occupied.has(key))
          return false;
      }
    }
    return true;
  }

  function markOccupied(anchorX, anchorY, sizeX, sizeY) {
    for (let dy = 0; dy < sizeY; dy++) {
      for (let dx = 0; dx < sizeX; dx++) {
        occupied.add(`${anchorX + dx}_${anchorY + dy}`);
      }
    }
  }

  let placed = 0;
  for (const [key] of candidates) {
    if (placed >= targetCount) break;
    const [x, y] = key.split('_').map(Number);

    // Pick decoration type deterministically for this cell
    const decId = validDecs[Math.floor(Math.abs(noise(x, y)) * validDecs.length) % validDecs.length];
    const { x: sizeX, y: sizeY } = getDecorationSize(decId);

    if (!canPlace(x, y, sizeX, sizeY)) continue;

    mapData.cells[key].decoration = decId;
    markOccupied(x, y, sizeX, sizeY);
    placed++;
  }
}

function placeSpawn(mapData, region) {
  if (!region) {
    region = findLargestConnectedRegion(mapData);
  }
  if (region.length === 0) {
    mapData.spawn = { x: 0, y: 0 };
    return;
  }
  const xs = region.map(k => Number(k.split('_')[0]));
  const ys = region.map(k => Number(k.split('_')[1]));
  const cx = Math.floor((Math.min(...xs) + Math.max(...xs)) / 2);
  const cy = Math.floor((Math.min(...ys) + Math.max(...ys)) / 2);
  let best = region[0];
  let bestDist = Infinity;
  for (const key of region) {
    const [x, y] = key.split('_').map(Number);
    const d = Math.abs(x - cx) + Math.abs(y - cy);
    if (d < bestDist) { bestDist = d; best = key; }
  }
  const [sx, sy] = best.split('_').map(Number);
  mapData.spawn = { x: sx, y: sy };
}

module.exports = {
  generateTerrain,
  countTerrain,
  isWalkable,
  findLargestConnectedRegion,
  ensureConnectivity,
  placeSpawn,
  placeDecorations
};
