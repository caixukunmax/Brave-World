using NUnit.Framework;
using UnityClientSharp.Map.Rendering;

namespace BraveWorld.Tests.EditMode
{
    /// <summary>
    /// 中文字体发现探针：开发机（中文 Windows）必须能找到含 CJK 字形的 OS 字体，
    /// 否则全部中文 TMP 文本（面板标题、日志、实体标签）静默不可见。
    /// </summary>
    public class FontUtilEditModeTests
    {
        [Test]
        public void GetCjkFont_ReturnsFontWithChineseGlyph()
        {
            var font = FontUtil.GetCjkFont();
            Assert.IsNotNull(font, "FontUtil 未找到任何含中文字形的 OS 字体（中文文本将全部不可见）");
        }
    }
}
