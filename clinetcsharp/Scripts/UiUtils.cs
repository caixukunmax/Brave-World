using Godot;
using Godot.Collections;

namespace ClinetCSharp
{
    /// <summary>
    /// UI 工具类 - 共用的 UI 检测方法
    /// </summary>
    public static class UiUtils
    {
        /// <summary>
        /// Godot.Collections.Dictionary 的 GetValueOrDefault 扩展方法
        /// </summary>
        public static Variant GetValueOrDefault(this Dictionary dict, string key, Variant defaultValue)
        {
            return dict.ContainsKey(key) ? dict[key] : defaultValue;
        }

        /// <summary>
        /// 检查控件是否为交互式 UI（应阻止游戏输入的控件类型）
        /// </summary>
        public static bool IsInteractiveControl(Control control)
        {
            if (control == null)
                return false;

            string[] interactiveTypes = new[]
            {
                "Slider", "HSlider", "VSlider", "SpinBox", "ProgressBar",
                "Button", "CheckButton", "CheckBox", "OptionButton", "MenuButton",
                "LineEdit", "TextEdit", "CodeEdit",
                "TabBar", "TabContainer", "ItemList", "Tree"
            };

            var className = control.GetClass();
            foreach (var type in interactiveTypes)
            {
                if (type == className || control.IsClass(type))
                    return true;
            }

            return false;
        }
    }
}
