using System.Collections.Generic;
using UnityClientSharp.Entity;
using UnityClientSharp.Map.Core;
using UnityEngine;
using UnityEngine.UI;

namespace UnityClientSharp.Map.Rendering
{
    /// <summary>
    /// 小地图视图：地形缩略图 + 实体标记 + 相机框。
    /// 移植自 clinetcsharp/Scripts/MapViewControl.cs：
    /// 缩略图等比适配居中（drawRect），玩家绿点 r5 / 怪物红点 r3 / NPC 蓝点 r3（30 格裁剪），
    /// 相机框黄色空心（公式 halfWorld = viewportSize/zoom × 0.5）。
    /// </summary>
    public class MapViewControl : MonoBehaviour
    {
        public RawImage TargetImage;
        public bool ShowCameraFrame;
        public Color CameraFrameColor = new Color(1f, 0.9f, 0.2f, 0.8f);
        public int MaxTextureLongSide = 256;

        public Color PlayerColor = new Color(0.2f, 1f, 0.2f);
        public Color MonsterColor = new Color(1f, 0.25f, 0.25f);
        public Color NpcColor = new Color(0.25f, 0.5f, 1f);
        public float MaxMarkerDistance = 30f; // 格

        private GridManager _gm;
        private RectTransform _rect;
        private RectTransform _markerLayer;
        private Texture2D _terrainTex;
        private int _lastGridCount = -1;
        private string _lastMapName;

        private readonly List<Image> _markerPool = new();
        private readonly List<Image> _frameBars = new();
        private static Sprite s_dotSprite;

        // 缩略图绘制区域（相对本控件中心，uGUI 局部坐标）
        private Vector2 _drawSize;
        private float _gridToPixel; // 1 格 = N 像素

        public void Setup(GridManager gm)
        {
            _gm = gm;
            _rect = (RectTransform)transform;
            _markerLayer = new GameObject("Markers", typeof(RectTransform)).GetComponent<RectTransform>();
            _markerLayer.SetParent(_rect, false);
            _markerLayer.anchorMin = Vector2.zero;
            _markerLayer.anchorMax = Vector2.one;
            _markerLayer.offsetMin = Vector2.zero;
            _markerLayer.offsetMax = Vector2.zero;
            for (int i = 0; i < 4; i++)
            {
                var bar = CreateImage(_markerLayer, "FrameBar" + i, CameraFrameColor, null);
                bar.gameObject.SetActive(false);
                _frameBars.Add(bar);
            }
        }

        private void Update()
        {
            if (_gm == null || TargetImage == null || _rect == null) return;

            int count = _gm.GridData?.Count ?? 0;
            if (_gm.CurrentMapName != _lastMapName || count != _lastGridCount)
            {
                _lastMapName = _gm.CurrentMapName;
                _lastGridCount = count;
                RebuildTerrainTexture();
                RecalculateDrawRect();
            }

            RefreshMarkers();
            if (ShowCameraFrame) RefreshCameraFrame();
        }

        private void RebuildTerrainTexture()
        {
            if (_terrainTex != null) Destroy(_terrainTex);
            _terrainTex = MapThumbnailBuilder.Build(_gm, MaxTextureLongSide);
            TargetImage.texture = _terrainTex;
        }

        /// <summary>缩略图等比适配居中（对齐 Godot RecalculateDrawRect）。</summary>
        private void RecalculateDrawRect()
        {
            var bounds = _gm.MapBounds;
            float availW = _rect.rect.width, availH = _rect.rect.height;
            _gridToPixel = Mathf.Min(availW / Mathf.Max(1, bounds.width), availH / Mathf.Max(1, bounds.height));
            _drawSize = new Vector2(bounds.width * _gridToPixel, bounds.height * _gridToPixel);

            // 缩略图 RawImage 也限制在 drawRect 内（保持与标记同一坐标系）
            var imgRect = TargetImage.rectTransform;
            imgRect.anchorMin = new Vector2(0.5f, 0.5f);
            imgRect.anchorMax = new Vector2(0.5f, 0.5f);
            imgRect.pivot = new Vector2(0.5f, 0.5f);
            imgRect.sizeDelta = _drawSize;
            imgRect.anchoredPosition = Vector2.zero;
        }

        /// <summary>格子 → 控件局部坐标（drawRect 居中；格子左上角对齐，对齐 Godot GridToMapPos）。</summary>
        private Vector2 GridToMapPos(Vector2 gridPos)
        {
            var bounds = _gm.MapBounds;
            return new Vector2(
                (gridPos.x - bounds.x) * _gridToPixel - _drawSize.x / 2f,
                // 逻辑 y 向下 = 地图下方；uGUI y 向上，取负
                _drawSize.y / 2f - (gridPos.y - bounds.y) * _gridToPixel);
        }

        private void RefreshMarkers()
        {
            int idx = 0;
            var player = PlayerEntity.Instance;

            // 玩家标记（不裁剪）
            if (player != null)
                PlaceMarker(idx++, player.GridPos, PlayerColor, 5f);

            // 怪物/NPC（30 格距离裁剪）
            Vector2 playerGrid = player != null ? player.GridPos : Vector2.zero;
            if (MonsterManager.Instance != null)
            {
                foreach (var m in MonsterManager.Instance.GetMonsters())
                {
                    if (player != null && ((Vector2)m.GridPos - playerGrid).sqrMagnitude > MaxMarkerDistance * MaxMarkerDistance)
                        continue;
                    PlaceMarker(idx++, m.GridPos, MonsterColor, 3f);
                }
            }
            if (NpcManager.Instance != null)
            {
                foreach (var n in NpcManager.Instance.GetNpcs())
                {
                    if (player != null && ((Vector2)n.GridPos - playerGrid).sqrMagnitude > MaxMarkerDistance * MaxMarkerDistance)
                        continue;
                    PlaceMarker(idx++, n.GridPos, NpcColor, 3f);
                }
            }

            for (int i = idx; i < _markerPool.Count; i++)
                _markerPool[i].gameObject.SetActive(false);
        }

        private void PlaceMarker(int idx, Vector2Int gridPos, Color color, float radius)
        {
            while (idx >= _markerPool.Count)
                _markerPool.Add(CreateImage(_markerLayer, "Marker" + _markerPool.Count, Color.white, DotSprite));

            var img = _markerPool[idx];
            img.gameObject.SetActive(true);
            img.color = color;
            var rt = img.rectTransform;
            // 标记定位取格子中心
            rt.anchoredPosition = GridToMapPos(new Vector2(gridPos.x + 0.5f, gridPos.y + 0.5f));
            rt.sizeDelta = new Vector2(radius * 2, radius * 2);
        }

        private void RefreshCameraFrame()
        {
            var camCtrl = MapCameraController.Instance;
            var cam = camCtrl != null ? camCtrl.Cam : null;
            if (cam == null || _gm == null) return;

            // halfWorld = viewportSize/zoom × 0.5（Unity 正交：高=2×orthoSize，宽=高×aspect）
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            var camPos = cam.transform.position;

            // 四角世界 → 逻辑（y 取负）→ 格子（浮点）
            Vector2 bl = WorldToGridF(new Vector3(camPos.x - halfW, camPos.y - halfH, 0));
            Vector2 tr = WorldToGridF(new Vector3(camPos.x + halfW, camPos.y + halfH, 0));
            Vector2 p1 = GridToMapPos(bl);
            Vector2 p2 = GridToMapPos(tr);

            SetFrameBars(p1, p2);
        }

        private Vector2 WorldToGridF(Vector3 world)
        {
            return new Vector2(world.x / _gm.GridSize, -world.y / _gm.GridSize);
        }

        private void SetFrameBars(Vector2 p1, Vector2 p2)
        {
            if (p1.x > p2.x) (p1.x, p2.x) = (p2.x, p1.x);
            if (p1.y > p2.y) (p1.y, p2.y) = (p2.y, p1.y);
            const float w = 2f;
            SetBar(_frameBars[0], new Vector2((p1.x + p2.x) / 2, p2.y), new Vector2(p2.x - p1.x + w, w)); // 上
            SetBar(_frameBars[1], new Vector2((p1.x + p2.x) / 2, p1.y), new Vector2(p2.x - p1.x + w, w)); // 下
            SetBar(_frameBars[2], new Vector2(p1.x, (p1.y + p2.y) / 2), new Vector2(w, p2.y - p1.y + w)); // 左
            SetBar(_frameBars[3], new Vector2(p2.x, (p1.y + p2.y) / 2), new Vector2(w, p2.y - p1.y + w)); // 右
        }

        private static void SetBar(Image bar, Vector2 center, Vector2 size)
        {
            bar.gameObject.SetActive(true);
            bar.rectTransform.anchoredPosition = center;
            bar.rectTransform.sizeDelta = size;
        }

        private Image CreateImage(RectTransform parent, string name, Color color, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            if (sprite != null) img.sprite = sprite;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            return img;
        }

        /// <summary>共享圆点贴图（标记用）。</summary>
        private static Sprite DotSprite
        {
            get
            {
                if (s_dotSprite == null)
                {
                    const int size = 32;
                    var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                            tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(size / 2f - d)));
                        }
                    tex.Apply();
                    s_dotSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                }
                return s_dotSprite;
            }
        }
    }
}
