// 网格覆盖着色器 — 移植自 clinetcsharp/assets/shaders/grid_overlay.gdshader (Godot GLSL canvas_item)。
// 在覆盖整张地图世界尺寸的 Quad 上绘制：抗锯齿网格线 + 地形色填充 + 水面 fbm 动画 + 地图外/地形墙灰色。
// Y 轴朝向：通过挂载此材质的 Quad 的 scale.y = -1 在渲染层统一翻转（不在此处处理），与 Godot 视觉一致。
Shader "UnityClientSharp/GridOverlay"
{
    Properties
    {
        _GridSize        ("Grid Size",        Float) = 111
        _LineWidth       ("Line Width",       Float) = 1.5
        _AaSoftness      ("AA Softness",      Float) = 1.0
        _LineColor       ("Line Color",       Color) = (1,1,1,1)
        _MapSizeWorld    ("Map Size World",   Vector)= (7100,7100,0,0)
        _TerrainMask     ("Terrain Mask",     2D)   = "white" {}
        _TerrainMaskSize ("Terrain Mask Size",Vector)= (1,1,0,0)
        _WaterMask       ("Water Mask",       2D)   = "black" {}
        _WaterMaskSize   ("Water Mask Size",  Vector)= (1,1,0,0)
        _OutsideMapColor ("Outside Map Color",Color) = (0.15,0.15,0.15,1)
        _WaterSpeed          ("Water Speed",          Float) = 0.05
        _WaterWaveStrength   ("Water Wave Strength",   Float) = 0.045
        _WaterSparkleSpeed   ("Water Sparkle Speed",   Float) = 0.12
        _WaterSparkleIntensity("Water Sparkle Intensity",Float) = 0.035
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        // scale.y = -1 会翻转三角形绕序，关闭剔除避免整块消失
        Cull Off
        ZWrite On
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            float _GridSize;
            float _LineWidth;
            float _AaSoftness;
            float4 _LineColor;
            float2 _MapSizeWorld;
            sampler2D _TerrainMask;
            float4 _TerrainMaskSize;
            sampler2D _WaterMask;
            float4 _WaterMaskSize;
            float4 _OutsideMapColor;
            float _WaterSpeed;
            float _WaterWaveStrength;
            float _WaterSparkleSpeed;
            float _WaterSparkleIntensity;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // 平滑 hash noise
            float water_hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            float water_noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(water_hash(i), water_hash(i + float2(1.0, 0.0)), u.x),
                            lerp(water_hash(i + float2(0.0, 1.0)), water_hash(i + float2(1.0, 1.0)), u.x), u.y);
            }

            float water_fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                for (int k = 0; k < 5; k++)
                {
                    value += amplitude * water_noise(p);
                    p *= 2.2;
                    amplitude *= 0.5;
                }
                return value;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Y 翻转只在 Quad 的 scale.y = -1 处做一次，此处不再二次翻转（双重翻转会让整张地图上下镜像）。
                // 翻转后 uv(0,0) 即逻辑地图左上角，遮罩纹理按 ly=0=逻辑 Y 顶部存储，行序与 grid_coord 对齐。
                float2 uv = i.uv;

                float2 world_pos = uv * _MapSizeWorld;

                // 当前像素所在格子坐标（限制在有效范围内）
                float2 grid_coord = world_pos / _GridSize;
                float2 cell_coord = clamp(floor(grid_coord), float2(0.0, 0.0), _TerrainMaskSize.xy - float2(1.0, 1.0));

                // 采样地形遮罩：R=遮罩(0=墙), GBA=地形颜色(RGB)
                float2 mask_uv = (cell_coord + 0.5) / _TerrainMaskSize.xy;
                float4 mask_sample = tex2D(_TerrainMask, mask_uv);
                float mask_value = mask_sample.r;
                float3 terrain_color = mask_sample.gba;

                // 采样水面遮罩
                float2 water_uv = (cell_coord + 0.5) / _WaterMaskSize.xy;
                float is_water = tex2D(_WaterMask, water_uv).r;

                // 到最近整数边界的距离
                float2 dist_to_line = abs(frac(grid_coord - 0.5) - 0.5);

                // 屏幕导数估算一个像素在网格坐标中的宽度
                float2 pixel_span = max(fwidth(grid_coord), float2(1e-6, 1e-6));
                float2 half_width = pixel_span * max(_LineWidth, 1.0) * 0.5;
                float2 aa_span = pixel_span * max(_AaSoftness, 1e-6);
                float2 axis_alpha = 1.0 - smoothstep(half_width, half_width + aa_span, dist_to_line);
                float grid_alpha = max(axis_alpha.x, axis_alpha.y) * _LineColor.a;

                // 地图外部检测
                bool outside_map = world_pos.x < 0.0 || world_pos.x > _MapSizeWorld.x ||
                                   world_pos.y < 0.0 || world_pos.y > _MapSizeWorld.y;

                float4 final_color;
                if (mask_value < 0.5 || outside_map)
                {
                    final_color = _OutsideMapColor;
                }
                else if (terrain_color.r > 0.001 || terrain_color.g > 0.001 || terrain_color.b > 0.001)
                {
                    float4 bg = float4(terrain_color, 1.0);

                    // 水面动画：柔和可见的波纹 + 偶尔微光
                    if (is_water > 0.5)
                    {
                        float t = _Time.y * _WaterSpeed;

                        float2 noise_uv = world_pos * 0.018 + float2(t, t * 0.35);
                        float n = water_fbm(noise_uv);

                        float phase = n * 3.14159;
                        float w1 = sin(world_pos.x * 0.035 + world_pos.y * 0.015 + t + phase) * _WaterWaveStrength;
                        float w2 = sin(world_pos.x * 0.022 - world_pos.y * 0.028 + t * 0.8 - phase * 0.7) * (_WaterWaveStrength * 0.7);
                        float wave = (w1 + w2) * 0.5;

                        float sparkle_phase = water_fbm(world_pos * 0.035 + float2(_Time.y * _WaterSparkleSpeed, -_Time.y * _WaterSparkleSpeed * 0.5));
                        float sparkle = pow(max(0.0, sparkle_phase - 0.55), 3.0) * _WaterSparkleIntensity;

                        bg.rgb += float3(wave * 0.45 + sparkle, wave * 0.55 + sparkle, wave * 0.95 + sparkle * 1.2);
                    }

                    final_color = lerp(bg, float4(_LineColor.rgb, 1.0), grid_alpha);
                }
                else
                {
                    // 有效格子但地形色为纯黑（如普通地形 color_a=0）：极暗灰填充
                    float4 default_fill = float4(0.06, 0.06, 0.06, 1.0);
                    final_color = lerp(default_fill, float4(_LineColor.rgb, 1.0), grid_alpha);
                }

                return final_color;
            }
            ENDCG
        }
    }
}
