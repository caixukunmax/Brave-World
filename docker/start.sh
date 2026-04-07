#!/bin/bash
# 把 Docker 环境变量写入 Skynet 运行时配置
export LD_LIBRARY_PATH=/app/luaclib:$LD_LIBRARY_PATH

# config.lua 可能通过 volume 只读挂载，复制到可写位置再追加
cp config.lua /tmp/config.lua
cat >> /tmp/config.lua << EOF
MONGO_HOST = "${MONGO_HOST:-127.0.0.1}"
MONGO_PORT = "${MONGO_PORT:-27017}"
GATEWAY_PORT = "${GATEWAY_PORT:-8889}"
DEBUG_PORT = "${DEBUG_PORT:-8000}"
EOF

exec ./skynet /tmp/config.lua
