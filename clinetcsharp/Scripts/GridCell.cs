using Godot;
using Godot.Collections;

namespace ClinetCSharp
{
    /// <summary>
    /// 格子数据类
    /// 存储单个格子的所有属性和状态
    /// </summary>
    [GlobalClass]
    public partial class GridCell : RefCounted
    {
        // 坐标ID (在网格中的位置)
        public Vector2I Pos { get; set; } = Vector2I.Zero;

        // 基础状态
        public bool Exists { get; set; } = true;        // 是否存在（false = 虚空/未创建）
        public bool Walkable { get; set; } = true;      // 是否可行走 (false = 障碍/墙)
        public bool Visible { get; set; } = true;       // 是否可见 (false = 不渲染网格线)

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
            if (TerrainConfig != null)
            {
                // 以配置表为准覆盖 Walkable (服务端权威)
                Walkable = TerrainConfig.Walkable;
            }
        }

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
        }

        /// <summary>
        /// 转换为字典 (用于序列化)
        /// </summary>
        public Dictionary ToDict()
        {
            return new Dictionary
            {
                ["x"] = Pos.X,
                ["y"] = Pos.Y,
                ["exists"] = Exists,
                ["walkable"] = Walkable,
                ["visible"] = Visible,
                ["terrain"] = TerrainType,
                ["height"] = Height,
                ["custom"] = CustomData
            };
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
            if (dict.ContainsKey("exists"))
                Exists = (bool)dict["exists"];
            if (dict.ContainsKey("walkable"))
                Walkable = (bool)dict["walkable"];
            if (dict.ContainsKey("visible"))
                Visible = (bool)dict["visible"];
            if (dict.ContainsKey("terrain"))
                TerrainType = (int)dict["terrain"];
            if (dict.ContainsKey("height"))
                Height = (int)dict["height"];
            if (dict.ContainsKey("custom"))
                CustomData = (string)dict["custom"];

            RefreshTerrainConfig();
        }

        /// <summary>
        /// 转换为CSV行 (不包含坐标,用于简化格式)
        /// </summary>
        public Godot.Collections.Array ToCsvValues()
        {
            return new Array
            {
                Exists ? "1" : "0",
                Walkable ? "1" : "0",
                Visible ? "1" : "0",
                TerrainType.ToString(),
                Height.ToString(),
                CustomData
            };
        }

        /// <summary>
        /// 从简化CSV格式加载 (exists,walkable,visible,terrain,height,custom)
        /// </summary>
        public void FromCsvValues(Godot.Collections.Array values)
        {
            if (values.Count >= 1)
            {
                var val = values[0].AsString();
                Exists = val == "1" || val == "true";
            }
            if (values.Count >= 2)
            {
                var val = values[1].AsString();
                Walkable = val == "1" || val == "true";
            }
            if (values.Count >= 3)
            {
                var val = values[2].AsString();
                Visible = val == "1" || val == "true";
            }
            if (values.Count >= 4)
                TerrainType = values[3].AsInt32();
            if (values.Count >= 5)
                Height = values[4].AsInt32();
            if (values.Count >= 6)
                CustomData = values[5].AsString();

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
            other.Exists = Exists;
            other.Walkable = Walkable;
            other.Visible = Visible;
            other.TerrainType = TerrainType;
            other.Height = Height;
            other.CustomData = CustomData;
            other.TerrainConfig = TerrainConfig;
        }

        public override string ToString()
        {
            return $"GridCell({Pos.X},{Pos.Y}) exists={Exists} walkable={Walkable} visible={Visible} terrain={TerrainType} name={GetTerrainName()}";
        }
    }
}
