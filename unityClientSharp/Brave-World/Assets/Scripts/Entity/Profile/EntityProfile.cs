using System.Collections.Generic;
using System.Linq;
using UnityClientSharp.Map.Core;
using UnityEngine;

namespace UnityClientSharp.Entity
{
    /// <summary>组件数据接口（移植自 clinetcsharp/Scripts/IComponentData.cs）。</summary>
    public interface IComponentData
    {
        IComponentData Clone();
    }

    /// <summary>外观组件数据 — 大小/比例/边框/圆角/背景/颜色。</summary>
    public class AppearanceData : IComponentData
    {
        public float VisualSizeScale = 1.0f;
        public float BorderWidthScale = 3.0f / 111.0f;
        public float CornerRadius = 12.0f;
        public float BgOpacity = 0.9f;
        public int FontSize = 0; // 0 = 自动
        public int SizeX = 1;    // 占地宽度（格子数）
        public int SizeY = 1;    // 占地高度（格子数）

        public Color BorderColor = Color.white;
        public Color BgColor = Color.white;
        public Color TextColor = Color.black;

        public IComponentData Clone() => (AppearanceData)MemberwiseClone();
    }

    /// <summary>标签组组件数据 — 4 行文字标签（装饰只用第 0 行名称）。</summary>
    public class LabelGroupData : IComponentData
    {
        public const int LabelCount = 4;

        public int DefaultFontSize = 0; // 0 = 自动
        public bool Bold;
        public bool Italic;
        public bool Shadow;

        public bool[] Visible = { true, true, true, true };
        public string[] Names = { "", "", "", "" };
        public string[] ContentPreview = { "", "", "", "" };
        public bool[] UseGlobalFontSize = { true, true, true, true };
        public int[] FontSizes = { 0, 0, 0, 0 };
        public float[] XOffset = { 0, 0, 0, 0 };
        public bool[] CenterX = { true, true, true, true };
        public float[] YOffset = { 0, 0, 0, 0 };

        public IComponentData Clone()
        {
            var c = (LabelGroupData)MemberwiseClone();
            c.Visible = (bool[])Visible.Clone();
            c.Names = (string[])Names.Clone();
            c.ContentPreview = (string[])ContentPreview.Clone();
            c.UseGlobalFontSize = (bool[])UseGlobalFontSize.Clone();
            c.FontSizes = (int[])FontSizes.Clone();
            c.XOffset = (float[])XOffset.Clone();
            c.CenterX = (bool[])CenterX.Clone();
            c.YOffset = (float[])YOffset.Clone();
            return c;
        }
    }

    /// <summary>障碍组件数据 — 控制建筑是否阻塞移动。</summary>
    public class ObstacleData : IComponentData
    {
        public bool BlockMovement = true;
        public IComponentData Clone() => (ObstacleData)MemberwiseClone();
    }

    /// <summary>血条/MP条组件数据（默认值与 Godot ComponentData/BarData.cs 一致）。</summary>
    public class BarData : IComponentData
    {
        public bool Visible = true;
        public float LengthScale = 102f / 111f; // × 视觉外尺寸
        public float HeightScale = 6f / 111f;   // × 格子尺寸
        public float FillPercent = 1.0f;
        public bool CenterX = true;
        public float OffsetX = 0f;
        public float OffsetY = -70f;
        public Color Color = new Color(0f, 0.8f, 0f);

        public IComponentData Clone() => (BarData)MemberwiseClone();

        public static BarData CreateHealthBarDefault() => new BarData();

        public static BarData CreateMpBarDefault() => new BarData
        {
            LengthScale = 80f / 111f,
            HeightScale = 4f / 111f,
            OffsetY = -62f,
            Color = new Color(0.2f, 0.4f, 1f),
        };

        /// <summary>读条（CastBar）默认值（对齐 Godot EntityBase：offset(0,-80)、长 60/111、高 4/111、色 (0.3,0.5,1)）。</summary>
        public static BarData CreateCastBarDefault() => new BarData
        {
            LengthScale = 60f / 111f,
            HeightScale = 4f / 111f,
            OffsetY = -80f,
            FillPercent = 0f,
            Color = new Color(0.3f, 0.5f, 1f),
        };
    }

    /// <summary>建筑分类组件数据 — Terrain / Building / Special / Legacy。</summary>
    public class CategoryData : IComponentData
    {
        public string Category = "";
        public IComponentData Clone() => (CategoryData)MemberwiseClone();
    }

    /// <summary>建筑类型组件数据 — 取值见 <see cref="BuildingType"/>。</summary>
    public class BuildingTypeData : IComponentData
    {
        public int Type = BuildingType.House;
        public IComponentData Clone() => (BuildingTypeData)MemberwiseClone();
    }

    /// <summary>动作栏组件数据 — 施法技能显示/进度条（移植自 Godot ComponentData/ActionBarData.cs）。</summary>
    public class ActionBarData : IComponentData
    {
        public bool ForceShow = false;
        public float TextYOffset = 0f;
        public float ProgressHeight = 4f;

        public IComponentData Clone() => (ActionBarData)MemberwiseClone();
    }

    /// <summary>铭牌背景组件数据 — 三层填充色块背景板（移植自 Godot ComponentData/NameplateData.cs）。带 alpha 的颜色需连 color_a 序列化。</summary>
    public class NameplateData : IComponentData
    {
        public bool Visible = false;
        public float YOffset = -80f;
        public float Spacing = 4f;

        public float BarHeight = 6f;
        public Color BarColor = new Color(0.1f, 0.1f, 0.1f, 0.7f);

        public float CenterBoxHeight = 24f;
        public float CenterBoxWidthScale = 0.6f;
        public Color CenterBoxColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);

        public IComponentData Clone() => (NameplateData)MemberwiseClone();
    }

    /// <summary>等级徽章组件数据（移植自 Godot ComponentData/LevelBadgeData.cs）。</summary>
    public class LevelBadgeData : IComponentData
    {
        public bool Visible = true;
        public float FontSize = 12f;
        public Color TextColor = new Color(1f, 1f, 0f);
        public string Text = "Lv.{level}";
        public float OffsetX = -35f;
        public float OffsetY = -35f;
        public bool CenterX = false;

        public IComponentData Clone() => (LevelBadgeData)MemberwiseClone();
    }

    /// <summary>怪物 AI 组件数据 — 移速/巡逻/仇恨（移植自 Godot ComponentData/MonsterAiData.cs）。</summary>
    public class MonsterAiData : IComponentData
    {
        public int MoveSpeedMs = 800;
        public float PatrolRange = 3f;
        public float AggroRange = 5f;
        public int MoveIntervalMs = 2000;

        public IComponentData Clone() => (MonsterAiData)MemberwiseClone();
    }

    /// <summary>NPC 交互面板偏移组件数据（移植自 Godot ComponentData/NpcInteractData.cs）。</summary>
    public class NpcInteractData : IComponentData
    {
        public float OffsetAX = 60f;
        public float OffsetAY = -20f;
        public float OffsetBX = -60f;
        public float OffsetBY = -20f;

        public IComponentData Clone() => (NpcInteractData)MemberwiseClone();
    }

    /// <summary>
    /// 实体配置档案 — 移植自 clinetcsharp/Scripts/EntityProfile.cs（裁剪为装饰链路所需部分）。
    /// 一个命名的配置：ID、名称、实体类型和一组组件数据。
    /// </summary>
    public class EntityProfile
    {
        public int Id;
        public string Name = "";
        public string EntityType = ""; // "decoration" / "player" / "monster" / "npc"（后三者在在线阶段接入）

        private readonly Dictionary<string, IComponentData> _componentData = new();

        // 停用组件集合（移植自 Godot EntityProfile）：组件仍在配置中但不生效，
        // 由 SaveConfig 写入 disabled_components，LoadConfig 还原。
        private readonly HashSet<string> _disabledComponents = new();

        public bool HasComponent(string name) => _componentData.ContainsKey(name);

        public T GetData<T>(string name) where T : class, IComponentData
            => _componentData.TryGetValue(name, out var d) ? (T)d : null;

        /// <summary>非泛型取组件数据（供持久化层按组件名遍历写盘）。</summary>
        public IComponentData GetData(string name)
            => _componentData.TryGetValue(name, out var d) ? d : null;

        public void SetData(string name, IComponentData data) => _componentData[name] = data;

        public void RemoveComponent(string name) => _componentData.Remove(name);

        public IEnumerable<string> ComponentNames => _componentData.Keys;

        public bool IsComponentDisabled(string name) => _disabledComponents.Contains(name);

        public void SetComponentDisabled(string name, bool disabled)
        {
            if (disabled) _disabledComponents.Add(name);
            else _disabledComponents.Remove(name);
        }

        /// <summary>创建默认建筑 Profile（移植自 Godot 端 CreateDecorationDefault，数值勿改：与服务器 buildings.json 对齐）。</summary>
        public static EntityProfile CreateDecorationDefault(int id, string name, string displayName, int buildingType,
            Color bgColor, Color borderColor, bool blockMovement, int sizeX = 0, int sizeY = 0, string category = "")
        {
            var profile = new EntityProfile
            {
                Id = id,
                Name = name,
                EntityType = "decoration",
            };

            // 默认占地：显式传入 size 时优先；未传入时按建筑类型取默认值
            if (sizeX <= 0 || sizeY <= 0)
            {
                var (defaultX, defaultY) = buildingType switch
                {
                    BuildingType.House => (2, 2),
                    BuildingType.Tavern => (2, 2),
                    BuildingType.Farm => (2, 1),
                    _ => (1, 1),
                };
                if (sizeX <= 0) sizeX = defaultX;
                if (sizeY <= 0) sizeY = defaultY;
            }

            profile.SetData("appearance", new AppearanceData
            {
                // 建筑默认填满整个 footprint，避免跨格建筑看起来"浮在中间"
                VisualSizeScale = 1.0f,
                BorderWidthScale = 3.0f / 111.0f,
                CornerRadius = 8.0f,
                BgOpacity = bgColor.a,
                FontSize = 0,
                SizeX = sizeX,
                SizeY = sizeY,
                BorderColor = borderColor,
                BgColor = new Color(bgColor.r, bgColor.g, bgColor.b, 1.0f),
                TextColor = new Color(1, 1, 0.9f),
            });

            var labels = new LabelGroupData();
            labels.ContentPreview[0] = displayName;
            labels.Names[0] = "名称";
            labels.Visible[0] = true;
            labels.CenterX[0] = true;
            profile.SetData("labels", labels);

            profile.SetData("obstacle", new ObstacleData { BlockMovement = blockMovement });
            profile.SetData("building_type", new BuildingTypeData { Type = buildingType });
            profile.SetData("category", new CategoryData { Category = category });

            return profile;
        }

        /// <summary>创建默认玩家 Profile（对齐 Godot EntityProfile.CreatePlayerDefault，裁剪 castbar/actionbar/levelbadge）。</summary>
        public static EntityProfile CreatePlayerDefault(int id = 1)
        {
            var profile = new EntityProfile { Id = id, Name = "玩家", EntityType = "player" };

            profile.SetData("appearance", new AppearanceData
            {
                VisualSizeScale = 1.0f,
                BorderWidthScale = 3.0f / 111.0f,
                CornerRadius = 12.0f,
                BgOpacity = 0.35f,
                FontSize = 0,
                BorderColor = Color.white,
                BgColor = Color.white,
                TextColor = Color.black,
            });

            profile.SetData("labels", new LabelGroupData());
            profile.SetData("healthbar", BarData.CreateHealthBarDefault());
            profile.SetData("mpbar", BarData.CreateMpBarDefault());
            profile.SetData("castbar", BarData.CreateCastBarDefault());
            // TODO(战斗阶段): actionbar / levelbadge 组件

            return profile;
        }

        /// <summary>创建默认怪物 Profile（对齐 Godot EntityProfile.CreateMonsterDefault，红色系）。</summary>
        public static EntityProfile CreateMonsterDefault(int id = 2)
        {
            var profile = new EntityProfile { Id = id, Name = "怪物", EntityType = "monster" };

            profile.SetData("appearance", new AppearanceData
            {
                VisualSizeScale = 1.0f,
                BorderWidthScale = 3.0f / 111.0f,
                CornerRadius = 12.0f,
                BgOpacity = 0.9f,
                FontSize = 0,
                BorderColor = new Color(0.9f, 0.3f, 0.3f),
                BgColor = new Color(0.8f, 0.2f, 0.2f),
                TextColor = new Color(1f, 0.95f, 0.95f),
            });

            // 标签行绑定（对齐 ConfigureMonsterLabelBindings）：0 名字 / 1 品质 / 2 预留(隐藏) / 3 状态
            var labelData = new LabelGroupData();
            labelData.Names[0] = "名字";
            labelData.Names[1] = "品质";
            labelData.Names[2] = "预留";
            labelData.Names[3] = "状态";
            labelData.Visible[0] = true;
            labelData.Visible[1] = true;
            labelData.Visible[2] = false;
            labelData.Visible[3] = true;
            profile.SetData("labels", labelData);

            profile.SetData("healthbar", BarData.CreateHealthBarDefault());
            profile.SetData("mpbar", BarData.CreateMpBarDefault());
            profile.SetData("castbar", BarData.CreateCastBarDefault());
            // TODO(战斗阶段): actionbar / monster_ai 组件

            return profile;
        }

        /// <summary>创建默认 NPC Profile（对齐 Godot EntityProfile.CreateNpcDefault，蓝色系；条默认隐藏）。</summary>
        public static EntityProfile CreateNpcDefault(int id = 3)
        {
            var profile = new EntityProfile { Id = id, Name = "NPC", EntityType = "npc" };

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
            var hp = BarData.CreateHealthBarDefault();
            hp.Visible = false;
            var mp = BarData.CreateMpBarDefault();
            mp.Visible = false;
            profile.SetData("healthbar", hp);
            profile.SetData("mpbar", mp);
            // TODO(交互阶段): npc_interact 组件

            return profile;
        }
    }
}
