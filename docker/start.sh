#!/bin/bash
# Runtime configuration for Skynet
export LD_LIBRARY_PATH=/app/luaclib:$LD_LIBRARY_PATH

# Export configuration as environment variables
export MONGO_HOST="${MONGO_HOST:-mongo}"
export MONGO_PORT="${MONGO_PORT:-27017}"

exec ./skynet config.lua
