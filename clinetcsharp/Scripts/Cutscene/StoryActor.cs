using Godot;
using ClinetCSharp;
using ClinetCSharp.RenderComponents;

namespace ClinetCSharp.Cutscene
{
    /// <summary>
    /// 演出假人 — 由 spawn_actor 创建的本地假实体，地图上不存在、纯演出用。
    /// 复用 EntityBase 的色块+标签绘制管线；ProcessMode=Always，
    /// 演出期间世界暂停时自身 Tween（MoveTo）仍可运行，无需外部宿主。
    /// </summary>
    public partial class StoryActor : EntityBase
    {
        private int _gridSize = 111;
        private int _gridX;
        private int _gridY;

        /// <summary>脚本中引用该假人的别名（actor:别名）</summary>
        public string Alias { get; private set; } = "";
        /// <summary>显示在头顶的名字</summary>
        public string ActorName { get; private set; } = "";

        protected override int GetGridSize() => _gridSize;
        protected override void SetGridSizeValue(int value) => _gridSize = value;
        protected override Vector2I GetGridPos() => new(_gridX, _gridY);

        /// <summary>
        /// 按真实外观配置（EntityStyleConfig）创建演出假人。
        /// 样式字段直接灌入 EntityBase 的渲染管线（与真实怪物同一套），使假人"相当于真的"。
        /// </summary>
        public void Setup(string alias, string actorName, EntityStyleConfig style, int x, int y, int gridSize)
        {
            AddToGroup("cutscene_actor");
            // 演出期间整棵树被 PauseGame 暂停，假实体必须 Always 才能继续移动/重绘
            ProcessMode = ProcessModeEnum.Always;

            Alias = alias;
            ActorName = actorName;
            _gridX = x;
            _gridY = y;
            _gridSize = gridSize;
            Position = GetWorldPositionForGridPos(new Vector2I(x, y));

            if (style != null)
            {
                BorderColor = style.BorderColor;
                BgColor = style.BgColor;
                TextColor = style.TextColor;
                CornerRadius = style.CornerRadius;
                BgOpacity = style.BgOpacity;
                VisualSizeScale = style.VisualSizeScale;
                BorderWidthScale = style.BorderWidthScale;
                FontSize = style.FontSize;
            }
            else
            {
                var def = EntityStyleConfig.CreateMonsterDefault();
                BorderColor = def.BorderColor;
                BgColor = def.BgColor;
                TextColor = def.TextColor;
                CornerRadius = 12f;
                BgOpacity = 0.9f;
            }

            // 假人不参与战斗，隐藏血条/MP条
            HealthBarVisible = false;
            MpBarVisible = false;

            LabelTexts[0] = actorName;
            LabelTexts[1] = "";
            LabelTexts[2] = "";
            LabelTexts[3] = "";

            EnsureRenderComponents();
            SetupRichLabels();
            QueueRedraw();
        }

        /// <summary>兜底重载：仅给单一颜色时，按颜色构造最小样式（兼容旧调用）</summary>
        public void Setup(string alias, string actorName, Color color, int x, int y, int gridSize)
            => Setup(alias, actorName, CutsceneActorCatalog.BuildStyleFromColor(color), x, y, gridSize);

        public override void _Draw()
        {
            foreach (var comp in _renderComponents)
                comp.Draw();
            EntityDrawUtils.DrawDirectionArrow(this);
        }

        /// <summary>确保渲染组件已添加（幂等，只添加一次）</summary>
        private void EnsureRenderComponents()
        {
            if (_renderComponents.Count > 0) return;
            AddRenderComponent(new RenderComponents.AppearanceComponent());
            AddRenderComponent(new RenderComponents.HealthBarComponent());
            AddRenderComponent(new RenderComponents.MpBarComponent());
        }

        /// <summary>演出走位：移动完成后同步格子坐标（Tween 由自身创建，Always 节点不受暂停影响）</summary>
        public override void MoveTo(Vector2I targetGridPos, float duration = 0.15f)
        {
            Direction = DirectionFromVector(targetGridPos.X - _gridX, targetGridPos.Y - _gridY);
            base.MoveTo(targetGridPos, duration);
            if (_currentTween != null)
            {
                _currentTween.Finished += () =>
                {
                    _gridX = targetGridPos.X;
                    _gridY = targetGridPos.Y;
                };
            }
        }
    }
}
