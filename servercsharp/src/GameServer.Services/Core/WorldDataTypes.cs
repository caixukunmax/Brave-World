namespace GameServer.Services.Core;

/// <summary>
/// 玩家在地图上的轻量状态 — 统一 PlayerSnapshot + PlayerState
/// </summary>
public class MapPlayerState
{
    public long AccountId { get; set; }
    public long RoleId { get; set; }
    public string RoleName { get; set; } = "";
    public int ServerId { get; set; }
    public int GridX { get; set; }
    public int GridY { get; set; }
    public int Level { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Mp { get; set; }
    public int MaxMp { get; set; }
    public int Agility { get; set; }
    public int Patk { get; set; }
    public int Matk { get; set; }
    public int Pdef { get; set; }
    public int Mdef { get; set; }
    public string Job { get; set; } = "";
    public int MoveSpeedMs { get; set; }
}

/// <summary>
/// 怪物在地图上的轻量状态
/// </summary>
public class MapMonsterState
{
    public long InstanceId { get; set; }
    public int MonsterId { get; set; }
    public string Name { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Level { get; set; }
    // 战斗属性
    public int Patk { get; set; } = 10;
    public int Matk { get; set; } = 10;
    public int Pdef { get; set; } = 5;
    public int Mdef { get; set; } = 5;
    public int Agility { get; set; } = 100;
}

/// <summary>
/// 单张地图的运行时状态
/// </summary>
public class MapInstance
{
    public int MapId { get; set; }
    public Dictionary<long, MapPlayerState> Players { get; } = new();
    public Dictionary<long, MapMonsterState> Monsters { get; } = new();
}
