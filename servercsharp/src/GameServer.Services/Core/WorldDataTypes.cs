using GameServer.Common;
using GameServer.Common.Buffs;
using System.Collections.Concurrent;
namespace GameServer.Services.Core;

/// <summary>
/// 战斗实体基类 — 玩家和怪物共享的战斗属性
/// 底层用字典存储（枚举驱动），上层暴露强类型属性（便捷访问）
/// 新增属性：加枚举值 + 加一行属性声明，不需要改逻辑
/// </summary>
public class CombatEntityState
{
    /// <summary>属性字典 — key 为 EAttr 枚举值</summary>
    private readonly Dictionary<int, int> _attrs = new();

    /// <summary>战斗有效位置（双格区间时为多个格子）</summary>
    public List<(int x, int y)> CombatPositions { get; set; } = new();

    public CombatEntityState()
    {
        // 初始化所有属性为 0（确保字典里有 key）
        foreach (var (key, _) in RoleAttrs.All)
            _attrs[key] = 0;
    }

    // ---- 字典访问 ----

    /// <summary>按枚举 key 读写属性</summary>
    public int GetAttr(int key) => _attrs.GetValueOrDefault(key, 0);
    public void SetAttr(int key, int value) => _attrs[key] = value;

    /// <summary>按枚举 key 增减属性值，返回新值</summary>
    public int AddAttr(int key, int delta)
    {
        _attrs.TryGetValue(key, out int cur);
        _attrs[key] = cur + delta;
        return cur + delta;
    }

    // ---- 强类型便捷属性（和 RoleAttrs 枚举对齐） ----

    public int Hp  { get => GetAttr((int)RoleAttrs.Hp);  set => SetAttr((int)RoleAttrs.Hp, value); }
    public int MaxHp { get => GetAttr((int)RoleAttrs.MaxHp); set => SetAttr((int)RoleAttrs.MaxHp, value); }
    public int Mp  { get => GetAttr((int)RoleAttrs.Mp);  set => SetAttr((int)RoleAttrs.Mp, value); }
    public int MaxMp { get => GetAttr((int)RoleAttrs.MaxMp); set => SetAttr((int)RoleAttrs.MaxMp, value); }
    public int Patk { get => GetAttr((int)RoleAttrs.PAtk); set => SetAttr((int)RoleAttrs.PAtk, value); }
    public int Matk { get => GetAttr((int)RoleAttrs.MAtk); set => SetAttr((int)RoleAttrs.MAtk, value); }
    public int Pdef { get => GetAttr((int)RoleAttrs.PDef); set => SetAttr((int)RoleAttrs.PDef, value); }
    public int Mdef { get => GetAttr((int)RoleAttrs.MDef); set => SetAttr((int)RoleAttrs.MDef, value); }
    public int Level { get; set; }

    // ---- 便捷方法 ----

    /// <summary>是否存活</summary>
    public bool IsAlive => Hp > 0;

    /// <summary>受到伤害，返回实际伤害值</summary>
    public int TakeDamage(int damage)
    {
        int actual = Math.Min(damage, Hp);
        Hp -= actual;
        return actual;
    }

    /// <summary>恢复HP，不超过MaxHp，返回实际恢复量</summary>
    public int HealHp(int amount)
    {
        int actual = Math.Min(amount, MaxHp - Hp);
        Hp += actual;
        return actual;
    }

    /// <summary>消耗MP，返回是否成功</summary>
    public bool ConsumeMp(int amount)
    {
        if (Mp < amount) return false;
        Mp -= amount;
        return true;
    }

    /// <summary>恢复MP，不超过MaxMp，返回实际恢复量</summary>
    public int HealMp(int amount)
    {
        int actual = Math.Min(amount, MaxMp - Mp);
        Mp += actual;
        return actual;
    }

    /// <summary>从另一个字典批量设置属性</summary>
    public void LoadAttrs(Dictionary<int, int> source)
    {
        foreach (var (k, v) in source)
            _attrs[k] = v;
    }

    /// <summary>导出所有属性为字典</summary>
    public Dictionary<int, int> ExportAttrs() => new(_attrs);
}

/// <summary>
/// 玩家在地图上的轻量状态 — 统一 PlayerSnapshot + PlayerState
/// </summary>
public class MapPlayerState : CombatEntityState
{
    public long AccountId { get; set; }
    public long RoleId { get; set; }
    public string RoleName { get; set; } = "";
    public int ServerId { get; set; }
    public int GridX { get; set; }
    public int GridY { get; set; }
    public int SizeX { get; set; } = 1;
    public int SizeY { get; set; } = 1;
    public int MpRegen { get => GetAttr((int)RoleAttrs.MpRegen); set => SetAttr((int)RoleAttrs.MpRegen, value); }
    public string Job { get; set; } = "";
    public int MoveSpeedMs { get; set; }
    public List<int> EquippedSkills { get; set; } = new();

    /// <summary>优先释放的技能 ID（0=无优先）— 由客户端技能栏选中设置</summary>
    public int PreferredSkillId { get; set; } = 0;

    /// <summary>Buff 容器 — 非战斗状态下也能挂载 Buff</summary>
    public BuffContainer Buffs { get; set; } = new();

    /// <summary>是否在战斗中 — 由 CombatManager 进入/脱战时设置</summary>
    public bool InCombat { get; set; } = false;
}

/// <summary>
/// 怪物在地图上的轻量状态
/// </summary>
public class MapMonsterState : CombatEntityState
{
    public long InstanceId { get; set; }
    public int MonsterId { get; set; }
    public string Name { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int SizeX { get; set; } = 1;
    public int SizeY { get; set; } = 1;
}

/// <summary>
/// NPC在地图上的轻量状态 — 继承 CombatEntityState，NPC 也可以参与战斗
/// </summary>
public class MapNpcState : CombatEntityState
{
    public long InstanceId { get; set; }
    public int NpcId { get; set; }  // 模板ID（配置表ID）
    public string Name { get; set; } = "";
    public int NpcType { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int SizeX { get; set; } = 1;
    public int SizeY { get; set; } = 1;

    /// <summary>NPC 是否处于战斗状态（对话触发）</summary>
    public bool InCombat { get; set; } = false;
}

/// <summary>
/// 单张地图的运行时状态
/// </summary>
public class MapState
{
    public int MapId { get; set; }
    public ConcurrentDictionary<long, MapPlayerState> Players { get; } = new();
    public ConcurrentDictionary<long, MapMonsterState> Monsters { get; } = new();
    public ConcurrentDictionary<long, MapNpcState> Npcs { get; } = new();

    /// <summary>
    /// 空间索引：格子 → 该格上的所有实体 ID（玩家、怪物、NPC）。
    /// 与 Players/Monsters/Npcs 同步维护，用于 O(1) 级别的占用/敌人查询。
    /// </summary>
    public Dictionary<(int x, int y), HashSet<long>> GridEntities { get; } = new();
}
