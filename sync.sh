#!/bin/bash
# 增量同步 skynet_src/ → docker/
# 只同步业务文件，不动 Docker 基础设施文件

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
SRC="$SCRIPT_DIR/skynet_src"
DST="$SCRIPT_DIR/docker"

echo "=== 同步 skynet_src → docker ==="

# 1. 同步整个 game 目录（包含 config/main/common 和所有服务）
rsync -av --delete \
    "$SRC/game/" "$DST/game/"

# 2. 同步配置表和协议文件
rsync -av --delete \
    "$SRC/tables/" "$DST/tables/"
rsync -av --delete \
    "$SRC/protos/" "$DST/protos/"

echo "=== 同步完成 ==="
echo "提示: docker-compose restart game-server 重启服务"
