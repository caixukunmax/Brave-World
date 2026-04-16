/**
 * @file socket_server.c
 * @brief Skynet Socket Server - 核心网络模块
 * 
 * 本模块实现了 Skynet 框架的底层网络通信功能，主要特性包括：
 * - 支持 TCP 和 UDP (IPv4/IPv6) 协议
 * - 基于多路复用(epoll/kqueue/select)的高性能异步 IO
 * - 双优先级写缓冲队列(高优先级/低优先级)
 * - 线程安全的 socket 管理
 * - 支持 EMFILE 优雅处理
 * - 支持直接写优化(绕过消息队列)
 */

#include "skynet.h"

#include "socket_server.h"
#include "socket_poll.h"
#include "atomic.h"
#include "spinlock.h"

#include <sys/types.h>
#include <sys/socket.h>
#include <netinet/tcp.h>
#include <unistd.h>
#include <errno.h>
#include <stdlib.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdint.h>
#include <assert.h>
#include <string.h>

/** ===================== 常量定义 ===================== */

#define MAX_INFO 128                    // 地址信息缓冲区最大长度
// MAX_SOCKET will be 2^MAX_SOCKET_P
#define MAX_SOCKET_P 16                 // socket ID 位数(2^16 = 65536)
#define MAX_EVENT 64                    // 每次 epoll_wait 最大事件数
#define MIN_READ_BUFFER 64              // 最小读缓冲区大小

/* Socket 类型定义 */
#define SOCKET_TYPE_INVALID 0           // 无效 socket
#define SOCKET_TYPE_RESERVE 1           // 预留中(正在分配)
#define SOCKET_TYPE_PLISTEN 2           // 预监听状态
#define SOCKET_TYPE_LISTEN 3            // 监听中
#define SOCKET_TYPE_CONNECTING 4        // 连接中
#define SOCKET_TYPE_CONNECTED 5         // 已连接
#define SOCKET_TYPE_HALFCLOSE_READ 6    // 半关闭(读端关闭)
#define SOCKET_TYPE_HALFCLOSE_WRITE 7   // 半关闭(写端关闭)
#define SOCKET_TYPE_PACCEPT 8           // 预接受状态(accept 后未 start)
#define SOCKET_TYPE_BIND 9              // 绑定状态(bind 后的 fd)

#define MAX_SOCKET (1<<MAX_SOCKET_P)    // 最大 socket 数量(65536)

/* 优先级定义 */
#define PRIORITY_HIGH 0                 // 高优先级队列
#define PRIORITY_LOW 1                  // 低优先级队列

/* Hash 计算宏 - 用于快速定位 socket 槽位 */
#define HASH_ID(id) (((unsigned)id) % MAX_SOCKET)

/* 提取 ID 的高 16 位作为 tag，用于验证 socket 有效性 */
#define ID_TAG16(id) ((id>>MAX_SOCKET_P) & 0xffff)

/* 协议类型定义 */
#define PROTOCOL_TCP 0                  // TCP 协议
#define PROTOCOL_UDP 1                  /** ===================== UDP 相关 API ===================== */ IPv4
#define PROTOCOL_UDPv6 2                // UDP IPv6
#define PROTOCOL_UNKNOWN 255            // 未知协议

#define UDP_ADDRESS_SIZE 19             // UDP 地址结构大小: 1字节类型 + 2字节端口 + 16字节IP

#define MAX_UDP_PACKAGE 65535           // UDP 最大包大小

/* EAGAIN 和 EWOULDBLOCK 处理 - 某些系统上它们值不同 */
#if (EAGAIN != EWOULDBLOCK)
#define AGAIN_WOULDBLOCK EAGAIN : case EWOULDBLOCK
#else
#define AGAIN_WOULDBLOCK EAGAIN
#endif

/* 写缓冲区警告阈值(1MB)，超过则触发警告 */
#define WARNING_SIZE (1024*1024)

/* 用户对象特殊标记 - 用于区分自定义 buffer 和普通内存 */
#define USEROBJECT ((size_t)(-1))

/** ===================== 数据结构定义 ===================== */

/* 写缓冲区节点 - 用于发送队列 */
struct write_buffer {
	struct write_buffer * next;      // 下一个节点
	const void *buffer;              // 原始缓冲区指针
	char *ptr;                       // 当前发送位置(可能已部分发送)
	size_t sz;                       // 剩余发送大小
	bool userobject;                 // 是否为用户自定义对象(使用自定义释放函数)
};

/* UDP 写缓冲区节点 - 扩展 write_buffer，增加地址信息 */
struct write_buffer_udp {
	struct write_buffer buffer;              // 基础写缓冲区
	uint8_t udp_address[UDP_ADDRESS_SIZE];   // UDP 目标地址
};

/* 写缓冲区链表(高优先级或低优先级队列) */
struct wb_list {
	struct write_buffer * head;      // 队列头
	struct write_buffer * tail;      // 队列尾
};

/* Socket 统计信息 */
struct socket_stat {
	uint64_t rtime;                  // 最后读取时间
	uint64_t wtime;                  // 最后写入时间
	uint64_t read;                   // 读取总字节数
	uint64_t write;                  // 写入总字节数
};

/**
 * Socket 结构体 - 表示一个网络连接
 * 
 * 这是 socket 服务器的核心数据结构，每个连接对应一个 socket 结构体
 * 位于 slot[HASH_ID(id)] 槽位中。
 * 
 * 注意：sending 字段是一个 32 位原子整数，高 16 位存储 ID_TAG，
 * 低 16 位存储正在发送的请求计数，用于实现线程安全的引用计数。
 */
struct socket {
	uintptr_t opaque;                // 关联的服务句柄(用于消息路由)
	struct wb_list high;             // 高优先级写队列
	struct wb_list low;              // 低优先级写队列
	int64_t wb_size;                 // 写缓冲区总大小(用于流量控制)
	struct socket_stat stat;         // 统计信息
	ATOM_ULONG sending;              // 发送引用计数(高16位=id tag, 低16位=计数)
	int fd;                          // 系统文件描述符
	int id;                          // socket ID(由 alloc_id 生成)
	ATOM_INT type;                   // socket 类型(原子操作)
	uint8_t protocol;                // 协议类型(PROTOCOL_TCP/UDP/UDPv6)
	bool reading;                    // 是否在读监听状态
	bool writing;                    // 是否在写监听状态
	bool closing;                    // 是否正在关闭
	ATOM_INT udpconnecting;          // UDP 连接中计数(原子)
	int64_t warn_size;               // 警告阈值大小
	union {
		int size;                    // TCP: 读缓冲区大小
		uint8_t udp_address[UDP_ADDRESS_SIZE];  // UDP: 默认目标地址
	} p;
	struct spinlock dw_lock;         // 直接写锁(用于直接写优化)
	int dw_offset;                   // 直接写偏移(已发送字节数)
	const void * dw_buffer;          // 直接写缓冲区
	size_t dw_size;                  // 直接写缓冲区大小
};

/**
 * Socket 服务器主结构体
 * 
 * 整个 socket 模块只有一个全局实例，包含所有 socket 的槽位、
 * epoll fd、控制管道等资源。
 */
struct socket_server {
	volatile uint64_t time;          // 当前时间戳(由外部更新)
	int reserve_fd;                  // 保留 fd(用于 EMFILE 优雅处理)
	int recvctrl_fd;                 // 控制管道读端(接收命令)
	int sendctrl_fd;                 // 控制管道写端(发送命令)
	int checkctrl;                   // 是否需要检查控制命令的标志
	poll_fd event_fd;                // epoll/kqueue fd
	ATOM_INT alloc_id;               // 分配 ID 计数器(原子)
	int event_n;                     // 当前事件数组中的事件数量
	int event_index;                 // 当前处理的事件索引
	struct socket_object_interface soi;  // 用户对象接口(自定义 buffer 管理)
	struct event ev[MAX_EVENT];      // 事件数组
	struct socket slot[MAX_SOCKET];  // socket 槽位数组
	char buffer[MAX_INFO];           // 通用缓冲区(用于地址转换等)
	uint8_t udpbuffer[MAX_UDP_PACKAGE];  // UDP 接收缓冲区
	fd_set rfds;                     // select 用的 fd 集合(检查控制命令)
};

/* ===================== 请求包结构定义 ===================== */

/* 打开连接请求 */
struct request_open {
	int id;                          // 预分配的 socket ID
	int port;                        // 目标端口
	uintptr_t opaque;                // 关联服务句柄
	char host[1];                    // 主机地址(变长数组)
};

/* 发送数据请求 */
struct request_send {
	int id;                          // socket ID
	size_t sz;                       // 数据大小
	const void * buffer;             // 数据缓冲区指针
};

/* UDP 发送数据请求 */
struct request_send_udp {
	struct request_send send;                // 基础发送请求
	uint8_t address[UDP_ADDRESS_SIZE];       // 目标 UDP 地址
};

/* 设置 UDP 默认地址请求 */
struct request_setudp {
	int id;                                  // socket ID
	uint8_t address[UDP_ADDRESS_SIZE];       // UDP 地址
};

/* 关闭 socket 请求 */
struct request_close {
	int id;                                  // socket ID
	int shutdown;                            // 是否立即关闭(shutdown=1)
	uintptr_t opaque;                        // 关联服务句柄
};

/* 监听 socket 请求 */
struct request_listen {
	int id;                                  // socket ID
	int fd;                                  // 已创建的监听 fd
	uintptr_t opaque;                        // 关联服务句柄
};

/* 绑定已有 fd 请求 */
struct request_bind {
	int id;                                  // socket ID
	int fd;                                  // 外部创建的 fd
	uintptr_t opaque;                        // 关联服务句柄
};

/* 恢复/暂停 socket 请求 */
struct request_resumepause {
	int id;                                  // socket ID
	uintptr_t opaque;                        // 关联服务句柄
};

/* 设置 socket 选项请求 */
struct request_setopt {
	int id;                                  // socket ID
	int what;                                // 选项类型(如 TCP_NODELAY)
	int value;                               // 选项值
};

/* 创建 UDP socket 请求 */
struct request_udp {
	int id;                                  // socket ID
	int fd;                                  // 已创建的 UDP fd
	int family;                              // 地址族(AF_INET/AF_INET6)
	uintptr_t opaque;                        // 关联服务句柄
};

/* UDP dial(连接)请求 */
struct request_dial_udp {
	int id;                                  // socket ID
	int fd;                                  // 已创建的 UDP fd
	uintptr_t opaque;                        // 关联服务句柄
	uint8_t address[UDP_ADDRESS_SIZE];       // 目标地址
};

/**
 * 控制命令类型说明(通过管道发送的第一个字节)
 * 
 * R - Resume socket:  恢复 socket 接收数据
 * S - Pause socket:   暂停 socket 接收数据
 * B - Bind socket:    绑定已有文件描述符
 * L - Listen socket:  开始监听连接
 * K - Close socket:   关闭 socket
 * O - Open/Connect:   连接远程服务器
 * X - Exit:           退出 socket 线程
 * W - Enable write:   启用写事件监听
 * D - Send (high):    发送高优先级数据
 * P - Send (low):     发送低优先级数据
 * A - Send UDP:       发送 UDP 数据包
 * C - Set UDP addr:   设置 UDP 默认地址
 * N - Dial UDP:       UDP 连接到指定地址
 * T - Set option:     设置 socket 选项
 * U - Create UDP:     创建 UDP socket
 */

/* 请求包结构体 - 用于通过管道传递命令 */
struct request_package {
	uint8_t header[8];               // 头部(第6、7字节用于存储类型和长度)
	union {
		char buffer[256];            // 通用缓冲区
		struct request_open open;            // 打开请求
		struct request_send send;            // 发送请求
		struct request_send_udp send_udp;    // UDP 发送请求
		struct request_close close;          // 关闭请求
		struct request_listen listen;        // 监听请求
		struct request_bind bind;            // 绑定请求
		struct request_resumepause resumepause;  // 恢复/暂停请求
		struct request_setopt setopt;        // 设置选项请求
		struct request_udp udp;              // UDP 创建请求
		struct request_setudp set_udp;       // 设置 UDP 地址请求
		struct request_dial_udp dial_udp;    // UDP dial 请求
	} u;
	uint8_t dummy[256];              // 填充(确保结构体足够大)
};

/* 通用 socket 地址联合体 - 支持 IPv4 和 IPv6 */
union sockaddr_all {
	struct sockaddr s;               // 通用地址
	struct sockaddr_in v4;           // IPv4 地址
	struct sockaddr_in6 v6;          // IPv6 地址
};

/* 发送对象 - 封装要发送的数据及其释放函数 */
struct send_object {
	const void * buffer;             // 数据缓冲区
	size_t sz;                       // 数据大小
	void (*free_func)(void *);       // 释放函数
};

/* 内存分配宏 - 使用 Skynet 的内存管理 */
#define MALLOC skynet_malloc
#define FREE skynet_free

/** ===================== Socket 锁机制 ===================== */

/**
 * Socket 锁结构体 - 支持递归锁
 * 
 * 用于保护直接写(dw_*)操作，确保在多线程环境下
 * 直接写和 socket 线程的写不会冲突。
 */
struct socket_lock {
	struct spinlock *lock;           // 自旋锁
	int count;                       // 递归计数
};

/* 初始化 socket 锁 */
static inline void
socket_lock_init(struct socket *s, struct socket_lock *sl) {
	sl->lock = &s->dw_lock;          // 绑定到 socket 的 dw_lock
	sl->count = 0;                   // 初始计数为0
}

/**
 * 获取 socket 锁(支持递归)
 * 
 * 如果当前线程已持有锁，则只增加计数
 * 否则获取锁并将计数设为1
 */
static inline void
socket_lock(struct socket_lock *sl) {
	if (sl->count == 0) {
		spinlock_lock(sl->lock);
	}
	++sl->count;
}

/**
 * 尝试获取 socket 锁(非阻塞)
 * 
 * @return 1 - 成功获取锁
 * @return 0 - 锁已被占用
 */
static inline int
socket_trylock(struct socket_lock *sl) {
	if (sl->count == 0) {
		if (!spinlock_trylock(sl->lock))
			return 0;	// lock failed
	}
	++sl->count;
	return 1;
}

/**
 * 释放 socket 锁
 * 
 * 递减计数，当计数为0时真正释放锁
 */
static inline void
socket_unlock(struct socket_lock *sl) {
	--sl->count;
	if (sl->count <= 0) {
		assert(sl->count == 0);
		spinlock_unlock(sl->lock);
	}
}

/**
 * 检查 socket 是否无效
 * 
 * @param s  socket 指针
 * @param id 期望的 socket ID
 * @return 非0 - socket 无效(ID不匹配或类型为INVALID)
 * @return 0   - socket 有效
 */
static inline int
socket_invalid(struct socket *s, int id) {
	return (s->id != id || ATOM_LOAD(&s->type) == SOCKET_TYPE_INVALID);
}

static inline bool
send_object_init(struct socket_server *ss, struct send_object *so, const void *object, size_t sz) {
	if (sz == USEROBJECT) {
		so->buffer = ss->soi.buffer(object);
		so->sz = ss->soi.size(object);
		so->free_func = ss->soi.free;
		return true;
	} else {
		so->buffer = object;
		so->sz = sz;
		so->free_func = FREE;
		return false;
	}
}

static void
dummy_free(void *ptr) {
	(void)ptr;
}

static inline void
send_object_init_from_sendbuffer(struct socket_server *ss, struct send_object *so, struct socket_sendbuffer *buf) {
	switch (buf->type) {
	case SOCKET_BUFFER_MEMORY:
		send_object_init(ss, so, buf->buffer, buf->sz);
		break;
	case SOCKET_BUFFER_OBJECT:
		send_object_init(ss, so, buf->buffer, USEROBJECT);
		break;
	case SOCKET_BUFFER_RAWPOINTER:
		so->buffer = buf->buffer;
		so->sz = buf->sz;
		so->free_func = dummy_free;
		break;
	default:
		// never get here
		so->buffer = NULL;
		so->sz = 0;
		so->free_func = NULL;
		break;
	}
}

static inline void
write_buffer_free(struct socket_server *ss, struct write_buffer *wb) {
	if (wb->userobject) {
		ss->soi.free((void *)wb->buffer);
	} else {
		FREE((void *)wb->buffer);
	}
	FREE(wb);
}

static void
socket_keepalive(int fd) {
	int keepalive = 1;
	setsockopt(fd, SOL_SOCKET, SO_KEEPALIVE, (void *)&keepalive , sizeof(keepalive));
}

static int
reserve_id(struct socket_server *ss) {
	int i;
	for (i=0;i<MAX_SOCKET;i++) {
		int id = ATOM_FINC(&(ss->alloc_id))+1;
		if (id < 0) {
			id = ATOM_FAND(&(ss->alloc_id), 0x7fffffff) & 0x7fffffff;
		}
		struct socket *s = &ss->slot[HASH_ID(id)];
		int type_invalid = ATOM_LOAD(&s->type);
		if (type_invalid == SOCKET_TYPE_INVALID) {
			if (ATOM_CAS(&s->type, type_invalid, SOCKET_TYPE_RESERVE)) {
				s->id = id;
				s->protocol = PROTOCOL_UNKNOWN;
				// socket_server_udp_connect may inc s->udpconncting directly (from other thread, before new_fd),
				// so reset it to 0 here rather than in new_fd.
				ATOM_INIT(&s->udpconnecting, 0);
				s->fd = -1;
				return id;
			} else {
				// retry
				--i;
			}
		}
	}
	return -1;
}

static inline void
clear_wb_list(struct wb_list *list) {
	list->head = NULL;
	list->tail = NULL;
}

/**
 * 创建 socket 服务器实例
 * 
 * 初始化流程：
 * 1. 创建 epoll/kqueue 实例
 * 2. 创建控制管道(用于线程间通信)
 * 3. 将管道读端加入 epoll
 * 4. 分配保留 fd(用于 EMFILE 处理)
 * 5. 初始化所有 socket 槽位
 * 
 * @param time 初始时间戳
 * @return socket_server 指针，失败返回 NULL
 */
struct socket_server *
socket_server_create(uint64_t time) {
	int i;
	int fd[2];
	poll_fd efd = sp_create();
	if (sp_invalid(efd)) {
		skynet_error(NULL, "socket-server error: create event pool failed.");
		return NULL;
	}
	// 创建控制管道(用于主线程向 socket 线程发送命令)
	if (pipe(fd)) {
		sp_release(efd);
		skynet_error(NULL, "socket-server error: create socket pair failed.");
		return NULL;
	}
	// 将管道读端加入 epoll，用于监听控制命令
	if (sp_add(efd, fd[0], NULL)) {
		skynet_error(NULL, "socket-server error: can't add server fd to event pool.");
		close(fd[0]);
		close(fd[1]);
		sp_release(efd);
		return NULL;
	}

	struct socket_server *ss = MALLOC(sizeof(*ss));
	ss->time = time;
	ss->event_fd = efd;
	ss->recvctrl_fd = fd[0];
	ss->sendctrl_fd = fd[1];
	ss->checkctrl = 1;
	// 复制 stdout 作为保留 fd，用于 EMFILE 情况的优雅处理
	ss->reserve_fd = dup(1);

	// 初始化所有 socket 槽位
	for (i=0;i<MAX_SOCKET;i++) {
		struct socket *s = &ss->slot[i];
		ATOM_INIT(&s->type, SOCKET_TYPE_INVALID);
		clear_wb_list(&s->high);
		clear_wb_list(&s->low);
		spinlock_init(&s->dw_lock);
	}
	ATOM_INIT(&ss->alloc_id , 0);
	ss->event_n = 0;
	ss->event_index = 0;
	memset(&ss->soi, 0, sizeof(ss->soi));
	FD_ZERO(&ss->rfds);
	assert(ss->recvctrl_fd < FD_SETSIZE);

	return ss;
}

/**
 * 更新 socket 服务器时间戳
 * 
 * 用于统计信息的时序记录
 */
void
socket_server_updatetime(struct socket_server *ss, uint64_t time) {
	ss->time = time;
}

/**
 * 释放整个写缓冲区链表
 * 
 * 遍历链表，释放每个节点及其数据
 */
static void
free_wb_list(struct socket_server *ss, struct wb_list *list) {
	struct write_buffer *wb = list->head;
	while (wb) {
		struct write_buffer *tmp = wb;
		wb = wb->next;
		write_buffer_free(ss, tmp);
	}
	list->head = NULL;
	list->tail = NULL;
}

/**
 * 根据类型释放 buffer
 * 
 * MEMORY: 使用 FREE 释放
 * OBJECT: 使用用户自定义释放函数
 * RAWPOINTER: 不释放
 */
static void
free_buffer(struct socket_server *ss, struct socket_sendbuffer *buf) {
	void *buffer = (void *)buf->buffer;
	switch (buf->type) {
	case SOCKET_BUFFER_MEMORY:
		FREE(buffer);
		break;
	case SOCKET_BUFFER_OBJECT:
		ss->soi.free(buffer);
		break;
	case SOCKET_BUFFER_RAWPOINTER:
		break;
	}
}

/**
 * 克隆 buffer 用于发送到 socket 线程
 * 
 * 对于 RAWPOINTER 类型需要复制数据(因为原始指针可能立即被释放)
 * 其他类型直接返回原指针
 */
static const void *
clone_buffer(struct socket_sendbuffer *buf, size_t *sz) {
	switch (buf->type) {
	case SOCKET_BUFFER_MEMORY:
		*sz = buf->sz;
		return buf->buffer;
	case SOCKET_BUFFER_OBJECT:
		*sz = USEROBJECT;
		return buf->buffer;
	case SOCKET_BUFFER_RAWPOINTER:
		// 原始指针需要复制，因为调用者可能立即释放
		*sz = buf->sz;
		void * tmp = MALLOC(*sz);
		memcpy(tmp, buf->buffer, *sz);
		return tmp;
	}
	// never get here
	*sz = 0;
	return NULL;
}

/**
 * 强制关闭 socket
 * 
 * 释放所有资源，包括：
 * - 清空高/低优先级写队列
 * - 从 epoll 中删除 fd
 * - 关闭 fd(如果不是 BIND 类型)
 * - 释放直接写缓冲区
 * 
 * @param ss     socket 服务器
 * @param s      要关闭的 socket
 * @param l      socket 锁(用于保护 dw_buffer 操作)
 * @param result 返回消息结构
 */
static void
force_close(struct socket_server *ss, struct socket *s, struct socket_lock *l, struct socket_message *result) {
	result->id = s->id;
	result->ud = 0;
	result->data = NULL;
	result->opaque = s->opaque;
	uint8_t type = ATOM_LOAD(&s->type);
	if (type == SOCKET_TYPE_INVALID) {
		return;
	}
	assert(type != SOCKET_TYPE_RESERVE);
	free_wb_list(ss,&s->high);
	free_wb_list(ss,&s->low);
	sp_del(ss->event_fd, s->fd);
	socket_lock(l);
	if (type != SOCKET_TYPE_BIND) {
		if (close(s->fd) < 0) {
			perror("close socket:");
		}
	}
	ATOM_STORE(&s->type, SOCKET_TYPE_INVALID);
	if (s->dw_buffer) {
		struct socket_sendbuffer tmp;
		tmp.buffer = s->dw_buffer;
		tmp.sz = s->dw_size;
		tmp.id = s->id;
		tmp.type = (tmp.sz == USEROBJECT) ? SOCKET_BUFFER_OBJECT : SOCKET_BUFFER_MEMORY;
		free_buffer(ss, &tmp);
		s->dw_buffer = NULL;
	}
	socket_unlock(l);
}

/**
 * 释放整个 socket 服务器
 * 
 * 关闭所有 socket，释放所有资源
 */
void
socket_server_release(struct socket_server *ss) {
	int i;
	struct socket_message dummy;
	for (i=0;i<MAX_SOCKET;i++) {
		struct socket *s = &ss->slot[i];
		struct socket_lock l;
		socket_lock_init(s, &l);
		if (ATOM_LOAD(&s->type) != SOCKET_TYPE_RESERVE) {
			force_close(ss, s, &l, &dummy);
		}
		spinlock_destroy(&s->dw_lock);
	}
	close(ss->sendctrl_fd);
	close(ss->recvctrl_fd);
	sp_release(ss->event_fd);
	if (ss->reserve_fd >= 0)
		close(ss->reserve_fd);
	FREE(ss);
}

static inline void
check_wb_list(struct wb_list *s) {
	assert(s->head == NULL);
	assert(s->tail == NULL);
}

/**
 * 启用/禁用写事件监听
 * 
 * @param enable true - 启用写监听，false - 禁用写监听
 * @return 0 - 成功，非0 - 失败
 */
static inline int
enable_write(struct socket_server *ss, struct socket *s, bool enable) {
	if (s->writing != enable) {
		s->writing = enable;
		return sp_enable(ss->event_fd, s->fd, s, s->reading, enable);
	}
	return 0;
}

/**
 * 启用/禁用读事件监听
 * 
 * @param enable true - 启用读监听，false - 禁用读监听
 * @return 0 - 成功，非0 - 失败
 */
static inline int
enable_read(struct socket_server *ss, struct socket *s, bool enable) {
	if (s->reading != enable) {
		s->reading = enable;
		return sp_enable(ss->event_fd, s->fd, s, enable, s->writing);
	}
	return 0;
}

/**
 * 初始化新的 socket 结构体
 * 
 * 将 fd 关联到 socket ID，添加到 epoll，初始化所有字段
 * 
 * @param ss       socket 服务器
 * @param id       socket ID
 * @param fd       系统文件描述符
 * @param protocol 协议类型
 * @param opaque   关联服务句柄
 * @param reading  是否立即启用读监听
 * @return socket 指针，失败返回 NULL
 */
static struct socket *
new_fd(struct socket_server *ss, int id, int fd, int protocol, uintptr_t opaque, bool reading) {
	struct socket * s = &ss->slot[HASH_ID(id)];
	assert(ATOM_LOAD(&s->type) == SOCKET_TYPE_RESERVE);

	if (sp_add(ss->event_fd, fd, s)) {
		ATOM_STORE(&s->type, SOCKET_TYPE_INVALID);
		return NULL;
	}

	s->id = id;
	s->fd = fd;
	s->reading = true;
	s->writing = false;
	s->closing = false;
	ATOM_INIT(&s->sending , ID_TAG16(id) << 16 | 0);
	s->protocol = protocol;
	s->p.size = MIN_READ_BUFFER;
	s->opaque = opaque;
	s->wb_size = 0;
	s->warn_size = 0;
	check_wb_list(&s->high);
	check_wb_list(&s->low);
	s->dw_buffer = NULL;
	s->dw_size = 0;
	memset(&s->stat, 0, sizeof(s->stat));
	if (enable_read(ss, s, reading)) {
		ATOM_STORE(&s->type , SOCKET_TYPE_INVALID);
		return NULL;
	}
	return s;
}

static inline void
stat_read(struct socket_server *ss, struct socket *s, int n) {
	s->stat.read += n;
	s->stat.rtime = ss->time;
}

static inline void
stat_write(struct socket_server *ss, struct socket *s, int n) {
	s->stat.write += n;
	s->stat.wtime = ss->time;
}

/**
 * 打开 TCP 连接到远程服务器
 * 
 * 流程：
 * 1. 解析目标地址(getaddrinfo)
 * 2. 创建 socket 并设置非阻塞
 * 3. 尝试连接(非阻塞 connect 立即返回 EINPROGRESS)
 * 4. 添加到 epoll 监听写事件
 * 
 * @return -1     - 正在连接中(需要等待 epoll 通知)
 * @return SOCKET_OPEN - 立即连接成功
 * @return SOCKET_ERR  - 连接失败
 */
static int
open_socket(struct socket_server *ss, struct request_open * request, struct socket_message *result) {
	int id = request->id;
	result->opaque = request->opaque;
	result->id = id;
	result->ud = 0;
	result->data = NULL;
	struct socket *ns;
	int status;
	struct addrinfo ai_hints;
	struct addrinfo *ai_list = NULL;
	struct addrinfo *ai_ptr = NULL;
	char port[16];
	sprintf(port, "%d", request->port);
	memset(&ai_hints, 0, sizeof( ai_hints ) );
	ai_hints.ai_family = AF_UNSPEC;
	ai_hints.ai_socktype = SOCK_STREAM;
	ai_hints.ai_protocol = IPPROTO_TCP;

	status = getaddrinfo( request->host, port, &ai_hints, &ai_list );
	if ( status != 0 ) {
		result->data = (void *)gai_strerror(status);
		goto _failed_getaddrinfo;
	}
	int sock= -1;
	for (ai_ptr = ai_list; ai_ptr != NULL; ai_ptr = ai_ptr->ai_next ) {
		sock = socket( ai_ptr->ai_family, ai_ptr->ai_socktype, ai_ptr->ai_protocol );
		if ( sock < 0 ) {
			continue;
		}
		socket_keepalive(sock);
		sp_nonblocking(sock);
		status = connect( sock, ai_ptr->ai_addr, ai_ptr->ai_addrlen);
		if ( status != 0 && errno != EINPROGRESS) {
			close(sock);
			sock = -1;
			continue;
		}
		break;
	}

	if (sock < 0) {
		result->data = strerror(errno);
		goto _failed;
	}

	ns = new_fd(ss, id, sock, PROTOCOL_TCP, request->opaque, true);
	if (ns == NULL) {
		result->data = "reach skynet socket number limit";
		goto _failed;
	}

	if(status == 0) {
		ATOM_STORE(&ns->type , SOCKET_TYPE_CONNECTED);
		struct sockaddr * addr = ai_ptr->ai_addr;
		void * sin_addr = (ai_ptr->ai_family == AF_INET) ? (void*)&((struct sockaddr_in *)addr)->sin_addr : (void*)&((struct sockaddr_in6 *)addr)->sin6_addr;
		if (inet_ntop(ai_ptr->ai_family, sin_addr, ss->buffer, sizeof(ss->buffer))) {
			result->data = ss->buffer;
		}
		freeaddrinfo( ai_list );
		return SOCKET_OPEN;
	} else {
		if (enable_write(ss, ns, true)) {
			result->data = "enable write failed";
			goto _failed;
		}
		ATOM_STORE(&ns->type , SOCKET_TYPE_CONNECTING);
	}

	freeaddrinfo( ai_list );
	return -1;
_failed:
	if (sock >= 0)
		close(sock);
	freeaddrinfo( ai_list );
_failed_getaddrinfo:
	ATOM_STORE(&ss->slot[HASH_ID(id)].type, SOCKET_TYPE_INVALID);
	return SOCKET_ERR;
}

/**
 * 报告 socket 错误
 * 
 * 填充错误消息结构并返回 SOCKET_ERR 类型
 */
static int
report_error(struct socket *s, struct socket_message *result, const char *err) {
	result->id = s->id;
	result->ud = 0;
	result->opaque = s->opaque;
	result->data = (char *)err;
	return SOCKET_ERR;
}

static int
close_write(struct socket_server *ss, struct socket *s, struct socket_lock *l, struct socket_message *result) {
	if (s->closing) {
		force_close(ss,s,l,result);
		return SOCKET_RST;
	} else {
		int t = ATOM_LOAD(&s->type);
		if (t == SOCKET_TYPE_HALFCLOSE_READ) {
			// recv 0 before, ignore the error and close fd
			force_close(ss,s,l,result);
			return SOCKET_RST;
		}
		if (t == SOCKET_TYPE_HALFCLOSE_WRITE) {
			// already raise SOCKET_ERR
			return SOCKET_RST;
		}
		ATOM_STORE(&s->type, SOCKET_TYPE_HALFCLOSE_WRITE);
		shutdown(s->fd, SHUT_WR);
		enable_write(ss, s, false);
		return report_error(s, result, strerror(errno));
	}
}

static int
send_list_tcp(struct socket_server *ss, struct socket *s, struct wb_list *list, struct socket_lock *l, struct socket_message *result) {
	while (list->head) {
		struct write_buffer * tmp = list->head;
		for (;;) {
			ssize_t sz = write(s->fd, tmp->ptr, tmp->sz);
			if (sz < 0) {
				switch(errno) {
				case EINTR:
					continue;
				case AGAIN_WOULDBLOCK:
					return -1;
				}
				return close_write(ss, s, l, result);
			}
			stat_write(ss,s,(int)sz);
			s->wb_size -= sz;
			if (sz != tmp->sz) {
				tmp->ptr += sz;
				tmp->sz -= sz;
				return -1;
			}
			break;
		}
		list->head = tmp->next;
		write_buffer_free(ss,tmp);
	}
	list->tail = NULL;

	return -1;
}

static socklen_t
udp_socket_address(struct socket *s, const uint8_t udp_address[UDP_ADDRESS_SIZE], union sockaddr_all *sa) {
	int type = (uint8_t)udp_address[0];
	if (type != s->protocol)
		return 0;
	uint16_t port = 0;
	memcpy(&port, udp_address+1, sizeof(uint16_t));
	switch (s->protocol) {
	case PROTOCOL_UDP:
		memset(&sa->v4, 0, sizeof(sa->v4));
		sa->s.sa_family = AF_INET;
		sa->v4.sin_port = port;
		memcpy(&sa->v4.sin_addr, udp_address + 1 + sizeof(uint16_t), sizeof(sa->v4.sin_addr));	// ipv4 address is 32 bits
		return sizeof(sa->v4);
	case PROTOCOL_UDPv6:
		memset(&sa->v6, 0, sizeof(sa->v6));
		sa->s.sa_family = AF_INET6;
		sa->v6.sin6_port = port;
		memcpy(&sa->v6.sin6_addr, udp_address + 1 + sizeof(uint16_t), sizeof(sa->v6.sin6_addr)); // ipv6 address is 128 bits
		return sizeof(sa->v6);
	}
	return 0;
}

static void
drop_udp(struct socket_server *ss, struct socket *s, struct wb_list *list, struct write_buffer *tmp) {
	s->wb_size -= tmp->sz;
	list->head = tmp->next;
	if (list->head == NULL)
		list->tail = NULL;
	write_buffer_free(ss,tmp);
}

static int
send_list_udp(struct socket_server *ss, struct socket *s, struct wb_list *list, struct socket_message *result) {
	while (list->head) {
		struct write_buffer * tmp = list->head;
		struct write_buffer_udp * udp = (struct write_buffer_udp *)tmp;
		union sockaddr_all sa;
		socklen_t sasz = udp_socket_address(s, udp->udp_address, &sa);
		if (sasz == 0) {
			skynet_error(NULL, "socket-server : udp (%d) error: type mismatch.", s->id);
			drop_udp(ss, s, list, tmp);
			return -1;
		}
		int err = sendto(s->fd, tmp->ptr, tmp->sz, 0, &sa.s, sasz);
		if (err < 0) {
			switch(errno) {
			case EINTR:
			case AGAIN_WOULDBLOCK:
				return -1;
			}
			skynet_error(NULL, "socket-server : udp (%d) sendto error %s.",s->id, strerror(errno));
			drop_udp(ss, s, list, tmp);
			return -1;
		}
		stat_write(ss,s,tmp->sz);
		s->wb_size -= tmp->sz;
		list->head = tmp->next;
		write_buffer_free(ss,tmp);
	}
	list->tail = NULL;

	return -1;
}

static int
send_list(struct socket_server *ss, struct socket *s, struct wb_list *list, struct socket_lock *l, struct socket_message *result) {
	if (s->protocol == PROTOCOL_TCP) {
		return send_list_tcp(ss, s, list, l, result);
	} else {
		return send_list_udp(ss, s, list, result);
	}
}

static inline int
list_uncomplete(struct wb_list *s) {
	struct write_buffer *wb = s->head;
	if (wb == NULL)
		return 0;

	return (void *)wb->ptr != wb->buffer;
}

static void
raise_uncomplete(struct socket * s) {
	struct wb_list *low = &s->low;
	struct write_buffer *tmp = low->head;
	low->head = tmp->next;
	if (low->head == NULL) {
		low->tail = NULL;
	}

	// move head of low list (tmp) to the empty high list
	struct wb_list *high = &s->high;
	assert(high->head == NULL);

	tmp->next = NULL;
	high->head = high->tail = tmp;
}

static inline int
send_buffer_empty(struct socket *s) {
	return (s->high.head == NULL && s->low.head == NULL);
}

/**
 * 发送缓冲区处理函数核心逻辑
 * 
 * 每个 socket 有两个发送队列：高优先级和低优先级
 * 发送策略：
 * 1. 优先发送高优先级队列的所有数据
 * 2. 高队列为空时，尝试发送低优先级队列
 * 3. 如果低队列头部数据未发送完(之前部分发送)，将其移到高队列头部
 * 4. 两个队列都为空时，关闭写事件监听
 * 
 * @return -1        - 正常，需要继续监听
 * @return SOCKET_ERR - 发生错误
 * @return SOCKET_WARNING - 缓冲区大小低于警告阈值
 */
static int
send_buffer_(struct socket_server *ss, struct socket *s, struct socket_lock *l, struct socket_message *result) {
	assert(!list_uncomplete(&s->low));
	// step 1
	int ret = send_list(ss,s,&s->high,l,result);
	if (ret != -1) {
		if (ret == SOCKET_ERR) {
			// HALFCLOSE_WRITE
			return SOCKET_ERR;
		}
		// SOCKET_RST (ignore)
		return -1;
	}
	if (s->high.head == NULL) {
		// step 2
		if (s->low.head != NULL) {
			int ret = send_list(ss,s,&s->low,l,result);
			if (ret != -1) {
				if (ret == SOCKET_ERR) {
					// HALFCLOSE_WRITE
					return SOCKET_ERR;
				}
				// SOCKET_RST (ignore)
				return -1;
			}
			// step 3
			if (list_uncomplete(&s->low)) {
				raise_uncomplete(s);
				return -1;
			}
			if (s->low.head)
				return -1;
		}
		// step 4
		assert(send_buffer_empty(s) && s->wb_size == 0);

		if (s->closing) {
			// finish writing
			force_close(ss, s, l, result);
			return -1;
		}

		int err = enable_write(ss, s, false);

		if (err) {
			return report_error(s, result, "disable write failed");
		}

		if(s->warn_size > 0){
			s->warn_size = 0;
			result->opaque = s->opaque;
			result->id = s->id;
			result->ud = 0;
			result->data = NULL;
			return SOCKET_WARNING;
		}
	}

	return -1;
}

static int
send_buffer(struct socket_server *ss, struct socket *s, struct socket_lock *l, struct socket_message *result) {
	if (!socket_trylock(l))
		return -1;	// blocked by direct write, send later.
	if (s->dw_buffer) {
		// add direct write buffer before high.head
		struct write_buffer * buf = MALLOC(sizeof(*buf));
		struct send_object so;
		buf->userobject = send_object_init(ss, &so, (void *)s->dw_buffer, s->dw_size);
		buf->ptr = (char*)so.buffer+s->dw_offset;
		buf->sz = so.sz - s->dw_offset;
		buf->buffer = (void *)s->dw_buffer;
		s->wb_size+=buf->sz;
		if (s->high.head == NULL) {
			s->high.head = s->high.tail = buf;
			buf->next = NULL;
		} else {
			buf->next = s->high.head;
			s->high.head = buf;
		}
		s->dw_buffer = NULL;
	}
	int r = send_buffer_(ss,s,l,result);
	socket_unlock(l);

	return r;
}

static struct write_buffer *
append_sendbuffer_(struct socket_server *ss, struct wb_list *s, struct request_send * request, int size) {
	struct write_buffer * buf = MALLOC(size);
	struct send_object so;
	buf->userobject = send_object_init(ss, &so, request->buffer, request->sz);
	buf->ptr = (char*)so.buffer;
	buf->sz = so.sz;
	buf->buffer = request->buffer;
	buf->next = NULL;
	if (s->head == NULL) {
		s->head = s->tail = buf;
	} else {
		assert(s->tail != NULL);
		assert(s->tail->next == NULL);
		s->tail->next = buf;
		s->tail = buf;
	}
	return buf;
}

static inline void
append_sendbuffer_udp(struct socket_server *ss, struct socket *s, int priority, struct request_send * request, const uint8_t udp_address[UDP_ADDRESS_SIZE]) {
	struct wb_list *wl = (priority == PRIORITY_HIGH) ? &s->high : &s->low;
	struct write_buffer_udp *buf = (struct write_buffer_udp *)append_sendbuffer_(ss, wl, request, sizeof(*buf));
	memcpy(buf->udp_address, udp_address, UDP_ADDRESS_SIZE);
	s->wb_size += buf->buffer.sz;
}

static inline void
append_sendbuffer(struct socket_server *ss, struct socket *s, struct request_send * request) {
	struct write_buffer *buf = append_sendbuffer_(ss, &s->high, request, sizeof(*buf));
	s->wb_size += buf->sz;
}

static inline void
append_sendbuffer_low(struct socket_server *ss,struct socket *s, struct request_send * request) {
	struct write_buffer *buf = append_sendbuffer_(ss, &s->low, request, sizeof(*buf));
	s->wb_size += buf->sz;
}

static int
trigger_write(struct socket_server *ss, struct request_send * request, struct socket_message *result) {
	int id = request->id;
	struct socket * s = &ss->slot[HASH_ID(id)];
	if (socket_invalid(s, id))
		return -1;
	if (enable_write(ss, s, true)) {
		return report_error(s, result, "enable write failed");
	}
	return -1;
}

/*
	When send a package , we can assign the priority : PRIORITY_HIGH or PRIORITY_LOW

	If socket buffer is empty, write to fd directly.
		If write a part, append the rest part to high list. (Even priority is PRIORITY_LOW)
	Else append package to high (PRIORITY_HIGH) or low (PRIORITY_LOW) list.
 */
static int
send_socket(struct socket_server *ss, struct request_send * request, struct socket_message *result, int priority, const uint8_t *udp_address) {
	int id = request->id;
	struct socket * s = &ss->slot[HASH_ID(id)];
	struct send_object so;
	send_object_init(ss, &so, request->buffer, request->sz);
	uint8_t type = ATOM_LOAD(&s->type);
	if (type == SOCKET_TYPE_INVALID || s->id != id
		|| type == SOCKET_TYPE_HALFCLOSE_WRITE
		|| type == SOCKET_TYPE_PACCEPT
		|| s->closing) {
		so.free_func((void *)request->buffer);
		return -1;
	}
	if (type == SOCKET_TYPE_PLISTEN || type == SOCKET_TYPE_LISTEN) {
		skynet_error(NULL, "socket-server error: write to listen fd %d.", id);
		so.free_func((void *)request->buffer);
		return -1;
	}
	if (send_buffer_empty(s)) {
		if (s->protocol == PROTOCOL_TCP) {
			append_sendbuffer(ss, s, request);	// add to high priority list, even priority == PRIORITY_LOW
		} else {
			// udp
			if (udp_address == NULL) {
				udp_address = s->p.udp_address;
			}
			union sockaddr_all sa;
			socklen_t sasz = udp_socket_address(s, udp_address, &sa);
			if (sasz == 0) {
				// udp type mismatch, just drop it.
				skynet_error(NULL, "socket-server: udp socket (%d) error: type mismatch.", id);
				so.free_func((void *)request->buffer);
				return -1;
			}
			int n = sendto(s->fd, so.buffer, so.sz, 0, &sa.s, sasz);
			if (n != so.sz) {
				append_sendbuffer_udp(ss,s,priority,request,udp_address);
			} else {
				stat_write(ss,s,n);
				so.free_func((void *)request->buffer);
				return -1;
			}
		}
		if (enable_write(ss, s, true)) {
			return report_error(s, result, "enable write failed");
		}
	} else {
		if (s->protocol == PROTOCOL_TCP) {
			if (priority == PRIORITY_LOW) {
				append_sendbuffer_low(ss, s, request);
			} else {
				append_sendbuffer(ss, s, request);
			}
		} else {
			if (udp_address == NULL) {
				udp_address = s->p.udp_address;
			}
			append_sendbuffer_udp(ss,s,priority,request,udp_address);
		}
	}
	if (s->wb_size >= WARNING_SIZE && s->wb_size >= s->warn_size) {
		s->warn_size = s->warn_size == 0 ? WARNING_SIZE *2 : s->warn_size*2;
		result->opaque = s->opaque;
		result->id = s->id;
		result->ud = s->wb_size%1024 == 0 ? s->wb_size/1024 : s->wb_size/1024 + 1;
		result->data = NULL;
		return SOCKET_WARNING;
	}
	return -1;
}

static int
listen_socket(struct socket_server *ss, struct request_listen * request, struct socket_message *result) {
	int id = request->id;
	int listen_fd = request->fd;
	struct socket *s = new_fd(ss, id, listen_fd, PROTOCOL_TCP, request->opaque, false);
	if (s == NULL) {
		goto _failed;
	}
	ATOM_STORE(&s->type , SOCKET_TYPE_PLISTEN);
	result->opaque = request->opaque;
	result->id = id;
	result->ud = 0;
	result->data = "listen";

	union sockaddr_all u;
	socklen_t slen = sizeof(u);
	if (getsockname(listen_fd, &u.s, &slen) == 0) {
		void * sin_addr = (u.s.sa_family == AF_INET) ? (void*)&u.v4.sin_addr : (void *)&u.v6.sin6_addr;
		if (inet_ntop(u.s.sa_family, sin_addr, ss->buffer, sizeof(ss->buffer)) == 0) {
			result->data = strerror(errno);
			return SOCKET_ERR;
		}
		int sin_port = ntohs((u.s.sa_family == AF_INET) ? u.v4.sin_port : u.v6.sin6_port);
		result->data = ss->buffer;
		result->ud = sin_port;
	} else {
		result->data = strerror(errno);
		return SOCKET_ERR;
	}

	return SOCKET_OPEN;
_failed:
	close(listen_fd);
	result->opaque = request->opaque;
	result->id = id;
	result->ud = 0;
	result->data = "reach skynet socket number limit";
	ss->slot[HASH_ID(id)].type = SOCKET_TYPE_INVALID;

	return SOCKET_ERR;
}

static inline int
nomore_sending_data(struct socket *s) {
	return (send_buffer_empty(s) && s->dw_buffer == NULL && (ATOM_LOAD(&s->sending) & 0xffff) == 0)
		|| (ATOM_LOAD(&s->type) == SOCKET_TYPE_HALFCLOSE_WRITE);
}

static void
close_read(struct socket_server *ss, struct socket * s, struct socket_message *result) {
	// Don't read socket later
	ATOM_STORE(&s->type , SOCKET_TYPE_HALFCLOSE_READ);
	enable_read(ss,s,false);
	shutdown(s->fd, SHUT_RD);
	result->id = s->id;
	result->ud = 0;
	result->data = NULL;
	result->opaque = s->opaque;
}

static inline int
halfclose_read(struct socket *s) {
	return ATOM_LOAD(&s->type) == SOCKET_TYPE_HALFCLOSE_READ;
}

/**
 * SOCKET_CLOSE 消息触发条件(只会触发一次)
 * 
 * 触发条件(满足其一)：
 * 1. 主动关闭 socket (close_socket函数)
 * 2. 收到对方关闭通知(recv 返回 0 或收到 EOF 事件)
 * 
 * 注意：在条件2下，收到 SOCKET_CLOSE 后仍可发送数据，
 * 但如果对方已完全关闭，可能会触发 SOCKET_ERR。
 * 
 * 详细讨论参见：https://github.com/cloudwu/skynet/issues/1346
 */
static int
close_socket(struct socket_server *ss, struct request_close *request, struct socket_message *result) {
	int id = request->id;
	struct socket * s = &ss->slot[HASH_ID(id)];
	if (socket_invalid(s, id)) {
		// The socket is closed, ignore
		return -1;
	}
	struct socket_lock l;
	socket_lock_init(s, &l);

	int shutdown_read = halfclose_read(s);

	if (request->shutdown || nomore_sending_data(s)) {
		// If socket is SOCKET_TYPE_HALFCLOSE_READ, Do not raise SOCKET_CLOSE again.
		int r = shutdown_read ? -1 : SOCKET_CLOSE;
		force_close(ss,s,&l,result);
		return r;
	}
	s->closing = true;
	if (!shutdown_read) {
		// don't read socket after socket.close()
		close_read(ss, s, result);
		return SOCKET_CLOSE;
	}
	// recv 0 before (socket is SOCKET_TYPE_HALFCLOSE_READ) and waiting for sending data out.
	return -1;
}

static int
bind_socket(struct socket_server *ss, struct request_bind *request, struct socket_message *result) {
	int id = request->id;
	result->id = id;
	result->opaque = request->opaque;
	result->ud = 0;
	struct socket *s = new_fd(ss, id, request->fd, PROTOCOL_TCP, request->opaque, true);
	if (s == NULL) {
		result->data = "reach skynet socket number limit";
		return SOCKET_ERR;
	}
	sp_nonblocking(request->fd);
	ATOM_STORE(&s->type , SOCKET_TYPE_BIND);
	result->data = "binding";
	return SOCKET_OPEN;
}

static int
resume_socket(struct socket_server *ss, struct request_resumepause *request, struct socket_message *result) {
	int id = request->id;
	result->id = id;
	result->opaque = request->opaque;
	result->ud = 0;
	result->data = NULL;
	struct socket *s = &ss->slot[HASH_ID(id)];
	if (socket_invalid(s, id)) {
		result->data = "invalid socket";
		return SOCKET_ERR;
	}
	if (halfclose_read(s)) {
		// The closing socket may be in transit, so raise an error. See https://github.com/cloudwu/skynet/issues/1374
		result->data = "socket closed";
		return SOCKET_ERR;
	}
	struct socket_lock l;
	socket_lock_init(s, &l);
	if (enable_read(ss, s, true)) {
		result->data = "enable read failed";
		return SOCKET_ERR;
	}
	uint8_t type = ATOM_LOAD(&s->type);
	if (type == SOCKET_TYPE_PACCEPT || type == SOCKET_TYPE_PLISTEN) {
		ATOM_STORE(&s->type , (type == SOCKET_TYPE_PACCEPT) ? SOCKET_TYPE_CONNECTED : SOCKET_TYPE_LISTEN);
		s->opaque = request->opaque;
		result->data = "start";
		return SOCKET_OPEN;
	} else if (type == SOCKET_TYPE_CONNECTED) {
		// todo: maybe we should send a message SOCKET_TRANSFER to s->opaque
		s->opaque = request->opaque;
		result->data = "transfer";
		return SOCKET_OPEN;
	}
	// if s->type == SOCKET_TYPE_HALFCLOSE_WRITE , SOCKET_CLOSE message will send later
	return -1;
}

static int
pause_socket(struct socket_server *ss, struct request_resumepause *request, struct socket_message *result) {
	int id = request->id;
	struct socket *s = &ss->slot[HASH_ID(id)];
	if (socket_invalid(s, id)) {
		return -1;
	}
	if (enable_read(ss, s, false)) {
		return report_error(s, result, "enable read failed");
	}
	return -1;
}

static void
setopt_socket(struct socket_server *ss, struct request_setopt *request) {
	int id = request->id;
	struct socket *s = &ss->slot[HASH_ID(id)];
	if (socket_invalid(s, id)) {
		return;
	}
	int v = request->value;
	setsockopt(s->fd, IPPROTO_TCP, request->what, &v, sizeof(v));
}

static void
block_readpipe(int pipefd, void *buffer, int sz) {
	for (;;) {
		int n = read(pipefd, buffer, sz);
		if (n<0) {
			if (errno == EINTR)
				continue;
			skynet_error(NULL, "socket-server : read pipe error %s.",strerror(errno));
			return;
		}
		// must atomic read from a pipe
		assert(n == sz);
		return;
	}
}

static int
has_cmd(struct socket_server *ss) {
	struct timeval tv = {0,0};
	int retval;

	FD_SET(ss->recvctrl_fd, &ss->rfds);

	retval = select(ss->recvctrl_fd+1, &ss->rfds, NULL, NULL, &tv);
	if (retval == 1) {
		return 1;
	}
	return 0;
}

static void
add_udp_socket(struct socket_server *ss, struct request_udp *udp) {
	int id = udp->id;
	int protocol;
	if (udp->family == AF_INET6) {
		protocol = PROTOCOL_UDPv6;
	} else {
		protocol = PROTOCOL_UDP;
	}
	struct socket *ns = new_fd(ss, id, udp->fd, protocol, udp->opaque, true);
	if (ns == NULL) {
		close(udp->fd);
		ss->slot[HASH_ID(id)].type = SOCKET_TYPE_INVALID;
		return;
	}
	ATOM_STORE(&ns->type , SOCKET_TYPE_CONNECTED);
	memset(ns->p.udp_address, 0, sizeof(ns->p.udp_address));
}

static int
set_udp_address(struct socket_server *ss, struct request_setudp *request, struct socket_message *result) {
	int id = request->id;
	struct socket *s = &ss->slot[HASH_ID(id)];
	if (socket_invalid(s, id)) {
		return -1;
	}
	int type = request->address[0];
	if (type != s->protocol) {
		// protocol mismatch
		return report_error(s, result, "protocol mismatch");
	}
	if (type == PROTOCOL_UDP) {
		memcpy(s->p.udp_address, request->address, 1+2+4);	// 1 type, 2 port, 4 ipv4
	} else {
		memcpy(s->p.udp_address, request->address, 1+2+16);	// 1 type, 2 port, 16 ipv6
	}
	ATOM_FDEC(&s->udpconnecting);
	return -1;
}

static int
dial_udp_socket(struct socket_server *ss, struct request_dial_udp *request, struct socket_message *result){
	int id = request->id;
	int protocol = request->address[0];

	struct socket *ns = new_fd(ss, id, request->fd, protocol, request->opaque, true);
	if (ns == NULL){
		close(request->fd);
		ss->slot[HASH_ID(id)].type = SOCKET_TYPE_INVALID;
		return -1;
	}

	if (protocol == PROTOCOL_UDP){
		memcpy(ns->p.udp_address, request->address, 1 + 2 + 4);
	} else {
		memcpy(ns->p.udp_address, request->address, 1 + 2 + 16);
	}

	ATOM_STORE(&ns->type , SOCKET_TYPE_CONNECTED);

	ATOM_FDEC(&ns->udpconnecting);
	return -1;
}

static inline void
inc_sending_ref(struct socket *s, int id) {
	if (s->protocol != PROTOCOL_TCP)
		return;
	for (;;) {
		unsigned long sending = ATOM_LOAD(&s->sending);
		if ((sending >> 16) == ID_TAG16(id)) {
			if ((sending & 0xffff) == 0xffff) {
				// s->sending may overflow (rarely), so busy waiting here for socket thread dec it. see issue #794
				continue;
			}
			// inc sending only matching the same socket id
			if (ATOM_CAS_ULONG(&s->sending, sending, sending + 1))
				return;
			// atom inc failed, retry
		} else {
			// socket id changed, just return
			return;
		}
	}
}

static inline void
dec_sending_ref(struct socket_server *ss, int id) {
	struct socket * s = &ss->slot[HASH_ID(id)];
	// Notice: udp may inc sending while type == SOCKET_TYPE_RESERVE
	if (s->id == id && s->protocol == PROTOCOL_TCP) {
		assert((ATOM_LOAD(&s->sending) & 0xffff) != 0);
		ATOM_FDEC(&s->sending);
	}
}

/**
 * 处理控制管道命令
 * 
 * 从管道读取命令并执行对应操作
 * 命令格式：1字节类型 + 1字节长度 + 变长数据
 * 
 * @return 处理结果类型(SOCKET_OPEN/SOCKET_CLOSE/SOCKET_ERR/等)，-1 表示无需返回消息
 */
static int
ctrl_cmd(struct socket_server *ss, struct socket_message *result) {
	int fd = ss->recvctrl_fd;
	// the length of message is one byte, so 256 buffer size is enough.
	uint8_t buffer[256];
	uint8_t header[2];
	block_readpipe(fd, header, sizeof(header));
	int type = header[0];
	int len = header[1];
	block_readpipe(fd, buffer, len);
	// ctrl command only exist in local fd, so don't worry about endian.
	switch (type) {
	case 'R':
		return resume_socket(ss,(struct request_resumepause *)buffer, result);
	case 'S':
		return pause_socket(ss,(struct request_resumepause *)buffer, result);
	case 'B':
		return bind_socket(ss,(struct request_bind *)buffer, result);
	case 'L':
		return listen_socket(ss,(struct request_listen *)buffer, result);
	case 'K':
		return close_socket(ss,(struct request_close *)buffer, result);
	case 'O':
		return open_socket(ss, (struct request_open *)buffer, result);
	case 'X':
		result->opaque = 0;
		result->id = 0;
		result->ud = 0;
		result->data = NULL;
		return SOCKET_EXIT;
	case 'W':
		return trigger_write(ss, (struct request_send *)buffer, result);
	case 'D':
	case 'P': {
		int priority = (type == 'D') ? PRIORITY_HIGH : PRIORITY_LOW;
		struct request_send * request = (struct request_send *) buffer;
		int ret = send_socket(ss, request, result, priority, NULL);
		dec_sending_ref(ss, request->id);
		return ret;
	}
	case 'A': {
		struct request_send_udp * rsu = (struct request_send_udp *)buffer;
		return send_socket(ss, &rsu->send, result, PRIORITY_HIGH, rsu->address);
	}
	case 'C':
		return set_udp_address(ss, (struct request_setudp *)buffer, result);
	case 'N':
		return dial_udp_socket(ss, (struct request_dial_udp *)buffer, result);
	case 'T':
		setopt_socket(ss, (struct request_setopt *)buffer);
		return -1;
	case 'U':
		add_udp_socket(ss, (struct request_udp *)buffer);
		return -1;
	default:
		skynet_error(NULL, "socket-server error: Unknown ctrl %c.",type);
		return -1;
	};

	return -1;
}

/**
 * 转发 TCP 消息(读取数据)
 * 
 * 从 socket 读取数据到动态分配的缓冲区
 * 
 * 返回值：
 * - SOCKET_DATA: 读取到数据
 * - SOCKET_MORE: 读取到数据，但缓冲区可能已满，建议立即再次读取
 * - SOCKET_CLOSE: 对方关闭连接(recv 返回 0)
 * - SOCKET_ERR: 读取错误
 * - -1: 无数据可读(EAGAIN)或已处理
 */
static int
forward_message_tcp(struct socket_server *ss, struct socket *s, struct socket_lock *l, struct socket_message * result) {
	int sz = s->p.size;
	char * buffer = MALLOC(sz);
	int n = (int)read(s->fd, buffer, sz);
	if (n<0) {
		FREE(buffer);
		switch(errno) {
		case EINTR:
		case AGAIN_WOULDBLOCK:
			break;
		default:
			return report_error(s, result, strerror(errno));
		}
		return -1;
	}
	if (n==0) {
		FREE(buffer);
		if (s->closing) {
			// Rare case : if s->closing is true, reading event is disable, and SOCKET_CLOSE is raised.
			if (nomore_sending_data(s)) {
				force_close(ss,s,l,result);
			}
			return -1;
		}
		int t = ATOM_LOAD(&s->type);
		if (t == SOCKET_TYPE_HALFCLOSE_READ) {
			// Rare case : Already shutdown read.
			return -1;
		}
		if (t == SOCKET_TYPE_HALFCLOSE_WRITE) {
			// Remote shutdown read (write error) before.
			force_close(ss,s,l,result);
		} else {
			close_read(ss, s, result);
		}
		return SOCKET_CLOSE;
	}

	if (halfclose_read(s)) {
		// discard recv data (Rare case : if socket is HALFCLOSE_READ, reading event is disable.)
		FREE(buffer);
		return -1;
	}

	stat_read(ss,s,n);

	result->opaque = s->opaque;
	result->id = s->id;
	result->ud = n;
	result->data = buffer;

	if (n == sz) {
		s->p.size *= 2;
		return SOCKET_MORE;
	} else if (sz > MIN_READ_BUFFER && n*2 < sz) {
		s->p.size /= 2;
	}

	return SOCKET_DATA;
}

static int
gen_udp_address(int protocol, union sockaddr_all *sa, uint8_t * udp_address) {
	int addrsz = 1;
	udp_address[0] = (uint8_t)protocol;
	if (protocol == PROTOCOL_UDP) {
		memcpy(udp_address+addrsz, &sa->v4.sin_port, sizeof(sa->v4.sin_port));
		addrsz += sizeof(sa->v4.sin_port);
		memcpy(udp_address+addrsz, &sa->v4.sin_addr, sizeof(sa->v4.sin_addr));
		addrsz += sizeof(sa->v4.sin_addr);
	} else {
		memcpy(udp_address+addrsz, &sa->v6.sin6_port, sizeof(sa->v6.sin6_port));
		addrsz += sizeof(sa->v6.sin6_port);
		memcpy(udp_address+addrsz, &sa->v6.sin6_addr, sizeof(sa->v6.sin6_addr));
		addrsz += sizeof(sa->v6.sin6_addr);
	}
	return addrsz;
}

/**
 * 转发 UDP 消息(接收数据包)
 * 
 * 【函数作用】
 * 从 UDP socket 接收数据报，将数据和发送方地址封装成消息返回
 * 
 * 【与 TCP 的区别】
 * 1. UDP 是数据报模式，每次 recvfrom 得到一个完整的数据包
 * 2. 需要记录发送方地址，以便后续回复
 * 3. 地址信息附加在数据末尾，格式: [数据][1字节类型][2字节端口][4/16字节IP]
 * 
 * 【数据格式】
 * 返回的 result->data 布局:
 * | 数据 (n字节) | 地址类型 (1字节) | 端口 (2字节大端) | IP地址 (4或16字节) |
 * 
 * 总长度: n + 1 + 2 + 4 = n+7 (IPv4) 或 n + 1 + 2 + 16 = n+19 (IPv6)
 * 
 * 【错误处理】
 * - EINTR/EAGAIN: 临时无数据，返回 -1，不关闭 socket
 * - 其他错误: 强制关闭 socket，返回 SOCKET_ERR
 * 
 * @param ss     socket 服务器实例
 * @param s      当前处理的 socket 结构体
 * @param l      socket 锁(UDP 实际未使用，保持接口统一)
 * @param result 输出参数，返回接收到的数据和地址信息
 * 
 * @return SOCKET_UDP:  成功接收数据包，result 中填充有效数据
 * @return SOCKET_ERR:  发生错误，socket 已被关闭
 * @return -1:          无数据可读(EAGAIN)或协议类型不匹配
 */
static int
forward_message_udp(struct socket_server *ss, struct socket *s, struct socket_lock *l, struct socket_message * result) {
	// 通用地址结构，可以容纳 IPv4 或 IPv6
	union sockaddr_all sa;
	socklen_t slen = sizeof(sa);
	
	// 使用 recvfrom 接收 UDP 数据包，同时获取发送方地址
	// ss->udpbuffer 是预分配的接收缓冲区(MAX_UDP_PACKAGE = 65535)
	int n = recvfrom(s->fd, ss->udpbuffer, MAX_UDP_PACKAGE, 0, &sa.s, &slen);
	
	if (n < 0) {
		// 错误处理
		switch(errno) {
		case EINTR:           // 被信号中断
		case AGAIN_WOULDBLOCK:// 缓冲区无数据(非阻塞模式)
			return -1;        // 临时错误，不关闭 socket，下次再试
		}
		
		// 其他错误(如 EBADF/ECONNREFUSED)，关闭 socket
		int error = errno;
		force_close(ss, s, l, result);
		result->data = strerror(error);
		return SOCKET_ERR;
	}
	
	// 统计读取数据量
	stat_read(ss, s, n);

	uint8_t * data;
	
	// 根据地址长度判断是 IPv4 还是 IPv6
	if (slen == sizeof(sa.v4)) {
		// IPv4 地址
		if (s->protocol != PROTOCOL_UDP)
			return -1;  // 协议类型不匹配(创建了 IPv4 socket 但收到 IPv6 数据)
		
		// 分配内存: 数据长度 + 1字节类型 + 2字节端口 + 4字节IPv4地址
		data = MALLOC(n + 1 + 2 + 4);
		
		// 将地址信息编码到数据末尾(data + n 位置)
		gen_udp_address(PROTOCOL_UDP, &sa, data + n);
		
	} else {
		// IPv6 地址
		if (s->protocol != PROTOCOL_UDPv6)
			return -1;  // 协议类型不匹配
		
		// 分配内存: 数据长度 + 1字节类型 + 2字节端口 + 16字节IPv6地址
		data = MALLOC(n + 1 + 2 + 16);
		
		// 将地址信息编码到数据末尾
		gen_udp_address(PROTOCOL_UDPv6, &sa, data + n);
	}
	
	// 拷贝数据到缓冲区前面
	memcpy(data, ss->udpbuffer, n);

	// 填充返回结果
	result->opaque = s->opaque;  // 关联的服务句柄(用于路由消息)
	result->id = s->id;          // socket id
	result->ud = n;              // 数据字节数(不包含地址信息)
	result->data = (char *)data; // 数据和地址的缓冲区(上层需要释放)

	return SOCKET_UDP;
}

static int
report_connect(struct socket_server *ss, struct socket *s, struct socket_lock *l, struct socket_message *result) {
	int error;
	socklen_t len = sizeof(error);
	int code = getsockopt(s->fd, SOL_SOCKET, SO_ERROR, &error, &len);
	if (code < 0 || error) {
		error = code < 0 ? errno : error;
		force_close(ss, s, l, result);
		result->data = strerror(error);
		return SOCKET_ERR;
	} else {
		ATOM_STORE(&s->type , SOCKET_TYPE_CONNECTED);
		result->opaque = s->opaque;
		result->id = s->id;
		result->ud = 0;
		if (nomore_sending_data(s)) {
			if (enable_write(ss, s, false)) {
				force_close(ss,s,l, result);
				result->data = "disable write failed";
				return SOCKET_ERR;
			}
		}
		union sockaddr_all u;
		socklen_t slen = sizeof(u);
		if (getpeername(s->fd, &u.s, &slen) == 0) {
			void * sin_addr = (u.s.sa_family == AF_INET) ? (void*)&u.v4.sin_addr : (void *)&u.v6.sin6_addr;
			if (inet_ntop(u.s.sa_family, sin_addr, ss->buffer, sizeof(ss->buffer))) {
				result->data = ss->buffer;
				return SOCKET_OPEN;
			}
		}
		result->data = NULL;
		return SOCKET_OPEN;
	}
}

static int
getname(union sockaddr_all *u, char *buffer, size_t sz) {
	char tmp[INET6_ADDRSTRLEN];
	void * sin_addr = (u->s.sa_family == AF_INET) ? (void*)&u->v4.sin_addr : (void *)&u->v6.sin6_addr;
	if (inet_ntop(u->s.sa_family, sin_addr, tmp, sizeof(tmp))) {
		int sin_port = ntohs((u->s.sa_family == AF_INET) ? u->v4.sin_port : u->v6.sin6_port);
		snprintf(buffer, sz, "%s:%d", tmp, sin_port);
		return 1;
	} else {
		buffer[0] = '\0';
		return 0;
	}
}

/**
 * 接受新连接
 * 
 * 处理监听 socket 的 accept 事件
 * 
 * @return 1:  成功接受连接，返回 SOCKET_ACCEPT 消息
 * @return 0:  接受失败(非 EMFILE 错误)，继续监听
 * @return -1: EMFILE 错误(文件描述符耗尽)
 */
static int
report_accept(struct socket_server *ss, struct socket *s, struct socket_message *result) {
	union sockaddr_all u;
	socklen_t len = sizeof(u);
	int client_fd = accept(s->fd, &u.s, &len);
	if (client_fd < 0) {
		if (errno == EMFILE || errno == ENFILE) {
			result->opaque = s->opaque;
			result->id = s->id;
			result->ud = 0;
			result->data = strerror(errno);

			// See https://stackoverflow.com/questions/47179793/how-to-gracefully-handle-accept-giving-emfile-and-close-the-connection
			if (ss->reserve_fd >= 0) {
				close(ss->reserve_fd);
				client_fd = accept(s->fd, &u.s, &len);
				if (client_fd >= 0) {
					close(client_fd);
				}
				ss->reserve_fd = dup(1);
			}
			return -1;
		} else {
			return 0;
		}
	}
	int id = reserve_id(ss);
	if (id < 0) {
		close(client_fd);
		return 0;
	}
	socket_keepalive(client_fd);
	sp_nonblocking(client_fd);
	struct socket *ns = new_fd(ss, id, client_fd, PROTOCOL_TCP, s->opaque, false);
	if (ns == NULL) {
		close(client_fd);
		return 0;
	}
	// accept new one connection
	stat_read(ss,s,1);

	ATOM_STORE(&ns->type , SOCKET_TYPE_PACCEPT);
	result->opaque = s->opaque;
	result->id = s->id;
	result->ud = id;
	result->data = NULL;

	if (getname(&u, ss->buffer, sizeof(ss->buffer))) {
		result->data = ss->buffer;
	}

	return 1;
}

static inline void
clear_closed_event(struct socket_server *ss, struct socket_message * result, int type) {
	if (type == SOCKET_CLOSE || type == SOCKET_ERR) {
		int id = result->id;
		int i;
		for (i=ss->event_index; i<ss->event_n; i++) {
			struct event *e = &ss->ev[i];
			struct socket *s = e->s;
			if (s) {
				if (socket_invalid(s, id) && s->id == id) {
					e->s = NULL;
					break;
				}
			}
		}
	}
}

/**
 * Socket 服务器主循环函数(事件轮询)
 * 
 * 【函数作用】
 * 这是 socket 线程的核心事件循环，阻塞等待并处理两类事件：
 * 1. 控制命令：来自其他线程的操作请求(发送数据、关闭连接等)
 * 2. IO 事件：来自网络的数据收发事件(通过 epoll/kqueue/select)
 * 
 * 【处理优先级】控制命令 > IO 事件
 * 原因：控制命令包含状态变更(如关闭socket)，需优先处理保证数据一致性
 * 
 * 【事件批处理】
 * 使用 event_index/event_n 进行批量处理，避免频繁系统调用
 * 
 * 【读写策略】同一 socket 可读可写时，先处理读，再处理写(通过 event_index-- 回退)
 * 
 * 【主循环流程】
 * 1. 检查控制管道命令(checkctrl/has_cmd/ctrl_cmd)
 * 2. 等待网络事件(sp_wait/epoll_wait)
 * 3. 根据 socket 类型分发处理(CONNECTING/LISTEN/CONNECTED等)
 * 4. 处理读写错误事件(read/write/error/eof)
 * 
 * @param ss     socket 服务器实例
 * @param result 输出参数，返回的事件消息结构
 * @param more   输出参数，指示当前批次是否还有更多事件
 *               0=新批次开始，1=当前批次还有事件
 * @return       事件类型(SOCKET_DATA/OPEN/CLOSE/ERR/ACCEPT/UDP/WARNING/EXIT)
 *               返回值 > 0 表示有有效事件需要上层处理，-1 表示无事件
 */
int
socket_server_poll(struct socket_server *ss, struct socket_message * result, int * more) {
	// 无限循环，直到有有效事件需要返回给上层
	for (;;) {
		/* ============================================================
		 * 阶段1：处理控制命令(优先级最高)
		 * 通过管道接收其他线程的命令。checkctrl 标志避免单批次内重复检查
		 * ============================================================ */
		if (ss->checkctrl) {
			// 检查控制管道是否有数据可读(使用 select 非阻塞检查)
			if (has_cmd(ss)) {
				// 读取并执行控制命令('D'发送/'K'关闭/'O'连接等)
				int type = ctrl_cmd(ss, result);
				if (type != -1) {
					// 如果命令导致 socket 关闭，清理 epoll 中该 socket 的待处理事件
					// 避免处理已关闭的 fd 造成错误
					clear_closed_event(ss, result, type);
					return type;  // 返回命令执行结果给上层
				} else {
					// type == -1 表示命令已处理但无需返回(如设置选项)，继续循环
					continue;
				}
			} else {
				// 当前没有控制命令，标记为已检查，本批次内不再检查
				ss->checkctrl = 0;
			}
		}
		/* ============================================================
		 * 阶段2：等待并获取 IO 事件
		 * 当当前批次事件全部处理完(event_index == event_n)时，调用 epoll_wait
		 * ============================================================ */
		if (ss->event_index == ss->event_n) {
			// 阻塞等待网络事件，最多返回 MAX_EVENT(64) 个事件
			// 超时时间由底层实现决定(epoll_wait 参数)
			ss->event_n = sp_wait(ss->event_fd, ss->ev, MAX_EVENT);
			
			// 新批次开始，重置控制命令检查标志
			ss->checkctrl = 1;
			
			// 重置 more 标志，告诉调用者这是新批次开始
			if (more) {
				*more = 0;
			}
			
			// 重置事件索引，从第一个事件开始处理
			ss->event_index = 0;
			
			// 错误处理
			if (ss->event_n <= 0) {
				ss->event_n = 0;
				int err = errno;
				// EINTR 是被信号中断，属于正常现象，静默重试
				// 其他错误记录日志但继续运行(不退出，保证服务可用性)
				if (err != EINTR) {
					skynet_error(NULL, "socket-server error: %s", strerror(err));
				}
				continue;  // 回到循环开头重新等待
			}
		}
		/* ============================================================
		 * 阶段3：处理单个 IO 事件
		 * 从事件数组中取出当前事件，根据 socket 类型和事件类型分发处理
		 * ============================================================ */
		struct event *e = &ss->ev[ss->event_index++];
		struct socket *s = e->s;
		
		// s == NULL 表示该事件已被 clear_closed_event 标记为无效
		// (如对应的 socket 已被控制命令关闭，需要从 epoll 队列中忽略)
		if (s == NULL) {
			continue;
		}
		
		// 初始化 socket 锁，用于保护直接写(dw_buffer)操作的线程安全
		struct socket_lock l;
		socket_lock_init(s, &l);
		/* ============================================================
		 * 阶段4：根据 socket 类型分发处理
		 * CONNECTING: 异步连接完成，检查连接结果
		 * LISTEN: 有新连接请求，执行 accept
		 * 其他: 已连接状态，处理读写事件
		 * ============================================================ */
		switch (ATOM_LOAD(&s->type)) {
			
		// 状态1：正在连接中(非阻塞 connect 已发出，等待连接完成)
		case SOCKET_TYPE_CONNECTING:
			// 检查连接结果，返回 CONNECT(成功) 或 ERR(失败)
			return report_connect(ss, s, &l, result);
			
		// 状态2：监听中，有连接请求到达
		case SOCKET_TYPE_LISTEN: {
			// accept 新连接，可能返回 EMFILE/ENFILE 错误
			int ok = report_accept(ss, s, result);
			if (ok > 0) {
				return SOCKET_ACCEPT;  // 成功接受连接，返回新 fd
			} else if (ok < 0) {
				return SOCKET_ERR;     // EMFILE 等系统错误
			}
			// ok == 0 表示临时失败(如客户端已断开)，继续处理其他事件
			break;
		}
			
		// 状态3：无效 socket(罕见情况，socket 已被关闭但 epoll 还有残留事件)
		case SOCKET_TYPE_INVALID:
			skynet_error(NULL, "socket-server error: invalid socket");
			break;
		// 状态4：已连接状态(CONNECTED/BIND/HALFCLOSE等)，处理数据收发
		default:
			// ====== 处理读事件(数据到达) ======
			if (e->read) {
				int type;
				if (s->protocol == PROTOCOL_TCP) {
					// TCP 流式数据读取
					type = forward_message_tcp(ss, s, &l, result);
					
					// SOCKET_MORE 表示读满了缓冲区，可能还有更多数据
					// 回退 event_index，让上层立即再次调用 poll 继续读取
					if (type == SOCKET_MORE) {
						--ss->event_index;
						return SOCKET_DATA;
					}
				} else {
					// UDP 数据报接收
					type = forward_message_udp(ss, s, &l, result);
					
					// UDP 也尝试多读，尽可能一次处理完所有到达的包
					if (type == SOCKET_UDP) {
						--ss->event_index;
						return SOCKET_UDP;
					}
				}
				
				// 如果同时有写事件，且读处理未导致关闭，回退索引用下次处理写
				// 这样可以保证读写都及时处理，但避免一次 poll 中处理过多逻辑
				if (e->write && type != SOCKET_CLOSE && type != SOCKET_ERR) {
					e->read = false;       // 标记读已处理
					--ss->event_index;     // 回退索引，下次继续处理此 socket 的写
				}
				
				// type == -1 表示无有效数据(EAGAIN等)，继续处理下一个事件
				if (type == -1)
					break;
					
				return type;  // 返回 DATA/CLOSE/ERR 等事件给上层
			}
			// ====== 处理写事件(发送缓冲区可写) ======
			if (e->write) {
				// 发送写缓冲区中的数据(high/low 优先级队列)
				int type = send_buffer(ss, s, &l, result);
				if (type == -1)
					break;  // 无可发送数据或 EAGAIN，继续下一个事件
				return type;  // 返回 ERR 或 WARNING 给上层
			}
			// ====== 处理错误事件(socket 出错) ======
			if (e->error) {
				int error;
				socklen_t len = sizeof(error);
				// 通过 getsockopt(SO_ERROR) 获取具体错误码
				int code = getsockopt(s->fd, SOL_SOCKET, SO_ERROR, &error, &len);
				const char * err = NULL;
				if (code < 0) {
					err = strerror(errno);
				} else if (error != 0) {
					err = strerror(error);
				} else {
					err = "Unknown error";
				}
				return report_error(s, result, err);
			}
			// ====== 处理 EOF 事件(对端发送 FIN 包关闭连接) ======
			if (e->eof) {
				// epoll 检测到连接关闭(HUP/ERR 事件)
				// 注意：对于 epoll，HUP 事件可能在读写之后触发
				// 参考: https://stackoverflow.com/questions/52976152/tcp-when-is-epollhup-generated
				int halfclose = halfclose_read(s);
				force_close(ss, s, &l, result);
				if (!halfclose) {
					return SOCKET_CLOSE;
				}
			}
			break;
		}
	}
}

/**
 * 发送控制请求到 socket 线程
 * 
 * 通过控制管道将命令发送给 socket 线程
 * 格式：header[6]=类型, header[7]=长度，后接数据
 * 
 * @param ss  socket 服务器
 * @param request 请求包
 * @param type 命令类型('O','K','D'等)
 * @param len  数据长度
 */
static void
send_request(struct socket_server *ss, struct request_package *request, char type, int len) {
	request->header[6] = (uint8_t)type;
	request->header[7] = (uint8_t)len;
	const char * req = (const char *)request + offsetof(struct request_package, header[6]);
	for (;;) {
		ssize_t n = write(ss->sendctrl_fd, req, len+2);
		if (n<0) {
			if (errno != EINTR) {
				skynet_error(NULL, "socket-server : send ctrl command error %s.", strerror(errno));
			}
			continue;
		}
		assert(n == len+2);
		return;
	}
}

static int
open_request(struct socket_server *ss, struct request_package *req, uintptr_t opaque, const char *addr, int port) {
	int len = strlen(addr);
	if (len + sizeof(req->u.open) >= 256) {
		skynet_error(NULL, "socket-server error: Invalid addr %s.",addr);
		return -1;
	}
	int id = reserve_id(ss);
	if (id < 0)
		return -1;
	req->u.open.opaque = opaque;
	req->u.open.id = id;
	req->u.open.port = port;
	memcpy(req->u.open.host, addr, len);
	req->u.open.host[len] = '\0';

	return len;
}

static inline void
request_init(struct request_package *req) {
	memset(req, 0, sizeof(*req));
}

/**
 * 连接到远程 TCP 服务器
 * 
 * 发送连接请求给 socket 线程，立即返回 socket ID
 * 连接结果通过 socket_server_poll 返回 SOCKET_OPEN 或 SOCKET_ERR
 * 
 * @param ss     socket 服务器
 * @param opaque 关联服务句柄
 * @param addr   目标地址(IP 或域名)
 * @param port   目标端口
 * @return socket ID(>0)，失败返回 -1
 */
int
socket_server_connect(struct socket_server *ss, uintptr_t opaque, const char * addr, int port) {
	struct request_package request;
	request_init(&request);
	int len = open_request(ss, &request, opaque, addr, port);
	if (len < 0)
		return -1;
	send_request(ss, &request, 'O', sizeof(request.u.open) + len);
	return request.u.open.id;
}

static inline int
can_direct_write(struct socket *s, int id) {
	return s->id == id && nomore_sending_data(s) && ATOM_LOAD(&s->type) == SOCKET_TYPE_CONNECTED && ATOM_LOAD(&s->udpconnecting) == 0;
}

/**
 * 发送数据(高优先级)
 * 
 * 尝试直接发送数据，如果 socket 正忙则将数据加入发送队列
 * 这是线程安全的，可以从任意线程调用
 * 
 * @param ss  socket 服务器
 * @param buf 发送缓冲区(包含 id, data, sz, type)
 * @return 0  - 成功(数据已入队或直接发送)
 * @return -1 - 失败(socket 无效或已关闭)
 */
int
socket_server_send(struct socket_server *ss, struct socket_sendbuffer *buf) {
	int id = buf->id;
	struct socket * s = &ss->slot[HASH_ID(id)];
	if (socket_invalid(s, id) || s->closing) {
		free_buffer(ss, buf);
		return -1;
	}

	struct socket_lock l;
	socket_lock_init(s, &l);

	if (can_direct_write(s,id) && socket_trylock(&l)) {
		// may be we can send directly, double check
		if (can_direct_write(s,id)) {
			// send directly
			struct send_object so;
			send_object_init_from_sendbuffer(ss, &so, buf);
			ssize_t n;
			if (s->protocol == PROTOCOL_TCP) {
				n = write(s->fd, so.buffer, so.sz);
			} else {
				union sockaddr_all sa;
				socklen_t sasz = udp_socket_address(s, s->p.udp_address, &sa);
				if (sasz == 0) {
					skynet_error(NULL, "socket-server : set udp (%d) error: address first.", id);
					socket_unlock(&l);
					so.free_func((void *)buf->buffer);
					return -1;
				}
				n = sendto(s->fd, so.buffer, so.sz, 0, &sa.s, sasz);
			}
			if (n<0) {
				// ignore error, let socket thread try again
				n = 0;
			}
			stat_write(ss,s,n);
			if (n == so.sz) {
				// write done
				socket_unlock(&l);
				so.free_func((void *)buf->buffer);
				return 0;
			}
			// write failed, put buffer into s->dw_* , and let socket thread send it. see send_buffer()
			s->dw_buffer = clone_buffer(buf, &s->dw_size);
			s->dw_offset = n;

			socket_unlock(&l);

			struct request_package request;
			request_init(&request);
			request.u.send.id = id;
			request.u.send.sz = 0;
			request.u.send.buffer = NULL;

			// let socket thread enable write event
			send_request(ss, &request, 'W', sizeof(request.u.send));

			return 0;
		}
		socket_unlock(&l);
	}

	inc_sending_ref(s, id);

	struct request_package request;
	request_init(&request);
	request.u.send.id = id;
	request.u.send.buffer = clone_buffer(buf, &request.u.send.sz);

	send_request(ss, &request, 'D', sizeof(request.u.send));
	return 0;
}

/**
 * 发送数据(低优先级)
 * 
 * 与 socket_server_send 类似，但数据加入低优先级队列
 * 只有当高优先级队列为空时才会发送低优先级数据
 * 
 * @param ss  socket 服务器
 * @param buf 发送缓冲区
 * @return 0  - 成功
 * @return -1 - 失败
 */
int
socket_server_send_lowpriority(struct socket_server *ss, struct socket_sendbuffer *buf) {
	int id = buf->id;

	struct socket * s = &ss->slot[HASH_ID(id)];
	if (socket_invalid(s, id)) {
		free_buffer(ss, buf);
		return -1;
	}

	inc_sending_ref(s, id);

	struct request_package request;
	request_init(&request);
	request.u.send.id = id;
	request.u.send.buffer = clone_buffer(buf, &request.u.send.sz);

	send_request(ss, &request, 'P', sizeof(request.u.send));
	return 0;
}

/**
 * 请求 socket 线程退出
 * 
 * 发送 'X' 命令，socket 线程收到后会退出主循环
 */
void
socket_server_exit(struct socket_server *ss) {
	struct request_package request;
	request_init(&request);
	send_request(ss, &request, 'X', 0);
}

/**
 * 关闭 socket
 * 
 * 优雅关闭：等待发送队列清空后再关闭
 * 
 * @param ss     socket 服务器
 * @param opaque 关联服务句柄(用于验证)
 * @param id     socket ID
 */
void
socket_server_close(struct socket_server *ss, uintptr_t opaque, int id) {
	struct request_package request;
	request_init(&request);
	request.u.close.id = id;
	request.u.close.shutdown = 0;
	request.u.close.opaque = opaque;
	send_request(ss, &request, 'K', sizeof(request.u.close));
}


/**
 * 立即关闭 socket
 * 
 * 强制关闭：丢弃发送队列立即关闭
 * 
 * @param ss     socket 服务器
 * @param opaque 关联服务句柄
 * @param id     socket ID
 */
void
socket_server_shutdown(struct socket_server *ss, uintptr_t opaque, int id) {
	struct request_package request;
	request_init(&request);
	request.u.close.id = id;
	request.u.close.shutdown = 1;
	request.u.close.opaque = opaque;
	send_request(ss, &request, 'K', sizeof(request.u.close));
}

/**
 * 创建并绑定 socket
 * 
 * @param host     绑定地址(NULL 或空字符串表示 INADDR_ANY)
 * @param port     绑定端口
 * @param protocol 协议(IPPROTO_TCP 或 IPPROTO_UDP)
 * @param family   输出参数，返回地址族
 * @return 绑定的 fd，失败返回 -1
 */
static int
do_bind(const char *host, int port, int protocol, int *family) {
	int fd;
	int status;
	int reuse = 1;
	struct addrinfo ai_hints;
	struct addrinfo *ai_list = NULL;
	char portstr[16];
	if (host == NULL || host[0] == 0) {
		host = "0.0.0.0";	// INADDR_ANY
	}
	sprintf(portstr, "%d", port);
	memset( &ai_hints, 0, sizeof( ai_hints ) );
	ai_hints.ai_family = AF_UNSPEC;
	if (protocol == IPPROTO_TCP) {
		ai_hints.ai_socktype = SOCK_STREAM;
	} else {
		assert(protocol == IPPROTO_UDP);
		ai_hints.ai_socktype = SOCK_DGRAM;
	}
	ai_hints.ai_protocol = protocol;

	status = getaddrinfo( host, portstr, &ai_hints, &ai_list );
	if ( status != 0 ) {
		return -1;
	}
	*family = ai_list->ai_family;
	fd = socket(*family, ai_list->ai_socktype, 0);
	if (fd < 0) {
		goto _failed_fd;
	}
	if (setsockopt(fd, SOL_SOCKET, SO_REUSEADDR, (void *)&reuse, sizeof(int))==-1) {
		goto _failed;
	}
	status = bind(fd, (struct sockaddr *)ai_list->ai_addr, ai_list->ai_addrlen);
	if (status != 0)
		goto _failed;

	freeaddrinfo( ai_list );
	return fd;
_failed:
	close(fd);
_failed_fd:
	freeaddrinfo( ai_list );
	return -1;
}

/**
 * 创建 TCP 监听 socket
 * 
 * @param host    监听地址
 * @param port    监听端口
 * @param backlog 监听队列长度
 * @return 监听 fd，失败返回 -1
 */
static int
do_listen(const char * host, int port, int backlog) {
	int family = 0;
	int listen_fd = do_bind(host, port, IPPROTO_TCP, &family);
	if (listen_fd < 0) {
		return -1;
	}
	if (listen(listen_fd, backlog) == -1) {
		close(listen_fd);
		return -1;
	}
	return listen_fd;
}

/**
 * 开始监听 TCP 端口
 * 
 * @param ss      socket 服务器
 * @param opaque  关联服务句柄
 * @param addr    监听地址(NULL 表示所有接口)
 * @param port    监听端口
 * @param backlog 监听队列长度
 * @return 监听 socket ID，失败返回 -1
 */
int
socket_server_listen(struct socket_server *ss, uintptr_t opaque, const char * addr, int port, int backlog) {
	int fd = do_listen(addr, port, backlog);
	if (fd < 0) {
		return -1;
	}
	struct request_package request;
	request_init(&request);
	int id = reserve_id(ss);
	if (id < 0) {
		close(fd);
		return id;
	}
	request.u.listen.opaque = opaque;
	request.u.listen.id = id;
	request.u.listen.fd = fd;
	send_request(ss, &request, 'L', sizeof(request.u.listen));
	return id;
}

/**
 * 绑定已存在的文件描述符
 * 
 * 用于将外部创建的 fd(如 stdin/stdout 或其他程序传递的 fd)纳入 socket 服务器管理
 * 
 * @param ss     socket 服务器
 * @param opaque 关联服务句柄
 * @param fd     已存在的文件描述符
 * @return socket ID，失败返回 -1
 */
int
socket_server_bind(struct socket_server *ss, uintptr_t opaque, int fd) {
	struct request_package request;
	request_init(&request);
	int id = reserve_id(ss);
	if (id < 0)
		return -1;
	request.u.bind.opaque = opaque;
	request.u.bind.id = id;
	request.u.bind.fd = fd;
	send_request(ss, &request, 'B', sizeof(request.u.bind));
	return id;
}

/**
 * 启动 socket(恢复数据接收)
 * 
 * 对于 PACCEPT/PLISTEN 状态的 socket，启动后变为 CONNECTED/LISTEN 状态
 * 对于已暂停的 socket，恢复接收数据
 * 
 * @param ss     socket 服务器
 * @param opaque 新的关联服务句柄(可转移 socket 所有权)
 * @param id     socket ID
 */
void
socket_server_start(struct socket_server *ss, uintptr_t opaque, int id) {
	struct request_package request;
	request_init(&request);
	request.u.resumepause.id = id;
	request.u.resumepause.opaque = opaque;
	send_request(ss, &request, 'R', sizeof(request.u.resumepause));
}

/**
 * 暂停 socket(停止数据接收)
 * 
 * 暂停后 socket 不再产生 READ 事件，但可继续发送数据
 * 
 * @param ss     socket 服务器
 * @param opaque 关联服务句柄
 * @param id     socket ID
 */
void
socket_server_pause(struct socket_server *ss, uintptr_t opaque, int id) {
	struct request_package request;
	request_init(&request);
	request.u.resumepause.id = id;
	request.u.resumepause.opaque = opaque;
	send_request(ss, &request, 'S', sizeof(request.u.resumepause));
}

/**
 * 设置 TCP_NODELAY 选项(禁用 Nagle 算法)
 * 
 * 开启后数据会立即发送，不等待合并小包
 * 适用于实时性要求高的场景(如游戏)
 * 
 * @param ss socket 服务器
 * @param id socket ID
 */
void
socket_server_nodelay(struct socket_server *ss, int id) {
	struct request_package request;
	request_init(&request);
	request.u.setopt.id = id;
	request.u.setopt.what = TCP_NODELAY;
	request.u.setopt.value = 1;
	send_request(ss, &request, 'T', sizeof(request.u.setopt));
}

void
socket_server_userobject(struct socket_server *ss, struct socket_object_interface *soi) {
	ss->soi = *soi;
}

// UDP

/**
 * 创建 UDP socket
 * 
 * @param ss   socket 服务器
 * @param opaque 关联服务句柄
 * @param addr 绑定地址(NULL 表示不绑定)
 * @param port 绑定端口(0 表示不绑定)
 * @return UDP socket ID，失败返回 -1
 */
int
socket_server_udp(struct socket_server *ss, uintptr_t opaque, const char * addr, int port) {
	int fd;
	int family;
	if (port != 0 || addr != NULL) {
		// bind
		fd = do_bind(addr, port, IPPROTO_UDP, &family);
		if (fd < 0) {
			return -1;
		}
	} else {
		family = AF_INET;
		fd = socket(family, SOCK_DGRAM, 0);
		if (fd < 0) {
			return -1;
		}
	}
	sp_nonblocking(fd);

	int id = reserve_id(ss);
	if (id < 0) {
		close(fd);
		return -1;
	}
	struct request_package request;
	request_init(&request);
	request.u.udp.id = id;
	request.u.udp.fd = fd;
	request.u.udp.opaque = opaque;
	request.u.udp.family = family;

	send_request(ss, &request, 'U', sizeof(request.u.udp));
	return id;
}

/**
 * 创建 UDP 监听 socket(必须绑定到指定端口)
 * 
 * @param ss   socket 服务器
 * @param opaque 关联服务句柄
 * @param addr 绑定地址
 * @param port 绑定端口(必须 > 0)
 * @return UDP socket ID，失败返回 -1
 */
int
socket_server_udp_listen(struct socket_server *ss, uintptr_t opaque, const char* addr, int port){
	int fd;
	if (port == 0){
		return -1;
	}

	int family;
	// bind
	fd = do_bind(addr, port, IPPROTO_UDP, &family);
	if (fd < 0) {
		return -1;
	}

	sp_nonblocking(fd);

	int id = reserve_id(ss);
	if (id < 0) {
		close(fd);
		return -1;
	}
	struct request_package request;
	request_init(&request);
	request.u.udp.id = id;
	request.u.udp.fd = fd;
	request.u.udp.opaque = opaque;
	request.u.udp.family = family;

	send_request(ss, &request, 'U', sizeof(request.u.udp));
	return id;
}

/**
 * UDP 连接到指定地址
 * 
 * 与 TCP connect 不同，UDP dial 只是设置默认目标地址
 * 数据仍可通过 socket_server_udp_send 发送到其他地址
 * 
 * @param ss   socket 服务器
 * @param opaque 关联服务句柄
 * @param addr 目标地址
 * @param port 目标端口
 * @return UDP socket ID，失败返回 -1
 */
int
socket_server_udp_dial(struct socket_server *ss, uintptr_t opaque, const char* addr, int port){
	int status;
	struct addrinfo ai_hints;
	struct addrinfo *ai_list = NULL;
	char portstr[16];
	sprintf(portstr, "%d", port);
	memset( &ai_hints, 0, sizeof( ai_hints ) );
	ai_hints.ai_family = AF_UNSPEC;
	ai_hints.ai_socktype = SOCK_DGRAM;
	ai_hints.ai_protocol = IPPROTO_UDP;


	status = getaddrinfo(addr, portstr, &ai_hints, &ai_list );
	if ( status != 0 ) {
		return -1;
	}

	int protocol;

	if (ai_list->ai_family == AF_INET) {
		protocol = PROTOCOL_UDP;
	} else if (ai_list->ai_family == AF_INET6) {
		protocol = PROTOCOL_UDPv6;
	} else {
		freeaddrinfo( ai_list );
		return -1;
	}

	int fd = socket(ai_list->ai_family, SOCK_DGRAM, 0);
	if (fd < 0){
		return -1;
	}

	sp_nonblocking(fd);
	int id = reserve_id(ss);
	if (id < 0){
		close(fd);
		return -1;
	}

	struct request_package request;
	request_init(&request);
	request.u.dial_udp.id = id;
	request.u.dial_udp.fd = fd;
	request.u.dial_udp.opaque = opaque;


	int addrsz = gen_udp_address(protocol, (union sockaddr_all *)ai_list->ai_addr, request.u.dial_udp.address);

	freeaddrinfo( ai_list );

	send_request(ss, &request, 'N', sizeof(request.u.dial_udp) - sizeof(request.u.dial_udp.address) + addrsz);
	return id;
}

/**
 * 发送 UDP 数据包到指定地址
 * 
 * @param ss   socket 服务器
 * @param addr 目标地址(包含在 msg 数据末尾，或通过 udp_address 指定)
 * @param buf  发送缓冲区
 * @return 0  - 成功
 * @return -1 - 失败
 */
int
socket_server_udp_send(struct socket_server *ss, const struct socket_udp_address *addr, struct socket_sendbuffer *buf) {
	int id = buf->id;
	struct socket * s = &ss->slot[HASH_ID(id)];
	if (socket_invalid(s, id)) {
		free_buffer(ss, buf);
		return -1;
	}

	const uint8_t *udp_address = (const uint8_t *)addr;
	int addrsz;
	switch (udp_address[0]) {
	case PROTOCOL_UDP:
		addrsz = 1+2+4;		// 1 type, 2 port, 4 ipv4
		break;
	case PROTOCOL_UDPv6:
		addrsz = 1+2+16;	// 1 type, 2 port, 16 ipv6
		break;
	default:
		free_buffer(ss, buf);
		return -1;
	}

	struct socket_lock l;
	socket_lock_init(s, &l);

	if (can_direct_write(s,id) && socket_trylock(&l)) {
		// may be we can send directly, double check
		if (can_direct_write(s,id)) {
			// send directly
			struct send_object so;
			send_object_init_from_sendbuffer(ss, &so, buf);
			union sockaddr_all sa;
			socklen_t sasz = udp_socket_address(s, udp_address, &sa);
			if (sasz == 0) {
				socket_unlock(&l);
				so.free_func((void *)buf->buffer);
				return -1;
			}
			int n = sendto(s->fd, so.buffer, so.sz, 0, &sa.s, sasz);
			if (n >= 0) {
				// sendto succ
				stat_write(ss,s,n);
				socket_unlock(&l);
				so.free_func((void *)buf->buffer);
				return 0;
			}
		}
		socket_unlock(&l);
		// let socket thread try again, udp doesn't care the order
	}

	struct request_package request;
	request_init(&request);
	request.u.send_udp.send.id = id;
	request.u.send_udp.send.buffer = clone_buffer(buf, &request.u.send_udp.send.sz);

	memcpy(request.u.send_udp.address, udp_address, addrsz);

	send_request(ss, &request, 'A', sizeof(request.u.send_udp.send)+addrsz);
	return 0;
}

/**
 * 为 UDP socket 设置默认连接地址
 * 
 * 设置后，socket_server_send 可直接发送数据到该地址
 * 无需再指定目标地址
 * 
 * @param ss   socket 服务器
 * @param id   UDP socket ID
 * @param addr 目标地址
 * @param port 目标端口
 * @return 0  - 成功
 * @return -1 - 失败
 */
int
socket_server_udp_connect(struct socket_server *ss, int id, const char * addr, int port) {
	struct socket * s = &ss->slot[HASH_ID(id)];
	if (socket_invalid(s, id)) {
		return -1;
	}
	struct socket_lock l;
	socket_lock_init(s, &l);
	socket_lock(&l);
	if (socket_invalid(s, id)) {
		socket_unlock(&l);
		return -1;
	}
	ATOM_FINC(&s->udpconnecting);
	socket_unlock(&l);

	int status;
	struct addrinfo ai_hints;
	struct addrinfo *ai_list = NULL;
	char portstr[16];
	sprintf(portstr, "%d", port);
	memset( &ai_hints, 0, sizeof( ai_hints ) );
	ai_hints.ai_family = AF_UNSPEC;
	ai_hints.ai_socktype = SOCK_DGRAM;
	ai_hints.ai_protocol = IPPROTO_UDP;

	status = getaddrinfo(addr, portstr, &ai_hints, &ai_list );
	if ( status != 0 ) {
		return -1;
	}
	struct request_package request;
	request_init(&request);
	request.u.set_udp.id = id;
	int protocol;

	if (ai_list->ai_family == AF_INET) {
		protocol = PROTOCOL_UDP;
	} else if (ai_list->ai_family == AF_INET6) {
		protocol = PROTOCOL_UDPv6;
	} else {
		freeaddrinfo( ai_list );
		return -1;
	}

	int addrsz = gen_udp_address(protocol, (union sockaddr_all *)ai_list->ai_addr, request.u.set_udp.address);

	freeaddrinfo( ai_list );

	send_request(ss, &request, 'C', sizeof(request.u.set_udp) - sizeof(request.u.set_udp.address) +addrsz);

	return 0;
}

/**
 * 从 UDP 消息中提取发送方地址
 * 
 * UDP 消息的数据格式：数据 + 地址信息(附加在末尾)
 * 此函数返回地址信息的指针
 * 
 * @param ss     socket 服务器
 * @param msg    UDP 消息(SOCKET_UDP 类型)
 * @param addrsz 输出参数，地址结构大小
 * @return 地址结构指针，失败返回 NULL
 */
const struct socket_udp_address *
socket_server_udp_address(struct socket_server *ss, struct socket_message *msg, int *addrsz) {
	uint8_t * address = (uint8_t *)(msg->data + msg->ud);
	int type = address[0];
	switch(type) {
	case PROTOCOL_UDP:
		*addrsz = 1+2+4;
		break;
	case PROTOCOL_UDPv6:
		*addrsz = 1+2+16;
		break;
	default:
		return NULL;
	}
	return (const struct socket_udp_address *)address;
}


/**
 * 创建 socket 信息节点
 * 
 * 用于构建 socket 信息链表
 * 
 * @param last 上一个节点
 * @return 新节点
 */
struct socket_info *
socket_info_create(struct socket_info *last) {
	struct socket_info *si = skynet_malloc(sizeof(*si));
	memset(si, 0 , sizeof(*si));
	si->next = last;
	return si;
}

/**
 * 释放 socket 信息链表
 * 
 * @param si 链表头
 */
void
socket_info_release(struct socket_info *si) {
	while (si) {
		struct socket_info *temp = si;
		si = si->next;
		skynet_free(temp);
	}
}

static int
query_info(struct socket *s, struct socket_info *si) {
	union sockaddr_all u;
	socklen_t slen = sizeof(u);
	int closing = 0;
	switch (ATOM_LOAD(&s->type)) {
	case SOCKET_TYPE_BIND:
		si->type = SOCKET_INFO_BIND;
		si->name[0] = '\0';
		break;
	case SOCKET_TYPE_LISTEN:
		si->type = SOCKET_INFO_LISTEN;
		if (getsockname(s->fd, &u.s, &slen) == 0) {
			getname(&u, si->name, sizeof(si->name));
		}
		break;
	case SOCKET_TYPE_HALFCLOSE_READ:
	case SOCKET_TYPE_HALFCLOSE_WRITE:
		closing = 1;
	case SOCKET_TYPE_CONNECTED:
		if (s->protocol == PROTOCOL_TCP) {
			si->type = closing ? SOCKET_INFO_CLOSING : SOCKET_INFO_TCP;
			if (getpeername(s->fd, &u.s, &slen) == 0) {
				getname(&u, si->name, sizeof(si->name));
			}
		} else {
			si->type = SOCKET_INFO_UDP;
			if (udp_socket_address(s, s->p.udp_address, &u)) {
				getname(&u, si->name, sizeof(si->name));
			}
		}
		break;
	default:
		return 0;
	}
	si->id = s->id;
	si->opaque = (uint64_t)s->opaque;
	si->read = s->stat.read;
	si->write = s->stat.write;
	si->rtime = s->stat.rtime;
	si->wtime = s->stat.wtime;
	si->wbuffer = s->wb_size;
	si->reading = s->reading;
	si->writing = s->writing;

	return 1;
}

/**
 * 获取所有 socket 的状态信息
 * 
 * 用于监控和调试，返回所有有效 socket 的信息链表
 * 
 * @param ss socket 服务器
 * @return socket 信息链表头
 */
struct socket_info *
socket_server_info(struct socket_server *ss) {
	int i;
	struct socket_info * si = NULL;
	for (i=0;i<MAX_SOCKET;i++) {
		struct socket * s = &ss->slot[i];
		int id = s->id;
		struct socket_info temp;
		if (query_info(s, &temp) && s->id == id) {
			// socket_server_info may call in different thread, so check socket id again
			si = socket_info_create(si);
			temp.next = si->next;
			*si = temp;
		}
	}
	return si;
}
