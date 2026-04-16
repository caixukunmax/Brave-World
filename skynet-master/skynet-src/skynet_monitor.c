/**
 * ============================================================================
 * Skynet 监控模块 - skynet_monitor.c
 * ============================================================================
 * 
 * 【文件作用】
 * 监控工作线程，检测服务是否陷入死循环（长时间不返回）
 * 
 * 【工作原理】
 * 1. 每个工作线程有一个对应的 skynet_monitor
 * 2. 工作线程开始处理消息时，记录 source 和 destination，并增加 version
 * 3. 工作线程处理完消息时，清除 destination，并增加 version
 * 4. 监控线程定期检查：
 *    - 如果 version 不变，说明线程卡住了
 *    - 如果 destination 不为 0，说明在处理某个服务的消息时卡住
 *    - 标记该服务为 endless，输出警告日志
 * 
 * 【使用方式】
 * 在 skynet_start.c 中，监控线程每 5 秒调用一次 skynet_monitor_check
 * ============================================================================
 */

#include "skynet.h"

#include "skynet_monitor.h"
#include "skynet_server.h"
#include "skynet.h"
#include "atomic.h"

#include <stdlib.h>
#include <string.h>

/**
 * 【数据结构】监控器
 * 
 * 每个工作线程对应一个监控器
 */
struct skynet_monitor {
	ATOM_INT version;       // 版本号（每次触发时递增）
	int check_version;      // 上次检查时记录的版本号
	uint32_t source;        // 消息来源
	uint32_t destination;   // 消息目标（不为0表示正在处理）
};

/**
 * 【接口】创建监控器
 */
struct skynet_monitor * 
skynet_monitor_new() {
	struct skynet_monitor * ret = skynet_malloc(sizeof(*ret));
	memset(ret, 0, sizeof(*ret));
	return ret;
}

/**
 * 【接口】删除监控器
 */
void 
skynet_monitor_delete(struct skynet_monitor *sm) {
	skynet_free(sm);
}

/**
 * 【接口】触发监控器
 * 
 * 工作线程开始或结束处理消息时调用
 * 
 * @param source       消息来源服务句柄
 * @param destination  消息目标服务句柄（0 表示处理结束）
 */
void 
skynet_monitor_trigger(struct skynet_monitor *sm, uint32_t source, uint32_t destination) {
	sm->source = source;
	sm->destination = destination;
	ATOM_FINC(&sm->version);  // 版本号递增
}

/**
 * 【接口】检查监控器
 * 
 * 监控线程定期调用，检测是否有工作线程卡住
 */
void 
skynet_monitor_check(struct skynet_monitor *sm) {
	if (sm->version == sm->check_version) {
		// 版本号没变，说明线程卡住了
		if (sm->destination) {
			// 标记目标服务为 endless（死循环）
			skynet_context_endless(sm->destination);
			skynet_error(NULL, "error: A message from [ :%08x ] to [ :%08x ] maybe in an endless loop (version = %d)", 
				sm->source , sm->destination, sm->version);
		}
	} else {
		// 版本号变化，更新检查版本
		sm->check_version = sm->version;
	}
}
