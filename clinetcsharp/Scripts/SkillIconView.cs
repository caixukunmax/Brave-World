using Godot;
using System;

namespace ClinetCSharp
{
    public partial class SkillIconView : Control
    {
        [Export] public uint SkillId { get; set; }
        [Export] public float CornerRadius { get; set; } = 7f;
        [Export] public float OutlineWidth { get; set; } = 2f;
        [Export] public bool DrawInset { get; set; } = true;

        public override void _Ready()
        {
            CustomMinimumSize = CustomMinimumSize == Vector2.Zero ? new Vector2(24, 24) : CustomMinimumSize;
        }

        public void SetSkill(uint skillId)
        {
            SkillId = skillId;
            QueueRedraw();
        }

        public override void _Draw()
        {
            var spec = SkillIconCatalog.Get(SkillId);
            var rect = new Rect2(Vector2.Zero, Size);
            if (rect.Size.X <= 2 || rect.Size.Y <= 2)
                return;

            DrawBackground(rect, spec);
            DrawGlyph(rect, spec);
        }

        private void DrawBackground(Rect2 rect, SkillIconSpec spec)
        {
            DrawRect(rect, spec.Background, true);
            var insetRect = rect.Grow(-Mathf.Max(2f, OutlineWidth));
            if (DrawInset && insetRect.Size.X > 2 && insetRect.Size.Y > 2)
                DrawRect(insetRect, spec.Secondary.Darkened(0.18f), true);

            DrawFrame(rect, spec.Outline, OutlineWidth);
        }

        private void DrawFrame(Rect2 rect, Color color, float width)
        {
            Vector2 tl = rect.Position;
            Vector2 tr = new(rect.End.X, rect.Position.Y);
            Vector2 bl = new(rect.Position.X, rect.End.Y);
            Vector2 br = rect.End;

            DrawLine(tl, tr, color, width);
            DrawLine(tr, br, color, width);
            DrawLine(br, bl, color, width);
            DrawLine(bl, tl, color, width);
        }

        private void DrawGlyph(Rect2 rect, SkillIconSpec spec)
        {
            switch (spec.Glyph)
            {
                case SkillIconGlyph.Slash:
                    DrawSlash(rect, spec);
                    break;
                case SkillIconGlyph.Shield:
                    DrawShield(rect, spec);
                    break;
                case SkillIconGlyph.Whirlwind:
                    DrawWhirlwind(rect, spec);
                    break;
                case SkillIconGlyph.Fireball:
                    DrawFireball(rect, spec);
                    break;
                case SkillIconGlyph.Snowflake:
                    DrawSnowflake(rect, spec);
                    break;
                case SkillIconGlyph.ArcaneBolt:
                    DrawArcaneBolt(rect, spec);
                    break;
                case SkillIconGlyph.Heal:
                    DrawHeal(rect, spec);
                    break;
                case SkillIconGlyph.HolyBurst:
                    DrawHolyBurst(rect, spec);
                    break;
                case SkillIconGlyph.Smite:
                    DrawSmite(rect, spec);
                    break;
                case SkillIconGlyph.Fangs:
                    DrawFangs(rect, spec);
                    break;
                case SkillIconGlyph.Howl:
                    DrawHowl(rect, spec);
                    break;
                case SkillIconGlyph.Spear:
                    DrawSpear(rect, spec);
                    break;
                case SkillIconGlyph.Roar:
                    DrawRoar(rect, spec);
                    break;
                case SkillIconGlyph.StoneSkin:
                    DrawStoneSkin(rect, spec);
                    break;
                case SkillIconGlyph.PoisonCloud:
                    DrawPoisonCloud(rect, spec);
                    break;
                case SkillIconGlyph.Freeze:
                    DrawFreeze(rect, spec);
                    break;
                case SkillIconGlyph.Bless:
                    DrawBless(rect, spec);
                    break;
                case SkillIconGlyph.VenomFang:
                    DrawVenomFang(rect, spec);
                    break;
                case SkillIconGlyph.Breath:
                    DrawBreath(rect, spec);
                    break;
                case SkillIconGlyph.Taunt:
                    DrawTaunt(rect, spec);
                    break;
                case SkillIconGlyph.Flame:
                    DrawFlame(rect, spec);
                    break;
                case SkillIconGlyph.Cleanse:
                    DrawCleanse(rect, spec);
                    break;
                case SkillIconGlyph.Berserk:
                    DrawBerserk(rect, spec);
                    break;
                default:
                    DrawCircle(rect.GetCenter(), rect.Size.X * 0.14f, spec.Accent);
                    break;
            }
        }

        private static Rect2 Inner(Rect2 rect, float pad) => rect.Grow(-pad);

        private void DrawSlash(Rect2 rect, SkillIconSpec spec)
        {
            var inner = Inner(rect, rect.Size.X * 0.18f);
            DrawLine(new Vector2(inner.Position.X, inner.End.Y), new Vector2(inner.End.X, inner.Position.Y), spec.Accent, 4f);
            DrawLine(new Vector2(inner.Position.X + 4f, inner.End.Y), new Vector2(inner.End.X, inner.Position.Y + 4f), spec.Secondary.Lightened(0.45f), 2f);
        }

        private void DrawShield(Rect2 rect, SkillIconSpec spec)
        {
            var c = rect.GetCenter();
            var pts = new Vector2[]
            {
                new(c.X, rect.Position.Y + rect.Size.Y * 0.18f),
                new(rect.Position.X + rect.Size.X * 0.78f, rect.Position.Y + rect.Size.Y * 0.30f),
                new(rect.Position.X + rect.Size.X * 0.70f, rect.Position.Y + rect.Size.Y * 0.70f),
                new(c.X, rect.Position.Y + rect.Size.Y * 0.86f),
                new(rect.Position.X + rect.Size.X * 0.30f, rect.Position.Y + rect.Size.Y * 0.70f),
                new(rect.Position.X + rect.Size.X * 0.22f, rect.Position.Y + rect.Size.Y * 0.30f),
            };
            DrawColoredPolygon(pts, spec.Accent);
            DrawPolyline(Array.ConvertAll(pts, p => p), spec.Outline, 2f);
        }

        private void DrawWhirlwind(Rect2 rect, SkillIconSpec spec)
        {
            var c = rect.GetCenter();
            DrawArc(c, rect.Size.X * 0.28f, 0.45f, 5.7f, 28, spec.Accent, 3f);
            DrawArc(c + new Vector2(1.5f, 1.5f), rect.Size.X * 0.16f, 0.3f, 5.4f, 24, spec.Secondary.Lightened(0.5f), 3f);
        }

        private void DrawFireball(Rect2 rect, SkillIconSpec spec)
        {
            var c = rect.GetCenter() + new Vector2(1f, 1f);
            DrawCircle(c, rect.Size.X * 0.18f, spec.Accent);
            DrawColoredPolygon(new[]
            {
                new Vector2(c.X, rect.Position.Y + rect.Size.Y * 0.18f),
                new Vector2(c.X + rect.Size.X * 0.10f, c.Y),
                new Vector2(c.X - rect.Size.X * 0.02f, c.Y - rect.Size.Y * 0.03f),
                new Vector2(c.X - rect.Size.X * 0.10f, c.Y + rect.Size.Y * 0.03f),
            }, spec.Secondary.Lightened(0.4f));
        }

        private void DrawSnowflake(Rect2 rect, SkillIconSpec spec)
        {
            var c = rect.GetCenter();
            float r = rect.Size.X * 0.26f;
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.Pi / 3f * i;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                DrawLine(c - dir * r, c + dir * r, spec.Accent, 2f);
            }
            DrawCircle(c, 2.2f, spec.Secondary.Lightened(0.5f));
        }

        private void DrawArcaneBolt(Rect2 rect, SkillIconSpec spec)
        {
            var p1 = new Vector2(rect.Position.X + rect.Size.X * 0.34f, rect.Position.Y + rect.Size.Y * 0.16f);
            var p2 = new Vector2(rect.Position.X + rect.Size.X * 0.56f, rect.Position.Y + rect.Size.Y * 0.40f);
            var p3 = new Vector2(rect.Position.X + rect.Size.X * 0.46f, rect.Position.Y + rect.Size.Y * 0.44f);
            var p4 = new Vector2(rect.Position.X + rect.Size.X * 0.66f, rect.Position.Y + rect.Size.Y * 0.80f);
            var p5 = new Vector2(rect.Position.X + rect.Size.X * 0.34f, rect.Position.Y + rect.Size.Y * 0.54f);
            DrawColoredPolygon(new[] { p1, p2, p3, p4, p5 }, spec.Accent);
        }

        private void DrawHeal(Rect2 rect, SkillIconSpec spec)
        {
            var c = rect.GetCenter();
            DrawRect(new Rect2(c.X - 3f, rect.Position.Y + rect.Size.Y * 0.22f, 6f, rect.Size.Y * 0.56f), spec.Accent, true);
            DrawRect(new Rect2(rect.Position.X + rect.Size.X * 0.22f, c.Y - 3f, rect.Size.X * 0.56f, 6f), spec.Accent, true);
        }

        private void DrawHolyBurst(Rect2 rect, SkillIconSpec spec)
        {
            var c = rect.GetCenter();
            for (int i = 0; i < 8; i++)
            {
                float a = Mathf.Pi / 4f * i;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                DrawLine(c, c + dir * rect.Size.X * 0.28f, spec.Accent, i % 2 == 0 ? 3f : 2f);
            }
            DrawCircle(c, rect.Size.X * 0.10f, spec.Secondary.Lightened(0.4f));
        }

        private void DrawSmite(Rect2 rect, SkillIconSpec spec)
        {
            DrawSlash(rect, spec);
            DrawCircle(rect.GetCenter() + new Vector2(rect.Size.X * 0.14f, -rect.Size.Y * 0.14f), 3f, spec.Secondary.Lightened(0.5f));
        }

        private void DrawFangs(Rect2 rect, SkillIconSpec spec)
        {
            DrawColoredPolygon(new[]
            {
                new Vector2(rect.Position.X + rect.Size.X * 0.28f, rect.Position.Y + rect.Size.Y * 0.18f),
                new Vector2(rect.Position.X + rect.Size.X * 0.46f, rect.Position.Y + rect.Size.Y * 0.80f),
                new Vector2(rect.Position.X + rect.Size.X * 0.38f, rect.Position.Y + rect.Size.Y * 0.80f),
            }, spec.Accent);
            DrawColoredPolygon(new[]
            {
                new Vector2(rect.Position.X + rect.Size.X * 0.72f, rect.Position.Y + rect.Size.Y * 0.18f),
                new Vector2(rect.Position.X + rect.Size.X * 0.54f, rect.Position.Y + rect.Size.Y * 0.80f),
                new Vector2(rect.Position.X + rect.Size.X * 0.62f, rect.Position.Y + rect.Size.Y * 0.80f),
            }, spec.Accent);
        }

        private void DrawHowl(Rect2 rect, SkillIconSpec spec)
        {
            var center = new Vector2(rect.Position.X + rect.Size.X * 0.36f, rect.Position.Y + rect.Size.Y * 0.50f);
            DrawCircle(center, rect.Size.X * 0.10f, spec.Accent);
            DrawArc(center, rect.Size.X * 0.18f, -0.8f, 0.8f, 18, spec.Accent, 2f);
            DrawArc(center, rect.Size.X * 0.28f, -0.7f, 0.7f, 18, spec.Secondary.Lightened(0.5f), 2f);
        }

        private void DrawSpear(Rect2 rect, SkillIconSpec spec)
        {
            var start = new Vector2(rect.Position.X + rect.Size.X * 0.24f, rect.Position.Y + rect.Size.Y * 0.74f);
            var end = new Vector2(rect.Position.X + rect.Size.X * 0.76f, rect.Position.Y + rect.Size.Y * 0.26f);
            DrawLine(start, end, spec.Accent, 3f);
            DrawColoredPolygon(new[]
            {
                end,
                end + new Vector2(-5f, 2f),
                end + new Vector2(-2f, 5f),
            }, spec.Secondary.Lightened(0.45f));
        }

        private void DrawRoar(Rect2 rect, SkillIconSpec spec)
        {
            var mouth = new Rect2(rect.Position.X + rect.Size.X * 0.24f, rect.Position.Y + rect.Size.Y * 0.38f, rect.Size.X * 0.18f, rect.Size.Y * 0.22f);
            DrawRect(mouth, spec.Accent, true);
            DrawArc(new Vector2(mouth.End.X, rect.GetCenter().Y), rect.Size.X * 0.22f, -0.6f, 0.6f, 18, spec.Accent, 3f);
            DrawArc(new Vector2(mouth.End.X, rect.GetCenter().Y), rect.Size.X * 0.32f, -0.5f, 0.5f, 18, spec.Secondary.Lightened(0.45f), 2f);
        }

        private void DrawStoneSkin(Rect2 rect, SkillIconSpec spec)
        {
            DrawColoredPolygon(new[]
            {
                new Vector2(rect.Position.X + rect.Size.X * 0.28f, rect.Position.Y + rect.Size.Y * 0.24f),
                new Vector2(rect.Position.X + rect.Size.X * 0.68f, rect.Position.Y + rect.Size.Y * 0.20f),
                new Vector2(rect.Position.X + rect.Size.X * 0.78f, rect.Position.Y + rect.Size.Y * 0.48f),
                new Vector2(rect.Position.X + rect.Size.X * 0.58f, rect.Position.Y + rect.Size.Y * 0.76f),
                new Vector2(rect.Position.X + rect.Size.X * 0.26f, rect.Position.Y + rect.Size.Y * 0.62f),
            }, spec.Accent);
            DrawLine(new Vector2(rect.Position.X + rect.Size.X * 0.46f, rect.Position.Y + rect.Size.Y * 0.22f), new Vector2(rect.Position.X + rect.Size.X * 0.52f, rect.Position.Y + rect.Size.Y * 0.72f), spec.Secondary.Lightened(0.4f), 2f);
        }

        private void DrawPoisonCloud(Rect2 rect, SkillIconSpec spec)
        {
            DrawCircle(new Vector2(rect.Position.X + rect.Size.X * 0.40f, rect.Position.Y + rect.Size.Y * 0.54f), rect.Size.X * 0.14f, spec.Accent);
            DrawCircle(new Vector2(rect.Position.X + rect.Size.X * 0.54f, rect.Position.Y + rect.Size.Y * 0.44f), rect.Size.X * 0.16f, spec.Secondary.Lightened(0.15f));
            DrawCircle(new Vector2(rect.Position.X + rect.Size.X * 0.68f, rect.Position.Y + rect.Size.Y * 0.56f), rect.Size.X * 0.12f, spec.Accent);
        }

        private void DrawFreeze(Rect2 rect, SkillIconSpec spec)
        {
            DrawSnowflake(rect, spec);
            DrawLine(new Vector2(rect.Position.X + rect.Size.X * 0.24f, rect.Position.Y + rect.Size.Y * 0.76f), new Vector2(rect.Position.X + rect.Size.X * 0.76f, rect.Position.Y + rect.Size.Y * 0.24f), spec.Secondary.Lightened(0.45f), 2f);
        }

        private void DrawBless(Rect2 rect, SkillIconSpec spec)
        {
            var c = rect.GetCenter();
            DrawLine(new Vector2(c.X, rect.Position.Y + rect.Size.Y * 0.22f), new Vector2(c.X, rect.Position.Y + rect.Size.Y * 0.72f), spec.Accent, 3f);
            DrawLine(new Vector2(rect.Position.X + rect.Size.X * 0.28f, c.Y), new Vector2(rect.Position.X + rect.Size.X * 0.72f, c.Y), spec.Accent, 3f);
            DrawArc(c + new Vector2(0f, rect.Size.Y * 0.06f), rect.Size.X * 0.22f, 0.5f, 2.6f, 18, spec.Secondary.Lightened(0.5f), 2f);
        }

        private void DrawVenomFang(Rect2 rect, SkillIconSpec spec)
        {
            DrawFangs(rect, spec);
            DrawCircle(new Vector2(rect.Position.X + rect.Size.X * 0.38f, rect.Position.Y + rect.Size.Y * 0.82f), 2f, spec.Secondary.Lightened(0.5f));
            DrawCircle(new Vector2(rect.Position.X + rect.Size.X * 0.62f, rect.Position.Y + rect.Size.Y * 0.82f), 2f, spec.Secondary.Lightened(0.5f));
        }

        private void DrawBreath(Rect2 rect, SkillIconSpec spec)
        {
            var c = new Vector2(rect.Position.X + rect.Size.X * 0.36f, rect.GetCenter().Y);
            DrawArc(c, rect.Size.X * 0.14f, -0.9f, 0.9f, 18, spec.Accent, 3f);
            DrawArc(c + new Vector2(rect.Size.X * 0.14f, 0f), rect.Size.X * 0.22f, -0.8f, 0.8f, 18, spec.Secondary.Lightened(0.5f), 3f);
        }

        private void DrawTaunt(Rect2 rect, SkillIconSpec spec)
        {
            var start = new Vector2(rect.Position.X + rect.Size.X * 0.28f, rect.Position.Y + rect.Size.Y * 0.32f);
            var mid = new Vector2(rect.Position.X + rect.Size.X * 0.66f, rect.Position.Y + rect.Size.Y * 0.32f);
            DrawLine(start, mid, spec.Accent, 3f);
            DrawLine(mid, mid + new Vector2(-5f, -4f), spec.Accent, 3f);
            DrawLine(mid, mid + new Vector2(-5f, 4f), spec.Accent, 3f);
            DrawArc(new Vector2(rect.Position.X + rect.Size.X * 0.36f, rect.Position.Y + rect.Size.Y * 0.66f), rect.Size.X * 0.10f, 0f, Mathf.Pi, 14, spec.Secondary.Lightened(0.5f), 2f);
        }

        private void DrawFlame(Rect2 rect, SkillIconSpec spec)
        {
            DrawColoredPolygon(new[]
            {
                new Vector2(rect.Position.X + rect.Size.X * 0.52f, rect.Position.Y + rect.Size.Y * 0.16f),
                new Vector2(rect.Position.X + rect.Size.X * 0.66f, rect.Position.Y + rect.Size.Y * 0.46f),
                new Vector2(rect.Position.X + rect.Size.X * 0.56f, rect.Position.Y + rect.Size.Y * 0.82f),
                new Vector2(rect.Position.X + rect.Size.X * 0.34f, rect.Position.Y + rect.Size.Y * 0.56f),
                new Vector2(rect.Position.X + rect.Size.X * 0.40f, rect.Position.Y + rect.Size.Y * 0.30f),
            }, spec.Accent);
        }

        private void DrawCleanse(Rect2 rect, SkillIconSpec spec)
        {
            DrawArc(rect.GetCenter(), rect.Size.X * 0.24f, 0.4f, 5.2f, 24, spec.Accent, 3f);
            var tip = new Vector2(rect.Position.X + rect.Size.X * 0.68f, rect.Position.Y + rect.Size.Y * 0.26f);
            DrawColoredPolygon(new[]
            {
                tip,
                tip + new Vector2(-2f, 6f),
                tip + new Vector2(-7f, 1f),
            }, spec.Secondary.Lightened(0.5f));
        }

        private void DrawBerserk(Rect2 rect, SkillIconSpec spec)
        {
            DrawColoredPolygon(new[]
            {
                new Vector2(rect.Position.X + rect.Size.X * 0.26f, rect.Position.Y + rect.Size.Y * 0.72f),
                new Vector2(rect.Position.X + rect.Size.X * 0.40f, rect.Position.Y + rect.Size.Y * 0.22f),
                new Vector2(rect.Position.X + rect.Size.X * 0.52f, rect.Position.Y + rect.Size.Y * 0.66f),
                new Vector2(rect.Position.X + rect.Size.X * 0.66f, rect.Position.Y + rect.Size.Y * 0.22f),
                new Vector2(rect.Position.X + rect.Size.X * 0.78f, rect.Position.Y + rect.Size.Y * 0.72f),
            }, spec.Accent);
        }
    }
}
