# tslua2 项目文档中心

> 最后更新：2026-05-14

本文档是 tslua2 项目的文档总索引，帮助团队成员快速定位所需信息。

---

## 📁 文档目录

| 目录 | 说明 | 适用场景 |
|------|------|----------|
| [architecture.md](architecture.md) | 项目架构总览 | 新人上手、跨模块排障 |
| [design/](design/) | 系统设计文档 | 需求评审、方案讨论、代码实现前参考 |
| [tech/](tech/) | 技术文档与规范 | 开发规范、排障手册、协议约定 |
| [daily/](daily/) | 开发日报 | 追溯决策历史、复盘问题根因 |
| [reports/](reports/) | 分析报告 | 代码审查、体验分析、待办清单 |
| [reference/](reference/) | 外部资料与参考 | 第三方框架学习、历史技术方案 |

---

## 🚀 快速入口

### 我是新人，刚加入项目
1. 先看 [architecture.md](architecture.md) — 了解项目整体架构和四条核心链路
2. 再看 [tech/项目优化建议-2026-04-28.md](tech/项目优化建议-2026-04-28.md) — 了解当前工程化现状
3. 根据你的方向，深入对应设计文档

### 我要新增一个系统/功能
1. 先在 [design/](design/) 搜索是否有相关设计文档
2. 没有的话，按 [AGENTS.md](../AGENTS.md) 要求先写设计文档
3. 确认文档无冲突后，再进入代码实现

### 我要排查一个问题
1. 先看 [tech/logging-and-troubleshooting.md](tech/logging-and-troubleshooting.md)
2. 如果是跨模块问题，参考 [architecture.md](architecture.md) 的链路图
3. 查阅相关 [daily/](daily/) 日报，看是否有类似问题的历史记录

### 我要了解当前未实现的功能
- 查看 [design/pending-features.md](design/pending-features.md)

---

## 📝 文档规范

### 命名规则
- 设计文档：优先使用英文 kebab-case，如 `combat-system.md`
- 日报：统一格式 `YYYY-MM-DD.md`
- 报告：统一格式 `YYYY-MM-DD-报告主题.md`

### 新增文档流程
1. 确定文档类型（设计 / 技术 / 日报 / 报告）
2. 放入对应目录
3. 在本索引的对应章节添加链接
4. 如涉及架构变更，同步更新 [architecture.md](architecture.md)

---

## ⚠️ 归档说明

- `design/_archive/`：已过时但仍具参考价值的历史设计文档
- 文档归档时，请在原文顶部标注**状态：已归档 / 已过时**，并说明替代文档
