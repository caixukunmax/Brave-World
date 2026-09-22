using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 方向箭头绘制 — 支持 6 种样式，根据 4 方向旋转
    /// </summary>
    public static partial class EntityDrawUtils
    {
        /// <summary>绘制方向箭头</summary>
        public static void DrawDirectionArrow(EntityBase entity)
        {
            // 死亡的怪物不显示箭头
            if (entity is Monster && entity.CurrentHp <= 0)
                return;

            int dir = entity.Direction;
            float arrowSize = entity.VisualOuterSize * EntityBase.DirectionArrowSize;
            // 使用该方向配置的角度
            float angle = Mathf.DegToRad(EntityBase.DirectionArrowAngles[dir]);
            Vector2[] points = BuildArrowPoints(EntityBase.DirectionArrowStyle, arrowSize, angle);

            if (points.Length < 3)
                return;

            // 应用该方向配置的偏移
            Vector2 offset = EntityBase.DirectionArrowOffsets[dir];
            for (int i = 0; i < points.Length; i++)
                points[i] += offset;

            var color = EntityBase.DirectionArrowColor;
            color.A = EntityBase.DirectionArrowAlpha;
            entity.DrawColoredPolygon(points, color);
        }

        private static Vector2[] BuildArrowPoints(int style, float size, float angle)
        {
            return style switch
            {
                0 => BuildTriangleArrow(size, angle),
                1 => BuildChevronArrow(size, angle),
                2 => BuildConeArrow(size, angle),
                3 => BuildPointerArrow(size, angle),
                4 => BuildDotArrow(size, angle),
                5 => BuildCompassArrow(size, angle),
                _ => BuildTriangleArrow(size, angle),
            };
        }

        /// <summary>样式 0: 三角箭头 — 等腰三角形，顶点指向方向</summary>
        private static Vector2[] BuildTriangleArrow(float size, float angle)
        {
            var tip = new Vector2(size, 0);
            var baseLeft = new Vector2(-size * 0.3f, -size * 0.55f);
            var baseRight = new Vector2(-size * 0.3f, size * 0.55f);
            return RotatePoints(new[] { tip, baseLeft, baseRight }, angle);
        }

        /// <summary>样式 1: V 形双线 — 两条线组成 chevron</summary>
        private static Vector2[] BuildChevronArrow(float size, float angle)
        {
            var tip = new Vector2(size, 0);
            var mid = new Vector2(size * 0.1f, 0);
            var baseLeft = new Vector2(-size * 0.5f, -size * 0.6f);
            var baseRight = new Vector2(-size * 0.5f, size * 0.6f);
            return RotatePoints(new[] { tip, mid, baseLeft, baseRight }, angle);
        }

        /// <summary>样式 2: 扇形锥形 — 5 点扇形</summary>
        private static Vector2[] BuildConeArrow(float size, float angle)
        {
            var tip = new Vector2(size, 0);
            var midLeft = new Vector2(size * 0.2f, -size * 0.25f);
            var baseLeft = new Vector2(-size * 0.3f, -size * 0.7f);
            var baseRight = new Vector2(-size * 0.3f, size * 0.7f);
            var midRight = new Vector2(size * 0.2f, size * 0.25f);
            return RotatePoints(new[] { tip, midLeft, baseLeft, baseRight, midRight }, angle);
        }

        /// <summary>样式 3: 细长指针 — 窄三角形</summary>
        private static Vector2[] BuildPointerArrow(float size, float angle)
        {
            var tip = new Vector2(size, 0);
            var baseLeft = new Vector2(-size * 0.6f, -size * 0.2f);
            var baseRight = new Vector2(-size * 0.6f, size * 0.2f);
            return RotatePoints(new[] { tip, baseLeft, baseRight }, angle);
        }

        /// <summary>样式 4: 圆点指针 — 圆 + 短线</summary>
        private static Vector2[] BuildDotArrow(float size, float angle)
        {
            // 画一个圆点 + 短线，用三角形近似
            var tip = new Vector2(size * 0.9f, 0);
            var baseLeft = new Vector2(size * 0.3f, -size * 0.25f);
            var baseRight = new Vector2(size * 0.3f, size * 0.25f);
            return RotatePoints(new[] { tip, baseLeft, baseRight }, angle);
        }

        /// <summary>样式 5: 罗盘针 — 细长指针 + 反向小三角根部</summary>
        private static Vector2[] BuildCompassArrow(float size, float angle)
        {
            // 正向大三角
            var tip = new Vector2(size, 0);
            var baseLeft = new Vector2(-size * 0.4f, -size * 0.15f);
            var baseRight = new Vector2(-size * 0.4f, size * 0.15f);
            var backTip = new Vector2(-size * 0.6f, 0);
            return RotatePoints(new[] { tip, baseLeft, baseRight, backTip }, angle);
        }

        private static Vector2[] RotatePoints(Vector2[] points, float angle)
        {
            var cos = Mathf.Cos(angle);
            var sin = Mathf.Sin(angle);
            for (int i = 0; i < points.Length; i++)
            {
                float x = points[i].X;
                float y = points[i].Y;
                points[i] = new Vector2(x * cos - y * sin, x * sin + y * cos);
            }
            return points;
        }
    }
}