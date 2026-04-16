/**
 * ============================================================================
 * Skynet 句柄管理模块 - skynet_handle.c
 * ============================================================================
 * 
 * 【文件作用】
 * 管理服务的句柄（Handle）分配、查询和回收，包括：
 * 1. 分配唯一的服务句柄（32位无符号整数）
 * 2. 句柄与服务上下文的映射（Hash 表）
 * 3. 本地名称与句柄的映射（有序数组，支持二分查找）
 * 4. 读写锁优化（分布式读锁槽）
 * 
 * 【句柄格式】
 * 32位句柄 = [节点ID(8位) | 本地句柄(24位)]
 * - 节点ID用于集群通信，标识服务所在的 Skynet 节点
 * - 本地句柄在当前节点内唯一
 * 
 * 【名称格式】
 * - .name  - 本地名称（当前节点内有效）
 * - :xxxxx - 十六进制句柄
 * ============================================================================
 */

#include "skynet.h"

#include "skynet_handle.h"
#include "skynet_imp.h"
#include "skynet_server.h"
#include "rwlock.h"
#include "spinlock.h"

#include <stdlib.h>
#include <assert.h>
#include <string.h>

// 缓存行大小（用于避免伪共享）
#define HANDLE_CACHE_LINE 64

/**
 * 【数据结构】读锁槽
 * 
 * 分布式读锁优化：每个线程有自己的读锁槽
 * 避免多个读线程竞争同一个锁，提高并发性能
 */
struct handle_reader_slot {
	ATOM_INT active;  // 是否正在读取
	char _pad[HANDLE_CACHE_LINE - sizeof(ATOM_INT)];  // 填充到缓存行大小
};

// 线程本地存储：当前线程的读锁槽索引
static _Thread_local int TLS_SLOT_IDX = -1;

// 初始槽大小
#define DEFAULT_SLOT_SIZE 4
// 最大槽大小
#define MAX_SLOT_SIZE 0x40000000

/**
 * 【数据结构】名称映射项
 */
struct handle_name {
	char * name;          // 名称字符串
	uint32_t handle;      // 对应的句柄
};

/**
 * 【数据结构】句柄存储
 * 
 * 包含句柄分配表和名称映射表
 */
struct handle_storage {
	struct rwlock lock;   // 读写锁

	uint32_t harbor;      // 节点ID（用于生成句柄的高8位）
	uint32_t handle_index; // 下一个要分配的句柄（自动递增）
	int slot_size;        // 槽数组大小
	struct skynet_context ** slot;  // 句柄到 context 的映射表（Hash表）

	int name_cap;         // 名称数组容量
	int name_count;       // 名称数组当前数量
	struct handle_name *name;  // 名称映射数组（有序，支持二分查找）

	// 分布式读锁槽
	ATOM_INT thread_idx;  // 线程索引分配计数
	int rslot_count;      // 读锁槽数量
	struct handle_reader_slot *rslots;  // 读锁槽数组
};

// 全局句柄存储实例
static struct handle_storage *H = NULL;

/**
 * 【内部】获取读锁（使用分布式读锁槽优化）
 */
static inline void
handle_rlock(struct handle_storage *s) {
	if (TLS_SLOT_IDX >= 0 && TLS_SLOT_IDX < s->rslot_count) {
		// 使用读锁槽
		for (;;) {
			ATOM_STORE(&s->rslots[TLS_SLOT_IDX].active, 1);
			if (!ATOM_LOAD(&s->lock.write)) {
				break;
			}
			// 有写者存在，退让避免死锁
			ATOM_STORE(&s->rslots[TLS_SLOT_IDX].active, 0);
			while (ATOM_LOAD(&s->lock.write)) { atomic_pause_(); }
		}
	} else {
		// 回退到普通读锁
		rwlock_rlock(&s->lock);
	}
}

/**
 * 【内部】释放读锁
 */
static inline void
handle_runlock(struct handle_storage *s) {
	if (TLS_SLOT_IDX >= 0 && TLS_SLOT_IDX < s->rslot_count) {
		ATOM_STORE(&s->rslots[TLS_SLOT_IDX].active, 0);
	} else {
		rwlock_runlock(&s->lock);
	}
}

/**
 * 【内部】获取写锁
 */
static inline void
handle_wlock(struct handle_storage *s) {
	rwlock_wlock(&s->lock);
	// 等待所有读锁槽释放
	for (int i = 0; i < s->rslot_count; i++) {
		while (ATOM_LOAD(&s->rslots[i].active)) { atomic_pause_(); }
	}
}

/**
 * 【内部】释放写锁
 */
static inline void
handle_wunlock(struct handle_storage *s) {
	rwlock_wunlock(&s->lock);
}

/**
 * 【接口】注册服务上下文，分配句柄
 * 
 * 为新创建的服务分配唯一句柄
 * 
 * @param ctx  服务上下文
 * @return     分配的句柄
 */
uint32_t
skynet_handle_register(struct skynet_context *ctx) {
	struct handle_storage *s = H;

	handle_wlock(s);

	for (;;) {
		int i;
		uint32_t handle = s->handle_index;
		// 查找空槽
		for (i=0;i<s->slot_size;i++,handle++) {
			if (handle > HANDLE_MASK) {
				// 0 保留，从 1 开始
				handle = 1;
			}
			int hash = handle & (s->slot_size-1);
			if (s->slot[hash] == NULL) {
				// 找到空槽，分配句柄
				s->slot[hash] = ctx;
				s->handle_index = handle + 1;

				handle_wunlock(s);

				// 与节点ID组合成完整句柄
				handle |= s->harbor;
				return handle;
			}
		}
		// 槽满了，扩容
		assert((s->slot_size*2 - 1) <= HANDLE_MASK);
		struct skynet_context ** new_slot = skynet_malloc(s->slot_size * 2 * sizeof(struct skynet_context *));
		memset(new_slot, 0, s->slot_size * 2 * sizeof(struct skynet_context *));
		// 重新哈希
		for (i=0;i<s->slot_size;i++) {
			if (s->slot[i]) {
				int hash = skynet_context_handle(s->slot[i]) & (s->slot_size * 2 - 1);
				assert(new_slot[hash] == NULL);
				new_slot[hash] = s->slot[i];
			}
		}
		skynet_free(s->slot);
		s->slot = new_slot;
		s->slot_size *= 2;
	}
}

/**
 * 【接口】注销句柄（服务退出时调用）
 * 
 * @param handle  要注销的句柄
 * @return        1 成功，0 失败（句柄不存在）
 */
int
skynet_handle_retire(uint32_t handle) {
	int ret = 0;
	struct handle_storage *s = H;

	handle_wlock(s);

	uint32_t hash = handle & (s->slot_size-1);
	struct skynet_context * ctx = s->slot[hash];

	if (ctx != NULL && skynet_context_handle(ctx) == handle) {
		// 从槽中移除
		s->slot[hash] = NULL;
		ret = 1;
		// 从名称数组中移除
		int i;
		int j=0, n=s->name_count;
		for (i=0; i<n; ++i) {
			if (s->name[i].handle == handle) {
				skynet_free(s->name[i].name);
				continue;
			} else if (i!=j) {
				s->name[j] = s->name[i];
			}
			++j;
		}
		s->name_count = j;
	} else {
		ctx = NULL;
	}

	handle_wunlock(s);

	if (ctx) {
		// 释放 context（可能调用 skynet_handle_*，所以先释放锁）
		skynet_context_release(ctx);
	}

	return ret;
}

/**
 * 【接口】注销所有句柄（退出时调用）
 */
void
skynet_handle_retireall() {
	struct handle_storage *s = H;
	for (;;) {
		int n=0;
		int i;
		for (i=0;i<s->slot_size;i++) {
			handle_rlock(s);
			struct skynet_context * ctx = s->slot[i];
			uint32_t handle = 0;
			if (ctx) {
				handle = skynet_context_handle(ctx);
				++n;
			}
			handle_runlock(s);
			if (handle != 0) {
				skynet_handle_retire(handle);
			}
		}
		if (n==0)
			return;
	}
}

/**
 * 【接口】通过句柄获取服务上下文（增加引用计数）
 * 
 * @param handle  服务句柄
 * @return        服务上下文（NULL 表示服务不存在）
 */
struct skynet_context *
skynet_handle_grab(uint32_t handle) {
	struct handle_storage *s = H;
	struct skynet_context * result = NULL;

	handle_rlock(s);

	uint32_t hash = handle & (s->slot_size-1);
	struct skynet_context * ctx = s->slot[hash];
	if (ctx && skynet_context_handle(ctx) == handle) {
		result = ctx;
		skynet_context_grab(result);  // 增加引用计数
	}

	handle_runlock(s);

	return result;
}

/**
 * 【接口】通过名称查找句柄（二分查找）
 * 
 * @param name  本地名称（不含 . 前缀）
 * @return      句柄（0 表示未找到）
 */
uint32_t
skynet_handle_findname(const char * name) {
	struct handle_storage *s = H;

	handle_rlock(s);

	uint32_t handle = 0;

	// 二分查找
	int begin = 0;
	int end = s->name_count - 1;
	while (begin<=end) {
		int mid = (begin+end)/2;
		struct handle_name *n = &s->name[mid];
		int c = strcmp(n->name, name);
		if (c==0) {
			handle = n->handle;
			break;
		}
		if (c<0) {
			begin = mid + 1;
		} else {
			end = mid - 1;
		}
	}

	handle_runlock(s);

	return handle;
}

/**
 * 【内部】在指定位置插入名称
 */
static void
_insert_name_before(struct handle_storage *s, char *name, uint32_t handle, int before) {
	// 容量检查
	if (s->name_count >= s->name_cap) {
		s->name_cap *= 2;
		assert(s->name_cap <= MAX_SLOT_SIZE);
		struct handle_name * n = skynet_malloc(s->name_cap * sizeof(struct handle_name));
		int i;
		for (i=0;i<before;i++) {
			n[i] = s->name[i];
		}
		for (i=before;i<s->name_count;i++) {
			n[i+1] = s->name[i];
		}
		skynet_free(s->name);
		s->name = n;
	} else {
		// 后移元素
		int i;
		for (i=s->name_count;i>before;i--) {
			s->name[i] = s->name[i-1];
		}
	}
	s->name[before].name = name;
	s->name[before].handle = handle;
	s->name_count ++;
}

/**
 * 【内部】插入名称（保持有序）
 * 
 * @return  成功返回名称字符串，失败返回 NULL（名称已存在）
 */
static const char *
_insert_name(struct handle_storage *s, const char * name, uint32_t handle) {
	int begin = 0;
	int end = s->name_count - 1;
	// 查找插入位置
	while (begin<=end) {
		int mid = (begin+end)/2;
		struct handle_name *n = &s->name[mid];
		int c = strcmp(n->name, name);
		if (c==0) {
			return NULL;  // 名称已存在
		}
		if (c<0) {
			begin = mid + 1;
		} else {
			end = mid - 1;
		}
	}
	char * result = skynet_strdup(name);

	_insert_name_before(s, result, handle, begin);

	return result;
}

/**
 * 【接口】为句柄注册名称
 * 
 * @param handle  句柄
 * @param name    本地名称（不含 . 前缀）
 * @return        成功返回名称字符串，失败返回 NULL
 */
const char *
skynet_handle_namehandle(uint32_t handle, const char *name) {
	handle_wlock(H);

	const char * ret = _insert_name(H, name, handle);

	handle_wunlock(H);

	return ret;
}

/**
 * 【接口】注册当前线程到读锁槽
 * 
 * 每个工作线程启动时调用，分配一个读锁槽
 */
void
skynet_handle_register_thread(void) {
	int idx = ATOM_FINC(&H->thread_idx);
	if (idx < H->rslot_count) {
		TLS_SLOT_IDX = idx;
	}
}

/**
 * 【接口】初始化句柄管理系统
 * 
 * @param harbor  节点ID
 * @param thread  工作线程数（用于确定读锁槽数量）
 */
void
skynet_handle_init(int harbor, int thread) {
	assert(H==NULL);
	struct handle_storage * s = skynet_malloc(sizeof(*H));
	s->slot_size = DEFAULT_SLOT_SIZE;
	s->slot = skynet_malloc(s->slot_size * sizeof(struct skynet_context *));
	memset(s->slot, 0, s->slot_size * sizeof(struct skynet_context *));

	rwlock_init(&s->lock);

	// 读锁槽数量 = 工作线程 + 监控线程 + 定时器线程 + socket 线程
	s->rslot_count = thread + 3;
	size_t rslot_sz = (size_t)s->rslot_count * sizeof(struct handle_reader_slot);
	s->rslots = (struct handle_reader_slot *)skynet_malloc(rslot_sz);
	memset(s->rslots, 0, rslot_sz);
	ATOM_INIT(&s->thread_idx, 0);

	// 节点ID放在高8位
	s->harbor = (uint32_t) (harbor & 0xff) << HANDLE_REMOTE_SHIFT;
	s->handle_index = 1;  // 从 1 开始分配
	s->name_cap = 2;
	s->name_count = 0;
	s->name = skynet_malloc(s->name_cap * sizeof(struct handle_name));

	H = s;

	// H 不需要释放，程序结束时自动清理
}
