using System.Collections.Generic;
using TMPro;
using UnityClientSharp.Map.Rendering;
using UnityClientSharp.Net;
using UnityEngine;
using UnityEngine.UI;

namespace UnityClientSharp.UI
{
    /// <summary>
    /// 转职面板 — 移植自 Godot ChangeJobPanel.cs：
    /// 三职业按钮（战士红/法师蓝/牧师绿），当前职业禁用高亮（取 NetworkManager.CachedRoleInfo.Job），
    /// 点击发 401（TargetJob=中文职业名），转职成功自动关闭，失败仅打日志（对齐 Godot，无错误提示 UI）。
    /// 单例；居中显示 + DraggablePanel 标题栏可拖。
    /// </summary>
    public class ChangeJobPanel : MonoBehaviour
    {
        private static readonly string[] Jobs = { "战士", "法师", "牧师" };
        private static readonly Color[] JobColors =
        {
            new Color(0.9f, 0.3f, 0.3f),  // 战士 - 红
            new Color(0.3f, 0.5f, 0.9f),  // 法师 - 蓝
            new Color(0.3f, 0.8f, 0.3f),  // 牧师 - 绿
        };

        private static ChangeJobPanel s_instance;

        private Transform _content;

        /// <summary>显示面板（单例；已存在则置顶并刷新当前职业）。</summary>
        public static ChangeJobPanel Show()
        {
            if (s_instance != null)
            {
                s_instance.gameObject.SetActive(true);
                s_instance.transform.SetAsLastSibling();
                s_instance.Refresh();
                return s_instance;
            }
            var go = new GameObject("ChangeJobPanel");
            s_instance = go.AddComponent<ChangeJobPanel>();
            s_instance.Build();
            return s_instance;
        }

        public static void Close()
        {
            if (s_instance != null) Destroy(s_instance.gameObject);
            s_instance = null;
        }

        private void Build()
        {
            UiEventSystemUtil.EnsureExists();

            var canvasGo = new GameObject("ChangeJobCanvas");
            canvasGo.transform.SetParent(transform, false);
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
            panelRt.sizeDelta = new Vector2(240, 236);
            var bg = panelGo.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.15f, 0.95f);
            var outline = panelGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.5f, 0.9f);
            outline.effectDistance = new Vector2(2, -2);

            DraggablePanel.MakeDraggable(panelRt, "转职", new Vector2(200, 180));

            // 内容区（标题栏以下）：当前职业 + 三职业按钮 + 关闭
            var contentGo = CreateUI(panelGo.transform, "Content");
            _content = contentGo.transform;
            var contentRt = (RectTransform)_content;
            contentRt.anchorMin = Vector2.zero;
            contentRt.anchorMax = Vector2.one;
            contentRt.offsetMin = new Vector2(12, 10);
            contentRt.offsetMax = new Vector2(-12, -30);
            var layout = contentGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Refresh();
        }

        /// <summary>重建内容（当前职业可能已变化）。</summary>
        public void Refresh()
        {
            foreach (Transform child in _content)
                Destroy(child.gameObject);

            var nm = NetworkManager.Instance;
            var currentJob = nm != null && nm.CachedRoleInfo != null ? nm.CachedRoleInfo.Job : "";

            var current = CreateText(_content, "Current", $"当前: {currentJob}", 12, new Color(0.7f, 0.7f, 0.7f));
            current.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;

            for (int i = 0; i < Jobs.Length; i++)
            {
                var job = Jobs[i];
                var color = JobColors[i];
                bool isCurrent = job == currentJob;
                var captured = job;
                AddJobButton(_content, job, color, isCurrent, () => OnJobClicked(captured));
            }

            var closeTmp = AddJobButton(_content, "关闭", new Color(0.8f, 0.8f, 0.8f), false, Close);
            closeTmp.fontSize = 12;
        }

        private TextMeshProUGUI AddJobButton(Transform parent, string label, Color color, bool isCurrent, UnityEngine.Events.UnityAction onClick)
        {
            var go = CreateUI(parent, "Btn_" + label);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 36;
            var img = go.AddComponent<Image>();
            var ol = go.AddComponent<Outline>();
            ol.effectDistance = new Vector2(1, -1);
            var btn = go.AddComponent<Button>();
            var tmp = CreateText(go.transform, "Text", label, 15, color);
            Stretch((RectTransform)tmp.transform);
            if (isCurrent)
            {
                // 当前职业：禁用 + 职业色描边高亮（对齐 Godot disabled 样式）
                btn.interactable = false;
                img.color = new Color(color.r, color.g, color.b, 0.3f);
                ol.effectColor = color;
            }
            else
            {
                img.color = new Color(0.15f, 0.15f, 0.25f, 0.9f);
                ol.effectColor = new Color(color.r, color.g, color.b, 0.5f);
                btn.onClick.AddListener(onClick);
            }
            return tmp;
        }

        private void Start()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.ChangeJobResponse += OnChangeJobResponse;
                NetworkManager.Instance.RoleAttrUpdated += OnRoleAttrUpdated;
            }
        }

        private void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.ChangeJobResponse -= OnChangeJobResponse;
                NetworkManager.Instance.RoleAttrUpdated -= OnRoleAttrUpdated;
            }
        }

        private void OnJobClicked(string targetJob)
        {
            var nm = NetworkManager.Instance;
            if (nm == null || !nm.IsServerConnected()) return;
            nm.SendPacket(Protocol.MessageId.GameChangeJobReq, new Game.ChangeJobRequest { TargetJob = targetJob });
        }

        private void OnChangeJobResponse(Game.ChangeJobResponse rsp)
        {
            if (rsp.Code == Common.ErrorCode.Success)
            {
                // 转职成功自动关闭（对齐 Godot）
                Close();
            }
            else
            {
                Debug.Log($"[ChangeJobPanel] 转职失败: {rsp.Message}");
            }
        }

        private void OnRoleAttrUpdated(Game.FullRoleInfo roleInfo) => Refresh();

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
