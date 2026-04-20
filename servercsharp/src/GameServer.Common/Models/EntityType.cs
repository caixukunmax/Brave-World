namespace GameServer.Common.Models;

/// <summary>
/// 地图实体类型枚举 — 对应 common.lua EMapEntityType
/// </summary>
public enum MapEntityType
{
    Chest = 1,
    Npc = 2,
    Portal = 3,
    Monster = 4,
}

/// <summary>
/// 地图实体实例ID工具 — 对应 common.lua makeInstanceId/parseInstanceId
/// 公式: mapId * 1000000 + entityType * 10000 + seq
/// </summary>
public static class InstanceId
{
    public static long Make(int mapId, MapEntityType entityType, int seq)
    {
        return mapId * 1000000L + (int)entityType * 10000 + seq;
    }

    public static (int mapId, MapEntityType entityType, int seq) Parse(long instanceId)
    {
        var mapId = (int)(instanceId / 1000000);
        var entityType = (MapEntityType)((instanceId % 1000000) / 10000);
        var seq = (int)(instanceId % 10000);
        return (mapId, entityType, seq);
    }
}

/// <summary>
/// 奖励解析 — 对应 common.lua parseRewards
/// 格式: "1001:5,1002:10" 或 "1001*5,1002*10" 或 "仙贝*5,精魔石*10"
/// </summary>
public static class RewardParser
{
    public record RewardItem(int ItemId, int Count);

    public static List<RewardItem> Parse(string rewardStr)
    {
        var items = new List<RewardItem>();
        if (string.IsNullOrEmpty(rewardStr)) return items;

        foreach (var pair in rewardStr.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = pair.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // 格式: id*count, id:count, name*count, name:count
            var sepIdx = trimmed.LastIndexOfAny(['*', ':']);
            if (sepIdx < 0) continue;

            var idOrName = trimmed[..sepIdx].Trim();
            if (!int.TryParse(trimmed[(sepIdx + 1)..], out var count)) continue;

            if (int.TryParse(idOrName, out var itemId))
            {
                if (itemId > 0)
                    items.Add(new RewardItem(itemId, count));
            }
            // else: 名称查找暂不实现，需要配置表支持
        }
        return items;
    }
}
