using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityClientSharp.Map.Rendering;

namespace UnityClientSharp.UI
{
    /// <summary>
    /// 功能按钮栏 — 移植自 Godot FunctionButtonBar.cs：屏幕左上角的横排功能按钮条。
    /// 样式对齐 Godot：半透明黑底圆角描边按钮、字号 12、间距 3、偏移 (8,8)、Canvas 层级 70。
    /// 按钮动作通过 <see cref="GamePanelManager"/> 查询并统一切换（对齐 Godot 端
    /// PanelManager.Instance?.GetDraggablePanel&lt;T&gt;()?.Toggle()，而非各自找面板实例）。
    ///
    /// 按钮集差异：Godot 端 8 个按钮（调试面板/GM面板/属性/实体列表/背包/技能/战斗日志/地图），
    /// Unity 侧只有已迁移的面板才配按钮（GM面板/属性/背包/技能，按 Godot 顺序）；调试面板是
    /// Editor 程序集 EditorWindow、实体列表/大地图尚未迁移、战斗日志是常驻 HUD 而非可切换面板——不摆死按钮。
    /// Godot 端的布局配置持久化（function_bar_config.cfg，由 DebugPanel 调整）未迁移，
    /// Unity 侧无对应调试页，布局固化为 Godot 默认值。
    /// </summary>
    public class FunctionButtonBarHud : MonoBehaviour
    {
        // 布局常量（对齐 Godot FunctionButtonBar 默认 Export 值）
        private const float OffsetX = 8;
        private const float OffsetY = 8;
        private const int ButtonSpacing = 3;
        private const int FontSize = 12;
        private const int ButtonHeight = 22;

        public static FunctionButtonBarHud Create()
        {
            var go = new GameObject("FunctionButtonBarHud");
            var hud = go.AddComponent<FunctionButtonBarHud>();
            hud.Build();
            return hud;
        }

        private void Build()
        {
            UiEventSystemUtil.EnsureExists();

            var canvasGo = new GameObject("FunctionBarCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70; // 对齐 Godot Layer = 70
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            var barGo = CreateUI(canvasGo.transform, "Bar");
            var barRt = (RectTransform)barGo.transform;
            barRt.anchorMin = new Vector2(0, 1);
            barRt.anchorMax = new Vector2(0, 1);
            barRt.pivot = new Vector2(0, 1);
            barRt.anchoredPosition = new Vector2(OffsetX, -OffsetY);
            var hlg = barGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = ButtonSpacing;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            var fitter = barGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 只给已迁移的面板配按钮（见类注释），按 Godot 按钮顺序
            AddButton(barGo.transform, "GM面板", () => TogglePanel<GMPanelHud>());
            AddButton(barGo.transform, "属性", () => TogglePanel<CharacterPanelHud>());
            AddButton(barGo.transform, "背包", () => TogglePanel<InventoryPanelHud>());
            AddButton(barGo.transform, "技能", () => TogglePanel<SkillPanelHud>());
        }

        /// <summary>经 GamePanelManager 统一切换（面板未创建时静默无操作，对齐 Godot ?.Toggle()）。</summary>
        private static void TogglePanel<T>() where T : class, IGamePanel
        {
            var mgr = GamePanelManager.Instance;
            if (mgr == null) return;
            var panel = mgr.GetPanel<T>();
            if (panel != null)
                mgr.TogglePanel(panel);
        }

        private void AddButton(Transform parent, string text, System.Action onPressed)
        {
            // 按钮外壳（对齐 Godot 的 PanelContainer 描边包装）
            var wrapper = CreateUI(parent, "Btn_" + text);
            int width = text.Length * (FontSize + 2) + 16;
            var le = wrapper.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = ButtonHeight;
            var bg = wrapper.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.05f, 0.65f);
            var border = wrapper.AddComponent<Outline>();
            border.effectColor = new Color(0.35f, 0.35f, 0.35f, 0.8f);
            border.effectDistance = new Vector2(1, -1);

            var btn = wrapper.AddComponent<Button>();
            btn.targetGraphic = bg;
            // 悬停提亮（对齐 Godot font_hover_color=白 / hover 底色）
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.6f, 1.6f, 1.6f, 1f);
            colors.pressedColor = new Color(0.8f, 1.2f, 0.8f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(() => onPressed());

            var tmpGo = CreateUI(wrapper.transform, "Text");
            var tmp = tmpGo.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = FontSize;
            tmp.color = new Color(0.85f, 0.85f, 0.85f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            FontUtil.ApplyCjkFont(tmp);
            Stretch((RectTransform)tmpGo.transform);
        }

        private static GameObject CreateUI(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            // 本引擎新建 RectTransform 的 sizeDelta 默认 (100,100)：清零防溢出（见 GMPanelHud）
            ((RectTransform)go.transform).sizeDelta = Vector2.zero;
            return go;
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
