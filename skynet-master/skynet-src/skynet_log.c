/**
 * ============================================================================
 * Skynet 消息日志模块 - skynet_log.c
 * ============================================================================
 * 
 * 【文件作用】
 * 提供服务的消息日志功能，可以记录服务收发的所有消息到文件
 * 
 * 【使用方式】
 * 通过 skynet 命令 LOGON/LOGOFF 控制：
 * - LOGON <handle>  - 开启指定服务的消息日志
 * - LOGOFF <handle> - 关闭指定服务的消息日志
 * 
 * 【配置】
 * 需要设置环境变量 "logpath" 指定日志文件存放目录
 * 
 * 【日志格式】
 * - 普通消息：:source type session timestamp hex_data
 * - Socket 消息：[socket] type id ud data
 * ============================================================================
 */

#include "skynet_log.h"
#include "skynet_timer.h"
#include "skynet.h"
#include "skynet_socket.h"
#include <string.h>
#include <time.h>

/**
 * 【接口】打开消息日志文件
 * 
 * @param ctx     服务上下文
 * @param handle  服务句柄
 * @return        文件指针（NULL 表示失败）
 */
FILE * 
skynet_log_open(struct skynet_context * ctx, uint32_t handle) {
	const char * logpath = skynet_getenv("logpath");
	if (logpath == NULL)
		return NULL;
	size_t sz = strlen(logpath);
	char tmp[sz + 16];
	sprintf(tmp, "%s/%08x.log", logpath, handle);
	FILE *f = fopen(tmp, "ab");  // 以追加模式打开
	if (f) {
		uint32_t starttime = skynet_starttime();
		uint64_t currenttime = skynet_now();
		time_t ti = starttime + currenttime/100;
		skynet_error(ctx, "Open log file %s", tmp);
		fprintf(f, "open time: %u %s", (uint32_t)currenttime, ctime(&ti));
		fflush(f);
	} else {
		skynet_error(ctx, "Open log file %s fail", tmp);
	}
	return f;
}

/**
 * 【接口】关闭消息日志文件
 */
void
skynet_log_close(struct skynet_context * ctx, FILE *f, uint32_t handle) {
	skynet_error(ctx, "Close log file :%08x", handle);
	fprintf(f, "close time: %u\n", (uint32_t)skynet_now());
	fclose(f);
}

/**
 * 【内部】以十六进制格式输出二进制数据
 */
static void
log_blob(FILE *f, void * buffer, size_t sz) {
	size_t i;
	uint8_t * buf = buffer;
	for (i=0;i!=sz;i++) {
		fprintf(f, "%02x", buf[i]);
	}
}

/**
 * 【内部】输出 socket 消息
 */
static void
log_socket(FILE * f, struct skynet_socket_message * message, size_t sz) {
	fprintf(f, "[socket] %d %d %d ", message->type, message->id, message->ud);

	if (message->buffer == NULL) {
		// 数据紧跟在消息结构后面
		const char *buffer = (const char *)(message + 1);
		sz -= sizeof(*message);
		const char * eol = memchr(buffer, '\0', sz);
		if (eol) {
			sz = eol - buffer;
		}
		fprintf(f, "[%*s]", (int)sz, (const char *)buffer);
	} else {
		// 数据在独立缓冲区中
		sz = message->ud;
		log_blob(f, message->buffer, sz);
	}
	fprintf(f, "\n");
	fflush(f);
}

/**
 * 【接口】输出消息到日志文件
 * 
 * @param f        日志文件
 * @param source   消息来源
 * @param type     消息类型
 * @param session  会话ID
 * @param buffer   消息数据
 * @param sz       消息大小
 */
void 
skynet_log_output(FILE *f, uint32_t source, int type, int session, void * buffer, size_t sz) {
	if (type == PTYPE_SOCKET) {
		// Socket 消息特殊格式
		log_socket(f, buffer, sz);
	} else {
		// 普通消息格式：source type session timestamp data
		uint32_t ti = (uint32_t)skynet_now();
		fprintf(f, ":%08x %d %d %u ", source, type, session, ti);
		log_blob(f, buffer, sz);
		fprintf(f,"\n");
		fflush(f);
	}
}
