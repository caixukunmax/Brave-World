using GameServer.Services.Core;
using GameServer.Services.Map.Combat.Actions;
using Microsoft.Extensions.Logging;

namespace GameServer.Services.Map.Combat;

/// <summary>
/// 弹道命中信息
/// </summary>
public record ProjectileHitInfo(
    long ProjectileId,
    long CasterId,
    int SkillId,
    long TargetId,
    string DamageType,
    double Coefficient,
    int FromX,
    int FromY,
    int ToX,
    int ToY
);

/// <summary>
/// 飞行中的弹道
/// </summary>
public class Projectile
{
    public long Id { get; }
    public long CasterId { get; }
    public int SkillId { get; }
    public string MapName { get; }
    public int FromX { get; }
    public int FromY { get; }
    public int ToX { get; }
    public int ToY { get; }
    public float Speed { get; } // 格/秒
    public int MaxRange { get; } // 格
    public string DamageType { get; }
    public double Coefficient { get; }
    public float Progress { get; set; } // 已飞行距离（格）
    public List<(int x, int y)> Path { get; } // Bresenham 路径点
    public bool Destroyed { get; set; }
    public int LastCheckedIndex { get; set; } = -1; // 上次检测到的路径索引

    public Projectile(long id, long casterId, int skillId, string mapName,
        int fromX, int fromY, int toX, int toY,
        float speed, int maxRange, string damageType, double coefficient)
    {
        Id = id;
        CasterId = casterId;
        SkillId = skillId;
        MapName = mapName;
        FromX = fromX;
        FromY = fromY;
        ToX = toX;
        ToY = toY;
        Speed = speed;
        MaxRange = maxRange;
        DamageType = damageType;
        Coefficient = coefficient;
        Progress = 0f;
        Path = ComputeBresenhamPath(fromX, fromY, toX, toY);
        Destroyed = false;
    }

    /// <summary>
    /// Bresenham 直线算法，返回从起点到终点的所有格子坐标
    /// </summary>
    private static List<(int x, int y)> ComputeBresenhamPath(int x0, int y0, int x1, int y1)
    {
        var path = new List<(int x, int y)>();
        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        int x = x0, y = y0;
        while (true)
        {
            path.Add((x, y));
            if (x == x1 && y == y1) break;
            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y += sy;
            }
        }
        return path;
    }
}

/// <summary>
/// 弹道管理器：管理飞行中的弹道，逐帧更新位置并检测碰撞
/// </summary>
public class ProjectileManager
{
    private readonly ILogger<ProjectileManager> _logger;
    private readonly List<Projectile> _projectiles = new();
    private long _nextProjectileId = 1;

    public ProjectileManager(ILogger<ProjectileManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 生成新弹道
    /// </summary>
    public long Spawn(long casterId, int skillId, string mapName,
        int fromX, int fromY, int toX, int toY,
        float speed, int maxRange, string damageType, double coefficient)
    {
        long id = _nextProjectileId++;
        var proj = new Projectile(id, casterId, skillId, mapName,
            fromX, fromY, toX, toY, speed, maxRange, damageType, coefficient);
        _projectiles.Add(proj);
        _logger.LogDebug("[Projectile] spawned id={Id} skill={SkillId} caster={Caster} from=({FromX},{FromY}) to=({ToX},{ToY})",
            id, skillId, casterId, fromX, fromY, toX, toY);
        return id;
    }

    /// <summary>
    /// 获取指定 ID 的弹道
    /// </summary>
    public Projectile? GetProjectile(long id)
    {
        return _projectiles.FirstOrDefault(p => p.Id == id && !p.Destroyed);
    }

    /// <summary>
    /// 每帧更新所有弹道，返回命中事件列表
    /// </summary>
    public List<ProjectileHitInfo> Tick(double dt, Dictionary<string, MapState> maps, CombatRelationManager relations)
    {
        var hits = new List<ProjectileHitInfo>();

        foreach (var proj in _projectiles.ToList())
        {
            if (proj.Destroyed) continue;

            // 更新飞行距离
            proj.Progress += (float)(proj.Speed * dt);

            // 计算当前覆盖到的路径索引
            int currentIndex = Math.Min((int)Math.Floor(proj.Progress), proj.Path.Count - 1);
            if (currentIndex < 0) currentIndex = 0;

            // 检测新覆盖的格子
            bool hit = false;
            for (int i = proj.LastCheckedIndex + 1; i <= currentIndex && i < proj.Path.Count; i++)
            {
                var (gx, gy) = proj.Path[i];

                // 检查该格子是否有与施法者为敌对的实体
                var targetId = FindEnemyAt(proj.CasterId, proj.MapName, gx, gy, maps, relations);
                if (targetId.HasValue)
                {
                    hits.Add(new ProjectileHitInfo(
                        proj.Id, proj.CasterId, proj.SkillId, targetId.Value,
                        proj.DamageType, proj.Coefficient,
                        proj.FromX, proj.FromY, proj.ToX, proj.ToY));
                    proj.Destroyed = true;
                    hit = true;
                    _logger.LogDebug("[Projectile] hit id={Id} target={Target} at=({X},{Y})",
                        proj.Id, targetId.Value, gx, gy);
                    break;
                }
            }
            proj.LastCheckedIndex = currentIndex;

            if (hit) continue;

            // 检查是否超出最大射程
            float distanceTraveled = (float)Math.Sqrt(
                Math.Pow(proj.Path[currentIndex].x - proj.FromX, 2) +
                Math.Pow(proj.Path[currentIndex].y - proj.FromY, 2));

            if (distanceTraveled >= proj.MaxRange || currentIndex >= proj.Path.Count - 1)
            {
                proj.Destroyed = true;
                _logger.LogDebug("[Projectile] expired id={Id} distance={Dist:F2} max={Max}",
                    proj.Id, distanceTraveled, proj.MaxRange);
            }
        }

        // 清理已销毁的弹道
        _projectiles.RemoveAll(p => p.Destroyed);

        return hits;
    }

    /// <summary>
    /// 检查指定格子上是否有与施法者为敌对的实体
    /// </summary>
    private static long? FindEnemyAt(long casterId, string mapName, int x, int y,
        Dictionary<string, MapState> maps, CombatRelationManager relations)
    {
        if (!maps.TryGetValue(mapName, out var map))
            return null;

        // 检查玩家
        foreach (var (playerId, player) in map.Players)
        {
            if (playerId == casterId) continue;
            if (player.GridX == x && player.GridY == y)
            {
                if (relations.HasActiveRelation(casterId, playerId))
                    return playerId;
            }
        }

        // 检查怪物
        foreach (var (monsterId, monster) in map.Monsters)
        {
            if (monsterId == casterId) continue;
            if (monster.X == x && monster.Y == y)
            {
                if (relations.HasActiveRelation(casterId, monsterId))
                    return monsterId;
            }
        }

        // 检查 NPC
        foreach (var (npcId, npc) in map.Npcs)
        {
            if (npcId == casterId) continue;
            if (npc.X == x && npc.Y == y)
            {
                if (relations.HasActiveRelation(casterId, npcId))
                    return npcId;
            }
        }

        return null;
    }

    /// <summary>
    /// 获取当前活跃的弹道数量
    /// </summary>
    public int Count => _projectiles.Count(p => !p.Destroyed);
}
