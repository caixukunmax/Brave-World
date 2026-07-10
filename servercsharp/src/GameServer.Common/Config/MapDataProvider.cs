using GameServer.Tables;
using System.Text.Json;

namespace GameServer.Common.Config;

/// <summary>
/// 地图数据提供者 — 从 JSON 文件加载地形数据
/// </summary>
public class MapDataProvider
{
    private readonly Dictionary<string, MapData> _maps = new();
    private readonly Dictionary<string, MapRegistryEntry> _registry = new();
    private readonly Dictionary<string, string> _mapNameAliases = new();
    private readonly LubanTableLoader? _tables;
    private readonly BuildingConfigProvider? _buildingConfig;

    public MapDataProvider(LubanTableLoader? tables = null, BuildingConfigProvider? buildingConfig = null)
    {
        _tables = tables;
        _buildingConfig = buildingConfig;
    }

    /// <summary>加载地图注册表 + JSON 地形数据</summary>
    public int LoadAllMaps(string dataDir)
    {
        var registryPath = Path.Combine(dataDir, "map_registry.json");
        if (!File.Exists(registryPath))
        {
#if DEBUG
            Console.WriteLine($"[MapDataProvider] map_registry.json not found: {registryPath}");
#endif
            return 0;
        }

        var json = File.ReadAllText(registryPath);
        var entries = System.Text.Json.JsonSerializer.Deserialize<List<MapRegistryEntry>>(json);
        if (entries == null) return 0;

        int loaded = 0;
        foreach (var entry in entries)
        {
            _registry[entry.map_name] = entry;

            var jsonPath = Path.Combine(dataDir, "maps", entry.map_name, "map.json");
            if (!File.Exists(jsonPath))
            {
#if DEBUG
                Console.WriteLine($"[MapDataProvider] JSON not found: {jsonPath}, skipping {entry.map_name}");
#endif
                continue;
            }

            var cells = ParseJsonFile(jsonPath, out int offsetX, out int offsetY, out int width, out int height);
            if (cells == null) continue;

            LoadMap(entry.map_name, offsetX, offsetY, width, height, cells);
#if DEBUG
            Console.WriteLine($"[MapDataProvider] Loaded: {entry.map_name} ({width}x{height}) offset=({offsetX},{offsetY}) spawn=({entry.spawn_x},{entry.spawn_y})");
#endif
            loaded++;
        }

        RegisterAliases();
        return loaded;
    }

    /// <summary>
    /// 注册地图名别名，兼容历史数据中的拼音/中文不一致问题。
    /// 例如历史角色数据可能存储 "xinshoucun"，而注册表使用 "新手村"。
    /// </summary>
    private void RegisterAliases()
    {
        // 新手村 <-> xinshoucun
        if (_registry.ContainsKey("新手村") && !_registry.ContainsKey("xinshoucun"))
            _mapNameAliases["xinshoucun"] = "新手村";
        if (_registry.ContainsKey("xinshoucun") && !_registry.ContainsKey("新手村"))
            _mapNameAliases["新手村"] = "xinshoucun";
    }

    /// <summary>将查询用的地图名解析为注册表中实际存在的 key。</summary>
    private string ResolveMapName(string mapName)
    {
        if (string.IsNullOrEmpty(mapName)) return mapName;
        if (_registry.ContainsKey(mapName)) return mapName;
        if (_mapNameAliases.TryGetValue(mapName, out var alias))
            return alias;
        return mapName;
    }

    public void LoadMap(string mapName, int offsetX, int offsetY, int width, int height, string[] cells)
    {
        var terrainType = new int[width, height];
        var decorationType = new int[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                if (idx < cells.Length)
                {
                    var cellJson = cells[idx];
                    var cell = string.IsNullOrEmpty(cellJson)
                        ? null
                        : System.Text.Json.JsonSerializer.Deserialize<MapCellJson>(cellJson);
                    terrainType[x, y] = cell?.terrain ?? 0;
                    decorationType[x, y] = cell?.decoration ?? 0;
                }
            }
        }
        var blocked = ComputeBlockedCells(width, height, decorationType);
        _maps[mapName] = new MapData(mapName, offsetX, offsetY, width, height, terrainType, decorationType, blocked);
    }

    /// <summary>
    /// 根据 decoration 配置计算阻塞格子。装饰锚点格及其 footprint 内所有格子都会被阻塞。
    /// </summary>
    private bool[,] ComputeBlockedCells(int width, int height, int[,] decorationType)
    {
        var blocked = new bool[width, height];
        if (_buildingConfig == null) return blocked;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int decoration = decorationType[x, y];
                if (decoration == 0) continue;
                if (!_buildingConfig.BlocksMovement(decoration)) continue;

                var (sizeX, sizeY) = _buildingConfig.GetSize(decoration);
                for (int dy = 0; dy < sizeY; dy++)
                {
                    for (int dx = 0; dx < sizeX; dx++)
                    {
                        int bx = x + dx;
                        int by = y + dy;
                        if (bx < width && by < height)
                            blocked[bx, by] = true;
                    }
                }
            }
        }
        return blocked;
    }

    /// <summary>解析 map.json 文件，返回扁平化 cell JSON 字符串数组</summary>
    private static string[]? ParseJsonFile(string jsonPath, out int offsetX, out int offsetY, out int width, out int height)
    {
        offsetX = 0;
        offsetY = 0;
        width = 0;
        height = 0;

        var text = File.ReadAllText(jsonPath);
        var doc = System.Text.Json.JsonSerializer.Deserialize<MapJsonDocument>(text);
        if (doc == null) return null;

        if (doc.version < 2)
        {
#if DEBUG
            Console.WriteLine($"[MapDataProvider] 地图 JSON 版本过低: {jsonPath}, version={doc.version}");
#endif
            return null;
        }

        offsetX = doc.bounds?.x ?? 0;
        offsetY = doc.bounds?.y ?? 0;
        width = doc.bounds?.w ?? 0;
        height = doc.bounds?.h ?? 0;

        if (width <= 0 || height <= 0)
        {
#if DEBUG
            Console.WriteLine($"[MapDataProvider] 地图边界无效: {jsonPath}");
#endif
            return null;
        }

        var cells = new string[width * height];
        for (int i = 0; i < cells.Length; i++) cells[i] = "";

        if (doc.cells != null)
        {
            foreach (var kvp in doc.cells)
            {
                var parts = kvp.Key.Split('_');
                if (parts.Length != 2 ||
                    !int.TryParse(parts[0], out int x) ||
                    !int.TryParse(parts[1], out int y))
                {
                    continue;
                }

                int localX = x - offsetX;
                int localY = y - offsetY;
                if (localX < 0 || localX >= width || localY < 0 || localY >= height)
                    continue;

                cells[localY * width + localX] = System.Text.Json.JsonSerializer.Serialize(kvp.Value);
            }
        }

        return cells;
    }

    public bool IsWalkable(string mapName, int x, int y)
    {
        mapName = ResolveMapName(mapName);
        if (!_maps.TryGetValue(mapName, out var map)) return false;
        int localX = x - map.OffsetX;
        int localY = y - map.OffsetY;
        if (localX < 0 || localX >= map.Width || localY < 0 || localY >= map.Height) return false;

        // 装饰摆件阻塞检查（按建筑配置展开 footprint）
        if (map.Blocked[localX, localY])
            return false;

        int terrain = map.TerrainType[localX, localY];
        var cfg = _tables?.TerrainConfigs.GetValueOrDefault(terrain);
        return cfg?.Walkable ?? true;
    }

    public bool IsBlockedByDecoration(string mapName, int x, int y)
    {
        mapName = ResolveMapName(mapName);
        if (!_maps.TryGetValue(mapName, out var map)) return false;
        int localX = x - map.OffsetX;
        int localY = y - map.OffsetY;
        if (localX < 0 || localX >= map.Width || localY < 0 || localY >= map.Height) return false;
        return map.Blocked[localX, localY];
    }

    public int GetTerrainType(string mapName, int x, int y)
    {
        mapName = ResolveMapName(mapName);
        if (!_maps.TryGetValue(mapName, out var map)) return 0;
        int localX = x - map.OffsetX;
        int localY = y - map.OffsetY;
        if (localX < 0 || localX >= map.Width || localY < 0 || localY >= map.Height) return 0;
        return map.TerrainType[localX, localY];
    }

    public int GetDecorationType(string mapName, int x, int y)
    {
        mapName = ResolveMapName(mapName);
        if (!_maps.TryGetValue(mapName, out var map)) return 0;
        int localX = x - map.OffsetX;
        int localY = y - map.OffsetY;
        if (localX < 0 || localX >= map.Width || localY < 0 || localY >= map.Height) return 0;
        return map.DecorationType[localX, localY];
    }

    /// <summary>获取地图装饰数据（用于进入/切地图时同步给客户端）</summary>
    public (int width, int height, int[,] decorationTypes)? GetMapDecorationData(string mapName)
    {
        mapName = ResolveMapName(mapName);
        if (!_maps.TryGetValue(mapName, out var map)) return null;

        var decorationTypes = new int[map.Width, map.Height];
        for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
                decorationTypes[x, y] = map.DecorationType[x, y];

        return (map.Width, map.Height, decorationTypes);
    }

    /// <summary>获取地图中被建筑 footprint 阻塞的所有格子（调试用）</summary>
    public List<(int x, int y)>? GetBlockedCells(string mapName)
    {
        mapName = ResolveMapName(mapName);
        if (!_maps.TryGetValue(mapName, out var map)) return null;
        var list = new List<(int x, int y)>();
        for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
                if (map.Blocked[x, y])
                    list.Add((x + map.OffsetX, y + map.OffsetY));
        return list;
    }

    public (int x, int y)? FindNearestWalkable(string mapName, int x, int y, int maxRadius = 10)
    {
        mapName = ResolveMapName(mapName);
        if (IsWalkable(mapName, x, y)) return (x, y);
        for (int r = 1; r <= maxRadius; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                    if (IsWalkable(mapName, x + dx, y + dy))
                        return (x + dx, y + dy);
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 查找最近的、能容纳指定 footprint 的锚点位置。
    /// 要求 footprint 内所有格子均可行走，且不会越界。
    /// </summary>
    public (int x, int y)? FindNearestWalkableForFootprint(string mapName, int x, int y, int sizeX, int sizeY, int maxRadius = 10)
    {
        mapName = ResolveMapName(mapName);
        sizeX = Math.Max(1, sizeX);
        sizeY = Math.Max(1, sizeY);

        if (IsFootprintWalkable(mapName, x, y, sizeX, sizeY))
            return (x, y);

        for (int r = 1; r <= maxRadius; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                    int ax = x + dx;
                    int ay = y + dy;
                    if (IsFootprintWalkable(mapName, ax, ay, sizeX, sizeY))
                        return (ax, ay);
                }
            }
        }
        return null;
    }

    private bool IsFootprintWalkable(string mapName, int anchorX, int anchorY, int sizeX, int sizeY)
    {
        if (!_maps.TryGetValue(mapName, out var map)) return false;

        int localX = anchorX - map.OffsetX;
        int localY = anchorY - map.OffsetY;
        if (localX < 0 || localY < 0 || localX + sizeX > map.Width || localY + sizeY > map.Height)
            return false;

        for (int dy = 0; dy < sizeY; dy++)
        {
            for (int dx = 0; dx < sizeX; dx++)
            {
                int cx = localX + dx;
                int cy = localY + dy;
                if (map.Blocked[cx, cy])
                    return false;
                int terrain = map.TerrainType[cx, cy];
                var cfg = _tables?.TerrainConfigs.GetValueOrDefault(terrain);
                if (!(cfg?.Walkable ?? true))
                    return false;
            }
        }
        return true;
    }

    public MapData? GetMap(string mapName)
    {
        mapName = ResolveMapName(mapName);
        return _maps.TryGetValue(mapName, out var map) ? map : null;
    }

    public Dictionary<string, MapData> GetAllMaps() => _maps;

    public MapRegistryEntry? GetRegistryEntry(string mapName)
    {
        mapName = ResolveMapName(mapName);
        return _registry.TryGetValue(mapName, out var entry) ? entry : null;
    }

    /// <summary>
    /// 出生点建筑配置 ID（与客户端 BuildingType.SpawnPoint 保持一致）
    /// </summary>
    public const int SpawnPointDecorationId = 60000;

    public (int x, int y) GetSpawnPoint(string mapName)
    {
        mapName = ResolveMapName(mapName);

        // 优先使用地图中的出生点建筑（60000）作为出生点来源
        if (_maps.TryGetValue(mapName, out var map))
        {
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    if (map.DecorationType[x, y] == SpawnPointDecorationId)
                    {
                        return (x + map.OffsetX, y + map.OffsetY);
                    }
                }
            }
        }

        if (_registry.TryGetValue(mapName, out var entry))
            return (entry.spawn_x, entry.spawn_y);
        return (25, 25);
    }

    public Dictionary<string, MapRegistryEntry> GetAllRegistryEntries() => _registry;

    public record MapData(string Name, int OffsetX, int OffsetY, int Width, int Height, int[,] TerrainType, int[,] DecorationType, bool[,] Blocked);
}

/// <summary>map_registry.json 中的条目</summary>
public class MapRegistryEntry
{
    public string map_name { get; set; } = "";
    public string display_name { get; set; } = "";
    public int width { get; set; }
    public int height { get; set; }
    public int spawn_x { get; set; }
    public int spawn_y { get; set; }
}

/// <summary>map.json 反序列化结构</summary>
public class MapJsonDocument
{
    public int version { get; set; }
    public string? display_name { get; set; }
    public MapBoundsJson? bounds { get; set; }
    public MapSpawnJson? spawn { get; set; }
    public Dictionary<string, MapCellJson>? cells { get; set; }
}

public class MapBoundsJson
{
    public int x { get; set; }
    public int y { get; set; }
    public int w { get; set; }
    public int h { get; set; }
}

public class MapSpawnJson
{
    public int x { get; set; }
    public int y { get; set; }
}

public class MapCellJson
{
    public int terrain { get; set; }
    public int height { get; set; }
    public string? custom { get; set; }
    public int decoration { get; set; }
}
