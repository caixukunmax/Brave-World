/**
 * ============================================================================
 * Skynet Gate 服务 - C 实现 (service_gate.c)
 * ============================================================================
 * 
 * 【文件作用】
 * 这是 Skynet 的 C 语言版 Gate 服务，用于网络接入层：
 * - 监听 TCP 端口，接受客户端连接
 * - 分包处理（支持 2字节或4字节长度头协议）
 * - 将客户端数据转发给 Agent 或 Watchdog 服务
 * - 管理连接生命周期
 * 
 * 【与 Lua Gate 的区别】
 * - 这是 C 模块，性能更高，延迟更低
 * - 分包逻辑在 C 层完成，不需要 netpack
 * - 通常用于需要极致性能的场景
 * 
 * 【协议格式】
 * 支持两种分包模式（由初始化参数指定）：
 * - 'S' 模式：2字节大端长度头，最大 64KB
 * - 'L' 模式：4字节大端长度头，最大 4GB
 * 
 * 【消息流向】
 * 客户端 -> Gate -> Agent/Watchdog
 *           |
 *           -> 分包后转发完整包
 * ============================================================================
 */

#include "skynet.h"
#include "skynet_socket.h"
#include "databuffer.h"
#include "hashid.h"

#include <stdlib.h>
#include <string.h>
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <stdarg.h>

#define BACKLOG 128  // TCP 监听队列长度

/**
 * 【数据结构】客户端连接
 * 
 * 每个 TCP 连接对应一个 connection 结构体
 */
struct connection {
	int id;				// skynet_socket 分配的 fd
	uint32_t agent;		// 绑定的 Agent 服务句柄（转发目标）
	uint32_t client;	// 客户端标识（用于消息路由）
	char remote_name[32]; // 远程地址字符串（如 "192.168.1.1:12345"）
	struct databuffer buffer; // 接收缓冲区（处理粘包/分包）
};

/**
 * 【数据结构】Gate 服务实例
 * 
 * 管理所有连接和配置
 */
struct gate {
	struct skynet_context *ctx;  // Skynet 上下文
	int listen_id;				 // 监听 socket 的 id
	uint32_t watchdog;			 // Watchdog 服务句柄（新连接/断开通知）
	uint32_t broker;			 // Broker 服务句柄（广播模式）
	int client_tag;				 // 消息类型标签（PTYPE_CLIENT 或自定义）
	int header_size;			 // 长度头大小（2 或 4）
	int max_connection;			 // 最大连接数
	struct hashid hash;			 // fd -> connection 索引的哈希表
	struct connection *conn;	 // 连接数组
	struct messagepool mp;		 // 消息内存池（用于 databuffer）
};

/**
 * 【接口】创建 Gate 实例
 */
struct gate *
gate_create(void) {
	struct gate * g = skynet_malloc(sizeof(*g));
	memset(g,0,sizeof(*g));
	g->listen_id = -1;  // -1 表示未监听
	return g;
}

/**
 * 【接口】释放 Gate 实例
 * 
 * 关闭所有连接和监听 socket，释放资源
 */
void
gate_release(struct gate *g) {
	int i;
	struct skynet_context *ctx = g->ctx;
	// 关闭所有客户端连接
	for (i=0;i<g->max_connection;i++) {
		struct connection *c = &g->conn[i];
		if (c->id >=0) {
			skynet_socket_close(ctx, c->id);
		}
	}
	// 关闭监听 socket
	if (g->listen_id >= 0) {
		skynet_socket_close(ctx, g->listen_id);
	}
	// 释放资源
	messagepool_free(&g->mp);
	hashid_clear(&g->hash);
	skynet_free(g->conn);
	skynet_free(g);
}

/**
 * 【内部】提取命令参数
 * 
 * 从 "kick 123" 中提取 "123"
 */
static void
_parm(char *msg, int sz, int command_sz) {
	// 跳过空格
	while (command_sz < sz) {
		if (msg[command_sz] != ' ')
			break;
		++command_sz;
	}
	// 移动参数到头部
	int i;
	for (i=command_sz;i<sz;i++) {
		msg[i-command_sz] = msg[i];
	}
	msg[i-command_sz] = '\0';
}

/**
 * 【内部】绑定 Agent 到连接
 * 
 * 将某个 fd 的后续消息转发给指定 Agent
 */
static void
_forward_agent(struct gate * g, int fd, uint32_t agentaddr, uint32_t clientaddr) {
	int id = hashid_lookup(&g->hash, fd);
	if (id >=0) {
		struct connection * agent = &g->conn[id];
		agent->agent = agentaddr;
		agent->client = clientaddr;
	}
}

/**
 * 【内部】处理控制命令
 * 
 * 支持的命令：
 * - kick <fd>:      断开指定连接
 * - forward <fd> <agent> <client>: 绑定 fd 到 agent
 * - broker <name>:  设置广播服务
 * - start <fd>:     开始接收数据（accept 后调用）
 * - close:          关闭监听
 */
static void
_ctrl(struct gate * g, const void * msg, int sz) {
	struct skynet_context * ctx = g->ctx;
	char tmp[sz+1];
	memcpy(tmp, msg, sz);
	tmp[sz] = '\0';
	char * command = tmp;
	int i;
	if (sz == 0)
		return;
	
	// 找到命令和参数的分隔（空格）
	for (i=0;i<sz;i++) {
		if (command[i]==' ') {
			break;
		}
	}
	
	// kick 命令：断开连接
	if (memcmp(command,"kick",i)==0) {
		_parm(tmp, sz, i);
		int uid = strtol(command , NULL, 10);
		int id = hashid_lookup(&g->hash, uid);
		if (id>=0) {
			skynet_socket_close(ctx, uid);
		}
		return;
	}
	
	// forward 命令：绑定到 Agent
	if (memcmp(command,"forward",i)==0) {
		_parm(tmp, sz, i);
		char * client = tmp;
		char * idstr = strsep(&client, " ");
		if (client == NULL) {
			return;
		}
		int id = strtol(idstr , NULL, 10);
		char * agent = strsep(&client, " ");
		if (client == NULL) {
			return;
		}
		uint32_t agent_handle = strtoul(agent+1, NULL, 16);
		uint32_t client_handle = strtoul(client+1, NULL, 16);
		_forward_agent(g, id, agent_handle, client_handle);
		return;
	}
	
	// broker 命令：设置广播服务
	if (memcmp(command,"broker",i)==0) {
		_parm(tmp, sz, i);
		g->broker = skynet_queryname(ctx, command);
		return;
	}
	
	// start 命令：开始接收数据
	if (memcmp(command,"start",i) == 0) {
		_parm(tmp, sz, i);
		int uid = strtol(command , NULL, 10);
		int id = hashid_lookup(&g->hash, uid);
		if (id>=0) {
			skynet_socket_start(ctx, uid);
		}
		return;
	}
	
	// close 命令：关闭监听
	if (memcmp(command, "close", i) == 0) {
		if (g->listen_id >= 0) {
			skynet_socket_close(ctx, g->listen_id);
			g->listen_id = -1;
		}
		return;
	}
	
	skynet_error(ctx, "[gate] Unknown command : %s", command);
}

/**
 * 【内部】发送报告给 Watchdog
 * 
 * 连接打开/关闭时通知 Watchdog
 */
static void
_report(struct gate * g, const char * data, ...) {
	if (g->watchdog == 0) {
		return;
	}
	struct skynet_context * ctx = g->ctx;
	va_list ap;
	va_start(ap, data);
	char tmp[1024];
	int n = vsnprintf(tmp, sizeof(tmp), data, ap);
	va_end(ap);

	skynet_send(ctx, 0, g->watchdog, PTYPE_TEXT,  0, tmp, n);
}

/**
 * 【内部】转发完整数据包
 * 
 * 分包完成后，将完整包转发给 Agent 或 Watchdog
 */
static void
_forward(struct gate *g, struct connection * c, int size) {
	struct skynet_context * ctx = g->ctx;
	int fd = c->id;
	if (fd <= 0) {
		return;
	}
	
	// 优先转发给 Broker（广播模式）
	if (g->broker) {
		void * temp = skynet_malloc(size);
		databuffer_read(&c->buffer,&g->mp,(char *)temp, size);
		skynet_send(ctx, 0, g->broker, g->client_tag | PTYPE_TAG_DONTCOPY, fd, temp, size);
		return;
	}
	
	// 转发给绑定的 Agent
	if (c->agent) {
		void * temp = skynet_malloc(size);
		databuffer_read(&c->buffer,&g->mp,(char *)temp, size);
		skynet_send(ctx, c->client, c->agent, g->client_tag | PTYPE_TAG_DONTCOPY, fd , temp, size);
	} 
	// 没有 Agent，发给 Watchdog
	else if (g->watchdog) {
		char * tmp = skynet_malloc(size + 32);
		int n = snprintf(tmp,32,"%d data ",c->id);
		databuffer_read(&c->buffer,&g->mp,tmp+n,size);
		skynet_send(ctx, 0, g->watchdog, PTYPE_TEXT | PTYPE_TAG_DONTCOPY, fd, tmp, size + n);
	}
}

/**
 * 【内部】处理收到的网络数据
 * 
 * 核心分包逻辑：
 * 1. 数据追加到缓冲区
 * 2. 尝试读取长度头
 * 3. 如果包完整，转发给业务层
 * 4. 循环处理，直到缓冲区数据不足一个包
 */
static void
dispatch_message(struct gate *g, struct connection *c, int id, void * data, int sz) {
	// 追加数据到接收缓冲区
	databuffer_push(&c->buffer,&g->mp, data, sz);
	
	// 循环处理，可能一次收到多个完整包
	for (;;) {
		// 读取长度头，返回包体大小（不含长度头）
		int size = databuffer_readheader(&c->buffer, &g->mp, g->header_size);
		
		if (size < 0) {
			// 数据不足，等待更多数据
			return;
		} else if (size > 0) {
			// 包太大（> 16MB），认为是攻击，断开连接
			if (size >= 0x1000000) {
				struct skynet_context * ctx = g->ctx;
				databuffer_clear(&c->buffer,&g->mp);
				skynet_socket_close(ctx, id);
				skynet_error(ctx, "Recv socket message > 16M");
				return;
			} else {
				// 转发完整包
				_forward(g, c, size);
				databuffer_reset(&c->buffer);
			}
		}
	}
}

/**
 * 【内部】处理 Socket 事件
 * 
 * 来自 skynet_socket 的消息（DATA/CONNECT/CLOSE/ACCEPT等）
 */
static void
dispatch_socket_message(struct gate *g, const struct skynet_socket_message * message, int sz) {
	struct skynet_context * ctx = g->ctx;
	switch(message->type) {
		
	// 收到数据
	case SKYNET_SOCKET_TYPE_DATA: {
		int id = hashid_lookup(&g->hash, message->id);
		if (id>=0) {
			struct connection *c = &g->conn[id];
			dispatch_message(g, c, message->id, message->buffer, message->ud);
		} else {
			// 未知连接，直接关闭
			skynet_error(ctx, "Drop unknown connection %d message", message->id);
			skynet_socket_close(ctx, message->id);
			skynet_free(message->buffer);
		}
		break;
	}
	
	// 连接建立（包括 accept 后的新连接）
	case SKYNET_SOCKET_TYPE_CONNECT: {
		if (message->id == g->listen_id) {
			// 监听 socket 的连接事件（忽略）
			break;
		}
		int id = hashid_lookup(&g->hash, message->id);
		if (id<0) {
			skynet_error(ctx, "Close unknown connection %d", message->id);
			skynet_socket_close(ctx, message->id);
		}
		break;
	}
	
	// 连接关闭或出错
	case SKYNET_SOCKET_TYPE_CLOSE:
	case SKYNET_SOCKET_TYPE_ERROR: {
		int id = hashid_remove(&g->hash, message->id);
		if (id>=0) {
			struct connection *c = &g->conn[id];
			databuffer_clear(&c->buffer,&g->mp);
			memset(c, 0, sizeof(*c));
			c->id = -1;
			_report(g, "%d close", message->id);
			skynet_socket_close(ctx, message->id);
		}
		break;
	}
	
	// 新连接接入（accept）
	case SKYNET_SOCKET_TYPE_ACCEPT:
		assert(g->listen_id == message->id);
		if (hashid_full(&g->hash)) {
			// 连接数已满，拒绝连接
			skynet_socket_close(ctx, message->ud);
		} else {
			// 分配 connection 结构体
			struct connection *c = &g->conn[hashid_insert(&g->hash, message->ud)];
			if (sz >= sizeof(c->remote_name)) {
				sz = sizeof(c->remote_name) - 1;
			}
			c->id = message->ud;
			memcpy(c->remote_name, message+1, sz);
			c->remote_name[sz] = '\0';
			_report(g, "%d open %d %s:0",c->id, c->id, c->remote_name);
			skynet_error(ctx, "socket open: %x", c->id);
		}
		break;
		
	// 发送缓冲区警告
	case SKYNET_SOCKET_TYPE_WARNING:
		skynet_error(ctx, "fd (%d) send buffer (%d)K", message->id, message->ud);
		break;
	}
}

/**
 * 【回调】消息处理入口
 * 
 * 接收来自其他服务的消息
 */
static int
_cb(struct skynet_context * ctx, void * ud, int type, int session, uint32_t source, const void * msg, size_t sz) {
	struct gate *g = ud;
	switch(type) {
		
	// 文本命令（如 kick/forward/broker/start/close）
	case PTYPE_TEXT:
		_ctrl(g , msg , (int)sz);
		break;
		
	// 客户端消息（需要发给客户端）
	case PTYPE_CLIENT: {
		if (sz <=4 ) {
			skynet_error(ctx, "Invalid client message from %x",source);
			break;
		}
		// 消息最后4字节是 socket id
		const uint8_t * idbuf = msg + sz - 4;
		uint32_t uid = idbuf[0] | idbuf[1] << 8 | idbuf[2] << 16 | idbuf[3] << 24;
		int id = hashid_lookup(&g->hash, uid);
		if (id>=0) {
			// 发送给客户端（去掉最后4字节的id）
			skynet_socket_send(ctx, uid, (void*)msg, sz-4);
			return 1;  // 返回1表示不要释放msg
		} else {
			skynet_error(ctx, "Invalid client id %d from %x",(int)uid,source);
			break;
		}
	}
	
	// Socket 事件（来自 skynet_socket）
	case PTYPE_SOCKET:
		dispatch_socket_message(g, msg, (int)(sz-sizeof(struct skynet_socket_message)));
		break;
	}
	return 0;
}

/**
 * 【内部】开始监听端口
 */
static int
start_listen(struct gate *g, char * listen_addr) {
	struct skynet_context * ctx = g->ctx;
	char * portstr = strrchr(listen_addr,':');
	const char * host = "";
	int port;
	if (portstr == NULL) {
		// 只有端口号，如 "8888"
		port = strtol(listen_addr, NULL, 10);
		if (port <= 0) {
			skynet_error(ctx, "Invalid gate address %s",listen_addr);
			return 1;
		}
	} else {
		// 有地址和端口，如 "0.0.0.0:8888"
		port = strtol(portstr + 1, NULL, 10);
		if (port <= 0) {
			skynet_error(ctx, "Invalid gate address %s",listen_addr);
			return 1;
		}
		portstr[0] = '\0';
		host = listen_addr;
	}
	// 创建监听 socket
	g->listen_id = skynet_socket_listen(ctx, host, port, BACKLOG);
	if (g->listen_id < 0) {
		return 1;
	}
	// 开始监听
	skynet_socket_start(ctx, g->listen_id);
	return 0;
}

/**
 * 【接口】初始化 Gate 服务
 * 
 * 参数格式: "<S|L> <watchdog> <binding> <client_tag> <max>"
 * 示例: "S watchdog 0.0.0.0:8888 1 1024"
 * 
 * - S/L: 2字节或4字节长度头
 * - watchdog: 看门狗服务名，! 表示无
 * - binding: 监听地址
 * - client_tag: 消息类型标签
 * - max: 最大连接数
 */
int
gate_init(struct gate *g , struct skynet_context * ctx, char * parm) {
	if (parm == NULL)
		return 1;
	int max = 0;
	int sz = strlen(parm)+1;
	char watchdog[sz];
	char binding[sz];
	int client_tag = 0;
	char header;
	// 解析参数
	int n = sscanf(parm, "%c %s %s %d %d", &header, watchdog, binding, &client_tag, &max);
	if (n<4) {
		skynet_error(ctx, "Invalid gate parm %s",parm);
		return 1;
	}
	if (max <=0 ) {
		skynet_error(ctx, "Need max connection");
		return 1;
	}
	if (header != 'S' && header !='L') {
		skynet_error(ctx, "Invalid data header style");
		return 1;
	}

	if (client_tag == 0) {
		client_tag = PTYPE_CLIENT;
	}
	// 解析 watchdog 服务名
	if (watchdog[0] == '!') {
		g->watchdog = 0;
	} else {
		g->watchdog = skynet_queryname(ctx, watchdog);
		if (g->watchdog == 0) {
			skynet_error(ctx, "Invalid watchdog %s",watchdog);
			return 1;
		}
	}

	g->ctx = ctx;

	// 初始化连接管理
	hashid_init(&g->hash, max);
	g->conn = skynet_malloc(max * sizeof(struct connection));
	memset(g->conn, 0, max *sizeof(struct connection));
	g->max_connection = max;
	int i;
	for (i=0;i<max;i++) {
		g->conn[i].id = -1;
	}
	
	g->client_tag = client_tag;
	g->header_size = header=='S' ? 2 : 4;  // 'S'=2字节, 'L'=4字节

	// 注册消息回调
	skynet_callback(ctx,g,_cb);

	// 开始监听
	return start_listen(g,binding);
}
