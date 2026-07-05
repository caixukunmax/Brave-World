namespace ClinetCSharp
{
    /// <summary>
    /// 障碍组件数据 — 控制建筑是否阻塞移动
    /// </summary>
    public class ObstacleData : IComponentData
    {
        public bool BlockMovement = true;

        public IComponentData Clone()
        {
            return new ObstacleData
            {
                BlockMovement = BlockMovement,
            };
        }
    }
}
