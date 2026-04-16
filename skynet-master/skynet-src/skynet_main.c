/**
 * ============================================================================
 * Skynet 入口文件 - skynet_main.c
 * ============================================================================
 * 
 * 【文件作用】
 * 这是 Skynet 框架的入口文件，负责：
 * 1. 解析命令行参数（读取配置文件路径）
 * 2. 加载并解析配置文件（Lua 格式）
 * 3. 初始化全局环境
 * 4. 调用 skynet_start() 启动框架
 * 
 * 【执行流程】
 * main() -> 加载配置 -> 设置环境变量 -> 读取配置参数 -> skynet_start()
 * 
 * 【配置文件】
 * 使用 Lua 语法，支持 include 嵌套，支持环境变量替换（$VAR）
 * ============================================================================
 */

#include "skynet.h"

#include "skynet_imp.h"      // 内部实现接口
#include "skynet_env.h"      // 环境变量管理
#include "skynet_server.h"   // 服务器核心

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <lua.h>
#include <lualib.h>
#include <lauxlib.h>
#include <signal.h>
#include <assert.h>

// 最大工作线程数限制
#ifndef SKYNET_MAXTHREAD
#define SKYNET_MAXTHREAD 1024
#endif

/**
 * 【配置读取辅助函数】
 * 从环境变量中读取整数配置，如果不存在则使用默认值
 * 
 * @param key  配置项名称
 * @param opt  默认值
 * @return     配置值（整数）
 */
static int
optint(const char *key, int opt) {
	const char * str = skynet_getenv(key);
	if (str == NULL) {
		char tmp[20];
		sprintf(tmp,"%d",opt);
		skynet_setenv(key, tmp);  // 将默认值写入环境
		return opt;
	}
	return strtol(str, NULL, 10);
}

/**
 * 【配置读取辅助函数】
 * 从环境变量中读取布尔配置
 * 
 * @param key  配置项名称
 * @param opt  默认值
 * @return     配置值（0 或 1）
 */
static int
optboolean(const char *key, int opt) {
	const char * str = skynet_getenv(key);
	if (str == NULL) {
		skynet_setenv(key, opt ? "true" : "false");
		return opt;
	}
	return strcmp(str,"true")==0;
}

/**
 * 【配置读取辅助函数】
 * 从环境变量中读取字符串配置
 * 
 * @param key  配置项名称
 * @param opt  默认值
 * @return     配置值（字符串）
 */
static const char *
optstring(const char *key,const char * opt) {
	const char * str = skynet_getenv(key);
	if (str == NULL) {
		if (opt) {
			skynet_setenv(key, opt);
			opt = skynet_getenv(key);
		}
		return opt;
	}
	return str;
}

/**
 * 【初始化环境变量】
 * 将 Lua 配置表中的所有键值对导入 Skynet 环境系统
 * 
 * 配置表示例：
 * {
 *     thread = 8,
 *     harbor = 1,
 *     bootstrap = "snlua bootstrap"
 * }
 * 
 * @param L Lua 状态机，栈顶是配置表
 */
static void
_init_env(lua_State *L) {
	lua_pushnil(L);  /* 第一个 key */
	// lua_next: 弹出 key，压入下一对 key-value
	while (lua_next(L, -2) != 0) {
		int keyt = lua_type(L, -2);
		if (keyt != LUA_TSTRING) {
			fprintf(stderr, "Invalid config table\n");
			exit(1);
		}
		const char * key = lua_tostring(L,-2);
		if (lua_type(L,-1) == LUA_TBOOLEAN) {
			// 布尔值转换为 "true" 或 "false"
			int b = lua_toboolean(L,-1);
			skynet_setenv(key,b ? "true" : "false" );
		} else {
			const char * value = lua_tostring(L,-1);
			if (value == NULL) {
				fprintf(stderr, "Invalid config table key = %s\n", key);
				exit(1);
			}
			skynet_setenv(key,value);
		}
		lua_pop(L,1);  // 弹出 value，保留 key 用于下一次迭代
	}
	lua_pop(L,1);  // 弹出配置表
}

/**
 * 【信号处理】
 * 忽略 SIGPIPE 信号，防止向已关闭的 socket 写入时程序崩溃
 */
int sigign() {
	struct sigaction sa;
	sa.sa_handler = SIG_IGN;  // 忽略信号
	sa.sa_flags = 0;
	sigemptyset(&sa.sa_mask);
	sigaction(SIGPIPE, &sa, 0);
	return 0;
}

/**
 * 【配置加载 Lua 代码】
 * 这段 Lua 代码用于加载和解析用户的配置文件
 * 
 * 功能：
 * 1. 支持 include 指令嵌套加载其他配置文件
 * 2. 支持环境变量替换（$VARNAME）
 * 3. 返回配置表
 */
static const char * load_config = "\
	local result = {}\n\
	local function getenv(name) return assert(os.getenv(name), [[os.getenv() failed: ]] .. name) end\n\
	local sep = package.config:sub(1,1)\n\
	local current_path = [[.]]..sep\n\
	local function include(filename)\n\
		local last_path = current_path\n\
		local path, name = filename:match([[(.*]]..sep..[[)(.*)$]])\n\
		if path then\n\
			if path:sub(1,1) == sep then	-- 绝对路径\n\
				current_path = path\n\
			else\n\
				current_path = current_path .. path\n\
			end\n\
		else\n\
			name = filename\n\
		end\n\
		local f = assert(io.open(current_path .. name))\n\
		local code = assert(f:read [[*a]])\n\
		code = string.gsub(code, [[%$([%w_%d]+)]], getenv)\n\
		f:close()\n\
		assert(load(code,[[@]]..filename,[[t]],result))()\n\
		current_path = last_path\n\
	end\n\
	setmetatable(result, { __index = { include = include } })\n\
	local config_name = ...\n\
	include(config_name)\n\
	setmetatable(result, nil)\n\
	return result\n\
";

/**
 * 【主函数】
 * Skynet 框架入口点
 * 
 * 执行步骤：
 * 1. 检查命令行参数（需要配置文件）
 * 2. 初始化全局系统和环境系统
 * 3. 设置信号处理
 * 4. 加载配置文件（Lua）
 * 5. 读取各项配置参数
 * 6. 启动 Skynet
 */
int
main(int argc, char *argv[]) {
	const char * config_file = NULL ;
	if (argc > 1) {
		config_file = argv[1];
	} else {
		fprintf(stderr, "Need a config file. Please read skynet wiki : https://github.com/cloudwu/skynet/wiki/Config\n"
			"usage: skynet configfilename\n");
		return 1;
	}

	// 【步骤1】初始化全局系统和环境系统
	skynet_globalinit();   // 初始化全局节点信息
	skynet_env_init();     // 初始化环境变量系统

	// 【步骤2】忽略 SIGPIPE 信号
	sigign();

	struct skynet_config config;

#ifdef LUA_CACHELIB
	// 初始化 Lua 代码缓存的锁
	luaL_initcodecache();
#endif

	// 【步骤3】创建 Lua 状态机并加载配置
	struct lua_State *L = luaL_newstate();
	luaL_openlibs(L);	// 加载 Lua 标准库

	// 加载配置解析代码
	int err =  luaL_loadbufferx(L, load_config, strlen(load_config), "=[skynet config]", "t");
	assert(err == LUA_OK);
	lua_pushstring(L, config_file);  // 配置文件名作为参数

	// 执行配置加载
	err = lua_pcall(L, 1, 1, 0);
	if (err) {
		fprintf(stderr,"%s\n",lua_tostring(L,-1));
		lua_close(L);
		return 1;
	}
	// 将 Lua 配置表导入环境系统
	_init_env(L);
	lua_close(L);

	// 【步骤4】读取各项配置参数
	// thread: 工作线程数（默认8）
	config.thread =  optint("thread",8);
	if (config.thread < 1 || config.thread > SKYNET_MAXTHREAD) {
		fprintf(stderr, "Invalid thread %d , should be in [1,%d]\n", config.thread, SKYNET_MAXTHREAD);
		return 1;
	}
	// module_path: C 服务模块搜索路径
	config.module_path = optstring("cpath","./cservice/?.so");
	// harbor: 节点ID（用于集群）
	config.harbor = optint("harbor", 1);
	// bootstrap: 启动服务命令（默认启动 snlua bootstrap）
	config.bootstrap = optstring("bootstrap","snlua bootstrap");
	// daemon: 守护进程配置
	config.daemon = optstring("daemon", NULL);
	// logger: 日志文件路径
	config.logger = optstring("logger", NULL);
	// logservice: 日志服务名称
	config.logservice = optstring("logservice", "logger");
	// profile: 是否开启性能分析
	config.profile = optboolean("profile", 1);

	// 【步骤5】启动 Skynet！
	skynet_start(&config);
	skynet_globalexit();

	return 0;
}
