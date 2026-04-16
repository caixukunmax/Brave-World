/**
 * ============================================================================
 * Skynet Socket 封装模块 - skynet_socket.c
 * ============================================================================
 * 
 * 【文件作用】
 * 封装底层的 socket_server，提供与 Skynet 框架集成的 socket API
 * 
 * 【职责】
 * 1. 将底层 socket 事件转换为 Skynet 消息发送给对应的服务
 * 2. 提供 socket 操作接口给 Lua 层使用
 * 3. 管理服务与 socket 的关联（opaque 字段）
 * 
 * 【消息类型】
 * 所有 socket 事件都通过 PTYPE_SOCKET 类型的消息发送给服务：
 * - DATA:     收到数据
 * - CLOSE:    连接关闭
 * - CONNECT:  连接建立
 * - ERROR:    发生错误
 * - ACCEPT:   接受新连接
 * - UDP:      UDP 数据
 * - WARNING:  发送缓冲区警告
 * 
 * 【线程模型】
 * socket_server 在独立的 socket 线程中运行
 * skynet_socket_poll 在 socket 线程的主循环中被调用
 * ============================================================================
 */

#include "skynet.h"

#include "skynet_socket.h"
#include "socket_server.h"
#include "skynet_server.h"
#include "skynet_mq.h"
#include "skynet_harbor.h"

#include <assert.h>
#include <stdlib.h>
#include <string.h>
#include <stdbool.h>

// 全局 socket_server 实例
static struct socket_server * SOCKET_SERVER = NULL;

/**
 * 【接口】初始化 socket 系统
 */
void 
skynet_socket_init() {
	SOCKET_SERVER = socket_server_create(skynet_now());
}

/**
 * 【接口】通知 socket_server 退出
 */
void
skynet_socket_exit() {
	socket_server_exit(SOCKET_SERVER);
}

/**
 * 【接口】释放 socket_server 资源
 */
void
skynet_socket_free() {
	socket_server_release(SOCKET_SERVER);
	SOCKET_SERVER = NULL;
}

/**
 * 【接口】更新 socket_server 时间
 * 
 * 用于超时检测，由定时器线程定期调用
 */
void
skynet_socket_updatetime() {
	socket_server_updatetime(SOCKET_SERVER, skynet_now());
}

/**
 * 【内部】转发 socket 消息给服务
 * 
 * 将底层的 socket_message 转换为 skynet_socket_message
 * 并作为 PTYPE_SOCKET 消息发送给对应的服务
 * 
 * @param type     消息类型
 * @param padding  是否将数据内联到消息中
 * @param result   底层 socket 消息
 */
static void
forward_message(int type, bool padding, struct socket_message * result) {
	struct skynet_socket_message *sm;
	size_t sz = sizeof(*sm);
	if (padding) {
		// 将数据内联到消息中（适用于短数据，如连接结果）
		if (result->data) {
			size_t msg_sz = strlen(result->data);
			if (msg_sz > 128) {
				msg_sz = 128;
			}
			sz += msg_sz;
		} else {
			result->data = "";
		}
	}
	sm = (struct skynet_socket_message *)skynet_malloc(sz);
	sm->type = type;
	sm->id = result->id;       // socket id
	sm->ud = result->ud;       // 附加数据（如数据长度）
	if (padding) {
		sm->buffer = NULL;
		memcpy(sm+1, result->data, sz - sizeof(*sm));
	} else {
		sm->buffer = result->data;  // 数据指针（需要接收者释放）
	}

	struct skynet_message message;
	message.source = 0;
	message.session = 0;
	message.data = sm;
	message.sz = sz | ((size_t)PTYPE_SOCKET << MESSAGE_TYPE_SHIFT);
	
	// 发送给创建这个 socket 的服务
	if (skynet_context_push((uint32_t)result->opaque, &message)) {
		// 服务已退出，清理资源
		// 注意：这里不能调用 skynet_socket_close，会阻塞主循环
		skynet_free(sm->buffer);
		skynet_free(sm);
	}
}

/**
 * 【接口】轮询 socket 事件（socket 线程主循环调用）
 * 
 * @return  0 退出，1 正常，-1 有事件需要继续处理
 */
int 
skynet_socket_poll() {
	struct socket_server *ss = SOCKET_SERVER;
	assert(ss);
	struct socket_message result;
	int more = 1;
	int type = socket_server_poll(ss, &result, &more);
	switch (type) {
	case SOCKET_EXIT:
		return 0;
	case SOCKET_DATA:
		forward_message(SKYNET_SOCKET_TYPE_DATA, false, &result);
		break;
	case SOCKET_CLOSE:
		forward_message(SKYNET_SOCKET_TYPE_CLOSE, false, &result);
		break;
	case SOCKET_OPEN:
		forward_message(SKYNET_SOCKET_TYPE_CONNECT, true, &result);
		break;
	case SOCKET_ERR:
		forward_message(SKYNET_SOCKET_TYPE_ERROR, true, &result);
		break;
	case SOCKET_ACCEPT:
		forward_message(SKYNET_SOCKET_TYPE_ACCEPT, true, &result);
		break;
	case SOCKET_UDP:
		forward_message(SKYNET_SOCKET_TYPE_UDP, false, &result);
		break;
	case SOCKET_WARNING:
		forward_message(SKYNET_SOCKET_TYPE_WARNING, false, &result);
		break;
	default:
		skynet_error(NULL, "error: Unknown socket message type %d.",type);
		return -1;
	}
	if (more) {
		return -1;  // 还有更多事件需要处理
	}
	return 1;
}

/**
 * 【接口】发送数据
 */
int
skynet_socket_sendbuffer(struct skynet_context *ctx, struct socket_sendbuffer *buffer) {
	return socket_server_send(SOCKET_SERVER, buffer);
}

/**
 * 【接口】发送数据（低优先级）
 */
int
skynet_socket_sendbuffer_lowpriority(struct skynet_context *ctx, struct socket_sendbuffer *buffer) {
	return socket_server_send_lowpriority(SOCKET_SERVER, buffer);
}

/**
 * 【接口】监听端口
 */
int 
skynet_socket_listen(struct skynet_context *ctx, const char *host, int port, int backlog) {
	uint32_t source = skynet_context_handle(ctx);
	return socket_server_listen(SOCKET_SERVER, source, host, port, backlog);
}

/**
 * 【接口】连接远程服务器
 */
int 
skynet_socket_connect(struct skynet_context *ctx, const char *host, int port) {
	uint32_t source = skynet_context_handle(ctx);
	return socket_server_connect(SOCKET_SERVER, source, host, port);
}

/**
 * 【接口】绑定已存在的文件描述符
 */
int 
skynet_socket_bind(struct skynet_context *ctx, int fd) {
	uint32_t source = skynet_context_handle(ctx);
	return socket_server_bind(SOCKET_SERVER, source, fd);
}

/**
 * 【接口】关闭 socket
 */
void 
skynet_socket_close(struct skynet_context *ctx, int id) {
	uint32_t source = skynet_context_handle(ctx);
	socket_server_close(SOCKET_SERVER, source, id);
}

/**
 * 【接口】关闭 socket 写端（半关闭）
 */
void 
skynet_socket_shutdown(struct skynet_context *ctx, int id) {
	uint32_t source = skynet_context_handle(ctx);
	socket_server_shutdown(SOCKET_SERVER, source, id);
}

/**
 * 【接口】开始接收数据
 * 
 * 对于 listen socket，调用 start 后才开始接受连接
 * 对于 connect socket，调用 start 后才开始接收数据
 */
void 
skynet_socket_start(struct skynet_context *ctx, int id) {
	uint32_t source = skynet_context_handle(ctx);
	socket_server_start(SOCKET_SERVER, source, id);
}

/**
 * 【接口】暂停接收数据
 */
void
skynet_socket_pause(struct skynet_context *ctx, int id) {
	uint32_t source = skynet_context_handle(ctx);
	socket_server_pause(SOCKET_SERVER, source, id);
}

/**
 * 【接口】设置 TCP_NODELAY
 */
void
skynet_socket_nodelay(struct skynet_context *ctx, int id) {
	socket_server_nodelay(SOCKET_SERVER, id);
}

/**
 * 【接口】创建 UDP socket
 */
int 
skynet_socket_udp(struct skynet_context *ctx, const char * addr, int port) {
	uint32_t source = skynet_context_handle(ctx);
	return socket_server_udp(SOCKET_SERVER, source, addr, port);
}

/**
 * 【接口】创建 UDP socket 并连接到远程地址
 */
int
skynet_socket_udp_dial(struct skynet_context *ctx, const char * addr, int port){
	uint32_t source = skynet_context_handle(ctx);
	return socket_server_udp_dial(SOCKET_SERVER, source, addr, port);
}

/**
 * 【接口】创建 UDP socket 并绑定地址
 */
int
skynet_socket_udp_listen(struct skynet_context *ctx, const char * addr, int port){
	uint32_t source = skynet_context_handle(ctx);
	return socket_server_udp_listen(SOCKET_SERVER, source, addr, port);
}

/**
 * 【接口】设置 UDP 连接地址
 */
int 
skynet_socket_udp_connect(struct skynet_context *ctx, int id, const char * addr, int port) {
	return socket_server_udp_connect(SOCKET_SERVER, id, addr, port);
}

/**
 * 【接口】发送 UDP 数据
 */
int 
skynet_socket_udp_sendbuffer(struct skynet_context *ctx, const char * address, struct socket_sendbuffer *buffer) {
	return socket_server_udp_send(SOCKET_SERVER, (const struct socket_udp_address *)address, buffer);
}

/**
 * 【接口】获取 UDP 消息的发送地址
 */
const char *
skynet_socket_udp_address(struct skynet_socket_message *msg, int *addrsz) {
	if (msg->type != SKYNET_SOCKET_TYPE_UDP) {
		return NULL;
	}
	struct socket_message sm;
	sm.id = msg->id;
	sm.opaque = 0;
	sm.ud = msg->ud;
	sm.data = msg->buffer;
	return (const char *)socket_server_udp_address(SOCKET_SERVER, &sm, addrsz);
}

/**
 * 【接口】获取 socket 信息列表
 */
struct socket_info *
skynet_socket_info() {
	return socket_server_info(SOCKET_SERVER);
}
