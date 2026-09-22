using TMPro;
using UnityClientSharp.Map.Rendering;
using UnityEngine;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// 标签阴影的统一实现：主标签背后一个黑色半透明副本，偏移 (1,-1) 世界单位、排序低 1 级。
    /// 与调试面板预览（+1,+1px 黑色 0.8 副本）效果一致；MapDecoration / EntityVisualBase 共用，
    /// 禁止各自实现阴影，否则预览/地图编辑器/游戏内显示不一致。
    /// 注意：阴影文本是独立副本，主标签文本变更（SetLabel）必须同步更新副本。
    /// </summary>
    public static class EntityLabelShadow
    {
        public static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.8f);

        /// <summary>为主标签创建阴影副本（同级兄弟节点），返回阴影 TMP 供调用方缓存同步文本。</summary>
        public static TextMeshPro Create(TextMeshPro main, string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(main.transform.parent, false);
            go.transform.localPosition = main.transform.localPosition + new Vector3(1f, -1f, 0f);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = main.text;
            tmp.isOrthographic = main.isOrthographic; // 跟随主标签的世界单位字号约定（FontUtil.SetWorldFontSize）
            tmp.fontSize = main.fontSize;
            tmp.alignment = main.alignment;
            tmp.fontStyle = main.fontStyle;
            tmp.enableWordWrapping = main.enableWordWrapping; // 跟随主标签，否则阴影换行与主标签错位
            tmp.color = ShadowColor;
            FontUtil.ApplyCjkFont(tmp);
            tmp.GetComponent<MeshRenderer>().sortingOrder = sortingOrder;
            return tmp;
        }
    }
}
