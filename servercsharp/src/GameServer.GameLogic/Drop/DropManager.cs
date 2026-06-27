using System.Linq;
using GameServer.GameLogic.Inventory;
using GameServer.Services.Core;
using GameServer.Services.Player;
using GameServer.Tables;
using Microsoft.Extensions.Logging;
using PGame = global::Game;

namespace GameServer.Services.Map.Drop;

/// <summary>
/// 掉落物管理器 — 生成、拾取、超时清理
/// </summary>
public class DropManager : IDropService
{
    private readonly ILogger<DropManager> _logger;
    private readonly LubanTableLoader _tables;
    private readonly PlayerSessionManager _session;

    /// <summary>掉落物存储：mapName → List&lt;DropItemEntity&gt;</summary>
    private readonly Dictionary<string, List<DropItemEntity>> _drops = new();

    /// <summary>掉落物 ID 索引：dropId → DropItemEntity（快速查找）</summary>
    private readonly Dictionary<long, DropItemEntity> _dropById = new();

    private long _nextDropId = 1;
    private readonly Random _random = new();

    // ---- 常量 ----
    private const float DropLifetime = 120f;       // 掉落物存在时间（秒）
    private const float OwnerLockDuration = 30f;    // 归属锁定时间（秒）
    private const int MaxDropsPerCell = 10;         // 同格最大掉落物

    /// <summary>掉落物生成回调（供网络广播用）</summary>
    public Action<List<DropItemEntity>, string>? OnDropsSpawned;
    /// <summary>掉落物拾取回调（供网络广播用）</summary>
    /// 参数：dropId, pickerId, itemId, actualCount, remainingCount, mapName
    public Action<long, long, int, int, int, string>? OnDropPickedUp;
    /// <summary>掉落物消失回调（供网络广播用）</summary>
    public Action<List<long>, string>? OnDropsRemoved;

    public DropManager(
        ILogger<DropManager> logger,
        LubanTableLoader tables,
        PlayerSessionManager session)
    {
        _logger = logger;
        _tables = tables;
        _session = session;
    }

    // ---- 生成掉落 ----

    /// <summary>
    /// 怪物死亡时生成掉落物
    /// </summary>
    /// <returns>生成的掉落物列表（可能为空）</returns>
    public List<DropItemEntity> GenerateDrops(int monsterId, string mapName, int x, int y, long killerId)
    {
        var monster = _tables.Monsters.GetValueOrDefault(monsterId);
        if (monster == null) return new();

        // 优先使用 drop_group_id，fallback 到 drop_items
        List<(int itemId, int count)> items;
        if (monster.DropGroupId > 0)
        {
            items = RollDropGroup(monster.DropGroupId);
        }
        else
        {
            items = ParseDropItems(monster.DropItems);
        }

        if (items.Count == 0) return new();

        var spawned = new List<DropItemEntity>();
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        foreach (var (itemId, count) in items)
        {
            // 随机偏移 ±1 格
            int dx = _random.Next(-1, 2);
            int dy = _random.Next(-1, 2);
            int dropX = Math.Max(0, x + dx);
            int dropY = Math.Max(0, y + dy);

            var drop = new DropItemEntity
            {
                DropId = _nextDropId++,
                ItemId = itemId,
                Count = count,
                MapName = mapName,
                X = dropX,
                Y = dropY,
                SpawnTime = now,
                OwnerId = killerId,
                OwnerLockTime = OwnerLockDuration,
            };

            if (!_drops.ContainsKey(mapName))
                _drops[mapName] = new List<DropItemEntity>();

            _drops[mapName].Add(drop);
            _dropById[drop.DropId] = drop;
            spawned.Add(drop);

            _logger.LogInformation("[Drop] spawned: dropId={DropId} item={ItemId}x{Count} at ({X},{Y}) owner={Owner}",
                drop.DropId, itemId, count, dropX, dropY, killerId);
        }

        // 触发生成回调
        if (spawned.Count > 0)
            OnDropsSpawned?.Invoke(spawned, mapName);

        return spawned;
    }

    // ---- 拾取 ----

    /// <summary>
    /// 尝试拾取掉落物
    /// </summary>
    /// <returns>(成功, itemId, added, remaining)</returns>
    public async Task<(bool ok, int itemId, int added, int remaining)> TryPickup(long playerId, string mapName, long dropId)
    {
        if (!_dropById.TryGetValue(dropId, out var drop)) return (false, 0, 0, 0);
        if (drop.MapName != mapName) return (false, 0, 0, 0);

        // 归属检查
        if (drop.OwnerId != 0 && drop.OwnerId != playerId && drop.OwnerLockTime > 0)
        {
            _logger.LogDebug("[Drop] pickup blocked: dropId={DropId} owner={Owner} player={Player} lockTime={Lock}",
                dropId, drop.OwnerId, playerId, drop.OwnerLockTime);
            return (false, 0, 0, 0);
        }

        int added = 0;
        int remaining = drop.Count;

        // 添加物品到背包（按容量部分拾取）
        if (_session.TryGetPlayer(playerId, out var role))
        {
            var dbItems = await _session.Inventory.GetByRole(role.RoleId);
            var (add, rem) = InventoryHelper.CalculatePickupCapacity(dbItems, _tables, drop.ItemId, drop.Count);
            added = add;
            remaining = rem;

            if (added > 0)
                await _session.Inventory.AddItem(role.RoleId, drop.ItemId, added);
        }

        // 只有实际放入物品时才移除掉落物
        if (added > 0)
            RemoveDrop(drop);

        _logger.LogInformation("[Drop] picked up: dropId={DropId} item={ItemId} requested={Count} added={Added} remaining={Remaining} by player={Player}",
            dropId, drop.ItemId, drop.Count, added, remaining, playerId);

        // 触发拾取回调
        OnDropPickedUp?.Invoke(dropId, playerId, drop.ItemId, added, remaining, mapName);

        return (added > 0, drop.ItemId, added, remaining);
    }

    /// <summary>
    /// 检查玩家所在格子是否有可拾取的掉落物，自动拾取
    /// </summary>
    public async Task TryAutoPickup(long playerId, string mapName, int x, int y)
    {
        if (!_drops.TryGetValue(mapName, out var list)) return;

        // 复制列表避免修改迭代
        var toPickup = list.Where(d => d.X == x && d.Y == y).ToList();
        foreach (var drop in toPickup)
        {
            await TryPickup(playerId, mapName, drop.DropId);
        }
    }

    // ---- Tick ----

    /// <summary>
    /// 每帧更新：归属锁定倒计时 + 超时清理
    /// </summary>
    public void Tick(float dt)
    {
        var expired = new List<DropItemEntity>();
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        foreach (var list in _drops.Values)
        {
            foreach (var drop in list)
            {
                // 归属锁定倒计时
                if (drop.OwnerLockTime > 0)
                {
                    drop.OwnerLockTime -= dt;
                    if (drop.OwnerLockTime <= 0)
                    {
                        drop.OwnerLockTime = 0;
                        drop.OwnerId = 0; // 开放给所有人
                    }
                }

                // 超时检查
                float age = now - drop.SpawnTime;
                if (age >= DropLifetime)
                {
                    expired.Add(drop);
                }
            }
        }

        // 移除超时掉落物（按地图分组广播）
        if (expired.Count > 0)
        {
            var byMap = expired.GroupBy(d => d.MapName);
            foreach (var group in byMap)
            {
                var expiredIds = group.Select(d => d.DropId).ToList();
                foreach (var drop in group)
                    RemoveDrop(drop);

                _logger.LogInformation("[Drop] expired {Count} drops on map {Map}", expiredIds.Count, group.Key);
                OnDropsRemoved?.Invoke(expiredIds, group.Key);
            }
        }
    }

    // ---- 查询 ----

    /// <summary>获取地图上所有掉落物（给新进地图的玩家同步）</summary>
    public List<DropItemEntity> GetDrops(string mapName)
    {
        if (!_drops.TryGetValue(mapName, out var list)) return new();
        return list.ToList();
    }

    /// <summary>获取地图上所有掉落物的协议表示（IDropService 实现）。</summary>
    List<PGame.DropItemInfo> IDropService.GetDrops(string mapName)
    {
        var result = new List<PGame.DropItemInfo>();
        if (!_drops.TryGetValue(mapName, out var list)) return result;

        foreach (var d in list)
        {
            result.Add(new PGame.DropItemInfo
            {
                DropId = (ulong)d.DropId,
                ItemId = (uint)d.ItemId,
                Count = (uint)d.Count,
                X = d.X,
                Y = d.Y,
                OwnerId = (ulong)d.OwnerId,
            });
        }
        return result;
    }

    // ---- 内部 ----

    private void RemoveDrop(DropItemEntity drop)
    {
        _dropById.Remove(drop.DropId);
        if (_drops.TryGetValue(drop.MapName, out var list))
        {
            list.Remove(drop);
        }
    }

    /// <summary>
    /// 根据掉落组配置随机生成掉落物品
    /// </summary>
    private List<(int itemId, int count)> RollDropGroup(int groupId)
    {
        var group = _tables.GetDropGroup(groupId);
        if (group == null) return new();

        var result = new List<(int, int)>();

        foreach (var entry in group.Entries)
        {
            if (entry.Guaranteed)
            {
                // 必掉：直接掉
                int count = _random.Next(entry.CountMin, entry.CountMax + 1);
                result.Add((entry.ItemId, count));
            }
            else
            {
                // 概率掉：按权重随机
                int roll = _random.Next(0, 100);
                if (roll < entry.Weight)
                {
                    int count = _random.Next(entry.CountMin, entry.CountMax + 1);
                    result.Add((entry.ItemId, count));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 解析旧格式 drop_items（"itemId:count" 格式）
    /// </summary>
    private List<(int itemId, int count)> ParseDropItems(string dropItems)
    {
        var result = new List<(int, int)>();
        if (string.IsNullOrEmpty(dropItems)) return result;

        foreach (var part in dropItems.Split('|'))
        {
            var segs = part.Split(':');
            if (segs.Length >= 2 && int.TryParse(segs[0], out var itemId) && int.TryParse(segs[1], out var count))
            {
                result.Add((itemId, count));
            }
        }

        return result;
    }
}
