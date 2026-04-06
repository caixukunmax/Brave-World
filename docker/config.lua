-- tslua2 Skynet 配置
root = "./"

-- 工作线程�?
thread = 8

-- 日志输出�?stdout（Docker 友好�?
logger = nil

-- 单机模式（不使用 harbor 集群�?
harbor = 0

-- 启动脚本
start = "main"
bootstrap = "snlua bootstrap"

-- 预加载：在每�?Lua 服务启动前注�?skynet 全局（供 tstl 生成代码使用�?
preload = root.."preload.lua"

-- C 服务路径
cpath = root.."cservice/?.so"

-- Lua 服务搜索路径
luaservice = root.."service/?.lua;"
            ..root.."lualib/tslua/services/?.lua;"
            ..root.."lualib/tslua/?.lua;"
            ..root.."?.lua"

-- Lua 模块加载�?
lualoader = root.."lualib/loader.lua"

-- Lua require 路径
lua_path = root.."lualib/?.lua;"
         ..root.."lualib/?/init.lua;"
         ..root.."lualib/tslua/?.lua;"
         ..root.."lualib/tslua/?/init.lua;"
         ..root.."lualib/tables/?.lua;"
         ..root.."lualib/tables/data/?.lua"

-- Lua C 模块路径
lua_cpath = root.."luaclib/?.so"

-- Snax 路径
snax = root.."service/?.lua"
