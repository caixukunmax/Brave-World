using Godot;
using Protocol;

namespace ClinetCSharp
{
    /// <summary>
    /// 怪物实体 - 渲染在地图格子上，风格与玩家一致（圆角正方形 + 中间信息）
    /// </summary>
    public partial class Monster : EntityBase
    {
        private int _gridSize = 111;
        private uint _instanceId;
        private uint _monsterId;
        private int _gridX;
        private int _gridY;

        protected override int GetGridSize() => _gridSize;
        protected override void SetGridSizeValue(int value) => _gridSize = value;

        public uint InstanceId => _instanceId;
        public uint MonsterId => _monsterId;
        protected override Vector2I GetGridPos() => new Vector2I(_gridX, _gridY);
        public int GridX => _gridX;
        public int GridY => _gridY;
        public string MonsterName { get; private set; } = "";
        public uint Level { get; private set; } = 1;
        public Godot.Collections.Array MonsterAttrs { get; private set; } = new Godot.Collections.Array(); // 保留兼容 DebugPanel

        public string CurrentState { get; set; } = "idle";

        // 标签名称（调试面板用，LabelTexts 在 EntityBase）
        public string[] LabelNames = new string[4] { "名称", "等级", "属性1", "属性2" };

        public void Setup(uint instanceId, uint monsterId, int x, int y, string name, uint level, int gridSize)
        {
            _instanceId = instanceId;
            _monsterId = monsterId;
            _gridX = x;
            _gridY = y;
            MonsterName = name;
            Level = level;
            _gridSize = gridSize;
            Position = UiUtils.GridToWorld(x, y, _gridSize);

            // 默认外观：红色系主题，实心背景（与玩家样式对齐）
            BorderColor = new Color(0.9f, 0.3f, 0.3f);
            BgColor = new Color(0.8f, 0.2f, 0.2f);
            BgOpacity = 0.9f;
            TextColor = new Color(1, 0.95f, 0.95f);
            CornerRadius = 12f;

            // 默认文字
            LabelTexts[0] = name;
            LabelTexts[1] = $"Lv.{level}";
            LabelTexts[2] = "";
            LabelTexts[3] = "";

            QueueRedraw();
        }

        public void Setup(uint instanceId, uint monsterId, int x, int y, string name, uint level, int gridSize, Google.Protobuf.Collections.RepeatedField<Game.MonsterAttr> attrs)
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

            EntityDrawUtils.DrawBody(this, drawSize, BgColor, BgOpacity, BorderColor, BorderWidth, CornerRadius);
            DrawBars();
            DrawLabels();
            DrawCastBar();
            DrawActionBar();
        }

        // ========== 移动 override（更新 _gridX/_gridY 后调用基类） ==========

        public override void MoveTo(Vector2I targetGridPos, float duration = 0.15f)
        {
            _gridX = targetGridPos.X;
            _gridY = targetGridPos.Y;
            base.MoveTo(targetGridPos, duration);
        }

        public override void RollbackTo(Vector2I pos)
        {
            _gridX = pos.X;
            _gridY = pos.Y;
            base.RollbackTo(pos);
        }

        public override void PlayBounceBack(Vector2I originPos, float duration = 0.12f)
        {
            _currentTween?.Kill();
            IsMoving = true;
            var originWorld = UiUtils.GridToWorld(originPos, GridSize);
            _currentTween = CreateTween();
            _currentTween.SetTrans(Tween.TransitionType.Quad);
            _currentTween.SetEase(Tween.EaseType.In);
            _currentTween.TweenProperty(this, "position", originWorld, duration);
            _currentTween.Finished += () =>
            {
                IsMoving = false;
                _currentTween = null;
                _gridX = originPos.X;
                _gridY = originPos.Y;
                Position = originWorld;
            };
        }
    }
}
