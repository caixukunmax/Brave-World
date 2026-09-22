namespace ClinetCSharp
{
    /// <summary>
    /// 建筑分类组件数据 — 用于在地图编辑器中把建筑分组（如地形/建筑/特殊）。
    /// </summary>
    public class CategoryData : IComponentData
    {
        /// <summary>建筑分类，例如：Terrain（地形）、Building（建筑）、Special（特殊）</summary>
        public string Category = "";

        public IComponentData Clone()
        {
            return new CategoryData
            {
                Category = Category,
            };
        }
    }
}
