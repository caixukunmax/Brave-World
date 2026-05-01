namespace ClinetCSharp
{
    public enum DebugPanelEntityType
    {
        Player,
        Monster,
        Npc,
    }

    public static class DebugPanelEntityStyleSyncPolicy
    {
        public static bool ShouldPropagateStyleChange(DebugPanelEntityType source, DebugPanelEntityType target)
        {
            return source == target;
        }
    }
}
