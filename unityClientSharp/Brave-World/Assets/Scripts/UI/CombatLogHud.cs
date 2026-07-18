using System.Collections.Generic;
using TMPro;
using UnityClientSharp.Net;
using UnityEngine;
using UnityEngine.UI;

namespace UnityClientSharp.UI
{
    /// <summary>
    /// 战斗日志面板 — 移植自 Godot IntegratedPanel 的日志部分（出生在左下，标题栏可拖动、右下角可拉伸）。
    /// 行格式 [color]hh:mm:ss {服务器格式化整句 extra}[/color]，上限 200 行裁剪，颜色按 logType。
    /// </summary>
    public class CombatLogHud : MonoBehaviour
    {
        private const int MaxLogLines = 200;

        private readonly Queue<string> _lines = new();
        private TextMeshProUGUI _text;
        private bool _dirty;

        public static CombatLogHud Create()
        {
            var go = new GameObject("CombatLogHud");
            var hud = go.AddComponent<CombatLogHud>();
            hud.Build();
            return hud;
        }

        private void Build()
        {
            var canvasGo = new GameObject("CombatLogCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            var panel = CreateUI(canvasGo.transform, "Panel");
            var panelRt = (RectTransform)panel.transform;
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.zero;
            panelRt.pivot = Vector2.zero;
            panelRt.anchoredPosition = new Vector2(20, 20);
            panelRt.sizeDelta = new Vector2(400, 220);
            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.08f, 0.75f);
            var outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(0.35f, 0.35f, 0.35f, 0.8f);
            outline.effectDistance = new Vector2(1, -1);

            // 标题栏拖动移动 + 右下角拉伸（最小 200×100）
            DraggablePanel.MakeDraggable(panelRt, "战斗日志", new Vector2(200, 100));

            var textGo = CreateUI(panel.transform, "Text");
            var textRt = (RectTransform)textGo.transform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(8, 6);
            textRt.offsetMax = new Vector2(-8, -28); // 顶部留标题栏
            _text = textGo.AddComponent<TextMeshProUGUI>();
            _text.fontSize = 12;
            _text.color = Color.white;
            _text.alignment = TextAlignmentOptions.TopLeft;
            _text.enableWordWrapping = true;
            _text.overflowMode = TextOverflowModes.Ellipsis;
            Map.Rendering.FontUtil.ApplyCjkFont(_text);
        }

        private void Start()
        {
            if (NetworkManager.Instance != null)
                NetworkManager.Instance.CombatLogNotify += OnCombatLog;
        }

        private void OnDestroy()
        {
            if (NetworkManager.Instance != null)
                NetworkManager.Instance.CombatLogNotify -= OnCombatLog;
        }

        private void OnCombatLog(Game.CombatLogNotify notify)
        {
            foreach (var entry in notify.Entries)
            {
                string time = System.DateTimeOffset.FromUnixTimeSeconds((long)entry.Timestamp)
                    .LocalDateTime.ToString("HH:mm:ss");
                string color = LogTypeColor(entry.LogType);
                // 服务器已用 CombatLogFormatter 把整句放进 extra（对齐 Godot 显示）
                _lines.Enqueue($"<color={color}>{time} {entry.Extra}</color>");
                while (_lines.Count > MaxLogLines)
                    _lines.Dequeue();
                _dirty = true;
            }
        }

        private void LateUpdate()
        {
            if (!_dirty) return;
            _dirty = false;
            _text.text = string.Join("\n", _lines);
        }

        /// <summary>日志类型颜色（对齐 Godot IntegratedPanel）。</summary>
        public static string LogTypeColor(Game.CombatLogType logType) => logType switch
        {
            Game.CombatLogType.CombatLogStart => "#FFD700",   // 开始-金
            Game.CombatLogType.CombatLogSkill => "#FFFFFF",   // 技能-白
            Game.CombatLogType.CombatLogDamage => "#FFA500",  // 伤害-橙
            Game.CombatLogType.CombatLogHeal => "#00FF00",    // 治疗-绿
            Game.CombatLogType.CombatLogBuff => "#FF00FF",    // Buff-紫
            Game.CombatLogType.CombatLogDodge => "#AAAAAA",   // 闪避-灰
            Game.CombatLogType.CombatLogDeath => "#FF0000",   // 死亡-红
            Game.CombatLogType.CombatLogEnd => "#AAAAAA",     // 结束-灰
            _ => "#FFFFFF",
        };

        private static GameObject CreateUI(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }
    }
}
