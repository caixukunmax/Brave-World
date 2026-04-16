/**
 * ============================================================================
 * Skynet 定时器模块 - skynet_timer.c
 * ============================================================================
 * 
 * 【文件作用】
 * 实现 Skynet 的高精度定时器系统，基于分级时间轮（Hierarchical Timing Wheels）算法
 * 
 * 【时间轮算法】
 * 使用 5 级时间轮（1 个近级 + 4 个远级）：
 * - 近级：256 个槽（TIME_NEAR = 1<<8），每 1/100 秒 tick 一次
 * - 远级1-4：每级 64 个槽（TIME_LEVEL = 1<<6）
 * 
 * 总时间范围：256 * (64^4) / 100 秒 ≈ 497 天
 * 
 * 【定时单位】
 * 使用"厘秒"（centisecond，1/100 秒）作为时间单位
 * 这与 Skynet 的 tick 频率（100Hz）匹配
 * 
 * 【触发方式】
 * 定时器到期时，向目标服务发送一条 PTYPE_RESPONSE 类型的消息
 * 消息中的 session 用于标识是哪个定时器
 * 
 * 【接口】
 * - skynet_timeout(handle, time, session): 设置定时器
 * - skynet_updatetime(): 更新时间轮（由定时器线程每 2.5ms 调用）
 * - skynet_now(): 获取当前时间（厘秒）
 * ============================================================================
 */

#include "skynet.h"

#include "skynet_timer.h"
#include "skynet_mq.h"
#include "skynet_server.h"
#include "skynet_handle.h"
#include "spinlock.h"

#include <time.h>
#include <assert.h>
#include <string.h>
#include <stdlib.h>
#include <stdint.h>

// 定时器回调函数类型
typedef void (*timer_execute_func)(void *ud,void *arg);

/**
 * 【时间轮配置】
 * 使用 8+6+6+6+6 = 32 位来表示时间
 */
#define TIME_NEAR_SHIFT 8
#define TIME_NEAR (1 << TIME_NEAR_SHIFT)       // 256
#define TIME_LEVEL_SHIFT 6
#define TIME_LEVEL (1 << TIME_LEVEL_SHIFT)     // 64
#define TIME_NEAR_MASK (TIME_NEAR-1)           // 0xFF
#define TIME_LEVEL_MASK (TIME_LEVEL-1)         // 0x3F

/**
 * 【数据结构】定时器事件
 * 
 * 存储在定时器节点中，到期时用于构造消息
 */
struct timer_event {
	uint32_t handle;    // 目标服务句柄
	int session;        // 会话ID（用于标识定时器）
};

/**
 * 【数据结构】定时器节点
 * 
 * 链表节点，存储在定时器队列中
 */
struct timer_node {
	struct timer_node *next;    // 链表下一个节点
	uint32_t expire;            // 到期时间（相对于 starttime 的厘秒数）
};

/**
 * 【数据结构】定时器链表
 * 
 * 时间轮的每个槽是一个链表，用于存储相同到期时间的定时器
 */
struct link_list {
	struct timer_node head;     // 链表头（哑节点）
	struct timer_node *tail;    // 链表尾指针
};

/**
 * 【数据结构】定时器
 * 
 * 分级时间轮的主结构
 */
struct timer {
	struct link_list near[TIME_NEAR];       // 近级时间轮（256槽）
	struct link_list t[4][TIME_LEVEL];      // 远级时间轮（4级，每级64槽）
	struct spinlock lock;                   // 自旋锁
	uint32_t time;                          // 当前时间（32位循环计数器）
	uint32_t starttime;                     // 启动时的 Unix 时间戳
	uint64_t current;                       // 当前时间（相对于 starttime 的厘秒数）
	uint64_t current_point;                 // 上一次的系统时间（用于计算时间差）
};

// 全局定时器实例
static struct timer * TI = NULL;

/**
 * 【内部】清空链表，返回原链表头
 */
static inline struct timer_node *
link_clear(struct link_list *list) {
	struct timer_node * ret = list->head.next;
	list->head.next = 0;
	list->tail = &(list->head);

	return ret;
}

/**
 * 【内部】将节点添加到链表尾部
 */
static inline void
link(struct link_list *list,struct timer_node *node) {
	list->tail->next = node;
	list->tail = node;
	node->next=0;
}

/**
 * 【内部】将节点添加到合适的时间轮槽
 * 
 * 根据到期时间与当前时间的差值，决定放入哪一级时间轮
 */
static void
add_node(struct timer *T,struct timer_node *node) {
	uint32_t time=node->expire;
	uint32_t current_time=T->time;
	
	// 检查是否在近级时间轮的范围内（低8位相同）
	if ((time|TIME_NEAR_MASK)==(current_time|TIME_NEAR_MASK)) {
		link(&T->near[time&TIME_NEAR_MASK],node);
	} else {
		// 放入远级时间轮
		int i;
		uint32_t mask=TIME_NEAR << TIME_LEVEL_SHIFT;
		for (i=0;i<3;i++) {
			if ((time|(mask-1))==(current_time|(mask-1))) {
				break;
			}
			mask <<= TIME_LEVEL_SHIFT;
		}

		link(&T->t[i][((time>>(TIME_NEAR_SHIFT + i*TIME_LEVEL_SHIFT)) & TIME_LEVEL_MASK)],node);	
	}
}

/**
 * 【内部】添加定时器
 * 
 * @param T     定时器实例
 * @param arg   定时器事件数据
 * @param sz    数据大小
 * @param time  到期时间（相对于当前时间的厘秒数）
 */
static void
timer_add(struct timer *T,void *arg,size_t sz,int time) {
	// 分配节点内存（节点 + 事件数据）
	struct timer_node *node = (struct timer_node *)skynet_malloc(sizeof(*node)+sz);
	memcpy(node+1,arg,sz);

	SPIN_LOCK(T);

		node->expire=time+T->time;  // 计算绝对到期时间
		add_node(T,node);

	SPIN_UNLOCK(T);
}

/**
 * 【内部】将远级时间轮的链表移动到近级
 * 
 * 当时间推进到某一级时间轮的某个槽时，将该槽的链表降级到近级时间轮
 */
static void
move_list(struct timer *T, int level, int idx) {
	struct timer_node *current = link_clear(&T->t[level][idx]);
	while (current) {
		struct timer_node *temp=current->next;
		add_node(T,current);
		current=temp;
	}
}

/**
 * 【内部】时间轮推进
 * 
 * 每 tick 一次，推进时间指针，并处理需要降级的时间轮
 */
static void
timer_shift(struct timer *T) {
	int mask = TIME_NEAR;
	uint32_t ct = ++T->time;
	if (ct == 0) {
		// 时间溢出，处理最高级时间轮
		move_list(T, 3, 0);
	} else {
		uint32_t time = ct >> TIME_NEAR_SHIFT;
		int i=0;

		// 检查每一级时间轮是否需要推进
		while ((ct & (mask-1))==0) {
			int idx=time & TIME_LEVEL_MASK;
			if (idx!=0) {
				// 推进到非0槽，移动该槽的链表
				move_list(T, i, idx);
				break;				
			}
			mask <<= TIME_LEVEL_SHIFT;
			time >>= TIME_LEVEL_SHIFT;
			++i;
		}
	}
}

/**
 * 【内部】分发定时器消息
 * 
 * 将到期的定时器转换为消息发送给目标服务
 */
static inline void
dispatch_list(struct timer_node *current) {
	do {
		struct timer_event * event = (struct timer_event *)(current+1);
		struct skynet_message message;
		message.source = 0;
		message.session = event->session;
		message.data = NULL;
		message.sz = (size_t)PTYPE_RESPONSE << MESSAGE_TYPE_SHIFT;

		// 向目标服务推送定时器消息
		skynet_context_push(event->handle, &message);
		
		struct timer_node * temp = current;
		current=current->next;
		skynet_free(temp);	
	} while (current);
}

/**
 * 【内部】执行当前到期的定时器
 */
static inline void
timer_execute(struct timer *T) {
	int idx = T->time & TIME_NEAR_MASK;
	
	// 处理该槽链表中的所有定时器
	while (T->near[idx].head.next) {
		struct timer_node *current = link_clear(&T->near[idx]);
		SPIN_UNLOCK(T);
		// dispatch_list 不需要锁 T
		dispatch_list(current);
		SPIN_LOCK(T);
	}
}

/**
 * 【内部】定时器更新（每次 tick 调用）
 */
static void 
timer_update(struct timer *T) {
	SPIN_LOCK(T);

	// 先执行到期的定时器（处理 timeout=0 的情况）
	timer_execute(T);

	// 推进时间轮
	timer_shift(T);

	// 再次执行到期的定时器
	timer_execute(T);

	SPIN_UNLOCK(T);
}

/**
 * 【内部】创建定时器实例
 */
static struct timer *
timer_create_timer() {
	struct timer *r=(struct timer *)skynet_malloc(sizeof(struct timer));
	memset(r,0,sizeof(*r));

	int i,j;

	// 初始化近级时间轮
	for (i=0;i<TIME_NEAR;i++) {
		link_clear(&r->near[i]);
	}

	// 初始化远级时间轮
	for (i=0;i<4;i++) {
		for (j=0;j<TIME_LEVEL;j++) {
			link_clear(&r->t[i][j]);
		}
	}

	SPIN_INIT(r)

	r->current = 0;

	return r;
}

/**
 * 【接口】设置定时器
 * 
 * @param handle   目标服务句柄
 * @param time     延迟时间（厘秒，1/100 秒）
 * @param session  会话ID
 * @return         session（time<=0 时可能返回 -1）
 */
int
skynet_timeout(uint32_t handle, int time, int session) {
	if (time <= 0) {
		// 立即触发，直接发送消息
		struct skynet_message message;
		message.source = 0;
		message.session = session;
		message.data = NULL;
		message.sz = (size_t)PTYPE_RESPONSE << MESSAGE_TYPE_SHIFT;

		if (skynet_context_push(handle, &message)) {
			return -1;
		}
	} else {
		// 添加定时器
		struct timer_event event;
		event.handle = handle;
		event.session = session;
		timer_add(TI, &event, sizeof(event), time);
	}

	return session;
}

/**
 * 【内部】获取系统时间
 * 
 * @param sec  输出秒数
 * @param cs   输出厘秒数（0-99）
 */
static void
systime(uint32_t *sec, uint32_t *cs) {
	struct timespec ti;
	clock_gettime(CLOCK_REALTIME, &ti);
	*sec = (uint32_t)ti.tv_sec;
	*cs = (uint32_t)(ti.tv_nsec / 10000000);  // 纳秒转厘秒
}

/**
 * 【内部】获取单调递增时间（厘秒）
 */
static uint64_t
gettime() {
	uint64_t t;
	struct timespec ti;
	clock_gettime(CLOCK_MONOTONIC, &ti);
	t = (uint64_t)ti.tv_sec * 100;
	t += ti.tv_nsec / 10000000;  // 纳秒转厘秒
	return t;
}

/**
 * 【接口】更新定时器（由定时器线程定期调用）
 * 
 * 计算时间差，对每过去的一厘秒调用一次 timer_update
 */
void
skynet_updatetime(void) {
	uint64_t cp = gettime();
	if(cp < TI->current_point) {
		skynet_error(NULL, "time diff error: change from %lld to %lld", cp, TI->current_point);
		TI->current_point = cp;
	} else if (cp != TI->current_point) {
		uint32_t diff = (uint32_t)(cp - TI->current_point);
		TI->current_point = cp;
		TI->current += diff;
		int i;
		for (i=0;i<diff;i++) {
			timer_update(TI);
		}
	}
}

/**
 * 【接口】获取启动时间戳（秒）
 */
uint32_t
skynet_starttime(void) {
	return TI->starttime;
}

/**
 * 【接口】获取当前时间（相对于启动的厘秒数）
 */
uint64_t 
skynet_now(void) {
	return TI->current;
}

/**
 * 【接口】初始化定时器
 */
void 
skynet_timer_init(void) {
	TI = timer_create_timer();
	uint32_t current = 0;
	systime(&TI->starttime, &current);
	TI->current = current;
	TI->current_point = gettime();
}

/**
 * ============================================================================
 * 性能分析相关函数
 * ============================================================================
 */

#define NANOSEC 1000000000
#define MICROSEC 1000000

/**
 * 【接口】获取当前线程的 CPU 时间（微秒）
 * 
 * 用于性能分析，统计服务处理消息的 CPU 耗时
 */
uint64_t
skynet_thread_time(void) {
	struct timespec ti;
	clock_gettime(CLOCK_THREAD_CPUTIME_ID, &ti);

	return (uint64_t)ti.tv_sec * MICROSEC + (uint64_t)ti.tv_nsec / (NANOSEC / MICROSEC);
}
