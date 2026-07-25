using TMPro;
using UnityEngine;

namespace UnityClientSharp.Map.Rendering
{
    /// <summary>
    /// 共享字体工具：运行时从 OS 字体创建动态 CJK TMP FontAsset。
    /// TMP 默认 LiberationSans 无中文字形，任何会显示中文的 TMP 文本都必须走这里（AGENTS.md 第 4 条）。
    /// 关键坑（AGENTS.md 第 20 条）：这版引擎的 TMP FontEngine 只接受"带导入数据"的字体——
    /// CreateDynamicFontFromOSFont / new Font(OS路径) 全部 LoadFontFace 失败（"Unable to load font face"），
    /// 只有 Assets 内的字体资产（Resources/Fonts）能创建 FontAsset，故内置优先、OS 扫描仅作兜底。
    /// 另：CreateFontAsset 内部要 new Material(TMP SDF shader)，工程必须导入 TMP Essentials
    /// （TMP 包 Package Resources 里的 unitypackage），否则 Shader.Find 返回 null 抛 ArgumentNullException。
    /// </summary>
    public static class FontUtil
    {
        private static TMP_FontAsset s_cjkFont;
        private static bool s_searched;

        // 内置字体（Assets/Resources/Fonts 下，按文件名不带扩展名；排前的优先）
        private static readonly string[] s_bundledPreferred = { "Fonts/NotoSansSC-VF", "Fonts/SimHei" };

        // 与 Godot 端 SystemFont 回退链一致 + 各系统语言的本地化名称；.ttf 家族优先（.ttc 不保证能加载）
        private static readonly string[] s_candidates =
        {
            "SimHei", "黑体",
            "DengXian", "等线",
            "KaiTi", "楷体",
            "FangSong", "仿宋",
            "SimSun", "宋体", "NSimSun", "新宋体",
            "Microsoft YaHei", "微软雅黑", "Microsoft YaHei UI",
            "Microsoft JhengHei", "微軟正黑體",
            "PingFang SC", "苹方-简", "Hiragana Sans GB", "冬青黑体简体中文",
            "Noto Sans CJK SC", "Noto Sans SC", "Source Han Sans SC", "思源黑体",
            "WenQuanYi Zen Hei", "文泉驿正黑",
            "Arial Unicode MS",
        };

        // 兜底扫描已安装字体时的名字特征（小写包含匹配）
        private static readonly string[] s_nameHints =
        {
            "yahei", "雅黑", "jhenghei", "正黑", "dengxian", "等线",
            "simhei", "黑体", "simsun", "宋体", "fangsong", "仿宋", "kaiti", "楷体",
            "pingfang", "苹方", "hiragana", "冬青", "cjk", "source han", "思源",
            "wenquanyi", "文泉", "noto sans sc", "noto sans tc", "unicode",
        };

        /// <summary>获取中文字体资产（懒加载，只查找一次；找不到返回 null 并告警）。</summary>
        public static TMP_FontAsset GetCjkFont()
        {
            if (s_searched) return s_cjkFont;
            s_searched = true;

            // 0) 项目内置字体资产：这版引擎唯一稳定接受的来源，且随包分发最确定
            foreach (var resPath in s_bundledPreferred)
            {
                var f = Resources.Load<Font>(resPath);
                if (TryMakeAsset(f, resPath, ref s_cjkFont)) return s_cjkFont;
            }
            foreach (var f in Resources.LoadAll<Font>("Fonts"))
            {
                if (TryMakeAsset(f, f != null ? f.name : "?", ref s_cjkFont)) return s_cjkFont;
            }

            var names = Font.GetOSInstalledFontNames();
            var paths = Font.GetPathsToOSFonts();
            bool pathValid = paths != null && paths.Length == names.Length;

            // 1) 候选名单：按名字命中后优先走字体文件路径加载
            foreach (var cand in s_candidates)
            {
                for (int i = 0; i < names.Length; i++)
                {
                    if (!string.Equals(names[i], cand, System.StringComparison.OrdinalIgnoreCase)) continue;
                    if (pathValid && TryCreateFromFile(paths[i], names[i], out s_cjkFont)) return s_cjkFont;
                    if (TryCreateDynamic(names[i], out s_cjkFont)) return s_cjkFont;
                }
            }

            // 2) 兜底：遍历已安装字体，按名字特征挑出可能含 CJK 字形的逐个实测
            for (int i = 0; i < names.Length; i++)
            {
                var lower = names[i].ToLowerInvariant();
                bool hinted = false;
                foreach (var hint in s_nameHints)
                {
                    if (lower.Contains(hint)) { hinted = true; break; }
                }
                if (!hinted) continue;
                if (pathValid && TryCreateFromFile(paths[i], names[i], out s_cjkFont)) return s_cjkFont;
                if (TryCreateDynamic(names[i], out s_cjkFont)) return s_cjkFont;
            }

            // 诊断：列出部分已安装字体名，便于现场排查
            string sample = string.Join(", ", names, 0, Mathf.Min(20, names.Length));
            Debug.LogWarning($"[FontUtil] 未找到可用的 OS 中文字体，中文文本将无法显示。已安装字体样例：{sample}");
            return null;
        }

        /// <summary>为 TMP 文本组件设置中文字体（若可用）。</summary>
        public static void ApplyCjkFont(TMP_Text text)
        {
            var font = GetCjkFont();
            if (text != null && font != null) text.font = font;
        }

        /// <summary>
        /// 设置世界空间 TextMeshPro 的字号（单位 = 世界单位像素，与 Godot 1px=1世界单位、
        /// EntityAppearanceLayout 的字号公式对齐）。
        /// 坑：世界空间 TMP 对非正交文本内建 0.1 缩放
        /// （TMP 源码 m_fontScale = fontSize/pointSize × (m_isOrthographic ? 1 : 0.1f)，
        /// TextMeshPro 的 m_isOrthographic 默认 false），直接 fontSize=18 只渲染 1.8 世界单位高。
        /// 本项目相机就是正交 2D，故统一 isOrthographic=true，fontSize 即世界单位。
        /// 世界空间 TMP 一律走这里，禁止直接给 fontSize 赋世界单位值。
        /// 另：运行时 AddComponent 的 TMP 其 RectTransform 宽度为 0，enableWordWrapping 默认 true
        /// 会导致多字标签（如"岩石"）逐字换行，与调试面板实体预览（IMGUI 单行）不一致，
        /// 故统一关闭换行；显式 \n（如坐标标注）不受影响。
        /// </summary>
        public static void SetWorldFontSize(TextMeshPro tmp, float worldSize)
        {
            if (tmp == null) return;
            tmp.isOrthographic = true;
            tmp.fontSize = worldSize;
            tmp.enableWordWrapping = false;
        }

        /// <summary>按字体文件路径加载（带数据，TMP FontEngine 可识别）并实测中文字形。</summary>
        private static bool TryCreateFromFile(string path, string family, out TMP_FontAsset asset)
        {
            asset = null;
            try
            {
                // UnityEngine.Font 只有 Font(string)：传文件路径即按文件加载（家族名从文件内读出）
                var font = new Font(path);
                if (font == null) return false;
                return TryMakeAsset(font, family, ref asset);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[FontUtil] 从文件创建字体 {family}({path}) 失败：{ex.Message}");
                return false;
            }
        }

        /// <summary>按名字创建 OS 引用动态字体（不带数据，部分平台/引擎可用，作为后备）。</summary>
        private static bool TryCreateDynamic(string family, out TMP_FontAsset asset)
        {
            asset = null;
            try
            {
                var font = Font.CreateDynamicFontFromOSFont(family, 16);
                if (font == null) return false;
                return TryMakeAsset(font, family, ref asset);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[FontUtil] 创建字体 {family} 失败：{ex.Message}");
                return false;
            }
        }

        /// <summary>验证中文字形并创建 TMP FontAsset（1024 图集 + 多图集，避免用字多撑爆小图集）。</summary>
        private static bool TryMakeAsset(Font font, string family, ref TMP_FontAsset asset)
        {
            if (font == null) return false;
            // 名字解析可能回退到无中文字形的字体，必须实测
            if (!font.HasCharacter('中')) return false;
            try
            {
                asset = TMP_FontAsset.CreateFontAsset(font, 90, 9,
                    UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024,
                    AtlasPopulationMode.Dynamic, true);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[FontUtil] 创建 FontAsset({family}) 失败：{ex.Message}");
                return false;
            }
            if (asset == null) return false;
            asset.name = "RuntimeCJK_" + family;
            return true;
        }
    }
}
