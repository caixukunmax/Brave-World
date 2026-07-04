from openpyxl import load_workbook
from openpyxl.styles import Font, Alignment, PatternFill, Border, Side
from openpyxl.utils import get_column_letter

skills = []

def add_skill(cls, stage, name, typ, upgrade_from, cast_range, cast_time, post_cast, cd, mp, target, actions, desc):
    skills.append((cls, stage, name, typ, upgrade_from, cast_range, cast_time, post_cast, cd, mp, target, actions, desc))

# ========== 剑修：共同基础线 ==========
# 技能线定义：
# A线：劈砍（近战单体）
# B线：凝聚剑气（自身增益）
# C线：剑气（远程单体）

# 剑徒
add_skill("剑修", "剑徒", "劈砍", "普攻", "初始", 1, "0.5s", "0.1s", "0s", 0, "SingleEnemy", "DealDamage", "最基础的近战单体剑击。")
add_skill("剑修", "剑徒", "凝聚剑气", "主动", "初始", 0, "0.5s", "0.2s", "6s", 10, "Self", "ApplyBuff(下次攻击增幅)", "凝聚剑气，使下次攻击伤害提升。")
add_skill("剑修", "剑徒", "剑气", "主动", "初始", 5, "0.8s", "0.2s", "3s", 10, "SingleEnemy", "SpawnProjectile -> DealDamage", "远程发射一道剑气。")

# 剑士：A/B/C 三技能均升级
add_skill("剑修", "剑士", "连斩", "普攻", "劈砍升级", 1, "0.5s", "0.1s", "0s", 0, "SingleEnemy", "DealDamage x2", "劈砍的进阶，快速二连击。")
add_skill("剑修", "剑士", "聚气", "主动", "凝聚剑气升级", 0, "0.5s", "0.2s", "6s", 12, "Self", "ApplyBuff(下次攻击增幅+暴击)", "凝聚剑气升级版，增幅效果更强并附带暴击率。")
add_skill("剑修", "剑士", "剑气外放", "主动", "剑气升级", 5, "0.8s", "0.2s", "3s", 12, "SingleEnemy", "SpawnProjectile -> DealDamage", "剑气升级版，飞行速度更快、伤害更高。")

# 剑师：A/B/C 三技能均升级，加新技能 D
add_skill("剑修", "剑师", "剑气斩", "普攻", "连斩升级", 1, "0.5s", "0.1s", "0s", 0, "SingleEnemy", "DealDamage(附加剑气)", "连斩升级，斩击附带剑气穿透伤害。")
add_skill("剑修", "剑师", "剑意", "主动", "聚气升级", 0, "0.5s", "0.2s", "6s", 15, "Self", "ApplyBuff(攻击附带剑气)", "聚气升级，接下来数次攻击均附带剑气伤害。")
add_skill("剑修", "剑师", "剑气纵横", "主动", "剑气外放升级", 5, "1.0s", "0.3s", "5s", 18, "AllEnemiesInRange", "SpawnProjectile(穿透) -> DealDamage", "剑气外放升级，可穿透路径上的多个敌人。")
add_skill("剑修", "剑师", "身法·闪", "主动", "新技能", 8, "0.3s", "0.1s", "10s", 15, "GroundPos", "Teleport -> ApplyBuff(加速)", "新学身法，瞬移并短暂加速。")

# 御剑使：A/B/C 三技能均升级（A开始远程化）
add_skill("剑修", "御剑使", "御剑斩", "普攻", "剑气斩升级", 6, "0.5s", "0.1s", "0s", 0, "SingleEnemy", "SpawnProjectile -> DealDamage", "剑气斩升级，改为远程御剑斩击。")
add_skill("剑修", "御剑使", "御剑心法", "主动", "剑意升级", 0, "0.5s", "0.2s", "6s", 18, "Self", "ApplyBuff(攻速/剑气增伤)", "剑意升级，提升攻速与剑气伤害。")
add_skill("剑修", "御剑使", "御剑术", "主动", "剑气纵横升级", 6, "0.8s", "0.3s", "5s", 22, "SingleEnemy", "SpawnProjectile x3 -> DealDamage", "剑气纵横升级，同时操控三把飞剑。")

# 剑修：A/B/C 三技能均升级
add_skill("剑修", "剑修", "归一剑", "普攻", "御剑斩升级", 6, "0.5s", "0.1s", "0s", 0, "SingleEnemy", "SpawnProjectile -> DealDamage", "御剑斩升级，多剑合一的精准打击。")
add_skill("剑修", "剑修", "剑心通明", "主动", "御剑心法升级", 0, "0.5s", "0.2s", "8s", 22, "Self", "ApplyBuff(暴击/看破)", "御剑心法升级，剑心澄澈，看破破绽。")
add_skill("剑修", "剑修", "万剑归宗", "主动", "御剑术升级", 6, "1.0s", "0.3s", "6s", 28, "GroundPos", "SpawnArea -> DealDamage", "御剑术升级，范围剑雨打击。")

# ========== 剑修：以技服人路线 ==========
# 剑心通明：A升级为控制型，B升级为增益，C升级为远程穿透，加新技能 D
add_skill("剑修", "剑心通明", "破招斩", "普攻", "归一剑升级", 3, "0.5s", "0.1s", "0s", 0, "SingleEnemy", "DealDamage -> ApplyDebuff(短暂沉默)", "归一剑升级，斩击附带短暂沉默效果。")
add_skill("剑修", "剑心通明", "心眼", "主动", "剑心通明升级", 0, "0.5s", "0.2s", "8s", 25, "Self", "ApplyBuff(高暴击/看破)", "剑心通明升级，大幅提升暴击与命中率。")
add_skill("剑修", "剑心通明", "气剑指", "主动", "万剑归宗升级", 8, "0.8s", "0.2s", "4s", 25, "SingleEnemy", "SpawnProjectile(穿透) -> DealDamage", "万剑归宗升级，凝练为一道高穿透剑气。")
add_skill("剑修", "剑心通明", "精准连击", "主动", "新技能", 1, "0.8s", "0.2s", "5s", 30, "SingleEnemy", "DealDamage x3", "新技能，快速三连击爆发。")

# 剑气化形
add_skill("剑修", "剑气化形", "剑气分身斩", "普攻", "破招斩升级", 3, "0.5s", "0.1s", "0s", 0, "SingleEnemy", "SpawnProjectile(分身) -> DealDamage", "破招斩升级，斩击时召唤剑气虚影协同攻击。")
add_skill("剑修", "剑气化形", "凝气护体", "主动", "心眼升级", 0, "0.5s", "0.2s", "8s", 25, "Self", "ApplyBuff(护盾)", "心眼升级，凝聚剑气形成吸收伤害的护盾。")
add_skill("剑修", "剑气化形", "穿透气剑指", "主动", "气剑指升级", 8, "0.8s", "0.2s", "4s", 28, "AllEnemiesInRange", "SpawnProjectile(穿透) -> DealDamage", "气剑指升级，穿透力更强，射程更远。")
add_skill("剑修", "剑气化形", "剑气化形", "主动", "新技能", 0, "1.0s", "0.3s", "10s", 35, "Self", "SpawnProjectile(剑气虚影) -> DealDamage", "新技能，召唤剑气虚影持续攻击周围敌人。")

# 天外剑仙
add_skill("剑修", "天外剑仙", "天外飞仙剑", "普攻", "剑气分身斩升级", 8, "0.6s", "0.1s", "0s", 0, "SingleEnemy", "SpawnProjectile -> DealDamage", "剑气分身斩升级，飞剑速度极快、射程极远。")
add_skill("剑修", "天外剑仙", "御空", "主动", "凝气护体升级", 0, "0.3s", "0.1s", "12s", 30, "Self", "ApplyBuff(移速/无视地形)", "凝气护体升级，御剑腾空，移速提升并无视地形。")
add_skill("剑修", "天外剑仙", "天外飞仙", "主动", "穿透气剑指升级", 10, "1.5s", "0.5s", "12s", 45, "AllEnemiesInRange", "SpawnProjectile(穿透) -> DealDamage", "穿透气剑指升级，全屏穿透的宏大剑气。")
add_skill("剑修", "天外剑仙", "剑气风暴", "主动", "新技能", 0, "1.0s", "0.3s", "8s", 40, "Self", "SpawnArea -> DealDamage", "新技能，以自身为中心释放剑气风暴。")

# 剑域主宰
add_skill("剑修", "剑域主宰", "域内瞬斩", "普攻", "天外飞仙剑升级", 8, "0.5s", "0.1s", "0s", 0, "SingleEnemy", "Teleport -> DealDamage", "天外飞仙剑升级，在剑域内可瞬移到敌人身边斩击。")
add_skill("剑修", "剑域主宰", "剑域展开", "主动", "御空升级", 0, "1.5s", "0.5s", "20s", 50, "Self", "SpawnArea -> ApplyBuff(自身) -> ApplyDebuff(敌人减速)", "御空升级，展开剑之领域，领域内敌减速、我增强。")
add_skill("剑修", "剑域主宰", "剑域镇压", "主动", "天外飞仙升级", 8, "1.0s", "0.3s", "12s", 45, "AllEnemiesInRange", "SpawnArea -> ApplyDebuff(禁锢)", "天外飞仙升级，剑域内范围禁锢敌人。")
add_skill("剑修", "剑域主宰", "剑域强化", "主动", "新技能", 0, "0.5s", "0.2s", "15s", 35, "Self", "ApplyBuff(领域内技能强化)", "新技能，进一步强化剑域内所有技能效果。")

# 剑道至尊
add_skill("剑修", "剑道至尊", "斩断", "普攻", "域内瞬斩升级", 3, "0.8s", "0.2s", "0s", 0, "SingleEnemy", "DealDamage(破甲)", "域内瞬斩升级，近身斩击无视部分防御。")
add_skill("剑修", "剑道至尊", "剑道法则", "被动", "剑域展开升级", 0, "-", "-", "-", 0, "Self", "ApplyBuff(常驻剑技增伤)", "剑域展开升华，常驻提升所有剑技伤害。")
add_skill("剑修", "剑道至尊", "归一剑法", "主动", "剑域镇压升级", 6, "1.5s", "0.5s", "10s", 50, "SingleEnemy", "SpawnProjectile -> DealDamage", "剑域镇压凝练为单体高爆发归一剑。")
add_skill("剑修", "剑道至尊", "万法归一", "主动", "新技能", 0, "1.0s", "0.3s", "15s", 45, "Self", "ApplyBuff(下一次技能伤害大幅提升)", "新技能，下一次释放的剑技伤害大幅提升。")

# 太上剑帝
add_skill("剑修", "太上剑帝", "太上无极剑", "普攻", "斩断升级", 6, "1.0s", "0.2s", "0s", 0, "SingleEnemy", "SpawnProjectile -> DealDamage(斩杀)", "斩断升级，终极单体斩杀剑技。")
add_skill("剑修", "太上剑帝", "剑意通神", "主动", "剑道法则升级", 0, "1.0s", "0.3s", "30s", 60, "Self", "ApplyBuff(全属性提升)", "剑道法则升级，进入剑意通神状态，全属性提升。")
add_skill("剑修", "太上剑帝", "无招胜有招", "主动", "归一剑法升级", 0, "0.5s", "0.2s", "10s", 45, "Self", "ApplyBuff(反击)", "归一剑法升级，进入反击姿态。")
add_skill("剑修", "太上剑帝", "太上领域", "主动", "新技能", 0, "2.0s", "0.5s", "25s", 80, "Self", "SpawnArea -> ApplyBuff(自身) -> ApplyDebuff(敌人)", "新技能，展开太上领域，领域内近乎无敌。")

# 虚空剑圣（预留）
add_skill("剑修", "虚空剑圣", "虚空斩", "普攻", "太上无极剑升级", 8, "1.0s", "0.2s", "0s", 0, "SingleEnemy", "SpawnProjectile -> DealDamage", "太上无极剑升级，剑破虚空。")
add_skill("剑修", "虚空剑圣", "虚空步", "主动", "剑意通神升级", 0, "0.3s", "0.1s", "12s", 50, "Self", "Teleport -> ApplyBuff(闪避)", "剑意通神升级，短距离空间跳跃。")
add_skill("剑修", "虚空剑圣", "虚空剑气", "主动", "无招胜有招升级", 10, "1.2s", "0.3s", "10s", 55, "AllEnemiesInRange", "SpawnProjectile(穿透) -> DealDamage", "无招胜有招升级，虚空剑气无视距离。")
add_skill("剑修", "虚空剑圣", "虚空裂隙", "主动", "新技能", 8, "1.5s", "0.5s", "18s", 60, "GroundPos", "SpawnArea -> DealDamage", "新技能，撕裂空间造成范围伤害。")

# 不灭剑祖（预留）
add_skill("剑修", "不灭剑祖", "不灭斩", "普攻", "虚空斩升级", 3, "0.8s", "0.2s", "0s", 0, "SingleEnemy", "DealDamage -> Heal(自身)", "虚空斩升级，攻击附带吸血。")
add_skill("剑修", "不灭剑祖", "不灭剑意", "被动", "虚空步升级", 0, "-", "-", "-", 0, "Self", "ApplyBuff(复活/免死一次)", "虚空步升华，获得一次免死效果。")
add_skill("剑修", "不灭剑祖", "不灭剑气", "主动", "虚空剑气升级", 10, "1.2s", "0.3s", "10s", 60, "AllEnemiesInRange", "SpawnArea -> DealDamage", "虚空剑气升级，范围更广、伤害更高。")
add_skill("剑修", "不灭剑祖", "剑魂不灭", "主动", "新技能", 0, "2.0s", "0.5s", "60s", 80, "Self", "ApplyBuff(死亡复活)", "新技能，死亡后立即复活并恢复部分生命。")

# 剑道真仙（预留）
add_skill("剑修", "剑道真仙", "真仙斩", "普攻", "不灭斩升级", 6, "1.0s", "0.2s", "0s", 0, "SingleEnemy", "SpawnProjectile -> DealDamage", "不灭斩升级，剑道真仙的普通一击。")
add_skill("剑修", "剑道真仙", "真仙剑意", "被动", "不灭剑意升级", 0, "-", "-", "-", 0, "Self", "ApplyBuff(常驻大幅增伤)", "不灭剑意升华，常驻巨额增伤。")
add_skill("剑修", "剑道真仙", "真仙剑气", "主动", "不灭剑气升级", 10, "1.5s", "0.5s", "12s", 70, "AllEnemiesInRange", "SpawnArea -> DealDamage", "不灭剑气升级，举手投足皆为剑招。")
add_skill("剑修", "剑道真仙", "剑道永恒", "主动", "新技能", 0, "3.0s", "0.5s", "60s", 100, "Self", "ApplyBuff(全技能无CD/大幅强化)", "新技能，进入剑道永恒状态，技能效果达到极致。")

# ========== 剑修：以狂服人路线 ==========
# 狂血剑魔：A/B/C 三技能狂化，加新技能 D
add_skill("剑修", "狂血剑魔", "狂血斩", "普攻", "归一剑狂化", 1, "0.6s", "0.2s", "0s", 0, "SingleEnemy", "ConsumeHP -> DealDamage(增伤)", "归一剑狂化，消耗自身 HP 增加伤害。")
add_skill("剑修", "狂血剑魔", "狂怒", "主动", "剑心通明狂化", 0, "0.5s", "0.2s", "8s", 20, "Self", "ApplyBuff(加攻减防)", "剑心通明狂化，攻击力提升但防御下降。")
add_skill("剑修", "狂血剑魔", "血剑", "主动", "万剑归宗狂化", 6, "0.8s", "0.2s", "4s", 25, "SingleEnemy", "SpawnProjectile -> DealDamage -> Heal", "万剑归宗狂化，远程血剑吸血。")
add_skill("剑修", "狂血剑魔", "嗜血", "主动", "新技能", 1, "0.8s", "0.2s", "6s", 25, "SingleEnemy", "DealDamage -> Heal(自身)", "新技能，吸血攻击。")

# 嗜血修罗
add_skill("剑修", "嗜血修罗", "血刃", "普攻", "狂血斩升级", 1, "0.5s", "0.2s", "0s", 0, "SingleEnemy", "DealDamage -> ApplyDebuff(流血)", "狂血斩升级，攻击附带流血。")
add_skill("剑修", "嗜血修罗", "杀戮本能", "被动", "狂怒升级", 0, "-", "-", "-", 0, "Self", "OnKill: Heal + 刷新CD", "狂怒升华，击杀目标回血并刷新技能。")
add_skill("剑修", "嗜血修罗", "血影突袭", "主动", "血剑升级", 5, "0.8s", "0.2s", "8s", 30, "SingleEnemy", "Teleport -> DealDamage", "血剑升级，化作血影突进攻击。")
add_skill("剑修", "嗜血修罗", "血刃风暴", "主动", "新技能", 0, "1.0s", "0.3s", "10s", 35, "Self", "SpawnArea -> DealDamage -> ApplyDebuff(流血)", "新技能，范围血刃风暴。")

# 镇狱剑王
add_skill("剑修", "镇狱剑王", "重剑劈山", "普攻", "血刃升级", 2, "1.2s", "0.5s", "0s", 0, "SingleEnemy", "DealDamage -> Knockback", "血刃升级，势大力沉，击退目标。")
add_skill("剑修", "镇狱剑王", "霸体", "主动", "杀戮本能升级", 0, "0.5s", "0.2s", "12s", 25, "Self", "ApplyBuff(霸体)", "杀戮本能升级，进入霸体免疫控制。")
add_skill("剑修", "镇狱剑王", "震地", "主动", "血影突袭升级", 0, "1.0s", "0.3s", "10s", 35, "Self", "SpawnArea -> ApplyDebuff(眩晕)", "血影突袭升级，重剑砸地范围眩晕。")
add_skill("剑修", "镇狱剑王", "镇狱", "主动", "新技能", 0, "1.5s", "0.5s", "20s", 45, "Self", "ApplyBuff(大幅减伤+反伤)", "新技能，如山岳般屹立，减伤并反弹伤害。")

# 血海剑尊
add_skill("剑修", "血海剑尊", "血海斩", "普攻", "重剑劈山升级", 2, "1.0s", "0.3s", "0s", 0, "SingleEnemy", "DealDamage -> ApplyDebuff(流血)", "重剑劈山升级，斩击附带血海腐蚀。")
add_skill("剑修", "血海剑尊", "血海领域", "主动", "霸体升级", 0, "1.2s", "0.3s", "15s", 45, "Self", "SpawnArea -> DealDamage(持续) -> ApplyDebuff", "霸体升级，展开血海领域持续伤害敌人。")
add_skill("剑修", "血海剑尊", "血剑", "主动", "震地升级", 6, "0.8s", "0.2s", "4s", 30, "SingleEnemy", "SpawnProjectile -> DealDamage", "震地升级，远程血气飞剑。")
add_skill("剑修", "血海剑尊", "血祭", "主动", "新技能", 0, "0.5s", "0.2s", "8s", 25, "Self", "ConsumeHP -> ApplyBuff(强化下次攻击)", "新技能，消耗 HP 强化下次攻击。")

# 狂战剑魔
add_skill("剑修", "狂战剑魔", "狂战斩", "普攻", "血海斩升级", 1, "0.8s", "0.2s", "0s", 0, "SingleEnemy", "DealDamage(低血增伤)", "血海斩升级，血量越低伤害越高。")
add_skill("剑修", "狂战剑魔", "越战越勇", "被动", "血海领域升级", 0, "-", "-", "-", 0, "Self", "ApplyBuff(战斗中持续加攻攻速)", "血海领域升华，战斗越久越强。")
add_skill("剑修", "狂战剑魔", "狂战连斩", "主动", "血剑升级", 1, "1.0s", "0.3s", "8s", 40, "SingleEnemy", "DealDamage x5", "血剑升级，近身快速五连斩。")
add_skill("剑修", "狂战剑魔", "濒死反击", "主动", "新技能", 0, "0.3s", "0.1s", "20s", 35, "Self", "ApplyBuff(无敌反击)", "新技能，濒死时短暂无敌并反击。")

# 血海修罗
add_skill("剑修", "血海修罗", "灭世一剑", "普攻", "狂战斩升级", 3, "1.5s", "0.5s", "0s", 0, "SingleEnemy", "SpawnArea -> DealDamage", "狂战斩升级，小范围灭世斩击。")
add_skill("剑修", "血海修罗", "血海滔天", "主动", "越战越勇升级", 0, "1.5s", "0.5s", "15s", 60, "Self", "SpawnArea -> DealDamage", "越战越勇升级，全屏血气爆发。")
add_skill("剑修", "血海修罗", "以命换命", "主动", "狂战连斩升级", 1, "1.0s", "0.3s", "12s", 45, "SingleEnemy", "ConsumeHP -> DealDamage", "狂战连斩升级，消耗大量 HP 造成巨额伤害。")
add_skill("剑修", "血海修罗", "修罗真身", "主动", "新技能", 0, "2.0s", "0.5s", "30s", 70, "Self", "ApplyBuff(大幅强化+吸血)", "新技能，化身血海修罗，全面强化。")

# 修罗剑祖（预留）
add_skill("剑修", "修罗剑祖", "修罗斩", "普攻", "灭世一剑升级", 3, "1.2s", "0.3s", "0s", 0, "SingleEnemy", "DealDamage -> Heal", "灭世一剑升级，攻击吸血。")
add_skill("剑修", "修罗剑祖", "修罗血气", "被动", "血海滔天升级", 0, "-", "-", "-", 0, "Self", "ApplyBuff(血气常驻增伤)", "血海滔天升华，血气常驻增伤。")
add_skill("剑修", "修罗剑祖", "修罗突刺", "主动", "以命换命升级", 5, "0.8s", "0.2s", "8s", 50, "SingleEnemy", "Teleport -> DealDamage", "以命换命升级，修罗突刺。")
add_skill("剑修", "修罗剑祖", "修罗领域", "主动", "新技能", 0, "1.5s", "0.5s", "20s", 65, "Self", "SpawnArea -> DealDamage -> Heal", "新技能，展开修罗领域，伤害并吸血。")

# 混沌剑魔（预留）
add_skill("剑修", "混沌剑魔", "混沌斩", "普攻", "修罗斩升级", 3, "1.0s", "0.3s", "0s", 0, "SingleEnemy", "DealDamage -> ApplyBuff(偷属性)", "修罗斩升级，攻击偷取属性。")
add_skill("剑修", "混沌剑魔", "混沌吞噬", "被动", "修罗血气升级", 0, "-", "-", "-", 0, "Self", "OnKill: ApplyBuff(永久加属性)", "修罗血气升华，击杀永久成长。")
add_skill("剑修", "混沌剑魔", "混沌血剑", "主动", "修罗突刺升级", 6, "0.8s", "0.2s", "6s", 50, "SingleEnemy", "SpawnProjectile -> DealDamage", "修罗突刺升级，远程混沌血剑。")
add_skill("剑修", "混沌剑魔", "混沌真身", "主动", "新技能", 0, "2.0s", "0.5s", "30s", 75, "Self", "ApplyBuff(大幅强化)", "新技能，化身混沌剑魔。")

# 天煞剑神（预留）
add_skill("剑修", "天煞剑神", "天煞斩", "普攻", "混沌斩升级", 5, "1.2s", "0.3s", "0s", 0, "SingleEnemy", "DealDamage(斩杀)", "混沌斩升级，高伤害斩杀。")
add_skill("剑修", "天煞剑神", "天煞气场", "被动", "混沌吞噬升级", 0, "-", "-", "-", 0, "Self", "ApplyBuff(煞气环绕反伤)", "混沌吞噬升华，煞气环绕反伤。")
add_skill("剑修", "天煞剑神", "天煞剑气", "主动", "混沌血剑升级", 8, "1.0s", "0.3s", "8s", 60, "AllEnemiesInRange", "SpawnArea -> DealDamage", "混沌血剑升级，范围天煞剑气。")
add_skill("剑修", "天煞剑神", "天煞灭世", "主动", "新技能", 0, "3.0s", "0.5s", "60s", 100, "Self", "SpawnArea -> DealDamage", "新技能，天煞灭世，全屏毁灭打击。")

# ========== 法修 ==========
# 法修也尽量体现升级线，但相对简化
add_skill("法修", "法徒", "火球术", "普攻", "初始", 5, "0.8s", "0.2s", "0s", 0, "SingleEnemy", "SpawnProjectile -> DealDamage", "最基础的火球投射。")
add_skill("法修", "法徒", "凝聚法力", "主动", "初始", 0, "0.5s", "0.2s", "6s", 10, "Self", "ApplyBuff(下次法术增幅)", "凝聚法力，使下次法术伤害提升。")
add_skill("法修", "法徒", "水弹", "主动", "初始", 5, "0.6s", "0.2s", "2s", 10, "SingleEnemy", "SpawnProjectile -> DealDamage", "低阶水系法术。")

add_skill("法修", "五行术士", "大火球", "普攻", "火球术升级", 6, "0.8s", "0.2s", "0s", 0, "SingleEnemy", "SpawnProjectile -> DealDamage", "火球术升级，更大的火球。")
add_skill("法修", "五行术士", "元素融合", "主动", "凝聚法力升级", 6, "1.0s", "0.3s", "6s", 25, "SingleEnemy", "SpawnProjectile -> ApplyDebuff -> DealDamage", "凝聚法力升级，组合元素触发反应。")
add_skill("法修", "五行术士", "火海", "主动", "水弹升级", 6, "1.2s", "0.3s", "8s", 30, "GroundPos", "SpawnArea -> DealDamage", "水弹升级（反差），地面持续燃烧 AOE。")

add_skill("法修", "单极法师", "极火球", "普攻", "大火球升级", 6, "1.0s", "0.3s", "0s", 0, "SingleEnemy", "SpawnProjectile -> DealDamage", "大火球升级，纯火系高伤害。")
add_skill("法修", "单极法师", "元素专注", "主动", "元素融合升级", 0, "0.5s", "0.2s", "10s", 20, "Self", "ApplyBuff(加MATK)", "元素融合专精，提升法术攻击。")
add_skill("法修", "单极法师", "烈焰冲击", "主动", "火海升级", 6, "1.0s", "0.3s", "6s", 35, "GroundPos", "SpawnArea -> DealDamage", "火海升级，爆发性火焰冲击。")

add_skill("法修", "万象真人", "连锁火球", "普攻", "极火球升级", 6, "0.8s", "0.2s", "0s", 0, "SingleEnemy", "SpawnProjectile -> DealDamage(弹射)", "极火球升级，火球可弹射多个目标。")
add_skill("法修", "万象真人", "冰火两重天", "主动", "元素专注升级", 6, "1.5s", "0.5s", "12s", 40, "GroundPos", "SpawnArea -> ApplyDebuff -> DealDamage", "元素专注升级，冰火组合范围伤害。")
add_skill("法修", "万象真人", "雷水交加", "主动", "烈焰冲击升级", 6, "1.2s", "0.3s", "10s", 35, "GroundPos", "SpawnArea -> ApplyDebuff(麻痹) -> DealDamage", "烈焰冲击升级，雷水组合控制伤害。")

add_skill("法修", "焚天/玄冰使", "焚天之火", "普攻", "连锁火球升级", 6, "1.0s", "0.3s", "0s", 0, "SingleEnemy", "SpawnProjectile -> ApplyDebuff(燃烧)", "连锁火球升级，附带燃烧效果。")
add_skill("法修", "焚天/玄冰使", "玄冰", "主动", "冰火两重天升级", 6, "1.0s", "0.3s", "8s", 35, "SingleEnemy", "SpawnProjectile -> ApplyDebuff(冰冻)", "冰火两重天分支，纯冰系冰冻控制。")
add_skill("法修", "焚天/玄冰使", "焚天", "主动", "雷水交加升级", 6, "1.2s", "0.3s", "10s", 40, "GroundPos", "SpawnArea -> ApplyDebuff(燃烧)", "雷水交加分支，纯火系持续燃烧 AOE。")

add_skill("法修", "天道法神", "天火", "普攻", "焚天之火升级", 8, "1.0s", "0.3s", "0s", 0, "SingleEnemy", "SpawnProjectile -> DealDamage", "焚天之火升级，远程天火。")
add_skill("法修", "天道法神", "陨石术", "主动", "玄冰升级", 8, "2.0s", "0.5s", "15s", 60, "GroundPos", "SpawnArea -> DealDamage", "玄冰升级（反差），大范围陨石打击。")
add_skill("法修", "天道法神", "暴风雪", "主动", "焚天升级", 8, "1.5s", "0.3s", "12s", 50, "GroundPos", "SpawnArea -> DealDamage -> ApplyDebuff(减速)", "焚天升级（反差），持续暴风雪减速。")

add_skill("法修", "森罗幻尊", "幻术·惑", "主动", "天火升级", 6, "1.0s", "0.3s", "10s", 40, "SingleEnemy", "ApplyDebuff(混乱)", "天火分支幻术，精神控制敌人。")
add_skill("法修", "森罗幻尊", "精神冲击", "主动", "陨石术分支", 5, "0.8s", "0.2s", "5s", 30, "SingleEnemy", "DealDamage", "直接精神冲击。")
add_skill("法修", "森罗幻尊", "幻境", "主动", "暴风雪分支", 0, "1.5s", "0.5s", "20s", 50, "Self", "SpawnArea -> ApplyDebuff(混乱)", "暴风雪分支，范围幻境混乱敌人。")

add_skill("法修", "九幽魔尊", "剧毒术", "主动", "幻术·惑分支", 6, "0.8s", "0.2s", "5s", 30, "SingleEnemy", "SpawnProjectile -> ApplyDebuff(中毒)", "幻术分支转暗黑，剧毒法术。")
add_skill("法修", "九幽魔尊", "生命吞噬", "主动", "精神冲击升级", 4, "1.0s", "0.3s", "8s", 35, "SingleEnemy", "DealDamage -> Heal", "精神冲击升级，伤害并回血。")
add_skill("法修", "九幽魔尊", "毒雾", "主动", "幻境分支", 0, "1.0s", "0.3s", "10s", 35, "Self", "SpawnArea -> ApplyDebuff(中毒)", "幻境分支转毒，范围毒雾。")

add_skill("法修", "紫霄雷尊", "瞬发雷暴", "主动", "剧毒术升级", 6, "0.3s", "0.1s", "4s", 35, "SingleEnemy", "SpawnProjectile -> DealDamage", "剧毒术升级（反差），极速雷暴。")
add_skill("法修", "紫霄雷尊", "雷池", "主动", "生命吞噬升级", 6, "1.2s", "0.3s", "10s", 45, "GroundPos", "SpawnArea -> DealDamage", "生命吞噬升级（反差），范围雷池。")
add_skill("法修", "紫霄雷尊", "天雷引", "主动", "毒雾升级", 8, "1.0s", "0.3s", "8s", 40, "GroundPos", "SpawnArea -> DealDamage", "毒雾升级（反差），引导天雷范围打击。")

add_skill("法修", "造化法祖", "言出法随", "主动", "瞬发雷暴升级", 0, "1.0s", "0.3s", "30s", 80, "Self", "ApplyBuff(无读条)", "瞬发雷暴升华，短时间内所有技能瞬发。")
add_skill("法修", "造化法祖", "规则改写", "主动", "雷池升级", 8, "1.5s", "0.5s", "15s", 60, "GroundPos", "SpawnArea -> ApplyDebuff(禁锢)", "雷池升级，改写战场规则范围禁锢。")
add_skill("法修", "造化法祖", "万法归源", "主动", "天雷引升级", 0, "2.0s", "0.5s", "25s", 80, "Self", "ApplyBuff(全技能强化)", "天雷引升华，全面强化法术效果。")

# ========== 体修 ==========
add_skill("体修", "武徒", "拳脚", "普攻", "初始", 1, "0.5s", "0.1s", "0s", 0, "SingleEnemy", "DealDamage", "近战拳脚攻击。")
add_skill("体修", "武徒", "蓄力", "主动", "初始", 0, "0.5s", "0.2s", "6s", 10, "Self", "ApplyBuff(下次攻击增幅)", "蓄力，使下次攻击伤害提升。")
add_skill("体修", "武徒", "硬抗", "主动", "初始", 0, "0.5s", "0.2s", "8s", 10, "Self", "ApplyBuff(减伤)", "短暂提升防御。")

add_skill("体修", "铁骨武者", "铁骨拳", "普攻", "拳脚升级", 1, "0.5s", "0.1s", "0s", 0, "SingleEnemy", "DealDamage", "拳脚升级，铁骨拳势。")
add_skill("体修", "铁骨武者", "铁骨", "主动", "蓄力升级", 0, "0.5s", "0.2s", "10s", 15, "Self", "ApplyBuff(加防)", "蓄力升级，大幅提升护甲。")
add_skill("体修", "铁骨武者", "嘲讽", "主动", "硬抗升级", 4, "0.5s", "0.2s", "8s", 15, "Self", "ApplyDebuff(嘲讽)", "硬抗升级，嘲讽周围敌人。")

add_skill("体修", "狂血斗士", "狂击", "普攻", "铁骨拳升级", 1, "0.4s", "0.1s", "0s", 0, "SingleEnemy", "DealDamage x2", "铁骨拳升级，快速二连击。")
add_skill("体修", "狂血斗士", "血怒", "主动", "铁骨升级分支", 0, "0.5s", "0.2s", "10s", 15, "Self", "ApplyBuff(加攻)", "铁骨分支，激发血气提升攻击。")
add_skill("体修", "狂血斗士", "冲锋", "主动", "嘲讽分支", 5, "0.8s", "0.2s", "8s", 20, "SingleEnemy", "Teleport -> DealDamage", "嘲讽分支，冲锋撞击敌人。")

add_skill("体修", "金刚力士", "金刚拳", "普攻", "铁骨拳升级", 1, "0.8s", "0.2s", "0s", 0, "SingleEnemy", "DealDamage", "铁骨拳升级，势大力沉。")
add_skill("体修", "金刚力士", "金刚不坏", "主动", "铁骨升级", 0, "1.0s", "0.3s", "15s", 30, "Self", "ApplyBuff(大幅减伤)", "铁骨升级，大幅度减伤。")
add_skill("体修", "金刚力士", "反弹", "被动", "嘲讽升级", 0, "-", "-", "-", 0, "Self", "ApplyBuff(反伤)", "嘲讽升级，反弹近战伤害。")

add_skill("体修", "修罗武者", "修罗拳", "普攻", "狂击升级", 1, "0.5s", "0.1s", "0s", 0, "SingleEnemy", "DealDamage(低血增伤)", "狂击升级，血量越低伤害越高。")
add_skill("体修", "修罗武者", "血怒爆发", "被动", "血怒升级", 0, "-", "-", "-", 0, "Self", "ApplyBuff(低血加攻)", "血怒升华，血量越低攻击力越高。")
add_skill("体修", "修罗武者", "吸血拳", "主动", "冲锋升级", 1, "0.8s", "0.2s", "5s", 25, "SingleEnemy", "DealDamage -> Heal", "冲锋升级，攻击吸血。")

add_skill("体修", "不动明王", "明王拳", "普攻", "金刚拳升级", 1, "0.8s", "0.2s", "0s", 0, "SingleEnemy", "DealDamage", "金刚拳升级，明王拳势。")
add_skill("体修", "不动明王", "绝对防御", "主动", "金刚不坏升级", 0, "1.2s", "0.3s", "20s", 40, "Self", "ApplyBuff(全队减伤)", "金刚不坏升级，为周围队友减伤。")
add_skill("体修", "不动明王", "群体嘲讽", "主动", "反弹升级", 5, "0.8s", "0.2s", "12s", 30, "Self", "ApplyDebuff(嘲讽)", "反弹升级，范围嘲讽。")

add_skill("体修", "大地灵尊", "地裂拳", "普攻", "明王拳升级", 2, "0.8s", "0.2s", "0s", 0, "SingleEnemy", "DealDamage", "明王拳升级，附带震地效果。")
add_skill("体修", "大地灵尊", "石墙", "主动", "绝对防御升级", 5, "1.0s", "0.3s", "15s", 35, "GroundPos", "SpawnArea(阻挡)", "绝对防御分支，升起石墙改变地形。")
add_skill("体修", "大地灵尊", "岩化身", "主动", "群体嘲讽升级", 0, "1.5s", "0.3s", "20s", 45, "Self", "ApplyBuff(防御大幅提升/无法移动)", "群体嘲讽分支，变成石头兵营。")

add_skill("体修", "八极武圣", "寸劲", "主动", "地裂拳升级", 2, "0.5s", "0.2s", "4s", 25, "SingleEnemy", "DealDamage", "地裂拳升级，短距离高爆发拳劲。")
add_skill("体修", "八极武圣", "连打", "主动", "石墙分支", 1, "1.0s", "0.3s", "8s", 35, "SingleEnemy", "DealDamage x4 -> ApplyDebuff(眩晕)", "石墙分支，多段连打眩晕。")
add_skill("体修", "八极武圣", "崩山", "主动", "岩化身分支", 2, "1.2s", "0.3s", "10s", 40, "SingleEnemy", "DealDamage -> Knockback", "岩化身分支，崩山一击击退。")

add_skill("体修", "吞天血煞", "血煞拳", "普攻", "修罗拳升级", 1, "0.6s", "0.2s", "0s", 0, "SingleEnemy", "DealDamage -> Heal", "修罗拳升级，攻击吸血。")
add_skill("体修", "吞天血煞", "吞噬", "被动", "血怒爆发升级", 0, "-", "-", "-", 0, "Self", "OnKill: Heal + 偷属性", "血怒爆发升华，击杀回血偷属性。")
add_skill("体修", "吞天血煞", "血煞掌", "主动", "吸血拳升级", 0, "1.0s", "0.3s", "8s", 35, "Self", "SpawnArea -> DealDamage", "吸血拳升级，范围血煞掌劲。")

add_skill("体修", "万劫肉身", "万劫拳", "普攻", "血煞拳升级", 2, "1.0s", "0.2s", "0s", 0, "SingleEnemy", "DealDamage -> SpawnArea(普攻AOE)", "血煞拳升级，普攻附带范围冲击波。")
add_skill("体修", "万劫肉身", "不朽", "主动", "吞噬升级", 0, "1.0s", "0.3s", "30s", 60, "Self", "ApplyBuff(霸体+免控)", "吞噬升华，免疫所有控制。")
add_skill("体修", "万劫肉身", "肉身成神", "主动", "血煞掌升级", 0, "2.0s", "0.5s", "25s", 70, "Self", "ApplyBuff(大幅减伤+反伤)", "血煞掌升华，肉身成神全面强化。")

# ========== 器修 ==========
add_skill("器修", "器徒", "机关鸟", "主动", "初始", 0, "0.5s", "0.2s", "10s", 10, "Self", "SpawnProjectile(侦查)", "召唤机关鸟侦查。")
add_skill("器修", "器徒", "回春丹", "主动", "初始", 4, "0.8s", "0.2s", "6s", 15, "SingleAlly", "Heal", "单体回血。")
add_skill("器修", "器徒", "掷石", "普攻", "初始", 4, "0.6s", "0.1s", "0s", 0, "SingleEnemy", "SpawnProjectile -> DealDamage", "投掷石块攻击。")

add_skill("器修", "炼丹师", "抛丹·回血", "主动", "回春丹升级", 5, "1.0s", "0.3s", "8s", 25, "GroundPos", "SpawnArea -> Heal", "回春丹升级，范围回血。")
add_skill("器修", "炼丹师", "抛丹·Buff", "主动", "机关鸟升级", 5, "1.0s", "0.3s", "10s", 25, "GroundPos", "SpawnArea -> ApplyBuff", "机关鸟分支，范围加 Buff。")
add_skill("器修", "炼丹师", "毒丹", "主动", "掷石升级", 5, "0.8s", "0.2s", "6s", 20, "SingleEnemy", "SpawnProjectile -> ApplyDebuff(中毒)", "掷石升级，投掷毒丹。")

add_skill("器修", "御器使", "召唤傀儡", "主动", "机关鸟升级", 0, "1.0s", "0.3s", "15s", 30, "Self", "SpawnProjectile(召唤物)", "机关鸟升级，召唤近战傀儡。")
add_skill("器修", "御器使", "傀儡修理", "主动", "抛丹·回血分支", 5, "0.8s", "0.2s", "6s", 20, "SingleAlly", "Heal", "抛丹分支，治疗召唤物。")
add_skill("器修", "御器使", "机关弩", "普攻", "毒丹升级", 5, "0.6s", "0.1s", "0s", 0, "SingleEnemy", "SpawnProjectile -> DealDamage", "毒丹分支，机关弩远程攻击。")

add_skill("器修", "丹鼎宗师", "瞬回丹", "主动", "抛丹·回血升级", 5, "0.8s", "0.2s", "8s", 35, "SingleAlly", "Heal", "抛丹回血升级，单体大量回血。")
add_skill("器修", "丹鼎宗师", "群疗", "主动", "抛丹·Buff升级", 5, "1.2s", "0.3s", "12s", 40, "GroundPos", "SpawnArea -> Heal", "抛丹 Buff 升级，范围持续回血。")
add_skill("器修", "丹鼎宗师", "净化", "主动", "傀儡修理升级", 5, "0.5s", "0.2s", "10s", 25, "SingleAlly", "ApplyBuff(净化)", "傀儡修理分支，解除负面状态。")
add_skill("器修", "丹鼎宗师", "丹鼎护体", "主动", "机关弩升级", 0, "0.5s", "0.2s", "10s", 25, "Self", "ApplyBuff(护盾)", "机关弩分支，丹鼎护盾。")

add_skill("器修", "千机宗师", "机甲附体", "主动", "召唤傀儡升级", 0, "1.5s", "0.5s", "20s", 50, "Self", "ApplyBuff(大幅强化)", "召唤傀儡升级，驾驶机甲。")
add_skill("器修", "千机宗师", "机关炮", "主动", "机关弩升级", 6, "1.0s", "0.3s", "6s", 35, "SingleEnemy", "SpawnProjectile -> DealDamage", "机关弩升级，机甲远程炮击。")
add_skill("器修", "千机宗师", "机甲修复", "主动", "净化分支", 0, "1.0s", "0.3s", "10s", 30, "Self", "Heal", "净化分支，机甲自我修复。")

add_skill("器修", "造化丹皇", "复生丹", "主动", "瞬回丹升级", 5, "2.0s", "0.5s", "60s", 80, "SingleAlly", "Heal(复活)", "瞬回丹升级，单体复活。")
add_skill("器修", "造化丹皇", "群体复活", "主动", "群疗升级", 5, "2.5s", "0.5s", "120s", 120, "GroundPos", "SpawnArea -> Heal(复活)", "群疗升级，范围复活。")
add_skill("器修", "造化丹皇", "造化之光", "主动", "丹鼎护体升级", 0, "1.5s", "0.5s", "20s", 60, "Self", "ApplyBuff(全队增益)", "丹鼎护体升华，全队增益。")

add_skill("器修", "毒瘴散人", "毒瘴", "主动", "毒丹升级", 5, "1.0s", "0.3s", "10s", 35, "GroundPos", "SpawnArea -> ApplyDebuff(中毒)", "毒丹升级，范围剧毒。")
add_skill("器修", "毒瘴散人", "以毒攻毒", "主动", "机关炮分支", 6, "0.8s", "0.2s", "5s", 25, "SingleEnemy", "SpawnProjectile -> DealDamage", "机关炮分支，对中毒敌人额外伤害。")
add_skill("器修", "毒瘴散人", "毒爆", "主动", "机甲修复分支", 5, "1.2s", "0.3s", "10s", 40, "GroundPos", "SpawnArea -> DealDamage", "机甲修复分支，引爆毒素范围伤害。")

add_skill("器修", "万机神君", "海陆空傀儡", "主动", "召唤傀儡升级", 0, "1.5s", "0.3s", "20s", 50, "Self", "SpawnProjectile x3(召唤物)", "召唤傀儡升级，召唤三种傀儡。")
add_skill("器修", "万机神君", "火力覆盖", "主动", "机关炮升级", 8, "1.5s", "0.5s", "12s", 55, "GroundPos", "SpawnArea -> DealDamage", "机关炮升级，大范围火力覆盖。")
add_skill("器修", "万机神君", "傀儡超载", "主动", "毒爆分支", 0, "1.0s", "0.3s", "15s", 45, "Self", "ApplyBuff(召唤物强化)", "毒爆分支，强化所有召唤物。")

add_skill("器修", "御兽天师", "驯化大妖", "主动", "海陆空傀儡升级", 0, "1.2s", "0.3s", "18s", 45, "Self", "SpawnProjectile(召唤物)", "海陆空傀儡分支，召唤灵兽。")
add_skill("器修", "御兽天师", "人兽合击", "主动", "火力覆盖分支", 6, "1.0s", "0.3s", "8s", 35, "SingleEnemy", "SpawnProjectile -> DealDamage", "火力覆盖分支，与灵兽合击。")
add_skill("器修", "御兽天师", "兽魂附体", "主动", "傀儡超载分支", 0, "1.5s", "0.5s", "20s", 50, "Self", "ApplyBuff(人兽合一)", "傀儡超载分支，兽魂附体强化自身。")

add_skill("器修", "创世灵尊", "神兵降临", "主动", "驯化大妖升级", 0, "2.0s", "0.5s", "30s", 80, "Self", "SpawnProjectile(强力召唤物)", "驯化大妖升华，召唤上古神兵。")
add_skill("器修", "创世灵尊", "神兽虚影", "主动", "人兽合击升级", 0, "2.0s", "0.5s", "20s", 100, "Self", "SpawnArea -> DealDamage -> Heal(全队)", "人兽合击升华，召唤神兽虚影。")
add_skill("器修", "创世灵尊", "创世之光", "主动", "兽魂附体升级", 0, "2.5s", "0.5s", "30s", 100, "Self", "ApplyBuff(全队无敌/复活)", "兽魂附体升华，全队保护。")

# 写入 xlsx
path = "docs/design/修仙职业成长体系/class-advancement-design.xlsx"
wb = load_workbook(path)

if "职业技能表" in wb.sheetnames:
    del wb["职业技能表"]
ws = wb.create_sheet("职业技能表")

headers = ["职业大类", "职业阶段", "技能名称", "技能类型", "升级来源", "施法距离", "读条时间", "后摇时间", "冷却时间", "MP消耗", "目标类型", "Action序列", "技能描述"]
ws.append(headers)
for row in skills:
    ws.append(row)

header_font = Font(bold=True, color="FFFFFF")
header_fill = PatternFill("solid", fgColor="4F81BD")
header_align = Alignment(horizontal="center", vertical="center", wrap_text=True)
cell_align = Alignment(horizontal="left", vertical="center", wrap_text=True)
thin = Side(style="thin", color="CCCCCC")
border = Border(left=thin, right=thin, top=thin, bottom=thin)

for col in range(1, len(headers) + 1):
    cell = ws.cell(row=1, column=col)
    cell.font = header_font
    cell.fill = header_fill
    cell.alignment = header_align
    cell.border = border

for row in ws.iter_rows(min_row=2, max_row=ws.max_row, min_col=1, max_col=len(headers)):
    for cell in row:
        cell.alignment = cell_align
        cell.border = border

widths = [10, 16, 16, 10, 16, 10, 10, 10, 10, 10, 16, 30, 45]
for i, w in enumerate(widths, 1):
    ws.column_dimensions[get_column_letter(i)].width = w

ws.freeze_panes = "A2"
wb.save(path)
print(f"saved {len(skills)} skills")
