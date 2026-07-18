using System.Collections.Generic;
using UnityEngine;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// 实体身体贴图生成器 — 与 Godot EntityDrawUtils.DrawBody 等价的程序绘制：
    /// 圆角边框环（不透明）+ 内填充（bg 色 × BgOpacity），1px SDF 抗锯齿边缘。
    /// 按样式参数缓存复用（1 贴图像素 = 1 世界单位，与 GridSize=111 对齐）。
    /// 全项目实体外观只走这里，不引入美术资源（AGENTS.md 第 7 条）。
    /// </summary>
    public static class EntityBodySprite
    {
        private static readonly Dictionary<(int w, int h, int borderWidth, float radius, Color borderColor, Color bgColor, float opacity), Sprite> _cache = new();

        /// <summary>获取（或生成）指定样式的身体 Sprite，pivot 居中。</summary>
        public static Sprite Get(int outerW, int outerH, int borderWidth, float cornerRadius,
            Color borderColor, Color bgColor, float bgOpacity)
        {
            borderWidth = Mathf.Clamp(borderWidth, 0, Mathf.Max(0, Mathf.Min(outerW, outerH) / 2));
            float ro = Mathf.Clamp(cornerRadius, 0f, Mathf.Min(outerW, outerH) / 2f);
            var key = (outerW, outerH, borderWidth, ro, borderColor, bgColor, bgOpacity);
            if (_cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var tex = new Texture2D(outerW, outerH, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"EntityBody_{outerW}x{outerH}_b{borderWidth}_r{ro:F0}",
            };

            float ri = Mathf.Max(0f, ro - borderWidth);
            float halfW = outerW / 2f, halfH = outerH / 2f;
            float innerHalfW = halfW - borderWidth, innerHalfH = halfH - borderWidth;
            var pixels = new Color[outerW * outerH];

            for (int y = 0; y < outerH; y++)
            {
                for (int x = 0; x < outerW; x++)
                {
                    // 像素中心相对贴图中心
                    float px = x + 0.5f - halfW;
                    float py = y + 0.5f - halfH;

                    float dOuter = RoundedBoxSdf(px, py, halfW, halfH, ro);
                    float dInner = RoundedBoxSdf(px, py, innerHalfW, innerHalfH, ri);

                    float covOuter = Mathf.Clamp01(0.5f - dOuter);
                    float covInner = Mathf.Clamp01(0.5f - dInner);

                    // bg 叠加在不透明边框色上（source-over）
                    float effBgA = bgOpacity * covInner;
                    float r = bgColor.r * effBgA + borderColor.r * (1f - effBgA);
                    float g = bgColor.g * effBgA + borderColor.g * (1f - effBgA);
                    float b = bgColor.b * effBgA + borderColor.b * (1f - effBgA);
                    pixels[y * outerW + x] = new Color(r, g, b, covOuter);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, outerW, outerH), new Vector2(0.5f, 0.5f), 1f);
            sprite.name = tex.name;
            _cache[key] = sprite;
            return sprite;
        }

        /// <summary>圆角矩形有符号距离（负值=内部）。</summary>
        private static float RoundedBoxSdf(float px, float py, float halfW, float halfH, float radius)
        {
            float qx = Mathf.Abs(px) - halfW + radius;
            float qy = Mathf.Abs(py) - halfH + radius;
            float ax = Mathf.Max(qx, 0f), ay = Mathf.Max(qy, 0f);
            return Mathf.Min(Mathf.Max(qx, qy), 0f) + Mathf.Sqrt(ax * ax + ay * ay) - radius;
        }
    }
}
