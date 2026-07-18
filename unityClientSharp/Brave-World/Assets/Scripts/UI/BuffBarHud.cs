using System.Collections.Generic;
using TMPro;
using UnityClientSharp.Net;
using UnityEngine;
using UnityEngine.UI;

namespace UnityClientSharp.UI
{
    /// <summary>
    /// Buff 栏 HUD — 移植自 Godot BuffBar.cs：
    /// 技能栏左侧（右下锚定，预留 234px 技能栏宽），≤10 格 36px 间距 4，
    /// buffId→纯色块，层数黄字 xN，剩余时间白字（≥10s 整数/<10s 一位小数，<0=永久不显示），
    /// 本地每秒倒计时，归零变半透明占位等服务器清除。
    /// </summary>
    public class BuffBarHud : MonoBehaviour
    {
        private const int MaxBuffs = 10;
        private const int IconSize = 36;
        private const int Spacing = 4;
        private const int SkillBarWidth = 234; // 对齐 Godot 硬编码偏移

        private class BuffSlot
        {
            public Image Icon;
            public TextMeshProUGUI StacksText;
            public TextMeshProUGUI TimeText;
            public float Remaining = -1f; // <0 = 永久
            public bool Occupied;
        }

        private readonly List<BuffSlot> _slots = new();
        private float _tickTimer;

        public static BuffBarHud Create(Canvas parentCanvas)
        {
            var go = new GameObject("BuffBarHud");
            var hud = go.AddComponent<BuffBarHud>();
            hud.Build(parentCanvas);
            return hud;
        }

        private void Build(Canvas parentCanvas)
        {
            Transform parent = parentCanvas != null ? parentCanvas.transform : CreateOwnCanvas();

            var panel = CreateUI(parent, "BuffBar");
            var panelRt = (RectTransform)panel.transform;
            panelRt.anchorMin = Vector2.one;
            panelRt.anchorMax = Vector2.one;
            panelRt.pivot = Vector2.one;
            // 技能栏左侧：右下锚定，右偏 234+20，下偏 20
            panelRt.anchoredPosition = new Vector2(-(SkillBarWidth + 20), -20);
            panelRt.sizeDelta = new Vector2(MaxBuffs * (IconSize + Spacing), IconSize + 4);

            for (int i = 0; i < MaxBuffs; i++)
                _slots.Add(BuildSlot(panel.transform, i));
        }

        private Transform CreateOwnCanvas()
        {
            var canvasGo = new GameObject("BuffBarCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasGo.AddComponent<GraphicRaycaster>();
            return canvasGo.transform;
        }

        private BuffSlot BuildSlot(Transform parent, int index)
        {
            var root = CreateUI(parent, $"Buff{index}");
            var rt = (RectTransform)root.transform;
            rt.anchorMin = new Vector2(1, 0.5f);
            rt.anchorMax = new Vector2(1, 0.5f);
            rt.pivot = new Vector2(1, 0.5f);
            // 右对齐排列
            rt.anchoredPosition = new Vector2(-index * (IconSize + Spacing), 0);
            rt.sizeDelta = new Vector2(IconSize, IconSize);
            root.SetActive(false);

            var icon = root.AddComponent<Image>();
            var stacks = CreateText(root.transform, "Stacks", "", 11, Color.yellow);
            var stacksRt = (RectTransform)stacks.transform;
            stacksRt.anchorMin = new Vector2(0, 1);
            stacksRt.anchorMax = new Vector2(0, 1);
            stacksRt.pivot = new Vector2(0, 1);
            stacksRt.anchoredPosition = new Vector2(1, -1);
            stacksRt.sizeDelta = new Vector2(18, 12);
            stacks.alignment = TMPro.TextAlignmentOptions.Left;

            var time = CreateText(root.transform, "Time", "", 10, Color.white);
            var timeRt = (RectTransform)time.transform;
            timeRt.anchorMin = new Vector2(1, 0);
            timeRt.anchorMax = new Vector2(1, 0);
            timeRt.pivot = new Vector2(1, 0);
            timeRt.anchoredPosition = new Vector2(-1, 1);
            timeRt.sizeDelta = new Vector2(24, 12);
            time.alignment = TMPro.TextAlignmentOptions.Right;

            return new BuffSlot { Icon = icon, StacksText = stacks, TimeText = time };
        }

        // ============ 数据刷新 ============

        private void Start()
        {
            var nm = NetworkManager.Instance;
            if (nm == null) return;
            nm.BuffUpdateNotify += OnBuffUpdate;
            nm.CombatStateNotify += OnCombatState;
        }

        private void OnDestroy()
        {
            var nm = NetworkManager.Instance;
            if (nm == null) return;
            nm.BuffUpdateNotify -= OnBuffUpdate;
            nm.CombatStateNotify -= OnCombatState;
        }

        /// <summary>非战斗 buff 全量替换（对齐 Godot BuffBar）。</summary>
        private void OnBuffUpdate(Game.BuffUpdateNotify notify)
        {
            var nm = NetworkManager.Instance;
            if (nm == null || notify.EntityId != (ulong)nm.AccountId) return;
            // BuffUpdateNotify.BuffEntry 与 CombatStateNotify.BuffInfo 字段同构，映射后共用刷新
            var buffs = new List<BuffView>(notify.Buffs.Count);
            foreach (var b in notify.Buffs)
                buffs.Add(new BuffView(b.BuffId, b.Stacks, b.RemainingTime, b.ShieldAmount));
            RefreshSlots(buffs);
        }

        /// <summary>战斗中走 CombatState 自己 unit 的 buffs；空 CombatState 不清空（等 BuffUpdateNotify）。</summary>
        private void OnCombatState(Game.CombatStateNotify notify)
        {
            if (notify.Units.Count == 0) return; // 对齐 Godot：空 notify 不清空
            var nm = NetworkManager.Instance;
            if (nm == null) return;
            foreach (var unit in notify.Units)
            {
                if (unit.IsPlayer && unit.EntityId == (ulong)nm.AccountId)
                {
                    var buffs = new List<BuffView>(unit.Buffs.Count);
                    foreach (var b in unit.Buffs)
                        buffs.Add(new BuffView(b.BuffId, b.Stacks, b.RemainingTime, b.ShieldAmount));
                    RefreshSlots(buffs);
                    return;
                }
            }
        }

        private readonly struct BuffView
        {
            public readonly int BuffId, Stacks, ShieldAmount;
            public readonly float RemainingTime;
            public BuffView(int buffId, int stacks, float remainingTime, int shieldAmount)
            {
                BuffId = buffId;
                Stacks = stacks;
                RemainingTime = remainingTime;
                ShieldAmount = shieldAmount;
            }
        }

        private void RefreshSlots(IReadOnlyList<BuffView> buffs)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (i < buffs.Count)
                {
                    var b = buffs[i];
                    slot.Occupied = true;
                    slot.Icon.gameObject.SetActive(true);
                    slot.Icon.color = b.ShieldAmount > 0 ? new Color(0.3f, 0.6f, 1f) : GetBuffColor(b.BuffId);
                    slot.StacksText.text = b.Stacks > 1 ? $"x{b.Stacks}" : "";
                    slot.Remaining = b.RemainingTime;
                    UpdateTimeText(slot);
                }
                else
                {
                    slot.Occupied = false;
                    slot.Icon.gameObject.SetActive(false);
                    slot.StacksText.text = "";
                    slot.TimeText.text = "";
                    slot.Remaining = -1f;
                }
            }
        }

        private void UpdateTimeText(BuffSlot slot)
        {
            if (!slot.Occupied || slot.Remaining < 0)
            {
                slot.TimeText.text = "";
                return;
            }
            slot.TimeText.text = slot.Remaining >= 10f
                ? $"{Mathf.FloorToInt(slot.Remaining)}s"
                : $"{slot.Remaining:F1}s";
        }

        /// <summary>本地每秒倒计时（对齐 Godot _Process 倒计时）。</summary>
        private void Update()
        {
            _tickTimer += Time.deltaTime;
            if (_tickTimer < 1f) return;
            _tickTimer -= 1f;

            foreach (var slot in _slots)
            {
                if (!slot.Occupied || slot.Remaining < 0) continue;
                slot.Remaining -= 1f;
                if (slot.Remaining <= 0)
                {
                    // 归零先变透明占位，等服务器正式清除（对齐 Godot）
                    slot.Remaining = 0;
                    slot.Icon.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
                }
                UpdateTimeText(slot);
            }
        }

        /// <summary>buffId → 纯色块（对齐 Godot GetBuffColor；shield>0 强制蓝）。</summary>
        public static Color GetBuffColor(int buffId) => buffId switch
        {
            1 => new Color(0.6f, 0.2f, 0.8f),   // 中毒-紫
            2 => new Color(0.3f, 0.6f, 1f),     // 冰冻-冰蓝
            6 => new Color(0.4f, 0.7f, 0.9f),   // 减速-浅蓝
            7 => new Color(1f, 0.4f, 0.1f),     // 灼烧-橙红
            9 => new Color(0.7f, 0.3f, 0.3f),   // 破甲-暗红
            10 => new Color(0.9f, 0.9f, 0.1f),  // 眩晕-黄
            3 => new Color(1f, 0.8f, 0.2f),     // 战吼-金
            4 => new Color(0.3f, 0.6f, 1f),     // 护盾-蓝
            5 => new Color(0.6f, 0.5f, 0.3f),   // 石肤-棕
            8 => new Color(0.9f, 0.9f, 0.4f),   // 祝福-浅金
            11 => new Color(1f, 0.3f, 0.1f),    // 狂暴-深红
            _ => new Color(0.5f, 0.5f, 0.5f),   // 未知-灰
        };

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
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            Map.Rendering.FontUtil.ApplyCjkFont(tmp);
            return tmp;
        }
    }
}
