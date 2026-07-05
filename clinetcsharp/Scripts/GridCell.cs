using Godot;
using Godot.Collections;

namespace ClinetCSharp
{
    /// <summary>
    /// 格子数据类
    /// 存储单个格子的地形属性和状态
    /// </summary>
    [GlobalClass]
    public partial class GridCell : RefCounted
    {
        // 坐标ID (在网格中的位置)
        public Vector2I Pos { get; set; } = Vector2I.Zero;

        /// <summary>
        /// 格子唯一标识符（UID），创建时基于坐标生成，格式 "x,y"。
        /// UID 在格子生命周期内保持不变，即使地图拓展导致逻辑坐标改变。
        /// </summary>
        public string Uid { get; set; } = "";

        // 地形属性
        public int TerrainType { get; set; } = 0;       // 地形类型: 0=普通, 1=水, 2=草地, 3=沙地, 4=岩石...
        public int Height { get; set; } = 0;            // 高度层级 (0-9, 用于高低差系统)

        // 运行时绑定的 Luban 配置 (从 terrain_config.json 加载)
        public TerrainConfig? TerrainConfig { get; set; }

        /// <summary>
        /// 刷新绑定的地形配置 (从 Luban 表读取)
        /// </summary>
        public void RefreshTerrainConfig()
        {
            TerrainConfig = TerrainConfigUtil.Get(TerrainType);
        }

        // 装饰类型: 0=无, 1=房舍, 后续扩展
        public int DecorationType { get; set; } = 0;

        // 扩展数据
        public string CustomData { get; set; } = "";    // 自定义数据字符串 (可用于标记特殊属性)

        // 运行时临时数据 (不保存到CSV)
        public bool IsDirty { get; set; } = false;      // 是否被修改过 (用于保存优化)

        public GridCell()
        {
            Pos = Vector2I.Zero;
        }

        public GridCell(int x, int y)
        {
            Pos = new Vector2I(x, y);
            Uid = $"{x}_{y}";
        }

        /// <summary>
        /// 转换为字典 (用于序列化)
        /// </summary>
        public Dictionary ToDict()
        {
            var dict = new Dictionary
            {
                ["x"] = Pos.X,
                ["y"] = Pos.Y,
                ["terrain"] = TerrainType,
                ["height"] = Height,
                ["custom"] = CustomData
            };
            if (DecorationType != 0)
                dict["decoration"] = DecorationType;
            return dict;
        }

        /// <summary>
        /// 从字典恢复
        /// </summary>
        public void FromDict(Dictionary dict)
        {
            if (dict.ContainsKey("x") && dict.ContainsKey("y"))
            {
                Pos = new Vector2I((int)dict["x"], (int)dict["y"]);
            }
            if (dict.ContainsKey("uid"))
                Uid = (string)dict["uid"];
            if (dict.ContainsKey("terrain"))
                TerrainType = (int)dict["terrain"];
            if (dict.ContainsKey("height"))
                Height = (int)dict["height"];
            if (dict.ContainsKey("custom"))
                CustomData = (string)dict["custom"];
            if (dict.ContainsKey("decoration"))
                DecorationType = MigrateOldDecorationType((int)dict["decoration"]);

            RefreshTerrainConfig();
        }

        /// <summary>兼容旧地图 decoration 字段：10/11/12 → 新的 build_cfg_id</summary>
        internal static int MigrateOldDecorationType(int oldValue)
        {
            return oldValue switch
            {
                10 => BuildingType.GetConfigBaseId(BuildingType.House),     // 房舍
                11 => BuildingType.GetConfigBaseId(BuildingType.Shop) + 1,  // 商店子配置
                12 => BuildingType.GetConfigBaseId(BuildingType.Shop) + 2,  // 商店子配置
                _ => oldValue,
            };
        }

        /// <summary>
        /// 转换为CSV行 (uid;terrain;height;custom)
        /// </summary>
        public Godot.Collections.Array ToCsvValues()
        {
            return new Array
            {
                Uid,
                TerrainType.ToString(),
                Height.ToString(),
                CustomData
            };
        }

        /// <summary>
        /// 从CSV格式加载。
        /// 兼容两种格式：
        /// - 新格式: uid;terrain;height;custom  (parts[0] 包含逗号)
        /// - 旧格式: terrain;height;custom       (parts[0] 是纯数字)
        /// </summary>
        public void FromCsvValues(Godot.Collections.Array values)
        {
            if (values.Count == 0) return;

            // 判断格式：新格式有 4 个字段（uid;terrain;height;custom），旧格式 2~3 个字段
            bool isNewFormat = values.Count >= 4;

            if (isNewFormat)
            {
                // 新格式: uid;terrain;height;custom
                if (values.Count >= 1)
                    Uid = values[0].AsString();
                if (values.Count >= 2)
                    TerrainType = values[1].AsInt32();
                if (values.Count >= 3)
                    Height = values[2].AsInt32();
                if (values.Count >= 4)
                    CustomData = values[3].AsString();
            }
            else
            {
                // 旧格式: terrain;height;custom
                if (values.Count >= 1)
                    TerrainType = values[0].AsInt32();
                if (values.Count >= 2)
                    Height = values[1].AsInt32();
                if (values.Count >= 3)
                    CustomData = values[2].AsString();
            }

            RefreshTerrainConfig();
        }

        /// <summary>
        /// 获取地形类型名称 (从 Luban 配置读取，优先于硬编码)
        /// </summary>
        public string GetTerrainName()
        {
            return TerrainConfig?.Name ?? "未知";
        }

        /// <summary>
        /// 获取地形颜色 (从 Luban 配置读取)
        /// </summary>
        public Color GetTerrainColor()
        {
            if (TerrainConfig == null)
                return Colors.Transparent;

            return new Color(
                TerrainConfig.ColorR / 255f,
                TerrainConfig.ColorG / 255f,
                TerrainConfig.ColorB / 255f,
                TerrainConfig.ColorA
            );
        }

        /// <summary>
        /// 复制数据到另一个格子
        /// </summary>
        public void CopyTo(GridCell other)
        {
            other.TerrainType = TerrainType;
            other.Height = Height;
            other.DecorationType = DecorationType;
            other.CustomData = CustomData;
            other.TerrainConfig = TerrainConfig;
            // 注意：不复制 Uid，UID 是格子的唯一身份标识
        }

        public override string ToString()
        {
            return $"GridCell(uid={Uid} pos={Pos.X},{Pos.Y}) terrain={TerrainType} name={GetTerrainName()}";
        }
    }
}
