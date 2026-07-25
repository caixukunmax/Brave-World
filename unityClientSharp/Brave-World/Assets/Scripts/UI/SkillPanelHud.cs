using System.Collections.Generic;
using TMPro;
using UnityClientSharp.Entity;
using UnityClientSharp.Map.Rendering;
using UnityClientSharp.Net;
using UnityEngine;
using UnityEngine.UI;

namespace UnityClientSharp.UI
{
    /// <summary>
    /// 技能面板（F4）— 移植自 Godot SkillPanel.cs：
    /// 三列（已装备 4 槽 / 已学习未装备 / 当前职业可学未学）+ 底部详情条；点名字选中刷新详情，
    /// "+" 装备到第一个空槽（395）、"-" 卸下（397，只填 slot_index）、"L" 学习（GM 后门 learnskill,id）。
    /// 普攻 id=1 在装备列强制显示空槽；响应后全量重建三列。
    /// 热键统一由 GamePanelManager 分发（对齐 Godot PanelManager.RegisterToggleKey(F4)）。
    /// </summary>
    public class SkillPanelHud : MonoBehaviour, IGamePanel
    {
        private const int MaxSlots = 4;

        private GameObject _panelRoot;
        private Transform _equippedContent;
        private Transform _learnedContent;
        private Transform _learnableContent;
        private TextMeshProUGUI _detail;
        private uint _selectedSkillId;

        // 测试/外部只读访问
        public Transform EquippedContent => _equippedContent;
        public Transform LearnedContent => _learnedContent;
        public Transform LearnableContent => _learnableContent;
        public TextMeshProUGUI Detail => _detail;
        public uint SelectedSkillId => _selectedSkillId;
        public bool PanelVisible => _panelRoot != null && _panelRoot.activeSelf;
        public string PanelName => "技能";

        public static SkillPanelHud Create()
        {
            var go = new GameObject("SkillPanelHud");
            var hud = go.AddComponent<SkillPanelHud>();
            hud.Build();
            return hud;
        }

        private void Build()
        {
            UiEventSystemUtil.EnsureExists();

            var canvasGo = new GameObject("SkillPanelCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasGo.AddComponent<GraphicRaycaster>();
            _panelRoot = canvasGo;

            var panelGo = CreateUI(canvasGo.transform, "Panel");
            var panelRt = (RectTransform)panelGo.transform;
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = new Vector2(560, 400);
            var bg = panelGo.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.15f, 0.95f);
            var outline = panelGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.5f, 0.9f);
            outline.effectDistance = new Vector2(2, -2);

            DraggablePanel.MakeDraggable(panelRt, "技能", new Vector2(460, 320), () => SetVisible(false));

            // 三列区（标题栏以下、详情条以上）
            var columnsGo = CreateUI(panelGo.transform, "Columns");
            var columnsRt = (RectTransform)columnsGo.transform;
            columnsRt.anchorMin = Vector2.zero;
            columnsRt.anchorMax = Vector2.one;
            columnsRt.offsetMin = new Vector2(10, 40);
            columnsRt.offsetMax = new Vector2(-10, -30);
            var hlg = columnsGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            _equippedContent = BuildColumn(columnsGo.transform, "已装备");
            _learnedContent = BuildColumn(columnsGo.transform, "已学习");
            _learnableContent = BuildColumn(columnsGo.transform, "可学习");

            // 底部详情条
            var detailGo = CreateUI(panelGo.transform, "Detail");
            var detailRt = (RectTransform)detailGo.transform;
            detailRt.anchorMin = new Vector2(0, 0);
            detailRt.anchorMax = new Vector2(1, 0);
            detailRt.pivot = new Vector2(0.5f, 0);
            detailRt.anchoredPosition = new Vector2(0, 8);
            detailRt.sizeDelta = new Vector2(-20, 28);
            _detail = detailGo.AddComponent<TextMeshProUGUI>();
            _detail.fontSize = 13;
            _detail.alignment = TextAlignmentOptions.Left;
            FontUtil.ApplyCjkFont(_detail);

            RefreshUI();
            SetVisible(false); // 默认隐藏，F4 唤出（对齐 Godot Visible=false）

            // 热键统一注册到 GamePanelManager（对齐 Godot SetToggleKey(Key.F4)）；无管理器时静默跳过
            GamePanelManager.Instance?.RegisterPanel(this);
            GamePanelManager.Instance?.RegisterHotkey(KeyCode.F4, this);
        }

        /// <summary>构建一列（标题 + 滚动列表），返回列表内容根。</summary>
        private Transform BuildColumn(Transform parent, string header)
        {
            var colGo = CreateUI(parent, "Col_" + header);
            var colLe = colGo.AddComponent<LayoutElement>();
            colLe.flexibleWidth = 1;
            var vlg = colGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            // 必须 true：controlSize=false 时 uGUI 取 child.sizeDelta（恒 0），
            // 列头/滚动区塌缩（同 GMPanel 布局事故根因）
            vlg.childControlHeight = true;

            var headerTmp = CreateText(colGo.transform, "Header", header, 14, new Color(1f, 0.9f, 0.6f));
            headerTmp.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;

            // 滚动区
            var scrollGo = CreateUI(colGo.transform, "Scroll");
            var scrollLe = scrollGo.AddComponent<LayoutElement>();
            scrollLe.flexibleHeight = 1;
            scrollLe.minHeight = 40;
            var scrollBg = scrollGo.AddComponent<Image>();
            scrollBg.color = new Color(0, 0, 0, 0.2f);
            scrollGo.AddComponent<RectMask2D>();
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;

            var contentGo = CreateUI(scrollGo.transform, "Content");
            var contentRt = (RectTransform)contentGo.transform;
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1);
            contentRt.anchoredPosition = Vector2.zero;
            var cvlg = contentGo.AddComponent<VerticalLayoutGroup>();
            cvlg.spacing = 2;
            cvlg.childForceExpandWidth = true;
            cvlg.childForceExpandHeight = false;
            cvlg.childControlWidth = true;
            // 必须 true：false 会让技能行高度塌缩为 0，ContentSizeFitter 读到的内容高恒为 0
            cvlg.childControlHeight = true;
            var csf = contentGo.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = contentRt;

            return contentGo.transform;
        }

        // ============ 刷新 ============

        public void RefreshUI()
        {
            RefreshEquipped();
            RefreshLearned();
            RefreshLearnable();
            RefreshDetail();
        }

        private void RefreshEquipped()
        {
            ClearChildren(_equippedContent);
            var nm = NetworkManager.Instance;
            var equipped = nm != null ? nm.CachedEquippedSkills : null;

            for (int i = 0; i < MaxSlots; i++)
            {
                uint raw = equipped != null && i < equipped.Count ? equipped[i] : 0;
                uint skillId = DisplaySkillId(raw); // 普攻 id=1 强制显示空槽（对齐 Godot）

                if (skillId > 0)
                {
                    int slotIndex = i;
                    AddSkillRow(_equippedContent, skillId, $"[{i}] {SkillDataUtil.GetName(skillId)}",
                        "-", () => SendUnequip(slotIndex));
                }
                else
                {
                    AddSkillRow(_equippedContent, 0, $"[{i}] （空）", null, null);
                }
            }
        }

        private void RefreshLearned()
        {
            ClearChildren(_learnedContent);
            var nm = NetworkManager.Instance;
            if (nm == null) return;

            var equippedSet = new HashSet<uint>(nm.CachedEquippedSkills);
            int count = 0;
            foreach (var id in nm.CachedLearnedSkills)
            {
                if (id <= 1 || equippedSet.Contains(id)) continue;
                uint captured = id;
                AddSkillRow(_learnedContent, id, SkillDataUtil.GetName(id),
                    "+", () => SendEquipToFirstEmpty(captured));
                count++;
            }
            if (count == 0) AddHintRow(_learnedContent, "（无）");
        }

        private void RefreshLearnable()
        {
            ClearChildren(_learnableContent);
            var nm = NetworkManager.Instance;
            if (nm == null) return;

            var learnedSet = new HashSet<uint>(nm.CachedLearnedSkills);
            var currentJob = nm.CachedRoleInfo != null ? nm.CachedRoleInfo.Job : "";
            int jobId = SkillDataUtil.JobNameToId(currentJob);
            var learnable = jobId > 0
                ? SkillDataUtil.GetLearnableIdsForJob(jobId)
                : SkillDataUtil.GetAllLearnableIds();

            int count = 0;
            foreach (var id in learnable)
            {
                if (learnedSet.Contains(id)) continue;
                uint captured = id;
                AddSkillRow(_learnableContent, id, SkillDataUtil.GetName(id),
                    "L", () => SendLearnSkill(captured));
                count++;
            }
            if (count == 0) AddHintRow(_learnableContent, "（已全部学会）");
        }

        private void RefreshDetail()
        {
            if (_selectedSkillId == 0)
            {
                _detail.text = "<color=#888888>选择一个技能查看详情</color>";
                return;
            }
            var data = SkillDataUtil.Get(_selectedSkillId);
            _detail.text =
                $"<color=#FFD700>{data.name}</color>  |  Range:{data.range}  Cast:{data.castTime:F1}s  CD:{data.cd:F1}s  MP:{data.mpCost}";
        }

        // ============ 行构建 ============

        /// <summary>加一行：图标 + 名字按钮（选中详情）+ 可选操作按钮（+/-/L）。</summary>
        private void AddSkillRow(Transform parent, uint skillId, string nameText, string actionLabel, System.Action onAction)
        {
            var rowGo = CreateUI(parent, "Row");
            rowGo.AddComponent<LayoutElement>().preferredHeight = 26;
            var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            // 图标 24×24（色块版；矢量 glyph 欠账见 SkillIconCatalog 注释）
            var iconGo = CreateUI(rowGo.transform, "Icon");
            iconGo.AddComponent<LayoutElement>().preferredWidth = 24;
            var icon = iconGo.AddComponent<Image>();
            if (skillId > 0)
            {
                icon.sprite = SkillIconCatalog.GetSprite(skillId);
                icon.color = Color.white;
            }
            else
            {
                icon.color = new Color(1, 1, 1, 0.1f);
            }
            icon.raycastTarget = false;

            // 名字按钮
            var nameGo = CreateUI(rowGo.transform, "Name");
            var nameLe = nameGo.AddComponent<LayoutElement>();
            nameLe.flexibleWidth = 1;
            var nameImg = nameGo.AddComponent<Image>();
            nameImg.color = skillId > 0 ? new Color(0.15f, 0.15f, 0.25f, 0.9f) : new Color(0.1f, 0.1f, 0.15f, 0.5f);
            var nameBtn = nameGo.AddComponent<Button>();
            var nameTmp = CreateText(nameGo.transform, "Text", nameText, 13,
                skillId > 0 ? Color.white : new Color(0.6f, 0.6f, 0.6f));
            Stretch((RectTransform)nameTmp.transform);
            if (skillId > 0)
            {
                uint captured = skillId;
                nameBtn.onClick.AddListener(() => SelectSkill(captured));
            }

            // 操作按钮（可空）
            if (actionLabel != null)
            {
                var actGo = CreateUI(rowGo.transform, "Action");
                actGo.AddComponent<LayoutElement>().preferredWidth = 30;
                var actImg = actGo.AddComponent<Image>();
                actImg.color = new Color(0.2f, 0.2f, 0.3f, 0.95f);
                var actBtn = actGo.AddComponent<Button>();
                actBtn.onClick.AddListener(() => onAction());
                var actTmp = CreateText(actGo.transform, "Text", actionLabel, 13, Color.white);
                Stretch((RectTransform)actTmp.transform);
            }
        }

        private void AddHintRow(Transform parent, string text)
        {
            var tmp = CreateText(parent, "Hint", text, 12, new Color(0.53f, 0.53f, 0.53f));
            tmp.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;
        }

        private void SelectSkill(uint skillId)
        {
            _selectedSkillId = skillId;
            RefreshDetail();
        }

        // ============ 发送 ============

        /// <summary>显示层技能 id：普攻 id=1 在装备列强制显示为空槽（对齐 Godot）。</summary>
        public static uint DisplaySkillId(uint raw) => raw == 1 ? 0u : raw;

        /// <summary>第一个空槽序号（无空槽返回 -1；对齐 Godot SendEquipToFirstEmpty 的扫描）。</summary>
        public static int FindFirstEmptySlot(IReadOnlyList<uint> equipped)
        {
            for (int i = 0; i < MaxSlots; i++)
            {
                uint id = equipped != null && i < equipped.Count ? equipped[i] : 0;
                if (id == 0) return i;
            }
            return -1;
        }

        private void SendEquipToFirstEmpty(uint skillId)
        {
            var nm = NetworkManager.Instance;
            if (nm == null || !nm.IsServerConnected()) return;
            int slotIndex = FindFirstEmptySlot(nm.CachedEquippedSkills);
            if (slotIndex < 0)
            {
                Debug.Log("[SkillPanel] 没有空槽位");
                return;
            }
            nm.SendPacket(Protocol.MessageId.GameEquipSkillReq, new Game.EquipSkillRequest
            {
                SkillId = skillId,
                SlotIndex = (uint)slotIndex,
            });
        }

        private void SendUnequip(int slotIndex)
        {
            var nm = NetworkManager.Instance;
            if (nm == null || !nm.IsServerConnected()) return;
            // 卸下只填 slot_index（对齐 Godot，服务器不看 skill_id）
            nm.SendPacket(Protocol.MessageId.GameUnequipSkillReq, new Game.UnequipSkillRequest
            {
                SlotIndex = (uint)slotIndex,
            });
        }

        private void SendLearnSkill(uint skillId)
        {
            var nm = NetworkManager.Instance;
            if (nm == null || !nm.IsServerConnected()) return;
            // 学习走 GM 后门（对齐 Godot，无正式学习协议）
            nm.SendPacket(Protocol.MessageId.GameGmReq, new Game.GmCommandRequest
            {
                Command = $"learnskill,{skillId}",
            });
        }

        // ============ 显隐 ============

        public void SetVisible(bool visible)
        {
            if (_panelRoot != null) _panelRoot.SetActive(visible);
            if (visible) RefreshUI();
        }

        public void Toggle() => SetVisible(!PanelVisible);

        // ============ 事件 ============

        private void Start()
        {
            var nm = NetworkManager.Instance;
            if (nm == null) return;
            nm.RoleAttrUpdated += OnRoleAttrUpdated;
            nm.ChangeJobResponse += OnChangeJobResponse;
            nm.EquipSkillResponse += OnEquipSkillResponse;
            nm.UnequipSkillResponse += OnUnequipSkillResponse;
            nm.GmResponse += OnGmResponse;
        }

        private void OnDestroy()
        {
            GamePanelManager.Instance?.UnregisterPanel(this);

            var nm = NetworkManager.Instance;
            if (nm == null) return;
            nm.RoleAttrUpdated -= OnRoleAttrUpdated;
            nm.ChangeJobResponse -= OnChangeJobResponse;
            nm.EquipSkillResponse -= OnEquipSkillResponse;
            nm.UnequipSkillResponse -= OnUnequipSkillResponse;
            nm.GmResponse -= OnGmResponse;
        }

        private void OnRoleAttrUpdated(Game.FullRoleInfo info) => RefreshUI();

        private void OnChangeJobResponse(Game.ChangeJobResponse rsp)
        {
            if (rsp.Code == Common.ErrorCode.Success) RefreshUI();
        }

        private void OnEquipSkillResponse(Game.EquipSkillResponse rsp)
        {
            if (rsp.Code != Common.ErrorCode.Success)
                Debug.Log($"[SkillPanel] 装备失败: {rsp.Message}");
            RefreshUI();
        }

        private void OnUnequipSkillResponse(Game.UnequipSkillResponse rsp)
        {
            if (rsp.Code != Common.ErrorCode.Success)
                Debug.Log($"[SkillPanel] 卸下失败: {rsp.Message}");
            RefreshUI();
        }

        private void OnGmResponse(Game.GmCommandResponse rsp) => RefreshUI();

        // ============ UI 工具 ============

        private static void ClearChildren(Transform parent)
        {
            foreach (Transform child in parent)
                Destroy(child.gameObject);
        }

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
