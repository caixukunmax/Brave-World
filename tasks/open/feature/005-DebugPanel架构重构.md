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
- [ ] 1. 配置 key 常量化 — 消除手写字符串
- [ ] 2. BarControlGroup 抽象 — 血条/MP条/施法条统一
- [ ] 3. LoadConfig/ApplyLoadedPlayerSettings 合并 — 消除双重真相源
- [ ] 4. EntityStyleTabBase 抽象 — Monster/Npc 重复消除
- [ ] 5. Undo 要么做完整要么去掉
- [ ] 6. AttachValueLineEdit 全局状态修复
