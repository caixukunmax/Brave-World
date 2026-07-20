using System.Linq;
using UnityClientSharp.Entity;
using UnityClientSharp.Map.Core;
using UnityEditor;
using UnityEngine;

namespace BraveWorld.Editor
{
    /// <summary>
    /// 各组件的 IMGUI 检查器：直接读写 IComponentData 字段（编辑器就地改数据对象，
    /// 无需 UGUI 版那套 SyncFrom/ToData）。字段标签/范围与已删除 UGUI 版一致。
    /// 调用方负责 EditorGUI.Begin/EndChangeCheck 与脏标记（MarkDirty）。
    /// </summary>
    public static class ComponentInspectors
    {
        public static void Appearance(AppearanceData d)
        {
            d.VisualSizeScale = EditorGUILayout.Slider("角色比例", d.VisualSizeScale, 0.1f, 1f);
            d.BorderWidthScale = EditorGUILayout.Slider("边框比例", d.BorderWidthScale, 0f, 0.2f);
            d.CornerRadius = EditorGUILayout.IntSlider("圆角半径", Mathf.RoundToInt(d.CornerRadius), 0, 60);
            d.BgOpacity = EditorGUILayout.Slider("背景不透明度", d.BgOpacity, 0f, 1f);
            d.FontSize = EditorGUILayout.IntSlider("字体大小(0=自动)", d.FontSize, 0, 48);
            d.SizeX = EditorGUILayout.IntSlider("占地宽度", d.SizeX, 1, 4);
            d.SizeY = EditorGUILayout.IntSlider("占地高度", d.SizeY, 1, 4);
            d.BorderColor = EditorGUILayout.ColorField("边框颜色", d.BorderColor);
            d.BgColor = EditorGUILayout.ColorField("背景颜色", d.BgColor);
            d.TextColor = EditorGUILayout.ColorField("文字颜色", d.TextColor);
        }

        public static void LabelGroup(LabelGroupData d)
        {
            d.DefaultFontSize = EditorGUILayout.IntSlider("默认字号(0=自动)", d.DefaultFontSize, 0, 48);
            using (new EditorGUILayout.HorizontalScope())
            {
                d.Bold = EditorGUILayout.ToggleLeft("粗体", d.Bold, GUILayout.Width(60));
                d.Italic = EditorGUILayout.ToggleLeft("斜体", d.Italic, GUILayout.Width(60));
                d.Shadow = EditorGUILayout.ToggleLeft("阴影", d.Shadow, GUILayout.Width(60));
            }
            for (int i = 0; i < LabelGroupData.LabelCount; i++)
            {
                EditorGUILayout.LabelField($"行{i}", EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    d.Visible[i] = EditorGUILayout.ToggleLeft("显示", d.Visible[i], GUILayout.Width(50));
                    GUILayout.Label("名称", GUILayout.Width(30));
                    d.Names[i] = EditorGUILayout.TextField(d.Names[i] ?? "");
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    d.CenterX[i] = EditorGUILayout.ToggleLeft("居中", d.CenterX[i], GUILayout.Width(50));
                    GUILayout.Label("X", GUILayout.Width(14));
                    d.XOffset[i] = EditorGUILayout.FloatField(d.XOffset[i], GUILayout.Width(56));
                    GUILayout.Label("Y", GUILayout.Width(14));
                    d.YOffset[i] = EditorGUILayout.FloatField(d.YOffset[i], GUILayout.Width(56));
                    GUILayout.Label("字号", GUILayout.Width(30));
                    d.FontSizes[i] = EditorGUILayout.IntField(d.FontSizes[i], GUILayout.Width(44));
                }
            }
        }

        public static void Nameplate(NameplateData d)
        {
            d.Visible = EditorGUILayout.Toggle("显示", d.Visible);
            d.YOffset = EditorGUILayout.Slider("Y偏移", d.YOffset, -120f, 0f);
            d.Spacing = EditorGUILayout.Slider("间距", d.Spacing, 0f, 16f);
            d.BarHeight = EditorGUILayout.Slider("顶/底栏高", d.BarHeight, 1f, 16f);
            d.CenterBoxHeight = EditorGUILayout.Slider("中块高", d.CenterBoxHeight, 8f, 48f);
            d.CenterBoxWidthScale = EditorGUILayout.Slider("中块宽比例", d.CenterBoxWidthScale, 0.2f, 1f);
            d.BarColor = EditorGUILayout.ColorField("顶/底栏颜色", d.BarColor);
            d.CenterBoxColor = EditorGUILayout.ColorField("中块颜色", d.CenterBoxColor);
        }

        /// <summary>血条/MP条/施法条共用（BarData）。</summary>
        public static void Bar(BarData d)
        {
            d.Visible = EditorGUILayout.Toggle("显示", d.Visible);
            d.LengthScale = EditorGUILayout.Slider("长度比例", d.LengthScale, 0.2f, 1.5f);
            d.HeightScale = EditorGUILayout.Slider("高度比例", d.HeightScale, 0.01f, 0.2f);
            d.FillPercent = EditorGUILayout.Slider("填充比例", d.FillPercent, 0f, 1f);
            d.CenterX = EditorGUILayout.Toggle("水平居中", d.CenterX);
            d.OffsetX = EditorGUILayout.Slider("X偏移", d.OffsetX, -100f, 100f);
            d.OffsetY = EditorGUILayout.Slider("Y偏移", d.OffsetY, -120f, 40f);
            d.Color = EditorGUILayout.ColorField("颜色", d.Color);
        }

        public static void ActionBar(ActionBarData d)
        {
            d.ForceShow = EditorGUILayout.Toggle("强制显示", d.ForceShow);
            d.TextYOffset = EditorGUILayout.Slider("文本Y偏移", d.TextYOffset, -40f, 40f);
            d.ProgressHeight = EditorGUILayout.Slider("进度条高度", d.ProgressHeight, 1f, 12f);
        }

        public static void LevelBadge(LevelBadgeData d)
        {
            d.Visible = EditorGUILayout.Toggle("显示", d.Visible);
            d.FontSize = EditorGUILayout.Slider("字号", d.FontSize, 0f, 48f);
            d.Text = EditorGUILayout.TextField("文本(含{level})", d.Text ?? "");
            d.CenterX = EditorGUILayout.Toggle("水平居中", d.CenterX);
            d.OffsetX = EditorGUILayout.Slider("X偏移", d.OffsetX, -100f, 100f);
            d.OffsetY = EditorGUILayout.Slider("Y偏移", d.OffsetY, -100f, 100f);
            d.TextColor = EditorGUILayout.ColorField("文字颜色", d.TextColor);
        }

        public static void MonsterAi(MonsterAiData d)
        {
            d.MoveSpeedMs = EditorGUILayout.IntSlider("移动速度(ms)", d.MoveSpeedMs, 100, 3000);
            d.PatrolRange = EditorGUILayout.Slider("巡逻范围", d.PatrolRange, 0f, 20f);
            d.AggroRange = EditorGUILayout.Slider("仇恨范围", d.AggroRange, 0f, 30f);
            d.MoveIntervalMs = EditorGUILayout.IntSlider("移动间隔(ms)", d.MoveIntervalMs, 200, 10000);
        }

        public static void NpcInteract(NpcInteractData d)
        {
            d.OffsetAX = EditorGUILayout.Slider("按钮A X", d.OffsetAX, -200f, 200f);
            d.OffsetAY = EditorGUILayout.Slider("按钮A Y", d.OffsetAY, -200f, 200f);
            d.OffsetBX = EditorGUILayout.Slider("按钮B X", d.OffsetBX, -200f, 200f);
            d.OffsetBY = EditorGUILayout.Slider("按钮B Y", d.OffsetBY, -200f, 200f);
        }

        public static void Obstacle(ObstacleData d)
        {
            d.BlockMovement = EditorGUILayout.Toggle("阻挡移动", d.BlockMovement);
            EditorGUILayout.LabelField("注：阻挡变更对寻路登记需重新进图才生效", EditorStyles.miniLabel);
        }

        public static void BuildingType(BuildingTypeData d)
        {
            int[] types = UnityClientSharp.Map.Core.BuildingType.GetAllTypes();
            string[] names = types.Select(UnityClientSharp.Map.Core.BuildingType.GetDisplayName).ToArray();
            int idx = System.Array.IndexOf(types, d.Type);
            if (idx < 0) idx = 0;
            idx = EditorGUILayout.Popup("类型", idx, names);
            d.Type = types[idx];
        }

        public static void Category(CategoryData d)
        {
            d.Category = EditorGUILayout.TextField("分类名", d.Category ?? "");
            EditorGUILayout.LabelField("常用: Terrain / Building / Special / Legacy", EditorStyles.miniLabel);
        }
    }
}
