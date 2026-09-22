namespace ClinetCSharp
{
    /// <summary>
    /// 预览实体类型判定（纯逻辑，可脱离引擎单测）。
    /// 是「EntityType 字符串 -> 预览实体种类」映射的唯一事实来源，
    /// EntityPreviewFactory 与所有预览调用点都以此为准，禁止在别处重复 switch。
    /// </summary>
    public static class EntityPreviewPolicy
    {
        public enum PreviewKind
        {
            Player,
            Monster,
            Npc,
            Decoration,
        }

        /// <summary>
        /// 解析 EntityType 为预览实体种类。
        /// 空串/未知串一律按 Decoration 兜底（MapDecoration 是最通用的盒子渲染路径，
        /// 且与地图编辑器对未知 Profile 的处理一致）。
        /// </summary>
        public static PreviewKind ResolveKind(string entityType)
        {
            return entityType switch
            {
                "player" => PreviewKind.Player,
                "monster" => PreviewKind.Monster,
                "npc" => PreviewKind.Npc,
                _ => PreviewKind.Decoration,
            };
        }
    }
}
