using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// 标签组组件数据 — 全局字体/样式 + 每行标签属性
    /// 合并原 TextStyle，含服务端权威/预览
    /// </summary>
    public class LabelGroupData : IComponentData
    {
        private const int LabelCount = 4;

        // 全局设置
        public string FontName = "";
        public int DefaultFontSize = 0;       // 0=自动
        public Color DefaultColor = Colors.Black;
        public bool Bold = false;
        public bool Italic = false;
        public bool Shadow = false;

        // 每行标签（索引 0~3）
        public bool[] Visible = { true, true, true, true };
        public string[] Names = { "", "", "", "" };
        public string[] ContentPreview = { "", "", "", "" };  // 预览值
        public int[] FontSizes = { 0, 0, 0, 0 };             // 0=跟随全局
        public Color[] ColorPreview =
        {
            Colors.Black, Colors.Black, Colors.Black, Colors.Black
        };
        public float[] XOffset = { 0, 0, 0, 0 };
        public bool[] CenterX = { true, true, true, true };
        public float[] YOffset = { 0, 0, 0, 0 };

        // 服务端权威标记（哪些属性被服务端锁定了）
        // 格式："content_0" 表示第0行内容被锁定，"color_1" 表示第1行颜色被锁定
        public HashSet<string> LockedProperties = new();

        public bool IsContentLocked(int i) => LockedProperties.Contains($"content_{i}");
        public bool IsColorLocked(int i) => LockedProperties.Contains($"color_{i}");
        public void LockContent(int i) => LockedProperties.Add($"content_{i}");
        public void UnlockContent(int i) => LockedProperties.Remove($"content_{i}");
        public void LockColor(int i) => LockedProperties.Add($"color_{i}");
        public void UnlockColor(int i) => LockedProperties.Remove($"color_{i}");

        public IComponentData Clone()
        {
            var clone = new LabelGroupData
            {
                FontName = FontName,
                DefaultFontSize = DefaultFontSize,
                DefaultColor = DefaultColor,
                Bold = Bold,
                Italic = Italic,
                Shadow = Shadow,
                Visible = (bool[])Visible.Clone(),
                Names = (string[])Names.Clone(),
                ContentPreview = (string[])ContentPreview.Clone(),
                FontSizes = (int[])FontSizes.Clone(),
                ColorPreview = (Color[])ColorPreview.Clone(),
                XOffset = (float[])XOffset.Clone(),
                CenterX = (bool[])CenterX.Clone(),
                YOffset = (float[])YOffset.Clone(),
                LockedProperties = new HashSet<string>(LockedProperties),
            };
            return clone;
        }
    }
}
