using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// 可复用地图视图控件 — 为小窗和大地图提供统一的地图绘制。
    /// </summary>
    [GlobalClass]
    public partial class MapViewControl : Control
    {
        #region Export Options
        [Export] public bool ShowPlayer { get; set; } = true;
        [Export] public bool ShowMonsters { get; set; } = true;
        [Export] public bool ShowNpcs { get; set; } = true;
        [Export] public bool ShowCameraFrame { get; set; } = false;
        [Export] public int MaxMarkerDistance { get; set; } = 30;
        [Export] public int MaxTextureLongSide { get; set; } = 256;
        [Export] public Color PlayerColor { get; set; } = new Color(0.2f, 1.0f, 0.2f);
        [Export] public Color MonsterColor { get; set; } = new Color(1.0f, 0.25f, 0.25f);
        [Export] public Color NpcColor { get; set; } = new Color(0.25f, 0.5f, 1.0f);
        [Export] public Color CameraFrameColor { get; set; } = new Color(1.0f, 0.9f, 0.2f, 0.8f);
        [Export] public Color UnknownTerrainColor { get; set; } = new Color(0.1f, 0.1f, 0.1f, 1.0f);
        #endregion

        private GridManager _gridManager;
        private Player _player;
        private MonsterManager _monsterManager;
        private NpcManager _npcManager;
        private CameraController _camera;

        private string _lastMapName;
        private int _lastGridCount = -1;
        private ImageTexture _terrainTexture;
        private Rect2 _mapDrawRect;
        private Vector2 _mapScale;
        private Vector2I _mapOrigin;

        public override void _Ready()
        {
            Resized += OnSizeChanged;
        }

        public override void _Process(double delta)
        {
            var gridMgr = GetGridManager();
            if (gridMgr == null) return;

            // 地图切换或格子数据变化时重建地形缩略图
            int gridCount = gridMgr.GridData?.Count ?? 0;
            if (gridMgr.CurrentMapName != _lastMapName || gridCount != _lastGridCount)
            {
                _lastMapName = gridMgr.CurrentMapName;
                _lastGridCount = gridCount;
                RebuildTerrainTexture();
            }

            // 懒刷新其他管理器引用
            _ = GetPlayer();
            _ = GetCamera();

            QueueRedraw();
        }

        public override void _Draw()
        {
            if (_terrainTexture == null) return;

            DrawTextureRect(_terrainTexture, _mapDrawRect, false);

            var player = GetPlayer();
            if (player != null)
            {
                if (ShowCameraFrame && _camera != null)
                    DrawCameraFrame();

                var playerGrid = player.GridPos;

                if (ShowPlayer)
                    DrawEntityMarker(playerGrid, PlayerColor, 5.0f);

                if (ShowMonsters)
                    DrawMonsterMarkers(playerGrid);

                if (ShowNpcs)
                    DrawNpcMarkers(playerGrid);
            }
        }

        private void OnSizeChanged()
        {
            RebuildTerrainTexture();
        }

        private void RebuildTerrainTexture()
        {
            var gridMgr = GetGridManager();
            if (gridMgr == null) return;

            var bounds = gridMgr.MapBounds;
            var gridData = gridMgr.GridData;

            _mapOrigin = bounds.Position;

            int mapW = Mathf.Max(1, bounds.Size.X);
            int mapH = Mathf.Max(1, bounds.Size.Y);

            // 限制缩略图分辨率
            int texW = mapW;
            int texH = mapH;
            int longSide = Mathf.Max(texW, texH);
            if (longSide > MaxTextureLongSide)
            {
                float scale = (float)MaxTextureLongSide / longSide;
                texW = Mathf.Max(1, Mathf.RoundToInt(texW * scale));
                texH = Mathf.Max(1, Mathf.RoundToInt(texH * scale));
            }

            var image = Image.CreateEmpty(texW, texH, false, Image.Format.Rgba8);
            image.Fill(UnknownTerrainColor);

            float uScale = (float)mapW / texW;
            float vScale = (float)mapH / texH;

            for (int y = 0; y < texH; y++)
            {
                for (int x = 0; x < texW; x++)
                {
                    int gx = Mathf.FloorToInt(_mapOrigin.X + x * uScale);
                    int gy = Mathf.FloorToInt(_mapOrigin.Y + y * vScale);
                    var gridPos = new Vector2I(gx, gy);

                    if (gridData.TryGetValue(gridPos, out var cell))
                    {
                        var color = cell.GetTerrainColor();
                        if (color.A <= 0.001f)
                            color = UnknownTerrainColor;
                        image.SetPixel(x, y, color);
                    }
                }
            }

            _terrainTexture = ImageTexture.CreateFromImage(image);
            RecalculateDrawRect();
            QueueRedraw();
        }

        private void RecalculateDrawRect()
        {
            var gridMgr = GetGridManager();
            if (gridMgr == null) return;

            var bounds = gridMgr.MapBounds;
            float mapW = bounds.Size.X;
            float mapH = bounds.Size.Y;

            Vector2 available = Size;
            float scale = Mathf.Min(available.X / mapW, available.Y / mapH);
            _mapScale = new Vector2(scale, scale);

            float drawW = mapW * scale;
            float drawH = mapH * scale;
            float offsetX = (available.X - drawW) * 0.5f;
            float offsetY = (available.Y - drawH) * 0.5f;

            _mapDrawRect = new Rect2(new Vector2(offsetX, offsetY), new Vector2(drawW, drawH));
        }

        private Vector2 GridToMapPos(Vector2I gridPos)
        {
            float localX = gridPos.X - _mapOrigin.X;
            float localY = gridPos.Y - _mapOrigin.Y;
            return new Vector2(
                _mapDrawRect.Position.X + localX * _mapScale.X,
                _mapDrawRect.Position.Y + localY * _mapScale.Y);
        }

        private void DrawEntityMarker(Vector2I gridPos, Color color, float radius)
        {
            Vector2 pos = GridToMapPos(gridPos);
            DrawCircle(pos, radius, color);
        }

        private void DrawMonsterMarkers(Vector2I playerGrid)
        {
            var mgr = GetMonsterManager();
            if (mgr == null) return;

            foreach (var monster in mgr.GetMonsters())
            {
                if (monster == null || !IsInstanceValid(monster)) continue;
                if (monster.GridPos.DistanceSquaredTo(playerGrid) > MaxMarkerDistance * MaxMarkerDistance)
                    continue;
                DrawEntityMarker(monster.GridPos, MonsterColor, 3.0f);
            }
        }

        private void DrawNpcMarkers(Vector2I playerGrid)
        {
            var mgr = GetNpcManager();
            if (mgr == null) return;

            foreach (var npc in mgr.GetNpcs())
            {
                if (npc == null || !IsInstanceValid(npc)) continue;
                if (npc.GridPos.DistanceSquaredTo(playerGrid) > MaxMarkerDistance * MaxMarkerDistance)
                    continue;
                DrawEntityMarker(npc.GridPos, NpcColor, 3.0f);
            }
        }

        private void DrawCameraFrame()
        {
            if (_camera == null) return;

            var viewport = GetViewport();
            if (viewport == null) return;

            Vector2 viewportSize = viewport.GetVisibleRect().Size;
            float zoom = _camera.Zoom.X;
            Vector2 halfWorldSize = (viewportSize / zoom) * 0.5f;
            Vector2 cameraWorldPos = _camera.Position;

            var gridMgr = GetGridManager();
            Vector2I topLeftGrid = gridMgr?.WorldToGrid(cameraWorldPos - halfWorldSize) ?? Vector2I.Zero;
            Vector2I bottomRightGrid = gridMgr?.WorldToGrid(cameraWorldPos + halfWorldSize) ?? Vector2I.Zero;

            Vector2 tl = GridToMapPos(topLeftGrid);
            Vector2 br = GridToMapPos(bottomRightGrid);
            DrawRect(new Rect2(tl, br - tl), CameraFrameColor, false, 2.0f);
        }

        #region Lazy Node Resolution
        private GridManager GetGridManager()
        {
            if (_gridManager != null && IsInstanceValid(_gridManager)) return _gridManager;
            _gridManager = GetTree()?.GetFirstNodeInGroup("grid_manager") as GridManager;
            return _gridManager;
        }

        private Player GetPlayer()
        {
            if (_player != null && IsInstanceValid(_player)) return _player;
            _player = GetTree()?.GetFirstNodeInGroup("player") as Player;
            return _player;
        }

        private MonsterManager GetMonsterManager()
        {
            if (_monsterManager != null && IsInstanceValid(_monsterManager)) return _monsterManager;
            _monsterManager = GetTree()?.GetFirstNodeInGroup("monster_manager") as MonsterManager;
            return _monsterManager;
        }

        private NpcManager GetNpcManager()
        {
            if (_npcManager != null && IsInstanceValid(_npcManager)) return _npcManager;
            _npcManager = NpcManager.Instance;
            if (_npcManager == null)
                _npcManager = GetTree()?.GetFirstNodeInGroup("npc_manager") as NpcManager;
            return _npcManager;
        }

        private CameraController GetCamera()
        {
            if (_camera != null && IsInstanceValid(_camera)) return _camera;
            _camera = GetTree()?.GetFirstNodeInGroup("camera") as CameraController;
            return _camera;
        }
        #endregion
    }
}
