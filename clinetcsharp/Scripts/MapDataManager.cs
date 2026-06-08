using Godot;
using Godot.Collections;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// 地图数据管理器
    /// 负责地图的加载、保存、CSV导入导出
    /// </summary>
    [GlobalClass]
    public partial class MapDataManager : Node
    {
        public const string MapsFolder = "res://maps/";
        public const string CsvFilename = "map.csv";
        public const string ConfigFilename = "config.cfg";

        /// <summary>
        /// 创建默认地图数据 (50x50, 全部为普通地形)
        /// </summary>
        public static List<List<GridCell>> CreateDefaultGridData(int width, int height)
        {
            var gridData = new List<List<GridCell>>();
            for (int y = 0; y < height; y++)
            {
                var row = new List<GridCell>();
                for (int x = 0; x < width; x++)
                {
                    var cell = new GridCell(x, y);
                    cell.TerrainType = 0;
                    row.Add(cell);
                }
                gridData.Add(row);
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
        /// 创建新地图 (文件夹 + 默认CSV)
        /// </summary>
        public static Error CreateNewMap(string mapName, int width = 50, int height = 50)
        {
            // 创建地图文件夹
            var mapPath = MapsFolder + mapName + "/";
            var err = DirAccess.MakeDirRecursiveAbsolute(mapPath);
            if (err != Error.Ok)
            {
                GD.PushError("创建地图文件夹失败: " + mapPath);
                return err;
            }

            // 创建默认网格数据
            var gridData = CreateDefaultGridData(width, height);

            // 保存CSV
            err = SaveMapToCsv(mapName, gridData);
            if (err != Error.Ok)
                return err;

            // 创建默认配置
            var config = new ConfigFile();
            config.SetValue("map", "width", width);
            config.SetValue("map", "height", height);
            config.SetValue("map", "spawn_x", width / 2);
            config.SetValue("map", "spawn_y", height / 2);
            config.SetValue("map", "display_name", mapName);

            err = config.Save(mapPath + ConfigFilename);
            if (err != Error.Ok)
            {
                GD.PushError("保存地图配置失败: " + mapPath + ConfigFilename);
                return err;
            }

            GD.Print("[MapDataManager] 创建新地图成功: " + mapName);
            return Error.Ok;
        }

        /// <summary>
        /// 从CSV加载地图
        /// </summary>
        public static List<List<GridCell>> LoadMapFromCsv(string mapName)
        {
            var csvPath = MapsFolder + mapName + "/" + CsvFilename;

            // 如果文件不存在,返回空数组
            if (!FileAccess.FileExists(csvPath))
            {
                GD.Print("[MapDataManager] 地图CSV不存在,将创建默认地图: " + mapName);
                return new List<List<GridCell>>();
            }

            var file = FileAccess.Open(csvPath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PushError("打开CSV失败: " + csvPath);
                return new List<List<GridCell>>();
            }

            var gridData = new List<List<GridCell>>();
            int y = 0;

            while (!file.EofReached())
            {
                var line = file.GetLine().StripEdges();

                // 跳过空行和注释行
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    continue;

                var values = line.Split(",");
                var row = new List<GridCell>();

                for (int x = 0; x < values.Length; x++)
                {
                    var cell = new GridCell(x, y);

                    // 解析值
                    var val = values[x].StripEdges();

                    // 格式: terrain;height;custom
                    if (val.Contains(";"))
                    {
                        var parts = new Array();
                        foreach (var p in val.Split(";"))
                            parts.Add(p);
                        cell.FromCsvValues(parts);
                    }
                    else
                    {
                        // 简单格式: 只有一个值表示 terrain
                        cell.TerrainType = int.TryParse(val, out var t) ? t : 0;
                        cell.RefreshTerrainConfig();
                    }

                    row.Add(cell);
                }

                if (row.Count > 0)
                {
                    gridData.Add(row);
                    y++;
                }
            }

            file.Close();

            var actualWidth = gridData.Count > 0 ? gridData[0].Count : 0;
            GD.Print($"[MapDataManager] 加载地图成功: {mapName} 尺寸: {actualWidth}x{gridData.Count}");

            // 统一应用地形配置，确保本地 CSV 与服务端 Walkable 一致，避免同步后突变
            foreach (var row in gridData)
            {
                foreach (var cell in row)
                {
                    cell.RefreshTerrainConfig();
                }
            }

            return gridData;
        }

        /// <summary>
        /// 保存地图到CSV（同时保存到客户端运行时目录和 tables 源目录）
        /// </summary>
        public static Error SaveMapToCsv(string mapName, List<List<GridCell>> gridData)
        {
            // 1. 保存到客户端运行时目录 (res://maps/)
            var csvPath = MapsFolder + mapName + "/" + CsvFilename;
            var err = WriteMapCsv(csvPath, gridData);
            if (err != Error.Ok)
                return err;

            // 2. 同时保存到 tables 源目录，供 Luban 导表使用
            try
            {
                var projectRoot = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"), ".."));
                var tablesPath = System.IO.Path.Combine(projectRoot, "tables", "datas", "maps", mapName, CsvFilename);
                var tablesDir = System.IO.Path.GetDirectoryName(tablesPath);
                if (!string.IsNullOrEmpty(tablesDir) && !System.IO.Directory.Exists(tablesDir))
                    System.IO.Directory.CreateDirectory(tablesDir);

                var content = BuildMapCsvContent(gridData);
                System.IO.File.WriteAllText(tablesPath, content, System.Text.Encoding.UTF8);
                GD.Print($"[MapDataManager] 同步保存到 tables: {tablesPath}");
            }
            catch (System.Exception ex)
            {
                GD.PushError($"[MapDataManager] 同步保存到 tables 失败: {ex.Message}");
                // 不影响主保存流程，只记录错误
            }

            GD.Print("[MapDataManager] 保存地图成功: " + mapName);
            return Error.Ok;
        }

        private static string BuildMapCsvContent(List<List<GridCell>> gridData)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# Grid Map Data");
            sb.AppendLine("# Format: terrain;height;custom");
            sb.AppendLine("# terrain: 0=normal, 1=water, 2=grass, 3=sand, 4=rock, 5=snow, 6=swamp, 7=lava, 8=holy, 9=wall, 10=airwall");
            sb.AppendLine("# height: 0-9");
            sb.AppendLine("# custom: optional string");

            foreach (var row in gridData)
            {
                var lineParts = new System.Collections.Generic.List<string>();
                foreach (var cell in row)
                {
                    var cellStr = $"{cell.TerrainType};{cell.Height};{cell.CustomData}";
                    lineParts.Add(cellStr);
                }
                sb.AppendLine(string.Join(",", lineParts));
            }
            return sb.ToString();
        }

        private static Error WriteMapCsv(string csvPath, List<List<GridCell>> gridData)
        {
            var file = FileAccess.Open(csvPath, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PushError("创建CSV文件失败: " + csvPath);
                return FileAccess.GetOpenError();
            }

            file.StoreLine("# Grid Map Data");
            file.StoreLine("# Format: terrain;height;custom");
            file.StoreLine("# terrain: 0=normal, 1=water, 2=grass, 3=sand, 4=rock, 5=snow, 6=swamp, 7=lava, 8=holy, 9=wall, 10=airwall");
            file.StoreLine("# height: 0-9");
            file.StoreLine("# custom: optional string");

            foreach (var row in gridData)
            {
                var lineParts = new System.Collections.Generic.List<string>();
                foreach (var cell in row)
                {
                    var cellStr = $"{cell.TerrainType};{cell.Height};{cell.CustomData}";
                    lineParts.Add(cellStr);
                }
                file.StoreLine(string.Join(",", lineParts));
            }

            file.Close();
            return Error.Ok;
        }

        /// <summary>
        /// 加载地图配置
        /// </summary>
        public static Dictionary LoadMapConfig(string mapName)
        {
            var configPath = MapsFolder + mapName + "/" + ConfigFilename;
            var result = new Dictionary
            {
                ["width"] = 50,
                ["height"] = 50,
                ["spawn_x"] = 25,
                ["spawn_y"] = 25,
                ["display_name"] = mapName
            };

            var config = new ConfigFile();
            var err = config.Load(configPath);
            if (err != Error.Ok)
                return result;

            result["width"] = (int)config.GetValue("map", "width", 50);
            result["height"] = (int)config.GetValue("map", "height", 50);
            result["spawn_x"] = (int)config.GetValue("map", "spawn_x", result["width"].AsInt32() / 2);
            result["spawn_y"] = (int)config.GetValue("map", "spawn_y", result["height"].AsInt32() / 2);
            result["display_name"] = (string)config.GetValue("map", "display_name", mapName);

            return result;
        }

        /// <summary>
        /// 保存地图配置
        /// </summary>
        public static Error SaveMapConfig(string mapName, Dictionary configDict)
        {
            var configPath = MapsFolder + mapName + "/" + ConfigFilename;
            var config = new ConfigFile();

            // 先加载现有配置
            config.Load(configPath);

            // 更新值
            foreach (var key in configDict.Keys)
            {
                config.SetValue("map", (string)key, configDict[key]);
            }

            return config.Save(configPath);
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
                // 文件夹不存在,创建它
                DirAccess.MakeDirRecursiveAbsolute(MapsFolder);
                return maps;
            }

            dir.ListDirBegin();
            var folderName = dir.GetNext();

            while (folderName != "")
            {
                if (dir.CurrentIsDir() && !folderName.StartsWith("."))
                {
                    // 检查是否有map.csv文件
                    var csvPath = MapsFolder + folderName + "/" + CsvFilename;
                    if (FileAccess.FileExists(csvPath))
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
        /// 导出CSV到指定路径 (用于用户导出)
        /// </summary>
        public static Error ExportCsv(string mapName, string exportPath)
        {
            var sourcePath = MapsFolder + mapName + "/" + CsvFilename;

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

            // 复制内容
            while (!sourceFile.EofReached())
            {
                targetFile.StoreLine(sourceFile.GetLine());
            }

            sourceFile.Close();
            targetFile.Close();

            return Error.Ok;
        }

        /// <summary>
        /// 从指定路径导入CSV (用于用户导入)
        /// </summary>
        public static Error ImportCsv(string importPath, string mapName)
        {
            if (!FileAccess.FileExists(importPath))
                return Error.FileNotFound;

            // 确保地图文件夹存在
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

            var targetPath = mapPath + CsvFilename;
            var targetFile = FileAccess.Open(targetPath, FileAccess.ModeFlags.Write);
            if (targetFile == null)
            {
                sourceFile.Close();
                return FileAccess.GetOpenError();
            }

            // 复制内容
            while (!sourceFile.EofReached())
            {
                targetFile.StoreLine(sourceFile.GetLine());
            }

            sourceFile.Close();
            targetFile.Close();

            GD.Print("[MapDataManager] 导入CSV成功: " + importPath + " -> " + targetPath);
            return Error.Ok;
        }

        /// <summary>
        /// 验证CSV数据
        /// </summary>
        public static Dictionary ValidateCsvData(string filePath)
        {
            var result = new Dictionary
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

            int rowCount = 0;
            int expectedColumns = -1;
            int lineNumber = 0;

            while (!file.EofReached())
            {
                var line = file.GetLine().StripEdges();
                lineNumber++;

                // 跳过空行和注释行
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    continue;

                var values = line.Split(",");
                rowCount++;

                // 检查列数一致性
                if (expectedColumns == -1)
                {
                    expectedColumns = values.Length;
                    if (expectedColumns == 0)
                    {
                        result["valid"] = false;
                        ((Array)result["errors"]).Add($"第{lineNumber}行: 没有数据列");
                        continue;
                    }
                }
                else if (values.Length != expectedColumns)
                {
                    result["valid"] = false;
                    ((Array)result["errors"]).Add($"第{lineNumber}行: 列数不一致 (期望{expectedColumns}, 实际{values.Length})");
                }

                // 检查每列的值
                for (int i = 0; i < values.Length; i++)
                {
                    var val = values[i].StripEdges();

                    // 检查复杂格式
                    if (val.Contains(";"))
                    {
                        var parts = val.Split(";");
                        if (parts.Length >= 4)
                        {
                            // 验证 terrain_type (0-6)
                            if (int.TryParse(parts[3], out int terrain))
                            {
                                if (terrain < 0 || terrain > 6)
                                {
                                    ((Array)result["warnings"]).Add($"第{lineNumber}行第{i + 1}列: 地形类型{terrain}超出范围(0-6)");
                                }
                            }

                            // 验证 height (0-9)
                            if (parts.Length >= 5 && int.TryParse(parts[4], out int height))
                            {
                                if (height < 0 || height > 9)
                                {
                                    ((Array)result["warnings"]).Add($"第{lineNumber}行第{i + 1}列: 高度值{height}超出范围(0-9)");
                                }
                            }
                        }
                    }
                }
            }

            file.Close();

            // 检查行数范围
            if (rowCount == 0)
            {
                result["valid"] = false;
                ((Array)result["errors"]).Add("文件没有有效数据");
            }
            else if (rowCount > 200)
            {
                ((Array)result["warnings"]).Add($"地图行数较多({rowCount})，可能影响性能");
            }

            return result;
        }
    }
}
