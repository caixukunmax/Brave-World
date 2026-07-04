using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ClinetCSharp
{
    /// <summary>
    /// 地图数据管理器
    /// 负责地图的加载、保存、JSON 导入导出
    /// </summary>
    [GlobalClass]
    public partial class MapDataManager : Node
    {
        public const string MapsFolder = "res://maps/";
        public const string JsonFilename = "map.json";

        /// <summary>
        /// 当前地图数据格式版本号。
        /// </summary>
        public const int CurrentMapVersion = 3;

        /// <summary>
        /// 最小支持的地图数据格式版本号。
        /// </summary>
        public const int MinSupportedMapVersion = 2;

        /// <summary>
        /// 创建默认地图数据 (50x50, 全部为普通地形)，返回稀疏字典。
        /// </summary>
        public static System.Collections.Generic.Dictionary<Vector2I, GridCell> CreateDefaultGridData(int width, int height, int originX = 0, int originY = 0)
        {
            var gridData = new System.Collections.Generic.Dictionary<Vector2I, GridCell>();
            for (int y = originY; y < originY + height; y++)
            {
                for (int x = originX; x < originX + width; x++)
                {
                    var cell = new GridCell(x, y);
                    cell.TerrainType = 0;
                    gridData[new Vector2I(x, y)] = cell;
                }
            }
            return gridData;
        }

        /// <summary>
        /// 检查地图是否存在
        /// </summary>
        public static bool MapExists(string mapName)
        {
            var dir = DirAccess.Open(MapsFolder);
            if (dir == null)
                return false;
            return dir.DirExists(mapName);
        }

        /// <summary>
        /// 创建新地图 (文件夹 + 默认 map.json)
        /// </summary>
        public static Error CreateNewMap(string mapName, int width = 50, int height = 50)
        {
            var mapPath = MapsFolder + mapName + "/";
            var err = DirAccess.MakeDirRecursiveAbsolute(mapPath);
            if (err != Error.Ok)
            {
                GD.PushError("创建地图文件夹失败: " + mapPath);
                return err;
            }

            var gridData = CreateDefaultGridData(width, height);
            var bounds = new Rect2I(0, 0, width, height);
            var spawn = new Vector2I(width / 2, height / 2);

            err = SaveMapToJson(mapName, gridData, mapName, bounds, spawn);
            if (err != Error.Ok)
                return err;

            GD.Print("[MapDataManager] 创建新地图成功: " + mapName);
            return Error.Ok;
        }

        /// <summary>
        /// 从 map.json 加载地图，返回稀疏字典。
        /// </summary>
        public static System.Collections.Generic.Dictionary<Vector2I, GridCell> LoadMapFromJson(string mapName, out Rect2I bounds, out Vector2I spawn, out string displayName)
        {
            bounds = new Rect2I(0, 0, 50, 50);
            spawn = new Vector2I(25, 25);
            displayName = mapName;

            var jsonPath = MapsFolder + mapName + "/" + JsonFilename;
            GD.Print($"[MapDataManager] LoadMapFromJson: 尝试加载 '{jsonPath}'");

            if (!FileAccess.FileExists(jsonPath))
            {
                GD.Print($"[MapDataManager] 地图 JSON 不存在，将创建默认地图: {mapName}");
                return new System.Collections.Generic.Dictionary<Vector2I, GridCell>();
            }

            var file = FileAccess.Open(jsonPath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PushError("打开 JSON 失败: " + jsonPath);
                return new System.Collections.Generic.Dictionary<Vector2I, GridCell>();
            }

            var jsonText = file.GetAsText();
            file.Close();

            var json = new Json();
            var parseErr = json.Parse(jsonText);
            if (parseErr != Error.Ok)
            {
                GD.PushError($"解析地图 JSON 失败: {jsonPath}, error={(int)parseErr}");
                return new System.Collections.Generic.Dictionary<Vector2I, GridCell>();
            }

            var root = json.Data.AsGodotDictionary();
            if (root == null)
            {
                GD.PushError($"地图 JSON 根对象不是字典: {jsonPath}");
                return new System.Collections.Generic.Dictionary<Vector2I, GridCell>();
            }

            int version = root.GetValueOrDefault("version", 0).AsInt32();
            if (version < MinSupportedMapVersion)
            {
                GD.PushError($"地图 JSON 版本过低: {jsonPath}, version={version}, expected>={MinSupportedMapVersion}");
                return new System.Collections.Generic.Dictionary<Vector2I, GridCell>();
            }
            if (version < CurrentMapVersion)
            {
                GD.Print($"[MapDataManager] 地图 JSON 版本较旧，将自动兼容: {jsonPath}, version={version}");
            }

            displayName = root.GetValueOrDefault("display_name", mapName).AsString();

            if (root.TryGetValue("bounds", out var boundsValue) && boundsValue.VariantType == Variant.Type.Dictionary)
            {
                var b = boundsValue.AsGodotDictionary();
                int bx = b.GetValueOrDefault("x", 0).AsInt32();
                int by = b.GetValueOrDefault("y", 0).AsInt32();
                int bw = b.GetValueOrDefault("w", 50).AsInt32();
                int bh = b.GetValueOrDefault("h", 50).AsInt32();
                bounds = new Rect2I(bx, by, bw, bh);
            }

            if (root.TryGetValue("spawn", out var spawnValue) && spawnValue.VariantType == Variant.Type.Dictionary)
            {
                var s = spawnValue.AsGodotDictionary();
                int sx = s.GetValueOrDefault("x", 25).AsInt32();
                int sy = s.GetValueOrDefault("y", 25).AsInt32();
                spawn = new Vector2I(sx, sy);
            }

            var gridData = new System.Collections.Generic.Dictionary<Vector2I, GridCell>();

            if (root.TryGetValue("cells", out var cellsValue) && cellsValue.VariantType == Variant.Type.Dictionary)
            {
                var cells = cellsValue.AsGodotDictionary();
                foreach (var key in cells.Keys)
                {
                    var uid = key.AsString();
                    if (string.IsNullOrEmpty(uid))
                        continue;

                    var pos = ParseUid(uid);
                    if (pos == null)
                        continue;

                    var cellData = cells[key];
                    if (cellData.VariantType != Variant.Type.Dictionary)
                        continue;

                    var cellDict = cellData.AsGodotDictionary();
                    var cell = new GridCell(pos.Value.X, pos.Value.Y);
                    cell.Uid = uid;
                    cell.TerrainType = cellDict.GetValueOrDefault("terrain", 0).AsInt32();
                    cell.Height = cellDict.GetValueOrDefault("height", 0).AsInt32();
                    cell.DecorationType = cellDict.GetValueOrDefault("decoration", 0).AsInt32();
                    cell.CustomData = cellDict.GetValueOrDefault("custom", "").AsString();
                    cell.RefreshTerrainConfig();

                    gridData[pos.Value] = cell;
                }
            }

            GD.Print($"[MapDataManager] 加载地图成功: {mapName} 格子数={gridData.Count}, bounds={bounds}");
            return gridData;
        }

        /// <summary>
        /// 兼容旧接口：加载时忽略输出参数。
        /// </summary>
        public static System.Collections.Generic.Dictionary<Vector2I, GridCell> LoadMapFromJson(string mapName)
        {
            return LoadMapFromJson(mapName, out _, out _, out _);
        }

        /// <summary>
        /// 保存地图到 map.json（同时保存到客户端运行时目录和 tables 源目录）
        /// </summary>
        public static Error SaveMapToJson(
            string mapName,
            System.Collections.Generic.Dictionary<Vector2I, GridCell> gridData,
            string displayName,
            Rect2I bounds,
            Vector2I spawn)
        {
            var jsonPath = MapsFolder + mapName + "/" + JsonFilename;
            var err = WriteMapJson(jsonPath, gridData, displayName, bounds, spawn);
            if (err != Error.Ok)
                return err;

            // 同时保存到 tables 源目录，供同步脚本使用
            try
            {
                var projectRoot = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"), ".."));
                var tablesPath = System.IO.Path.Combine(projectRoot, "tables", "datas", "maps", mapName, JsonFilename);
                var tablesDir = System.IO.Path.GetDirectoryName(tablesPath);
                if (!string.IsNullOrEmpty(tablesDir) && !System.IO.Directory.Exists(tablesDir))
                    System.IO.Directory.CreateDirectory(tablesDir);

                var content = BuildMapJsonContent(gridData, displayName, bounds, spawn);
                System.IO.File.WriteAllText(tablesPath, content, System.Text.Encoding.UTF8);
                GD.Print($"[MapDataManager] 同步保存到 tables: {tablesPath}");
            }
            catch (System.Exception ex)
            {
                GD.PushError($"[MapDataManager] 同步保存到 tables 失败: {ex.Message}");
            }

            GD.Print("[MapDataManager] 保存地图成功: " + mapName);
            return Error.Ok;
        }

        /// <summary>
        /// 自动从 gridData 计算边界。
        /// </summary>
        public static Error SaveMapToJson(
            string mapName,
            System.Collections.Generic.Dictionary<Vector2I, GridCell> gridData,
            string displayName,
            Vector2I spawn)
        {
            var bounds = CalculateBounds(gridData);
            return SaveMapToJson(mapName, gridData, displayName, bounds, spawn);
        }

        /// <summary>
        /// 兼容旧接口：使用当前 MapBounds 和默认 spawn。
        /// </summary>
        public static Error SaveMapToJson(
            string mapName,
            System.Collections.Generic.Dictionary<Vector2I, GridCell> gridData)
        {
            var bounds = CalculateBounds(gridData);
            return SaveMapToJson(mapName, gridData, mapName, bounds, new Vector2I(25, 25));
        }

        private static Rect2I CalculateBounds(System.Collections.Generic.Dictionary<Vector2I, GridCell> gridData)
        {
            if (gridData.Count == 0)
                return new Rect2I(0, 0, 50, 50);

            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            foreach (var pos in gridData.Keys)
            {
                if (pos.X < minX) minX = pos.X;
                if (pos.X > maxX) maxX = pos.X;
                if (pos.Y < minY) minY = pos.Y;
                if (pos.Y > maxY) maxY = pos.Y;
            }
            return new Rect2I(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private static string BuildMapJsonContent(
            System.Collections.Generic.Dictionary<Vector2I, GridCell> gridData,
            string displayName,
            Rect2I bounds,
            Vector2I spawn)
        {
            var cells = new Godot.Collections.Dictionary();
            foreach (var cell in gridData.Values.OrderBy(c => c.Pos.Y).ThenBy(c => c.Pos.X))
            {
                var cellDict = new Godot.Collections.Dictionary
                {
                    ["terrain"] = cell.TerrainType,
                    ["height"] = cell.Height,
                    ["custom"] = cell.CustomData ?? ""
                };
                if (cell.DecorationType != 0)
                    cellDict["decoration"] = cell.DecorationType;
                cells[cell.Uid] = cellDict;
            }

            var root = new Godot.Collections.Dictionary
            {
                ["version"] = CurrentMapVersion,
                ["display_name"] = displayName,
                ["bounds"] = new Godot.Collections.Dictionary
                {
                    ["x"] = bounds.Position.X,
                    ["y"] = bounds.Position.Y,
                    ["w"] = bounds.Size.X,
                    ["h"] = bounds.Size.Y
                },
                ["spawn"] = new Godot.Collections.Dictionary
                {
                    ["x"] = spawn.X,
                    ["y"] = spawn.Y
                },
                ["cells"] = cells
            };

            return Json.Stringify(root, "  ");
        }

        private static Error WriteMapJson(
            string jsonPath,
            System.Collections.Generic.Dictionary<Vector2I, GridCell> gridData,
            string displayName,
            Rect2I bounds,
            Vector2I spawn)
        {
            var file = FileAccess.Open(jsonPath, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PushError("创建 JSON 文件失败: " + jsonPath);
                return FileAccess.GetOpenError();
            }

            file.StoreString(BuildMapJsonContent(gridData, displayName, bounds, spawn));
            file.Close();
            return Error.Ok;
        }

        /// <summary>
        /// 解析 uid（格式 "x_y"），支持负坐标。
        /// </summary>
        private static Vector2I? ParseUid(string uid)
        {
            if (string.IsNullOrEmpty(uid))
                return null;

            // 支持负坐标：uid 形如 "-1_5"
            var parts = uid.Split('_');
            if (parts.Length != 2)
                return null;

            if (int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
                return new Vector2I(x, y);

            return null;
        }

        /// <summary>
        /// 获取所有地图列表
        /// </summary>
        public static List<string> GetMapList()
        {
            var maps = new List<string>();
            var dir = DirAccess.Open(MapsFolder);

            if (dir == null)
            {
                DirAccess.MakeDirRecursiveAbsolute(MapsFolder);
                return maps;
            }

            dir.ListDirBegin();
            var folderName = dir.GetNext();

            while (folderName != "")
            {
                if (dir.CurrentIsDir() && !folderName.StartsWith("."))
                {
                    var jsonPath = MapsFolder + folderName + "/" + JsonFilename;
                    if (FileAccess.FileExists(jsonPath))
                    {
                        maps.Add(folderName);
                    }
                }
                folderName = dir.GetNext();
            }

            dir.ListDirEnd();
            return maps;
        }

        /// <summary>
        /// 导出 map.json 到指定路径 (用于用户导出)
        /// </summary>
        public static Error ExportJson(string mapName, string exportPath)
        {
            var sourcePath = MapsFolder + mapName + "/" + JsonFilename;

            if (!FileAccess.FileExists(sourcePath))
            {
                GD.PushError("源地图不存在: " + sourcePath);
                return Error.FileNotFound;
            }

            var sourceFile = FileAccess.Open(sourcePath, FileAccess.ModeFlags.Read);
            if (sourceFile == null)
                return FileAccess.GetOpenError();

            var targetFile = FileAccess.Open(exportPath, FileAccess.ModeFlags.Write);
            if (targetFile == null)
            {
                sourceFile.Close();
                return FileAccess.GetOpenError();
            }

            targetFile.StoreString(sourceFile.GetAsText());

            sourceFile.Close();
            targetFile.Close();

            return Error.Ok;
        }

        /// <summary>
        /// 从指定路径导入 map.json (用于用户导入)
        /// </summary>
        public static Error ImportJson(string importPath, string mapName)
        {
            if (!FileAccess.FileExists(importPath))
                return Error.FileNotFound;

            var mapPath = MapsFolder + mapName + "/";
            if (!DirAccess.DirExistsAbsolute(mapPath))
            {
                var err = DirAccess.MakeDirRecursiveAbsolute(mapPath);
                if (err != Error.Ok)
                    return err;
            }

            var sourceFile = FileAccess.Open(importPath, FileAccess.ModeFlags.Read);
            if (sourceFile == null)
                return FileAccess.GetOpenError();

            var targetPath = mapPath + JsonFilename;
            var targetFile = FileAccess.Open(targetPath, FileAccess.ModeFlags.Write);
            if (targetFile == null)
            {
                sourceFile.Close();
                return FileAccess.GetOpenError();
            }

            targetFile.StoreString(sourceFile.GetAsText());

            sourceFile.Close();
            targetFile.Close();

            GD.Print("[MapDataManager] 导入 JSON 成功: " + importPath + " -> " + targetPath);
            return Error.Ok;
        }

        /// <summary>
        /// 验证 map.json 数据
        /// </summary>
        public static Godot.Collections.Dictionary ValidateMapJson(string filePath)
        {
            var result = new Godot.Collections.Dictionary
            {
                ["valid"] = true,
                ["errors"] = new Array(),
                ["warnings"] = new Array()
            };

            if (!FileAccess.FileExists(filePath))
            {
                result["valid"] = false;
                ((Array)result["errors"]).Add("文件不存在: " + filePath);
                return result;
            }

            var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                result["valid"] = false;
                ((Array)result["errors"]).Add("无法打开文件: " + filePath);
                return result;
            }

            var jsonText = file.GetAsText();
            file.Close();

            var json = new Json();
            var parseErr = json.Parse(jsonText);
            if (parseErr != Error.Ok)
            {
                result["valid"] = false;
                ((Array)result["errors"]).Add($"JSON 解析失败: {(int)parseErr}");
                return result;
            }

            var root = json.Data.AsGodotDictionary();
            if (root == null)
            {
                result["valid"] = false;
                ((Array)result["errors"]).Add("根对象不是字典");
                return result;
            }

            int version = root.GetValueOrDefault("version", 0).AsInt32();
            if (version < MinSupportedMapVersion)
            {
                result["valid"] = false;
                ((Array)result["errors"]).Add($"版本过低: {version}, 需要 >= {MinSupportedMapVersion}");
            }

            if (!root.ContainsKey("cells"))
            {
                result["valid"] = false;
                ((Array)result["errors"]).Add("缺少 cells 字段");
            }
            else if (root["cells"].VariantType != Variant.Type.Dictionary)
            {
                result["valid"] = false;
                ((Array)result["errors"]).Add("cells 字段必须是字典");
            }

            return result;
        }

        /// <summary>
        /// 重命名地图文件夹
        /// </summary>
        public static Error RenameMap(string oldName, string newName)
        {
            var oldPath = MapsFolder + oldName;
            var newPath = MapsFolder + newName;

            if (!DirAccess.DirExistsAbsolute(oldPath))
                return Error.DoesNotExist;
            if (DirAccess.DirExistsAbsolute(newPath))
                return Error.AlreadyExists;

            var err = DirAccess.RenameAbsolute(oldPath, newPath);
            if (err != Error.Ok)
                return err;

            // 更新 display_name
            var gridData = LoadMapFromJson(newName, out var bounds, out var spawn, out _);
            SaveMapToJson(newName, gridData, newName, bounds, spawn);

            return Error.Ok;
        }

        /// <summary>
        /// 删除地图文件夹（递归删除其内容后删除空目录）
        /// </summary>
        public static Error DeleteMap(string mapName)
        {
            var mapPath = MapsFolder + mapName;
            if (!DirAccess.DirExistsAbsolute(mapPath))
                return Error.DoesNotExist;

            var err = DeleteRecursive(mapPath);
            if (err != Error.Ok)
                return err;

            err = DirAccess.RemoveAbsolute(mapPath);
            return err == Error.Ok ? Error.Ok : Error.Failed;
        }

        /// <summary>
        /// 递归删除目录下的所有文件和子目录。
        /// </summary>
        private static Error DeleteRecursive(string path)
        {
            var dir = DirAccess.Open(path);
            if (dir == null)
                return DirAccess.GetOpenError();

            var err = dir.ListDirBegin();
            if (err != Error.Ok)
                return err;

            while (true)
            {
                string fileName = dir.GetNext();
                if (string.IsNullOrEmpty(fileName))
                    break;

                if (fileName == "." || fileName == "..")
                    continue;

                string fullPath = path + "/" + fileName;
                if (dir.CurrentIsDir())
                {
                    err = DeleteRecursive(fullPath);
                    if (err != Error.Ok)
                        return err;

                    err = DirAccess.RemoveAbsolute(fullPath);
                    if (err != Error.Ok)
                        return err;
                }
                else
                {
                    err = DirAccess.RemoveAbsolute(fullPath);
                    if (err != Error.Ok)
                        return err;
                }
            }

            dir.ListDirEnd();
            return Error.Ok;
        }
    }
}
