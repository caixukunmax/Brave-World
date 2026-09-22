using UnityEngine;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// 实体外观布局的唯一权威来源：身体外框尺寸/边框宽、标签字号、标签行位置。
    /// 运行时（MapDecoration / EntityVisualBase）与编辑器调试面板预览都必须走这里，
    /// 禁止各自复制公式，否则预览/地图/游戏三者显示会不一致。
    /// 公式与 Godot EntityDrawUtils / EntityLabelLayout 对齐（数值勿改）。
    /// </summary>
    public static class EntityAppearanceLayout
    {
        /// <summary>默认标签行数（与 LabelGroupData.DefaultRowCount 对齐；实际行数按各 Profile 的 labels.Count）。</summary>
        public const int LabelRows = 4;

        /// <summary>默认格子尺寸（GridManager.GridSize 默认值；调试面板预览等无地图上下文处统一用此值）。</summary>
        public const int DefaultGridSize = 111;

        public const float DefaultBorderWidthScale = 3.0f / 111.0f;

        /// <summary>1x1 实体外框参考边长（血条长度等也基于此）。</summary>
        public static int ComputeOuterRef(int gridSize, float visualScale)
            => Mathf.Clamp(Mathf.RoundToInt(gridSize * visualScale), 10, gridSize);

        /// <summary>身体外框尺寸（像素=世界单位）与边框宽。</summary>
        public static void ComputeBody(int gridSize, int sizeX, int sizeY, float visualScale, float borderWidthScale,
            out int outerW, out int outerH, out int border)
        {
            visualScale = visualScale > 0f ? visualScale : 1.0f;
            outerW = Mathf.Clamp(Mathf.RoundToInt(gridSize * sizeX * visualScale), 10, gridSize * sizeX);
            outerH = Mathf.Clamp(Mathf.RoundToInt(gridSize * sizeY * visualScale), 10, gridSize * sizeY);
            int outerRef = ComputeOuterRef(gridSize, visualScale);
            border = Mathf.Clamp(Mathf.RoundToInt(gridSize * borderWidthScale), 1, Mathf.Max(1, outerRef / 2));
        }

        /// <summary>标签字号（fontSizeSetting 0=自动，基于 1x1 内尺寸）。</summary>
        public static int ComputeFontSize(int gridSize, int fontSizeSetting, float visualScale, float borderWidthScale)
        {
            visualScale = visualScale > 0f ? visualScale : 1.0f;
            int border = Mathf.Clamp(Mathf.RoundToInt(gridSize * borderWidthScale), 1, gridSize / 2);
            int innerRef = Mathf.Max(2, ComputeOuterRef(gridSize, visualScale) - border * 2);
            return fontSizeSetting > 0 ? fontSizeSetting : Mathf.Max((int)(innerRef / 4.0f * 0.7f), 8);
        }

        /// <summary>标签行高。</summary>
        public static float LineHeight(int fontSize) => fontSize * 1.1f;

        /// <summary>
        /// 行有效字号：勾选「使用默认」或行内字号未指定(&lt;=0)时走全局默认（含 0=自动），否则用行内字号。
        /// 调试面板预览 / MapDecoration / EntityVisualBase 三端统一走这里，禁止各自复制取值逻辑。
        /// </summary>
        public static int RowFontSize(int gridSize, LabelGroupData labels, int row, float visualScale, float borderWidthScale)
        {
            if (!labels.UseGlobalFontSize[row] && labels.FontSizes[row] > 0) return labels.FontSizes[row];
            return ComputeFontSize(gridSize, labels.DefaultFontSize, visualScale, borderWidthScale);
        }

        /// <summary>行有效文字颜色：勾选「使用默认」时用全局默认色，否则用行内颜色（三端共用）。</summary>
        public static Color RowTextColor(LabelGroupData labels, int row)
            => labels.UseGlobalFontSize[row] ? labels.DefaultTextColor : labels.TextColors[row];

        /// <summary>标签字体样式（粗体/斜体；阴影由 EntityLabelShadow 单独处理）。三端共用。</summary>
        public static TMPro.FontStyles RowFontStyle(LabelGroupData labels)
            => (labels.Bold ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal)
             | (labels.Italic ? TMPro.FontStyles.Italic : TMPro.FontStyles.Normal);

        /// <summary>
        /// rowCount 行标签块中第 row 行中心相对实体中心的世界 Y 偏移（上为正），含行内 yOffset。
        /// 对齐 Godot：godotY = -(lineH*rowCount/2) + lineH*0.5 + row*lineH + yOffset（y 向下），世界 y 取负。
        /// </summary>
        public static float LineWorldOffsetY(int row, int fontSize, float yOffset = 0f, int rowCount = LabelRows)
            => (rowCount / 2f - (row + 0.5f)) * LineHeight(fontSize) - yOffset;
    }
}
