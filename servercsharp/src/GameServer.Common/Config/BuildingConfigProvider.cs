using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameServer.Common.Config;

/// <summary>
/// 建筑占地配置 — 服务端运行时从 JSON 加载，用于把地图中的 decoration 锚点展开成 footprint。
/// 阶段 2 临时方案：与客户端 EntityProfile 并行存在，后续统一为共享配置。
/// </summary>
public class BuildingConfigProvider
{
    private readonly Dictionary<int, BuildingConfig> _configs = new();

    public IReadOnlyDictionary<int, BuildingConfig> Configs => _configs;

    public void Load(string filePath)
    {
        _configs.Clear();
        if (!File.Exists(filePath))
        {
#if DEBUG
            Console.WriteLine($"[BuildingConfigProvider] config not found: {filePath}");
#endif
            return;
        }

        var json = File.ReadAllText(filePath);
        var raw = JsonSerializer.Deserialize<Dictionary<string, BuildingConfig>>(json);
        if (raw == null) return;

        foreach (var kvp in raw)
        {
            if (int.TryParse(kvp.Key, out int id))
                _configs[id] = kvp.Value;
        }

#if DEBUG
        Console.WriteLine($"[BuildingConfigProvider] loaded {_configs.Count} configs from {filePath}");
#endif
    }

    public BuildingConfig Get(int id)
    {
        return _configs.GetValueOrDefault(id, new BuildingConfig { SizeX = 1, SizeY = 1, BlockMovement = false });
    }

    public (int sizeX, int sizeY) GetSize(int id)
    {
        var cfg = Get(id);
        return (Math.Max(1, cfg.SizeX), Math.Max(1, cfg.SizeY));
    }

    public bool BlocksMovement(int id)
    {
        var cfg = Get(id);
        return cfg.BlockMovement;
    }
}

public class BuildingConfig
{
    [JsonPropertyName("sizeX")]
    public int SizeX { get; set; } = 1;

    [JsonPropertyName("sizeY")]
    public int SizeY { get; set; } = 1;

    [JsonPropertyName("blockMovement")]
    public bool BlockMovement { get; set; } = false;
}
