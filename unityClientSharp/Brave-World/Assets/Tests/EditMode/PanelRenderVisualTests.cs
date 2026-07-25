using System.IO;
using System.Text;
using NUnit.Framework;
using UnityClientSharp.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BraveWorld.Tests
{
    /// <summary>
    /// 面板布局渲染验证（GMPanel 布局事故第二轮回归）。
    /// 实例化面板 → 强制布局 → 打印全部关键 RectTransform 尺寸（排查塌缩）→
    /// ScreenSpaceCamera + cam.Render() 同步渲到 RenderTexture → PNG 落盘 %TEMP% 供人工审查。
    /// 参考 PlayMode FontRenderVisualTests 的做法；batchmode 无帧循环，全部同步调用。
    /// 断言只覆盖"渲染出了非背景像素"，布局正确性以 PNG 人工审查 + GMPanelLayoutTests 的数值断言为准。
    /// </summary>
    public class PanelRenderVisualTests
    {
        [Test]
        public void GMPanel_RenderToPng()
        {
            // 探针：确认本引擎新建 RectTransform 的默认 sizeDelta（504=404+100 溢出根因假设）
            var probe = new GameObject("probe", typeof(RectTransform));
            Debug.Log($"[Probe] fresh RectTransform sizeDelta = {((RectTransform)probe.transform).sizeDelta}");
            Object.DestroyImmediate(probe);

            var hud = GMPanelHud.Create();
            try
            {
                hud.transform.Find("GMPanelCanvas").gameObject.SetActive(true);
                Canvas.ForceUpdateCanvases();
                DumpRects(hud, "GMPanel");
                DumpLayoutDetail(hud, "GMPanel",
                    "Content", "CommandBar", "CmdInput", "TextArea", "Placeholder", "Exec",
                    "ResponseLog", "GroupScroll", "GroupContainer", "Header_道具", "Name",
                    "FlowRow", "Cmd_药水x1", "Collapse");
                int lit = RenderPanelToPng(hud, "gm_panel_render", 500, 560);
                Assert.Greater(lit, 2000, "面板几乎没渲染出内容像素");
            }
            finally
            {
                Object.DestroyImmediate(hud.gameObject);
            }
        }

        [Test]
        public void CharacterPanel_RenderToPng()
        {
            var hud = CharacterPanelHud.Create();
            try
            {
                hud.transform.Find("CharacterPanelCanvas").gameObject.SetActive(true);
                Canvas.ForceUpdateCanvases();
                DumpRects(hud, "CharacterPanel");
                int lit = RenderPanelToPng(hud, "character_panel_render", 400, 460);
                Assert.Greater(lit, 2000, "面板几乎没渲染出内容像素");
            }
            finally
            {
                Object.DestroyImmediate(hud.gameObject);
            }
        }

        [Test]
        public void SkillPanel_RenderToPng()
        {
            var hud = SkillPanelHud.Create();
            try
            {
                hud.transform.Find("SkillPanelCanvas").gameObject.SetActive(true);
                Canvas.ForceUpdateCanvases();
                DumpRects(hud, "SkillPanel");
                int lit = RenderPanelToPng(hud, "skill_panel_render", 620, 460);
                Assert.Greater(lit, 2000, "面板几乎没渲染出内容像素");
            }
            finally
            {
                Object.DestroyImmediate(hud.gameObject);
            }
        }

        // ============ 工具 ============

        /// <summary>打印所有 RectTransform 的路径与宽高（塌缩排查用，输出进测试日志）。</summary>
        private static void DumpRects(Component root, string tag)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[RectDump] {tag}:");
            foreach (var rt in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (!rt.gameObject.activeInHierarchy) continue;
                var r = rt.rect;
                sb.AppendLine($"  {GetPath(rt)}  {r.width:F0}x{r.height:F0}");
            }
            Debug.Log(sb.ToString());
        }

        /// <summary>对指定节点打印锚点/sizeDelta 与布局 min/preferred（定位"谁写大了宽度"）。</summary>
        private static void DumpLayoutDetail(Component root, string tag, params string[] names)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[LayoutDetail] {tag}:");
            foreach (var rt in root.GetComponentsInChildren<RectTransform>(true))
            {
                bool hit = false;
                foreach (var n in names)
                    if (rt.name == n) { hit = true; break; }
                if (!hit) continue;
                sb.AppendLine($"  {GetPath(rt)}: anchors=({rt.anchorMin.x:F2},{rt.anchorMin.y:F2})-({rt.anchorMax.x:F2},{rt.anchorMax.y:F2})" +
                    $" sizeDelta=({rt.sizeDelta.x:F0},{rt.sizeDelta.y:F0}) pos=({rt.anchoredPosition.x:F0},{rt.anchoredPosition.y:F0})" +
                    $" minW={LayoutUtility.GetMinWidth(rt):F1} prefW={LayoutUtility.GetPreferredWidth(rt):F1} flexW={LayoutUtility.GetFlexibleWidth(rt):F1}" +
                    $" minH={LayoutUtility.GetMinHeight(rt):F1} prefH={LayoutUtility.GetPreferredHeight(rt):F1} flexH={LayoutUtility.GetFlexibleHeight(rt):F1}");
            }
            Debug.Log(sb.ToString());
        }

        private static string GetPath(RectTransform rt)
        {
            string path = rt.name;
            var p = rt.parent;
            while (p != null && p.parent != null)
            {
                path = p.name + "/" + path;
                p = p.parent;
            }
            return path;
        }

        /// <summary>2x 渲染到 %TEMP%/&lt;name&gt;.png，返回明显亮于背景的像素数。</summary>
        private static int RenderPanelToPng(Component hud, string name, int logicalW, int logicalH)
        {
            UiEventSystemUtil.EnsureExists();

            var camGo = new GameObject("RenderTestCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
            cam.orthographic = true;

            try
            {
                // 强制文本网格 + 动态图集同步生成
                Canvas.ForceUpdateCanvases();
                foreach (var t in hud.GetComponentsInChildren<TMPro.TMP_Text>(true))
                    t.ForceMeshUpdate();

                var canvas = hud.GetComponentInChildren<Canvas>();
                // 2x 渲染：逻辑尺寸 ×2 的 RT，scaleFactor=2 让 UI 单位放大两倍（文字可读）
                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null) scaler.scaleFactor = 2f;
                var rt = new RenderTexture(logicalW * 2, logicalH * 2, 24);
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 10f;
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;

                string path = Path.Combine(Path.GetTempPath(), name + ".png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Debug.Log($"[RenderTest] {name} -> {path}");

                int lit = 0;
                foreach (var c in tex.GetPixels(0, 0, tex.width, tex.height))
                {
                    // 面板底色约 (0.08,0.08,0.15)，统计与相机背景/面板底色差异明显的像素
                    if (Mathf.Abs(c.r - 0.1f) + Mathf.Abs(c.g - 0.1f) + Mathf.Abs(c.b - 0.1f) > 0.15f)
                        lit++;
                }

                cam.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(rt);
                return lit;
            }
            finally
            {
                Object.DestroyImmediate(camGo);
            }
        }
    }
}
