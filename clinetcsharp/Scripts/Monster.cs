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
        private Tween _currentTween;

        public override int GridSize => _gridSize;

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

        // 动作栏（角色下方）
        public string CastingSkill { get; set; } = "";
        public float CastProgress { get; set; } = 0f;
        public float ActionBarTextYOffset { get; set; } = 0f;
        public float ActionBarProgressHeight { get; set; } = 4f;

        // 标签名称（调试面板用，LabelTexts 在 EntityBase）
        public string[] LabelNames = new string[4] { "名称", "等级", "属性1", "属性2" };

        public void Setup(uint instanceId, uint monsterId, int x, int y, string name, uint level, int gridSize)
        {
            _instanceId = instanceId;
            _monsterId = monsterId;
            GridX = x;
            GridY = y;
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

            // 动作栏（角色下方）
            EntityDrawUtils.DrawActionBar(this, drawSize, CastingSkill, CastProgress, ActionBarTextYOffset, ActionBarProgressHeight);
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
            // VisualSize/BorderWidth/HealthBarLength/Height 等都是计算属性，自动跟随 GridSize
            Position = UiUtils.GridToWorld(GridX, GridY, _gridSize);
            QueueRedraw();
        }

        public void MoveTo(Vector2I targetGridPos, float duration = 0.15f)
        {
            GridX = targetGridPos.X;
            GridY = targetGridPos.Y;
            IsMoving = true;
            _currentTween?.Kill();
            _currentTween = CreateTween();
            _currentTween.SetTrans(Tween.TransitionType.Quad);
            _currentTween.SetEase(Tween.EaseType.Out);
            _currentTween.TweenProperty(this, "position", UiUtils.GridToWorld(GridX, GridY, _gridSize), duration);
            _currentTween.Finished += () => { IsMoving = false; _currentTween = null; };
        }

        public void RollbackTo(Vector2I pos)
        {
            _currentTween?.Kill();
            _currentTween = null;
            IsMoving = false;
            GridX = pos.X;
            GridY = pos.Y;
            Position = UiUtils.GridToWorld(GridX, GridY, _gridSize);
            QueueRedraw();
        }

        /// <summary>
        /// 平滑弹回到指定位置（不瞬移），用于碰撞取消等场景
        /// </summary>
        public void PlayBounceBack(Vector2I originPos, float duration = 0.12f)
        {
            _currentTween?.Kill();

            IsMoving = true;
            var originWorld = UiUtils.GridToWorld(originPos.X, originPos.Y, _gridSize);

            _currentTween = CreateTween();
            _currentTween.SetTrans(Tween.TransitionType.Quad);
            _currentTween.SetEase(Tween.EaseType.In);
            _currentTween.TweenProperty(this, "position", originWorld, duration);
            _currentTween.Finished += () =>
            {
                IsMoving = false;
                _currentTween = null;
                GridX = originPos.X;
                GridY = originPos.Y;
                Position = originWorld;
            };
        }

        // 外观/标签 setter 已移至 EntityBase

                public void SetActionBarTextYOffset(float offset)
        {
            ActionBarTextYOffset = offset;
            QueueRedraw();
        }

        public void SetActionBarProgressHeight(float height)
        {
            ActionBarProgressHeight = Mathf.Max(height, 1f);
            QueueRedraw();
        }

        
        // 血条/MP条 setter 已移至 EntityBase

    }
}
