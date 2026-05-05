---
type: feature
status: open
priority: P1
module: DebugPanel
created: 2026-05-02
completed:
blocked:
related_files:
  - clinetcsharp/Scripts/DebugPanelPlayerTab.cs
  - clinetcsharp/Scripts/DebugPanelMonsterTab.cs
  - clinetcsharp/Scripts/DebugPanelNpcTab.cs
  - clinetcsharp/Scripts/DebugPanelTab.cs
  - clinetcsharp/Scripts/DebugPanel.Config.cs
  - clinetcsharp/Scripts/DebugPanel.Undo.cs
  - clinetcsharp/Scripts/EntityStyleConfig.cs
  - clinetcsharp/Scripts/MonsterManager.cs
fix_commit:
---

# DebugPanel 架构重构

## 目标
消除 DebugPanel 系统中的复制粘贴和双重真相源问题，防止"修一处忘一处"的 bug 反复出现。

## 修改记录

### 2026-05-02 创建
- [x] 1. 配置 key 常量化 — BarControlGroup 里定义了所有 key 常量
- [x] 2. BarControlGroup 抽象 — 血条/MP条/施法条统一，PlayerTab 2685→1616 行
- [x] 3. LoadConfig/ApplyLoadedPlayerSettings 合并 — ApplyLoadedPlayerSettings 现在委托给 LoadConfig
- [ ] 4. EntityStyleTabBase 抽象 — Monster/Npc 重复消除（子代理正在做）
- [x] 5. Undo 去掉 — 半吊子 Undo 比没有更糟，改为空操作
- [x] 6. AttachValueLineEdit 全局状态修复 — 改为 SetActiveLineEdit/ClearActiveLineEdit
