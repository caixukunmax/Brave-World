#!/bin/bash
# 把 Docker 环境变量写入 Skynet 运行时配置
cat >> config.lua << EOF
MONGO_HOST = "${MONGO_HOST:-127.0.0.1}"
MONGO_PORT = "${MONGO_PORT:-27017}"
GATEWAY_PORT = "${GATEWAY_PORT:-8889}"
DEBUG_PORT = "${DEBUG_PORT:-8000}"
EOF

exec ./skynet config.lua
