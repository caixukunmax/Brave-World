using GameServer.Common.Config;
using GameServer.Services.Core;

namespace GameServer.Services.Monster.AI;

/// <summary>
/// 巡逻行为：在出生点附近随机游走
/// </summary>
public class PatrolBehavior : IBehaviorHandler
{
    private readonly MapDataProvider _mapData;
    private static readonly Random _rng = new();

    public PatrolBehavior(MapDataProvider mapData) => _mapData = mapData;

    public (int x, int y)? Run(MonsterRuntimeState m, string mapName, Dictionary<long, PlayerStateView> players)
    {
        long now = Environment.TickCount64;
        if (m.LastMoveTime > 0 && (now - m.LastMoveTime) < (m.AiConfig.MoveIntervalMs ?? 2000))
            return null;

        int[][] dirs = [[0, -1], [0, 1], [-1, 0], [1, 0]];
        for (int i = dirs.Length - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (dirs[i], dirs[j]) = (dirs[j], dirs[i]);
        }

        int range = m.AiConfig.PatrolRange ?? 3;
        foreach (var d in dirs)
        {
            int nx = m.X + d[0], ny = m.Y + d[1];
            if (Math.Abs(nx - m.SpawnX) <= range && Math.Abs(ny - m.SpawnY) <= range)
            {
                if (_mapData.IsWalkable(mapName, nx, ny))
                {
                    m.State = "patrol";
                    m.LastMoveTime = now;
                    return (nx, ny);
                }
            }
        }
        return null;
    }
}
