/**
 * ============================================================================
 * Skynet 主头文件 - skynet.h
 * ============================================================================
 * 
 * 【文件作用】
 * Skynet 框架的公共头文件，定义了：
 * 1. 消息类型（PTYPE_*）
 * 2. 核心 API 函数
 * 3. 回调函数类型
 * 
 * 【消息类型】
 * 消息类型定义了服务间通信的数据类型：
 * - TEXT:     文本消息（日志等）
 * - RESPONSE: 响应消息（用于 request/response 模式）
 * - CLIENT:   客户端消息
 * - SYSTEM:   系统消息
 * - SOCKET:   Socket 事件消息
 * - ERROR:    错误消息
 * 
 * 【消息类型标志】
 * - PTYPE_TAG_DONTCOPY:      不复制消息数据（发送者负责管理内存）
 * - PTYPE_TAG_ALLOCSESSION:  自动分配 session
 * ============================================================================
 */

#ifndef SKYNET_H
#define SKYNET_H

#include "skynet_malloc.h"

#include <stddef.h>
#include <stdint.h>

// ==================== 消息类型 ====================
#define PTYPE_TEXT 0
#define PTYPE_RESPONSE 1
#define PTYPE_MULTICAST 2
#define PTYPE_CLIENT 3
#define PTYPE_SYSTEM 4
#define PTYPE_HARBOR 5
#define PTYPE_SOCKET 6
// read lualib/skynet.lua examples/simplemonitor.lua
#define PTYPE_ERROR 7	
// read lualib/skynet.lua lualib/mqueue.lua lualib/snax.lua
#define PTYPE_RESERVED_QUEUE 8
#define PTYPE_RESERVED_DEBUG 9
#define PTYPE_RESERVED_LUA 10
#define PTYPE_RESERVED_SNAX 11

// ==================== 消息类型标志 ====================
#define PTYPE_TAG_DONTCOPY 0x10000
#define PTYPE_TAG_ALLOCSESSION 0x20000

// 前向声明
struct skynet_context;

// ==================== 核心 API ====================

/**
 * 输出错误日志
 */
void skynet_error(struct skynet_context * context, const char *msg, ...);

/**
 * 执行 Skynet 命令
 * 常用命令：TIMEOUT, LAUNCH, KILL, EXIT, REG, QUERY, GETENV, SETENV
 */
const char * skynet_command(struct skynet_context * context, const char * cmd , const char * parm);

/**
 * 查询服务句柄（支持 :hex 或 .name 格式）
 */
uint32_t skynet_queryname(struct skynet_context * context, const char * name);

/**
 * 发送消息到指定句柄
 */
int skynet_send(struct skynet_context * context, uint32_t source, uint32_t destination , int type, int session, void * msg, size_t sz);

/**
 * 发送消息到指定名称
 */
int skynet_sendname(struct skynet_context * context, uint32_t source, const char * destination , int type, int session, void * msg, size_t sz);

/**
 * 判断句柄是否指向远程节点
 */
int skynet_isremote(struct skynet_context *, uint32_t handle, int * harbor);

/**
 * 消息回调函数类型
 * 
 * @param context    服务上下文
 * @param ud         用户数据
 * @param type       消息类型
 * @param session    会话ID
 * @param source     消息来源
 * @param msg        消息数据
 * @param sz         消息大小
 * @return           0 释放消息内存，1 保留消息内存
 */
typedef int (*skynet_cb)(struct skynet_context * context, void *ud, int type, int session, uint32_t source , const void * msg, size_t sz);

/**
 * 设置消息回调函数
 */
void skynet_callback(struct skynet_context * context, void *ud, skynet_cb cb);

/**
 * 获取当前服务的句柄
 */
uint32_t skynet_current_handle(void);

/**
 * 获取当前时间（相对于启动的厘秒数）
 */
uint64_t skynet_now(void);

/**
 * 调试：输出当前服务内存使用情况到 stderr
 */
void skynet_debug_memory(const char *info);

#endif
