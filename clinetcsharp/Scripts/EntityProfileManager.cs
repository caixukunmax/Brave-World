using Godot;
using System.Collections.Generic;
using System.Linq;

namespace ClinetCSharp
{
    /// <summary>
    /// 实体配置档案管理器 — Autoload 单例
    /// 统一管理所有 EntityProfile 的 CRUD、配置保存/加载、Profile 应用
    /// 兼容旧格式（monster_*/npc_*/player/labels/actionbar/levelbadge）自动转换
    /// </summary>
    public partial class EntityProfileManager : Node
    {
        public static EntityProfileManager Instance { get; private set; }

        private Dictionary<int, EntityProfile> _profiles = new();
        private int _nextId = 1;

        internal const string ConfigPath = "res://debug_panel_config.cfg";
        internal const int ConfigVersion = 4; // v4 = 定点整数序列化

        public override void _Ready()
        {
            if (Instance != null && Instance != this)
            {
                QueueFree();
                return;
            }
            Instance = this;

            EnsureDefaultProfiles();
            LoadConfig();
            NormalizeBuiltInProfiles();
            // 延迟绑定 ProfileId，等场景中的实体都 Ready 后再执行
            CallDeferred(nameof(AssignDefaultProfileIds));
        }

        #region Profile CRUD

        public EntityProfile GetProfile(int id)
        {
            _profiles.TryGetValue(id, out var profile);
            return profile;
        }

        public EntityProfile GetOrCreateProfile(int id, string name, string entityType)
        {
            if (_profiles.TryGetValue(id, out var profile))
                return profile;

            profile = new EntityProfile
            {
                Id = id,
                Name = name,
                EntityType = entityType,
            };
            _profiles[id] = profile;
            if (id >= _nextId)
                _nextId = id + 1;
            return profile;
        }

        public IEnumerable<EntityProfile> GetProfilesByType(string entityType)
            => _profiles.Values.Where(p => p.EntityType == entityType);

        public IEnumerable<EntityProfile> GetAllProfiles()
            => _profiles.Values;

        public EntityProfile CreateProfile(string name, string entityType)
        {
            var profile = new EntityProfile
            {
                Id = _nextId++,
                Name = name,
                EntityType = entityType,
            };
            _profiles[profile.Id] = profile;
            return profile;
        }

        public bool IsDefaultProfile(int id) => id >= 1 && id <= 3;

        public bool DeleteProfile(int id)
        {
            if (IsDefaultProfile(id))
                return false;
            return _profiles.Remove(id);
        }

        public EntityProfile CreateProfileFromTemplate(int templateId, string newName)
        {
            var template = GetProfile(templateId);
            if (template == null)
                return null;

            var profile = template.Clone(_nextId++, newName);
            _profiles[profile.Id] = profile;
            return profile;
        }

        #endregion

        #region Built-in Profile Normalization

        private void NormalizeBuiltInProfiles()
        {
            foreach (var profile in _profiles.Values)
            {
                if (profile.EntityType == "monster")
                {
                    var labels = profile.GetData<LabelGroupData>("labels");
                    if (labels != null)
                        EntityProfile.ConfigureMonsterLabelBindings(labels);
                    continue;
                }

                if (profile.EntityType == "player")
                {
                    var appearance = profile.GetData<AppearanceData>("appearance");
                    if (appearance != null && IsLegacyPlayerBackgroundDefault(appearance))
                        appearance.BgOpacity = 0.35f;
                }
            }
        }

        private static bool IsLegacyPlayerBackgroundDefault(AppearanceData appearance)
        {
            if (appearance == null)
                return false;

            bool opacityLooksLegacyDefault = Mathf.Abs(appearance.BgOpacity - 0.1f) < 0.001f;
            bool colorLooksUntouchedDefault =
                Mathf.Abs(appearance.BgColor.R - 1.0f) < 0.001f &&
                Mathf.Abs(appearance.BgColor.G - 1.0f) < 0.001f &&
                Mathf.Abs(appearance.BgColor.B - 1.0f) < 0.001f;

            return opacityLooksLegacyDefault && colorLooksUntouchedDefault;
        }

        #endregion

        #region Default Profiles

        private void EnsureDefaultProfiles()
        {
            if (!_profiles.ContainsKey(1))
                _profiles[1] = EntityProfile.CreatePlayerDefault(1);
            if (!_profiles.ContainsKey(2))
                _profiles[2] = EntityProfile.CreateMonsterDefault(2);
            if (!_profiles.ContainsKey(3))
                _profiles[3] = EntityProfile.CreateNpcDefault(3);

            _nextId = Mathf.Max(_nextId, _profiles.Keys.Max() + 1);
        }

        #endregion

        #region Default Profile Binding

        /// <summary>
        /// 给场景中已有的实体绑定默认 ProfileId
        /// Player → Profile 1, Monster → Profile 2, Npc → Profile 3
        /// 如果实体已有 ProfileId（从配置加载），则跳过
        /// </summary>
        private void AssignDefaultProfileIds()
        {
            var tree = GetTree();
            if (tree == null) return;

            int count = 0;
            // Player: 单例，通过 group 查找
            var player = tree.GetFirstNodeInGroup("player") as EntityBase;
            if (player != null && player.ProfileId > 0)
            {
                ApplyProfile(player, player.ProfileId);
                count++;
            }
            else if (player != null && player.ProfileId < 0)
            {
                player.ProfileId = 1;
                ApplyProfile(player, 1);
                count++;
                GD.Print($"[ProfileMgr] Assigned Player → ProfileId=1");
            }
            // Monster/NPC: 在 Manager 创建时已 ApplyProfile，这里也检查一次确保
            foreach (var node in tree.GetNodesInGroup("monster"))
            {
                if (node is EntityBase entity && entity.ProfileId > 0)
                {
                    ApplyProfile(entity, entity.ProfileId);
                    count++;
                }
            }
            foreach (var node in tree.GetNodesInGroup("npc"))
            {
                if (node is EntityBase entity && entity.ProfileId > 0)
                {
                    ApplyProfile(entity, entity.ProfileId);
                    count++;
                }
            }
            GD.Print($"[ProfileMgr] AssignDefaultProfileIds: assigned {count} entities");
        }

        #endregion

        #region Apply Profile

        /// <summary>
        /// 统一配置驱动 — 将 Profile 数据写入实体属性
        /// 不管是 Player/Monster/NPC，都是同一条路径
        /// <summary>
        /// 重置实体所有可选属性为默认/隐藏状态
        /// ApplyProfile 开头调用，确保 Profile 缺少的组件不会残留旧状态
        /// </summary>
        private void ResetEntityToDefaults(EntityBase entity)
        {
            // 血条/MP条/施法条：隐藏
            entity.HealthBarVisible = false;
            entity.MpBarVisible = false;
            entity.CastBarVisible = false;

            // Player 特有
            if (entity is Player p)
            {
                p.LevelBadgeVisible = false;
                p.FontSizeOverride = 0;
                p.SetFontBold(false);
                p.SetFontItalic(false);
                p.SetFontShadow(false);
            }
        }

        /// </summary>
        public void ApplyProfile(EntityBase entity, int profileId)
        {
            var profile = GetProfile(profileId);
            if (profile == null)
            {
                GD.PushError($"[EntityProfileManager] Profile {profileId} not found");
                return;
            }

            entity.ProfileId = profileId;

            // ═══ 先重置所有可选属性为默认/隐藏 ═══
            // 这样 Profile 缺什么组件，对应属性就不会残留旧状态
            ResetEntityToDefaults(entity);

            // ═══ 然后按 Profile 有的组件覆盖 ═══

            // Appearance
            var app = profile.GetData<AppearanceData>("appearance");
            if (app != null && !profile.IsComponentDisabled("appearance"))
            {
                entity.SetVisualSizeScale(app.VisualSizeScale);
                entity.SetBorderWidthScale(app.BorderWidthScale);
                entity.CornerRadius = app.CornerRadius;
                entity.BgOpacity = app.BgOpacity;
                entity.FontSize = app.FontSize;
                entity.BorderColor = app.BorderColor;
                entity.BgColor = app.BgColor;
                entity.TextColor = app.TextColor;
            }

            // Labels
            var labels = profile.GetData<LabelGroupData>("labels");
            if (labels != null && !profile.IsComponentDisabled("labels"))
            {
                for (int i = 0; i < 4; i++)
                {
                    // ContentPreview 为空时不覆盖实体运行时标签文字（如怪物名称/等级由 Setup 设定）
                    if (!string.IsNullOrEmpty(labels.ContentPreview[i]))
                        entity.SetRichLabelText(i, labels.ContentPreview[i]);
                    entity.SetLabelVisible(i, labels.Visible[i]);
                    entity.LabelFontSizes[i] = labels.UseGlobalFontSize[i] ? 0 : labels.FontSizes[i];
                    entity.LabelXOffsets[i] = labels.XOffset[i];
                    entity.LabelCenterX[i] = labels.CenterX[i];
                    entity.LabelYOffsets[i] = labels.YOffset[i];
                }

                // Player 使用 RichTextLabel 系统，需要同步到 _labelOffsets 和 _labelFontSizes
                if (entity is Player p)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        float newX = labels.CenterX[i] ? 0 : labels.XOffset[i];
                        var newOffset = new Vector2(newX, labels.YOffset[i]);
                        p.SetLabelOffset(i, newOffset);
                        p.SetLabelFontSize(i, labels.UseGlobalFontSize[i] ? 0 : labels.FontSizes[i]);
                        if (!string.IsNullOrEmpty(labels.ContentPreview[i]))
                            p.SetLabelText(i, labels.ContentPreview[i]);
                    }
                    // 全局字号
                    p.FontSizeOverride = labels.DefaultFontSize;
                    p.SetFontBold(labels.Bold);
                    p.SetFontItalic(labels.Italic);
                    p.SetFontShadow(labels.Shadow);
                    p.UpdateLabelFontSize();
                }

                // 非 Player 实体更新 RichTextLabel
                if (entity is not Player)
                {
                    entity.UpdateRichLabelFontSize();
                    entity.UpdateRichLabelPositions();
                }
            }

            // HealthBar
            var hpBar = profile.GetData<BarData>("healthbar");
            if (hpBar != null && !profile.IsComponentDisabled("healthbar"))
            {
                entity.HealthBarVisible = hpBar.Visible;
                entity.SetHealthBarLengthScale(hpBar.LengthScale);
                entity.SetHealthBarHeightScale(hpBar.HeightScale);
                entity.HealthBarFillPercent = hpBar.FillPercent;
                entity.HealthBarCenterX = hpBar.CenterX;
                entity.HealthBarOffset = new Vector2(hpBar.OffsetX, hpBar.OffsetY);
                entity.HealthBarColor = hpBar.Color;
            }

            // MPBar
            var mpBar = profile.GetData<BarData>("mpbar");
            if (mpBar != null && !profile.IsComponentDisabled("mpbar"))
            {
                entity.MpBarVisible = mpBar.Visible;
                entity.SetMpBarLengthScale(mpBar.LengthScale);
                entity.SetMpBarHeightScale(mpBar.HeightScale);
                entity.MpBarFillPercent = mpBar.FillPercent;
                entity.MpBarCenterX = mpBar.CenterX;
                entity.MpBarOffset = new Vector2(mpBar.OffsetX, mpBar.OffsetY);
                entity.MpBarColor = mpBar.Color;
            }

            // CastBar
            var castBar = profile.GetData<CastBarData>("castbar");
            if (castBar != null && !profile.IsComponentDisabled("castbar"))
            {
                entity.CastBarVisible = castBar.Visible;
                entity.SetCastBarLengthScale(castBar.LengthScale);
                entity.SetCastBarHeightScale(castBar.HeightScale);
                entity.CastBarFillPercent = castBar.FillPercent;
                entity.CastBarCenterX = castBar.CenterX;
                entity.CastBarOffset = new Vector2(castBar.OffsetX, castBar.OffsetY);
                entity.CastBarColor = castBar.Color;
            }

            // ActionBar
            var actionBar = profile.GetData<ActionBarData>("actionbar");
            if (actionBar != null && !profile.IsComponentDisabled("actionbar"))
            {
                entity.ActionBarForceShow = actionBar.ForceShow;
                entity.ActionBarTextYOffset = actionBar.TextYOffset;
                entity.ActionBarProgressHeight = actionBar.ProgressHeight;
            }

            // LevelBadge (Player specific)
            if (entity is Player player)
            {
                var badge = profile.GetData<LevelBadgeData>("levelbadge");
                if (badge != null && !profile.IsComponentDisabled("levelbadge"))
                {
                    player.LevelBadgeVisible = badge.Visible;
                    player.LevelBadgeFontSize = badge.FontSize;
                    player.LevelBadgeTextColor = badge.TextColor;
                    player.LevelBadgeText = badge.Text;
                    player.SetLevelBadgeOffset(new Vector2(badge.OffsetX, badge.OffsetY));
                }
            }

            // NpcInteract
            if (entity is Npc npc)
            {
                var interact = profile.GetData<NpcInteractData>("npc_interact");
                if (interact != null && !profile.IsComponentDisabled("npc_interact"))
                {
                    // NPC 交互面板偏移通过 NpcManager 设置
                    // 这里预留接口，Phase 2+ 实际对接
                }
            }

            entity.QueueRedraw();
        }

        /// <summary>
        /// 遍历所有用该 Profile 的实体，逐个 ApplyProfile
        /// </summary>
        public void ApplyProfileToAll(int profileId)
        {
            var profile = GetProfile(profileId);
            if (profile == null) { GD.PrintErr($"[ProfileMgr] ApplyProfileToAll: profile {profileId} not found"); return; }

            var tree = GetTree();
            if (tree == null) { GD.PrintErr("[ProfileMgr] ApplyProfileToAll: no tree"); return; }

            int applied = 0;
            string entityType = profile.EntityType;

            // Player: 单例，通过 group 查找，ProfileId 不匹配也尝试应用
            if (entityType == "player")
            {
                var player = tree.GetFirstNodeInGroup("player") as EntityBase;
                if (player != null)
                {
                    if (player.ProfileId < 0) player.ProfileId = profileId;
                    if (player.ProfileId == profileId)
                    { ApplyProfile(player, profileId); applied++; }
                }
            }

            // Monster/NPC: 通过 group 遍历，按 ProfileId 匹配
            foreach (var node in tree.GetNodesInGroup("monster"))
            {
                if (node is EntityBase entity && entity.ProfileId == profileId)
                { ApplyProfile(entity, profileId); applied++; }
            }
            foreach (var node in tree.GetNodesInGroup("npc"))
            {
                if (node is EntityBase entity && entity.ProfileId == profileId)
                { ApplyProfile(entity, profileId); applied++; }
            }
            GD.Print($"[ProfileMgr] ApplyProfileToAll: profileId={profileId}, type={entityType}, applied={applied} entities");
        }

        /// <summary>
        /// 对所有 Profile 逐一调用 ApplyProfileToAll，确保每个实体都被应用其 Profile
        /// 用于游戏启动时 Player 可能还没加入场景树的情况
        /// </summary>
        public void ApplyAllProfiles()
        {
            foreach (var profile in _profiles.Values)
                ApplyProfileToAll(profile.Id);
        }

        #endregion
    }
}
