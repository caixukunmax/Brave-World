/**
 * ============================================================================
 * Skynet 核心服务管理 - skynet_server.c
 * ============================================================================
 * 
 * 【文件作用】
 * 这是 Skynet 框架最核心的文件，实现了：
 * 1. 服务上下文（skynet_context）的生命周期管理
 * 2. Actor 模型的消息调度机制
 * 3. 服务的创建、销毁、查询
 * 4. 消息的发送和分发
 * 5. Skynet 命令系统（TIMEOUT、LAUNCH、KILL 等）
 * 
 * 【核心概念 - Actor 模型】
 * - 每个服务是一个独立的 Actor
 * - Actor 之间通过异步消息通信，不直接调用
 * - 每个 Actor 有自己的消息队列
 * - 工作线程从队列取消息并调用回调处理
 * 
 * 【消息格式】
 * struct skynet_message {
 *     uint32_t source;    // 消息来源服务句柄
 *     int      session;   // 会话ID（用于请求-响应匹配）
 *     void*    data;      // 消息数据指针
 *     size_t   sz;        // 消息大小（高8位存储消息类型）
 * };
 * ============================================================================
 */

#include "skynet.h"

#include "skynet_server.h"
#include "skynet_module.h"
#include "skynet_handle.h"
#include "skynet_mq.h"
#include "skynet_timer.h"
#include "skynet_harbor.h"
#include "skynet_env.h"
#include "skynet_monitor.h"
#include "skynet_imp.h"
#include "skynet_log.h"
#include "spinlock.h"
#include "atomic.h"

#include <pthread.h>

#include <string.h>
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <stdbool.h>

/**
 * 【调试宏】CALLING_CHECK
 * 用于检测回调是否被重入（一个回调未完成时又被调用）
 * 默认不开启，需要时在编译时定义 CALLING_CHECK
 */
#ifdef CALLING_CHECK
#define CHECKCALLING_BEGIN(ctx) if (!(spinlock_trylock(&ctx->calling))) { assert(0); }
#define CHECKCALLING_END(ctx) spinlock_unlock(&ctx->calling);
#define CHECKCALLING_INIT(ctx) spinlock_init(&ctx->calling);
#define CHECKCALLING_DESTROY(ctx) spinlock_destroy(&ctx->calling);
#define CHECKCALLING_DECL struct spinlock calling;
#else
#define CHECKCALLING_BEGIN(ctx)
#define CHECKCALLING_END(ctx)
#define CHECKCALLING_INIT(ctx)
#define CHECKCALLING_DESTROY(ctx)
#define CHECKCALLING_DECL
#endif

/**
 * 【核心数据结构】服务上下文 skynet_context
 * 
 * 每个服务实例对应一个 skynet_context，包含：
 * - instance/mod: 模块实例信息（C 模块）
 * - cb/cb_ud: 消息回调函数及用户数据
 * - queue: 服务的私有消息队列
 * - handle: 服务的唯一标识符（32位无符号整数）
 * - ref: 引用计数（用于内存管理）
 * - cpu_cost/cpu_start: CPU 耗时统计（用于性能分析）
 */
struct skynet_context {
	void * instance;                    // C 模块实例指针
	struct skynet_module * mod;         // 模块信息（函数指针表）
	void * cb_ud;                       // 回调函数的用户数据
	skynet_cb cb;                       // 消息回调函数
	struct message_queue *queue;        // 消息队列指针
	ATOM_POINTER logfile;               // 日志文件（原子指针）
	uint64_t cpu_cost;	                // CPU 总耗时（微秒）
	uint64_t cpu_start;	                // 当前消息开始处理时间
	char result[32];                    // 命令返回结果缓冲区
	uint32_t handle;                    // 服务句柄（唯一ID）
	int session_id;                     // 会话ID计数器（用于生成唯一 session）
	ATOM_INT ref;                       // 引用计数
	size_t message_count;               // 处理的消息总数
	bool init;                          // 是否初始化完成
	bool endless;                       // 是否陷入死循环（监控用）
	bool profile;                       // 是否开启性能分析

	CHECKCALLING_DECL                   // 调试用的调用检查锁
};

/**
 * 【全局节点信息】skynet_node
 * 
 * 存储整个 Skynet 节点的全局状态
 */
struct skynet_node {
	ATOM_INT total;                     // 当前存活的服务数量
	int init;                           // 是否已初始化
	uint32_t monitor_exit;              // 监控服务的句柄（服务退出时通知它）
	pthread_key_t handle_key;           // 线程本地存储 key（存储当前线程处理的服务句柄）
	bool profile;	                    // 是否默认开启性能分析
};

// 全局节点实例
static struct skynet_node G_NODE;

/**
 * 【接口】获取当前存活的服务总数
 */
int
skynet_context_total() {
	return ATOM_LOAD(&G_NODE.total);
}

/**
 * 【内部】增加服务计数
 */
static void
context_inc() {
	ATOM_FINC(&G_NODE.total);
}

/**
 * 【内部】减少服务计数
 */
static void
context_dec() {
	ATOM_FDEC(&G_NODE.total);
}

/**
 * 【接口】获取当前线程正在处理的服务句柄
 * 
 * 通过线程本地存储（TLS）实现，每个工作线程处理消息时会设置这个值
 * 用于 skynet.self() 等 API
 */
uint32_t
skynet_current_handle(void) {
	if (G_NODE.init) {
		void * handle = pthread_getspecific(G_NODE.handle_key);
		return (uint32_t)(uintptr_t)handle;
	} else {
		uint32_t v = (uint32_t)(-THREAD_MAIN);
		return v;
	}
}

/**
 * 【内部】将句柄转换为十六进制字符串（如 :01000001）
 */
static void
id_to_hex(char * str, uint32_t id) {
	int i;
	static char hex[16] = { '0','1','2','3','4','5','6','7','8','9','A','B','C','D','E','F' };
	str[0] = ':';
	for (i=0;i<8;i++) {
		str[i+1] = hex[(id >> ((7-i) * 4))&0xf];
	}
	str[9] = '\0';
}

/**
 * 【辅助结构】用于释放消息队列时传递参数
 */
struct drop_t {
	uint32_t handle;
};

/**
 * 【回调函数】释放消息队列中剩余的消息
 * 当服务被销毁时，其消息队列中可能还有未处理的消息，需要清理
 */
static void
drop_message(struct skynet_message *msg, void *ud) {
	struct drop_t *d = ud;
	skynet_free(msg->data);
	uint32_t source = d->handle;
	assert(source);
	// 向消息来源发送错误消息，通知它消息被丢弃了
	skynet_send(NULL, source, msg->source, PTYPE_ERROR, msg->session, NULL, 0);
}

/**
 * 【核心接口】创建新服务
 * 
 * 这是创建服务的核心函数，流程如下：
 * 1. 查询模块（skynet_module_query）
 * 2. 创建模块实例（skynet_module_instance_create）
 * 3. 创建服务上下文（skynet_context）
 * 4. 注册句柄（skynet_handle_register）
 * 5. 创建消息队列（skynet_mq_create）
 * 6. 调用模块初始化（skynet_module_instance_init）
 * 7. 将消息队列推入全局队列，开始接收消息
 * 
 * @param name   模块名称（如 "snlua"）
 * @param param  初始化参数（传递给模块的 init 函数）
 * @return       服务句柄（失败返回 0）
 */
uint32_t
skynet_context_new(const char * name, const char *param) {
	// 【步骤1】查询模块
	struct skynet_module * mod = skynet_module_query(name);
	if (mod == NULL)
		return 0;

	// 【步骤2】创建模块实例
	void *inst = skynet_module_instance_create(mod);
	if (inst == NULL)
		return 0;

	// 【步骤3】创建服务上下文
	struct skynet_context * ctx = skynet_malloc(sizeof(*ctx));
	CHECKCALLING_INIT(ctx)

	ctx->mod = mod;
	ctx->instance = inst;
	ATOM_INIT(&ctx->ref , 2); // 引用计数=2：handle注册 + init函数
	ctx->cb = NULL;
	ctx->cb_ud = NULL;
	ctx->session_id = 0;
	ATOM_INIT(&ctx->logfile, (uintptr_t)NULL);

	ctx->init = false;
	ctx->endless = false;

	ctx->cpu_cost = 0;
	ctx->cpu_start = 0;
	ctx->message_count = 0;
	ctx->profile = G_NODE.profile;
	
	// 先设为0，避免 handle_retireall 拿到未初始化的句柄
	ctx->handle = 0;
	
	// 【步骤4】注册句柄
	const uint32_t handle = skynet_handle_register(ctx);
	ctx->handle = handle;
	
	// 【步骤5】创建消息队列
	struct message_queue * queue = ctx->queue = skynet_mq_create(handle);
	
	// 增加服务计数
	context_inc();

	// 【步骤6】调用模块初始化
	CHECKCALLING_BEGIN(ctx)
	int r = skynet_module_instance_init(mod, inst, ctx, param);
	CHECKCALLING_END(ctx)
	
	if (r == 0) {
		// 初始化成功
		ctx->init = true;
		// 【步骤7】将队列推入全局队列，服务开始运行
		skynet_globalmq_push(queue);
		skynet_error(ctx, "LAUNCH %s %s", name, param ? param : "");
		skynet_context_release(ctx);  // 释放 init 函数的引用
		return handle;
	} else {
		// 初始化失败，清理资源
		skynet_error(ctx, "error: launch %s FAILED", name);
		uint32_t handle = ctx->handle;
		skynet_context_release(ctx);
		skynet_handle_retire(handle);
		struct drop_t d = { handle };
		skynet_mq_release(queue, drop_message, &d);
		return 0;
	}
}

/**
 * 【接口】生成新的会话ID
 * 
 * 用于请求-响应模式：发送请求时分配一个 session，响应时带回相同的 session
 */
int
skynet_context_newsession(struct skynet_context *ctx) {
	// session 总是正数
	int session = ++ctx->session_id;
	if (session <= 0) {
		ctx->session_id = 1;
		return 1;
	}
	return session;
}

/**
 * 【接口】增加服务引用计数
 */
void
skynet_context_grab(struct skynet_context *ctx) {
	ATOM_FINC(&ctx->ref);
}

/**
 * 【接口】保留服务（不随服务总数归零而退出）
 * 
 * 某些特殊服务（如 logger）需要在其他服务都退出后仍然存活
 */
void
skynet_context_reserve(struct skynet_context *ctx) {
	skynet_context_grab(ctx);
	// 不计入服务总数，这样 skynet 退出时（total==0）不会等待这个服务
	context_dec();
}

/**
 * 【内部】删除服务上下文
 */
static void
delete_context(struct skynet_context *ctx) {
	FILE *f = (FILE *)ATOM_LOAD(&ctx->logfile);
	if (f) {
		fclose(f);
	}
	skynet_module_instance_release(ctx->mod, ctx->instance);
	skynet_mq_mark_release(ctx->queue);  // 标记消息队列可以释放
	CHECKCALLING_DESTROY(ctx)
	skynet_free(ctx);
	context_dec();
}

/**
 * 【接口】释放服务上下文（减少引用计数）
 * 
 * 当引用计数归零时，真正删除服务
 */
void
skynet_context_release(struct skynet_context *ctx) {
	if (ATOM_FDEC(&ctx->ref) == 1) {
		delete_context(ctx);
	}
}

/**
 * 【接口】向指定服务推送消息
 * 
 * @param handle   目标服务句柄
 * @param message  消息结构体
 * @return         0成功，-1失败（服务不存在）
 */
int
skynet_context_push(uint32_t handle, struct skynet_message *message) {
	struct skynet_context * ctx = skynet_handle_grab(handle);
	if (ctx == NULL) {
		return -1;
	}
	skynet_mq_push(ctx->queue, message);
	skynet_context_release(ctx);

	return 0;
}

/**
 * 【接口】标记服务为 endless（死循环）
 * 
 * 监控线程检测到这个标记会报警告
 */
void
skynet_context_endless(uint32_t handle) {
	struct skynet_context * ctx = skynet_handle_grab(handle);
	if (ctx == NULL) {
		return;
	}
	ctx->endless = true;
	skynet_context_release(ctx);
}

/**
 * 【接口】判断目标服务是否在远程节点
 * 
 * @param ctx     当前服务上下文
 * @param handle  目标服务句柄
 * @param harbor  输出参数，远程节点ID
 * @return        1表示远程，0表示本地
 */
int
skynet_isremote(struct skynet_context * ctx, uint32_t handle, int * harbor) {
	int ret = skynet_harbor_message_isremote(handle);
	if (harbor) {
		*harbor = (int)(handle >> HANDLE_REMOTE_SHIFT);
	}
	return ret;
}

/**
 * 【核心】分发单条消息
 * 
 * 这是消息处理的核心函数：
 * 1. 设置线程本地存储（记录当前处理的服务）
 * 2. 解析消息类型和大小
 * 3. 如有日志文件，记录日志
 * 4. 调用服务的回调函数处理消息
 * 5. 统计 CPU 耗时
 * 6. 根据回调返回值决定是否释放消息内存
 * 
 * @param ctx  服务上下文
 * @param msg  消息
 */
static void
dispatch_message(struct skynet_context *ctx, struct skynet_message *msg) {
	assert(ctx->init);
	CHECKCALLING_BEGIN(ctx)
	
	// 设置当前线程正在处理的服务句柄（用于 skynet.self()）
	pthread_setspecific(G_NODE.handle_key, (void *)(uintptr_t)(ctx->handle));
	
	// 解析消息类型和大小（sz 的高8位是类型，低24位是大小）
	int type = msg->sz >> MESSAGE_TYPE_SHIFT;
	size_t sz = msg->sz & MESSAGE_TYPE_MASK;
	
	// 如果配置了消息日志，记录消息内容
	FILE *f = (FILE *)ATOM_LOAD(&ctx->logfile);
	if (f) {
		skynet_log_output(f, msg->source, type, msg->session, msg->data, sz);
	}
	
	++ctx->message_count;
	
	int reserve_msg;  // 回调返回值：1表示保留消息内存，0表示释放
	
	if (ctx->profile) {
		// 开启性能分析，统计 CPU 耗时
		ctx->cpu_start = skynet_thread_time();
		reserve_msg = ctx->cb(ctx, ctx->cb_ud, type, msg->session, msg->source, msg->data, sz);
		uint64_t cost_time = skynet_thread_time() - ctx->cpu_start;
		ctx->cpu_cost += cost_time;
	} else {
		// 不统计性能
		reserve_msg = ctx->cb(ctx, ctx->cb_ud, type, msg->session, msg->source, msg->data, sz);
	}
	
	// 如果回调返回 0，释放消息内存
	if (!reserve_msg) {
		skynet_free(msg->data);
	}
	
	CHECKCALLING_END(ctx)
}

/**
 * 【接口】处理服务队列中的所有消息
 * 
 * 用于服务退出前处理完剩余消息（如 logger）
 */
void
skynet_context_dispatchall(struct skynet_context * ctx) {
	struct skynet_message msg;
	struct message_queue *q = ctx->queue;
	while (!skynet_mq_pop(q,&msg)) {
		dispatch_message(ctx, &msg);
	}
}

/**
 * 【核心】消息分发调度
 * 
 * 这是工作线程的主循环调用的函数，负责：
 * 1. 从全局队列获取一个服务的消息队列
 * 2. 处理该服务的多条消息（根据权重决定数量）
 * 3. 将处理完的消息队列重新放回全局队列（如果还有消息）
 * 
 * @param sm      监控器（用于死循环检测）
 * @param q       上次处理的消息队列（NULL 表示从全局队列取）
 * @param weight  处理权重（决定处理多少条消息）
 * @return        下一个要处理的消息队列
 */
struct message_queue *
skynet_context_message_dispatch(struct skynet_monitor *sm, struct message_queue *q, int weight) {
	// 如果 q 为 NULL，从全局队列取一个
	if (q == NULL) {
		q = skynet_globalmq_pop();
		if (q==NULL)
			return NULL;
	}

	uint32_t handle = skynet_mq_handle(q);

	// 获取服务上下文（增加引用计数）
	struct skynet_context * ctx = skynet_handle_grab(handle);
	if (ctx == NULL) {
		// 服务已销毁，释放消息队列
		struct drop_t d = { handle };
		skynet_mq_release(q, drop_message, &d);
		return skynet_globalmq_pop();
	}

	int i,n=1;
	struct skynet_message msg;

	// 根据权重决定处理多少条消息
	for (i=0;i<n;i++) {
		// 从队列弹出一条消息
		if (skynet_mq_pop(q,&msg)) {
			// 队列为空
			skynet_context_release(ctx);
			return skynet_globalmq_pop();
		} else if (i==0 && weight >= 0) {
			// 根据权重计算要处理的消息数
			// weight=-1: 处理所有消息
			// weight=0: 处理队列长度
			// weight=1: 处理队列长度/2
			// weight=2: 处理队列长度/4，以此类推
			n = skynet_mq_length(q);
			n >>= weight;
		}
		
		// 检查队列是否过载（堆积太多消息）
		int overload = skynet_mq_overload(q);
		if (overload) {
			skynet_error(ctx, "error: May overload, message queue length = %d", overload);
		}

		// 通知监控器开始处理消息（用于死循环检测）
		skynet_monitor_trigger(sm, msg.source , handle);

		// 调用回调处理消息（如果回调已设置）
		if (ctx->cb == NULL) {
			skynet_free(msg.data);
		} else {
			dispatch_message(ctx, &msg);
		}

		// 通知监控器消息处理完成
		skynet_monitor_trigger(sm, 0,0);
	}

	// 当前处理的队列
	assert(q == ctx->queue);
	
	// 从全局队列取下一个队列
	struct message_queue *nq = skynet_globalmq_pop();
	if (nq) {
		// 如果全局队列不为空，将当前队列放回，处理下一个
		skynet_globalmq_push(q);
		q = nq;
	}
	// 如果全局队列为空，继续处理当前队列（不放回，减少竞争）
	
	skynet_context_release(ctx);

	return q;
}

/**
 * 【内部】复制名称到固定长度缓冲区
 */
static void
copy_name(char name[GLOBALNAME_LENGTH], const char * addr) {
	int i;
	for (i=0;i<GLOBALNAME_LENGTH && addr[i];i++) {
		name[i] = addr[i];
	}
	for (;i<GLOBALNAME_LENGTH;i++) {
		name[i] = '\0';
	}
}

/**
 * 【接口】根据名称查询服务句柄
 * 
 * 名称格式：
 * - :xxxxx  - 直接解析十六进制句柄
 * - .xxxxx  - 查询本地名称
 * 
 * @return 服务句柄，失败返回 0
 */
uint32_t
skynet_queryname(struct skynet_context * context, const char * name) {
	switch(name[0]) {
	case ':':
		return strtoul(name+1,NULL,16);
	case '.':
		return skynet_handle_findname(name + 1);
	}
	skynet_error(context, "error: Don't support query global name %s",name);
	return 0;
}

/**
 * 【内部】处理服务退出
 * 
 * @param context  发起退出的服务
 * @param handle   要退出的服务句柄（0 表示自己）
 */
static void
handle_exit(struct skynet_context * context, uint32_t handle) {
	if (handle == 0) {
		handle = context->handle;
		skynet_error(context, "KILL self");
	} else {
		skynet_error(context, "KILL :%0x", handle);
	}
	// 如果有监控服务，发送通知
	if (G_NODE.monitor_exit) {
		skynet_send(context,  handle, G_NODE.monitor_exit, PTYPE_CLIENT, 0, NULL, 0);
	}
	// 注销句柄（触发服务销毁）
	skynet_handle_retire(handle);
}

// ============================================================================
// Skynet 命令系统
// ============================================================================

/**
 * 【命令函数表】
 * Skynet 提供了一组内部命令，用于服务管理、环境变量操作等
 * 这些命令通过 skynet_command() 接口调用
 */

struct command_func {
	const char *name;
	const char * (*func)(struct skynet_context * context, const char * param);
};

/**
 * 【命令】TIMEOUT - 设置定时器
 * 
 * 参数格式：<时间(厘秒)> 
 * 返回：session ID
 */
static const char *
cmd_timeout(struct skynet_context * context, const char * param) {
	char * session_ptr = NULL;
	int ti = strtol(param, &session_ptr, 10);
	int session = skynet_context_newsession(context);
	skynet_timeout(context->handle, ti, session);
	sprintf(context->result, "%d", session);
	return context->result;
}

/**
 * 【命令】REG - 注册或查询句柄
 * 
 * 无参数：返回自己的句柄（如 :01000001）
 * 以 . 开头：注册本地名称（如 ".my_service"）
 */
static const char *
cmd_reg(struct skynet_context * context, const char * param) {
	if (param == NULL || param[0] == '\0') {
		sprintf(context->result, ":%x", context->handle);
		return context->result;
	} else if (param[0] == '.') {
		return skynet_handle_namehandle(context->handle, param + 1);
	} else {
		skynet_error(context, "error: Can't register global name %s in C", param);
		return NULL;
	}
}

/**
 * 【命令】QUERY - 查询本地名称对应的句柄
 */
static const char *
cmd_query(struct skynet_context * context, const char * param) {
	if (param[0] == '.') {
		uint32_t handle = skynet_handle_findname(param+1);
		if (handle) {
			sprintf(context->result, ":%x", handle);
			return context->result;
		}
	}
	return NULL;
}

/**
 * 【命令】NAME - 给指定句柄命名
 * 
 * 参数格式：<名称> <句柄>
 * 示例：NAME .my_service :01000001
 */
static const char *
cmd_name(struct skynet_context * context, const char * param) {
	int size = strlen(param);
	char name[size+1];
	char handle[size+1];
	sscanf(param,"%s %s",name,handle);
	if (handle[0] != ':') {
		return NULL;
	}
	uint32_t handle_id = strtoul(handle+1, NULL, 16);
	if (handle_id == 0) {
		return NULL;
	}
	if (name[0] == '.') {
		return skynet_handle_namehandle(handle_id, name + 1);
	} else {
		skynet_error(context, "error: Can't set global name %s in C", name);
	}
	return NULL;
}

/**
 * 【命令】EXIT - 退出当前服务
 */
static const char *
cmd_exit(struct skynet_context * context, const char * param) {
	handle_exit(context, 0);
	return NULL;
}

/**
 * 【辅助】将参数字符串转换为句柄
 */
static uint32_t
tohandle(struct skynet_context * context, const char * param) {
	uint32_t handle = 0;
	if (param[0] == ':') {
		handle = strtoul(param+1, NULL, 16);
	} else if (param[0] == '.') {
		handle = skynet_handle_findname(param+1);
	} else {
		skynet_error(context, "error: Can't convert %s to handle",param);
	}

	return handle;
}

/**
 * 【命令】KILL - 杀死指定服务
 */
static const char *
cmd_kill(struct skynet_context * context, const char * param) {
	uint32_t handle = tohandle(context, param);
	if (handle) {
		handle_exit(context, handle);
	}
	return NULL;
}

/**
 * 【命令】LAUNCH - 启动新服务
 * 
 * 参数格式：<模块名> <启动参数>
 * 示例：LAUNCH snlua my_service
 */
static const char *
cmd_launch(struct skynet_context * context, const char * param) {
	size_t sz = strlen(param);
	char tmp[sz+1];
	strcpy(tmp,param);
	char * args = tmp;
	char * mod = strsep(&args, " \t\r\n");
	args = strsep(&args, "\r\n");
	const uint32_t handle = skynet_context_new(mod,args);
	if (handle == 0) {
		return NULL;
	} else {
		id_to_hex(context->result, handle);
		return context->result;
	}
}

/**
 * 【命令】GETENV - 获取环境变量
 */
static const char *
cmd_getenv(struct skynet_context * context, const char * param) {
	return skynet_getenv(param);
}

/**
 * 【命令】SETENV - 设置环境变量
 * 
 * 参数格式：<key> <value>
 */
static const char *
cmd_setenv(struct skynet_context * context, const char * param) {
	size_t sz = strlen(param);
	char key[sz+1];
	int i;
	for (i=0;param[i] != ' ' && param[i];i++) {
		key[i] = param[i];
	}
	if (param[i] == '\0')
		return NULL;

	key[i] = '\0';
	param += i+1;

	skynet_setenv(key,param);
	return NULL;
}

/**
 * 【命令】STARTTIME - 获取启动时间
 */
static const char *
cmd_starttime(struct skynet_context * context, const char * param) {
	uint32_t sec = skynet_starttime();
	sprintf(context->result,"%u",sec);
	return context->result;
}

/**
 * 【命令】ABORT - 强制退出所有服务
 */
static const char *
cmd_abort(struct skynet_context * context, const char * param) {
	skynet_handle_retireall();
	return NULL;
}

/**
 * 【命令】MONITOR - 设置/查询监控服务
 * 
 * 监控服务会在其他服务退出时收到通知
 */
static const char *
cmd_monitor(struct skynet_context * context, const char * param) {
	uint32_t handle=0;
	if (param == NULL || param[0] == '\0') {
		if (G_NODE.monitor_exit) {
			// 返回当前监控服务
			sprintf(context->result, ":%x", G_NODE.monitor_exit);
			return context->result;
		}
		return NULL;
	} else {
		handle = tohandle(context, param);
	}
	G_NODE.monitor_exit = handle;
	return NULL;
}

/**
 * 【命令】STAT - 获取统计信息
 * 
 * 支持的统计项：
 * - mqlen:   消息队列长度
 * - endless: 是否陷入死循环
 * - cpu:     CPU 总耗时（秒）
 * - time:    当前消息处理时间（秒）
 * - message: 处理的消息总数
 */
static const char *
cmd_stat(struct skynet_context * context, const char * param) {
	if (strcmp(param, "mqlen") == 0) {
		int len = skynet_mq_length(context->queue);
		sprintf(context->result, "%d", len);
	} else if (strcmp(param, "endless") == 0) {
		if (context->endless) {
			strcpy(context->result, "1");
			context->endless = false;
		} else {
			strcpy(context->result, "0");
		}
	} else if (strcmp(param, "cpu") == 0) {
		double t = (double)context->cpu_cost / 1000000.0;	// 微秒转秒
		sprintf(context->result, "%lf", t);
	} else if (strcmp(param, "time") == 0) {
		if (context->profile) {
			uint64_t ti = skynet_thread_time() - context->cpu_start;
			double t = (double)ti / 1000000.0;
			sprintf(context->result, "%lf", t);
		} else {
			strcpy(context->result, "0");
		}
	} else if (strcmp(param, "message") == 0) {
		sprintf(context->result, "%zu", context->message_count);
	} else {
		context->result[0] = '\0';
	}
	return context->result;
}

/**
 * 【命令】LOGON - 开启消息日志
 * 
 * 将指定服务的所有收发消息记录到文件
 */
static const char *
cmd_logon(struct skynet_context * context, const char * param) {
	uint32_t handle = tohandle(context, param);
	if (handle == 0)
		return NULL;
	struct skynet_context * ctx = skynet_handle_grab(handle);
	if (ctx == NULL)
		return NULL;
	FILE *f = NULL;
	FILE * lastf = (FILE *)ATOM_LOAD(&ctx->logfile);
	if (lastf == NULL) {
		f = skynet_log_open(context, handle);
		if (f) {
			// CAS 操作确保线程安全
			if (!ATOM_CAS_POINTER(&ctx->logfile, 0, (uintptr_t)f)) {
				// 其他线程已打开，关闭这个
				fclose(f);
			}
		}
	}
	skynet_context_release(ctx);
	return NULL;
}

/**
 * 【命令】LOGOFF - 关闭消息日志
 */
static const char *
cmd_logoff(struct skynet_context * context, const char * param) {
	uint32_t handle = tohandle(context, param);
	if (handle == 0)
		return NULL;
	struct skynet_context * ctx = skynet_handle_grab(handle);
	if (ctx == NULL)
		return NULL;
	FILE * f = (FILE *)ATOM_LOAD(&ctx->logfile);
	if (f) {
		// CAS 操作确保线程安全
		if (ATOM_CAS_POINTER(&ctx->logfile, (uintptr_t)f, (uintptr_t)NULL)) {
			skynet_log_close(context, f, handle);
		}
	}
	skynet_context_release(ctx);
	return NULL;
}

/**
 * 【命令】SIGNAL - 向服务发送信号
 */
static const char *
cmd_signal(struct skynet_context * context, const char * param) {
	uint32_t handle = tohandle(context, param);
	if (handle == 0)
		return NULL;
	struct skynet_context * ctx = skynet_handle_grab(handle);
	if (ctx == NULL)
		return NULL;
	param = strchr(param, ' ');
	int sig = 0;
	if (param) {
		sig = strtol(param, NULL, 0);
	}
	// 注意：信号函数需要是线程安全的
	skynet_module_instance_signal(ctx->mod, ctx->instance, sig);

	skynet_context_release(ctx);
	return NULL;
}

/**
 * 【命令表】
 */
static struct command_func cmd_funcs[] = {
	{ "TIMEOUT", cmd_timeout },
	{ "REG", cmd_reg },
	{ "QUERY", cmd_query },
	{ "NAME", cmd_name },
	{ "EXIT", cmd_exit },
	{ "KILL", cmd_kill },
	{ "LAUNCH", cmd_launch },
	{ "GETENV", cmd_getenv },
	{ "SETENV", cmd_setenv },
	{ "STARTTIME", cmd_starttime },
	{ "ABORT", cmd_abort },
	{ "MONITOR", cmd_monitor },
	{ "STAT", cmd_stat },
	{ "LOGON", cmd_logon },
	{ "LOGOFF", cmd_logoff },
	{ "SIGNAL", cmd_signal },
	{ NULL, NULL },
};

/**
 * 【接口】执行 Skynet 命令
 * 
 * @param context  当前服务上下文
 * @param cmd      命令名称
 * @param param    命令参数
 * @return         命令返回的字符串（可能为 NULL）
 */
const char *
skynet_command(struct skynet_context * context, const char * cmd , const char * param) {
	struct command_func * method = &cmd_funcs[0];
	while(method->name) {
		if (strcmp(cmd, method->name) == 0) {
			return method->func(context, param);
		}
		++method;
	}

	return NULL;
}

/**
 * 【内部】过滤和预处理消息参数
 * 
 * 处理消息类型标志：
 * - PTYPE_TAG_DONTCOPY: 不复制消息数据
 * - PTYPE_TAG_ALLOCSESSION: 自动分配 session
 */
static void
_filter_args(struct skynet_context * context, int type, int *session, void ** data, size_t * sz) {
	int needcopy = !(type & PTYPE_TAG_DONTCOPY);
	int allocsession = type & PTYPE_TAG_ALLOCSESSION;
	type &= 0xff;

	if (allocsession) {
		assert(*session == 0);
		*session = skynet_context_newsession(context);
	}

	if (needcopy && *data) {
		char * msg = skynet_malloc(*sz+1);
		memcpy(msg, *data, *sz);
		msg[*sz] = '\0';
		*data = msg;
	}

	// 将消息类型编码到 sz 的高8位
	*sz |= (size_t)type << MESSAGE_TYPE_SHIFT;
}

/**
 * 【核心接口】发送消息
 * 
 * 这是 Skynet 最核心的消息发送接口：
 * - 如果目标是远程服务，通过 harbor 发送
 * - 如果目标是本地服务，直接推送到其消息队列
 * 
 * @param context     发送者上下文（可为 NULL）
 * @param source      发送者句柄（0 表示使用 context 的句柄）
 * @param destination 目标句柄
 * @param type        消息类型（含标志位）
 * @param session     会话ID（用于请求-响应匹配）
 * @param data        消息数据
 * @param sz          消息大小
 * @return            session ID（失败返回 -1 或 -2）
 */
int
skynet_send(struct skynet_context * context, uint32_t source, uint32_t destination , int type, int session, void * data, size_t sz) {
	// 检查消息大小是否超出限制（24位能表示的最大值）
	if ((sz & MESSAGE_TYPE_MASK) != sz) {
		skynet_error(context, "error: The message to %x is too large", destination);
		if (type & PTYPE_TAG_DONTCOPY) {
			skynet_free(data);
		}
		return -2;
	}
	
	// 处理类型标志
	_filter_args(context, type, &session, (void **)&data, &sz);

	// 如果 source 为 0，使用当前 context 的句柄
	if (source == 0) {
		source = context->handle;
	}

	// destination 为 0 表示发送给自己（用于 timeout）
	if (destination == 0) {
		if (data) {
			skynet_error(context, "error: Destination address can't be 0");
			skynet_free(data);
			return -1;
		}
		return session;
	}
	
	// 判断是否是远程消息
	if (skynet_harbor_message_isremote(destination)) {
		// 远程消息，通过 harbor 发送
		struct remote_message * rmsg = skynet_malloc(sizeof(*rmsg));
		rmsg->destination.handle = destination;
		rmsg->message = data;
		rmsg->sz = sz & MESSAGE_TYPE_MASK;
		rmsg->type = sz >> MESSAGE_TYPE_SHIFT;
		skynet_harbor_send(rmsg, source, session);
	} else {
		// 本地消息，直接推送
		struct skynet_message smsg;
		smsg.source = source;
		smsg.session = session;
		smsg.data = data;
		smsg.sz = sz;

		if (skynet_context_push(destination, &smsg)) {
			skynet_free(data);
			return -1;
		}
	}
	return session;
}

/**
 * 【接口】通过名称发送消息
 * 
 * 支持通过名称（如 ".my_service"）发送消息
 * 如果是全局名称（不以 . 开头），通过 harbor 发送到远程
 */
int
skynet_sendname(struct skynet_context * context, uint32_t source, const char * addr , int type, int session, void * data, size_t sz) {
	if (source == 0) {
		source = context->handle;
	}
	uint32_t des = 0;
	if (addr[0] == ':') {
		// 十六进制句柄
		des = strtoul(addr+1, NULL, 16);
	} else if (addr[0] == '.') {
		// 本地名称
		des = skynet_handle_findname(addr + 1);
		if (des == 0) {
			if (type & PTYPE_TAG_DONTCOPY) {
				skynet_free(data);
			}
			return -1;
		}
	} else {
		// 全局名称，通过 harbor 发送
		if ((sz & MESSAGE_TYPE_MASK) != sz) {
			skynet_error(context, "error: The message to %s is too large", addr);
			if (type & PTYPE_TAG_DONTCOPY) {
				skynet_free(data);
			}
			return -2;
		}
		_filter_args(context, type, &session, (void **)&data, &sz);

		struct remote_message * rmsg = skynet_malloc(sizeof(*rmsg));
		copy_name(rmsg->destination.name, addr);
		rmsg->destination.handle = 0;
		rmsg->message = data;
		rmsg->sz = sz & MESSAGE_TYPE_MASK;
		rmsg->type = sz >> MESSAGE_TYPE_SHIFT;

		skynet_harbor_send(rmsg, source, session);
		return session;
	}

	return skynet_send(context, source, des, type, session, data, sz);
}

/**
 * 【接口】获取服务的句柄
 */
uint32_t
skynet_context_handle(struct skynet_context *ctx) {
	return ctx->handle;
}

/**
 * 【接口】设置消息回调函数
 * 
 * 服务通过设置回调函数来接收和处理消息
 */
void
skynet_callback(struct skynet_context * context, void *ud, skynet_cb cb) {
	context->cb = cb;
	context->cb_ud = ud;
}

/**
 * 【接口】直接发送消息到指定服务（不通过句柄查找）
 * 
 * 用于已知 context 指针的情况，避免句柄查找的开销
 */
void
skynet_context_send(struct skynet_context * ctx, void * msg, size_t sz, uint32_t source, int type, int session) {
	struct skynet_message smsg;
	smsg.source = source;
	smsg.session = session;
	smsg.data = msg;
	smsg.sz = sz | (size_t)type << MESSAGE_TYPE_SHIFT;

	skynet_mq_push(ctx->queue, &smsg);
}

/**
 * 【接口】初始化 Skynet 全局节点
 */
void
skynet_globalinit(void) {
	ATOM_INIT(&G_NODE.total , 0);
	G_NODE.monitor_exit = 0;
	G_NODE.init = 1;
	if (pthread_key_create(&G_NODE.handle_key, NULL)) {
		fprintf(stderr, "pthread_key_create failed");
		exit(1);
	}
	// 设置主线程的 key
	skynet_initthread(THREAD_MAIN);
}

/**
 * 【接口】清理 Skynet 全局节点
 */
void
skynet_globalexit(void) {
	pthread_key_delete(G_NODE.handle_key);
}

/**
 * 【接口】初始化线程类型
 * 
 * 每个线程启动时需要设置其类型，用于调试和监控
 */
void
skynet_initthread(int m) {
	uintptr_t v = (uint32_t)(-m);
	pthread_setspecific(G_NODE.handle_key, (void *)v);
}

/**
 * 【接口】启用/禁用性能分析
 */
void
skynet_profile_enable(int enable) {
	G_NODE.profile = (bool)enable;
}
