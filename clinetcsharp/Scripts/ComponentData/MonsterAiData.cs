namespace ClinetCSharp
{
    /// <summary>
    /// 怪物 AI 组件数据 — 移速/巡逻/仇恨
    /// </summary>
    public class MonsterAiData : IComponentData
    {
        public int MoveSpeedMs = 800;
        public float PatrolRange = 3f;
        public float AggroRange = 5f;
        public int MoveIntervalMs = 2000;

        public IComponentData Clone()
        {
            return new MonsterAiData
            {
                MoveSpeedMs = MoveSpeedMs,
                PatrolRange = PatrolRange,
                AggroRange = AggroRange,
                MoveIntervalMs = MoveIntervalMs,
            };
        }
    }
}
