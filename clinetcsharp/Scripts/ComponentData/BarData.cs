using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// 条形组件数据 — 血条/MP条通用
    /// Visible, LengthScale, HeightScale, FillPercent, CenterX, OffsetX/Y, Color
    /// </summary>
    public class BarData : IComponentData
    {
        public bool Visible = true;
        public float LengthScale = 102.0f / 111.0f;
        public float HeightScale = 6.0f / 111.0f;
        public float FillPercent = 1.0f;
        public bool CenterX = true;
        public float OffsetX = 0;
        public float OffsetY = -70;
        public Color Color = new Color(0, 0.8f, 0, 1);

        // 服务端权威标记
        public HashSet<string> LockedProperties = new();

        public bool IsPropertyLocked(string propertyName) => LockedProperties.Contains(propertyName);
        public void LockProperty(string propertyName) => LockedProperties.Add(propertyName);
        public void UnlockProperty(string propertyName) => LockedProperties.Remove(propertyName);

        public IComponentData Clone()
        {
            return new BarData
            {
                Visible = Visible,
                LengthScale = LengthScale,
                HeightScale = HeightScale,
                FillPercent = FillPercent,
                CenterX = CenterX,
                OffsetX = OffsetX,
                OffsetY = OffsetY,
                Color = Color,
                LockedProperties = new HashSet<string>(LockedProperties),
            };
        }

        /// <summary>创建默认血条数据</summary>
        public static BarData CreateHealthBarDefault() => new BarData
        {
            Visible = true,
            LengthScale = 102.0f / 111.0f,
            HeightScale = 6.0f / 111.0f,
            FillPercent = 1.0f,
            CenterX = true,
            OffsetX = 0,
            OffsetY = -70,
            Color = new Color(0, 0.8f, 0, 1),
        };

        /// <summary>创建默认MP条数据</summary>
        public static BarData CreateMpBarDefault() => new BarData
        {
            Visible = true,
            LengthScale = 80.0f / 111.0f,
            HeightScale = 4.0f / 111.0f,
            FillPercent = 1.0f,
            CenterX = true,
            OffsetX = 0,
            OffsetY = -62,
            Color = new Color(0.2f, 0.4f, 1.0f, 1),
        };
    }
}
