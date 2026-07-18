using UnityClientSharp.Map.Core;
using UnityEngine;

namespace UnityClientSharp.Map.Rendering
{
    /// <summary>
    /// 地图缩略图构建器：从 GridData 生成一张地形颜色缩略图（小地图 / 预览图共用）。
    /// 移植自 Godot MapViewControl.RebuildTerrainTexture 的缩略图生成逻辑。
    /// 静态缩略图不含水面动画，足够小地图/预览用途。
    /// </summary>
    public static class MapThumbnailBuilder
    {
        public static Texture2D Build(GridManager gm, int maxLongSide = 256)
        {
            var bounds = gm.MapBounds;
            int w = Mathf.Max(1, bounds.width);
            int h = Mathf.Max(1, bounds.height);

            int texW = w, texH = h;
            int longSide = Mathf.Max(texW, texH);
            if (longSide > maxLongSide)
            {
                float s = (float)maxLongSide / longSide;
                texW = Mathf.Max(1, Mathf.RoundToInt(texW * s));
                texH = Mathf.Max(1, Mathf.RoundToInt(texH * s));
            }

            var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };

            Color unknown = new Color(0.1f, 0.1f, 0.1f, 1f);
            float uScale = (float)w / texW;
            float vScale = (float)h / texH;

            for (int ty = 0; ty < texH; ty++)
            {
                for (int tx = 0; tx < texW; tx++)
                {
                    int gx = Mathf.FloorToInt(bounds.x + tx * uScale);
                    int gy = Mathf.FloorToInt(bounds.y + ty * vScale);
                    var cell = gm.GetCell(new Vector2Int(gx, gy));

                    Color c = unknown;
                    if (cell != null)
                    {
                        if (cell.TerrainType == 9)
                            c = gm.OutsideMapColor;
                        else
                        {
                            c = cell.GetTerrainColor();
                            if (c.a <= 0.001f) c = unknown;
                        }
                    }
                    tex.SetPixel(tx, ty, c);
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
