using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityClientSharp.Map.Rendering;
using UnityClientSharp.UI;

namespace BraveWorld.Tests.PlayMode
{
    /// <summary>
    /// 像素级可视验证：战斗日志面板的中文标题与日志行必须真的渲染出像素
    /// （数据写入正确但字体缺失时面板"看起来空"，本测试专抓这类问题）。
    /// 实现注意：batchmode 没有帧循环（WaitForEndOfFrame / CaptureScreenshot 系列会挂死或报错），
    /// 因此改为把面板画布切到 ScreenSpaceCamera，用相机手动 Render 到 RenderTexture 同步读像素；
    /// 渲染结果同时 EncodeToPNG 落盘 %TEMP%/bw_combatlog_visual.png 供人工复核。
    /// </summary>
    public class FontRenderVisualTests
    {
        [UnityTest]
        public IEnumerator CombatLogPanel_RendersChinesePixels()
        {
            UiEventSystemUtil.EnsureExists();
            var hud = CombatLogHud.Create();

            // 测试相机（正交朝 +Z，ScreenSpaceCamera 画布挂在 planeDistance 前方）
            var camGo = new GameObject("TestCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
            cam.orthographic = true;

            // 走与 CombatLogHudTests 相同的注入路径喂一行中文日志
            var notify = new Game.CombatLogNotify();
            notify.Entries.Add(new Game.CombatLogEntry
            {
                LogType = Game.CombatLogType.CombatLogDamage,
                Timestamp = 1784336400,
                ActorName = "测试勇者",
                TargetName = "史莱姆",
                Extra = "测试勇者 对 史莱姆 造成 15 点伤害",
            });
            typeof(CombatLogHud)
                .GetMethod("OnCombatLog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(hud, new object[] { notify });

            yield return null; // LateUpdate 写入 _text

            Assert.IsNotNull(FontUtil.GetCjkFont(), "CJK 字体未创建，中文必然不可见");

            // 强制文本网格 + 动态图集同步生成
            Canvas.ForceUpdateCanvases();
            foreach (var t in hud.GetComponentsInChildren<TMPro.TMP_Text>(true))
                t.ForceMeshUpdate();

            // 切画布到 ScreenSpaceCamera，手动渲染到 RT 读像素（同步，无帧循环依赖）
            var rt = new RenderTexture(800, 600, 24);
            var canvas = hud.GetComponentInChildren<Canvas>();
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

            // 面板锚定左下 (20,20)，400×220（ConstantPixelSize 1:1 像素）：
            // 标题栏 = 面板顶 24px → y 216..240；正文 TopLeft，首行紧贴标题栏下方（y≈186..210）。
            int titleLit = CountLitPixels(tex, 28, 218, 200, 20, 0.12f, 0.12f, 0.16f);
            int bodyLit = CountNonDarkPixels(tex, 28, 186, 360, 24);

            // 落盘供人工复核
            string path = Path.Combine(Path.GetTempPath(), "bw_combatlog_visual.png");
            File.WriteAllBytes(path, tex.EncodeToPNG());

            // 清理
            cam.targetTexture = null;
            rt.Release();
            Object.Destroy(tex);
            Object.Destroy(rt);
            Object.Destroy(hud.gameObject);
            Object.Destroy(camGo);

            Assert.Greater(titleLit, 50, $"标题栏几乎无文字像素（lit={titleLit}）——中文标题未渲染");
            Assert.Greater(bodyLit, 50, $"正文区几乎无文字像素（lit={bodyLit}）——日志行未渲染");
        }

        /// <summary>统计区域内与给定背景色差异明显的像素数（文字/描边）。</summary>
        private static int CountLitPixels(Texture2D tex, int x, int y, int w, int h, float bgR, float bgG, float bgB)
        {
            int lit = 0;
            foreach (var c in tex.GetPixels(x, y, w, h))
            {
                if (Mathf.Abs(c.r - bgR) + Mathf.Abs(c.g - bgG) + Mathf.Abs(c.b - bgB) > 0.15f)
                    lit++;
            }
            return lit;
        }

        /// <summary>统计区域内明显亮于面板底色的像素数（面板底色约 0.05~0.1）。</summary>
        private static int CountNonDarkPixels(Texture2D tex, int x, int y, int w, int h)
        {
            int lit = 0;
            foreach (var c in tex.GetPixels(x, y, w, h))
            {
                if (c.r + c.g + c.b > 0.45f)
                    lit++;
            }
            return lit;
        }
    }
}
