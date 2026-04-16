#ifndef skynet_socket_server_h
#define skynet_socket_server_h

#include <stdint.h>
#include "socket_info.h"
#include "socket_buffer.h"

#define SOCKET_DATA 0
#define SOCKET_CLOSE 1
#define SOCKET_OPEN 2
#define SOCKET_ACCEPT 3
#define SOCKET_ERR 4
#define SOCKET_EXIT 5
#define SOCKET_UDP 6
#define SOCKET_WARNING 7

// Only for internal use
#define SOCKET_RST 8
#define SOCKET_MORE 9

struct socket_server;

/**
 * Socket 事件消息结构体
 * 
 * 用于 socket_server_poll() 返回各种网络事件(数据到达、连接建立、断开等)
 * 该结构体由 socket_server 填充，调用者负责处理其中的数据
 * 
 * 各字段含义根据事件类型(type)不同而变化：
 * 
 * | 事件类型      | id       | opaque       | ud                    | data              |
 * |--------------|----------|--------------|----------------------|-------------------|
 * | SOCKET_DATA  | socket id| 关联服务句柄  | 数据字节数            | 数据缓冲区指针     |
 * | SOCKET_CLOSE | socket id| 关联服务句柄  | 0                    | NULL              |
 * | SOCKET_OPEN  | socket id| 关联服务句柄  | 0                    | 对端地址字符串     |
 * | SOCKET_ACCEPT| listen id| 关联服务句柄  | 新连接 id             | 客户端地址字符串   |
 * | SOCKET_ERR   | socket id| 关联服务句柄  | 0                    | 错误信息字符串     |
 * | SOCKET_UDP   | socket id| 关联服务句柄  | 数据字节数            | 数据+地址(附加末尾)|
 * | SOCKET_WARN  | socket id| 关联服务句柄  | 缓冲区大小(KB)        | NULL              |
 */
struct socket_message {
	int id;              // socket id (创建时分配的唯一标识)
	uintptr_t opaque;    // 关联的服务句柄 (创建 socket 时传入，用于消息路由)
	int ud;              // 附加数据：accept时为连接id，data时为数据大小，warning时为KB数
	char * data;         // 数据指针或字符串指针 (需要调用者释放，或使用后丢弃)
};

struct socket_server * socket_server_create(uint64_t time);
void socket_server_release(struct socket_server *);
void socket_server_updatetime(struct socket_server *, uint64_t time);
int socket_server_poll(struct socket_server *, struct socket_message *result, int *more);

void socket_server_exit(struct socket_server *);
void socket_server_close(struct socket_server *, uintptr_t opaque, int id);
void socket_server_shutdown(struct socket_server *, uintptr_t opaque, int id);
void socket_server_start(struct socket_server *, uintptr_t opaque, int id);
void socket_server_pause(struct socket_server *, uintptr_t opaque, int id);

// return -1 when error
int socket_server_send(struct socket_server *, struct socket_sendbuffer *buffer);
int socket_server_send_lowpriority(struct socket_server *, struct socket_sendbuffer *buffer);

// ctrl command below returns id
int socket_server_listen(struct socket_server *, uintptr_t opaque, const char * addr, int port, int backlog);
int socket_server_connect(struct socket_server *, uintptr_t opaque, const char * addr, int port);
int socket_server_bind(struct socket_server *, uintptr_t opaque, int fd);

// for tcp
void socket_server_nodelay(struct socket_server *, int id);

struct socket_udp_address;

// create an udp socket handle, attach opaque with it . udp socket don't need call socket_server_start to recv message
// if port != 0, bind the socket . if addr == NULL, bind ipv4 0.0.0.0 . If you want to use ipv6, addr can be "::" and port 0.
int socket_server_udp(struct socket_server *, uintptr_t opaque, const char * addr, int port);
// set default dest address, return 0 when success
int socket_server_udp_connect(struct socket_server *, int id, const char * addr, int port);

// create an udp client socket handle, and connect to server addr, return id when success
int socket_server_udp_dial(struct socket_server *ss, uintptr_t opaque, const char* addr, int port);
// create an udp server socket handle, and bind the host port, return id when success
int socket_server_udp_listen(struct socket_server *ss, uintptr_t opaque, const char* addr, int port);

// If the socket_udp_address is NULL, use last call socket_server_udp_connect address instead
// You can also use socket_server_send 
int socket_server_udp_send(struct socket_server *, const struct socket_udp_address *, struct socket_sendbuffer *buffer);
// extract the address of the message, struct socket_message * should be SOCKET_UDP
const struct socket_udp_address * socket_server_udp_address(struct socket_server *, struct socket_message *, int *addrsz);

struct socket_object_interface {
	const void * (*buffer)(const void *);
	size_t (*size)(const void *);
	void (*free)(void *);
};

// if you send package with type SOCKET_BUFFER_OBJECT, use soi.
void socket_server_userobject(struct socket_server *, struct socket_object_interface *soi);

struct socket_info * socket_server_info(struct socket_server *);

#endif
