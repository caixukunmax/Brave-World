using TMPro;
using UnityEngine;
using UnityClientSharp.Map.Rendering;

namespace UnityClientSharp.DebugPanel
{
    /// <summary>
    /// DebugPanel 中文文本助手。
    /// 项目已有 <see cref="FontUtil"/>：运行时从 Resources/Fonts/NotoSansSC-VF.ttf
    /// 动态创建 CJK TMP FontAsset（避免中文方块），故无需预烘焙 SDF 资源。
    /// 建 UI 后对根节点递归应用即可。
    /// </summary>
    public static class DebugPanelFonts
    {
        /// <summary>为 root 下所有 TMP 文本（含未激活）应用中文动态字体。</summary>
        public static void ApplyCjkFontRecursive(Transform root)
        {
            if (root == null) return;
            var texts = root.GetComponentsInChildren<TMP_Text>(true);
            foreach (var t in texts) FontUtil.ApplyCjkFont(t);
        }
    }
}
