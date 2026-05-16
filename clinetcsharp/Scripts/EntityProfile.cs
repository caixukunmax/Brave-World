using Godot;
using System.Collections.Generic;
using System.Linq;

namespace ClinetCSharp
{
    /// <summary>
    /// 实体配置档案 — 替代 EntityStyleConfig + Player 散落属性
    /// 一个命名的配置，包含 ID、名称、实体类型和一组组件数据
    /// </summary>
    public class EntityProfile
    {
        public int Id;
        public string Name = "";
        public string EntityType = ""; // "player" / "monster" / "npc" / 自定义

        private Dictionary<string, IComponentData> _componentData = new();
        private HashSet<string> _disabledComponents = new();

        public bool HasComponent(string name) => _componentData.ContainsKey(name);

        /// <summary>组件是否停用（数据保留但不应用）</summary>
        public bool IsComponentDisabled(string name) => _disabledComponents.Contains(name);

        /// <summary>设置组件停用状态</summary>
        public void SetComponentDisabled(string name, bool disabled)
        {
            if (disabled) _disabledComponents.Add(name);
            else _disabledComponents.Remove(name);
        }

        /// <summary>获取启用的组件名（非停用）</summary>
        public IEnumerable<string> ActiveComponentNames => _componentData.Keys.Where(k => !_disabledComponents.Contains(k));

        public T GetData<T>(string name) where T : class, IComponentData
            => _componentData.TryGetValue(name, out var d) ? (T)d : null;

        public IComponentData GetData(string name)
            => _componentData.TryGetValue(name, out var d) ? d : null;

        public void SetData(string name, IComponentData data) => _componentData[name] = data;

        public void RemoveComponent(string name) { _componentData.Remove(name); _disabledComponents.Remove(name); }

        public IEnumerable<string> ComponentNames => _componentData.Keys;

        /// <summary>从旧 EntityStyleConfig 迁移</summary>
        public static EntityProfile FromStyleConfig(int id, EntityStyleConfig cfg,
            string name, string entityType)
        {
            var profile = new EntityProfile
            {
                Id = id,
                Name = name,
                EntityType = entityType,
            };

            // Appearance — 所有实体都有
            profile.SetData("appearance", new AppearanceData
            {
                VisualSizeScale = cfg.VisualSizeScale,
                BorderWidthScale = cfg.BorderWidthScale,
                CornerRadius = cfg.CornerRadius,
                BgOpacity = cfg.BgOpacity,
                FontSize = cfg.FontSize,
                BorderColor = cfg.BorderColor,
                BgColor = cfg.BgColor,
                TextColor = cfg.TextColor,
            });

            // Labels — 所有实体都有
            var labelData = new LabelGroupData();
            for (int i = 0; i < 4; i++)
            {
                labelData.ContentPreview[i] = cfg.LabelTexts[i] ?? "";
                labelData.UseGlobalFontSize[i] = cfg.LabelFontSizes[i] <= 0;
                labelData.FontSizes[i] = cfg.LabelFontSizes[i];
                labelData.XOffset[i] = cfg.LabelXOffsets[i];
                labelData.CenterX[i] = cfg.LabelCenterX[i];
                labelData.YOffset[i] = cfg.LabelYOffsets[i];
            }
            profile.SetData("labels", labelData);

            // HealthBar — 所有实体都有
            profile.SetData("healthbar", new BarData
            {
                Visible = cfg.HpBarVisible,
                LengthScale = cfg.HpBarLengthScale,
                HeightScale = cfg.HpBarHeightScale,
                FillPercent = cfg.HpBarFillPercent,
                CenterX = cfg.HpBarCenterX,
                OffsetX = cfg.HpBarOffsetX,
                OffsetY = cfg.HpBarOffsetY,
                Color = cfg.HpBarColor,
            });

            // MPBar — 所有实体都有
            profile.SetData("mpbar", new BarData
            {
                Visible = cfg.MpBarVisible,
                LengthScale = cfg.MpBarLengthScale,
                HeightScale = cfg.MpBarHeightScale,
                FillPercent = cfg.MpBarFillPercent,
                CenterX = cfg.MpBarCenterX,
                OffsetX = cfg.MpBarOffsetX,
                OffsetY = cfg.MpBarOffsetY,
                Color = cfg.MpBarColor,
            });

            // NPC 交互面板偏移
            if (entityType == "npc")
            {
                profile.SetData("npc_interact", new NpcInteractData
                {
                    OffsetAX = cfg.InteractMenuOffsetAX,
                    OffsetAY = cfg.InteractMenuOffsetAY,
                    OffsetBX = cfg.InteractMenuOffsetBX,
                    OffsetBY = cfg.InteractMenuOffsetBY,
                });
            }

            return profile;
        }

        /// <summary>创建默认玩家 Profile</summary>
        public static EntityProfile CreatePlayerDefault(int id = 1)
        {
            var profile = new EntityProfile
            {
                Id = id,
                Name = "玩家",
                EntityType = "player",
            };

            profile.SetData("appearance", new AppearanceData
            {
                VisualSizeScale = 1.0f,
                BorderWidthScale = 3.0f / 111.0f,
                CornerRadius = 12.0f,
                BgOpacity = 0.35f,
                FontSize = 0,
                BorderColor = Colors.White,
                BgColor = Colors.White,
                TextColor = Colors.Black,
            });

            profile.SetData("labels", new LabelGroupData());

            profile.SetData("healthbar", BarData.CreateHealthBarDefault());
            profile.SetData("mpbar", BarData.CreateMpBarDefault());

            profile.SetData("castbar", new CastBarData());
            profile.SetData("actionbar", new ActionBarData());
            profile.SetData("levelbadge", new LevelBadgeData());

            return profile;
        }

        /// <summary>创建默认怪物 Profile</summary>
        public static EntityProfile CreateMonsterDefault(int id = 2)
        {
            var profile = new EntityProfile
            {
                Id = id,
                Name = "怪物",
                EntityType = "monster",
            };

            profile.SetData("appearance", new AppearanceData
            {
                VisualSizeScale = 1.0f,
                BorderWidthScale = 3.0f / 111.0f,
                CornerRadius = 12.0f,
                BgOpacity = 0.9f,
                FontSize = 0,
                BorderColor = new Color(0.9f, 0.3f, 0.3f),
                BgColor = new Color(0.8f, 0.2f, 0.2f),
                TextColor = new Color(1, 0.95f, 0.95f),
            });

            var labelData = new LabelGroupData();
            ConfigureMonsterLabelBindings(labelData);
            profile.SetData("labels", labelData);

            profile.SetData("healthbar", BarData.CreateHealthBarDefault());
            profile.SetData("mpbar", BarData.CreateMpBarDefault());

            profile.SetData("monster_ai", new MonsterAiData());

            return profile;
        }

        public static void ConfigureMonsterLabelBindings(LabelGroupData labels)
        {
            if (labels == null)
                return;

            labels.Names[0] = "名字";
            labels.Names[1] = "品质";
            labels.Names[2] = "状态";
            labels.Names[3] = string.IsNullOrWhiteSpace(labels.Names[3]) ? "预留" : labels.Names[3];

            labels.Visible[0] = true;
            labels.Visible[1] = true;
            labels.Visible[2] = true;
            labels.Visible[3] = false;

            labels.ContentPreview[0] = "";
            labels.ContentPreview[1] = "";
            labels.ContentPreview[2] = "";

            labels.LockContent(0);
            labels.LockContent(1);
            labels.LockContent(2);
        }

        /// <summary>创建默认 NPC Profile</summary>
        public static EntityProfile CreateNpcDefault(int id = 3)
        {
            var profile = new EntityProfile
            {
                Id = id,
                Name = "NPC",
                EntityType = "npc",
            };

            profile.SetData("appearance", new AppearanceData
            {
                VisualSizeScale = 1.0f,
                BorderWidthScale = 3.0f / 111.0f,
                CornerRadius = 12.0f,
                BgOpacity = 0.9f,
                FontSize = 0,
                BorderColor = new Color(0.3f, 0.5f, 0.9f),
                BgColor = new Color(0.2f, 0.4f, 0.8f),
                TextColor = new Color(0.95f, 0.97f, 1.0f),
            });

            profile.SetData("labels", new LabelGroupData());

            profile.SetData("npc_interact", new NpcInteractData());

            return profile;
        }
    }
}
