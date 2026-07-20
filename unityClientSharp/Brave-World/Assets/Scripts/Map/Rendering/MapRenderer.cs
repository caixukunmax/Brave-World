using UnityClientSharp.Map.Core;
using UnityEngine;

namespace UnityClientSharp.Map.Rendering
{
    /// <summary>
    /// 地图渲染器：创建一个覆盖整张地图世界尺寸的 Quad，挂载 GridOverlay 材质，
    /// 每帧把 GridManager 的网格参数与遮罩纹理推给材质。
    /// 缩放/平移由 MapCameraController 控制；本组件只负责"画"。
    /// Y 翻转集中在 Quad 的 scale.y = -1（着色器内不做二次翻转），坐标换算保持 Godot 语义。
    /// </summary>
    [RequireComponent(typeof(GridManager))]
    public class MapRenderer : MonoBehaviour
    {
        private GridManager _gm;
        private Transform _quadTransform;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Material _material;

        private Texture2D _lastTerrainMask;
        private Texture2D _lastWaterMask;
        private int _lastX, _lastY, _lastW, _lastH;
        private int _lastGridSize;

        private void Awake()
        {
            EnsureInit();
        }

        /// <summary>
        /// 初始化渲染 Quad 与材质（幂等）。编辑器地图编辑会话在 Edit 模式手动调用——
        /// Awake 在 Edit 模式不执行（AGENTS.md 编译验证节）。
        /// </summary>
        public void EnsureInit()
        {
            if (_material != null) return;
            _gm = GetComponent<GridManager>();

            var quadGo = new GameObject("MapQuad");
            quadGo.transform.SetParent(transform, false);
            _meshFilter = quadGo.AddComponent<MeshFilter>();
            _meshRenderer = quadGo.AddComponent<MeshRenderer>();
            _quadTransform = quadGo.transform;
            _meshFilter.mesh = CreateQuadMesh();

            _material = new Material(Shader.Find("UnityClientSharp/GridOverlay"));
            _meshRenderer.material = _material;

            LayoutQuad();
        }

        private static Mesh CreateQuadMesh()
        {
            var mesh = new Mesh();
            var verts = new Vector3[] { new(-0.5f, -0.5f, 0), new(0.5f, -0.5f, 0), new(0.5f, 0.5f, 0), new(-0.5f, 0.5f, 0) };
            var uvs = new Vector2[] { new(0, 0), new(1, 0), new(1, 1), new(0, 1) };
            var tris = new int[] { 0, 2, 1, 0, 3, 2 };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            return mesh;
        }

        private void LayoutQuad()
        {
            float w = _gm.MapBounds.width * _gm.GridSize;
            float h = _gm.MapBounds.height * _gm.GridSize;
            float cx = (_gm.MapBounds.x + _gm.MapBounds.width / 2f) * _gm.GridSize;
            float cy = -((_gm.MapBounds.y + _gm.MapBounds.height / 2f) * _gm.GridSize);

            _quadTransform.localPosition = new Vector3(cx, cy, 0);
            _quadTransform.localScale = new Vector3(w, -h, 1); // scale.y = -1 翻转 Y
            _material.SetVector("_MapSizeWorld", new Vector4(w, h, 0, 0));
            _lastX = _gm.MapBounds.x;
            _lastY = _gm.MapBounds.y;
            _lastW = _gm.MapBounds.width;
            _lastH = _gm.MapBounds.height;
            _lastGridSize = _gm.GridSize;
        }

        private void EnsureLayout()
        {
            if (_gm.MapBounds.x != _lastX || _gm.MapBounds.y != _lastY ||
                _gm.MapBounds.width != _lastW || _gm.MapBounds.height != _lastH ||
                _gm.GridSize != _lastGridSize)
                LayoutQuad();
        }

        private void Update()
        {
            RefreshFrame(null);
        }

        /// <summary>
        /// 把 GridManager 的网格参数与遮罩纹理推给材质。
        /// zoomOverride 供编辑器地图编辑会话传入（Edit 模式无 MapCameraController）；为空时照旧取相机 zoom。
        /// </summary>
        public void RefreshFrame(float? zoomOverride = null)
        {
            if (_gm == null || _material == null) return;
            EnsureLayout();

            float zoom = zoomOverride ?? (MapCameraController.Instance != null ? MapCameraController.Instance.Zoom : 1f);
            zoom = Mathf.Clamp(zoom, GridManager.GridRenderMinZoom, GridManager.GridRenderMaxZoom);

            var (lineWidthWorld, lineColor) = _gm.ComputeGridLineRenderStyle(zoom);
            // shader 内部用 fwidth(grid_coord) 把线宽换算回屏幕像素，故须传屏幕像素单位，
            // 与 Godot 版 GridManager 的 screenLineWidth = lineWidthWorld * cameraZoom 一致。
            _material.SetFloat("_LineWidth", lineWidthWorld * zoom);
            _material.SetColor("_LineColor", lineColor);
            _material.SetFloat("_AaSoftness", _gm.GridAntiAliasSoftness);
            _material.SetColor("_OutsideMapColor", _gm.ShowOutsideMapGray ? _gm.OutsideMapColor : new Color(0, 0, 0, 0));
            _material.SetFloat("_GridSize", _gm.GridSize);

            if (_gm.TerrainMask != _lastTerrainMask && _gm.TerrainMask != null)
            {
                _lastTerrainMask = _gm.TerrainMask;
                _material.SetTexture("_TerrainMask", _gm.TerrainMask);
                _material.SetVector("_TerrainMaskSize", new Vector4(_gm.TerrainMaskSize.x, _gm.TerrainMaskSize.y, 0, 0));
            }
            if (_gm.WaterMask != _lastWaterMask && _gm.WaterMask != null)
            {
                _lastWaterMask = _gm.WaterMask;
                _material.SetTexture("_WaterMask", _gm.WaterMask);
                _material.SetVector("_WaterMaskSize", new Vector4(_gm.WaterMaskSize.x, _gm.WaterMaskSize.y, 0, 0));
            }
        }

        /// <summary>计算逻辑格子中心在 GridManager 本地坐标系中的世界位置（与 Quad 渲染对齐）。</summary>
        public Vector3 CellWorldPos(int x, int y)
        {
            // 单格即 1x1 footprint；Y 翻转统一走 GridMath（AGENTS.md 第 1 条）
            return GridMath.FootprintCenterWorld(x, y, 1, 1, _gm.GridSize);
        }
    }
}
