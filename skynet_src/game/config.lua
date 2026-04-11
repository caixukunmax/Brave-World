-- skynet_src/game Skynet 配置
root = "./"

-- 工作线程数
thread = 8

-- 日志输出到 stdout（Docker 友好）
logger = nil

-- 单机模式
harbor = 0

-- 启动脚本
start = "game/main"
bootstrap = "snlua bootstrap"

-- 预加载脚本（每个 Lua 服务启动前注入全局变量）
preload = root.."preload.lua"

-- C 服务路径
cpath = root.."cservice/?.so"

-- Lua 服务搜索路径
luaservice = root.."service/?.lua;"
            ..root.."?/service.lua;"
            ..root.."?.lua"

-- Lua 模块加载器
lualoader = root.."lualib/loader.lua"

-- Lua require 路径
lua_path = root.."lualib/?.lua;"
         ..root.."lualib/?/init.lua;"
         ..root.."game/?.lua;"
         ..root.."?.lua;"
         ..root.."?/?.lua;"
         ..root.."tables/?.lua;"
         ..root.."tables/data/?.lua"

-- Lua C 模块路径
lua_cpath = root.."luaclib/?.so"

-- Snax 路径
snax = root.."?/service.lua"
