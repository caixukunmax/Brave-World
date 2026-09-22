using System.Collections;
using System.Collections.Generic;
using Protocol;
using TMPro;
using UnityClientSharp.Entity;
using UnityClientSharp.Net;
using UnityEngine;
using UnityEngine.UI;

namespace UnityClientSharp.UI
{
    /// <summary>
    /// 技能栏 HUD — 移植自 Godot SkillBar.cs：
    /// 右下 4 槽（44px 图标、槽宽 56、间距 6、边距 20），单击（0.4s 双击判定）设/撤优先技能，
    /// 双击释放（CastRequest interrupt），CD 遮罩从下往上收缩 + 红字，高亮管理。
    /// </summary>
    public class SkillBarHud : MonoBehaviour
    {
        private const int MaxSlots = 4;
        private const int IconSize = 44;
        private const int SlotWidth = IconSize + 12;
        private const int SlotSpacing = 6;
        private const float DoubleClickWindow = 0.4f;

        private class Slot
        {
            public uint SkillId;
            public Image Icon;
            public Image CdMask;
            public TextMeshProUGUI CdText;
            public TextMeshProUGUI NameText;
            public GameObject Highlight;
            public Button Btn;
            public float RemainingCd, TotalCd;
        }

        private readonly List<Slot> _slots = new();
        private uint _highlightedSkillId;
        private Coroutine _clickTimer;
        private int _clickSlot = -1;

        public static SkillBarHud Create()
        {
            var go = new GameObject("SkillBarHud");
            var hud = go.AddComponent<SkillBarHud>();
            hud.Build();
            return hud;
        }

        private Canvas _canvas;
        public Canvas Canvas => _canvas;

        private void Build()
        {
            var canvasGo = new GameObject("SkillBarCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 70; // 对齐 Godot FunctionButtonBar 层级
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            var panel = CreateUI(canvasGo.transform, "Panel");
            var panelRt = (RectTransform)panel.transform;
            panelRt.anchorMin = Vector2.one;
            panelRt.anchorMax = Vector2.one;
            panelRt.pivot = Vector2.one;
            panelRt.anchoredPosition = new Vector2(-20, -20);
            panelRt.sizeDelta = new Vector2(MaxSlots * SlotWidth + (MaxSlots - 1) * SlotSpacing, 78);

            for (int i = 0; i < MaxSlots; i++)
                _slots.Add(BuildSlot(panel.transform, i));

            RefreshSlots();
        }

        private Slot BuildSlot(Transform parent, int index)
        {
            var root = CreateUI(parent, $"Slot{index}");
            var rt = (RectTransform)root.transform;
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(index * (SlotWidth + SlotSpacing), 0);
            rt.sizeDelta = new Vector2(SlotWidth, 78);
            var btn = root.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            int slotIndex = index;
            btn.onClick.AddListener(() => OnSlotClick(slotIndex));

            // 高亮框（整槽覆盖，默认隐藏）
            var hl = CreateUI(root.transform, "Highlight");
            var hlRt = (RectTransform)hl.transform;
            Fill(hlRt, Vector2.zero, Vector2.one, -2, -2);
            var hlImg = hl.AddComponent<Image>();
            hlImg.color = new Color(1f, 0.9f, 0.2f, 0.25f);
            hlImg.raycastTarget = false;
            hl.SetActive(false);

            // CD 文本行（顶部 16px）
            var cdText = CreateText(root.transform, "CdText", "", 11, new Color(1f, 0.6f, 0.6f));
            var cdTextRt = (RectTransform)cdText.transform;
            cdTextRt.anchorMin = new Vector2(0, 1);
            cdTextRt.anchorMax = new Vector2(1, 1);
            cdTextRt.pivot = new Vector2(0.5f, 1);
            cdTextRt.anchoredPosition = Vector2.zero;
            cdTextRt.sizeDelta = new Vector2(0, 16);

            // 图标框（44×44 居中偏上）
            var iconGo = CreateUI(root.transform, "Icon");
            var iconRt = (RectTransform)iconGo.transform;
            iconRt.anchorMin = new Vector2(0.5f, 1);
            iconRt.anchorMax = new Vector2(0.5f, 1);
            iconRt.pivot = new Vector2(0.5f, 1);
            iconRt.anchoredPosition = new Vector2(0, -16);
            iconRt.sizeDelta = new Vector2(IconSize, IconSize);
            var icon = iconGo.AddComponent<Image>();

            // CD 遮罩（黑色 0.55，盖图标上，从下往上收缩）
            var maskGo = CreateUI(iconGo.transform, "CdMask");
            var maskRt = (RectTransform)maskGo.transform;
            maskRt.anchorMin = new Vector2(0, 1);
            maskRt.anchorMax = new Vector2(1, 1);
            maskRt.pivot = new Vector2(0.5f, 1);
            maskRt.anchoredPosition = Vector2.zero;
            maskRt.sizeDelta = Vector2.zero;
            var mask = maskGo.AddComponent<Image>();
            mask.color = new Color(0, 0, 0, 0.55f);
            mask.raycastTarget = false;
            maskGo.SetActive(false);

            // 技能名（图标下方，10 号灰字）
            var nameText = CreateText(root.transform, "Name", "", 10, new Color(0.8f, 0.8f, 0.8f));
            var nameRt = (RectTransform)nameText.transform;
            nameRt.anchorMin = new Vector2(0, 0);
            nameRt.anchorMax = new Vector2(1, 0);
            nameRt.pivot = new Vector2(0.5f, 0);
            nameRt.anchoredPosition = Vector2.zero;
            nameRt.sizeDelta = new Vector2(0, 14);

            return new Slot { Icon = icon, CdMask = mask, CdText = cdText, NameText = nameText, Highlight = hl, Btn = btn };
        }

        // ============ 数据刷新 ============

        private void Start()
        {
            var nm = NetworkManager.Instance;
            if (nm == null) return;
            nm.RoleAttrUpdated += OnRoleAttrUpdated;
            nm.CombatStateNotify += OnCombatState;
            nm.CombatEndNotify += OnCombatEnd;
            nm.SetPreferredSkillResponse += OnSetPreferredSkillResponse;
            nm.CastResultNotify += OnCastResultNotify;
            nm.PlayerDeathNotify += OnPlayerDeathNotify;
            RefreshSlots();
        }

        private void OnDestroy()
        {
            var nm = NetworkManager.Instance;
            if (nm == null) return;
            nm.RoleAttrUpdated -= OnRoleAttrUpdated;
            nm.CombatStateNotify -= OnCombatState;
            nm.CombatEndNotify -= OnCombatEnd;
            nm.SetPreferredSkillResponse -= OnSetPreferredSkillResponse;
            nm.CastResultNotify -= OnCastResultNotify;
            nm.PlayerDeathNotify -= OnPlayerDeathNotify;
        }

        private void RefreshSlots()
        {
            var nm = NetworkManager.Instance;
            var equipped = nm != null ? nm.CachedEquippedSkills : new List<uint>();
            for (int i = 0; i < _slots.Count; i++)
            {
                uint skillId = i < equipped.Count ? equipped[i] : 0;
                var slot = _slots[i];
                slot.SkillId = skillId;
                if (skillId > 0)
                {
                    slot.Icon.sprite = SkillIconCatalog.GetSprite(skillId);
                    slot.Icon.color = Color.white;
                    slot.NameText.text = SkillDataUtil.GetName(skillId);
                    slot.NameText.color = new Color(0.8f, 0.8f, 0.8f);
                }
                else
                {
                    slot.Icon.sprite = null;
                    slot.Icon.color = new Color(1, 1, 1, 0.1f);
                    slot.NameText.text = "空";
                    slot.NameText.color = new Color(0.8f, 0.8f, 0.8f, 0.5f);
                }
            }
            RefreshHighlightVisual();
        }

        private void OnRoleAttrUpdated(Game.FullRoleInfo roleInfo) => RefreshSlots();

        private void OnCombatState(Game.CombatStateNotify notify)
        {
            var nm = NetworkManager.Instance;
            if (nm == null) return;
            Game.CombatStateNotify.Types.CombatUnit self = null;
            foreach (var unit in notify.Units)
            {
                if (unit.IsPlayer && unit.EntityId == (ulong)nm.AccountId)
                {
                    self = unit;
                    break;
                }
            }
            if (self == null)
            {
                // 找不到自己 → 清空全部 CD（对齐 Godot）
                foreach (var slot in _slots) SetSlotCd(slot, 0, 0);
                return;
            }
            // 按 skill_cds 更新（只推 remaining>0 的；未出现的技能清零）
            var cdMap = new Dictionary<uint, (float remaining, float total)>();
            foreach (var cd in self.SkillCds)
                cdMap[cd.SkillId] = (cd.RemainingCd, cd.TotalCd);
            foreach (var slot in _slots)
            {
                if (slot.SkillId > 0 && cdMap.TryGetValue(slot.SkillId, out var cd))
                    SetSlotCd(slot, cd.remaining, cd.total);
                else
                    SetSlotCd(slot, 0, 0);
            }
        }

        private void SetSlotCd(Slot slot, float remaining, float total)
        {
            slot.RemainingCd = remaining;
            slot.TotalCd = total;
            bool onCd = remaining > 0 && total > 0;
            slot.CdMask.gameObject.SetActive(onCd);
            if (onCd)
            {
                // 从下往上收缩：遮罩高 = IconSize × remaining/total，锚定图标顶部
                float h = IconSize * Mathf.Clamp01(remaining / total);
                slot.CdMask.rectTransform.sizeDelta = new Vector2(0, h);
                slot.CdText.text = remaining >= 1f ? $"{Mathf.CeilToInt(remaining)}s" : $"{remaining:F1}s";
            }
            else
            {
                slot.CdText.text = "";
            }
        }

        // ============ 点击交互 ============

        private void OnSlotClick(int index)
        {
            var slot = _slots[index];
            if (slot.SkillId == 0) return;

            // 0.4s 内第二次点击 = 双击
            if (_clickTimer != null && _clickSlot == index)
            {
                StopCoroutine(_clickTimer);
                _clickTimer = null;
                _clickSlot = -1;
                OnSlotDoubleClick(slot);
                return;
            }
            _clickSlot = index;
            _clickTimer = StartCoroutine(ClickTimerRoutine(slot));
        }

        private IEnumerator ClickTimerRoutine(Slot slot)
        {
            yield return new WaitForSecondsRealtime(DoubleClickWindow);
            _clickTimer = null;
            _clickSlot = -1;
            OnSlotSingleClick(slot);
        }

        /// <summary>单击：设/撤优先技能（已高亮发 0 取消）。</summary>
        private void OnSlotSingleClick(Slot slot)
        {
            var nm = NetworkManager.Instance;
            if (nm == null || !nm.IsServerConnected()) return;
            uint reqId = _highlightedSkillId == slot.SkillId ? 0u : slot.SkillId;
            nm.SendPacket(MessageId.GameSetPreferredSkillReq, new Game.SetPreferredSkillRequest { SkillId = reqId });
        }

        /// <summary>双击：直接释放（CD 中忽略）。</summary>
        private void OnSlotDoubleClick(Slot slot)
        {
            if (slot.RemainingCd > 0) return;
            var nm = NetworkManager.Instance;
            if (nm == null || !nm.IsServerConnected()) return;
            nm.SendPacket(MessageId.GameCastReq, new Game.CastRequest { SkillId = slot.SkillId, Interrupt = true });
        }

        // ============ 高亮管理 ============

        private void OnSetPreferredSkillResponse(Game.SetPreferredSkillResponse rsp)
        {
            if (rsp.Code != Common.ErrorCode.Success) return;
            _highlightedSkillId = rsp.PreferredSkillId;
            RefreshHighlightVisual();
        }

        private void OnCastResultNotify(Game.CastResultNotify notify)
        {
            // 释放的是当前高亮技能 → 取消高亮（对齐 Godot）
            var nm = NetworkManager.Instance;
            if (nm != null && notify.CasterId == (ulong)nm.AccountId && notify.SkillId == _highlightedSkillId)
            {
                _highlightedSkillId = 0;
                RefreshHighlightVisual();
            }
        }

        private void OnCombatEnd(Game.CombatEndNotify notify)
        {
            var nm = NetworkManager.Instance;
            if (nm == null) return;
            foreach (var id in notify.EntityIds)
            {
                if (id == (ulong)nm.AccountId)
                {
                    _highlightedSkillId = 0;
                    RefreshHighlightVisual();
                    return;
                }
            }
        }

        private void OnPlayerDeathNotify(Game.PlayerDeathNotify notify)
        {
            _highlightedSkillId = 0;
            RefreshHighlightVisual();
        }

        private void RefreshHighlightVisual()
        {
            foreach (var slot in _slots)
                slot.Highlight.SetActive(slot.SkillId > 0 && slot.SkillId == _highlightedSkillId);
        }

        // ============ UI 工具 ============

        private static GameObject CreateUI(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Fill(RectTransform rt, Vector2 min, Vector2 max, float dx, float dy)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = new Vector2(dx, dy);
            rt.offsetMax = new Vector2(-dx, -dy);
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string text, int fontSize, Color color)
        {
            var go = CreateUI(parent, name);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            Map.Rendering.FontUtil.ApplyCjkFont(tmp);
            return tmp;
        }
    }
}
