using Godot;

namespace ClinetCSharp
{
    // [Tool]：本类由 GridManager 动态创建；地图编辑器插件在编辑器下复用 GridManager 渲染地形/网格，
    // 需要 _EnterTree（创建 ShaderMaterial）与 _Draw（画 shader 矩形）在编辑器下执行。
    [Tool]
    [GlobalClass]
    public partial class GridShaderOverlay : Node2D
    {
        private const string ShaderPath = "res://assets/shaders/grid_overlay.gdshader";

        private ShaderMaterial? _shaderMaterial;
        private float _mapWidthWorld;
        private float _mapHeightWorld;
        private Vector2 _mapOffsetWorld;

        public override void _EnterTree()
        {
            // 在 _EnterTree 中初始化材质，确保 AddChild 后立即可用。
            // 若放到 _Ready，GridManager 在 AddChild 后立刻调用 UpdateOverlay 时 _shaderMaterial 可能还没创建。
            if (_shaderMaterial != null)
                return;

            var shader = GD.Load<Shader>(ShaderPath);
            if (shader == null)
            {
                GD.PushError($"[GridShaderOverlay] Failed to load shader: {ShaderPath}");
                return;
            }

            _shaderMaterial = new ShaderMaterial();
            _shaderMaterial.Shader = shader;
            Material = _shaderMaterial;

            // 设置默认的地形遮罩纹理：R=1(普通格子)，GBA=0(无地形颜色)。
            // 避免 hint_default_white 导致全屏白色/透明，并在真实纹理更新前提供可辨别的网格底。
            var defaultImage = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
            defaultImage.SetPixel(0, 0, new Color(1.0f, 0.0f, 0.0f, 0.0f));
            var defaultMask = ImageTexture.CreateFromImage(defaultImage);
            _shaderMaterial.SetShaderParameter("terrain_mask", defaultMask);
            _shaderMaterial.SetShaderParameter("terrain_mask_size", new Vector2(1.0f, 1.0f));

            // 默认水面遮罩：全黑（无水）
            var defaultWaterImage = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
            defaultWaterImage.SetPixel(0, 0, new Color(0.0f, 0.0f, 0.0f, 0.0f));
            var defaultWaterMask = ImageTexture.CreateFromImage(defaultWaterImage);
            _shaderMaterial.SetShaderParameter("water_mask", defaultWaterMask);
            _shaderMaterial.SetShaderParameter("water_mask_size", new Vector2(1.0f, 1.0f));
        }

        public void UpdateOverlay(
            int gridSize,
            int mapWidth,
            int mapHeight,
            float screenLineWidth,
            Color lineColor,
            float antiAliasSoftness)
        {
            UpdateOverlay(gridSize, new Rect2I(0, 0, mapWidth, mapHeight), screenLineWidth, lineColor, antiAliasSoftness);
        }

        public void UpdateOverlay(
            int gridSize,
            Rect2I mapBounds,
            float screenLineWidth,
            Color lineColor,
            float antiAliasSoftness)
        {
            // 尺寸和位置必须先保存，即使 _shaderMaterial 尚未初始化（_Ready 可能还没执行）。
            // 否则首次 _Draw 会因为 _mapWidthWorld <= 0 而直接返回，导致地图完全不渲染。
            _mapWidthWorld = mapBounds.Size.X * gridSize;
            _mapHeightWorld = mapBounds.Size.Y * gridSize;
            _mapOffsetWorld = new Vector2(mapBounds.Position.X * gridSize, mapBounds.Position.Y * gridSize);
            Position = _mapOffsetWorld;

            if (_shaderMaterial == null)
                return;

            _shaderMaterial.SetShaderParameter("grid_size", (float)gridSize);
            _shaderMaterial.SetShaderParameter("line_width", screenLineWidth);
            _shaderMaterial.SetShaderParameter("aa_softness", antiAliasSoftness);
            _shaderMaterial.SetShaderParameter("line_color", lineColor);
            _shaderMaterial.SetShaderParameter("map_size_world", new Vector2(_mapWidthWorld, _mapHeightWorld));

            QueueRedraw();
        }

        /// <summary>
        /// 更新地形墙遮罩纹理。遮罩中黑色(0)表示地形墙（不绘制网格线），白色(1)表示普通格子。
        /// </summary>
        public void UpdateTerrainMask(ImageTexture? maskTexture, int mapWidth, int mapHeight)
        {
            if (_shaderMaterial == null)
                return;

            if (maskTexture != null)
            {
                _shaderMaterial.SetShaderParameter("terrain_mask", maskTexture);
                _shaderMaterial.SetShaderParameter("terrain_mask_size", new Vector2(mapWidth, mapHeight));
            }
            else
            {
                // 没有遮罩时，重置为默认 1x1 纹理，避免旧纹理残留
                var defaultImage = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
                defaultImage.SetPixel(0, 0, new Color(1.0f, 0.0f, 0.0f, 0.0f));
                var defaultMask = ImageTexture.CreateFromImage(defaultImage);
                _shaderMaterial.SetShaderParameter("terrain_mask", defaultMask);
                _shaderMaterial.SetShaderParameter("terrain_mask_size", new Vector2(1.0f, 1.0f));
            }

            QueueRedraw();
        }

        public override void _Draw()
        {
            if (_shaderMaterial == null || _mapWidthWorld <= 0.0f || _mapHeightWorld <= 0.0f)
            {
#if DEBUG
                // 编辑器下一次性诊断：_Draw 被调用但提前返回，说明材质未建或尺寸为 0
                if (!_earlyReturnLogged && Engine.IsEditorHint())
                {
                    _earlyReturnLogged = true;
                    GD.Print($"[GridShaderOverlay] _Draw 提前返回: material={_shaderMaterial != null}, size={_mapWidthWorld}x{_mapHeightWorld}");
                }
#endif
                return;
            }

#if DEBUG
            // 编辑器下一次性诊断：确认 _Draw 真的被引擎调用（验证 [Tool] 是否生效）
            if (!_drawLogged && Engine.IsEditorHint())
            {
                _drawLogged = true;
                GD.Print($"[GridShaderOverlay] _Draw 正常执行: rect={_mapWidthWorld}x{_mapHeightWorld}");
            }
#endif
            DrawRect(new Rect2(0.0f, 0.0f, _mapWidthWorld, _mapHeightWorld), Colors.White, true);
        }

#if DEBUG
        private bool _drawLogged;
        private bool _earlyReturnLogged;
#endif

        /// <summary>
        /// 更新水面遮罩纹理。R=1 表示该格子是水域，R=0 表示非水域。
        /// </summary>
        public void UpdateWaterMask(ImageTexture? waterMaskTexture, int mapWidth, int mapHeight)
        {
            if (_shaderMaterial == null)
                return;

            if (waterMaskTexture != null)
            {
                _shaderMaterial.SetShaderParameter("water_mask", waterMaskTexture);
                _shaderMaterial.SetShaderParameter("water_mask_size", new Vector2(mapWidth, mapHeight));
            }
            else
            {
                var defaultImage = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
                defaultImage.SetPixel(0, 0, new Color(0.0f, 0.0f, 0.0f, 0.0f));
                var defaultMask = ImageTexture.CreateFromImage(defaultImage);
                _shaderMaterial.SetShaderParameter("water_mask", defaultMask);
                _shaderMaterial.SetShaderParameter("water_mask_size", new Vector2(1.0f, 1.0f));
            }

            QueueRedraw();
        }

        /// <summary>
        /// 更新地图外部/地形墙的填充颜色（传给 shader 的 outside_map_color uniform）
        /// </summary>
        public void UpdateOutsideMapColor(Color color)
        {
            if (_shaderMaterial == null) return;
            _shaderMaterial.SetShaderParameter("outside_map_color", color);
            QueueRedraw();
        }
    }
}
