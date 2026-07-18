using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityClientSharp.Map.Core
{
    /// <summary>
    /// 地图数据管理器 — 负责 map.json 的加载、保存、JSON 导入导出。
    /// 移植自 clinetcsharp/Scripts/MapDataManager.cs：
    ///   Godot.FileAccess/DirAccess/Json -> System.IO.File + Newtonsoft.Json（随仓库 vendoring 于 Assets/Plugins/NewtonsoftJson），
    ///   保持原 map.json schema 不变。
    /// </summary>
    public static class MapDataManager
    {
        public const int CurrentMapVersion = 3;
        public const int MinSupportedMapVersion = 2;
        public const string JsonFilename = "map.json";

        public static string MapsRoot => Path.Combine(Application.streamingAssetsPath, "Data", "maps");

        /// <summary>创建默认地图数据 (width x height, 全部普通地形)，返回稀疏字典。</summary>
        public static Dictionary<Vector2Int, GridCell> CreateDefaultGridData(int width, int height, int originX = 0, int originY = 0)
        {
            var gridData = new Dictionary<Vector2Int, GridCell>();
            for (int y = originY; y < originY + height; y++)
            {
                for (int x = originX; x < originX + width; x++)
                {
                    var cell = new GridCell(x, y);
                    cell.TerrainType = 0;
                    cell.RefreshTerrainConfig();
                    gridData[new Vector2Int(x, y)] = cell;
                }
            }
            return gridData;
        }

        public static bool MapExists(string mapName)
        {
            return Directory.Exists(Path.Combine(MapsRoot, mapName));
        }

        /// <summary>从 map.json 加载地图，返回稀疏字典。</summary>
        public static Dictionary<Vector2Int, GridCell> LoadMapFromJson(
            string mapName, out RectInt bounds, out Vector2Int spawn, out string displayName)
        {
            bounds = new RectInt(0, 0, 50, 50);
            spawn = new Vector2Int(25, 25);
            displayName = mapName;

            string jsonPath = Path.Combine(MapsRoot, mapName, JsonFilename);
            if (!File.Exists(jsonPath))
            {
                Debug.Log($"[MapDataManager] 地图 JSON 不存在，将创建默认地图: {mapName}");
                return new Dictionary<Vector2Int, GridCell>();
            }

            try
            {
                string json = File.ReadAllText(jsonPath);
                var root = JObject.Parse(json);

                int version = (int?)root["version"] ?? 0;
                if (version < MinSupportedMapVersion)
                {
                    Debug.LogError($"[MapDataManager] 地图 JSON 版本过低: {jsonPath}, version={version}");
                    return new Dictionary<Vector2Int, GridCell>();
                }
                if (version < CurrentMapVersion)
                {
                    Debug.Log($"[MapDataManager] 地图 JSON 版本较旧，将自动兼容: {jsonPath}, version={version}");
                }

                displayName = (string)root["display_name"] ?? mapName;

                var b = root["bounds"] as JObject;
                if (b != null)
                {
                    int bx = (int?)b["x"] ?? 0;
                    int by = (int?)b["y"] ?? 0;
                    int bw = (int?)b["w"] ?? 50;
                    int bh = (int?)b["h"] ?? 50;
                    bounds = new RectInt(bx, by, bw, bh);
                }

                var s = root["spawn"] as JObject;
                if (s != null)
                {
                    int sx = (int?)s["x"] ?? 25;
                    int sy = (int?)s["y"] ?? 25;
                    spawn = new Vector2Int(sx, sy);
                }

                var gridData = new Dictionary<Vector2Int, GridCell>();
                var cells = root["cells"] as JObject;
                if (cells != null)
                {
                    foreach (var prop in cells.Properties())
                    {
                        var uid = prop.Name;
                        if (string.IsNullOrEmpty(uid)) continue;
                        var pos = ParseUid(uid);
                        if (pos == null) continue;
                        var cd = prop.Value as JObject;
                        if (cd == null) continue;

                        var cell = new GridCell(pos.Value.x, pos.Value.y);
                        cell.Uid = uid;
                        cell.TerrainType = (int?)cd["terrain"] ?? 0;
                        cell.Height = (int?)cd["height"] ?? 0;
                        cell.DecorationType = GridCell.MigrateOldDecorationType((int?)cd["decoration"] ?? 0);
                        cell.CustomData = (string)cd["custom"] ?? "";
                        cell.RefreshTerrainConfig();

                        gridData[pos.Value] = cell;
                    }
                }

                Debug.Log($"[MapDataManager] 加载地图成功: {mapName} 格子数={gridData.Count}, bounds={bounds}");
                return gridData;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MapDataManager] 加载失败: {ex.Message}");
                return new Dictionary<Vector2Int, GridCell>();
            }
        }

        /// <summary>兼容接口：忽略输出参数。</summary>
        public static Dictionary<Vector2Int, GridCell> LoadMapFromJson(string mapName)
        {
            return LoadMapFromJson(mapName, out _, out _, out _);
        }

        /// <summary>保存地图到 map.json（保持原 schema）。</summary>
        public static bool SaveMapToJson(
            string mapName,
            Dictionary<Vector2Int, GridCell> gridData,
            string displayName,
            RectInt bounds,
            Vector2Int spawn)
        {
            try
            {
                string jsonPath = Path.Combine(MapsRoot, mapName, JsonFilename);
                Directory.CreateDirectory(Path.GetDirectoryName(jsonPath));

                var cells = new JObject();
                var sorted = new List<GridCell>(gridData.Values);
                sorted.Sort((a, b) => a.Pos.y != b.Pos.y ? a.Pos.y.CompareTo(b.Pos.y) : a.Pos.x.CompareTo(b.Pos.x));
                foreach (var cell in sorted)
                {
                    var obj = new JObject
                    {
                        ["terrain"] = cell.TerrainType,
                        ["height"] = cell.Height,
                        ["custom"] = cell.CustomData ?? ""
                    };
                    if (cell.DecorationType != 0)
                        obj["decoration"] = cell.DecorationType;
                    cells[cell.Uid] = obj;
                }

                var root = new JObject
                {
                    ["version"] = CurrentMapVersion,
                    ["display_name"] = displayName,
                    ["bounds"] = new JObject { ["x"] = bounds.x, ["y"] = bounds.y, ["w"] = bounds.width, ["h"] = bounds.height },
                    ["spawn"] = new JObject { ["x"] = spawn.x, ["y"] = spawn.y },
                    ["cells"] = cells
                };

                string json = root.ToString(Formatting.Indented);
                // 不写 BOM（对齐 Godot FileAccess 的输出）：仓库 Python 工具链按 utf-8 读取，带 BOM 会解析失败
                File.WriteAllText(jsonPath, json, new System.Text.UTF8Encoding(false));
                Debug.Log($"[MapDataManager] 保存地图成功: {mapName}");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MapDataManager] 保存失败: {ex.Message}");
                return false;
            }
        }

        public static bool SaveMapToJson(string mapName, Dictionary<Vector2Int, GridCell> gridData)
        {
            return SaveMapToJson(mapName, gridData, mapName, CalculateBounds(gridData), new Vector2Int(25, 25));
        }

        /// <summary>从地图数据中查找出生点建筑（60000）的位置。</summary>
        public static Vector2Int FindSpawnPointFromGridData(Dictionary<Vector2Int, GridCell> gridData, Vector2Int fallbackSpawn)
        {
            int spawnPointConfigId = BuildingType.GetConfigBaseId(BuildingType.SpawnPoint);
            foreach (var cell in gridData.Values)
            {
                if (cell.DecorationType == spawnPointConfigId)
                    return cell.Pos;
            }
            return fallbackSpawn;
        }

        public static RectInt CalculateBounds(Dictionary<Vector2Int, GridCell> gridData)
        {
            if (gridData.Count == 0)
                return new RectInt(0, 0, 50, 50);

            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            foreach (var pos in gridData.Keys)
            {
                if (pos.x < minX) minX = pos.x;
                if (pos.x > maxX) maxX = pos.x;
                if (pos.y < minY) minY = pos.y;
                if (pos.y > maxY) maxY = pos.y;
            }
            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private static Vector2Int? ParseUid(string uid)
        {
            var parts = uid.Split('_');
            if (parts.Length < 2) return null;
            if (int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
                return new Vector2Int(x, y);
            return null;
        }
    }
}
