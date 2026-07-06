const { describe, it } = require('node:test');
const assert = require('node:assert');
const { generateFormMarkdown } = require('../lib/form');

describe('form', () => {
  it('includes all required fields', () => {
    const md = generateFormMarkdown({
      requestedName: '遗迹沼泽',
      finalName: '遗迹沼泽_2',
      mode: '生成新地图',
      width: 42,
      height: 38,
      seed: 123,
      description: '潮湿的沼泽，中央有废墟',
      style: 'swamp',
      water: 0.35,
      obstacle: 0.15,
      decoration: 'high',
      force: false,
      outputPath: 'clinetcsharp/maps/遗迹沼泽_2/map.json'
    });
    assert(md.includes('遗迹沼泽_2'));
    assert(md.includes('潮湿的沼泽'));
    assert(md.includes('42'));
    assert(md.includes('123'));
    assert(md.includes('swamp'));
  });
});
