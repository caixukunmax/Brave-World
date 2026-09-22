using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 预览实体工厂（单一事实来源）——按 Profile.EntityType 创建与游戏地图渲染一致的预览实体。
    /// 运行时调试面板（DebugPanelEntityTab）与编辑器插件（addons/debug_panel_editor）都必须走这里，
    /// 禁止各自 new 预览实体，否则两边效果必然再次漂移
    /// （历史教训：编辑器侧曾一律 new MapDecoration，player/monster/npc 预览全部失真）。
    ///
    /// 初始化顺序约定（要改实体初始化流程只能改这里）：
    ///   Monster/Npc：先 Setup 再 ApplyProfileToEntity（Setup 内部建渲染组件/标签节点）；
    ///   PlayerPreview：先 LoadStyleConfig()（与游戏 Player._Ready 一致的全局视觉缩放覆盖），
    ///     再在 AddChild（_Ready 建标签节点）之前 ApplyProfileToEntity（标签节点创建时读取已写入的字号/偏移）；
    ///     顺序与游戏一致（style 先、profile 后），故 Profile 有 appearance 时以其为准、否则回落 style 配置。
    ///   Decoration：走 SetupFromProfile（不依赖 EntityProfileManager 运行时单例，编辑器安全）。
    /// 渲染组件完整性由 ApplyProfileToEntity → SyncRenderComponents +
    /// 各实体 EnsureRenderComponents（按类型幂等，禁止 Count>0 早退）共同保证。
    /// </summary>
    public static class EntityPreviewFactory
    {
        /// <summary>创建预览实体并按给定 Profile 完成套用（无需调用方再 ApplyProfile）。</summary>
        public static EntityBase CreatePreviewEntity(EntityProfile profile)
        {
            switch (EntityPreviewPolicy.ResolveKind(profile.EntityType))
            {
                case EntityPreviewPolicy.PreviewKind.Monster:
                    var monster = new Monster();
                    monster.Setup(0, 0, 0, 0, "预览怪物", 1, 111, profile.Id);
                    EntityProfileManager.ApplyProfileToEntity(monster, profile);
                    return monster;

                case EntityPreviewPolicy.PreviewKind.Npc:
                    var npc = new Npc();
                    npc.Setup(0, "预览NPC", 0, 0, 0, 111);
                    EntityProfileManager.ApplyProfileToEntity(npc, profile);
                    return npc;

                case EntityPreviewPolicy.PreviewKind.Player:
                    var player = new PlayerPreview();
                    // 与游戏 Player._Ready 一致：先应用全局视觉缩放覆盖（debug_panel_config.cfg 的 player 段），
                    // 再 ApplyProfileToEntity 套用 Profile 外观。这样 Profile 有 appearance 时以其为准、
                    // 无 appearance 时回落到 style 配置——预览与游戏渲染规则完全对齐。
                    player.LoadStyleConfig();
                    EntityProfileManager.ApplyProfileToEntity(player, profile);
                    return player;

                default: // Decoration 及未知类型
                    var decoration = new MapDecoration();
                    decoration.SetProcessInput(false);
                    decoration.IsEditable = true;
                    // 与游戏同一套套用逻辑：设置视觉/尺寸/标签/血条/铭牌并同步渲染组件
                    decoration.SetupFromProfile(profile);
                    return decoration;
            }
        }

        /// <summary>当前预览实体是否仍与 Profile 类型匹配（不匹配需销毁重建）。</summary>
        public static bool TypeMatches(EntityProfile profile, EntityBase entity)
        {
            if (entity == null) return false;
            return EntityPreviewPolicy.ResolveKind(profile.EntityType) switch
            {
                EntityPreviewPolicy.PreviewKind.Monster => entity is Monster,
                EntityPreviewPolicy.PreviewKind.Npc => entity is Npc,
                EntityPreviewPolicy.PreviewKind.Player => entity is PlayerPreview,
                _ => entity is MapDecoration,
            };
        }
    }
}
