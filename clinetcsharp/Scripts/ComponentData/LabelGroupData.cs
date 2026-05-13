using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// 标签组组件数据。
    /// 全局层负责共性字体样式；每个标签只在必要时覆盖局部字号与位置。
    /// </summary>
    public class LabelGroupData : IComponentData
    {
        public const int LabelCount = 4;

        public int DefaultFontSize = 0; // 0 = 自动
        public bool Bold = false;
        public bool Italic = false;
        public bool Shadow = false;

        public bool[] Visible = { true, true, true, true };
        public string[] Names = { "", "", "", "" };
        public string[] ContentPreview = { "", "", "", "" };
        public bool[] UseGlobalFontSize = { true, true, true, true };
        public int[] FontSizes = { 0, 0, 0, 0 };
        public float[] XOffset = { 0, 0, 0, 0 };
        public bool[] CenterX = { true, true, true, true };
        public float[] YOffset = { 0, 0, 0, 0 };

        // 服务端锁定标记
        public HashSet<string> LockedProperties = new();

        public bool IsContentLocked(int i) => LockedProperties.Contains($"content_{i}");
        public bool IsColorLocked(int i) => LockedProperties.Contains($"color_{i}");
        public void LockContent(int i) => LockedProperties.Add($"content_{i}");
        public void UnlockContent(int i) => LockedProperties.Remove($"content_{i}");
        public void LockColor(int i) => LockedProperties.Add($"color_{i}");
        public void UnlockColor(int i) => LockedProperties.Remove($"color_{i}");

        public IComponentData Clone()
        {
            return new LabelGroupData
            {
                DefaultFontSize = DefaultFontSize,
                Bold = Bold,
                Italic = Italic,
                Shadow = Shadow,
                Visible = (bool[])Visible.Clone(),
                Names = (string[])Names.Clone(),
                ContentPreview = (string[])ContentPreview.Clone(),
                UseGlobalFontSize = (bool[])UseGlobalFontSize.Clone(),
                FontSizes = (int[])FontSizes.Clone(),
                XOffset = (float[])XOffset.Clone(),
                CenterX = (bool[])CenterX.Clone(),
                YOffset = (float[])YOffset.Clone(),
                LockedProperties = new HashSet<string>(LockedProperties),
            };
        }
    }
}
