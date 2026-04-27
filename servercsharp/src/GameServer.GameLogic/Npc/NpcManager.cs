using GameServer.Services.Core;
using GameServer.Services.World;
using Microsoft.Extensions.Logging;

namespace GameServer.GameLogic.Npc;

/// <summary>
/// NPC 静态定义
/// </summary>
public class NpcDef
{
    public int NpcId { get; set; }
    public string Name { get; set; } = "";
    public string MapName { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int NpcType { get; set; }  // NPC类型枚举
}

/// <summary>
/// NPC 管理器 — 初始化时将 NPC 加入地图，提供查询接口
/// </summary>
public class NpcManager : INpcManager
{
    private readonly WorldState _worldState;
    private readonly ILogger<NpcManager> _logger;
    private long _nextNpcSeq = 1;

    /// <summary>
    /// 硬编码的 NPC 列表（后续可迁移到配置表）
    /// </summary>
    private static readonly List<NpcDef> NpcDefs = new()
    {
        new NpcDef { NpcId = 1, Name = "转职大师", MapName = "xinshoucun", X = 25, Y = 24, NpcType = (int)NpcType.JobMaster },
    };

    public NpcManager(WorldState worldState, ILogger<NpcManager> logger)
    {
        _worldState = worldState;
        _logger = logger;
    }

    /// <summary>
    /// 初始化所有 NPC，将它们加入对应地图
    /// </summary>
    public void Init()
    {
        foreach (var def in NpcDefs)
        {
            var map = _worldState.GetMapState(def.MapName);
            if (map == null)
            {
                _logger.LogWarning("[Npc] Map {Map} not found, skipping NPC {Name}", def.MapName, def.Name);
                continue;
            }
            var instanceId = _nextNpcSeq++;

            map.Npcs[instanceId] = new MapNpcState
            {
                InstanceId = instanceId,
                NpcId = def.NpcId,
                Name = def.Name,
                NpcType = def.NpcType,
                X = def.X,
                Y = def.Y,
            };

            _logger.LogInformation("[Npc] Initialized NPC {Name} (type={Type}) at {Map} ({X},{Y}), instanceId={InstId}",
                def.Name, def.NpcType, def.MapName, def.X, def.Y, instanceId);
        }
    }

    /// <summary>
    /// 获取指定地图上的 NPC 列表
    /// </summary>
    public List<MapNpcState> GetNpcsOnMap(string mapName)
    {
        var map = _worldState.GetMapState(mapName);
        return map?.Npcs.Values.ToList() ?? new List<MapNpcState>();
    }
}
