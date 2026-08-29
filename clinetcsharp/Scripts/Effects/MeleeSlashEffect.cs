using System;
using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 程序化近战斩击特效（可复用）。放一个实例到施法者位置，调用 Start(朝向角, 距离)，
    /// 它会：起手聚能 → 朝目标方向扫过一道弧形刀光（shader）→ 命中处白闪 + 火星迸射 + 震屏回调，
    /// 播完自动 QueueFree。
    ///
    /// 纯代码/shader 构建，不依赖美术资源；所有观感参数 [Export] 可调。
    /// 默认配色为“烈斩”基调：炽白刀光 + 灼烧橙红火星。
    /// </summary>
    public partial class MeleeSlashEffect : Node2D
    {
        // ---- 观感参数（可调）----
        [Export] public Color CoreColor { get; set; } = new Color(1f, 1f, 1f);          // 刀光核心：炽白
        [Export] public Color EdgeColor { get; set; } = new Color(1f, 0.45f, 0.12f);     // 刀光边缘：灼烧橙
        [Export] public Color SparkColorA { get; set; } = new Color(1f, 0.85f, 0.55f);   // 火星亮端
        [Export] public Color SparkColorB { get; set; } = new Color(1f, 0.35f, 0.08f);   // 火星暗端
        [Export] public float ArcRadius { get; set; } = 96f;      // 刀光半径(px)
        [Export] public float ArcSpanRad { get; set; } = 2.1f;    // 弧长(弧度)
        [Export] public float ChargeTime { get; set; } = 0.14f;   // 起手聚能
        [Export] public float SweepTime { get; set; } = 0.16f;     // 刀光扫掠
        [Export] public float FadeTime { get; set; } = 0.16f;      // 刀光淡出
        [Export] public int SparkAmount { get; set; } = 46;        // 火星数量

        /// <summary>请求外部震屏（预览场景接一下即可）。</summary>
        [Signal] public delegate void ScreenShakeEventHandler(string kind);

        private ColorRect _arc = null!;
        private ShaderMaterial _arcMat = null!;
        private Node2D _impact = null!;      // 命中点容器（在本地 +X 方向）
        private GpuParticles2D _sparks = null!;
        private Sprite2D _flash = null!;
        private Sprite2D _chargeGlow = null!;

        public override void _Ready()
        {
            BuildArc();
            BuildChargeGlow();
            BuildImpact();
        }

        /// <summary>在 _Ready 之后调用；angleRad 为朝向目标的方向角，distance 为施法者到目标的距离(px)。</summary>
        public void Start(float angleRad, float distance)
        {
            Rotation = angleRad;
            if (_impact != null)
                _impact.Position = new Vector2(distance, 0);
            PlaySequence();
        }

        // ================= 构建 =================

        // 静态共享资源：避免每次施法都重新编译 shader / 逐像素生成纹理（战斗热路径）
        private static Shader? _sharedArcShader;
        private static readonly System.Collections.Generic.Dictionary<int, ImageTexture> _glowTexCache = new();

        private void BuildArc()
        {
            _sharedArcShader ??= new Shader { Code = ArcShaderSource };

            _arcMat = new ShaderMaterial { Shader = _sharedArcShader };
            _arcMat.SetShaderParameter("core_color", CoreColor);
            _arcMat.SetShaderParameter("edge_color", EdgeColor);
            _arcMat.SetShaderParameter("arc_len", ArcSpanRad);
            _arcMat.SetShaderParameter("sweep", 0f);
            _arcMat.SetShaderParameter("intensity", 0f);

            float r = ArcRadius;
            _arc = new ColorRect
            {
                Material = _arcMat,
                Size = new Vector2(r * 2f, r * 2f),
                Position = new Vector2(-r, -r),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                // 让 shader 的 UV 覆盖整块，ColorRect 自身颜色不影响（shader 完全决定 COLOR）
                Color = Colors.White,
            };
            AddChild(_arc);
        }

        private void BuildChargeGlow()
        {
            _chargeGlow = new Sprite2D
            {
                Texture = GetGlowTexture(64),
                Modulate = new Color(EdgeColor.R, EdgeColor.G, EdgeColor.B, 0.0f),
                Scale = new Vector2(0.2f, 0.2f),
                ZIndex = 1,
            };
            AddChild(_chargeGlow);
        }

        private void BuildImpact()
        {
            _impact = new Node2D { ZIndex = 2 };
            AddChild(_impact);

            _flash = new Sprite2D
            {
                Texture = GetGlowTexture(96),
                Modulate = new Color(1, 1, 1, 0f),
                Scale = new Vector2(0.3f, 0.3f),
            };
            _impact.AddChild(_flash);

            _sparks = new GpuParticles2D
            {
                Amount = SparkAmount,
                Lifetime = 0.55,
                OneShot = true,
                Emitting = false,
                Texture = GetGlowTexture(24),
                LocalCoords = false,
                ZIndex = 3,
            };
            _sparks.ProcessMaterial = MakeSparkMaterial();
            _impact.AddChild(_sparks);
        }

        private ParticleProcessMaterial MakeSparkMaterial()
        {
            var m = new ParticleProcessMaterial
            {
                Direction = new Vector3(0, -1, 0),
                Spread = 180f,
                Gravity = new Vector3(0, 420, 0),
                InitialVelocityMin = 120f,
                InitialVelocityMax = 300f,
                AngularVelocityMin = -180f,
                AngularVelocityMax = 180f,
                ScaleMin = 0.5f,
                ScaleMax = 1.1f,
            };

            var scaleCurve = new Curve();
            scaleCurve.AddPoint(new Vector2(0f, 1f));
            scaleCurve.AddPoint(new Vector2(1f, 0f));
            m.ScaleCurve = new CurveTexture { Curve = scaleCurve };

            var grad = new Gradient();
            grad.Colors = new[] { SparkColorA, SparkColorB, new Color(SparkColorB.R, SparkColorB.G, SparkColorB.B, 0f) };
            grad.Offsets = new[] { 0f, 0.6f, 1f };
            var ramp = new GradientTexture1D { Gradient = grad };
            m.ColorRamp = ramp;

            var alphaCurve = new Curve();
            alphaCurve.AddPoint(new Vector2(0f, 1f));
            alphaCurve.AddPoint(new Vector2(0.25f, 1f));
            alphaCurve.AddPoint(new Vector2(1f, 0f));
            m.AlphaCurve = new CurveTexture { Curve = alphaCurve };

            return m;
        }

        // ================= 播放 =================

        private void PlaySequence()
        {
            var tw = CreateTween();

            // 1) 起手聚能：光点放大 + 亮起
            tw.TweenProperty(_chargeGlow, "scale", new Vector2(0.9f, 0.9f), ChargeTime)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tw.Parallel().TweenProperty(_chargeGlow, "modulate:a", 0.9f, ChargeTime * 0.6f);

            // 2) 刀光淡入 + 扫掠
            tw.TweenMethod(Callable.From<float>(v => _arcMat.SetShaderParameter("intensity", v)), 0f, 1f, 0.05f);
            tw.Parallel().TweenMethod(Callable.From<float>(v => _arcMat.SetShaderParameter("sweep", v)), 0f, 1f, SweepTime)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);

            // 命中：在扫掠接近末尾时触发爆点
            tw.TweenCallback(Callable.From(TriggerImpact)).SetDelay(SweepTime * 0.75f);

            // 3) 刀光淡出
            tw.TweenMethod(Callable.From<float>(v => _arcMat.SetShaderParameter("intensity", v)), 1f, 0f, FadeTime)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);

            // 4) 收尾：聚能光淡出 + 释放
            tw.TweenProperty(_chargeGlow, "modulate:a", 0f, 0.1f);
            tw.TweenCallback(Callable.From(QueueFree));
        }

        private void TriggerImpact()
        {
            // 火星迸射
            _sparks.Restart();

            // 白闪：快速放大 + 淡出
            var ftw = CreateTween();
            ftw.TweenProperty(_flash, "modulate:a", 1f, 0.03f);
            ftw.Parallel().TweenProperty(_flash, "scale", new Vector2(1.3f, 1.3f), 0.18f)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            ftw.TweenProperty(_flash, "modulate:a", 0f, 0.22f).SetDelay(0.05f);

            // 通知外部震屏（预览场景会接）
            EmitSignal(SignalName.ScreenShake, "liezhan");
        }

        // ================= 工具 =================

        /// <summary>生成一张柔和径向发光贴图（白心→透明边），用作刀光/火星/闪光的纹理。</summary>
        private static ImageTexture GetGlowTexture(int size)
        {
            if (_glowTexCache.TryGetValue(size, out var cached) && cached != null)
                return cached;

            var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp(1f - d, 0f, 1f);
                    a = a * a; // 更聚心的高光
                    img.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            var tex = ImageTexture.CreateFromImage(img);
            _glowTexCache[size] = tex;
            return tex;
        }

        // 注意：CanvasItem shader 的 fragment() 里禁止 return（AGENTS.md 约定），用单一 COLOR 赋值收尾。
        private const string ArcShaderSource = @"shader_type canvas_item;

uniform vec4 core_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec4 edge_color : source_color = vec4(1.0, 0.45, 0.12, 1.0);
uniform float inner_radius = 0.30;
uniform float outer_radius = 0.50;
uniform float arc_len = 2.1;
uniform float sweep = 0.0;
uniform float intensity = 0.0;

void fragment() {
    vec2 p = UV - vec2(0.5);
    float r = length(p);
    float ang = atan(p.y, p.x);
    float half = arc_len * 0.5;

    // 径向环带（刀光的粗细）
    float band = smoothstep(inner_radius, inner_radius + 0.03, r)
               * (1.0 - smoothstep(outer_radius - 0.05, outer_radius, r));

    // 角向范围（弧的跨度，居中于本地 +X）
    float in_arc = 1.0 - smoothstep(half * 0.85, half, abs(ang));

    // 移动的高光头部（扫掠）
    float lead = mix(-half, half, clamp(sweep, 0.0, 1.0));
    float d = abs(ang - lead);
    float head = exp(-(d * d) / (0.20 * 0.20));

    float g = band * (in_arc * 0.5 + head * 1.7) * intensity;
    g = clamp(g, 0.0, 1.0);

    vec3 col = mix(edge_color.rgb, core_color.rgb, clamp(head, 0.0, 1.0));
    float a = g * mix(edge_color.a, core_color.a, clamp(head, 0.0, 1.0));
    COLOR = vec4(col * (1.0 + head * 0.8), a);
}
";
    }
}
