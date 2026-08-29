using System.Collections.Generic;
using Godot;

namespace ClinetCSharp
{
	public enum SkillArchetype { Slash, Projectile, Aoe, Aura }

	/// <summary>一个技能特效的规格：原型 + 配色 + 变化维度(数量/尺寸/旋转)。</summary>
	public class EffectSpec
	{
		public string Name = "";
		public SkillArchetype Archetype;
		public Color Main = Colors.White;
		public Color Accent = Colors.Orange;
		public float Radius = 96f;
		public float Duration = 0.4f;
		public int Count = 1;      // Slash=几道斩; Projectile=几发; Aura=几个环绕光点
		public bool Spin = false;  // Aoe: 旋转刀刃(true) vs 扩散环(false)
		public float Size = 1f;    // 整体缩放
	}

	/// <summary>
	/// 技能特效注册表：skillId → 规格。新增/调整只改这里。
	/// 用 Count/Spin/Size/配色 让同原型也演出不同动作。
	/// </summary>
	public static class SkillEffectRegistry
	{
		public static readonly Dictionary<int, EffectSpec> All = new()
		{
			// ===== 战士 job1 =====
			[2]  = S("烈斩",     SkillArchetype.Slash,     C(1,1,1),        C(1,0.45,0.12)),
			[3]  = S("盾击",     SkillArchetype.Slash,     C(0.7,0.85,1),   C(0.4,0.6,0.9), count: 2, size: 1.1f),
			[4]  = S("旋风斩",   SkillArchetype.Aoe,       C(1,1,1),        C(1,0.85,0.3),  130f, 0.5f, count: 4, spin: true, size: 1.4f),
			[20] = S("战吼",     SkillArchetype.Aoe,       C(1,0.35,0.3),   C(1,0.6,0.2),   120f, 0.5f, count: 3, spin: true),
			[21] = S("石肤术",   SkillArchetype.Aura,      C(0.75,0.6,0.4), C(0.9,0.8,0.5),  80f, 0.7f, size: 1.2f),
			[30] = S("挑衅",     SkillArchetype.Slash,     C(1,0.3,0.25),   C(1,0.55,0.2),  count: 1, size: 0.9f),
			// ===== 法师 job2 =====
			[5]  = S("火球术",   SkillArchetype.Projectile,C(1,0.55,0.15),  C(1,0.25,0.1),  count: 1, size: 1.4f),
			[6]  = S("冰霜新星", SkillArchetype.Aoe,       C(0.55,0.85,1),  C(0.85,0.95,1), 140f, 0.55f, size: 1.5f),
			[7]  = S("奥术飞弹", SkillArchetype.Projectile,C(0.75,0.45,1),  C(0.95,0.6,1),  count: 3, size: 0.7f),
			[22] = S("毒雾",     SkillArchetype.Aoe,       C(0.45,0.8,0.3), C(0.2,0.5,0.15),135f, 0.6f, count: 5, spin: true),
			[23] = S("冰冻术",   SkillArchetype.Projectile,C(0.4,0.8,1),    C(0.7,0.95,1),  count: 1, size: 1.0f),
			[31] = S("燃烧",     SkillArchetype.Projectile,C(1,0.5,0.15),   C(1,0.2,0.05),  count: 2, size: 0.9f),
			// ===== 牧师 job3 =====
			[9]  = S("治疗术",   SkillArchetype.Aura,      C(0.4,1,0.55),   C(0.85,1,0.9),   80f, 0.7f, count: 3),
			[10] = S("神圣之光", SkillArchetype.Aura,      C(1,0.9,0.45),   C(1,1,0.85),     85f, 0.7f, count: 1, size: 1.3f),
			[11] = S("惩击",     SkillArchetype.Projectile,C(1,0.92,0.5),   C(1,1,0.8),      count: 1, size: 0.9f),
			[24] = S("神圣护盾", SkillArchetype.Aura,      C(1,0.9,0.5),    C(1,1,0.9),      80f, 0.7f, count: 4, size: 1.1f),
			[25] = S("祝福",     SkillArchetype.Aura,      C(1,1,0.9),      C(1,0.9,0.5),    80f, 0.7f, count: 2, size: 1.1f),
			[32] = S("净化",     SkillArchetype.Aura,      C(0.9,1,1),      C(0.6,0.9,1),    80f, 0.7f, count: 5),
			// ===== 游侠 job4 =====
			[12] = S("撕咬",     SkillArchetype.Slash,     C(1,0.35,0.3),   C(0.7,0.1,0.1),  count: 2, size: 0.9f),
			[13] = S("狼嚎",     SkillArchetype.Aoe,       C(0.7,0.8,1),    C(0.4,0.5,0.8),  130f, 0.55f, size: 1.4f),
			[14] = S("骷髅突刺", SkillArchetype.Slash,     C(0.85,0.9,0.75),C(0.4,0.8,0.4),  count: 1, size: 1.1f),
			[26] = S("猛毒撕咬", SkillArchetype.Slash,     C(0.5,0.9,0.35), C(0.2,0.6,0.15), count: 2),
			[27] = S("寒冰吐息", SkillArchetype.Aoe,       C(0.55,0.85,1),  C(0.8,0.95,1),   135f, 0.55f, count: 4, spin: true, size: 1.4f),
			[33] = S("狂暴",     SkillArchetype.Aura,      C(1,0.3,0.25),   C(1,0.5,0.2),    80f, 0.7f, count: 3, size: 1.2f),
		};

		public static EffectSpec? Get(int skillId) => All.TryGetValue(skillId, out var s) ? s : null;

		private static EffectSpec S(string name, SkillArchetype a, Color main, Color accent,
			float radius = 96f, float dur = 0.4f, int count = 1, bool spin = false, float size = 1f)
			=> new() { Name = name, Archetype = a, Main = main, Accent = accent, Radius = radius, Duration = dur, Count = count, Spin = spin, Size = size };

		private static Color C(double r, double g, double b) => new((float)r, (float)g, (float)b);
	}

	/// <summary>
	/// 通用技能特效：按 EffectSpec 播放。播完自动 QueueFree。
	/// Slash→交叉多道斩; Projectile→扇形散射多枚+拖尾+命中爆点; Aoe→旋转刀刃 或 扩散新星; Aura→环绕光点 或 上升光柱。
	/// </summary>
	public partial class SkillEffect : Node2D
	{
		public EffectSpec Spec = new();
		public Vector2 TargetOffset;

		private static readonly Dictionary<int, ImageTexture> _glowCache = new();
		private static readonly Dictionary<int, ImageTexture> _ringCache = new();

		public static void Play(Node2D parent, Vector2 from, Vector2 to, EffectSpec spec)
		{
			if (parent == null || spec == null)
				return;
			var d = to - from;
			var fx = new SkillEffect { Spec = spec, ZIndex = 100, Position = from, TargetOffset = d };
			parent.AddChild(fx); // _Ready 读到已设好的 Position/TargetOffset
		}

		public override void _Ready()
		{
			switch (Spec.Archetype)
			{
				case SkillArchetype.Slash: BuildSlash(); break;
				case SkillArchetype.Projectile: BuildProjectile(); break;
				case SkillArchetype.Aoe: BuildAoe(); break;
				case SkillArchetype.Aura: BuildAura(); break;
			}
		}

		// ---------- Slash：1~2 道(交叉)斩，逐道错开 ----------
		private void BuildSlash()
		{
			float baseAng = TargetOffset.Angle();
			float len = TargetOffset.Length();
			int n = Mathf.Max(1, Spec.Count);
			var tw = CreateTween();
			for (int i = 0; i < n; i++)
			{
				int idx = i;
				tw.TweenCallback(Callable.From(() =>
				{
					float off = (idx - (n - 1) * 0.5f) * 0.55f; // 交叉角度
					var s = new MeleeSlashEffect
					{
						CoreColor = Spec.Main,
						EdgeColor = Spec.Accent,
						ArcRadius = Spec.Radius * Spec.Size,
						ZIndex = 2,
					};
					AddChild(s);
					s.Position = Vector2.Zero;
					s.Start(baseAng + off, len);
				}));
				if (i < n - 1) tw.TweenInterval(0.1f);
			}
			tw.TweenInterval(0.35f);
			tw.TweenCallback(Callable.From(QueueFree));
		}

		// ---------- Projectile：1~3 枚，扇形散开→汇聚到目标，带拖尾 ----------
		private void BuildProjectile()
		{
			float dist = TargetOffset.Length();
			float flight = Mathf.Clamp(dist / 900f, 0.3f, 0.6f);
			int n = Mathf.Max(1, Spec.Count);
			var dir = dist > 0.01f ? TargetOffset / dist : Vector2.Right;
			var side = new Vector2(-dir.Y, dir.X);
			var tw = CreateTween();
			for (int i = 0; i < n; i++)
			{
				int idx = i;
				tw.TweenCallback(Callable.From(() =>
				{
					float off = (idx - (n - 1) * 0.5f) * 30f;
					SpawnBolt(side * off * 0.6f, TargetOffset, flight);
				}));
				if (i < n - 1) tw.TweenInterval(0.07f);
			}
			tw.TweenInterval(flight + 0.5f);
			tw.TweenCallback(Callable.From(QueueFree));
		}

		private void SpawnBolt(Vector2 startLocal, Vector2 endLocal, float flight)
		{
			float sz = 0.85f * Spec.Size;
			var bolt = new Sprite2D { Texture = Glow(64), Modulate = new Color(Spec.Main, 1f), Scale = new Vector2(sz, sz), Position = startLocal, ZIndex = 2 };
			AddChild(bolt);

			var trail = new GpuParticles2D { Emitting = true, OneShot = false, Amount = 36, Lifetime = 0.35f, LocalCoords = false, Texture = Glow(24), Modulate = new Color(Spec.Accent, 0.9f), ZIndex = 1 };
			var tm = new ParticleProcessMaterial { Direction = new Vector3(0, -1, 0), Spread = 0f, Gravity = new Vector3(0, 0, 0), InitialVelocityMin = 0f, InitialVelocityMax = 18f, ScaleMin = 0.3f, ScaleMax = 0.7f };
			var ac = new Curve(); ac.AddPoint(new Vector2(0, 0.8f)); ac.AddPoint(new Vector2(1, 0));
			tm.AlphaCurve = new CurveTexture { Curve = ac };
			trail.ProcessMaterial = tm;
			bolt.AddChild(trail);

			var t = CreateTween();
			t.TweenProperty(bolt, "position", endLocal, flight).SetTrans(Tween.TransitionType.Linear);
			t.TweenCallback(Callable.From(() => { trail.Emitting = false; SpawnImpact(endLocal); }));
		}

		// ---------- Aoe：旋转刀刃(Spin) 或 扩散新星 ----------
		private void BuildAoe()
		{
			if (Spec.Spin)
			{
				BuildOrbit(Mathf.Max(3, Spec.Count), Spec.Radius * Spec.Size, Spec.Main, Spec.Accent, true, Spec.Duration + 0.35f);
				return;
			}
			var ring = new Sprite2D { Texture = Ring(128), Modulate = new Color(Spec.Main, 0.9f), Scale = new Vector2(0.2f, 0.2f), ZIndex = 1 };
			AddChild(ring);
			var core = new Sprite2D { Texture = Glow(96), Modulate = new Color(Spec.Accent, 0f), ZIndex = 2 };
			AddChild(core);
			SpawnBurst(Vector2.Zero, Spec.Accent, Spec.Radius * 1.6f);
			float target = Spec.Radius * Spec.Size * 2f / 128f;
			var tw = CreateTween();
			tw.TweenProperty(ring, "scale", new Vector2(target, target), Spec.Duration).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			tw.Parallel().TweenProperty(ring, "modulate:a", 0f, Spec.Duration);
			tw.TweenProperty(core, "modulate:a", 0f, 0.1f);
			tw.TweenCallback(Callable.From(QueueFree));
		}

		// ---------- Aura：多光点环绕(Count>1) 或 上升光柱 ----------
		private void BuildAura()
		{
			Position = Position + TargetOffset; // 移到目标处
			if (Spec.Count > 1)
			{
				var baseRing = new Sprite2D { Texture = Ring(96), Modulate = new Color(Spec.Main, 0.8f), Scale = new Vector2(0.3f, 0.3f), ZIndex = 1 };
				AddChild(baseRing);
				var rt = CreateTween();
				rt.TweenProperty(baseRing, "scale", new Vector2(1f, 1f), Spec.Duration).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
				rt.Parallel().TweenProperty(baseRing, "modulate:a", 0f, Spec.Duration);
				BuildOrbit(Spec.Count, Spec.Radius * Spec.Size, Spec.Main, Spec.Accent, false, Spec.Duration + 0.2f);
				return;
			}
			var ring = new Sprite2D { Texture = Ring(96), Modulate = new Color(Spec.Main, 0.9f), Scale = new Vector2(0.3f, 0.3f), ZIndex = 1 };
			AddChild(ring);
			var rise = new Sprite2D { Texture = Glow(64), Modulate = new Color(Spec.Accent, 0.9f), ZIndex = 2 };
			AddChild(rise);
			var tw = CreateTween();
			tw.TweenProperty(ring, "scale", new Vector2(1f, 1f), Spec.Duration).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			tw.Parallel().TweenProperty(ring, "modulate:a", 0f, Spec.Duration);
			tw.Parallel().TweenProperty(rise, "position", new Vector2(0, -60), Spec.Duration).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			tw.Parallel().TweenProperty(rise, "modulate:a", 0f, Spec.Duration);
			tw.TweenCallback(Callable.From(QueueFree));
		}

		// ---------- 环绕：N 个精灵绕中心旋转+淡出。blades=刀刃(大/快) 否则光点(小/慢) ----------
		private void BuildOrbit(int count, float radius, Color main, Color accent, bool blades, float dur)
		{
			var sp = new Node2D { ZIndex = 2 };
			AddChild(sp);
			float r = radius * 0.5f;
			for (int i = 0; i < count; i++)
			{
				float a = Mathf.Tau * i / count;
				var s = new Sprite2D
				{
					Texture = Glow(blades ? 48 : 40),
					Modulate = new Color(blades ? main : accent, 0.95f),
					Position = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r,
					Scale = new Vector2(blades ? 0.9f : 0.55f, blades ? 0.9f : 0.55f),
				};
				sp.AddChild(s);
			}
			var tw = CreateTween();
			tw.TweenMethod(Callable.From<float>(rot => sp.Rotation = rot), 0f, blades ? Mathf.Tau : Mathf.Tau * 0.6f, dur)
				.SetTrans(Tween.TransitionType.Linear);
			tw.Parallel().TweenProperty(sp, "modulate:a", 0f, dur);
			tw.TweenCallback(Callable.From(QueueFree));
		}

		private void SpawnImpact(Vector2 at)
		{
			var flash = new Sprite2D { Texture = Glow(96), Modulate = new Color(Spec.Main, 1f), Scale = new Vector2(0.3f, 0.3f), Position = at, ZIndex = 3 };
			AddChild(flash);
			SpawnBurst(at, Spec.Accent, Spec.Radius);
			var tw = CreateTween();
			tw.TweenProperty(flash, "scale", new Vector2(1.1f * Spec.Size, 1.1f * Spec.Size), 0.18f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			tw.Parallel().TweenProperty(flash, "modulate:a", 0f, 0.22f);
			tw.TweenCallback(Callable.From(() => { if (IsInstanceValid(flash)) flash.QueueFree(); }));
		}

		private void SpawnBurst(Vector2 at, Color color, float speed)
		{
			var p = new GpuParticles2D { Amount = 24, Lifetime = 0.5f, OneShot = true, Emitting = true, Explosiveness = 1f, Texture = Glow(24), Position = at, Modulate = color, ZIndex = 3 };
			var m = new ParticleProcessMaterial { Direction = new Vector3(0, -1, 0), Spread = 180f, Gravity = new Vector3(0, 260, 0), InitialVelocityMin = speed * 0.6f, InitialVelocityMax = speed, ScaleMin = 0.4f, ScaleMax = 0.9f };
			var sc = new Curve(); sc.AddPoint(new Vector2(0, 1)); sc.AddPoint(new Vector2(1, 0));
			m.ScaleCurve = new CurveTexture { Curve = sc };
			p.ProcessMaterial = m;
			AddChild(p);
		}

		// ---------- 共享纹理缓存 ----------
		private static ImageTexture Glow(int size)
		{
			if (_glowCache.TryGetValue(size, out var t)) return t;
			var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
			float h = size * 0.5f;
			for (int y = 0; y < size; y++)
				for (int x = 0; x < size; x++)
				{
					float dx = (x - h) / h, dy = (y - h) / h;
					float a = Mathf.Clamp(1f - Mathf.Sqrt(dx * dx + dy * dy), 0f, 1f);
					img.SetPixel(x, y, new Color(1, 1, 1, a * a));
				}
			var tex = ImageTexture.CreateFromImage(img);
			_glowCache[size] = tex;
			return tex;
		}

		private static ImageTexture Ring(int size)
		{
			if (_ringCache.TryGetValue(size, out var t)) return t;
			var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
			float h = size * 0.5f;
			for (int y = 0; y < size; y++)
				for (int x = 0; x < size; x++)
				{
					float dx = (x - h) / h, dy = (y - h) / h;
					float d = Mathf.Sqrt(dx * dx + dy * dy);
					float a = Mathf.Clamp(1f - Mathf.Abs(d - 0.82f) / 0.16f, 0f, 1f);
					img.SetPixel(x, y, new Color(1, 1, 1, a));
				}
			var tex = ImageTexture.CreateFromImage(img);
			_ringCache[size] = tex;
			return tex;
		}
	}
}
