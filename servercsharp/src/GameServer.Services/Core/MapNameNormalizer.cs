namespace GameServer.Services.Core;

/// <summary>
/// 地图名规范化器：统一历史数据中的拼音/中文差异。
/// 别名映射现在由 MapDataProvider.RegisterAliases() 动态管理，
/// 此类仅保留向后兼容的空壳。
/// </summary>
public static class MapNameNormalizer
{
    public static string Normalize(string mapName)
    {
        // 别名统一由 MapDataProvider.ResolveMapName 处理，
        // 此处仅做透传（保留调用方兼容性）。
        return mapName ?? "";
    }
}
