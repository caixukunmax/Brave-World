/**
 * ============================================================================
 * Skynet 模块系统 - skynet_module.c
 * ============================================================================
 * 
 * 【文件作用】
 * 实现 Skynet 的 C 服务模块动态加载系统：
 * 1. 动态加载共享库（.so / .dll）
 * 2. 解析模块中的导出函数（create/init/release/signal）
 * 3. 管理模块缓存（避免重复加载）
 * 
 * 【模块接口规范】
 * C 服务模块需要导出以下函数（命名格式：<模块名>_<函数名>）：
 * - <name>_create():   创建模块实例，返回实例指针
 * - <name>_init():     初始化实例，参数: (inst, ctx, param)
 * - <name>_release():  释放实例资源
 * - <name>_signal():   信号处理（可选）
 * 
 * 示例：snlua 模块需要导出 snlua_create、snlua_init 等
 * 
 * 【模块搜索路径】
 * 使用类似 Lua 的 path 格式，用 ? 作为模块名占位符
 * 示例："./cservice/?.so;./build/?.so"
 * ============================================================================
 */

#include "skynet.h"

#include "skynet_imp.h"
#include "skynet_module.h"
#include "spinlock.h"

#include <assert.h>
#include <string.h>
#include <dlfcn.h>
#include <stdlib.h>
#include <stdint.h>
#include <stdio.h>

// 最大模块类型数量
#define MAX_MODULE_TYPE 32

/**
 * 【数据结构】模块管理器
 */
struct modules {
	int count;                          // 已加载的模块数量
	struct spinlock lock;               // 自旋锁
	const char * path;                  // 模块搜索路径
	struct skynet_module m[MAX_MODULE_TYPE];  // 模块数组
};

// 全局模块管理器实例
static struct modules * M = NULL;

/**
 * 【内部】尝试打开模块共享库
 * 
 * 按照 path 中指定的搜索路径查找并加载模块
 * 
 * @param m     模块管理器
 * @param name  模块名称
 * @return      动态库句柄（dlopen 返回）
 */
static void *
_try_open(struct modules *m, const char * name) {
	const char *l;
	const char * path = m->path;
	size_t path_size = strlen(path);
	size_t name_size = strlen(name);

	int sz = path_size + name_size;
	// 搜索路径
	void * dl = NULL;
	char tmp[sz];
	do
	{
		memset(tmp,0,sz);
		while (*path == ';') path++;
		if (*path == '\0') break;
		l = strchr(path, ';');
		if (l == NULL) l = path + strlen(path);
		int len = l - path;
		int i;
		// 复制路径模板，用模块名替换 ?
		for (i=0;path[i]!='?' && i < len ;i++) {
			tmp[i] = path[i];
		}
		memcpy(tmp+i,name,name_size);
		if (path[i] == '?') {
			strncpy(tmp+i+name_size,path+i+1,len - i - 1);
		} else {
			fprintf(stderr,"Invalid C service path\n");
			exit(1);
		}
		// 加载共享库
		dl = dlopen(tmp, RTLD_NOW | RTLD_GLOBAL);
		path = l;
	}while(dl == NULL);

	if (dl == NULL) {
		fprintf(stderr, "try open %s failed : %s\n",name,dlerror());
	}

	return dl;
}

/**
 * 【内部】查询已加载的模块
 */
static struct skynet_module *
_query(const char * name) {
	int i;
	for (i=0;i<M->count;i++) {
		if (strcmp(M->m[i].name,name)==0) {
			return &M->m[i];
		}
	}
	return NULL;
}

/**
 * 【内部】获取模块的导出函数
 * 
 * 函数命名格式：<模块名>_<api_name>
 * 示例：snlua_create、snlua_init
 */
static void *
get_api(struct skynet_module *mod, const char *api_name) {
	size_t name_size = strlen(mod->name);
	size_t api_size = strlen(api_name);
	char tmp[name_size + api_size + 1];
	memcpy(tmp, mod->name, name_size);
	memcpy(tmp+name_size, api_name, api_size+1);
	char *ptr = strrchr(tmp, '.');
	if (ptr == NULL) {
		ptr = tmp;
	} else {
		ptr = ptr + 1;
	}
	return dlsym(mod->module, ptr);
}

/**
 * 【内部】打开模块的所有导出函数
 * 
 * @return 0 成功，非0 失败（init 函数必须存在）
 */
static int
open_sym(struct skynet_module *mod) {
	mod->create = get_api(mod, "_create");
	mod->init = get_api(mod, "_init");
	mod->release = get_api(mod, "_release");
	mod->signal = get_api(mod, "_signal");

	return mod->init == NULL;  // init 是必须的
}

/**
 * 【接口】查询模块（如果不存在则加载）
 * 
 * @param name  模块名称
 * @return      模块指针（NULL 表示加载失败）
 */
struct skynet_module *
skynet_module_query(const char * name) {
	// 先不加锁检查（快速路径）
	struct skynet_module * result = _query(name);
	if (result)
		return result;

	SPIN_LOCK(M)

	// 双重检查
	result = _query(name);

	if (result == NULL && M->count < MAX_MODULE_TYPE) {
		int index = M->count;
		void * dl = _try_open(M,name);
		if (dl) {
			M->m[index].name = name;
			M->m[index].module = dl;

			if (open_sym(&M->m[index]) == 0) {
				M->m[index].name = skynet_strdup(name);
				M->count ++;
				result = &M->m[index];
			}
		}
	}

	SPIN_UNLOCK(M)

	return result;
}

/**
 * 【接口】创建模块实例
 * 
 * @return 实例指针（~0 表示 create 函数不存在）
 */
void *
skynet_module_instance_create(struct skynet_module *m) {
	if (m->create) {
		return m->create();
	} else {
		return (void *)(intptr_t)(~0);
	}
}

/**
 * 【接口】初始化模块实例
 * 
 * @param m     模块
 * @param inst  实例指针
 * @param ctx   服务上下文
 * @param parm  初始化参数
 * @return      0 成功，非0 失败
 */
int
skynet_module_instance_init(struct skynet_module *m, void * inst, struct skynet_context *ctx, const char * parm) {
	return m->init(inst, ctx, parm);
}

/**
 * 【接口】释放模块实例
 */
void
skynet_module_instance_release(struct skynet_module *m, void *inst) {
	if (m->release) {
		m->release(inst);
	}
}

/**
 * 【接口】发送信号给模块实例
 */
void
skynet_module_instance_signal(struct skynet_module *m, void *inst, int signal) {
	if (m->signal) {
		m->signal(inst, signal);
	}
}

/**
 * 【接口】初始化模块系统
 * 
 * @param path  模块搜索路径（如 "./cservice/?.so"）
 */
void
skynet_module_init(const char *path) {
	struct modules *m = skynet_malloc(sizeof(*m));
	m->count = 0;
	m->path = skynet_strdup(path);

	SPIN_INIT(m)

	M = m;
}
