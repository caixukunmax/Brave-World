using Godot;
using Protocol;

namespace ClinetCSharp
{
    /// <summary>
    /// 怪物实体 - 渲染在地图格子上，风格与玩家一致（圆角正方形 + 中间信息）
    /// </summary>
    public partial class Monster : Node2D
    {
        private int _gridSize = 111;
        private uint _instanceId;
        private uint _monsterId;

        // 外观配置（与 Player 对齐）
        [Export] public int VisualSize { get; set; } = 111;
        [Export] public float VisualSizeScale { get; set; } = 1.0f;
        [Export] public float BorderWidth { get; set; } = 3.0f;
        [Export] public float BorderWidthScale { get; set; } = 3.0f / 111.0f;
        [Export] public Color BorderColor { get; set; } = Colors.White;
        [Export] public Color BgColor { get; set; } = new Color(1, 1, 1, 0.1f);
        [Export] public Color TextColor { get; set; } = Colors.Black;
        [Export] public float CornerRadius { get; set; } = 12.0f;
        [Export] public float BgOpacity { get; set; } = 0.1f;
        [Export] public int FontSize { get; set; } = 0; // 0 = 自动

        public uint InstanceId => _instanceId;
        public uint MonsterId => _monsterId;
        public int GridX { get; private set; }
        public int GridY { get; private set; }
        public Vector2I GridPos => new Vector2I(GridX, GridY);
        public string MonsterName { get; private set; } = "";
        public uint Level { get; private set; } = 1;
        public Godot.Collections.Array MonsterAttrs { get; private set; } = new Godot.Collections.Array(); // 保留兼容 DebugPanel

        public bool IsMoving { get; set; } = false;
        public string CurrentState { get; set; } = "idle";

        // 4 行文字（名称 / 等级 / 属性1 / 属性2）
        public string[] LabelTexts = new string[4] { "", "", "", "" };
        public string[] LabelNames = new string[4] { "名称", "等级", "属性1", "属性2" };
        public int[] LabelFontSizes = new int[4] { 0, 0, 0, 0 };
        public float[] LabelXOffsets = new float[4] { 0, 0, 0, 0 };
        public bool[] LabelCenterX = new bool[4] { true, true, true, true };
        public float[] LabelYOffsets = new float[4] { 0, 0, 0, 0 };

        public void Setup(uint instanceId, uint monsterId, int x, int y, string name, uint level, int gridSize)
        {
            _instanceId = instanceId;
            _monsterId = monsterId;
            GridX = x;
            GridY = y;
            MonsterName = name;
            Level = level;
            _gridSize = gridSize;
            VisualSize = gridSize;
            Position = UiUtils.GridToWorld(x, y, _gridSize);

            // 默认外观：红色系主题，实心背景（与玩家样式对齐）
            BorderColor = new Color(0.9f, 0.3f, 0.3f);
            BgColor = new Color(0.8f, 0.2f, 0.2f);
            BgOpacity = 0.9f;
            TextColor = new Color(1, 0.95f, 0.95f);
            CornerRadius = 12f;
            BorderWidth = 3f;

            // 默认文字
            LabelTexts[0] = name;
            LabelTexts[1] = $"Lv.{level}";
            LabelTexts[2] = "";
            LabelTexts[3] = "";

            QueueRedraw();
        }

        public void Setup(uint instanceId, uint monsterId, int x, int y, string name, uint level, int gridSize, Google.Protobuf.Collections.RepeatedField<Game.AttributePair> attrs)
        {
            Setup(instanceId, monsterId, x, y, name, level, gridSize);

            int attrLine = 2;
            foreach (var attr in attrs)
            {
                if (attrLine > 3) break;
                int key = (int)attr.AttrKey;
                int val = attr.AttrValue;
                string keyName = key switch
                {
                    1 => "HP",
                    2 => "ATK",
                    3 => "DEF",
                    _ => $"ATTR{key}",
                };
                LabelTexts[attrLine] = $"{keyName}:{val}";
                attrLine++;
            }

            QueueRedraw();
        }

        public override void _Draw()
        {
            var drawSize = VisualSize;
            if (drawSize < 10) drawSize = 10;

            var halfDraw = drawSize / 2.0f;
            var rect = new Rect2(new Vector2(-halfDraw, -halfDraw), new Vector2(drawSize, drawSize));
            var actualBgColor = new Color(BgColor.R, BgColor.G, BgColor.B, BgOpacity);

            if (CornerRadius > 0)
            {
                var maxRadius = halfDraw - BorderWidth;
                var actualRadius = Mathf.Min(CornerRadius, Mathf.Max(maxRadius, 0));
                this.DrawRoundedRect(rect, actualBgColor, true, actualRadius);
                this.DrawRoundedRect(rect, BorderColor, false, actualRadius, BorderWidth);
            }
            else
            {
                DrawRect(rect, actualBgColor, true);
                DrawRect(rect, BorderColor, false, BorderWidth);
            }

            // 绘制 4 行文字（以文字自身中心点对齐）
            var font = ThemeDB.FallbackFont;
            int baseFs = FontSize > 0 ? FontSize : Mathf.Max((int)(drawSize / 4.0f * 0.7f), 8);
            float baseLineHeight = baseFs * 1.1f;
            float totalHeight = baseLineHeight * 4;
            float startY = -(totalHeight / 2.0f) + baseLineHeight * 0.5f;

            for (int i = 0; i < 4; i++)
            {
                if (string.IsNullOrEmpty(LabelTexts[i])) continue;
                int fs = LabelFontSizes[i] > 0 ? LabelFontSizes[i] : baseFs;
                float posY = startY + i * baseLineHeight + LabelYOffsets[i];
                float posX = LabelCenterX[i] ? 0 : LabelXOffsets[i];

                var textSize = font.GetStringSize(LabelTexts[i], HorizontalAlignment.Left, -1, fs);
                // 当 width=-1 时 DrawString 的 HorizontalAlignment 无效，总是左对齐；
                // 因此需要手动把 x 左移半宽，让文字中心对准 posX
                float drawX = posX - textSize.X / 2f;
                // DrawString 的 y 是 baseline；要让文字视觉中心对齐到 posY，
                // 需要把 baseline 向上偏移 (ascent - descent)/2
                float baselineY = posY + (font.GetAscent(fs) - font.GetDescent(fs)) * 0.5f;
                var pos = new Vector2(drawX, baselineY);
                DrawString(font, pos, LabelTexts[i], HorizontalAlignment.Left, -1, fs, TextColor);
            }
        }

        public bool HitTest(Vector2 worldPos)
        {
            float half = _gridSize / 2.0f;
            var worldCenter = UiUtils.GridToWorld(GridX, GridY, _gridSize);
            return Mathf.Abs(worldPos.X - worldCenter.X) < half &&
                   Mathf.Abs(worldPos.Y - worldCenter.Y) < half;
        }

        // ========== 公共接口（供调试面板调用）==========

        public void SetGridSize(int size)
        {
            _gridSize = size;
            VisualSize = Mathf.Clamp((int)(size * VisualSizeScale), 10, size);
            BorderWidth = Mathf.Clamp(size * BorderWidthScale, 1.0f, 20.0f);
            Position = UiUtils.GridToWorld(GridX, GridY, _gridSize);
            QueueRedraw();
        }

        public void MoveTo(Vector2I targetGridPos, float duration = 0.15f)
        {
            GridX = targetGridPos.X;
            GridY = targetGridPos.Y;
            IsMoving = true;
            var tween = CreateTween();
            tween.SetTrans(Tween.TransitionType.Quad);
            tween.SetEase(Tween.EaseType.Out);
            tween.TweenProperty(this, "position", UiUtils.GridToWorld(GridX, GridY, _gridSize), duration);
            tween.Finished += () => { IsMoving = false; };
        }

        public void SetVisualSize(int size)
        {
            VisualSize = size;
            QueueRedraw();
        }

        public void SetVisualSizeScale(float scale)
        {
            VisualSizeScale = scale;
            VisualSize = Mathf.Clamp((int)(_gridSize * VisualSizeScale), 10, _gridSize);
            QueueRedraw();
        }

        public void SetBorderWidth(float width)
        {
            BorderWidth = width;
            QueueRedraw();
        }

        public void SetBorderWidthScale(float scale)
        {
            BorderWidthScale = scale;
            BorderWidth = Mathf.Clamp(_gridSize * BorderWidthScale, 1.0f, 20.0f);
            QueueRedraw();
        }

        public void SetBorderColor(Color color)
        {
            BorderColor = color;
            QueueRedraw();
        }

        public void SetBgColor(Color color)
        {
            BgColor = color;
            QueueRedraw();
        }

        public void SetTextColor(Color color)
        {
            TextColor = color;
            QueueRedraw();
        }

        public void SetCornerRadius(float radius)
        {
            CornerRadius = radius;
            QueueRedraw();
        }

        public void SetBgOpacity(float opacity)
        {
            BgOpacity = opacity;
            QueueRedraw();
        }

        public void SetFontSize(int size)
        {
            FontSize = size;
            QueueRedraw();
        }

        public void SetLabelText(int index, string text)
        {
            if (index < 0 || index >= 4) return;
            LabelTexts[index] = text;
            QueueRedraw();
        }

        public void SetLabelFontSize(int index, int size)
        {
            if (index < 0 || index >= 4) return;
            LabelFontSizes[index] = size;
            QueueRedraw();
        }

        public void SetLabelYOffset(int index, float offset)
        {
            if (index < 0 || index >= 4) return;
            LabelYOffsets[index] = offset;
            QueueRedraw();
        }

        public void SetLabelXOffset(int index, float offset)
        {
            if (index < 0 || index >= 4) return;
            LabelXOffsets[index] = offset;
            QueueRedraw();
        }

        public void SetLabelCenterX(int index, bool center)
        {
            if (index < 0 || index >= 4) return;
            LabelCenterX[index] = center;
            QueueRedraw();
        }

        public string GetLabelText(int index)
        {
            if (index < 0 || index >= 4) return "";
            return LabelTexts[index];
        }

    }
}
