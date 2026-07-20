using System.Collections.Generic;
using UnityClientSharp.Map.Core;
using UnityEngine;

namespace UnityClientSharp.Map.Rendering
{
    /// <summary>
    /// 网格管理器（Unity 控制器版）。
    /// 移植自 clinetcsharp/Scripts/GridManager.cs 的渲染相关部分：
    /// 持有 GridData/MapBounds，生成地形/水遮罩纹理，按相机 zoom 计算网格线渲染样式，提供坐标换算。
    /// 不再继承 Node2D，改为 MonoBehaviour，挂载在场景中的地图根节点上。
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        public enum GridLineWidthMode
        {
            FixedWorld = 0,
            FixedScreen = 1,
            AdaptiveCalibration = 2,
        }

        public enum GridSizeMode
        {
            Manual = 0,
            ResponsiveVisibleCount = 1,
        }

        // 网格参数
        public int GridSize = 111;
        public Color LineColor = new Color(0.7f, 0.7f, 0.7f);
        public float LineWidth = 1.0f;
        public float DashLength = 8.0f;
        public float GapLength = 4.0f;

        public const float GridRenderMinZoom = 0.1f;
        public const float GridRenderMaxZoom = 10.0f; // 网格稳定渲染的 zoom 安全区上限（5.0 为 Godot 保守值，实测 10 内自适应线宽仍成立）

        // 线宽自适应
        public bool AutoLineWidth = true;
        public float LineWidthScale = 1.0f;
        public float MinScreenLineWidth = 1.0f;
        public float MaxScreenLineWidth = 2.0f;
        public float GridAntiAliasSoftness = 1.0f;

        public bool AdaptiveCalibrationEnabled;
        public float RefZoomA = 0.4f;
        public float RefWidthA = 3.0f;
        public float RefZoomB = 1.0f;
        public float RefWidthB = 1.5f;

        private float _previewLineWidth = -1.0f;

        // 数据
        public Dictionary<Vector2Int, GridCell> GridData = new();
        public string CurrentMapName = "落叶乡";
        public RectInt MapBounds = new RectInt(0, 0, 50, 50);

        /// <summary>map.json 的 display_name/spawn（LoadMap 时保留原值，SaveCurrentMap 原样回写；勿丢，否则保存会把它们重置成默认值）。</summary>
        private string _displayName = "";
        private Vector2Int _spawn = new Vector2Int(25, 25);
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? CurrentMapName : _displayName;
        public Vector2Int Spawn => _spawn;
        /// <summary>地图编辑器重命名地图后同步 display_name。</summary>
        public void SetDisplayName(string name) => _displayName = name;

        // 编辑模式 / 标注
        public bool IsEditMode;
        public bool ShowGridCoords;
        public bool ShowTerrainLabels;
        public bool ShowCellUids;

        // 地图外灰色
        public bool ShowOutsideMapGray;
        public Color OutsideMapColor = new Color(0.15f, 0.15f, 0.15f, 1.0f);

        // 遮罩纹理
        private Texture2D _terrainMask;
        private Texture2D _waterMask;

        public Texture2D TerrainMask => _terrainMask;
        public Texture2D WaterMask => _waterMask;
        public Vector2 TerrainMaskSize => _terrainMask != null ? new Vector2(_terrainMask.width, _terrainMask.height) : Vector2.one;
        public Vector2 WaterMaskSize => _waterMask != null ? new Vector2(_waterMask.width, _waterMask.height) : Vector2.one;

        private void Awake()
        {
            TerrainConfigUtil.Load();
            UnityClientSharp.Entity.Walkability.Grid = this;
        }

        private void OnDestroy()
        {
            if (UnityClientSharp.Entity.Walkability.Grid == this)
                UnityClientSharp.Entity.Walkability.Grid = null;
        }

        public void LoadMap(string mapName)
        {
            var data = MapDataManager.LoadMapFromJson(mapName, out var bounds, out var spawn, out var displayName);
            // 信任 map.json 的 bounds：稀疏地图不按已有格子缩水；文件缺失/加载失败时回退默认 (0,0,50,50)
            MapBounds = bounds;
            _spawn = spawn;
            _displayName = displayName;
            GridData = data.Count > 0
                ? data
                : MapDataManager.CreateDefaultGridData(bounds.width, bounds.height, bounds.x, bounds.y);
            CurrentMapName = mapName;
            UpdateTerrainMask();
        }

        /// <summary>保存当前地图到 map.json（保留 LoadMap 时的 display_name/spawn；地图编辑器用，保存为显式动作）。</summary>
        public bool SaveCurrentMap()
        {
            return MapDataManager.SaveMapToJson(CurrentMapName, GridData, DisplayName, MapBounds, _spawn);
        }

        /// <summary>
        /// 开辟地图：把界外格加入 GridData（默认普通地形）并重算 bounds（移植 Godot GridManager.ExtendMap）。
        /// 不自动落盘（保存是显式动作）。返回实际新增格数。
        /// </summary>
        public int ExtendMap(IEnumerable<Vector2Int> cellsToAdd)
        {
            int added = 0;
            foreach (var pos in cellsToAdd)
            {
                if (GridData.ContainsKey(pos)) continue;
                var cell = new GridCell(pos.x, pos.y) { TerrainType = 0 };
                cell.RefreshTerrainConfig();
                GridData[pos] = cell;
                added++;
            }
            if (added > 0)
            {
                RecalculateMapBounds();
                UpdateTerrainMask();
            }
            return added;
        }

        /// <summary>根据当前 GridData 所有存在格子的坐标重算 MapBounds。</summary>
        public void RecalculateMapBounds()
        {
            if (GridData.Count == 0)
            {
                MapBounds = new RectInt(0, 0, 50, 50);
                return;
            }

            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            foreach (var pos in GridData.Keys)
            {
                if (pos.x < minX) minX = pos.x;
                if (pos.x > maxX) maxX = pos.x;
                if (pos.y < minY) minY = pos.y;
                if (pos.y > maxY) maxY = pos.y;
            }
            MapBounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        public void NotifyTerrainChanged() => UpdateTerrainMask();

        // ============ 坐标换算 ============

        public Vector2 GridToWorld(Vector2Int gridPos) => GridMath.GridToWorld(gridPos, GridSize);
        public Vector2Int WorldToGrid(Vector2 worldPos) => GridMath.WorldToGrid(worldPos, GridSize);
        public bool IsInBounds(Vector2Int gridPos) => GridData.ContainsKey(gridPos);
        public GridCell GetCell(Vector2Int gridPos) => GridData.TryGetValue(gridPos, out var cell) ? cell : null;

        /// <summary>
        /// 行走判定（移植自 Godot GridManager.IsWalkable）：
        /// 格子存在 → 未被阻挡建筑 footprint 覆盖 → 地形 Walkable。
        /// TODO(在线实体阶段): 宝箱/怪物/NPC 占用判定接入。
        /// </summary>
        public bool IsWalkable(Vector2Int gridPos, UnityClientSharp.Entity.MapDecorationManager decorations = null)
        {
            if (!GridData.TryGetValue(gridPos, out var cell))
                return false;
            if (decorations != null && decorations.IsBlockedByDecoration(gridPos))
                return false;
            return (cell.TerrainConfig ?? TerrainConfigUtil.Get(cell.TerrainType))?.Walkable ?? true;
        }

        // ============ 线宽自适应 ============

        public (float lineWidthWorld, Color lineColor) ComputeGridLineRenderStyle(float cameraZoom)
        {
            float targetScreenLineWidth;
            if (IsEditMode)
            {
                targetScreenLineWidth = 2.0f;
            }
            else
            {
                targetScreenLineWidth = GetTargetScreenLineWidth(cameraZoom);
                float maxWidth = AdaptiveCalibrationEnabled
                    ? Mathf.Max(MaxScreenLineWidth, Mathf.Max(RefWidthA, RefWidthB))
                    : MaxScreenLineWidth;
                targetScreenLineWidth = Mathf.Clamp(targetScreenLineWidth, MinScreenLineWidth, maxWidth);
            }

            float alphaScale = 1.0f;
            float drawScreenLineWidth = targetScreenLineWidth;

            // 小于 1px 时改用 1px 几何线 + alpha 模拟细线
            if (targetScreenLineWidth < 1.0f)
            {
                drawScreenLineWidth = 1.0f;
                alphaScale = Mathf.Clamp(targetScreenLineWidth, 0.35f, 1.0f);
            }

            float lineWidthWorld = drawScreenLineWidth / cameraZoom;
            lineWidthWorld = Mathf.Max(lineWidthWorld, 0.01f);

            var lineColor = IsEditMode ? new Color(1f, 1f, 1f, 1f) : LineColor;
            lineColor.a *= alphaScale;
            return (lineWidthWorld, lineColor);
        }

        private float GetTargetScreenLineWidth(float cameraZoom)
        {
            if (_previewLineWidth > 0.0f)
                return _previewLineWidth;
            if (AdaptiveCalibrationEnabled)
                return GetAdaptiveLineWidth(cameraZoom);
            if (AutoLineWidth)
                return Mathf.Max(LineWidthScale, MinScreenLineWidth);
            return Mathf.Max(LineWidth, MinScreenLineWidth);
        }

        private float GetAdaptiveLineWidth(float zoom)
        {
            const float EPSILON = 0.001f;
            zoom = Mathf.Max(zoom, 0.01f);

            if (Mathf.Abs(RefZoomB - RefZoomA) < EPSILON)
            {
                return (RefWidthA + RefWidthB) / 2.0f;
            }

            float lowZoom, lowWidth, highZoom, highWidth;
            if (RefZoomA < RefZoomB)
            {
                lowZoom = RefZoomA; lowWidth = RefWidthA;
                highZoom = RefZoomB; highWidth = RefWidthB;
            }
            else
            {
                lowZoom = RefZoomB; lowWidth = RefWidthB;
                highZoom = RefZoomA; highWidth = RefWidthA;
            }

            float logLow = Mathf.Log(Mathf.Max(lowZoom, 0.01f));
            float logHigh = Mathf.Log(Mathf.Max(highZoom, 0.01f));
            float logZoom = Mathf.Log(zoom);
            float t = Mathf.InverseLerp(logLow, logHigh, logZoom);
            t = t * t * (3.0f - 2.0f * t);
            return Mathf.Lerp(lowWidth, highWidth, t);
        }

        // ============ 遮罩纹理生成 ============

        /// <summary>
        /// 根据当前 GridData 生成 RGBA8 地形遮罩纹理（供 Shader 采样）。
        /// R 通道：0=地形墙/地图外（不绘制网格线，显示灰色填充），255=普通格子。
        /// GBA 通道：地形配置颜色（RGB）；地形墙/地图外使用 OutsideMapColor。
        /// 同时生成 water_mask：R=1 表示水域，R=0 表示非水域。
        /// 仅在地形变化或加载地图时调用（与格子数无关，开销低）。
        /// </summary>
        public void UpdateTerrainMask()
        {
            if (GridData.Count == 0) return;

            var bounds = MapBounds;
            int w = Mathf.Max(1, bounds.width);
            int h = Mathf.Max(1, bounds.height);

            EnsureMaskTextures(w, h);

            var terrainPixels = _terrainMask.GetPixels();
            var waterPixels = _waterMask.GetPixels();

            for (int ly = 0; ly < h; ly++)
            {
                int cellY = bounds.y + ly;
                for (int lx = 0; lx < w; lx++)
                {
                    int cellX = bounds.x + lx;
                    int idx = ly * w + lx;

                    byte maskValue;
                    float r, g, b;
                    bool isWater = false;

                    if (GridData.TryGetValue(new Vector2Int(cellX, cellY), out var cell))
                    {
                        if (cell.TerrainType == 9)
                        {
                            maskValue = 0;
                            r = OutsideMapColor.r; g = OutsideMapColor.g; b = OutsideMapColor.b;
                        }
                        else if (cell.TerrainType == 10)
                        {
                            maskValue = 255;
                            r = 1.0f; g = 0.0f; b = 0.0f;
                        }
                        else
                        {
                            maskValue = 255;
                            var cfg = cell.TerrainConfig ?? TerrainConfigUtil.Get(cell.TerrainType);
                            if (cfg != null && cfg.ColorA > 0 && (cfg.ColorR > 0 || cfg.ColorG > 0 || cfg.ColorB > 0))
                            {
                                r = cfg.ColorR / 255f; g = cfg.ColorG / 255f; b = cfg.ColorB / 255f;
                            }
                            else
                            {
                                r = 0f; g = 0f; b = 0f;
                            }
                            isWater = cell.TerrainType == 1;
                        }
                    }
                    else
                    {
                        maskValue = 0;
                        r = OutsideMapColor.r; g = OutsideMapColor.g; b = OutsideMapColor.b;
                    }

                    terrainPixels[idx] = new Color(maskValue / 255f, r, g, b);
                    waterPixels[idx] = isWater ? new Color(1f, 0f, 0f, 0f) : new Color(0f, 0f, 0f, 0f);
                }
            }

            _terrainMask.SetPixels(terrainPixels);
            _terrainMask.Apply();
            _waterMask.SetPixels(waterPixels);
            _waterMask.Apply();

            Debug.Log($"[GridManager] UpdateTerrainMask: 已更新 terrain/water mask {w}x{h}, bounds={bounds}");
        }

        /// <summary>Edit 模式下 Destroy 非法（编辑器地图编辑会话会重建遮罩纹理），走 DestroyImmediate。</summary>
        private static void DestroyEditSafe(Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        private void EnsureMaskTextures(int w, int h)
        {
            if (_terrainMask == null || _terrainMask.width != w || _terrainMask.height != h)
            {
                if (_terrainMask != null) DestroyEditSafe(_terrainMask);
                _terrainMask = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "TerrainMask"
                };
            }
            if (_waterMask == null || _waterMask.width != w || _waterMask.height != h)
            {
                if (_waterMask != null) DestroyEditSafe(_waterMask);
                _waterMask = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "WaterMask"
                };
            }
        }

        // ============ 设置方法（供编辑器/调试面板后续使用）============

        public void SetGridSize(int newSize)
        {
            GridSize = Mathf.Clamp(newSize, 32, 256);
        }

        public void SetEditMode(bool enabled) => IsEditMode = enabled;

        public void SetShowGridCoords(bool show) => ShowGridCoords = show;
        public void SetShowCellUids(bool show) => ShowCellUids = show;
        public void SetShowTerrainLabels(bool show) => ShowTerrainLabels = show;

        public void SetLineWidthScale(float v) => LineWidthScale = Mathf.Clamp(v, 0.1f, 20.0f);
        public void SetLineBrightness(float brightness) => LineColor = new Color(brightness, brightness, brightness, 1.0f);
        public void SetAutoLineWidth(bool enabled) => AutoLineWidth = enabled;
        public void SetAdaptiveCalibrationEnabled(bool enabled) => AdaptiveCalibrationEnabled = enabled;
        public void SetPreviewLineWidth(float w) => _previewLineWidth = w;
        public void ClearPreviewLineWidth() => _previewLineWidth = -1.0f;

        public GridLineWidthMode GetGridLineWidthMode()
        {
            if (AdaptiveCalibrationEnabled) return GridLineWidthMode.AdaptiveCalibration;
            if (AutoLineWidth) return GridLineWidthMode.FixedScreen;
            return GridLineWidthMode.FixedWorld;
        }

        public void SetGridLineWidthMode(GridLineWidthMode mode)
        {
            switch (mode)
            {
                case GridLineWidthMode.FixedWorld:
                    SetAdaptiveCalibrationEnabled(false);
                    SetAutoLineWidth(false);
                    break;
                case GridLineWidthMode.FixedScreen:
                    SetAdaptiveCalibrationEnabled(false);
                    SetAutoLineWidth(true);
                    break;
                case GridLineWidthMode.AdaptiveCalibration:
                    SetAutoLineWidth(false);
                    SetAdaptiveCalibrationEnabled(true);
                    break;
            }
        }
    }
}
