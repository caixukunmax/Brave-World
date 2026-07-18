using UnityEngine;
using UnityEngine.UI;
using UnityClientSharp.Entity;
using UnityClientSharp.UI;

namespace UnityClientSharp.DebugPanel
{
    /// <summary>
    /// DebugPanel 运行时 shell（UGUI 浮窗，移植自 Godot DebugPanel.cs）。
    /// - DraggablePanel 载体 + 标题栏（可拖拽/拉伸/关闭）
    /// - 顶部 Tab 栏切换 5 个 Tab
    /// - 内容区 ScrollRect，承载各 Tab 的纵向布局
    /// - 配置读写：逻辑层 EntityProfileManager（SaveConfig 立即落盘 / ScheduleSave 防抖 1s）
    /// - 中文：递归应用 FontUtil（动态 CJK TMP）
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class DebugPanel : MonoBehaviour
    {
        public static DebugPanel Instance { get; private set; }

        private RectTransform _panel;
        private RectTransform _scrollContent;
        private DebugPanelTab[] _tabs;
        private int _activeTab = -1;
        private float _saveTimer = -1;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            BuildUI();
        }

        /// <summary>属性微调防抖保存（拖 slider 时调用）。</summary>
        public void ScheduleSave() => _saveTimer = 1f;

        /// <summary>结构性变更立即落盘（组件增删/启停、Profile 增删）。</summary>
        public void SaveNow() => EntityProfileManager.SaveConfig();

        private void Update()
        {
            if (_saveTimer > 0)
            {
                _saveTimer -= Time.deltaTime;
                if (_saveTimer <= 0)
                {
                    _saveTimer = -1;
                    EntityProfileManager.SaveConfig();
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void BuildUI()
        {
            EntityProfileManager.EnsureInitialized();
            UiEventSystemUtil.EnsureExists();

            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                var cgo = new GameObject("DebugPanelCanvas", typeof(Canvas));
                canvas = cgo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1000;
                cgo.AddComponent<CanvasScaler>();
                cgo.AddComponent<GraphicRaycaster>();
            }

            _panel = new GameObject("DebugPanel", typeof(RectTransform)).transform as RectTransform;
            _panel.SetParent(canvas.transform, false);
            _panel.anchorMin = new Vector2(0, 1);
            _panel.anchorMax = new Vector2(0, 1);
            _panel.pivot = new Vector2(0, 1);
            _panel.anchoredPosition = new Vector2(20, -20);
            _panel.sizeDelta = new Vector2(440, 600);
            var panelImg = _panel.gameObject.AddComponent<Image>();
            panelImg.color = new Color(0.1f, 0.1f, 0.14f, 0.94f);

            DraggablePanel.MakeDraggable(_panel, "Debug 调试面板", new Vector2(320, 240),
                () => _panel.gameObject.SetActive(false));

            // Tab 栏（标题栏下方）
            var tabBar = new GameObject("TabBar", typeof(RectTransform));
            tabBar.transform.SetParent(_panel, false);
            var tbg = tabBar.AddComponent<HorizontalLayoutGroup>();
            tbg.spacing = 2;
            tbg.childControlWidth = true;
            tbg.childControlHeight = false;
            tbg.childForceExpandWidth = true;
            tbg.childForceExpandHeight = false;
            var tbgRt = (RectTransform)tabBar.transform;
            tbgRt.anchorMin = new Vector2(0, 1);
            tbgRt.anchorMax = new Vector2(1, 1);
            tbgRt.pivot = new Vector2(0.5f, 1);
            tbgRt.anchoredPosition = new Vector2(0, -28);
            tbgRt.sizeDelta = new Vector2(0, 24);

            // 滚动视图
            var scroll = new GameObject("Scroll", typeof(RectTransform));
            scroll.transform.SetParent(_panel, false);
            var srt = (RectTransform)scroll.transform;
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = new Vector2(4, 4);
            srt.offsetMax = new Vector2(-4, -56);
            var scrollRect = scroll.AddComponent<ScrollRect>();
            scroll.AddComponent<Image>().color = new Color(0, 0, 0, 0.2f);
            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(scroll.transform, false);
            var vpRt = (RectTransform)viewport.transform;
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = vpRt.offsetMax = Vector2.zero;
            viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0.15f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            scrollRect.viewport = vpRt;
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var cvRt = (RectTransform)content.transform;
            cvRt.anchorMin = new Vector2(0, 1);
            cvRt.anchorMax = new Vector2(1, 1);
            cvRt.pivot = new Vector2(0.5f, 1);
            cvRt.anchoredPosition = Vector2.zero;
            cvRt.sizeDelta = new Vector2(0, 0);
            var cvlg = content.AddComponent<VerticalLayoutGroup>();
            cvlg.spacing = 4;
            cvlg.padding = new RectOffset(4, 4, 4, 4);
            cvlg.childControlWidth = true;
            cvlg.childControlHeight = false;
            cvlg.childForceExpandWidth = true;
            cvlg.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = cvRt;
            _scrollContent = cvRt;

            _tabs = new DebugPanelTab[]
            {
                new DebugPanelMapTab(this),
                new DebugPanelEntityTab(this),
                new DebugPanelSystemTab(this),
                new DebugPanelUITab(this),
                new DebugPanelDecorationTab(this),
            };
            foreach (var t in _tabs)
            {
                t.Build(_scrollContent);
                t.Root.gameObject.SetActive(false);
            }
            for (int i = 0; i < _tabs.Length; i++)
            {
                int idx = i;
                DebugPanelUI.AddButton(tbgRt, _tabs[i].TabName, () => SelectTab(idx));
            }

            SelectTab(0);
            DebugPanelFonts.ApplyCjkFontRecursive(_panel);
        }

        private void SelectTab(int idx)
        {
            if (_activeTab >= 0 && _activeTab < _tabs.Length) _tabs[_activeTab].OnHide();
            _activeTab = idx;
            for (int i = 0; i < _tabs.Length; i++)
                _tabs[i].Root.gameObject.SetActive(i == idx);
            _tabs[idx].OnShow();
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("BraveWorld/DebugPanel/打开调试面板")]
        public static void OpenFromMenu()
        {
            if (Instance != null) { Instance._panel.gameObject.SetActive(true); return; }
            var go = new GameObject("DebugPanelRoot");
            go.AddComponent<DebugPanel>();
        }
#endif
    }
}
