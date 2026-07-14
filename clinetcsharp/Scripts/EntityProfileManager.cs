using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using ClinetCSharp.RenderComponents;

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

        /// <summary>建筑配置 ID 按建筑类型的自增计数器：type → next sequence</summary>
        private readonly Dictionary<int, int> _buildingConfigCounters = new();

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
            // 旧格式配置会清空 profile，新格式也可能缺少默认建筑的 category 组件，
            // 因此在加载完成后再兜底一次，确保默认建筑及其分类存在。
            EnsureDefaultProfiles();
            NormalizeBuiltInProfiles();

            // 同步到 DecorationConfigUtil 兼容层，保证旧接口（如 MapEditor 建筑列表）能读到数据。
            // NetworkManager._Ready 调用 DecorationConfigUtil.Load 时，本单例可能尚未初始化，
            // 因此必须在加载完成后主动刷新一次。
            DecorationConfigUtil.RefreshFromProfileManager();

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

        public bool IsDefaultProfile(int id)
        {
            // 玩家/怪物/NPC 默认 1-3；建筑默认配置区间基址 10000/20000/30000/40000/50000
            // 10000=树，10001=房舍（2x2），两者都视为默认配置防止误删
            return (id >= 1 && id <= 3)
                || id == BuildingType.GetConfigBaseId(BuildingType.House)
                || id == BuildingType.GetConfigBaseId(BuildingType.House) + 1
                || id == BuildingType.GetConfigBaseId(BuildingType.Shop)
                || id == BuildingType.GetConfigBaseId(BuildingType.Well)
                || id == BuildingType.GetConfigBaseId(BuildingType.Farm)
                || id == BuildingType.GetConfigBaseId(BuildingType.Tavern)
                || id == BuildingType.GetConfigBaseId(BuildingType.SpawnPoint)
                || id == BuildingType.GetConfigBaseId(BuildingType.Portal)
                || id == BuildingType.GetConfigBaseId(BuildingType.Water)
                || id == BuildingType.GetConfigBaseId(BuildingType.Rock)
                // 旧岩石装饰 ID 10002 / 草地 10003，保留兼容
                || id == BuildingType.GetConfigBaseId(BuildingType.House) + 2
                || id == BuildingType.GetConfigBaseId(BuildingType.House) + 3;
        }

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

        /// <summary>从模板克隆一个新的建筑配置，ID 落在建筑类型对应区间</summary>
        public EntityProfile CreateBuildingProfileFromTemplate(int templateId, string newName)
        {
            var template = GetProfile(templateId);
            if (template == null)
                return null;

            int buildingType = template.GetData<BuildingTypeData>("building_type")?.Type ?? BuildingType.House;
            int newId = GetNextBuildingConfigId(buildingType);
            var profile = template.Clone(newId, newName);
            profile.EntityType = "decoration";
            _profiles[profile.Id] = profile;
            return profile;
        }

        /// <summary>注册一个已构造好的 Profile（用于外部克隆后注册）</summary>
        public void RegisterProfile(EntityProfile profile)
        {
            if (profile == null) return;
            _profiles[profile.Id] = profile;
            if (profile.Id >= _nextId)
                _nextId = profile.Id + 1;

            if (profile.EntityType == "decoration")
            {
                int type = BuildingType.GetTypeFromConfigId(profile.Id);
                if (BuildingType.IsValid(type))
                {
                    int seq = profile.Id - BuildingType.GetConfigBaseId(type) + 1;
                    if (_buildingConfigCounters.TryGetValue(type, out var existing))
                        _buildingConfigCounters[type] = Mathf.Max(existing, seq);
                    else
                        _buildingConfigCounters[type] = seq;
                }
            }
        }

        /// <summary>获取下一个可用的 Profile ID</summary>
        public int GetNextId() => _nextId++;

        /// <summary>获取下一个可用的建筑配置 ID（按建筑类型在对应区间自增）</summary>
        public int GetNextBuildingConfigId(int buildingType)
        {
            if (!BuildingType.IsValid(buildingType))
                buildingType = BuildingType.House;

            if (!_buildingConfigCounters.TryGetValue(buildingType, out var seq))
            {
                int baseId = BuildingType.GetConfigBaseId(buildingType);
                seq = 0;
                foreach (var id in _profiles.Keys)
                {
                    if (id >= baseId && id < baseId + BuildingType.ConfigIdMultiplier)
                        seq = Mathf.Max(seq, id - baseId + 1);
                }
            }

            int result = BuildingType.GetConfigBaseId(buildingType) + seq;
            _buildingConfigCounters[buildingType] = seq + 1;

            if (result >= _nextId)
                _nextId = result + 1;

            return result;
        }

        #endregion

        #region Built-in Profile Normalization

        private void NormalizeBuiltInProfiles()
        {
            foreach (var profile in _profiles.Values)
            {
                // 默认房舍（10001）强制 2x2，与服务器 buildings.json 保持一致
                if (profile.EntityType == "decoration" && profile.Id == BuildingType.GetConfigBaseId(BuildingType.House) + 1)
                {
                    var app = profile.GetData<AppearanceData>("appearance");
                    if (app != null)
                    {
                        app.SizeX = 2;
                        app.SizeY = 2;
                    }
                }

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

            // 默认建筑 Profiles
            // 10000=树(1x1, Terrain)，10001=房舍(2x2, Building)，10002=岩石(旧兼容, Terrain)，10003=草地(Terrain)，
            // 20000=商店(Building)，30000=水井(Building)，40000=农田(Building)，50000=酒馆(Building)，
            // 60000=出生点(Special)，70000=共享传送门(Special)，80000=水域(Terrain)，90000=岩石(Terrain)
            // 局部辅助：若已存在则从配置加载的 profile 缺少 category 组件，这里只补 category，避免覆盖用户其他自定义。
            void EnsureDeco(int id, string name, string displayName, int buildingType, Color bgColor, Color borderColor, bool blockMovement, int sizeX, int sizeY, string category)
            {
                if (!_profiles.TryGetValue(id, out var profile))
                {
                    profile = EntityProfile.CreateDecorationDefault(id, name, displayName, buildingType, bgColor, borderColor, blockMovement, sizeX, sizeY, category);
                    _profiles[id] = profile;
                    return;
                }
                var catData = profile.GetData<CategoryData>("category");
                if (catData == null)
                    profile.SetData("category", new CategoryData { Category = category });
                else
                    catData.Category = category;
            }

            EnsureDeco(BuildingType.GetConfigBaseId(BuildingType.House), "Tree", "树", BuildingType.House,
                new Color(0.2f, 0.5f, 0.25f, 0.9f), new Color(0.1f, 0.35f, 0.15f), false, 1, 1, "Terrain");
            EnsureDeco(BuildingType.GetConfigBaseId(BuildingType.House) + 1, "House", "房舍", BuildingType.House,
                new Color(0.545f, 0.353f, 0.169f, 0.9f), new Color(0.4f, 0.2f, 0.1f), true, 2, 2, "Building");
            // 10002 岩石：历史地图生成器使用的旧 ID（House 区间），保留兼容防止旧地图渲染 fallback 成树
            EnsureDeco(BuildingType.GetConfigBaseId(BuildingType.House) + 2, "RockLegacy", "岩石", BuildingType.House,
                new Color(0.53f, 0.53f, 0.53f, 0.9f), new Color(0.35f, 0.35f, 0.35f), true, 1, 1, "Terrain");
            // 10003 草地：替换旧棕榈，作为地形类建筑
            EnsureDeco(BuildingType.GetConfigBaseId(BuildingType.House) + 3, "Grass", "草地", BuildingType.House,
                new Color(0.35f, 0.65f, 0.35f, 0.9f), new Color(0.2f, 0.45f, 0.2f), false, 1, 1, "Terrain");
            EnsureDeco(BuildingType.GetConfigBaseId(BuildingType.Shop), "Shop", "商店", BuildingType.Shop,
                new Color(0.2f, 0.4f, 0.6f, 0.9f), new Color(0.1f, 0.3f, 0.5f), true, 1, 1, "Building");
            EnsureDeco(BuildingType.GetConfigBaseId(BuildingType.Well), "Well", "水井", BuildingType.Well,
                new Color(0.5f, 0.5f, 0.55f, 0.9f), new Color(0.3f, 0.3f, 0.35f), true, 1, 1, "Building");
            EnsureDeco(BuildingType.GetConfigBaseId(BuildingType.Farm), "Farm", "农田", BuildingType.Farm,
                new Color(0.8f, 0.7f, 0.3f, 0.9f), new Color(0.5f, 0.4f, 0.1f), true, 2, 1, "Building");
            EnsureDeco(BuildingType.GetConfigBaseId(BuildingType.Tavern), "Tavern", "酒馆", BuildingType.Tavern,
                new Color(0.6f, 0.3f, 0.2f, 0.9f), new Color(0.4f, 0.15f, 0.1f), true, 2, 2, "Building");
            EnsureDeco(BuildingType.GetConfigBaseId(BuildingType.SpawnPoint), "SpawnPoint", "出生点", BuildingType.SpawnPoint,
                new Color(0.2f, 0.8f, 0.9f, 0.9f), new Color(0.1f, 0.5f, 0.6f), false, 1, 1, "Special");
            EnsureDeco(BuildingType.GetConfigBaseId(BuildingType.Portal), "Portal", "共享传送门", BuildingType.Portal,
                new Color(0.6f, 0.2f, 0.9f, 0.9f), new Color(0.4f, 0.1f, 0.7f), false, 1, 1, "Special");
            // 水域装饰：把底层水地形转换为建筑装饰，与房舍同为 decoration 级别
            EnsureDeco(BuildingType.GetConfigBaseId(BuildingType.Water), "Water", "水", BuildingType.Water,
                new Color(0.29f, 0.56f, 0.85f, 0.9f), new Color(0.15f, 0.35f, 0.6f), true, 1, 1, "Terrain");
            // 岩石装饰：独立建筑类型，取代旧 10002 成为岩石的标准 decoration ID
            EnsureDeco(BuildingType.GetConfigBaseId(BuildingType.Rock), "Rock", "岩石", BuildingType.Rock,
                new Color(0.53f, 0.53f, 0.53f, 0.9f), new Color(0.35f, 0.35f, 0.35f), true, 1, 1, "Terrain");

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
                entity.GridSizeX = app.SizeX > 0 ? app.SizeX : 1;
                entity.GridSizeY = app.SizeY > 0 ? app.SizeY : 1;
                entity.OnGridSizeChanged();
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

            // Obstacle (Decoration specific)
            if (entity is MapDecoration dec)
            {
                var obstacle = profile.GetData<ObstacleData>("obstacle");
                if (obstacle != null && !profile.IsComponentDisabled("obstacle"))
                    dec.BlockMovement = obstacle.BlockMovement;
                else
                    dec.BlockMovement = false;
            }

            // 同步渲染组件与 Profile 组件：避免渲染层硬编码
            SyncRenderComponents(entity, profile);

            entity.QueueRedraw();
        }

        /// <summary>
        /// 根据 Profile 组件启用情况，动态增删对应的渲染组件
        /// </summary>
        private void SyncRenderComponents(EntityBase entity, EntityProfile profile)
        {
            SyncRenderComponent<RenderComponents.CastBarComponent>(entity, profile, "castbar", () => new RenderComponents.CastBarComponent());
            SyncRenderComponent<RenderComponents.ActionBarComponent>(entity, profile, "actionbar", () => new RenderComponents.ActionBarComponent());
        }

        private void SyncRenderComponent<T>(EntityBase entity, EntityProfile profile, string componentName, Func<T> factory) where T : class, IRenderComponent
        {
            bool has = entity.GetRenderComponent<T>() != null;
            bool want = profile.HasComponent(componentName) && !profile.IsComponentDisabled(componentName);
            if (want && !has)
                entity.AddRenderComponent(factory());
            else if (!want && has)
                entity.RemoveRenderComponent<T>();
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

            // Decoration: 通过 group 遍历，按 ProfileId 匹配
            foreach (var node in tree.GetNodesInGroup("decoration"))
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
