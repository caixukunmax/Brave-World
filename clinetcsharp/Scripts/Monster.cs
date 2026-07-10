using ClinetCSharp.RenderComponents;
using Godot;
using Protocol;

namespace ClinetCSharp
{
    /// <summary>
    /// 怪物实体，渲染在地图格子上。
    /// </summary>
    public partial class Monster : EntityBase
    {
        public event System.Action<Monster, Vector2I, Vector2I>? MoveVisualCompleted;

        private int _gridSize = 111;
        private uint _instanceId;
        private uint _monsterId;
        private int _gridX;
        private int _gridY;
        private string _currentState = "idle";
        private Vector2I? _pendingGridPos;

        protected override int GetGridSize() => _gridSize;
        protected override void SetGridSizeValue(int value) => _gridSize = value;

        public uint InstanceId => _instanceId;
        public uint MonsterId => _monsterId;
        public int UiConfigId { get; private set; }
        protected override Vector2I GetGridPos() => new Vector2I(_gridX, _gridY);
        public int GridX => _gridX;
        public int GridY => _gridY;
        public Vector2I CurrentGridPos => new(_gridX, _gridY);
        public Vector2I? PendingGridPos => _pendingGridPos;
        public string MonsterName { get; private set; } = "";
        public string MonsterQuality { get; set; } = "普通";
        public uint Level { get; private set; } = 1;
        public Godot.Collections.Array MonsterAttrs { get; private set; } = new();
        /// <summary>历史遗留字段，纯 CD 制下始终为 0，保留以避免破坏序列化兼容性</summary>
        public float AtbValue { get; set; } = 0f;

        public uint NextSkillId { get; set; }
        public float NextSkillReadyIn { get; set; }

        public string CurrentState
        {
            get => _currentState;
            set
            {
                _currentState = string.IsNullOrWhiteSpace(value) ? "idle" : value;
                RefreshDataBoundLabels();
            }
        }

        public string[] LabelNames = { "名字", "品质", "状态", "预留" };

        public void Setup(uint instanceId, uint monsterId, int x, int y, string name, uint level, int gridSize, int uiConfigId = 1, int sizeX = 1, int sizeY = 1)
        {
            AddToGroup("monster");
            _instanceId = instanceId;
            _monsterId = monsterId;
            _gridX = x;
            _gridY = y;
            MonsterName = name;
            Level = level;
            _gridSize = gridSize;
            UiConfigId = uiConfigId;
            GridSizeX = sizeX > 0 ? sizeX : 1;
            GridSizeY = sizeY > 0 ? sizeY : 1;
            Position = GetWorldPositionForGridPos(new Vector2I(x, y));

            BorderColor = new Color(0.9f, 0.3f, 0.3f);
            BgColor = new Color(0.8f, 0.2f, 0.2f);
            BgOpacity = 0.9f;
            TextColor = new Color(1, 0.95f, 0.95f);
            CornerRadius = 12f;

            EnsureRenderComponents();
            SetupRichLabels();
            CurrentState = "idle";
            RefreshDataBoundLabels();
            QueueRedraw();
        }

        public void Setup(
            uint instanceId,
            uint monsterId,
            int x,
            int y,
            string name,
            uint level,
            int gridSize,
            Google.Protobuf.Collections.RepeatedField<Game.MonsterAttr> attrs,
            int uiConfigId = 0,
            string quality = "",
            int sizeX = 1,
            int sizeY = 1)
        {
            Setup(instanceId, monsterId, x, y, name, level, gridSize, uiConfigId, sizeX, sizeY);

            MonsterQuality = string.IsNullOrWhiteSpace(quality) ? "普通" : quality;
            MonsterAttrs = new Godot.Collections.Array();
            foreach (var attr in attrs)
            {
                MonsterAttrs.Add(new Godot.Collections.Dictionary
                {
                    ["attr_key"] = (int)attr.AttrKey,
                    ["attr_value"] = attr.AttrValue,
                });
            }

            RefreshDataBoundLabels();
            QueueRedraw();
        }

        public override void _Input(InputEvent @event)
        {
            CheckEntityClick(@event);
        }

        public override void _Draw()
        {
            foreach (var comp in _renderComponents)
                comp.Draw();
        }

        private void EnsureRenderComponents()
        {
            if (_renderComponents.Count > 0)
                return;

            AddRenderComponent(new RenderComponents.CombatAuraComponent());
            AddRenderComponent(new RenderComponents.AppearanceComponent());
            AddRenderComponent(new RenderComponents.HealthBarComponent());
            AddRenderComponent(new RenderComponents.MpBarComponent());
            AddRenderComponent(new RenderComponents.DebugOverlayComponent());
            // 施法条/动作栏由 Profile 组件动态驱动，不再硬编码
        }

        public override void MoveTo(Vector2I targetGridPos, float duration = 0.15f)
        {
            var fromGridPos = new Vector2I(_gridX, _gridY);
            _pendingGridPos = targetGridPos;
            base.MoveTo(targetGridPos, duration);
            if (_currentTween != null)
            {
                _currentTween.Finished += () =>
                {
                    _gridX = targetGridPos.X;
                    _gridY = targetGridPos.Y;
                    _pendingGridPos = null;
                    MoveVisualCompleted?.Invoke(this, fromGridPos, targetGridPos);
                };
            }
        }

        public override void RollbackTo(Vector2I pos)
        {
            _gridX = pos.X;
            _gridY = pos.Y;
            _pendingGridPos = null;
            base.RollbackTo(pos);
        }

        /// <summary>
        /// 播放死亡消失动画
        /// mode: 0=直接删除, 1=淡出消失, 2=变灰停留后淡出
        /// </summary>
        public void PlayDeathAnimation(int mode, float fadeDuration, float grayDelay, System.Action onFinished)
        {
            _currentTween?.Kill();

            if (mode == 0)
            {
                onFinished?.Invoke();
                return;
            }

            if (mode == 1)
            {
                // 淡出 + 缩小
                _currentTween = CreateTween();
                _currentTween.SetParallel(true);
                _currentTween.TweenProperty(this, "modulate:a", 0.0f, fadeDuration);
                _currentTween.TweenProperty(this, "scale", Vector2.Zero, fadeDuration);
                _currentTween.SetTrans(Tween.TransitionType.Quad);
                _currentTween.SetEase(Tween.EaseType.Out);
                _currentTween.Finished += () => onFinished?.Invoke();
                return;
            }

            // 模式 2: 变灰 → 停留 → 淡出缩小
            var grayColor = new Color(0.4f, 0.4f, 0.4f, 1.0f);
            _currentTween = CreateTween();
            _currentTween.TweenProperty(this, "modulate", grayColor, 0.3f);
            _currentTween.TweenInterval(grayDelay);
            _currentTween.TweenProperty(this, "modulate:a", 0.0f, fadeDuration);
            _currentTween.SetTrans(Tween.TransitionType.Quad);
            _currentTween.SetEase(Tween.EaseType.Out);
            _currentTween.Finished += () => onFinished?.Invoke();
        }

        public override void PlayBounceBack(Vector2I originPos, float duration = -1f)
        {
            _currentTween?.Kill();
            IsMoving = true;
            var originWorld = GetWorldPositionForGridPos(originPos);
            _currentTween = CreateTween();

            float d = duration < 0 ? BounceBackDuration : duration;

            // 计算当前已走的距离比例
            float distFromOrigin = Position.DistanceTo(originWorld);
            float ratio = distFromOrigin / Mathf.Max(1f, GridSize);

            if (_pendingGridPos.HasValue && ratio < BounceBackOvershootThreshold)
            {
                // 走得不多时：轻微 overshoot 然后弹回（更有"撞到东西"的感觉）
                var targetWorld = GetWorldPositionForGridPos(_pendingGridPos.Value);
                var dir = targetWorld - originWorld;
                if (dir.Length() > 0.001f)
                {
                    dir = dir.Normalized();
                    var overshootWorld = Position + dir * GridSize * BounceBackOvershootRatio;

                    _currentTween.SetTrans(Tween.TransitionType.Sine);
                    _currentTween.SetEase(Tween.EaseType.Out);
                    _currentTween.TweenProperty(this, "position", overshootWorld, d * 0.35f);

                    _currentTween.SetTrans(Tween.TransitionType.Cubic);
                    _currentTween.SetEase(Tween.EaseType.In);
                    _currentTween.TweenProperty(this, "position", originWorld, d * 0.65f);
                }
                else
                {
                    _currentTween.SetTrans(Tween.TransitionType.Cubic);
                    _currentTween.SetEase(Tween.EaseType.In);
                    _currentTween.TweenProperty(this, "position", originWorld, d);
                }
            }
            else
            {
                // 走得较远时（如贴脸碰撞，已走约 50%）：直接快速弹回，不再前冲
                // 避免"走了很远还继续冲一段"的突兀感
                _currentTween.SetTrans(Tween.TransitionType.Cubic);
                _currentTween.SetEase(Tween.EaseType.In);
                _currentTween.TweenProperty(this, "position", originWorld, d);
            }

            _currentTween.Finished += () =>
            {
                IsMoving = false;
                _currentTween = null;
                _gridX = originPos.X;
                _gridY = originPos.Y;
                _pendingGridPos = null;
                Position = originWorld;
            };
        }

        public void RefreshDataBoundLabels()
        {
            SetRichLabelText(0, BuildDisplayName());
            SetRichLabelText(1, string.IsNullOrWhiteSpace(MonsterQuality) ? "普通" : MonsterQuality);

            // 标签2：预留（隐藏）
            SetRichLabelText(2, "");

            // 标签3（状态）：施法中 > 过渡期“技能准备中” > 普通状态文本
            if (!string.IsNullOrEmpty(CastingSkill))
            {
                SetRichLabelText(3, CastingSkill);
                CastBarVisible = true;
                // CastBarFillPercent 由 StartCastAnimation 的 Tween 驱动
            }
            else if (NextSkillId > 0)
            {
                SetRichLabelText(3, "技能准备中");
                CastBarVisible = true;
                var skillData = SkillDataUtil.Get(NextSkillId);
                double totalCd = skillData.cd > 0 ? skillData.cd : NextSkillReadyIn;
                CastBarFillPercent = totalCd > 0
                    ? 1f - Mathf.Clamp(NextSkillReadyIn / (float)totalCd, 0f, 1f)
                    : 1f;
            }
            else
            {
                SetRichLabelText(3, GetStateDisplayText(CurrentState));
                CastBarVisible = false;
            }
        }

        private string BuildDisplayName()
        {
            string name = string.IsNullOrWhiteSpace(MonsterName) ? "未知野怪" : MonsterName;
            return $"LV.{Level} {name}";
        }

        private static string GetStateDisplayText(string state)
        {
            return (state ?? "").ToLowerInvariant() switch
            {
                "" => "待机",
                "idle" => "待机",
                "patrol" => "巡逻",
                "chase" => "追击",
                "return" => "回撤",
                "combat" => "接战",
                "combat_hold" => "对峙",
                "combat_chase" => "追杀",
                "combat_ranged_hold" => "远程瞄准",
                "combat_ranged_chase" => "远程追击",
                "combat_cast_hold" => "蓄法",
                "combat_cast_chase" => "施法追击",
                "dead" => "死亡",
                _ => state,
            };
        }
    }
}
