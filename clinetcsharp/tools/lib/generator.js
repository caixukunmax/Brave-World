const { createNoise2D } = require('./noise');
const config = require('../map-gen-config.json');

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
    const name = Object.entries(config.terrains).find(([k, v]) => v.id === cell.terrain)?.[0] || 'unknown';
    counts[name] = (counts[name] || 0) + 1;
  }
  return counts;
}

module.exports = { generateTerrain, countTerrain };
