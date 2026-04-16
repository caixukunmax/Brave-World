/**
 * ============================================================================
 * Skynet 启动模块 - skynet_start.c
 * ============================================================================
 * 
 * 【文件作用】
 * 负责 Skynet 框架的启动流程，包括：
 * 1. 初始化各个子系统（harbor、handle、mq、module、timer、socket）
 * 2. 创建并管理各类工作线程
 * 3. 启动引导服务（bootstrap）
 * 4. 协调线程间的同步与退出
 * 
 * 【线程模型】
 * Skynet 使用多线程架构，包含以下几类线程：
 * - 监控线程（1个）：检测服务死循环
 * - 定时器线程（1个）：驱动时间轮，每 2.5ms 更新一次
 * - Socket 线程（1个）：处理网络事件（epoll/kqueue）
 * - 工作线程（N个）：执行业务逻辑，处理消息（N = config.thread）
 * 
 * 【权重调度】
 * 工作线程有不同的处理权重，影响每次调度处理的消息数量
 * ============================================================================
 */

#include "skynet.h"
#include "skynet_server.h"
#include "skynet_imp.h"
#include "skynet_mq.h"
#include "skynet_handle.h"
#include "skynet_module.h"
#include "skynet_timer.h"
#include "skynet_monitor.h"
#include "skynet_socket.h"
#include "skynet_daemon.h"
#include "skynet_harbor.h"

#include <pthread.h>
#include <unistd.h>
#include <assert.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <signal.h>

/**
 * 【监控结构体】
 * 用于管理所有工作线程的监控和同步
 */
struct monitor {
	int count;                      // 工作线程总数
	struct skynet_monitor ** m;     // 每个工作线程对应的监控器数组
	pthread_cond_t cond;            // 条件变量（用于线程休眠/唤醒）
	pthread_mutex_t mutex;          // 互斥锁
	int sleep;                      // 当前休眠的工作线程数
	int quit;                       // 退出标志
};

/**
 * 【工作线程参数】
 * 传递给每个工作线程的参数
 */
struct worker_parm {
	struct monitor *m;              // 监控结构体
	int id;                         // 线程ID
	int weight;                     // 权重（影响处理消息的数量）
};

static volatile int SIG = 0;      // SIGHUP 信号标志

/**
 * 【信号处理函数】
 * 处理 SIGHUP 信号（通常用于重新打开日志文件）
 */
static void
handle_hup(int signal) {
	if (signal == SIGHUP) {
		SIG = 1;
	}
}

// 检查是否需要中止（没有服务时退出）
#define CHECK_ABORT if (skynet_context_total()==0) break;

/**
 * 【创建线程】
 * 封装 pthread_create，失败时退出程序
 */
static void
create_thread(pthread_t *thread, void *(*start_routine) (void *), void *arg) {
	if (pthread_create(thread,NULL, start_routine, arg)) {
		fprintf(stderr, "Create thread failed");
		exit(1);
	}
}

/**
 * 【唤醒工作线程】
 * 当有新消息或网络事件时唤醒休眠的工作线程
 * 
 * @param m     监控结构体
 * @param busy  忙碌线程数（这些线程不需要被唤醒）
 */
static void
wakeup(struct monitor *m, int busy) {
	if (m->sleep >= m->count - busy) {
		// 发送信号给休眠的工作线程
		// "spurious wakeup"（虚假唤醒）是无害的
		pthread_cond_signal(&m->cond);
	}
}

/**
 * 【Socket 线程】
 * 负责处理所有网络 I/O 事件
 * 
 * 工作流程：
 * 1. 初始化 socket 线程标识
 * 2. 进入事件循环，调用 skynet_socket_poll() 处理网络事件
 * 3. 当有事件发生时，唤醒工作线程处理消息
 */
static void *
thread_socket(void *p) {
	struct monitor * m = p;
	skynet_initthread(THREAD_SOCKET);      // 设置线程类型
	skynet_handle_register_thread();        // 注册线程句柄
	for (;;) {
		int r = skynet_socket_poll();       // 轮询网络事件
		if (r==0)
			break;                          // 返回0表示退出
		if (r<0) {
			CHECK_ABORT                     // 检查是否应该退出
			continue;
		}
		wakeup(m,0);                        // 唤醒工作线程处理网络消息
	}
	return NULL;
}

/**
 * 【释放监控结构体】
 */
static void
free_monitor(struct monitor *m) {
	int i;
	int n = m->count;
	for (i=0;i<n;i++) {
		skynet_monitor_delete(m->m[i]);
	}
	pthread_mutex_destroy(&m->mutex);
	pthread_cond_destroy(&m->cond);
	skynet_free(m->m);
	skynet_free(m);
}

/**
 * 【监控线程】
 * 定期检查工作线程是否卡死（死循环）
 * 
 * 检查频率：每 5 秒检查一次所有工作线程
 */
static void *
thread_monitor(void *p) {
	struct monitor * m = p;
	int i;
	int n = m->count;
	skynet_initthread(THREAD_MONITOR);
	skynet_handle_register_thread();
	for (;;) {
		CHECK_ABORT
		// 检查所有工作线程
		for (i=0;i<n;i++) {
			skynet_monitor_check(m->m[i]);
		}
		// 休眠 5 秒（分 5 次休眠，每次检查是否需要退出）
		for (i=0;i<5;i++) {
			CHECK_ABORT
			sleep(1);
		}
	}

	return NULL;
}

/**
 * 【处理 SIGHUP 信号】
 * 向 logger 服务发送系统消息，触发日志文件重新打开
 */
static void
signal_hup() {
	struct skynet_message smsg;
	smsg.source = 0;
	smsg.session = 0;
	smsg.data = NULL;
	smsg.sz = (size_t)PTYPE_SYSTEM << MESSAGE_TYPE_SHIFT;
	uint32_t logger = skynet_handle_findname("logger");
	if (logger) {
		skynet_context_push(logger, &smsg);
	}
}

/**
 * 【定时器线程】
 * 驱动时间轮，定期更新系统时间
 * 
 * 执行周期：每 2.5 毫秒（2500 微秒）
 * 
 * 工作内容：
 * 1. 更新系统时间（skynet_updatetime）
 * 2. 更新 socket 超时检查
 * 3. 唤醒工作线程处理到期的定时器消息
 * 4. 处理 SIGHUP 信号
 */
static void *
thread_timer(void *p) {
	struct monitor * m = p;
	skynet_initthread(THREAD_TIMER);
	skynet_handle_register_thread();
	for (;;) {
		skynet_updatetime();                // 更新时间轮
		skynet_socket_updatetime();         // 更新 socket 超时
		CHECK_ABORT
		wakeup(m,m->count-1);               // 唤醒工作线程
		usleep(2500);                       // 休眠 2.5ms
		if (SIG) {
			signal_hup();                   // 处理 SIGHUP
			SIG = 0;
		}
	}
	// 退出时唤醒其他线程
	skynet_socket_exit();                   // 唤醒 socket 线程
	// 唤醒所有工作线程
	pthread_mutex_lock(&m->mutex);
	m->quit = 1;
	pthread_cond_broadcast(&m->cond);
	pthread_mutex_unlock(&m->mutex);
	return NULL;
}

/**
 * 【工作线程】
 * 核心消息处理线程，执行实际的服务逻辑
 * 
 * @param p  worker_parm 结构体，包含线程ID、权重等
 * 
 * 工作流程：
 * 1. 从全局队列获取消息队列
 * 2. 从消息队列中取出消息
 * 3. 调用对应服务的回调函数处理消息
 * 4. 如果没有消息，进入休眠等待
 */
static void *
thread_worker(void *p) {
	struct worker_parm *wp = p;
	int id = wp->id;                        // 线程ID
	int weight = wp->weight;                // 处理权重
	struct monitor *m = wp->m;
	struct skynet_monitor *sm = m->m[id];   // 本线程的监控器
	skynet_initthread(THREAD_WORKER);
	skynet_handle_register_thread();
	struct message_queue * q = NULL;        // 当前处理的消息队列
	while (!m->quit) {
		// 分发消息，返回下一个要处理的消息队列
		q = skynet_context_message_dispatch(sm, q, weight);
		if (q == NULL) {
			// 没有可处理的消息，进入休眠
			if (pthread_mutex_lock(&m->mutex) == 0) {
				++ m->sleep;                // 休眠计数+1
				// 等待条件变量（虚假唤醒无害）
				if (!m->quit)
					pthread_cond_wait(&m->cond, &m->mutex);
				-- m->sleep;                // 被唤醒，休眠计数-1
				if (pthread_mutex_unlock(&m->mutex)) {
					fprintf(stderr, "unlock mutex error");
					exit(1);
				}
			}
		}
	}
	return NULL;
}

/**
 * 【启动所有线程】
 * 
 * 线程创建顺序：
 * 1. 监控线程（pid[0]）
 * 2. 定时器线程（pid[1]）
 * 3. Socket 线程（pid[2]）
 * 4. 工作线程（pid[3] ~ pid[thread+2]）
 * 
 * 工作线程权重表：
 * - -1: 处理所有堆积的消息（4个线程）
 * -  0: 平均处理（4个线程）
 * -  1: 处理 1/2 消息（8个线程）
 * -  2: 处理 1/4 消息（8个线程）
 * -  3: 处理 1/8 消息（8个线程）
 */
static void
start(int thread) {
	pthread_t pid[thread+3];

	// 初始化监控结构体
	struct monitor *m = skynet_malloc(sizeof(*m));
	memset(m, 0, sizeof(*m));
	m->count = thread;
	m->sleep = 0;

	// 为每个工作线程创建监控器
	m->m = skynet_malloc(thread * sizeof(struct skynet_monitor *));
	int i;
	for (i=0;i<thread;i++) {
		m->m[i] = skynet_monitor_new();
	}
	if (pthread_mutex_init(&m->mutex, NULL)) {
		fprintf(stderr, "Init mutex error");
		exit(1);
	}
	if (pthread_cond_init(&m->cond, NULL)) {
		fprintf(stderr, "Init cond error");
		exit(1);
	}

	// 创建各类线程
	create_thread(&pid[0], thread_monitor, m);
	create_thread(&pid[1], thread_timer, m);
	create_thread(&pid[2], thread_socket, m);

	// 工作线程权重表（影响每次处理多少消息）
	static int weight[] = {
		-1, -1, -1, -1, 0, 0, 0, 0,       // 权重 -1 和 0
		1, 1, 1, 1, 1, 1, 1, 1,           // 权重 1
		2, 2, 2, 2, 2, 2, 2, 2,           // 权重 2
		3, 3, 3, 3, 3, 3, 3, 3, };        // 权重 3
	struct worker_parm wp[thread];
	for (i=0;i<thread;i++) {
		wp[i].m = m;
		wp[i].id = i;
		if (i < sizeof(weight)/sizeof(weight[0])) {
			wp[i].weight= weight[i];
		} else {
			wp[i].weight = 0;
		}
		create_thread(&pid[i+3], thread_worker, &wp[i]);
	}

	// 等待所有线程结束
	for (i=0;i<thread+3;i++) {
		pthread_join(pid[i], NULL);
	}

	free_monitor(m);
}

/**
 * 【启动引导服务】
 * 启动配置中指定的 bootstrap 服务
 * 
 * @param logger_handle  logger 服务的句柄
 * @param cmdline        启动命令（如 "snlua bootstrap"）
 * 
 * 命令格式：<模块名> <参数>
 * 示例："snlua bootstrap" -> 加载 snlua 模块，参数为 "bootstrap"
 */
static void
bootstrap(uint32_t logger_handle, const char * cmdline) {
	int sz = strlen(cmdline);
	char name[sz+1];
	char args[sz+1];
	int arg_pos;
	sscanf(cmdline, "%s", name);
	arg_pos = strlen(name);
	if (arg_pos < sz) {
		while(cmdline[arg_pos] == ' ') {
			arg_pos++;
		}
		strncpy(args, cmdline + arg_pos, sz);
	} else {
		args[0] = '\0';
	}
	const uint32_t handle = skynet_context_new(name, args);
	if (handle == 0) {
		struct skynet_context *logger = skynet_handle_grab(logger_handle);
		if (logger != NULL) {
			skynet_error(NULL, "Bootstrap error : %s\n", cmdline);
			skynet_context_dispatchall(logger);
			skynet_context_release(logger);
		}
		exit(1);
	}
}

/**
 * 【Skynet 启动入口】
 * 
 * 初始化流程：
 * 1. 注册 SIGHUP 信号处理
 * 2. 初始化守护进程（如果配置了）
 * 3. 初始化各子系统
 * 4. 创建 logger 服务
 * 5. 启动引导服务
 * 6. 启动所有工作线程
 * 7. 清理资源
 */
void
skynet_start(struct skynet_config * config) {
	// 注册 SIGHUP 信号（用于日志重开）
	struct sigaction sa;
	sa.sa_handler = &handle_hup;
	sa.sa_flags = SA_RESTART;
	sigfillset(&sa.sa_mask);
	sigaction(SIGHUP, &sa, NULL);

	// 初始化守护进程
	if (config->daemon) {
		if (daemon_init(config->daemon)) {
			exit(1);
		}
	}

	// 【初始化各子系统】（按依赖顺序）
	skynet_harbor_init(config->harbor);         // 集群系统
	skynet_handle_init(config->harbor, config->thread);  // 句柄管理
	skynet_mq_init();                           // 消息队列
	skynet_module_init(config->module_path);    // 模块系统
	skynet_timer_init();                        // 定时器
	skynet_socket_init();                       // Socket
	skynet_profile_enable(config->profile);     // 性能分析

	// 创建 logger 服务
	const uint32_t logger_handle = skynet_context_new(config->logservice, config->logger);
	if (logger_handle == 0) {
		fprintf(stderr, "Can't launch %s service\n", config->logservice);
		exit(1);
	}

	// 给 logger 服务命名，方便其他服务查找
	skynet_handle_namehandle(logger_handle, "logger");

	// 启动引导服务
	bootstrap(logger_handle, config->bootstrap);

	// 启动所有工作线程（阻塞，直到所有线程结束）
	start(config->thread);

	// 清理资源
	// harbor_exit 可能调用 socket send，所以要在 socket_free 之前
	skynet_harbor_exit();
	skynet_socket_free();
	if (config->daemon) {
		daemon_exit(config->daemon);
	}
}
