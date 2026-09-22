using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnityClientSharp.Map.Rendering
{
    /// <summary>
    /// 小地图 HUD — 移植自 Godot MinimapHud.cs：
    /// 右上角 240×180 面板（底 (0.05,0.05,0.05,0.85)、灰边框）、标题栏（地图名 + 折叠按钮"−"）、
    /// 折叠态 32×32"地"按钮；内嵌 MapViewControl（地形 + 标记 + 相机框）。
    /// </summary>
    public class MinimapHud : MonoBehaviour
    {
        private MapViewControl _mapView;
        private GameObject _panel;
        private GameObject _expandButton;
        private TextMeshProUGUI _title;
        private GridManager _gm;

        public static MinimapHud Create(GridManager gm)
        {
            var go = new GameObject("MinimapHud");
            var hud = go.AddComponent<MinimapHud>();
            hud.Build(gm);
            return hud;
        }

        private void Build(GridManager gm)
        {
            _gm = gm;

            // Canvas（对齐 Godot CanvasLayer Layer=75）
            var canvasGo = new GameObject("MinimapCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 75;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            // 主面板（右上 240×180，偏移 16,16）
            _panel = CreateUI(canvasGo.transform, "Panel");
            var panelRt = (RectTransform)_panel.transform;
            AnchorTopRight(panelRt, new Vector2(-16, -16), new Vector2(240, 180));
            var panelImg = _panel.AddComponent<Image>();
            panelImg.color = new Color(0.05f, 0.05f, 0.05f, 0.85f);
            // 灰色边框（Outline 组件近似 1px 描边）
            var outline = _panel.AddComponent<Outline>();
            outline.effectColor = new Color(0.35f, 0.35f, 0.35f, 0.8f);
            outline.effectDistance = new Vector2(1, -1);

            // 标题栏（24px 高）
            var titleBar = CreateUI(_panel.transform, "TitleBar");
            var titleBarRt = (RectTransform)titleBar.transform;
            titleBarRt.anchorMin = new Vector2(0, 1);
            titleBarRt.anchorMax = new Vector2(1, 1);
            titleBarRt.pivot = new Vector2(0.5f, 1);
            titleBarRt.anchoredPosition = Vector2.zero;
            titleBarRt.sizeDelta = new Vector2(0, 24);

            _title = CreateText(titleBar.transform, "Title", "", 12, new Color(1, 0.9f, 0.6f));
            var titleRt = (RectTransform)_title.transform;
            titleRt.anchorMin = new Vector2(0, 0);
            titleRt.anchorMax = new Vector2(1, 1);
            titleRt.offsetMin = new Vector2(8, 0);
            titleRt.offsetMax = new Vector2(-28, 0);
            _title.alignment = TextAlignmentOptions.Left;

            // 折叠按钮 "−"（24×24 右上）
            var collapseBtn = CreateButton(titleBar.transform, "Collapse", "−", 13);
            var collapseRt = (RectTransform)collapseBtn.transform;
            collapseRt.anchorMin = new Vector2(1, 0.5f);
            collapseRt.anchorMax = new Vector2(1, 0.5f);
            collapseRt.pivot = new Vector2(1, 0.5f);
            collapseRt.anchoredPosition = new Vector2(-2, 0);
            collapseRt.sizeDelta = new Vector2(24, 24);
            collapseBtn.onClick.AddListener(Collapse);

            // 地图视图区域（标题栏以下）
            var mapGo = CreateUI(_panel.transform, "MapView");
            var mapRt = (RectTransform)mapGo.transform;
            mapRt.anchorMin = Vector2.zero;
            mapRt.anchorMax = Vector2.one;
            mapRt.offsetMin = new Vector2(2, 2);
            mapRt.offsetMax = new Vector2(-2, -26);
            mapGo.AddComponent<RawImage>();
            _mapView = mapGo.AddComponent<MapViewControl>();
            _mapView.TargetImage = mapGo.GetComponent<RawImage>();
            _mapView.ShowCameraFrame = true;
            _mapView.Setup(gm);

            // 展开按钮 "地"（32×32，折叠时显示）
            _expandButton = CreateUI(canvasGo.transform, "Expand");
            var expandRt = (RectTransform)_expandButton.transform;
            AnchorTopRight(expandRt, new Vector2(-16, -16), new Vector2(32, 32));
            _expandButton.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.85f);
            var expandBtn = CreateButton(_expandButton.transform, "Btn", "地", 13);
            var expandBtnRt = (RectTransform)expandBtn.transform;
            expandBtnRt.anchorMin = Vector2.zero;
            expandBtnRt.anchorMax = Vector2.one;
            expandBtnRt.offsetMin = Vector2.zero;
            expandBtnRt.offsetMax = Vector2.zero;
            expandBtn.onClick.AddListener(Expand);
            _expandButton.SetActive(false);
        }

        private void Update()
        {
            // 标题显示地图文件夹名（对齐 Godot：非 display_name）
            if (_title != null && _gm != null && _title.text != _gm.CurrentMapName)
                _title.text = _gm.CurrentMapName;
        }

        private void Collapse()
        {
            _panel.SetActive(false);
            _expandButton.SetActive(true);
        }

        private void Expand()
        {
            _panel.SetActive(true);
            _expandButton.SetActive(false);
        }

        private static void AnchorTopRight(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = Vector2.one;
            rt.anchorMax = Vector2.one;
            rt.pivot = Vector2.one;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
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

        private static Button CreateButton(Transform parent, string name, string label, int fontSize)
        {
            var go = CreateUI(parent, name);
            go.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            var btn = go.AddComponent<Button>();
            var tmp = CreateText(go.transform, "Label", label, fontSize, Color.white);
            var rt = (RectTransform)tmp.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return btn;
        }
    }
}
