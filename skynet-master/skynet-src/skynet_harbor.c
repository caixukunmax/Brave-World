/**
 * ============================================================================
 * Skynet 集群通信模块 - skynet_harbor.c
 * ============================================================================
 * 
 * 【文件作用】
 * 提供 Skynet 节点间的消息路由功能
 * 
 * 【句柄格式】
 * 32位句柄 = [节点ID(8位) | 本地句柄(24位)]
 * - 节点ID为0表示本地服务
 * - 节点ID不为0表示远程服务
 * 
 * 【工作流程】
 * 1. 发送消息时，检查目标句柄的节点ID
 * 2. 如果是远程节点，将消息转发给 harbor 服务
 * 3. harbor 服务负责网络传输到目标节点
 * 
 * 【注意】
 * 这个文件只提供 C 层的接口封装，实际的网络通信由 harbor 服务（Lua 层）实现
 * ============================================================================
 */

#include "skynet.h"
#include "skynet_harbor.h"
#include "skynet_server.h"
#include "skynet_mq.h"
#include "skynet_handle.h"

#include <string.h>
#include <stdio.h>
#include <assert.h>

// harbor 服务上下文（用于发送远程消息）
static struct skynet_context * REMOTE = 0;
// 本节点ID（放在句柄高8位）
static unsigned int HARBOR = ~0;

/**
 * 【内部】检查消息类型是否有效
 * 
 * 远程消息只能是 SYSTEM 或 HARBOR 类型
 */
static inline int
invalid_type(int type) {
	return type != PTYPE_SYSTEM && type != PTYPE_HARBOR;
}

/**
 * 【接口】发送远程消息
 * 
 * @param rmsg     远程消息结构
 * @param source   来源服务句柄
 * @param session  会话ID
 */
void 
skynet_harbor_send(struct remote_message *rmsg, uint32_t source, int session) {
	assert(invalid_type(rmsg->type) && REMOTE);
	// 将消息发送给 harbor 服务，由它负责网络传输
	skynet_context_send(REMOTE, rmsg, sizeof(*rmsg) , source, PTYPE_SYSTEM , session);
}

/**
 * 【接口】判断句柄是否指向远程节点
 * 
 * @param handle  服务句柄
 * @return        1 表示远程，0 表示本地
 */
int 
skynet_harbor_message_isremote(uint32_t handle) {
	assert(HARBOR != ~0);
	int h = (handle & ~HANDLE_MASK);
	return h != HARBOR && h !=0;
}

/**
 * 【接口】初始化 harbor
 * 
 * @param harbor  本节点ID（1-255）
 */
void
skynet_harbor_init(int harbor) {
	HARBOR = (unsigned int)harbor << HANDLE_REMOTE_SHIFT;
}

/**
 * 【接口】启动 harbor 服务
 * 
 * @param ctx  harbor 服务上下文
 */
void
skynet_harbor_start(void *ctx) {
	// 保留 harbor 服务，确保指针有效
	// 它会在 skynet_harbor_exit 时被释放
	skynet_context_reserve(ctx);
	REMOTE = ctx;
}

/**
 * 【接口】退出 harbor
 */
void
skynet_harbor_exit() {
	struct skynet_context * ctx = REMOTE;
	REMOTE= NULL;
	if (ctx) {
		skynet_context_release(ctx);
	}
}
