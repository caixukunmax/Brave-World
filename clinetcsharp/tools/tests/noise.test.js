const { describe, it } = require('node:test');
const assert = require('node:assert');
const { createNoise2D } = require('../lib/noise');

describe('noise', () => {
  it('returns deterministic values for the same seed', () => {
    const n1 = createNoise2D(123);
    const n2 = createNoise2D(123);
    assert.strictEqual(n1(0.5, 1.5), n2(0.5, 1.5));
  });

  it('returns values in [-1, 1]', () => {
    const n = createNoise2D(42);
    for (let x = 0; x < 10; x++) {
      const v = n(x * 0.5, x * 0.3);
      assert(v >= -1 && v <= 1);
    }
  });

  it('makes seed 0 distinct from seed 1', () => {
    const n0 = createNoise2D(0);
    const n1 = createNoise2D(1);
    const v0 = n0(0.5, 1.5);
    const v1 = n1(0.5, 1.5);
    assert.notStrictEqual(v0, v1, 'seed 0 and seed 1 should produce different noise values');
  });
});
