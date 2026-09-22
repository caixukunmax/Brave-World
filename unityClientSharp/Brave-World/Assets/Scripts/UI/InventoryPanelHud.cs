using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityClientSharp.Map.Rendering;

namespace UnityClientSharp.UI
{
    /// <summary>
    /// 背包面板（I 键）— 移植自 Godot InventoryUI.cs + InventoryUI.Build.cs + InventoryUI.Actions.cs。
    /// 文本流式背包：固定字符容量（默认 30x × 10 行 = 300 格），左键拖拽排序（服务器拒绝时回滚）、
    /// 右键菜单（使用 x1 / 丢弃 x1 / 丢弃全部）、底部容量计数按占比染色。
    /// 数据层在 <see cref="InventoryManager"/>；打开时若背包数据已在网络缓存则直接渲染缓存
    /// （AGENTS.md 第 21/22 条，由 InventoryManager 构造时重放）。
    /// 热键统一由 GamePanelManager 分发（对齐 Godot PanelManager.RegisterToggleKey(I)）。
    /// Godot 端 DebugPanel 运行时调参（BackpackPaddingH 等静态配置）在 Unity 侧无对应调试页，
    /// 固化为常量；尺寸换算公式（内容宽/字号=行宽，区域高/行高=行数）保持一致。
    /// </summary>
    public class InventoryPanelHud : MonoBehaviour, IGamePanel
    {
        // 布局常量（对齐 Godot InventoryUI 静态配置的默认值）
        public const int InventoryFontSize = 16;
        public const int InventoryLineSpacing = 8;
        public const int BackpackPaddingH = 8;
        public const int BackpackContentWidth = 480;
        public const int BackpackAreaHeight = 240;
        public const int BackpackItemSpacing = 8;

        private const int TitleBarHeight = 24;
        private const int CapacityLabelHeight = 16;

        private GameObject _panelRoot;
        private RectTransform _panelRt;
        private InventoryTextContainer _container;
        private TextMeshProUGUI _capacityLabel;
        private InventoryManager _manager;
        private GameObject _contextMenu;

        public bool PanelVisible => _panelRoot != null && _panelRoot.activeSelf;
        public string PanelName => "背包";
        public InventoryManager Manager => _manager;
        public InventoryTextContainer Container => _container;

        public static InventoryPanelHud Create()
        {
            var go = new GameObject("InventoryPanelHud");
            var hud = go.AddComponent<InventoryPanelHud>();
            hud.Build();
            return hud;
        }

        private void Build()
        {
            UiEventSystemUtil.EnsureExists();

            var canvasGo = new GameObject("InventoryPanelCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasGo.AddComponent<GraphicRaycaster>();
            _panelRoot = canvasGo;

            // 尺寸换算（对齐 Godot BuildContent）：行宽 = 内容宽 / 字号，行数 = 区域高 / 行高
            int lineWidth = Mathf.Max(1, BackpackContentWidth / InventoryFontSize);
            float contentWidth = lineWidth * InventoryFontSize;
            int lineHeight = InventoryFontSize + InventoryLineSpacing;
            int lineCount = Mathf.Max(1, Mathf.RoundToInt(BackpackAreaHeight / (float)lineHeight));
            float containerHeight = lineCount * lineHeight;

            float panelWidth = contentWidth + BackpackPaddingH * 2;
            float panelHeight = TitleBarHeight + 8 + containerHeight + 4 + CapacityLabelHeight + 8;

            var panelGo = CreateUI(canvasGo.transform, "Panel");
            _panelRt = (RectTransform)panelGo.transform;
            _panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            _panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            _panelRt.pivot = new Vector2(0.5f, 0.5f);
            _panelRt.anchoredPosition = Vector2.zero; // 对齐 Godot 首次 CenterOnViewport
            _panelRt.sizeDelta = new Vector2(panelWidth, panelHeight);
            var bg = panelGo.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.15f, 0.95f);
            var outline = panelGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.5f, 0.9f);
            outline.effectDistance = new Vector2(2, -2);

            // Godot 端锁定水平缩放（LockHorizontalResize=true）；DraggablePanel 拉伸柄无方向锁，
            // 拉伸仅影响底板，背包区尺寸固定，故给出最小尺寸即可
            DraggablePanel.MakeDraggable(_panelRt, "背包", new Vector2(240, 120), () => SetVisible(false));

            // 背包区（左右留 BackpackPaddingH 边距，对齐 Godot MarginContainer）
            var areaGo = CreateUI(panelGo.transform, "InventoryArea");
            var areaRt = (RectTransform)areaGo.transform;
            areaRt.anchorMin = new Vector2(0, 1);
            areaRt.anchorMax = new Vector2(0, 1);
            areaRt.pivot = new Vector2(0, 1);
            areaRt.anchoredPosition = new Vector2(BackpackPaddingH, -(TitleBarHeight + 8));
            areaRt.sizeDelta = new Vector2(contentWidth, containerHeight);
            _container = areaGo.AddComponent<InventoryTextContainer>();
            _container.LineWidth = lineWidth;
            _container.LineCount = lineCount;
            _container.FontSize = InventoryFontSize;
            _container.LineSpacing = InventoryLineSpacing;
            _container.ItemSpacing = BackpackItemSpacing;
            _container.ItemRightClicked += OnItemRightClicked;
            _container.ReorderRequested += OnReorderRequested;

            // 容量标签：内容下方右对齐（对齐 Godot BuildContent 的 capacityLabel）
            var capGo = CreateUI(panelGo.transform, "Capacity");
            var capRt = (RectTransform)capGo.transform;
            capRt.anchorMin = new Vector2(0, 0);
            capRt.anchorMax = new Vector2(1, 0);
            capRt.pivot = new Vector2(0.5f, 0);
            capRt.anchoredPosition = new Vector2(0, 6);
            capRt.sizeDelta = new Vector2(-BackpackPaddingH * 2, CapacityLabelHeight);
            _capacityLabel = capGo.AddComponent<TextMeshProUGUI>();
            _capacityLabel.fontSize = 12;
            _capacityLabel.alignment = TextAlignmentOptions.Right;
            _capacityLabel.color = new Color(0.7f, 0.7f, 0.7f);
            _capacityLabel.raycastTarget = false;
            FontUtil.ApplyCjkFont(_capacityLabel);

            // 数据层（构造即订阅网络事件 + 重放缓存）
            _manager = new InventoryManager();
            _manager.InventoryChanged += OnInventoryChanged;

            RefreshInventory();
            SetVisible(false); // 默认隐藏，I 唤出（对齐 Godot SetToggleKey(Key.I)）

            // 热键统一注册到 GamePanelManager（对齐 Godot SetToggleKey(Key.I)）；无管理器时静默跳过
            GamePanelManager.Instance?.RegisterPanel(this);
            GamePanelManager.Instance?.RegisterHotkey(KeyCode.I, this);
        }

        // ============ 刷新 ============

        private void OnInventoryChanged() => RefreshInventory();

        private void RefreshInventory()
        {
            if (_container == null)
                return;

            var entries = new List<TextInventoryLayout.ItemEntry>();
            if (_manager != null)
            {
                foreach (var slot in _manager.Items)
                {
                    entries.Add(new TextInventoryLayout.ItemEntry
                    {
                        ItemId = slot.ItemId,
                        Name = slot.Name,
                        Count = slot.Count,
                        Quality = _manager.GetItemQuality(slot.ItemId),
                    });
                }
            }

            _container.SetItems(entries);
            UpdateCapacityLabel();
        }

        /// <summary>容量计数染色（对齐 Godot UpdateCapacityLabel：满=红，≥80%=橙，其余灰）。</summary>
        private void UpdateCapacityLabel()
        {
            var (used, total) = _container.GetCapacity();
            _capacityLabel.text = $"{used} / {total}";

            if (used >= total)
                _capacityLabel.color = new Color(1f, 0.4f, 0.4f);
            else if (used >= total * 0.8f)
                _capacityLabel.color = new Color(1f, 0.7f, 0.2f);
            else
                _capacityLabel.color = new Color(0.7f, 0.7f, 0.7f);
        }

        // ============ 交互 ============

        private void OnReorderRequested(List<uint> orderedItemIds)
        {
            _manager?.SendReorderItems(orderedItemIds);
        }

        /// <summary>右键菜单（对齐 Godot OnItemRightClicked 的 PopupMenu：使用 x1 / 丢弃 x1 / 丢弃全部）。</summary>
        private void OnItemRightClicked(uint itemId, uint count)
        {
            CloseContextMenu();

            // 全屏透明挡板：点击菜单外任意处关闭（对齐 Godot PopupHide → QueueFree）
            var backdrop = CreateUI(_panelRoot.transform, "ContextBackdrop");
            var backdropRt = (RectTransform)backdrop.transform;
            backdropRt.anchorMin = Vector2.zero;
            backdropRt.anchorMax = Vector2.one;
            backdropRt.offsetMin = Vector2.zero;
            backdropRt.offsetMax = Vector2.zero;
            var backdropImg = backdrop.AddComponent<Image>();
            backdropImg.color = new Color(0f, 0f, 0f, 0f);
            backdropImg.raycastTarget = true;
            var backdropBtn = backdrop.AddComponent<Button>();
            backdropBtn.onClick.AddListener(CloseContextMenu);
            _contextMenu = backdrop;

            var menuGo = CreateUI(backdrop.transform, "Menu");
            var menuRt = (RectTransform)menuGo.transform;
            menuRt.pivot = new Vector2(0, 1);
            var menuBg = menuGo.AddComponent<Image>();
            menuBg.color = new Color(0.12f, 0.12f, 0.18f, 0.98f);
            var menuOutline = menuGo.AddComponent<Outline>();
            menuOutline.effectColor = new Color(0.4f, 0.4f, 0.5f);
            menuOutline.effectDistance = new Vector2(1, -1);
            var vlg = menuGo.AddComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = true;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            AddMenuItem(menuGo.transform, "使用 x1", () => _manager?.SendUseItem(itemId, 1));
            AddMenuItem(menuGo.transform, "丢弃 x1", () => _manager?.SendDropItem(itemId, 1));
            AddMenuItem(menuGo.transform, "丢弃全部", () => _manager?.SendDropItem(itemId, count));

            menuRt.sizeDelta = new Vector2(120, 3 * 26 + 4);

            // 菜单放在鼠标位置（对齐 Godot popup.Position = 鼠标处），并收拢到屏幕内
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)_panelRoot.transform, Input.mousePosition, null, out var local);
            var canvasRect = ((RectTransform)_panelRoot.transform).rect;
            local.x = Mathf.Clamp(local.x, canvasRect.xMin, canvasRect.xMax - menuRt.sizeDelta.x);
            local.y = Mathf.Clamp(local.y, canvasRect.yMin + menuRt.sizeDelta.y, canvasRect.yMax);
            menuRt.anchoredPosition = local;
        }

        private void AddMenuItem(Transform parent, string label, System.Action onClick)
        {
            var itemGo = CreateUI(parent, "Item_" + label);
            var img = itemGo.AddComponent<Image>();
            img.color = new Color(0.18f, 0.18f, 0.26f, 0.95f);
            var btn = itemGo.AddComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                onClick();
                CloseContextMenu(); // 对齐 Godot IdPressed → 执行后 QueueFree
            });
            var tmp = CreateText(itemGo.transform, "Text", label, 13, Color.white);
            Stretch((RectTransform)tmp.transform);
        }

        private void CloseContextMenu()
        {
            if (_contextMenu != null)
            {
                Destroy(_contextMenu);
                _contextMenu = null;
            }
        }

        // ============ 显隐 ============

        public void SetVisible(bool visible)
        {
            if (_panelRoot != null) _panelRoot.SetActive(visible);
            if (visible)
                RefreshInventory(); // 对齐 Godot NotifyFocusGained → RefreshInventory
            else
                CloseContextMenu();
        }

        public void Toggle() => SetVisible(!PanelVisible);

        private void OnDestroy()
        {
            GamePanelManager.Instance?.UnregisterPanel(this);

            if (_manager != null)
            {
                _manager.InventoryChanged -= OnInventoryChanged;
                _manager.Dispose();
                _manager = null;
            }
        }

        // ============ UI 工具 ============

        private static GameObject CreateUI(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            // 本引擎新建 RectTransform 的 sizeDelta 默认 (100,100)：清零防溢出（见 GMPanelHud）
            ((RectTransform)go.transform).sizeDelta = Vector2.zero;
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
