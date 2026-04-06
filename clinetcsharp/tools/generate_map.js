// 生成默认地图CSV数据
const fs = require('fs');
const path = 'maps/新手村/map.csv';

let lines = [
    '# Grid Map Data',
    '# Format: exists;walkable;visible;terrain;height;custom',
    '# exists: 0=false, 1=true',
    '# walkable: 0=false, 1=true',
    '# visible: 0=false, 1=true',
    '# terrain: 0=normal, 1=water, 2=grass, 3=sand, 4=rock...',
    '# height: 0-9'
];

for (let y = 0; y < 50; y++) {
    let row = [];
    for (let x = 0; x < 50; x++) {
        row.push('1;1;1;0;0;'); // exists=1, walkable=1, visible=1, terrain=0, height=0, custom=''
    }
    lines.push(row.join(','));
}

fs.writeFileSync(path, lines.join('\n'));
console.log('Generated 50x50 map.csv');
