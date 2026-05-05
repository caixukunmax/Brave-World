# Buff系统 - Phase 1: 基础框架

## 目标
搭建 Buff 系统的基础数据结构和配置加载。

## 任务清单
- [ ] 新增 BuffInstance 类
- [ ] 新增 BuffContainer 类（注入 LubanTableLoader）
- [ ] Luban 表定义（common.xml + xlsx）
- [ ] LubanBeans.cs 新增 BuffConfigRow 等
- [ ] LubanTableLoader 加载 Buff 表
- [ ] CombatContext 增加 Buffs 字段
- [ ] ActionContext 增加 Tables 字段
- [ ] CombatRelationManager.GetOrCreateContext 注入 LubanTableLoader

## 验证
- 双端编译 0 错误
- 服务端启动正常

## 设计文档
docs/design/Buff系统设计.md
