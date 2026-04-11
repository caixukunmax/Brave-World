#!/bin/bash
export LD_LIBRARY_PATH=/app/luaclib:$LD_LIBRARY_PATH

exec ./skynet game/config.lua
