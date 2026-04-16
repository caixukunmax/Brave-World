/**
 * ============================================================================
 * Skynet 消息队列模块 - skynet_mq.c
 * ============================================================================
 * 
 * 【文件作用】
 * 实现 Skynet 的消息队列系统，包括：
 * 1. 服务的私有消息队列（每个服务一个）
 * 2. 全局消息队列（存储有消息待处理的服务队列）
 * 3. 线程安全的队列操作（使用自旋锁）
 * 
 * 【设计思想】
 * - 每个服务有自己的私有消息队列
 * - 全局队列只存储"有消息待处理"的服务队列指针
 * - 工作线程从全局队列获取一个服务队列，处理其部分消息
 * - 使用自旋锁保证线程安全（临界区很短，自旋锁效率高）
 * 
 * 【关键数据结构】
 * - message_queue: 服务的私有消息队列（环形缓冲区）
 * - global_queue:  全局队列（链表结构）
 * 
 * 【过载检测】
 * 当消息队列堆积超过阈值（默认1024）时，会触发过载警告
 * ============================================================================
 */

#include "skynet.h"
#include "skynet_mq.h"
#include "skynet_handle.h"
#include "spinlock.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <assert.h>
#include <stdbool.h>

// 默认队列容量（可自动扩容）
#define DEFAULT_QUEUE_SIZE 64
// 最大全局队列大小
#define MAX_GLOBAL_MQ 0x10000

// in_global 状态：
// 0 - 队列不在全局队列中
// 1 - 队列在全局队列中，或正在被处理
#define MQ_IN_GLOBAL 1

// 过载阈值（消息堆积超过此值触发警告）
#define MQ_OVERLOAD 1024

/**
 * 【数据结构】服务的私有消息队列
 * 
 * 使用环形缓冲区实现，支持动态扩容
 */
struct message_queue {
	struct spinlock lock;           // 自旋锁（保护队列操作）
	uint32_t handle;                // 所属服务的句柄
	int cap;                        // 队列容量
	int head;                       // 头指针（读取位置）
	int tail;                       // 尾指针（写入位置）
	int release;                    // 释放标记（服务销毁时设置）
	int in_global;                  // 是否在全局队列中
	int overload;                   // 当前过载计数
	int overload_threshold;         // 过载阈值（动态调整）
	struct skynet_message *queue;   // 消息数组（环形缓冲区）
	struct message_queue *next;     // 全局队列链表指针
};

/**
 * 【数据结构】全局消息队列
 * 
 * 链表结构，存储有消息待处理的服务队列
 */
struct global_queue {
	struct message_queue *head;     // 链表头
	struct message_queue *tail;     // 链表尾
	struct spinlock lock;           // 自旋锁
};

// 全局队列实例
static struct global_queue *Q = NULL;

/**
 * 【接口】将服务队列推入全局队列
 * 
 * 当服务收到新消息时，需要将其队列推入全局队列
 * 工作线程会从全局队列获取队列并处理消息
 * 
 * @param queue  服务队列
 */
void 
skynet_globalmq_push(struct message_queue * queue) {
	struct global_queue *q= Q;

	SPIN_LOCK(q)
	assert(queue->next == NULL);
	if(q->tail) {
		// 链表非空，添加到尾部
		q->tail->next = queue;
		q->tail = queue;
	} else {
		// 链表为空
		q->head = q->tail = queue;
	}
	SPIN_UNLOCK(q)
}

/**
 * 【接口】从全局队列弹出一个服务队列
 * 
 * @return 服务队列指针（NULL 表示全局队列为空）
 */
struct message_queue * 
skynet_globalmq_pop() {
	struct global_queue *q = Q;

	SPIN_LOCK(q)
	struct message_queue *mq = q->head;
	if(mq) {
		q->head = mq->next;
		if(q->head == NULL) {
			assert(mq == q->tail);
			q->tail = NULL;
		}
		mq->next = NULL;
	}
	SPIN_UNLOCK(q)

	return mq;
}

/**
 * 【接口】创建服务消息队列
 * 
 * @param handle  服务句柄
 * @return        消息队列指针
 */
struct message_queue * 
skynet_mq_create(uint32_t handle) {
	struct message_queue *q = skynet_malloc(sizeof(*q));
	q->handle = handle;
	q->cap = DEFAULT_QUEUE_SIZE;
	q->head = 0;
	q->tail = 0;
	SPIN_INIT(q)
	
	// 创建队列时（在服务创建和初始化之间），设置 in_global 标志
	// 避免在初始化完成前被推入全局队列
	// 如果服务初始化成功，skynet_context_new 会调用 skynet_mq_push 将其推入全局队列
	q->in_global = MQ_IN_GLOBAL;
	q->release = 0;
	q->overload = 0;
	q->overload_threshold = MQ_OVERLOAD;
	q->queue = skynet_malloc(sizeof(struct skynet_message) * q->cap);
	q->next = NULL;

	return q;
}

/**
 * 【内部】释放消息队列
 */
static void 
_release(struct message_queue *q) {
	assert(q->next == NULL);
	SPIN_DESTROY(q)
	skynet_free(q->queue);
	skynet_free(q);
}

/**
 * 【接口】获取队列所属的服务句柄
 */
uint32_t 
skynet_mq_handle(struct message_queue *q) {
	return q->handle;
}

/**
 * 【接口】获取队列中的消息数量
 */
int
skynet_mq_length(struct message_queue *q) {
	int head, tail,cap;

	SPIN_LOCK(q)
	head = q->head;
	tail = q->tail;
	cap = q->cap;
	SPIN_UNLOCK(q)
	
	// 计算环形缓冲区中的元素数量
	if (head <= tail) {
		return tail - head;
	}
	return tail + cap - head;
}

/**
 * 【接口】检查并获取过载状态
 * 
 * @return 过载时的消息数量（0 表示未过载）
 */
int
skynet_mq_overload(struct message_queue *q) {
	if (q->overload) {
		int overload = q->overload;
		q->overload = 0;
		return overload;
	} 
	return 0;
}

/**
 * 【接口】从队列弹出一条消息
 * 
 * @param q        消息队列
 * @param message  输出参数，存储弹出的消息
 * @return         0 成功，1 队列为空
 */
int
skynet_mq_pop(struct message_queue *q, struct skynet_message *message) {
	int ret = 1;
	SPIN_LOCK(q)

	if (q->head != q->tail) {
		// 队列非空，弹出消息
		*message = q->queue[q->head++];
		ret = 0;
		int head = q->head;
		int tail = q->tail;
		int cap = q->cap;

		// 环形缓冲区：head 到达容量限制时回绕
		if (head >= cap) {
			q->head = head = 0;
		}
		
		// 计算当前队列长度
		int length = tail - head;
		if (length < 0) {
			length += cap;
		}
		
		// 过载检测：如果长度超过阈值，记录过载状态
		while (length > q->overload_threshold) {
			q->overload = length;
			q->overload_threshold *= 2;  // 动态提高阈值，避免频繁警告
		}
	} else {
		// 队列为空，重置过载阈值
		q->overload_threshold = MQ_OVERLOAD;
	}

	if (ret) {
		// 队列为空，标记不在全局队列中
		q->in_global = 0;
	}
	
	SPIN_UNLOCK(q)

	return ret;
}

/**
 * 【内部】扩容队列
 * 
 * 当队列满时，容量翻倍
 */
static void
expand_queue(struct message_queue *q) {
	struct skynet_message *new_queue = skynet_malloc(sizeof(struct skynet_message) * q->cap * 2);
	int i;
	// 将旧队列中的消息复制到新队列（重新排列，消除环形）
	for (i=0;i<q->cap;i++) {
		new_queue[i] = q->queue[(q->head + i) % q->cap];
	}
	q->head = 0;
	q->tail = q->cap;
	q->cap *= 2;
	
	skynet_free(q->queue);
	q->queue = new_queue;
}

/**
 * 【接口】向队列推送消息
 * 
 * 如果队列当前不在全局队列中，会自动将其推入全局队列
 * 
 * @param q        消息队列
 * @param message  消息
 */
void 
skynet_mq_push(struct message_queue *q, struct skynet_message *message) {
	assert(message);
	SPIN_LOCK(q)

	// 写入消息到队列尾部
	q->queue[q->tail] = *message;
	if (++ q->tail >= q->cap) {
		q->tail = 0;  // 环形回绕
	}

	// 队列满，扩容
	if (q->head == q->tail) {
		expand_queue(q);
	}

	// 如果队列不在全局队列中，推入全局队列
	if (q->in_global == 0) {
		q->in_global = MQ_IN_GLOBAL;
		skynet_globalmq_push(q);
	}
	
	SPIN_UNLOCK(q)
}

/**
 * 【接口】初始化消息队列系统
 */
void 
skynet_mq_init() {
	struct global_queue *q = skynet_malloc(sizeof(*q));
	memset(q,0,sizeof(*q));
	SPIN_INIT(q);
	Q=q;
}

/**
 * 【接口】标记队列准备释放
 * 
 * 当服务被销毁时调用，确保队列中的消息被处理后再释放
 */
void 
skynet_mq_mark_release(struct message_queue *q) {
	SPIN_LOCK(q)
	assert(q->release == 0);
	q->release = 1;
	if (q->in_global != MQ_IN_GLOBAL) {
		skynet_globalmq_push(q);
	}
	SPIN_UNLOCK(q)
}

/**
 * 【内部】释放队列中的所有消息
 */
static void
_drop_queue(struct message_queue *q, message_drop drop_func, void *ud) {
	struct skynet_message msg;
	while(!skynet_mq_pop(q, &msg)) {
		drop_func(&msg, ud);
	}
	_release(q);
}

/**
 * 【接口】释放消息队列
 * 
 * 如果队列已标记释放，则直接释放
 * 否则将其推回全局队列等待处理
 */
void 
skynet_mq_release(struct message_queue *q, message_drop drop_func, void *ud) {
	SPIN_LOCK(q)
	
	if (q->release) {
		SPIN_UNLOCK(q)
		_drop_queue(q, drop_func, ud);
	} else {
		skynet_globalmq_push(q);
		SPIN_UNLOCK(q)
	}
}
