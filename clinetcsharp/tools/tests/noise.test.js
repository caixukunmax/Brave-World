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
});
