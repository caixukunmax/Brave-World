using TMPro;
using UnityClientSharp.Entity;
using UnityClientSharp.Map.Rendering;
using UnityEngine;
using UnityEngine.UI;

namespace UnityClientSharp.UI
{
    /// <summary>
    /// NPC 交互菜单 — 移植自 Godot NpcManager.ShowInteractMenu 的 UI 部分：
    /// 世界空间挂 NPC 子节点（随 NPC/相机移动），深蓝底蓝边，玩家在 NPC 右侧用 B 偏移（菜单在左）否则 A（在右）。
    /// 菜单项由 NpcManager 配置表驱动：对话弹窗 / 转职面板 / 挑战；点击后一律关菜单。
    /// </summary>
    public static class NpcInteractMenu
    {
        // 对齐 Godot EntityStyleConfig 默认值 A=(60,-20)/B=(-60,-20)（Godot y 向下 → 世界 y 取负）
        private static readonly Vector2 OffsetA = new(60, 20);
        private static readonly Vector2 OffsetB = new(-60, 20);

        /// <summary>在 NPC 旁构建交互菜单，返回菜单根 GameObject（调用方持有并销毁）。</summary>
        public static GameObject Show(NpcEntity npc, Vector2Int playerGridPos)
        {
            UiEventSystemUtil.EnsureExists();

            var root = new GameObject("NpcInteractMenu", typeof(RectTransform));
            root.transform.SetParent(npc.transform, false);
            bool useB = playerGridPos.x > npc.GridPos.x;
            var off = useB ? OffsetB : OffsetA;
            root.transform.localPosition = new Vector3(off.x, off.y, 0);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;
            root.AddComponent<GraphicRaycaster>();

            var panelGo = CreateUI(root.transform, "Panel");
            var panelRt = (RectTransform)panelGo.transform;
            panelRt.pivot = new Vector2(0, 1); // Godot Control Position 语义 = 左上角锚点
            panelRt.anchoredPosition = Vector2.zero;
            var bg = panelGo.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.2f, 0.9f);
            var outline = panelGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.5f, 0.9f);
            outline.effectDistance = new Vector2(2, -2);
            var layout = panelGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4;
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = panelGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateText(panelGo.transform, "Name", npc.NpcName, 14, new Color(0.3f, 0.7f, 1.0f))
                .alignment = TextAlignmentOptions.Left;

            var options = NpcManager.GetInteractOptions(npc.Type);
            if (options != null)
            {
                foreach (var opt in options)
                {
                    var captured = opt;
                    AddButton(panelGo.transform, captured.Label, () => OnOptionClicked(npc, captured));
                }
            }
            else
            {
                // 无配置的 NPC：只显示默认对话（对齐 Godot）
                AddButton(panelGo.transform, "对话", () =>
                {
                    ShowDialog(npc.NpcName, NpcManager.GetDefaultDialog(npc.Type));
                    NpcManager.Instance?.CloseInteractMenu();
                });
            }
            return root;
        }

        private static void OnOptionClicked(NpcEntity npc, NpcInteractOption opt)
        {
            if (!string.IsNullOrEmpty(opt.DialogText))
                ShowDialog(npc.NpcName, opt.DialogText);
            if (opt.ShowChangeJob)
                ChangeJobPanel.Show();
            if (opt.TriggerCombat)
                NpcManager.Instance?.TriggerNpcCombat(npc);
            NpcManager.Instance?.CloseInteractMenu();
        }

        private static void AddButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = CreateUI(parent, "Btn_" + label);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 24;
            le.minWidth = 72;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.25f, 0.95f);
            var ol = go.AddComponent<Outline>();
            ol.effectColor = new Color(0.3f, 0.5f, 0.9f, 0.5f);
            ol.effectDistance = new Vector2(1, -1);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);
            var tmp = CreateText(go.transform, "Text", label, 13, Color.white);
            Stretch((RectTransform)tmp.transform);
        }

        /// <summary>对话弹窗（屏幕居中，关闭即销毁）— 对齐 Godot AcceptDialog（标题=NPC 名，按钮"关闭"）。</summary>
        public static void ShowDialog(string npcName, string text)
        {
            UiEventSystemUtil.EnsureExists();

            var canvasGo = new GameObject("NpcDialog");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            var panelGo = CreateUI(canvasGo.transform, "Panel");
            var panelRt = (RectTransform)panelGo.transform;
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = new Vector2(320, 150);
            var bg = panelGo.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.15f, 0.95f);
            var outline = panelGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.5f, 0.9f);
            outline.effectDistance = new Vector2(2, -2);

            var title = CreateText(panelGo.transform, "Title", npcName, 14, new Color(0.3f, 0.7f, 1.0f));
            var titleRt = (RectTransform)title.transform;
            titleRt.anchorMin = new Vector2(0, 1);
            titleRt.anchorMax = new Vector2(1, 1);
            titleRt.pivot = new Vector2(0.5f, 1);
            titleRt.anchoredPosition = Vector2.zero;
            titleRt.sizeDelta = new Vector2(-16, 24);

            var body = CreateText(panelGo.transform, "Body", text, 13, Color.white);
            body.alignment = TextAlignmentOptions.TopLeft;
            body.enableWordWrapping = true;
            var bodyRt = (RectTransform)body.transform;
            bodyRt.anchorMin = Vector2.zero;
            bodyRt.anchorMax = Vector2.one;
            bodyRt.offsetMin = new Vector2(12, 40);
            bodyRt.offsetMax = new Vector2(-12, -28);

            var btnGo = CreateUI(panelGo.transform, "OkBtn");
            var btnRt = (RectTransform)btnGo.transform;
            btnRt.anchorMin = new Vector2(0.5f, 0);
            btnRt.anchorMax = new Vector2(0.5f, 0);
            btnRt.pivot = new Vector2(0.5f, 0);
            btnRt.anchoredPosition = new Vector2(0, 8);
            btnRt.sizeDelta = new Vector2(80, 26);
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.2f, 0.2f, 0.3f, 0.95f);
            var btn = btnGo.AddComponent<Button>();
            btn.onClick.AddListener(() => Object.Destroy(canvasGo));
            var btnTmp = CreateText(btnGo.transform, "Text", "关闭", 13, Color.white);
            Stretch((RectTransform)btnTmp.transform);
        }

        private static GameObject CreateUI(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string text, int fontSize, Color color)
        {
            var go = CreateUI(parent, name);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            FontUtil.ApplyCjkFont(tmp);
            return tmp;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
