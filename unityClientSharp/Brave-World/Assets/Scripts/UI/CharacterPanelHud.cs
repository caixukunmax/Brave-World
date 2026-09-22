using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityClientSharp.Map.Rendering;
using UnityClientSharp.Net;

namespace UnityClientSharp.UI
{
    /// <summary>
    /// 角色属性面板 — 移植自 Godot CharacterPanel(.Data/.Build/.Actions)。
    /// 每行 = 属性名 + 数值输入框（Godot SpinBox 的 uGUI 等价物 TMP_InputField）+ "应用"，
    /// 底部 "全部应用" / "刷新"；应用走 GM 后门 setattr,{gmName},{value}（对齐 Godot OnApplyAttr）。
    /// 属性定义表（key/标签/GM 名/上下限）逐行对齐 Godot AttrDefs。
    ///
    /// 数据源差异（等价且更直接）：Godot 读 Player 节点的 CombatAttrs（roleInfo.Attrs 的拷贝），
    /// Unity 直接读 NetworkManager.CachedRoleInfo.Attrs——同一份服务器数据，且打开面板时
    /// 立即用缓存渲染（AGENTS.md 第 22 条），并订阅 RoleAttrUpdated 实时刷新
    /// （Godot 同样订阅 RoleAttrUpdated → RefreshFromPlayer，行为一致）。
    /// Godot 端无热键（仅功能按钮栏入口），Unity 同样不配热键。
    /// 离线降级（AGENTS.md 第 18 条）：无缓存则保持空值，应用/全部应用静默不发送。
    /// </summary>
    public class CharacterPanelHud : MonoBehaviour, IGamePanel
    {
        // 属性定义（逐行对齐 Godot CharacterPanel.Data AttrDefs）
        public readonly struct AttrDefinition
        {
            public AttrDefinition(uint key, string label, string gmName, int minValue, int maxValue)
            {
                Key = key;
                Label = label;
                GmName = gmName;
                MinValue = minValue;
                MaxValue = maxValue;
            }

            public uint Key { get; }
            public string Label { get; }
            public string GmName { get; }
            public int MinValue { get; }
            public int MaxValue { get; }
        }

        public static readonly AttrDefinition[] AttrDefs =
        {
            new(1, "HP", "hp", 0, 99999),
            new(2, "MaxHP", "max_hp", 1, 99999),
            new(3, "MP", "mp", 0, 99999),
            new(4, "MaxMP", "max_mp", 1, 99999),
            new(6, "物攻", "patk", 0, 99999),
            new(7, "魔攻", "matk", 0, 99999),
            new(8, "物防", "pdef", 0, 99999),
            new(9, "魔防", "mdef", 0, 99999),
            new(10, "移速", "move_speed", 120, 600),
            new(11, "MP恢复/秒", "mp_regen", 0, 99999),
        };

        private const int RowHeight = 26;
        private const int TitleBarHeight = 24;

        private GameObject _panelRoot;
        private readonly Dictionary<uint, TMP_InputField> _valueInputs = new();

        public bool PanelVisible => _panelRoot != null && _panelRoot.activeSelf;
        public string PanelName => "属性";

        /// <summary>测试/外部只读访问。</summary>
        public IReadOnlyDictionary<uint, TMP_InputField> ValueInputs => _valueInputs;

        public static CharacterPanelHud Create()
        {
            var go = new GameObject("CharacterPanelHud");
            var hud = go.AddComponent<CharacterPanelHud>();
            hud.Build();
            return hud;
        }

        private void Build()
        {
            UiEventSystemUtil.EnsureExists();

            var canvasGo = new GameObject("CharacterPanelCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = GamePanelManager.BaseSortingOrder;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasGo.AddComponent<GraphicRaycaster>();
            _panelRoot = canvasGo;

            const float panelWidth = 320;
            // 标题 + 属性行 + 底部按钮行 + 边距
            float panelHeight = TitleBarHeight + 8 + AttrDefs.Length * (RowHeight + 4) + 8 + 30 + 10;

            var panelGo = CreateUI(canvasGo.transform, "Panel");
            var panelRt = (RectTransform)panelGo.transform;
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = new Vector2(panelWidth, panelHeight);
            var bg = panelGo.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.15f, 0.95f);
            var outline = panelGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.5f, 0.9f);
            outline.effectDistance = new Vector2(2, -2);

            DraggablePanel.MakeDraggable(panelRt, "角色属性", new Vector2(320, 200), () => SetVisible(false));

            // 内容区（标题栏以下）：属性行 + 底部按钮行
            var contentGo = CreateUI(panelGo.transform, "Content");
            var contentRt = (RectTransform)contentGo.transform;
            contentRt.anchorMin = Vector2.zero;
            contentRt.anchorMax = Vector2.one;
            contentRt.offsetMin = new Vector2(10, 10);
            contentRt.offsetMax = new Vector2(-10, -(TitleBarHeight + 6));
            var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            // 必须 true：controlSize=false 时 uGUI 取 child.sizeDelta（恒 0），属性行全部塌缩
            vlg.childControlHeight = true;

            foreach (var def in AttrDefs)
                BuildAttributeRow(contentGo.transform, def);
            BuildBottomActions(contentGo.transform);

            // 订阅先于使用；打开/收到属性推送时刷新（对齐 Godot RoleAttrUpdated → RefreshFromPlayer）
            var nm = NetworkManager.Instance;
            if (nm != null)
                nm.RoleAttrUpdated += OnRoleAttrUpdated;

            RefreshFromCache();
            SetVisible(false); // 对齐 Godot Visible=false

            // 无热键（对齐 Godot：CharacterPanel 没有 SetToggleKey），仅注册面板供按钮栏查询
            GamePanelManager.Instance?.RegisterPanel(this);
        }

        // ============ 行构建（对齐 Godot BuildAttributeRow/BuildBottomActions） ============

        private void BuildAttributeRow(Transform parent, AttrDefinition def)
        {
            var rowGo = CreateUI(parent, "Row_" + def.GmName);
            rowGo.AddComponent<LayoutElement>().preferredHeight = RowHeight;
            var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.childForceExpandWidth = false;
            // 禁止 childForceExpandHeight：HLG 会向父 VLG 报告 flexibleHeight=1 抢弹性余量
            // （GMPanel 第二轮事故同根因）；子元素用 LayoutElement 定高
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            var label = CreateText(rowGo.transform, "Label", def.Label, 13, Color.white);
            label.alignment = TextAlignmentOptions.Left;
            var labelLe = label.gameObject.AddComponent<LayoutElement>();
            labelLe.preferredWidth = 80;
            labelLe.preferredHeight = RowHeight;

            var input = CreateValueInput(rowGo.transform, def);
            var inputLe = input.gameObject.AddComponent<LayoutElement>();
            inputLe.preferredWidth = 100;
            inputLe.flexibleWidth = 1;
            inputLe.preferredHeight = RowHeight;
            _valueInputs[def.Key] = input;

            var applyBtn = CreateButton(rowGo.transform, "Apply", "应用", 50);
            applyBtn.gameObject.GetComponent<LayoutElement>().preferredHeight = RowHeight;
            applyBtn.onClick.AddListener(() => OnApplyAttr(def.GmName, ParseInput(def.Key)));
        }

        private void BuildBottomActions(Transform parent)
        {
            var rowGo = CreateUI(parent, "BottomActions");
            rowGo.AddComponent<LayoutElement>().preferredHeight = 30;
            var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.childForceExpandWidth = true;
            // 见 BuildAttributeRow 注释
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            var applyAllBtn = CreateButton(rowGo.transform, "ApplyAll", "全部应用", 0);
            applyAllBtn.gameObject.GetComponent<LayoutElement>().preferredHeight = 30;
            applyAllBtn.onClick.AddListener(OnApplyAll);

            var refreshBtn = CreateButton(rowGo.transform, "Refresh", "刷新", 60);
            refreshBtn.gameObject.GetComponent<LayoutElement>().preferredHeight = 30;
            refreshBtn.onClick.AddListener(RefreshFromCache);
        }

        /// <summary>数值输入框（Godot SpinBox 等价物；结构对齐 LoginPanel.CreateInput）。</summary>
        private TMP_InputField CreateValueInput(Transform parent, AttrDefinition def)
        {
            var go = CreateUI(parent, "Input");
            var img = go.AddComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.22f, 1f);

            var area = CreateUI(go.transform, "TextArea");
            var areaRt = (RectTransform)area.transform;
            areaRt.anchorMin = Vector2.zero;
            areaRt.anchorMax = Vector2.one;
            areaRt.offsetMin = new Vector2(6, 2);
            areaRt.offsetMax = new Vector2(-6, -2);
            area.AddComponent<RectMask2D>();

            var textGo = CreateUI(area.transform, "Text");
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 13;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.raycastTarget = false;
            // 单行输入框：关闭换行，防止 preferredHeight 被换行撑大泄漏给父布局组
            tmp.enableWordWrapping = false;
            FontUtil.ApplyCjkFont(tmp);
            Stretch((RectTransform)textGo.transform);

            var input = go.AddComponent<TMP_InputField>();
            input.targetGraphic = img;
            input.textViewport = areaRt;
            input.textComponent = tmp;
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.lineType = TMP_InputField.LineType.SingleLine;
            // 对齐 Godot SpinBox 的 MinValue/MaxValue：输入失焦时收拢到定义范围
            int min = def.MinValue, max = def.MaxValue;
            input.onEndEdit.AddListener(_ =>
            {
                int v = Mathf.Clamp(ParseInputText(input.text, min), min, max);
                input.text = v.ToString();
            });
            input.text = "0";
            return input;
        }

        private Button CreateButton(Transform parent, string name, string label, int preferredWidth)
        {
            var go = CreateUI(parent, name);
            // 始终加 LayoutElement（调用方可能继续设 preferredHeight）
            var le = go.AddComponent<LayoutElement>();
            if (preferredWidth > 0)
                le.preferredWidth = preferredWidth;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.3f, 0.95f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var tmp = CreateText(go.transform, "Text", label, 13, Color.white);
            Stretch((RectTransform)tmp.transform);
            return btn;
        }

        // ============ 数据与动作（对齐 Godot CharacterPanel.Actions） ============

        /// <summary>从网络缓存刷新所有属性值（对齐 Godot RefreshFromPlayer，数据源换为 CachedRoleInfo）。</summary>
        public void RefreshFromCache()
        {
            var nm = NetworkManager.Instance;
            var roleInfo = nm != null ? nm.CachedRoleInfo : null;
            if (roleInfo == null || roleInfo.Attrs.Count == 0)
                return; // 对齐 Godot: player == null || CombatAttrs.Count == 0 → return

            foreach (var def in AttrDefs)
            {
                if (_valueInputs.TryGetValue(def.Key, out var input) && TryGetAttr(roleInfo, def.Key, out var value))
                    input.text = value.ToString();
            }
        }

        private void OnApplyAttr(string gmName, int value)
        {
            var nm = NetworkManager.Instance;
            if (nm == null || !nm.IsServerConnected())
                return; // 离线静默降级（AGENTS.md 第 18 条）
            nm.SendPacket(Protocol.MessageId.GameGmReq, new Game.GmCommandRequest
            {
                Command = $"setattr,{gmName},{value}",
            });
        }

        private void OnApplyAll()
        {
            var nm = NetworkManager.Instance;
            if (nm == null || !nm.IsServerConnected())
                return;

            foreach (var def in AttrDefs)
            {
                if (!_valueInputs.ContainsKey(def.Key))
                    continue;
                nm.SendPacket(Protocol.MessageId.GameGmReq, new Game.GmCommandRequest
                {
                    Command = $"setattr,{def.GmName},{ParseInput(def.Key)}",
                });
            }
        }

        private void OnRoleAttrUpdated(Game.FullRoleInfo roleInfo) => RefreshFromCache();

        private int ParseInput(uint key)
        {
            return _valueInputs.TryGetValue(key, out var input) ? ParseInputText(input.text, 0) : 0;
        }

        private static int ParseInputText(string text, int fallback)
        {
            return int.TryParse(text, out var v) ? v : fallback;
        }

        /// <summary>从 FullRoleInfo.Attrs 取属性值（对齐 Godot CombatAttrs.TryGetValue）。</summary>
        public static bool TryGetAttr(Game.FullRoleInfo roleInfo, uint key, out int value)
        {
            value = 0;
            if (roleInfo == null) return false;
            foreach (var a in roleInfo.Attrs)
            {
                if (a.Key == key)
                {
                    value = a.Value;
                    return true;
                }
            }
            return false;
        }

        // ============ 显隐 ============

        public void SetVisible(bool visible)
        {
            if (_panelRoot != null) _panelRoot.SetActive(visible);
            if (visible)
                RefreshFromCache(); // 对齐 Godot NotifyFocusGained → RefreshFromPlayer
        }

        public void Toggle() => SetVisible(!PanelVisible);

        private void OnDestroy()
        {
            GamePanelManager.Instance?.UnregisterPanel(this);

            var nm = NetworkManager.Instance;
            if (nm != null)
                nm.RoleAttrUpdated -= OnRoleAttrUpdated;
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
            tmp.raycastTarget = false;
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
