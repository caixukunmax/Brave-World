/**
 * ============================================================================
 * Skynet Lua 服务核心模块 - service_snlua.c
 * ============================================================================
 * 
 * 【文件作用】
 * 这是 Skynet 运行 Lua 服务的核心 C 模块，所有 Lua 服务都是它的实例。
 * 
 * 【核心功能】
 * 1. 创建独立的 Lua 虚拟机（Lua State）
 * 2. 加载并执行 Lua 服务脚本
 * 3. 拦截 Skynet 消息并转发给 Lua 层处理
 * 4. 提供性能分析（profile）功能
 * 5. 内存管理和限制
 * 6. 信号处理（支持中断 Lua 执行）
 * 
 * 【创建流程】
 * snlua_create() → 创建 Lua VM
 * snlua_init()   → 注册回调，发送初始化消息
 * launch_cb()    → 收到消息后调用 init_cb()
 * init_cb()      → 加载 loader.lua，执行用户脚本
 * 
 * 【消息流转】
 * C 层(skynet_socket/server) → snlua 回调 → Lua VM → 业务脚本
 * ============================================================================
 */

#include "skynet.h"
#include "atomic.h"

#include <lua.h>
#include <lualib.h>
#include <lauxlib.h>

#include <assert.h>
#include <string.h>
#include <stdlib.h>
#include <stdio.h>
#include <time.h>

#if defined(__APPLE__)
#include <mach/task.h>
#include <mach/mach.h>
#endif

#define NANOSEC 1000000000   // 1秒 = 10亿纳秒
#define MICROSEC 1000000     // 1秒 = 100万微秒

// #define DEBUG_LOG  // 调试日志开关

#define MEMORY_WARNING_REPORT (1024 * 1024 * 32)  // 内存警告阈值：32MB

/**
 * 【数据结构】snlua 实例
 * 
 * 每个 Lua 服务对应一个 snlua 结构体
 */
struct snlua {
	lua_State * L;              // Lua 虚拟机主线程
	struct skynet_context * ctx;// Skynet 服务上下文
	size_t mem;                 // 当前内存使用量
	size_t mem_report;          // 下次报告内存的阈值
	size_t mem_limit;           // 内存限制（0=无限制）
	lua_State * activeL;        // 当前正在执行的 Lua 线程（用于信号中断）
	ATOM_INT trap;              // 信号陷阱标记（0=正常，1=设置中，-1=已设置）
};

/**
 * 【代码缓存】
 * 
 * 支持共享 Lua proto（字节码）的补丁版本
 * 如果没有打补丁，提供空的默认实现
 */
#ifdef LUA_CACHELIB

#define codecache luaopen_cache  // 使用补丁提供的缓存库

#else

// 默认空实现（无缓存功能）
static int
cleardummy(lua_State *L) {
  return 0;
}

static int 
codecache(lua_State *L) {
	luaL_Reg l[] = {
		{ "clear", cleardummy },
		{ "mode", cleardummy },
		{ NULL, NULL },
	};
	luaL_newlib(L,l);
	lua_getglobal(L, "loadfile");
	lua_setfield(L, -2, "loadfile");
	return 1;
}

#endif

/**
 * 【信号处理】Lua 钩子函数
 * 
 * 当收到信号 0 时，设置此钩子让 Lua 立即抛出错误中断执行
 * 用于处理死循环或长时间运行的脚本
 */
static void
signal_hook(lua_State *L, lua_Debug *ar) {
	void *ud = NULL;
	lua_getallocf(L, &ud);
	struct snlua *l = (struct snlua *)ud;

	lua_sethook (L, NULL, 0, 0);  // 清除钩子，避免重复触发
	if (ATOM_LOAD(&l->trap)) {
		ATOM_STORE(&l->trap , 0);
		luaL_error(L, "signal 0");  // 抛出错误中断执行
	}
}

/**
 * 【内部】切换当前活跃的 Lua 线程
 * 
 * 用于协程切换时更新 activeL，以便信号能中断正确的线程
 */
static void
switchL(lua_State *L, struct snlua *l) {
	l->activeL = L;
	if (ATOM_LOAD(&l->trap)) {
		// 如果有待处理的信号，设置钩子
		lua_sethook(L, signal_hook, LUA_MASKCOUNT, 1);
	}
}

/**
 * 【内部】带性能分析的 Lua resume
 * 
 * 包装 lua_resume，在协程切换前后记录时间（用于 profile）
 */
static int
lua_resumeX(lua_State *L, lua_State *from, int nargs, int *nresults) {
	void *ud = NULL;
	lua_getallocf(L, &ud);
	struct snlua *l = (struct snlua *)ud;
	switchL(L, l);  // 设置活跃线程
	int err = lua_resume(L, from, nargs, nresults);
	if (ATOM_LOAD(&l->trap)) {
		// 等待 signal_hook 完成设置（l->trap 从 1 变为 -1）
		while (ATOM_LOAD(&l->trap) >= 0) ;
	}
	switchL(from, l);  // 恢复原始线程
	return err;
}

/**
 * 【性能分析】获取当前 CPU 时间
 * 
 * 使用线程级别的 CPU 时间，而非 wall clock 时间
 * 这样可以准确统计 Lua 代码消耗的 CPU，不受阻塞影响
 */
static double
get_time() {
#if  !defined(__APPLE__)
	struct timespec ti;
	clock_gettime(CLOCK_THREAD_CPUTIME_ID, &ti);  // 线程 CPU 时间

	int sec = ti.tv_sec & 0xffff;  // 只保留低16位秒数（防止溢出）
	int nsec = ti.tv_nsec;

	return (double)sec + (double)nsec / NANOSEC;
#else
	// macOS 使用 mach API
	struct task_thread_times_info aTaskInfo;
	mach_msg_type_number_t aTaskInfoCount = TASK_THREAD_TIMES_INFO_COUNT;
	if (KERN_SUCCESS != task_info(mach_task_self(), TASK_THREAD_TIMES_INFO, (task_info_t )&aTaskInfo, &aTaskInfoCount)) {
		return 0;
	}

	int sec = aTaskInfo.user_time.seconds & 0xffff;
	int msec = aTaskInfo.user_time.microseconds;

	return (double)sec + (double)msec / MICROSEC;
#endif
}

/**
 * 【性能分析】计算时间差
 * 
 * 处理秒数回绕（16位秒数每18小时回绕一次）
 */
static inline double
diff_time(double start) {
	double now = get_time();
	if (now < start) {
		return now + 0x10000 - start;  // 发生回绕
	} else {
		return now - start;
	}
}

/*
** ============================================================================
** 协程性能分析库（profile）
** ============================================================================
** 
** 替换标准库的 coroutine.resume 和 coroutine.wrap
** 在协程切换时自动记录 CPU 时间消耗
*/

/**
 * 【内部】恢复协程（带性能统计）
 */
static int auxresume (lua_State *L, lua_State *co, int narg) {
  int status, nres;
  if (!lua_checkstack(co, narg)) {
    lua_pushliteral(L, "too many arguments to resume");
    return -1;  /* error flag */
  }
  lua_xmove(L, co, narg);
  status = lua_resumeX(co, L, narg, &nres);  // 使用我们的包装函数
  if (status == LUA_OK || status == LUA_YIELD) {
    if (!lua_checkstack(L, nres + 1)) {
      lua_pop(co, nres);  /* remove results anyway */
      lua_pushliteral(L, "too many results to resume");
      return -1;  /* error flag */
    }
    lua_xmove(co, L, nres);  /* move yielded values */
    return nres;
  }
  else {
    lua_xmove(co, L, 1);  /* move error message */
    return -1;  /* error flag */
  }
}

/**
 * 【内部】检查是否启用了性能分析
 */
static int
timing_enable(lua_State *L, int co_index, lua_Number *start_time) {
	lua_pushvalue(L, co_index);
	lua_rawget(L, lua_upvalueindex(1));  // 查询 start_time 表
	if (lua_isnil(L, -1)) {
		lua_pop(L, 1);
		return 0;  // 未启用
	}
	*start_time = lua_tonumber(L, -1);
	lua_pop(L,1);
	return 1;  // 已启用
}

/**
 * 【内部】获取协程累计时间
 */
static double
timing_total(lua_State *L, int co_index) {
	lua_pushvalue(L, co_index);
	lua_rawget(L, lua_upvalueindex(2));  // 查询 total_time 表
	double total_time = lua_tonumber(L, -1);
	lua_pop(L,1);
	return total_time;
}

/**
 * 【内部】带计时的 resume
 */
static int
timing_resume(lua_State *L, int co_index, int n) {
	lua_State *co = lua_tothread(L, co_index);
	lua_Number start_time = 0;
	if (timing_enable(L, co_index, &start_time)) {
		// 记录恢复执行的时间
		start_time = get_time();
#ifdef DEBUG_LOG
		double ti = diff_time(start_time);
		fprintf(stderr, "PROFILE [%p] resume %lf\n", co, ti);
#endif
		lua_pushvalue(L, co_index);
		lua_pushnumber(L, start_time);
		lua_rawset(L, lua_upvalueindex(1));  // 更新 start_time
	}

	int r = auxresume(L, co, n);  // 实际恢复协程

	if (timing_enable(L, co_index, &start_time)) {
		// 计算本次执行耗时
		double total_time = timing_total(L, co_index);
		double diff = diff_time(start_time);
		total_time += diff;
#ifdef DEBUG_LOG
		fprintf(stderr, "PROFILE [%p] yield (%lf/%lf)\n", co, diff, total_time);
#endif
		lua_pushvalue(L, co_index);
		lua_pushnumber(L, total_time);
		lua_rawset(L, lua_upvalueindex(2));  // 更新 total_time
	}

	return r;
}

/**
 * 【Lua API】coroutine.resume 替换
 */
static int luaB_coresume (lua_State *L) {
  luaL_checktype(L, 1, LUA_TTHREAD);
  int r = timing_resume(L, 1, lua_gettop(L) - 1);
  if (r < 0) {
    lua_pushboolean(L, 0);
    lua_insert(L, -2);
    return 2;  /* return false + error message */
  }
  else {
    lua_pushboolean(L, 1);
    lua_insert(L, -(r + 1));
    return r + 1;  /* return true + 'resume' returns */
  }
}

/**
 * 【Lua API】coroutine.wrap 的内部辅助函数（带计时）
 */
static int luaB_auxwrap (lua_State *L) {
  lua_State *co = lua_tothread(L, lua_upvalueindex(3));
  int r = timing_resume(L, lua_upvalueindex(3), lua_gettop(L));
  if (r < 0) {
    int stat = lua_status(co);
    if (stat != LUA_OK && stat != LUA_YIELD)
      lua_closethread(co, L);  /* close variables in case of errors */
    if (lua_type(L, -1) == LUA_TSTRING) {  /* error object is a string? */
      luaL_where(L, 1);  /* add extra info, if available */
      lua_insert(L, -2);
      lua_concat(L, 2);
    }
    return lua_error(L);  /* propagate error */
  }
  return r;
}

/**
 * 【Lua API】coroutine.create
 */
static int luaB_cocreate (lua_State *L) {
  lua_State *NL;
  luaL_checktype(L, 1, LUA_TFUNCTION);
  NL = lua_newthread(L);
  lua_pushvalue(L, 1);  /* move function to top */
  lua_xmove(L, NL, 1);  /* move function from L to NL */
  return 1;
}

/**
 * 【Lua API】coroutine.wrap
 */
static int luaB_cowrap (lua_State *L) {
  lua_pushvalue(L, lua_upvalueindex(1));  // start_time table
  lua_pushvalue(L, lua_upvalueindex(2));  // total_time table
  luaB_cocreate(L);
  lua_pushcclosure(L, luaB_auxwrap, 3);   // 创建闭包，绑定计时表
  return 1;
}

/**
 * 【Lua API】profile.start
 * 
 * 开始对指定协程进行性能分析
 */
static int
lstart(lua_State *L) {
	if (lua_gettop(L) != 0) {
		lua_settop(L,1);
		luaL_checktype(L, 1, LUA_TTHREAD);
	} else {
		lua_pushthread(L);  // 默认分析当前线程
	}
	lua_Number start_time = 0;
	if (timing_enable(L, 1, &start_time)) {
		return luaL_error(L, "Thread %p start profile more than once", lua_topointer(L, 1));
	}

	// 重置累计时间
	lua_pushvalue(L, 1);
	lua_pushnumber(L, 0);
	lua_rawset(L, lua_upvalueindex(2));

	// 记录开始时间
	lua_pushvalue(L, 1);
	start_time = get_time();
#ifdef DEBUG_LOG
	fprintf(stderr, "PROFILE [%p] start\n", L);
#endif
	lua_pushnumber(L, start_time);
	lua_rawset(L, lua_upvalueindex(1));

	return 0;
}

/**
 * 【Lua API】profile.stop
 * 
 * 停止性能分析，返回累计 CPU 时间
 */
static int
lstop(lua_State *L) {
	if (lua_gettop(L) != 0) {
		lua_settop(L,1);
		luaL_checktype(L, 1, LUA_TTHREAD);
	} else {
		lua_pushthread(L);
	}
	lua_Number start_time = 0;
	if (!timing_enable(L, 1, &start_time)) {
		return luaL_error(L, "Call profile.start() before profile.stop()");
	}
	double ti = diff_time(start_time);
	double total_time = timing_total(L,1);

	lua_pushvalue(L, 1);	lua_pushnil(L);
	lua_rawset(L, lua_upvalueindex(1));  // 清除 start_time

	lua_pushvalue(L, 1);	lua_pushnil(L);
	lua_rawset(L, lua_upvalueindex(2));  // 清除 total_time

	total_time += ti;
	lua_pushnumber(L, total_time);  // 返回总时间
#ifdef DEBUG_LOG
	fprintf(stderr, "PROFILE [%p] stop (%lf/%lf)\n", lua_tothread(L,1), ti, total_time);
#endif

	return 1;
}

/**
 * 【内部】初始化 profile 库
 * 
 * 创建两个弱引用表来存储协程的开始时间和累计时间
 */
static int
init_profile(lua_State *L) {
	luaL_Reg l[] = {
		{ "start", lstart },
		{ "stop", lstop },
		{ "resume", luaB_coresume },  // 替换系统的 coroutine.resume
		{ "wrap", luaB_cowrap },      // 替换系统的 coroutine.wrap
		{ NULL, NULL },
	};
	luaL_newlibtable(L,l);
	
	lua_newtable(L);	// table 1: thread -> start time
	lua_newtable(L);	// table 2: thread -> total time

	// 设置为弱引用表（key 弱引用），允许协程被 GC
	lua_newtable(L);
	lua_pushliteral(L, "kv");
	lua_setfield(L, -2, "__mode");

	lua_pushvalue(L, -1);
	lua_setmetatable(L, -3);
	lua_setmetatable(L, -3);

	luaL_setfuncs(L,l,2);  // 注册函数，绑定两个 upvalue

	return 1;
}

/// 协程性能分析结束

/**
 * 【内部】错误追踪
 * 
 * 打印 Lua 调用栈
 */
static int 
traceback (lua_State *L) {
	const char *msg = lua_tostring(L, 1);
	if (msg)
		luaL_traceback(L, L, msg, 1);
	else {
		lua_pushliteral(L, "(no error message)");
	}
	return 1;
}

/**
 * 【内部】报告启动错误给 launcher
 */
static void
report_launcher_error(struct skynet_context *ctx) {
	skynet_sendname(ctx, 0, ".launcher", PTYPE_TEXT, 0, "ERROR", 5);
}

/**
 * 【内部】获取环境变量（带默认值）
 */
static const char *
optstring(struct skynet_context *ctx, const char *key, const char * str) {
	const char * ret = skynet_command(ctx, "GETENV", key);
	if (ret == NULL) {
		return str;
	}
	return ret;
}

/**
 * 【核心】初始化 Lua 服务
 * 
 * 这是 Lua 服务启动的核心流程：
 * 1. 初始化 Lua 环境（标准库、skynet 库）
 * 2. 设置路径（LUA_PATH, LUA_CPATH, LUA_SERVICE）
 * 3. 加载 loader.lua
 * 4. 执行用户指定的服务脚本
 * 
 * @param args 服务参数（如 "agent" 或 "login 8888"）
 */
static int
init_cb(struct snlua *l, struct skynet_context *ctx, const char * args, size_t sz) {
	lua_State *L = l->L;
	l->ctx = ctx;
	
	lua_gc(L, LUA_GCSTOP, 0);  // 暂停 GC，避免初始化时频繁触发
	
	// 标记：忽略环境变量（使用我们设置的路径）
	lua_pushboolean(L, 1);
	lua_setfield(L, LUA_REGISTRYINDEX, "LUA_NOENV");
	
	// 打开标准库
	luaL_openlibs(L);
	
	// 加载 skynet.profile 库（包含计时的 resume/wrap）
	luaL_requiref(L, "skynet.profile", init_profile, 0);
	int profile_lib = lua_gettop(L);
	
	// 替换标准库的 coroutine.resume 和 coroutine.wrap
	lua_getglobal(L, "coroutine");
	lua_getfield(L, profile_lib, "resume");
	lua_setfield(L, -2, "resume");
	lua_getfield(L, profile_lib, "wrap");
	lua_setfield(L, -2, "wrap");
	lua_settop(L, profile_lib-1);  // 恢复栈

	// 保存 skynet_context 到注册表（供 Lua 层获取）
	lua_pushlightuserdata(L, ctx);
	lua_setfield(L, LUA_REGISTRYINDEX, "skynet_context");
	
	// 加载代码缓存模块
	luaL_requiref(L, "skynet.codecache", codecache , 0);
	lua_pop(L,1);
	
	// 使用分代 GC
	lua_gc(L, LUA_GCGEN, 0, 0);

	// 设置 Lua 搜索路径（从环境变量或默认值）
	const char *path = optstring(ctx, "lua_path","./lualib/?.lua;./lualib/?/init.lua");
	lua_pushstring(L, path);
	lua_setglobal(L, "LUA_PATH");
	
	const char *cpath = optstring(ctx, "lua_cpath","./luaclib/?.so");
	lua_pushstring(L, cpath);
	lua_setglobal(L, "LUA_CPATH");
	
	const char *service = optstring(ctx, "luaservice", "./service/?.lua");
	lua_pushstring(L, service);
	lua_setglobal(L, "LUA_SERVICE");
	
	const char *preload = skynet_command(ctx, "GETENV", "preload");
	lua_pushstring(L, preload);
	lua_setglobal(L, "LUA_PRELOAD");

	// 设置错误处理函数（traceback）
	lua_pushcfunction(L, traceback);
	assert(lua_gettop(L) == 1);

	// 加载 loader.lua（负责加载实际的服务脚本）
	const char * loader = optstring(ctx, "lualoader", "./lualib/loader.lua");
	int r = luaL_loadfile(L,loader);
	if (r != LUA_OK) {
		skynet_error(ctx, "Can't load %s : %s", loader, lua_tostring(L, -1));
		report_launcher_error(ctx);
		return 1;
	}
	
	// 传递参数给 loader（如服务名 "agent"）
	lua_pushlstring(L, args, sz);
	r = lua_pcall(L,1,0,1);  // 调用 loader，1个参数，0个返回值，1=错误处理函数
	if (r != LUA_OK) {
		skynet_error(ctx, "lua loader error : %s", lua_tostring(L, -1));
		report_launcher_error(ctx);
		return 1;
	}
	
	lua_settop(L,0);
	
	// 检查是否设置了内存限制
	if (lua_getfield(L, LUA_REGISTRYINDEX, "memlimit") == LUA_TNUMBER) {
		size_t limit = lua_tointeger(L, -1);
		l->mem_limit = limit;
		skynet_error(ctx, "Set memory limit to %.2f M", (float)limit / (1024 * 1024));
		lua_pushnil(L);
		lua_setfield(L, LUA_REGISTRYINDEX, "memlimit");
	}
	lua_pop(L, 1);

	lua_gc(L, LUA_GCRESTART, 0);  // 恢复 GC

	return 0;
}

/**
 * 【回调】启动消息处理
 * 
 * 这是 snlua 服务收到的第一条消息（由 snlua_init 发送）
 * 在此完成 Lua 环境的初始化
 */
static int
launch_cb(struct skynet_context * context, void *ud, int type, int session, uint32_t source , const void * msg, size_t sz) {
	assert(type == 0 && session == 0);
	struct snlua *l = ud;
	skynet_callback(context, NULL, NULL);  // 临时清空回调，防止重入
	int err = init_cb(l, context, msg, sz);  // 初始化 Lua
	if (err) {
		skynet_command(context, "EXIT", NULL);  // 初始化失败，退出服务
	}
	return 0;
}

/**
 * 【接口】初始化 snlua 服务
 * 
 * 1. 注册 launch_cb 作为临时回调
 * 2. 发送第一条消息触发初始化
 */
int
snlua_init(struct snlua *l, struct skynet_context *ctx, const char * args) {
	int sz = strlen(args);
	char * tmp = skynet_malloc(sz);
	memcpy(tmp, args, sz);
	skynet_callback(ctx, l , launch_cb);  // 设置临时回调
	const char * self = skynet_command(ctx, "REG", NULL);
	uint32_t handle_id = strtoul(self+1, NULL, 16);
	// 发送初始化消息（携带参数如 "agent"）
	skynet_send(ctx, 0, handle_id, PTYPE_TAG_DONTCOPY,0, tmp, sz);
	return 0;
}

/**
 * 【Lua 分配器】带内存统计和限制
 * 
 * 替换 Lua 默认的内存分配器，实现：
 * 1. 统计内存使用量
 * 2. 内存限制检查
 * 3. 内存使用警告
 */
static void *
lalloc(void * ud, void *ptr, size_t osize, size_t nsize) {
	struct snlua *l = ud;
	size_t mem = l->mem;
	l->mem += nsize;
	if (ptr)
		l->mem -= osize;
	
	// 检查内存限制
	if (l->mem_limit != 0 && l->mem > l->mem_limit) {
		if (ptr == NULL || nsize > osize) {  // 只有在申请新内存时检查
			l->mem = mem;  // 回滚计数
			return NULL;   // 返回 NULL 触发 Lua 内存错误
		}
	}
	
	// 内存使用警告
	if (l->mem > l->mem_report) {
		l->mem_report *= 2;
		skynet_error(l->ctx, "Memory warning %.2f M", (float)l->mem / (1024 * 1024));
	}
	
	return skynet_lalloc(ptr, osize, nsize);  // 调用 Skynet 的分配器
}

/**
 * 【内部】生成全局随机种子
 */
static unsigned
global_seed() {
	static ATOM_INT seed = 0;
	unsigned ret = ATOM_LOAD(&seed);
	while (ret == 0) {
		unsigned t = luaL_makeseed(NULL);  // Lua 5.4+ 的随机种子函数
		if (t == 0)
			t = 1;
		ATOM_CAS(&seed, 0, t);
		ret = ATOM_LOAD(&seed);
	}
	return ret;
}

/**
 * 【接口】创建 snlua 实例
 * 
 * 创建 Lua 虚拟机，设置自定义内存分配器
 */
struct snlua *
snlua_create(void) {
	struct snlua * l = skynet_malloc(sizeof(*l));
	memset(l,0,sizeof(*l));
	l->mem_report = MEMORY_WARNING_REPORT;  // 32MB
	l->mem_limit = 0;  // 默认无限制
	l->L = lua_newstate(lalloc, l, global_seed());  // 创建 Lua VM，使用我们的分配器
	l->activeL = NULL;
	ATOM_INIT(&l->trap , 0);
	return l;
}

/**
 * 【接口】释放 snlua 实例
 */
void
snlua_release(struct snlua *l) {
	lua_close(l->L);  // 关闭 Lua VM
	skynet_free(l);
}

/**
 * 【接口】信号处理
 * 
 * signal 0: 中断 Lua 执行（用于处理死循环）
 * signal 1: 打印当前内存使用
 */
void
snlua_signal(struct snlua *l, int signal) {
	skynet_error(l->ctx, "recv a signal %d", signal);
	if (signal == 0) {
		if (ATOM_LOAD(&l->trap) == 0) {
			// 设置陷阱，让 Lua 钩子函数抛出错误
			if (!ATOM_CAS(&l->trap, 0, 1))
				return;
			lua_sethook (l->activeL, signal_hook, LUA_MASKCOUNT, 1);
			ATOM_CAS(&l->trap, 1, -1);  // 标记设置完成
		}
	} else if (signal == 1) {
		// 报告当前内存使用
		skynet_error(l->ctx, "Current Memory %.3fK", (float)l->mem / 1024);
	}
}
