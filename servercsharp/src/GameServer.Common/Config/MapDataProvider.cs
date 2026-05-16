namespace GameServer.Common.Config;

/// <summary>
/// 地图数据提供者 — 从 CSV 文件加载地形数据
/// </summary>
public class MapDataProvider
{
    private readonly Dictionary<string, MapData> _maps = new();
    private readonly Dictionary<string, MapRegistryEntry> _registry = new();

    /// <summary>加载地图注册表 + CSV 地形数据</summary>
    public int LoadAllMaps(string dataDir)
    {
        var registryPath = Path.Combine(dataDir, "map_registry.json");
        if (!File.Exists(registryPath))
        {
            Console.WriteLine($"[MapDataProvider] map_registry.json not found: {registryPath}");
            return 0;
        }

        var json = File.ReadAllText(registryPath);
        var entries = System.Text.Json.JsonSerializer.Deserialize<List<MapRegistryEntry>>(json);
        if (entries == null) return 0;

        int loaded = 0;
        foreach (var entry in entries)
        {
            _registry[entry.map_name] = entry;

            var csvPath = Path.Combine(dataDir, "maps", entry.map_name, "map.csv");
            if (!File.Exists(csvPath))
            {
                Console.WriteLine($"[MapDataProvider] CSV not found: {csvPath}, skipping {entry.map_name}");
                continue;
            }

            var cells = ParseCsvFile(csvPath, out int width, out int height);
            if (cells == null) continue;

            LoadMap(entry.map_name, width, height, cells);
            Console.WriteLine($"[MapDataProvider] Loaded: {entry.map_name} ({width}x{height}) spawn=({entry.spawn_x},{entry.spawn_y})");
            loaded++;
        }

        return loaded;
    }

    public void LoadMap(string mapName, int width, int height, string[] cells)
    {
        var walkable = new bool[width, height];
        var terrainType = new int[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                if (idx < cells.Length)
                {
                    var parts = cells[idx].Split(';');
                    walkable[x, y] = parts.Length >= 2 && parts[1] == "1";
                    if (parts.Length >= 4 && int.TryParse(parts[3], out var t))
                        terrainType[x, y] = t;
                }
            }
        }
        _maps[mapName] = new MapData(mapName, width, height, walkable, terrainType);
    }

    /// <summary>解析 CSV 文件，返回扁平化 cell 数组</summary>
    private static string[]? ParseCsvFile(string csvPath, out int width, out int height)
    {
        width = 0;
        height = 0;
        var lines = File.ReadAllLines(csvPath);
        var dataLines = new List<string>();
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;
            dataLines.Add(trimmed);
        }

        if (dataLines.Count == 0) return null;

        height = dataLines.Count;
        width = dataLines[0].Split(',').Length;

        var cells = new string[width * height];
        for (int y = 0; y < height; y++)
        {
            var cols = dataLines[y].Split(',');
            for (int x = 0; x < width; x++)
            {
                cells[y * width + x] = x < cols.Length ? cols[x].Trim() : "0;0;0;0;0;";
            }
        }

        return cells;
    }

    public bool IsWalkable(string mapName, int x, int y)
    {
        if (!_maps.TryGetValue(mapName, out var map)) return false;
        if (x < 0 || x >= map.Width || y < 0 || y >= map.Height) return false;
        if (!map.Walkable[x, y]) return false;
        // 水域(1)和岩浆(7)不可走
        int terrain = map.TerrainType[x, y];
        return terrain != 1 && terrain != 7;
    }

    public int GetTerrainType(string mapName, int x, int y)
    {
        if (!_maps.TryGetValue(mapName, out var map)) return 0;
        if (x < 0 || x >= map.Width || y < 0 || y >= map.Height) return 0;
        return map.TerrainType[x, y];
    }

    public (int x, int y)? FindNearestWalkable(string mapName, int x, int y, int maxRadius = 10)
    {
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

    public MapData? GetMap(string mapName) =>
        _maps.TryGetValue(mapName, out var map) ? map : null;

    public Dictionary<string, MapData> GetAllMaps() => _maps;

    public MapRegistryEntry? GetRegistryEntry(string mapName) =>
        _registry.TryGetValue(mapName, out var entry) ? entry : null;

    public (int x, int y) GetSpawnPoint(string mapName)
    {
        if (_registry.TryGetValue(mapName, out var entry))
            return (entry.spawn_x, entry.spawn_y);
        return (25, 25);
    }

    public Dictionary<string, MapRegistryEntry> GetAllRegistryEntries() => _registry;

    public record MapData(string Name, int Width, int Height, bool[,] Walkable, int[,] TerrainType);
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
