namespace GameServer.Services.Core;

/// <summary>
/// 地图名规范化器：统一历史数据中的拼音/中文差异。
/// 例如旧数据库中可能存储 "xinshoucun"，现在统一为 "新手村"。
/// </summary>
public static class MapNameNormalizer
{
    private static readonly System.Collections.Generic.Dictionary<string, string> Aliases = new()
    {
        ["xinshoucun"] = "新手村"
    };

    public static string Normalize(string mapName)
    {
        if (string.IsNullOrEmpty(mapName)) return mapName;
        return Aliases.TryGetValue(mapName, out var normalized) ? normalized : mapName;
    }
}
