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

  const cells = Object.values(mapData.cells);
  const values = cells.map(c => ({ c, v: noise(c.uid.split('_').map(Number)[0] * 0.1, c.uid.split('_').map(Number)[1] * 0.1) }));
  values.sort((a, b) => a.v - b.v);

  const waterCount = Math.floor(cells.length * waterRatio);
  const obstacleCount = Math.floor(cells.length * obstacleRatio);

  // Reset
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

function placeDecorations(mapData, density, styleName, seed) {
  const densityValue = config.decorationDensity[density] || config.decorationDensity.medium;
  const style = config.styles[styleName] || config.styles.mixed;
  const noise = createNoise2D(seed + 1);
  const candidates = Object.entries(mapData.cells).filter(([_, c]) => isWalkable(c) && !c.decoration);
  const targetCount = Math.floor(candidates.length * densityValue);

  // Filter decorations by biome compatibility
  const validDecs = Object.entries(config.decorations)
    .filter(([_, d]) => d.biomes.includes(styleName) || d.biomes.includes('mixed') || styleName === 'mixed')
    .map(([_, d]) => d.id);

  if (validDecs.length === 0) return;

  // Shuffle-ish via noise
  candidates.sort((a, b) => noise(a[0].split('_').map(Number)[0], a[0].split('_').map(Number)[1]) - noise(b[0].split('_').map(Number)[0], b[0].split('_').map(Number)[1]));

  for (let i = 0; i < Math.min(targetCount, candidates.length); i++) {
    const [key, cell] = candidates[i];
    cell.decoration = validDecs[Math.floor(Math.abs(noise(...key.split('_').map(Number))) * validDecs.length) % validDecs.length];
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
