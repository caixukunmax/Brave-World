// 一次性迁移脚本：将旧地形装饰 ID 替换为新 ID
// 10000 -> 100000 (Tree), 10003 -> 110000 (Grass)
const fs = require('fs');
const path = require('path');

const MAP_DIRS = [
    path.join(__dirname, '..', 'maps'),
    path.join(__dirname, '..', '..', 'servercsharp', 'data', 'maps'),
    path.join(__dirname, '..', '..', 'tables', 'datas', 'maps'),
];

const MIGRATION = {
    10000: 100000, // Tree
    10002: 90000,  // Rock
    10003: 110000, // Grass
};

function migrateMap(filePath) {
    const buffer = fs.readFileSync(filePath);
    // 去除 BOM
    let text = buffer.toString('utf8');
    if (text.charCodeAt(0) === 0xFEFF) {
        text = text.slice(1);
    }
    const mapData = JSON.parse(text);

    let changed = false;
    if (mapData.cells) {
        for (const key of Object.keys(mapData.cells)) {
            const cell = mapData.cells[key];
            if (cell && cell.decoration !== undefined && MIGRATION[cell.decoration] !== undefined) {
                cell.decoration = MIGRATION[cell.decoration];
                changed = true;
            }
        }
    }

    if (changed) {
        fs.writeFileSync(filePath, JSON.stringify(mapData, null, 2) + '\n', 'utf8');
        console.log(`[Migrated] ${filePath}`);
    } else {
        console.log(`[NoChange] ${filePath}`);
    }
}

function main() {
    for (const dir of MAP_DIRS) {
        if (!fs.existsSync(dir)) {
            console.log(`[Skip] ${dir} does not exist`);
            continue;
        }
        for (const name of fs.readdirSync(dir)) {
            const mapFile = path.join(dir, name, 'map.json');
            if (fs.existsSync(mapFile)) {
                migrateMap(mapFile);
            }
        }
    }
    console.log('Migration done.');
}

main();
