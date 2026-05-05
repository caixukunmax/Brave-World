using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// 施法条组件数据 — 同 BarData 结构，默认值不同
    /// </summary>
    public class CastBarData : IComponentData
    {
        public bool Visible = true;
        public float LengthScale = 60.0f / 111.0f;
        public float HeightScale = 4.0f / 111.0f;
        public float FillPercent = 0.0f;
        public bool CenterX = true;
        public float OffsetX = 0;
        public float OffsetY = -80;
        public Color Color = new Color(0.3f, 0.5f, 1, 1);

        // 服务端权威标记
        public HashSet<string> LockedProperties = new();

        public bool IsPropertyLocked(string propertyName) => LockedProperties.Contains(propertyName);
        public void LockProperty(string propertyName) => LockedProperties.Add(propertyName);
        public void UnlockProperty(string propertyName) => LockedProperties.Remove(propertyName);

        public IComponentData Clone()
        {
            return new CastBarData
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
    }
}
