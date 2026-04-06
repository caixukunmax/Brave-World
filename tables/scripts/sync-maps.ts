/**
 * 地图配置同步脚本
 * TODO: 从客户端同步地图配置到服务器，待接入客户端项目后启用
 */

import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

function syncMaps() {
  console.log('[SyncMaps] 跳过：客户端地图目录未接入');
}

syncMaps();
