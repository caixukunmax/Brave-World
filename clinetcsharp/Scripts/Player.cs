using Godot;
using Godot.Collections;

namespace ClinetCSharp
{
    /// <summary>
    /// 玩家角色类
    /// </summary>
    [GlobalClass]
    public partial class Player : EntityBase
    {
        private int _gridSize = 111;
        protected override int GetGridSize() => _gridSize;
        protected override void SetGridSizeValue(int value) => _gridSize = value;

        [Export] public float MoveDuration { get; set; } = 0.18f;
        [Export] public int FontSizeOverride { get; set; } = 0;
        [Export] public float LineSpacing { get; set; } = 0.8f;
        [Export] public float LetterSpacing { get; set; } = 0.0f;

        public bool FontBold { get; set; } = false;
        public bool FontItalic { get; set; } = false;
        public bool FontShadow { get; set; } = false;
        public bool ShowDebugInfo { get; set; } = true;
        public Array<Color> LineColors { get; set; } = new Array<Color> { Colors.Black, Colors.Black, Colors.Black, Colors.Black };

        public int Level { get; set; } = 1;
        public string CharacterName { get; set; } = "王建国";
        public string Job { get; set; } = "农夫";
        public string Title { get; set; } = "普通人";
        public string Status { get; set; } = "闲逛中...";

        public System.Collections.Generic.Dictionary<uint, int> CombatAttrs { get; } = new();

        public Vector2 LevelBadgeOffset { get; set; } = new Vector2(-35, -35);
        public float LevelBadgeFontSize { get; set; } = 12;
        public Color LevelBadgeTextColor { get; set; } = Colors.Yellow;
        public bool LevelBadgeVisible { get; set; } = true;
        public string LevelBadgeText { get; set; } = "LV.{level}";

        private Vector2I _gridPos = new Vector2I(25, 25);
        protected override Vector2I GetGridPos() => _gridPos;

        public HorizontalAlignment TextAlignment { get; set; } = HorizontalAlignment.Center;

        protected const int LabelCount = 4;
        private readonly RichTextLabel[] _labels = new RichTextLabel[LabelCount];
        private readonly Control[] _labelContainers = new Control[LabelCount];
        protected readonly Vector2[] _labelOffsets = new Vector2[LabelCount];
        private readonly bool[] _labelVisible = new bool[LabelCount] { true, true, true, true };
        private readonly int[] _labelFontSizes = new int[LabelCount];

        public static readonly Vector2[] DefaultOffsets = new Vector2[LabelCount]
        {
            Vector2.Zero,
            Vector2.Zero,
            Vector2.Zero,
            Vector2.Zero,
        };

        public bool LabelAutoCenterX { get; set; } = false;

        private bool _dragging = false;
        private int _dragIndex = -1;
        private Vector2 _dragStartMouse;
        private Vector2 _dragStartOffset;

        public string[] LabelNames = new string[LabelCount] { "名称", "职业", "称号", "状态" };
        public string[] PlayerLabelTexts = new string[LabelCount] { "LV.1 王建国", "农夫", "普通人", "闲逛中..." };

        private Vector2I? _pendingServerGridPos;
        private Tween? _serverGridCorrectionTween;

        /// <summary>
        /// 直接传送到指定格子坐标（用于 GM 命令、死亡重生等场景）
        /// </summary>
        public void TeleportToGrid(int x, int y)
        {
            ClearPendingServerGridCorrection();
            _currentTween?.Kill();
            _checkTimer?.Stop();
            _checkTimer?.QueueFree();
            _checkTimer = null;
            IsMoving = false;
            _bouncingBack = false;
            _collisionMove = false;
            _moveSentCount = 0;

            _gridPos = new Vector2I(x, y);
            _moveFromPos = _gridPos;
            Position = UiUtils.GridToWorld(_gridPos, GridSize);
        }
    }
}
