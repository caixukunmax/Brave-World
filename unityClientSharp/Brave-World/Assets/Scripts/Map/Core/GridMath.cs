using UnityEngine;

namespace UnityClientSharp.Map.Core
{
    /// <summary>
    /// 网格坐标 <-> 世界坐标 纯数学换算。
    /// 保持与 Godot 版 UiUtils.GridToWorld / WorldToGrid 完全等价（逻辑坐标，y 向下）。
    /// Y 轴朝向的显示翻转集中在渲染层（Quad 的 scale.y = -1），不在此处处理。
    /// </summary>
    public static class GridMath
    {
        /// <summary>网格坐标 -> 世界坐标（格子中心，对齐整数像素避免亚像素抖动）。</summary>
        public static Vector2 GridToWorld(int x, int y, int gridSize)
        {
            return new Vector2(
                Mathf.RoundToInt(x * gridSize + gridSize / 2f),
                Mathf.RoundToInt(y * gridSize + gridSize / 2f));
        }

        public static Vector2 GridToWorld(Vector2Int p, int gridSize)
        {
            return GridToWorld(p.x, p.y, gridSize);
        }

        /// <summary>世界坐标 -> 网格坐标（向下取整）。</summary>
        public static Vector2Int WorldToGrid(Vector2 w, int gridSize)
        {
            return new Vector2Int(
                Mathf.FloorToInt(w.x / gridSize),
                Mathf.FloorToInt(w.y / gridSize));
        }

        /// <summary>
        /// 逻辑坐标（Godot 语义，y 向下）-> Unity 世界坐标（y 向上）。
        /// 全项目唯一的 Y 翻转点：任何按格子/像素逻辑定位的渲染都必须经由这里（AGENTS.md 第 1 条）。
        /// </summary>
        public static Vector2 LogicToWorld(float logicX, float logicY) => new(logicX, -logicY);

        /// <summary>
        /// footprint 左上角锚点 + 占地尺寸 -> 渲染中心的世界坐标。
        /// 与 Godot EntityBase.GetWorldPositionForGridAnchor 等价（再经 LogicToWorld 翻转）。
        /// </summary>
        public static Vector3 FootprintCenterWorld(int anchorX, int anchorY, int sizeX, int sizeY, int gridSize)
        {
            sizeX = Mathf.Max(1, sizeX);
            sizeY = Mathf.Max(1, sizeY);
            var w = LogicToWorld(
                anchorX * gridSize + gridSize * sizeX / 2f,
                anchorY * gridSize + gridSize * sizeY / 2f);
            return new Vector3(w.x, w.y, 0f);
        }
    }
}
