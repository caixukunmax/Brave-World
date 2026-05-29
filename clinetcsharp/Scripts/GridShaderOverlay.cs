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

        public override void _Draw()
        {
            if (_shaderMaterial == null || _mapWidthWorld <= 0.0f || _mapHeightWorld <= 0.0f)
                return;

            DrawRect(new Rect2(0.0f, 0.0f, _mapWidthWorld, _mapHeightWorld), Colors.White, true);
        }
    }
}