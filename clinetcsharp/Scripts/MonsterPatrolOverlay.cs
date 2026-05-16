using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClinetCSharp
{
    /// <summary>
    /// Debug-only overlay that draws patrol regions for all monster spawns on the current map.
    /// Data prefers generated server tables and falls back to client monster_config.json.
    /// </summary>
    public partial class MonsterPatrolOverlay : Node2D
    {
        private const string AiTablePath = "res://../servercsharp/data/tables/common_tbai.json";
        private const string MapMonsterTablePath = "res://../servercsharp/data/tables/common_tbmapmonster.json";
        private const string MapConfigTablePath = "res://../servercsharp/data/tables/common_tbmapconfig.json";

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        private GridManager _gridManager;
        private NetworkManager _networkManager;
        private MonsterConfigManager _monsterConfigManager;
        private List<PatrolAreaEntry> _entries = new();
        private string _lastMapName = "";
        private int _lastGridSize = -1;

        public bool OverlayEnabled { get; private set; }

        public override void _Ready()
        {
            TopLevel = false;
            ZIndex = 30;
            SetProcess(false);
            Visible = false;
        }

        public override void _Process(double delta)
        {
            if (!OverlayEnabled)
                return;

            EnsureServices();
            string mapName = GetCurrentMapName();
            int gridSize = _gridManager?.GridSize ?? -1;
            if (!string.Equals(mapName, _lastMapName, StringComparison.Ordinal) || gridSize != _lastGridSize)
            {
                ReloadForCurrentMap();
            }
        }

        public override void _Draw()
        {
            if (!OverlayEnabled || _gridManager == null || _entries.Count == 0)
                return;

            var fillColor = new Color(0.20f, 0.85f, 0.35f, 0.10f);
            var lineColor = new Color(0.20f, 0.95f, 0.45f, 0.70f);
            var spawnColor = new Color(1.0f, 0.95f, 0.35f, 0.95f);
            float borderWidth = 1.5f;
            float markerRadius = Mathf.Clamp(_gridManager.GridSize * 0.08f, 2.0f, 6.0f);

            foreach (var entry in _entries)
            {
                if (entry.PatrolRange <= 0)
                    continue;

                float gridSize = _gridManager.GridSize;
                float left = (entry.SpawnX - entry.PatrolRange) * gridSize;
                float top = (entry.SpawnY - entry.PatrolRange) * gridSize;
                float size = (entry.PatrolRange * 2 + 1) * gridSize;

                var rect = new Rect2(left, top, size, size);
                DrawRect(rect, fillColor, true);
                DrawRect(rect, lineColor, false, borderWidth);

                var center = new Vector2(
                    (entry.SpawnX + 0.5f) * gridSize,
                    (entry.SpawnY + 0.5f) * gridSize);
                DrawCircle(center, markerRadius, spawnColor);
            }
        }

        public void SetOverlayEnabled(bool enabled)
        {
            OverlayEnabled = enabled;
            Visible = enabled;
            SetProcess(enabled);

            if (enabled)
                ReloadForCurrentMap();
            else
                QueueRedraw();
        }

        public void ReloadForCurrentMap()
        {
            EnsureServices();
            _lastMapName = GetCurrentMapName();
            _lastGridSize = _gridManager?.GridSize ?? -1;
            _entries = LoadPatrolAreas(_lastMapName);
            QueueRedraw();
        }

        private void EnsureServices()
        {
            if (_gridManager == null || !IsInstanceValid(_gridManager))
                _gridManager = GetParentOrNull<GridManager>() ?? GetTree()?.GetFirstNodeInGroup("grid_manager") as GridManager;
            if (_networkManager == null || !IsInstanceValid(_networkManager))
                _networkManager = UiServices.GetNetworkManager(this);
            if (_monsterConfigManager == null || !IsInstanceValid(_monsterConfigManager))
                _monsterConfigManager = GetNodeOrNull<MonsterConfigManager>("/root/MonsterConfigManager");
        }

        private string GetCurrentMapName()
        {
            if (!string.IsNullOrWhiteSpace(_networkManager?.CurrentMapName))
                return _networkManager.CurrentMapName;
            return _gridManager?.CurrentMapName ?? "xinshoucun";
        }

        private List<PatrolAreaEntry> LoadPatrolAreas(string currentMapName)
        {
            var fromTables = TryLoadFromGeneratedTables(currentMapName);
            if (fromTables.Count > 0)
                return fromTables;

            return LoadFromMonsterConfig(currentMapName);
        }

        private List<PatrolAreaEntry> TryLoadFromGeneratedTables(string currentMapName)
        {
            try
            {
                string aiPath = ProjectSettings.GlobalizePath(AiTablePath);
                string mapMonsterPath = ProjectSettings.GlobalizePath(MapMonsterTablePath);
                string mapConfigPath = ProjectSettings.GlobalizePath(MapConfigTablePath);

                if (!File.Exists(aiPath) || !File.Exists(mapMonsterPath) || !File.Exists(mapConfigPath))
                    return new List<PatrolAreaEntry>();

                var aiRows = JsonSerializer.Deserialize<List<AiTableRow>>(File.ReadAllText(aiPath), _jsonOptions) ?? new();
                var mapRows = JsonSerializer.Deserialize<List<MapMonsterTableRow>>(File.ReadAllText(mapMonsterPath), _jsonOptions) ?? new();
                var mapConfigs = JsonSerializer.Deserialize<List<MapConfigTableRow>>(File.ReadAllText(mapConfigPath), _jsonOptions) ?? new();

                var targetMapIds = new HashSet<int>(mapConfigs
                    .Where(row => string.Equals(row.MapName, currentMapName, StringComparison.OrdinalIgnoreCase))
                    .Select(row => row.Id));
                if (targetMapIds.Count == 0)
                    return new List<PatrolAreaEntry>();

                var aiById = aiRows.ToDictionary(row => row.Id, row => row);
                var result = new List<PatrolAreaEntry>();
                foreach (var row in mapRows)
                {
                    if (!targetMapIds.Contains(row.MapId))
                        continue;
                    if (!aiById.TryGetValue(row.AiId, out var ai))
                        continue;

                    result.Add(new PatrolAreaEntry
                    {
                        SpawnX = row.X,
                        SpawnY = row.Y,
                        PatrolRange = Math.Max(ai.PatrolRange, 0),
                        AiType = ai.AiType ?? "patrol",
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                GD.PushWarning($"[MonsterPatrolOverlay] Failed to load generated tables: {ex.Message}");
                return new List<PatrolAreaEntry>();
            }
        }

        private List<PatrolAreaEntry> LoadFromMonsterConfig(string currentMapName)
        {
            if (_monsterConfigManager?.Config == null)
                return new List<PatrolAreaEntry>();

            var result = new List<PatrolAreaEntry>();
            foreach (var monster in _monsterConfigManager.Config.Monsters)
            {
                if (!string.Equals(monster.MapName, currentMapName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var defaults = _monsterConfigManager.GetAiDefaults(monster.AiType);
                result.Add(new PatrolAreaEntry
                {
                    SpawnX = monster.X,
                    SpawnY = monster.Y,
                    PatrolRange = Math.Max(defaults.PatrolRange ?? 0, 0),
                    AiType = monster.AiType ?? "patrol",
                });
            }

            return result;
        }

        private sealed class PatrolAreaEntry
        {
            public int SpawnX { get; set; }
            public int SpawnY { get; set; }
            public int PatrolRange { get; set; }
            public string AiType { get; set; } = "patrol";
        }

        private sealed class AiTableRow
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }
            [JsonPropertyName("ai_type")]
            public string AiType { get; set; } = "patrol";
            [JsonPropertyName("patrol_range")]
            public int PatrolRange { get; set; }
        }

        private sealed class MapMonsterTableRow
        {
            [JsonPropertyName("map_id")]
            public int MapId { get; set; }
            [JsonPropertyName("x")]
            public int X { get; set; }
            [JsonPropertyName("y")]
            public int Y { get; set; }
            [JsonPropertyName("ai_id")]
            public int AiId { get; set; }
        }

        private sealed class MapConfigTableRow
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }
            [JsonPropertyName("map_name")]
            public string MapName { get; set; } = "";
        }
    }
}
