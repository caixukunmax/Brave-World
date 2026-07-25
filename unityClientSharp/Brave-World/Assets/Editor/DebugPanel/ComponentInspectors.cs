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
            d.SizeX = EditorGUILayout.IntSlider("占地宽度", d.SizeX, 1, 4);
            d.SizeY = EditorGUILayout.IntSlider("占地高度", d.SizeY, 1, 4);
            d.BorderColor = EditorGUILayout.ColorField("边框颜色", d.BorderColor);
            d.BgColor = EditorGUILayout.ColorField("背景颜色", d.BgColor);
        }

        // 折叠状态随行数伸缩（增删行后下标对齐）
        private static readonly System.Collections.Generic.List<bool> s_rowFolds = new() { true, true, true, true };

        public static void LabelGroup(LabelGroupData d)
        {
            d.DefaultFontSize = EditorGUILayout.IntSlider("字体大小(0=自动)", d.DefaultFontSize, 0, 48);
            using (new EditorGUILayout.HorizontalScope())
            {
                d.DefaultTextColor = EditorGUILayout.ColorField("文字颜色", d.DefaultTextColor);
                d.Bold = EditorGUILayout.ToggleLeft("粗体", d.Bold, GUILayout.Width(50));
                d.Italic = EditorGUILayout.ToggleLeft("斜体", d.Italic, GUILayout.Width(50));
                d.Shadow = EditorGUILayout.ToggleLeft("阴影", d.Shadow, GUILayout.Width(50));
            }
            EditorGUILayout.LabelField("（全局默认：下面每行勾选「使用默认」时，采用此处的字号 / 颜色 / 粗斜体 / 阴影）", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("（「备注」只是给配置者看的说明，代码按行下标绑定内容：0=名字 / 1=副标题 / 3=状态）", EditorStyles.miniLabel);

            while (s_rowFolds.Count < d.Count) s_rowFolds.Add(true);
            if (s_rowFolds.Count > d.Count) s_rowFolds.RemoveRange(d.Count, s_rowFolds.Count - d.Count);

            int removeIdx = -1;
            for (int i = 0; i < d.Count; i++)
            {
                // 折叠头：标题（含备注/内容预览便于辨识）占满左侧，右侧「显示」开关 + 删除按钮，不被标题挤掉
                using (new EditorGUILayout.HorizontalScope())
                {
                    string title = $"行{i}";
                    if (!string.IsNullOrEmpty(d.Names[i])) title += $"  {d.Names[i]}";
                    if (!string.IsNullOrEmpty(d.ContentPreview[i])) title += $"：{d.ContentPreview[i]}";
                    s_rowFolds[i] = EditorGUILayout.Foldout(s_rowFolds[i], title, true, EditorStyles.foldoutHeader);
                    d.Visible[i] = EditorGUILayout.ToggleLeft("显示", d.Visible[i], GUILayout.Width(50));
                    if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(22))) removeIdx = i;
                }
                if (!s_rowFolds[i]) continue;
                using (new EditorGUI.IndentLevelScope())
                {
                    // 行标识：备注（仅配置用说明）/ 内容（实际显示文字），内容不会很长，输入框限制宽度
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        d.Names[i] = LabeledTextField("备注", d.Names[i], 90f);
                        d.ContentPreview[i] = LabeledTextField("内容", d.ContentPreview[i], 160f);
                        GUILayout.FlexibleSpace();
                    }
                    // 位置：X/Y 偏移（标签可左右拖拽调值，同 Transform 的 X/Y/Z）等宽 + 右侧「水平居中」
                    // 勾选「水平居中」时预览/运行时都忽略 X 偏移，禁用 X 字段避免"改了没效果"的误解
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(d.CenterX[i]))
                            d.XOffset[i] = DragFloatField("X偏移", d.XOffset[i], 50f, 90f);
                        d.YOffset[i] = DragFloatField("Y偏移", d.YOffset[i], 50f, 90f);
                        d.CenterX[i] = EditorGUILayout.ToggleLeft("水平居中", d.CenterX[i], GUILayout.Width(76));
                    }
                    // 样式：勾选「使用默认」时该行字号/颜色取区块全局默认值，行内字段禁用
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(d.UseGlobalFontSize[i]))
                        {
                            d.FontSizes[i] = DragIntField("字号", d.FontSizes[i], 40f, 90f);
                            GUILayout.Label("颜色", GUILayout.Width(40));
                            d.TextColors[i] = EditorGUILayout.ColorField(GUIContent.none, d.TextColors[i],
                                GUILayout.MinWidth(48), GUILayout.ExpandWidth(true));
                        }
                        d.UseGlobalFontSize[i] = EditorGUILayout.ToggleLeft("使用默认", d.UseGlobalFontSize[i], GUILayout.Width(82));
                    }
                }
            }

            // 增删行：数据已直接改，置 GUI.changed 让外层 ChangeCheck 触发脏标记与重绘
            if (removeIdx >= 0)
            {
                d.RemoveRow(removeIdx);
                GUI.changed = true;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("＋ 添加行", GUILayout.Width(80)))
                {
                    d.AddRow();
                    GUI.changed = true;
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

        /// <summary>带小标签的 TextField：标签固定 40 宽，输入框固定 width（备注/内容类短文本用，不随窗口拉伸）。</summary>
        private static string LabeledTextField(string label, string value, float width)
        {
            float oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 40f;
            try
            {
                return EditorGUILayout.TextField(label, value ?? "", GUILayout.Width(width));
            }
            finally
            {
                EditorGUIUtility.labelWidth = oldLabelWidth;
            }
        }

        /// <summary>
        /// 紧凑布局用的带标签 FloatField：标签区域可按住左右拖拽调值（Unity 内置行为，
        /// 与 Inspector 中 Transform 的 X/Y/Z 一致）。labelWidth 控制标签宽，minWidth 为最小总宽，
        /// 字段在水平布局中弹性占满剩余空间。
        /// </summary>
        private static float DragFloatField(string label, float value, float labelWidth, float minWidth)
        {
            float oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = labelWidth;
            try
            {
                return EditorGUILayout.FloatField(label, value, GUILayout.MinWidth(minWidth));
            }
            finally
            {
                EditorGUIUtility.labelWidth = oldLabelWidth;
            }
        }

        /// <summary>同 <see cref="DragFloatField"/>，整数值版本（字号等）。</summary>
        private static int DragIntField(string label, int value, float labelWidth, float minWidth)
        {
            float oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = labelWidth;
            try
            {
                return EditorGUILayout.IntField(label, value, GUILayout.MinWidth(minWidth));
            }
            finally
            {
                EditorGUIUtility.labelWidth = oldLabelWidth;
            }
        }

        public static void Category(CategoryData d)
        {
            d.Category = EditorGUILayout.TextField("分类名", d.Category ?? "");
            EditorGUILayout.LabelField("常用: Terrain / Building / Special / Legacy", EditorStyles.miniLabel);
        }
    }
}
