/**
 * ============================================================================
 * Skynet 内存信息模块 - mem_info.c
 * ============================================================================
 * 
 * 【文件作用】
 * 提供内存统计信息的数据结构和操作函数
 * 
 * 【数据结构】
 * - MemInfo:       普通内存统计（alloc/free 计数和大小）
 * - AtomicMemInfo: 原子内存统计（线程安全版本）
 * 
 * 【使用场景】
 * 用于 malloc_hook.c 中按服务统计内存使用情况
 * ============================================================================
 */

#include <string.h>

#include "mem_info.h"

/**
 * 【接口】初始化 MemInfo
 */
void
meminfo_init(MemInfo *info) {
    memset(info, 0, sizeof(*info));
}

/**
 * 【接口】初始化 AtomicMemInfo
 */
void
atomic_meminfo_init(AtomicMemInfo *info) {
    ATOM_INIT(&info->alloc, 0);
    ATOM_INIT(&info->alloc_count, 0);
    ATOM_INIT(&info->free, 0);
    ATOM_INIT(&info->free_count, 0);
}

/**
 * 【接口】记录分配（普通版本）
 */
void
meminfo_alloc(MemInfo *info, size_t size) {
    info->alloc += size;
    ++info->alloc_count;
}

/**
 * 【接口】记录分配（原子版本）
 */
void
atomic_meminfo_alloc(AtomicMemInfo *info, size_t size) {
    ATOM_FADD(&info->alloc, size);
    ATOM_FADD(&info->alloc_count, 1);
}

/**
 * 【接口】记录释放（普通版本）
 */
void
meminfo_free(MemInfo *info, size_t size) {
    info->free += size;
    ++info->free_count;
}

/**
 * 【接口】记录释放（原子版本）
 */
void
atomic_meminfo_free(AtomicMemInfo *info, size_t size) {
    ATOM_FADD(&info->free, size);
    ATOM_FADD(&info->free_count, 1);
}

/**
 * 【接口】合并统计信息
 */
void
meminfo_merge(MemInfo *dest, const MemInfo *src) {
    dest->alloc += src->alloc;
    dest->alloc_count += src->alloc_count;
    dest->free += src->free;
    dest->free_count += src->free_count;
}

/**
 * 【接口】从原子统计读取并合并
 * 
 * 注意：先读取 free 后读取 alloc，避免大小错乱
 * （alloc 总是 >= free，如果顺序反过来，可能读到 alloc < free 的中间状态）
 */
void
atomic_meminfo_merge(MemInfo *dest, const AtomicMemInfo *src) {
    MemInfo info;
    info.free_count = ATOM_LOAD(&src->free_count);
    info.free = ATOM_LOAD(&src->free);
    info.alloc_count = ATOM_LOAD(&src->alloc_count);
    info.alloc = ATOM_LOAD(&src->alloc);
    meminfo_merge(dest, &info);
}
