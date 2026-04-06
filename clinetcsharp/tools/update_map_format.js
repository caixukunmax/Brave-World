// 更新现有地图CSV格式，添加 exists 列
const fs = require('fs');
const path = require('path');

const mapFiles = [
    'maps/新手村/map.csv',
    'maps/新手村/map_data.csv'
];

mapFiles.forEach(filePath => {
    if (!fs.existsSync(filePath)) {
        console.log(`跳过不存在的文件: ${filePath}`);
        return;
    }

    const content = fs.readFileSync(filePath, 'utf8');
    const lines = content.split('\n');

    const newLines = lines.map((line, index) => {
        // 保留注释行
        if (line.startsWith('#')) {
            if (line.includes('Format:')) {
                return '# Format: exists;walkable;visible;terrain;height;custom';
            }
            if (line.includes('walkable:')) {
                return '# exists: 0=false, 1=true\n# walkable: 0=false, 1=true';
            }
            return line;
        }

        // 跳过空行
        if (!line.trim()) return line;

        // 更新数据行
        // 原格式: walkable;visible;terrain;height;custom
        // 新格式: exists;walkable;visible;terrain;height;custom
        // 在所有格子数据前添加 "1;"（表示 exists=true）
        return line.split(',').map(cell => {
            if (cell.includes(';')) {
                return '1;' + cell; // 在原有数据前添加 exists=1
            }
            return cell;
        }).join(',');
    });

    fs.writeFileSync(filePath, newLines.join('\n'));
    console.log(`已更新: ${filePath}`);
});

console.log('地图格式更新完成！');
