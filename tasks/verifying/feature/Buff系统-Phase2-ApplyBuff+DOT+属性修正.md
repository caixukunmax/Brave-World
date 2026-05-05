# Buff系统 - Phase 2: ApplyBuffAction + DOT + 属性修正

## 目标
实现 Buff 施加、Tick 逻辑、属性修正集成。

## 任务清单
- [ ] 新增 ApplyBuffAction（action_type=4）
- [ ] SkillPipeline.ExecuteActions 支持 action_type=4/5
- [ ] CombatManager.TickBuffs 逻辑
- [ ] DOT Buff（中毒）+ 战吼 Buff（属性加成）
- [ ] DealDamageAction.CalcDamage 加属性修正
- [ ] HealAction.CalcHeal 加属性修正
- [ ] 脱战清 Buff

## 验证
- 毒雾持续扣血
- 战吼加攻击力
- 脱战后 Buff 清除
