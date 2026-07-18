using UnityEngine;

namespace UnityClientSharp.Map.Core
{
    /// <summary>
    /// 格子数据类 — 存储单个格子的地形属性与状态。
    /// 移植自 clinetcsharp/Scripts/GridCell.cs：去除 RefCounted / Godot.Collections，
    /// 仅保留纯 C# 字段与运行时配置绑定；序列化在 MapDataManager 中完成（schema 与原 map.json 一致）。
    /// </summary>
    public class GridCell
    {
        // 坐标（网格中的位置）
        public Vector2Int Pos = Vector2Int.zero;

        /// <summary>格子唯一标识符（UID），创建时基于坐标生成，格式 "x_y"。生命周期内不变。</summary>
        public string Uid = "";

        // 地形属性（TerrainType 仅保留底层平地标识，0=普通平地）
        public int TerrainType;
        public int Height;            // 高度层级 (0-9)

        // 运行时绑定的地形配置（从 terrain_config.json 加载）
        public TerrainConfig TerrainConfig;

        // 装饰类型: 0=无, 否则为 build_cfg_id
        public int DecorationType;

        // 扩展数据
        public string CustomData = "";

        // 运行时临时数据（不保存）
        public bool IsDirty;

        public GridCell() { Pos = Vector2Int.zero; }

        public GridCell(int x, int y)
        {
            Pos = new Vector2Int(x, y);
            Uid = $"{x}_{y}";
        }

        /// <summary>刷新绑定的地形配置（从 Luban 表读取）</summary>
        public void RefreshTerrainConfig()
        {
            TerrainConfig = TerrainConfigUtil.Get(TerrainType);
        }

        /// <summary>获取地形类型名称</summary>
        public string GetTerrainName()
        {
            return TerrainConfig?.Name ?? "未知";
        }

        /// <summary>获取地形颜色（0..1 区间的 RGBA）</summary>
        public Color GetTerrainColor()
        {
            if (TerrainConfig == null)
                return Color.clear;

            return new Color(
                TerrainConfig.ColorR / 255f,
                TerrainConfig.ColorG / 255f,
                TerrainConfig.ColorB / 255f,
                TerrainConfig.ColorA);
        }

        /// <summary>复制数据到另一个格子（不复制 Uid）</summary>
        public void CopyTo(GridCell other)
        {
            other.TerrainType = TerrainType;
            other.Height = Height;
            other.DecorationType = DecorationType;
            other.CustomData = CustomData;
            other.TerrainConfig = TerrainConfig;
        }

        public override string ToString()
        {
            return $"GridCell(uid={Uid} pos={Pos.x},{Pos.y}) terrain={TerrainType} name={GetTerrainName()}";
        }

        /// <summary>兼容旧地图 decoration 字段：10/11/12 -> 新的 build_cfg_id</summary>
        internal static int MigrateOldDecorationType(int oldValue)
        {
            return oldValue switch
            {
                1 => BuildingType.GetConfigBaseId(BuildingType.House) + 1, // 旧房舍
                10 => BuildingType.GetConfigBaseId(BuildingType.House) + 1,  // 房舍
                11 => BuildingType.GetConfigBaseId(BuildingType.Shop) + 1,   // 商店子配置
                12 => BuildingType.GetConfigBaseId(BuildingType.Shop) + 2,   // 商店子配置
                _ => oldValue,
            };
        }
    }
}
