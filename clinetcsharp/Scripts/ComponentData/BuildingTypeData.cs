namespace ClinetCSharp
{
    /// <summary>
    /// 建筑类型组件数据 — 决定建筑配置 ID 与 UID 的区间。
    /// 取值见 <see cref="BuildingType"/>。
    /// </summary>
    public class BuildingTypeData : IComponentData
    {
        /// <summary>建筑类型，例如：房舍(1) / 商店(2)</summary>
        public int Type = BuildingType.House;

        public IComponentData Clone()
        {
            return new BuildingTypeData
            {
                Type = Type,
            };
        }
    }
}
