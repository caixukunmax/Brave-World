/**
 * ============================================================================
 * Skynet 核心 Lua 绑定模块 - lua-skynet.c
 * ============================================================================
 * 
 * 【文件作用】
 * 这是 Skynet 最核心的 Lua C 扩展模块，提供 Lua 层与 Skynet C 核心交互的所有 API。
 * 
 * 【核心功能】
 * 1. 消息发送：send、redirect（支持 name 或 handle 寻址）
 * 2. 回调注册：callback（将 C 层消息转发给 Lua）
 * 3. 命令执行：command（EXIT、REG、GETENV 等）
 * 4. 时间管理：now、hpc
 * 5. 序列化：pack、unpack（配合 lua-seri.c）
 * 6. 错误输出：error、trace
 * 
 * 【消息流转】
 * Lua 层 (skynet.send) 
 *   → lsend() [本文件]
 *   → skynet_send() [skynet_server.c]
 *   → 目标服务的消息队列
 * 
 * C 层消息到来
 *   → _cb() / forward_cb() [本文件]
 *   → 调用 Lua 注册的 dispatch 函数
 *   → 业务逻辑处理
 * 
 * 【注册表项】
 * - "skynet_context": 存储 skynet_context 指针
 * - "callback_context": 存储回调上下文
 * ============================================================================
 */

#define LUA_LIB

#include "skynet.h"
#include "lua-seri.h"

// ANSI 颜色代码
#define KNRM  "\x1B[0m"   // 重置
#define KRED  "\x1B[31m"  // 红色（错误）

#include <lua.h>
#include <lauxlib.h>
#include <stdlib.h>
#include <string.h>
#include <assert.h>
#include <inttypes.h>

#include <time.h>

#if defined(__APPLE__)
#include <sys/time.h>
#endif

#include "skynet.h"

/**
 * 【内部】获取高精度时间（纳秒）
 * 
 * 用于性能分析和 trace
 */
static int64_t
get_time() {
#if !defined(__APPLE__) || defined(AVAILABLE_MAC_OS_X_VERSION_10_12_AND_LATER)
	struct timespec ti;
	clock_gettime(CLOCK_MONOTONIC, &ti);
	return (int64_t)1000000000 * ti.tv_sec + ti.tv_nsec;
#else
	struct timeval tv;
	gettimeofday(&tv, NULL);
	return (int64_t)1000000000 * tv.tv_sec + tv.tv_usec * 1000;
#endif
}

/**
 * 【内部】错误追踪函数
 * 
 * 获取 Lua 调用栈，用于错误处理
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
 * 【数据结构】回调上下文
 * 
 * 存储 Lua 状态和回调函数
 */
struct callback_context {
	lua_State *L;
};

/**
 * 【回调】处理收到的消息（标准模式）
 * 
 * 将 C 层消息转换为 Lua 调用：
 * dispatch(type, msg_ptr, sz, session, source)
 * 
 * @param msg  消息数据指针（C 层缓冲区）
 * @param sz   消息大小
 * @return 0   成功处理，skynet 会释放 msg
 */
static int
_cb(struct skynet_context * context, void * ud, int type, int session, uint32_t source, const void * msg, size_t sz) {
	struct callback_context *cb_ctx = (struct callback_context *)ud;
	lua_State *L = cb_ctx->L;
	int trace = 1;  // 使用 traceback 作为错误处理
	int r;
	
	// 压入回调函数（从注册表获取）
	lua_pushvalue(L,2);

	// 压入参数
	lua_pushinteger(L, type);                    // 消息类型
	lua_pushlightuserdata(L, (void *)msg);       // 消息数据指针（轻量用户数据）
	lua_pushinteger(L,sz);                       // 消息大小
	lua_pushinteger(L, session);                 // session（用于 call 返回）
	lua_pushinteger(L, source);                  // 发送方 handle

	// 调用 Lua 函数：dispatch(type, msg, sz, session, source)
	r = lua_pcall(L, 5, 0 , trace);

	if (r == LUA_OK) {
		return 0;  // 成功，Skynet 会释放 msg
	}
	
	// 错误处理
	const char * self = skynet_command(context, "REG", NULL);
	switch (r) {
	case LUA_ERRRUN:
		skynet_error(context, "lua call [%x to %s : %d msgsz = %d] error : " KRED "%s" KNRM, 
			source , self, session, sz, lua_tostring(L,-1));
		break;
	case LUA_ERRMEM:
		skynet_error(context, "lua memory error : [%x to %s : %d]", source , self, session);
		break;
	case LUA_ERRERR:
		skynet_error(context, "lua error in error : [%x to %s : %d]", source , self, session);
		break;
	};

	lua_pop(L,1);

	return 0;
}

/**
 * 【回调】处理收到的消息（转发模式）
 * 
 * 与 _cb 相同，但返回 1 表示不释放 msg
 * 用于消息转发场景
 */
static int
forward_cb(struct skynet_context * context, void * ud, int type, int session, uint32_t source, const void * msg, size_t sz) {
	_cb(context, ud, type, session, source, msg, sz);
	// don't delete msg in forward mode.
	return 1;
}

/**
 * 【内部】清除上一次的回调上下文
 * 
 * 避免循环引用，允许 GC 回收
 */
static void
clear_last_context(lua_State *L) {
	if (lua_getfield(L, LUA_REGISTRYINDEX, "callback_context") == LUA_TUSERDATA) {
		lua_pushnil(L);
		lua_setiuservalue(L, -2, 2);
	}
	lua_pop(L, 1);
}

/**
 * 【回调】预处理函数（标准模式）
 * 
 * 第一次收到消息时调用，清除旧上下文后切换到正式回调
 */
static int
_cb_pre(struct skynet_context * context, void * ud, int type, int session, uint32_t source, const void * msg, size_t sz) {
	struct callback_context *cb_ctx = (struct callback_context *)ud;
	clear_last_context(cb_ctx->L);
	skynet_callback(context, ud, _cb);  // 切换到正式回调
	return _cb(context, cb_ctx, type, session, source, msg, sz);
}

/**
 * 【回调】预处理函数（转发模式）
 */
static int
_forward_pre(struct skynet_context *context, void *ud, int type, int session, uint32_t source, const void *msg, size_t sz) {
	struct callback_context *cb_ctx = (struct callback_context *)ud;
	clear_last_context(cb_ctx->L);
	skynet_callback(context, ud, forward_cb);
	return forward_cb(context, cb_ctx, type, session, source, msg, sz);
}

/**
 * 【Lua API】skynet.callback(dispatch_func, forward_mode)
 * 
 * 注册消息处理函数，所有发送到该服务的消息都会回调此函数
 * 
 * @param L         Lua 状态
 * @param 参数1     回调函数：function(type, msg, sz, session, source)
 * @param 参数2     是否转发模式（bool，可选）
 * @return 0
 */
static int
lcallback(lua_State *L) {
	struct skynet_context * context = lua_touserdata(L, lua_upvalueindex(1));
	int forward = lua_toboolean(L, 2);
	luaL_checktype(L,1,LUA_TFUNCTION);
	lua_settop(L,1);
	
	// 创建回调上下文
	struct callback_context * cb_ctx = (struct callback_context *)lua_newuserdatauv(L, sizeof(*cb_ctx), 2);
	cb_ctx->L = lua_newthread(L);  // 创建独立线程用于回调
	
	// 设置错误处理函数
	lua_pushcfunction(cb_ctx->L, traceback);
	lua_setiuservalue(L, -2, 1);
	
	// 保存回调上下文到注册表
	lua_getfield(L, LUA_REGISTRYINDEX, "callback_context");
	lua_setiuservalue(L, -2, 2);
	lua_setfield(L, LUA_REGISTRYINDEX, "callback_context");
	
	// 移动回调函数到新线程
	lua_xmove(L, cb_ctx->L, 1);

	// 设置 C 层回调（先使用 _pre 版本进行初始化）
	skynet_callback(context, cb_ctx, (forward)?(_forward_pre):(_cb_pre));
	return 0;
}

/**
 * 【Lua API】skynet.command(cmd, param)
 * 
 * 执行 Skynet 命令，返回字符串结果
 * 
 * 常用命令：
 * - "EXIT": 退出服务
 * - "REG": 获取自身 handle
 * - "GETENV key": 获取环境变量
 * - "SETENV key value": 设置环境变量
 */
static int
lcommand(lua_State *L) {
	struct skynet_context * context = lua_touserdata(L, lua_upvalueindex(1));
	const char * cmd = luaL_checkstring(L,1);
	const char * result;
	const char * parm = NULL;
	if (lua_gettop(L) == 2) {
		parm = luaL_checkstring(L,2);
	}

	result = skynet_command(context, cmd, parm);
	if (result) {
		lua_pushstring(L, result);
		return 1;
	}
	return 0;
}

/**
 * 【Lua API】skynet.addresscommand(cmd, param)
 * 
 * 执行命令，将形如 ":01000001" 的结果转换为整数 handle
 */
static int
laddresscommand(lua_State *L) {
	struct skynet_context * context = lua_touserdata(L, lua_upvalueindex(1));
	const char * cmd = luaL_checkstring(L,1);
	const char * result;
	const char * parm = NULL;
	if (lua_gettop(L) == 2) {
		parm = luaL_checkstring(L,2);
	}
	result = skynet_command(context, cmd, parm);
	if (result && result[0] == ':') {
		int i;
		uint32_t addr = 0;
		for (i=1;result[i];i++) {
			int c = result[i];
			if (c>='0' && c<='9') {
				c = c - '0';
			} else if (c>='a' && c<='f') {
				c = c - 'a' + 10;
			} else if (c>='A' && c<='F') {
				c = c - 'A' + 10;
			} else {
				return 0;
			}
			addr = addr * 16 + c;
		}
		lua_pushinteger(L, addr);
		return 1;
	}
	return 0;
}

/**
 * 【Lua API】skynet.intcommand(cmd, param)
 * 
 * 执行命令，将结果转换为整数或浮点数
 */
static int
lintcommand(lua_State *L) {
	struct skynet_context * context = lua_touserdata(L, lua_upvalueindex(1));
	const char * cmd = luaL_checkstring(L,1);
	const char * result;
	const char * parm = NULL;
	char tmp[64];	// 用于转换整数参数
	if (lua_gettop(L) == 2) {
		if (lua_isnumber(L, 2)) {
			int32_t n = (int32_t)luaL_checkinteger(L,2);
			sprintf(tmp, "%d", n);
			parm = tmp;
		} else {
			parm = luaL_checkstring(L,2);
		}
	}

	result = skynet_command(context, cmd, parm);
	if (result) {
		char *endptr = NULL;
		lua_Integer r = strtoll(result, &endptr, 0);
		if (endptr == NULL || *endptr != '\0') {
			// 可能是浮点数
			double n = strtod(result, &endptr);
			if (endptr == NULL || *endptr != '\0') {
				return luaL_error(L, "Invalid result %s", result);
			} else {
				lua_pushnumber(L, n);
			}
		} else {
			lua_pushinteger(L, r);
		}
		return 1;
	}
	return 0;
}

/**
 * 【Lua API】skynet.genid()
 * 
 * 生成唯一的 session ID（用于 call）
 */
static int
lgenid(lua_State *L) {
	struct skynet_context * context = lua_touserdata(L, lua_upvalueindex(1));
	int session = skynet_send(context, 0, 0, PTYPE_TAG_ALLOCSESSION , 0 , NULL, 0);
	lua_pushinteger(L, session);
	return 1;
}

/**
 * 【内部】获取目标地址字符串
 */
static const char *
get_dest_string(lua_State *L, int index) {
	const char * dest_string = lua_tostring(L, index);
	if (dest_string == NULL) {
		luaL_error(L, "dest address type (%s) must be a string or number.", lua_typename(L, lua_type(L,index)));
	}
	return dest_string;
}

/**
 * 【内部】发送消息的通用实现
 * 
 * 支持两种寻址方式：
 * - 整数 handle：skynet.send(handle, ...)
 * - 字符串 name：skynet.send("agent", ...)
 * 
 * 支持两种消息格式：
 * - 字符串：自动拷贝
 * - lightuserdata + size：零拷贝（DONTCOPY）
 */
static int
send_message(lua_State *L, int source, int idx_type) {
	struct skynet_context * context = lua_touserdata(L, lua_upvalueindex(1));
	uint32_t dest = (uint32_t)lua_tointeger(L, 1);
	const char * dest_string = NULL;
	
	// 判断目标地址类型
	if (dest == 0) {
		if (lua_type(L,1) == LUA_TNUMBER) {
			return luaL_error(L, "Invalid service address 0");
		}
		dest_string = get_dest_string(L, 1);
	}

	int type = luaL_checkinteger(L, idx_type+0);
	int session = 0;
	if (lua_isnil(L,idx_type+1)) {
		type |= PTYPE_TAG_ALLOCSESSION;  // 自动分配 session
	} else {
		session = luaL_checkinteger(L,idx_type+1);
	}

	// 根据消息类型发送
	int mtype = lua_type(L,idx_type+2);
	switch (mtype) {
	case LUA_TSTRING: {
		size_t len = 0;
		void * msg = (void *)lua_tolstring(L,idx_type+2,&len);
		if (len == 0) {
			msg = NULL;
		}
		if (dest_string) {
			session = skynet_sendname(context, source, dest_string, type, session , msg, len);
		} else {
			session = skynet_send(context, source, dest, type, session , msg, len);
		}
		break;
	}
	case LUA_TLIGHTUSERDATA: {
		// 轻量用户数据，需要指定大小，使用零拷贝
		void * msg = lua_touserdata(L,idx_type+2);
		int size = luaL_checkinteger(L,idx_type+3);
		if (dest_string) {
			session = skynet_sendname(context, source, dest_string, type | PTYPE_TAG_DONTCOPY, session, msg, size);
		} else {
			session = skynet_send(context, source, dest, type | PTYPE_TAG_DONTCOPY, session, msg, size);
		}
		break;
	}
	default:
		luaL_error(L, "invalid param %s", lua_typename(L, lua_type(L,idx_type+2)));
	}
	
	// 处理返回值
	if (session < 0) {
		if (session == -2) {
			// 包太大
			lua_pushboolean(L, 0);
			return 1;
		}
		// 发送到无效地址
		return 0;
	}
	lua_pushinteger(L,session);
	return 1;
}

/**
 * 【Lua API】skynet.send(addr, type, session, msg)
 * 
 * 发送消息到指定服务
 * 
 * 参数：
 * - addr:     目标 handle（整数）或名字（字符串）
 * - type:     消息类型（如 PTYPE_LUA）
 * - session:  session（nil 表示自动分配）
 * - msg:      消息内容（字符串或 lightuserdata）
 * - len:      消息长度（lightuserdata 时需要）
 * 
 * 返回：session（失败返回 nil）
 */
static int
lsend(lua_State *L) {
	return send_message(L, 0, 2);
}

/**
 * 【Lua API】skynet.redirect(addr, source, type, session, msg)
 * 
 * 以 source 的身份发送消息（伪造发送方）
 * 用于消息转发场景
 */
static int
lredirect(lua_State *L) {
	uint32_t source = (uint32_t)luaL_checkinteger(L,2);
	return send_message(L, source, 3);
}

/**
 * 【Lua API】skynet.error(...)
 * 
 * 输出错误日志到 logger 服务
 * 支持多个参数，自动拼接
 */
static int
lerror(lua_State *L) {
	struct skynet_context * context = lua_touserdata(L, lua_upvalueindex(1));
	int n = lua_gettop(L);
	if (n <= 1) {
		lua_settop(L, 1);
		size_t len;
		const char *s = luaL_tolstring(L, 1, &len);
		skynet_error(context, "%*s", (int)len, s);
		return 0;
	}
	// 多个参数拼接
	luaL_Buffer b;
	luaL_buffinit(L, &b);
	int i;
	for (i=1; i<=n; i++) {
		luaL_tolstring(L, i, NULL);
		luaL_addvalue(&b);
		if (i<n) {
			luaL_addchar(&b, ' ');
		}
	}
	luaL_pushresult(&b);
	size_t len;
	const char *s = luaL_tolstring(L, -1, &len);
	skynet_error(context, "%*s", (int)len, s);
	return 0;
}

/**
 * 【Lua API】skynet.tostring(msg_ptr, sz)
 * 
 * 将 lightuserdata 转换为 Lua 字符串
 */
static int
ltostring(lua_State *L) {
	if (lua_isnoneornil(L,1)) {
		return 0;
	}
	char * msg = lua_touserdata(L,1);
	int sz = luaL_checkinteger(L,2);
	lua_pushlstring(L,msg,sz);
	return 1;
}

/**
 * 【Lua API】skynet.harbor(handle)
 * 
 * 查询 handle 所属的 harbor（集群节点）
 * 返回：harbor_id, is_remote
 */
static int
lharbor(lua_State *L) {
	struct skynet_context * context = lua_touserdata(L, lua_upvalueindex(1));
	uint32_t handle = (uint32_t)luaL_checkinteger(L,1);
	int harbor = 0;
	int remote = skynet_isremote(context, handle, &harbor);
	lua_pushinteger(L,harbor);
	lua_pushboolean(L, remote);

	return 2;
}

/**
 * 【Lua API】skynet.packstring(...)
 * 
 * 序列化参数为字符串（使用 lua-seri）
 */
static int
lpackstring(lua_State *L) {
	luaseri_pack(L);
	char * str = (char *)lua_touserdata(L, -2);
	int sz = lua_tointeger(L, -1);
	lua_pushlstring(L, str, sz);
	skynet_free(str);
	return 1;
}

/**
 * 【Lua API】skynet.trash(msg, sz)
 * 
 * 释放消息内存（DONTCOPY 模式收到消息后手动释放）
 */
static int
ltrash(lua_State *L) {
	int t = lua_type(L,1);
	switch (t) {
	case LUA_TSTRING: {
		// 字符串无需释放（Lua 管理）
		break;
	}
	case LUA_TLIGHTUSERDATA: {
		void * msg = lua_touserdata(L,1);
		luaL_checkinteger(L,2);
		skynet_free(msg);  // 释放 C 层内存
		break;
	}
	default:
		luaL_error(L, "skynet.trash invalid param %s", lua_typename(L,t));
	}

	return 0;
}

/**
 * 【Lua API】skynet.now()
 * 
 * 获取 Skynet 启动后的时间（单位：1/100 秒）
 */
static int
lnow(lua_State *L) {
	uint64_t ti = skynet_now();
	lua_pushinteger(L, ti);
	return 1;
}

/**
 * 【Lua API】skynet.hpc()
 * 
 * 获取高精度时间（纳秒，用于性能分析）
 */
static int
lhpc(lua_State *L) {
	lua_pushinteger(L, get_time());
	return 1;
}

#define MAX_LEVEL 3

struct source_info {
	const char * source;
	int line;
};

/**
 * 【Lua API】skynet.trace(tag, userstring, [co, [level]])
 * 
 * 输出带调用栈的跟踪日志，用于调试
 */
static int
ltrace(lua_State *L) {
	struct skynet_context * context = lua_touserdata(L, lua_upvalueindex(1));
	const char * tag = luaL_checkstring(L, 1);
	const char * user = luaL_checkstring(L, 2);
	if (!lua_isnoneornil(L, 3)) {
		lua_State * co = L;
		int level;
		if (lua_isthread(L, 3)) {
			co = lua_tothread (L, 3);
			level = luaL_optinteger(L, 4, 1);
		} else {
			level = luaL_optinteger(L, 3, 1);
		}
		struct source_info si[MAX_LEVEL];
		lua_Debug d;
		int index = 0;
		do {
			if (!lua_getstack(co, level, &d))
				break;
			lua_getinfo(co, "Sl", &d);
			level++;
			si[index].source = d.source;
			si[index].line = d.currentline;
			if (d.currentline >= 0)
				++index;
		} while (index < MAX_LEVEL);
		switch (index) {
		case 1:
			skynet_error(context, "<TRACE %s> %" PRId64 " %s : %s:%d", tag, get_time(), user, si[0].source, si[0].line);
			break;
		case 2:
			skynet_error(context, "<TRACE %s> %" PRId64 " %s : %s:%d %s:%d", tag, get_time(), user,
				si[0].source, si[0].line,
				si[1].source, si[1].line
				);
			break;
		case 3:
			skynet_error(context, "<TRACE %s> %" PRId64 " %s : %s:%d %s:%d %s:%d", tag, get_time(), user,
				si[0].source, si[0].line,
				si[1].source, si[1].line,
				si[2].source, si[2].line
				);
			break;
		default:
			skynet_error(context, "<TRACE %s> %" PRId64 " %s", tag, get_time(), user);
			break;
		}
		return 0;
	}
	skynet_error(context, "<TRACE %s> %" PRId64 " %s", tag, get_time(), user);
	return 0;
}

/**
 * 【模块入口】luaopen_skynet_core
 * 
 * 注册所有 API 到 skynet.core 模块
 */
LUAMOD_API int
luaopen_skynet_core(lua_State *L) {
	luaL_checkversion(L);

	// 需要 skynet_context 的函数
	luaL_Reg l[] = {
		{ "send" , lsend },
		{ "genid", lgenid },
		{ "redirect", lredirect },
		{ "command" , lcommand },
		{ "intcommand", lintcommand },
		{ "addresscommand", laddresscommand },
		{ "error", lerror },
		{ "harbor", lharbor },
		{ "callback", lcallback },
		{ "trace", ltrace },
		{ NULL, NULL },
	};

	// 不需要 skynet_context 的函数
	luaL_Reg l2[] = {
		{ "tostring", ltostring },
		{ "pack", luaseri_pack },
		{ "unpack", luaseri_unpack },
		{ "packstring", lpackstring },
		{ "trash" , ltrash },
		{ "now", lnow },
		{ "hpc", lhpc },	// getHPCounter
		{ NULL, NULL },
	};

	lua_createtable(L, 0, sizeof(l)/sizeof(l[0]) + sizeof(l2)/sizeof(l2[0]) -2);

	// 从注册表获取 skynet_context
	lua_getfield(L, LUA_REGISTRYINDEX, "skynet_context");
	struct skynet_context *ctx = lua_touserdata(L,-1);
	if (ctx == NULL) {
		return luaL_error(L, "Init skynet context first");
	}

	// 注册函数（带 upvalue）
	luaL_setfuncs(L,l,1);  // l 组函数使用 1 个 upvalue（skynet_context）
	luaL_setfuncs(L,l2,0); // l2 组函数不使用 upvalue

	return 1;
}
