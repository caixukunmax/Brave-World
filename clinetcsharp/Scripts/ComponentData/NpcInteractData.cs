namespace ClinetCSharp
{
    /// <summary>
    /// NPC 交互面板偏移组件数据
    /// </summary>
    public class NpcInteractData : IComponentData
    {
        public float OffsetAX = 60f;
        public float OffsetAY = -20f;
        public float OffsetBX = -60f;
        public float OffsetBY = -20f;

        public IComponentData Clone()
        {
            return new NpcInteractData
            {
                OffsetAX = OffsetAX,
                OffsetAY = OffsetAY,
                OffsetBX = OffsetBX,
                OffsetBY = OffsetBY,
            };
        }
    }
}
