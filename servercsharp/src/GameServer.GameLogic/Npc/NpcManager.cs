using GameServer.Services.Core;
using GameServer.Services.World;
using GameServer.Tables;
using Microsoft.Extensions.Logging;

namespace GameServer.GameLogic.Npc;

/// <summary>
/// NPC 管理器 — 初始化时从 Luban 配置表加载 NPC 并加入地图，提供查询接口
/// </summary>
public class NpcManager : INpcManager
{
    private readonly WorldState _worldState;
    private readonly LubanTableLoader _tables;
    private readonly ILogger<NpcManager> _logger;
    private long _nextNpcSeq = 1;

    public NpcManager(WorldState worldState, LubanTableLoader tables, ILogger<NpcManager> logger)
    {
        _worldState = worldState;
        _tables = tables;
        _logger = logger;
    }

    /// <summary>
    /// 初始化所有 NPC，将它们加入对应地图
    /// </summary>
    public void Init()
    {
        foreach (var (mapName, _) in _tables.MapConfigs.Values.ToDictionary(m => m.MapName, m => m.Id))
        {
            var spawns = _tables.GetNpcSpawnsForMap(mapName);
            foreach (var spawn in spawns)
            {
                var npcTemplate = _tables.GetNpc(spawn.NpcId);
                if (npcTemplate == null)
                {
                    _logger.LogWarning("[Npc] NPC template not found: npcId={Id}", spawn.NpcId);
                    continue;
                }

                var map = _worldState.GetMapState(mapName);
                if (map == null)
                {
                    _logger.LogWarning("[Npc] Map {Map} not found, skipping NPC {Name}", mapName, npcTemplate.Name);
                    continue;
                }

                var instanceId = _nextNpcSeq++;
                int sizeX = 1;
                int sizeY = 1;
                var corrected = _worldState.FindNearestWalkableForFootprint(mapName, spawn.X, spawn.Y, sizeX, sizeY, requireVacant: false);
                int x = corrected?.x ?? spawn.X;
                int y = corrected?.y ?? spawn.Y;
                if (x != spawn.X || y != spawn.Y)
                {
                    _logger.LogWarning("[Npc] spawn corrected: {Name} at {Map} from ({OldX},{OldY}) to ({NewX},{NewY}) footprint={SizeX}x{SizeY}",
                        npcTemplate.Name, mapName, spawn.X, spawn.Y, x, y, sizeX, sizeY);
                }

                var npc = new MapNpcState
                {
                    InstanceId = instanceId,
                    NpcId = spawn.NpcId,
                    Name = npcTemplate.Name,
                    NpcType = npcTemplate.NpcType,
                    X = x,
                    Y = y,
                    SizeX = sizeX,
                    SizeY = sizeY,
                };
                _worldState.NpcEnter(mapName, npc);

                _logger.LogInformation("[Npc] Initialized NPC {Name} (type={Type}) at {Map} ({X},{Y}), instanceId={InstId}",
                    npcTemplate.Name, npcTemplate.NpcType, mapName, x, y, instanceId);
            }
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
