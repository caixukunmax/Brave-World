namespace ClinetCSharp
{
    /// <summary>
    /// 动作栏组件数据 — 施法技能显示/进度条
    /// </summary>
    public class ActionBarData : IComponentData
    {
        public bool ForceShow = false;
        public float TextYOffset = 0f;
        public float ProgressHeight = 4f;

        public IComponentData Clone()
        {
            return new ActionBarData
            {
                ForceShow = ForceShow,
                TextYOffset = TextYOffset,
                ProgressHeight = ProgressHeight,
            };
        }
    }
}
