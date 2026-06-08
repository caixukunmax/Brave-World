using Godot;

namespace ClinetCSharp
{
    [GlobalClass]
    public partial class GridShaderOverlay : Node2D
    {
        private const string ShaderPath = "res://assets/shaders/grid_overlay.gdshader";

        private ShaderMaterial? _shaderMaterial;
        private float _mapWidthWorld;
        private float _mapHeightWorld;

        public override void _Ready()
        {
            ShowBehindParent = false;
            ZAsRelative = true;
            ZIndex = 0;

            var shader = GD.Load<Shader>(ShaderPath);
            if (shader == null)
            {
                GD.PushError($"[GridShaderOverlay] Failed to load shader: {ShaderPath}");
                return;
            }

            _shaderMaterial = new ShaderMaterial();
            _shaderMaterial.Shader = shader;
            Material = _shaderMaterial;
        }

        public void UpdateOverlay(
            int gridSize,
            int mapWidth,
            int mapHeight,
            float screenLineWidth,
            Color lineColor,
            float antiAliasSoftness)
        {
            if (_shaderMaterial == null)
                return;

            _mapWidthWorld = mapWidth * gridSize;
            _mapHeightWorld = mapHeight * gridSize;

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
                // 没有遮罩时，使用默认值（全白，所有格子都绘制网格线）
                _shaderMaterial.SetShaderParameter("terrain_mask_size", new Vector2(1.0f, 1.0f));
            }

            QueueRedraw();
        }

        public override void _Draw()
        {
            if (_shaderMaterial == null || _mapWidthWorld <= 0.0f || _mapHeightWorld <= 0.0f)
                return;

            DrawRect(new Rect2(0.0f, 0.0f, _mapWidthWorld, _mapHeightWorld), Colors.White, true);
        }

        /// <summary>
        /// 更新地图外部/地形墙的填充颜色（传给 shader 的 outside_map_color uniform）
        /// </summary>
        public void UpdateOutsideMapColor(Color color)
        {
            if (_shaderMaterial == null) return;
            _shaderMaterial.SetShaderParameter("outside_map_color", new Vector4(color.R, color.G, color.B, color.A));
        }
    }
}
