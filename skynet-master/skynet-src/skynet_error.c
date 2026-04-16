/**
 * ============================================================================
 * Skynet 错误日志模块 - skynet_error.c
 * ============================================================================
 * 
 * 【文件作用】
 * 提供错误日志输出功能，将日志消息发送给 logger 服务
 * 
 * 【工作流程】
 * 1. 格式化日志消息（支持 printf 风格的格式化）
 * 2. 查找 logger 服务
 * 3. 构造 skynet_message 发送给 logger
 * 
 * 【消息类型】
 * PTYPE_TEXT - 文本消息，logger 服务会将内容写入日志文件或输出到控制台
 * ============================================================================
 */

#include "skynet.h"
#include "skynet_handle.h"
#include "skynet_imp.h"
#include "skynet_mq.h"
#include "skynet_server.h"

#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>

// 日志消息的最大长度
#define LOG_MESSAGE_SIZE 256

/**
 * 【内部】格式化日志字符串
 * 
 * 特殊处理 "%*s" 格式（用于 lua-skynet.c 中的 lerror）
 */
static int
log_try_vasprintf(char **strp, const char *fmt, va_list ap) {
	if (strcmp(fmt, "%*s") == 0) {
		// 处理 lerror 的特殊格式
		const int len = va_arg(ap, int);
		const char *tmp = va_arg(ap, const char*);
		*strp = skynet_strndup(tmp, len);
		return *strp != NULL ? len : -1;
	}

	// 普通格式，先尝试使用栈缓冲区
	char tmp[LOG_MESSAGE_SIZE];
	int len = vsnprintf(tmp, LOG_MESSAGE_SIZE, fmt, ap);
	if (len >= 0 && len < LOG_MESSAGE_SIZE) {
		*strp = skynet_strndup(tmp, len);
		if (*strp == NULL) return -1;
	}
	return len;
}

/**
 * 【接口】输出错误日志
 * 
 * @param context  当前服务上下文（可为 NULL）
 * @param msg      格式化字符串
 * @param ...      可变参数
 */
void
skynet_error(struct skynet_context * context, const char *msg, ...) {
	// 缓存 logger 句柄（避免每次查找）
	static uint32_t logger = 0;
	if (logger == 0) {
		logger = skynet_handle_findname("logger");
	}
	if (logger == 0) {
		return;  // logger 服务尚未启动
	}

	char *data = NULL;

	va_list ap;

	// 第一次尝试格式化
	va_start(ap, msg);
	int len = log_try_vasprintf(&data, msg, ap);
	va_end(ap);
	if (len < 0) {
		perror("vasprintf error :");
		return;
	}

	// 如果消息太长，使用堆内存重新格式化
	if (data == NULL) { // unlikely
		data = skynet_malloc(len + 1);
		va_start(ap, msg);
		len = vsnprintf(data, len + 1, msg, ap);
		va_end(ap);
		if (len < 0) {
			skynet_free(data);
			perror("vsnprintf error :");
			return;
		}
	}

	// 构造消息并发送给 logger
	struct skynet_message smsg;
	if (context == NULL) {
		smsg.source = 0;
	} else {
		smsg.source = skynet_context_handle(context);
	}
	smsg.session = 0;
	smsg.data = data;
	smsg.sz = len | ((size_t)PTYPE_TEXT << MESSAGE_TYPE_SHIFT);
	skynet_context_push(logger, &smsg);
}
