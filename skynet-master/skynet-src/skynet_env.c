/**
 * ============================================================================
 * Skynet 环境变量模块 - skynet_env.c
 * ============================================================================
 * 
 * 【文件作用】
 * 提供全局键值对存储，用于存储配置参数和其他全局信息
 * 
 * 【实现方式】
 * 使用 Lua State 作为存储后端，可以存储字符串类型的值
 * 
 * 【使用场景】
 * 1. 存储配置文件中的配置项
 * 2. 服务间共享全局信息（通过 getenv/setenv 命令）
 * 3. 启动时传递参数给各个服务
 * 
 * 【注意】
 * - 设置后不可修改（assert 检查）
 * - 线程安全（使用自旋锁保护）
 * ============================================================================
 */

#include "skynet.h"
#include "skynet_env.h"
#include "spinlock.h"

#include <lua.h>
#include <lauxlib.h>

#include <stdlib.h>
#include <assert.h>

/**
 * 【数据结构】环境变量存储
 */
struct skynet_env {
	struct spinlock lock;   // 自旋锁
	lua_State *L;           // Lua 状态机（存储键值对）
};

// 全局环境变量实例
static struct skynet_env *E = NULL;

/**
 * 【接口】获取环境变量
 * 
 * @param key  键名
 * @return     值字符串（NULL 表示不存在）
 */
const char * 
skynet_getenv(const char *key) {
	SPIN_LOCK(E)

	lua_State *L = E->L;
	
	lua_getglobal(L, key);          // 获取全局变量
	const char * result = lua_tostring(L, -1);  // 转换为字符串
	lua_pop(L, 1);                  // 弹出值

	SPIN_UNLOCK(E)

	return result;
}

/**
 * 【接口】设置环境变量
 * 
 * @param key    键名
 * @param value  值字符串
 * 
 * 【注意】只能设置一次，重复设置会触发 assert 失败
 */
void 
skynet_setenv(const char *key, const char *value) {
	SPIN_LOCK(E)
	
	lua_State *L = E->L;
	lua_getglobal(L, key);
	assert(lua_isnil(L, -1));       // 断言：不能重复设置
	lua_pop(L,1);
	lua_pushstring(L,value);        // 压入值
	lua_setglobal(L,key);           // 设置为全局变量

	SPIN_UNLOCK(E)
}

/**
 * 【接口】初始化环境变量系统
 */
void
skynet_env_init() {
	E = skynet_malloc(sizeof(*E));
	SPIN_INIT(E)
	E->L = luaL_newstate();         // 创建独立的 Lua 状态机
}
