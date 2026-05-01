namespace ClinetCSharp
{
    /// <summary>
    /// 调试面板全局精度常量。
    /// 所有比例/倍率/亮度/透明度类滑条的 Step 和显示格式均由此统一控制。
    /// 改一处即可全局生效。
    /// </summary>
    public static class DebugPanelLengthScalePolicy
    {
        /// <summary>比例类滑条的统一步长（支持到小数点后三位）</summary>
        public const double Step = 0.001;
        public const float StepF = 0.001f;

        /// <summary>比例类数值的格式化字符串（与 Step 精度一致）</summary>
        public const string FormatStr = "F3";

        public static string Format(double value) => DebugPanelSliderValueFormatter.Format(value, Step);

        public static bool ShouldPersistLengthKey(string key) => !ShouldRemoveDirectLengthKey(key);

        public static bool ShouldRemoveDirectLengthKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (key == "length") return true;
            if (key.EndsWith("_length") && !key.EndsWith("_scale")) return true;
            return false;
        }
    }
}
