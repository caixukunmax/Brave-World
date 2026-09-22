using Godot;
using System.Collections.Generic;
using ClinetCSharp.Cutscene;

namespace ClinetCSharp
{
    /// <summary>
    /// 演出编辑器地图叠加层（SubViewport 内的 Node2D）：
    /// 只画演出相关的静态示意——trigger.rect 触发区、actor_enter 登场落点标记、
    /// move 走位折线（按执行顺序连点）、camera_set 机位标记、选中 cue 高亮、框选预览。
    /// 地形/网格线/图外灰由同级的 GridManager（运行时同款 GPU shader）渲染，装饰物由真实
    /// MapDecoration 节点渲染（见 CutsceneDock.LoadViewMap），本层一律不画（§5.5）。
    /// 地图数据由主面板灌入，本节点不自己读文件。
    /// </summary>
    [Tool]
    public partial class CutsceneMapRenderer : Node2D
    {
        /// <summary>与运行时 GridManager.GridSize 默认值一致，预览画面与游戏 1:1</summary>
        public const int GridSize = 111;

        public Dictionary<Vector2I, GridCell> GridData;
        public Rect2I MapBounds;
        public string CurrentMapName = "";
        public CutsceneScript Script;
        public CutsceneCue SelectedCue;
        /// <summary>框选触发区过程中的预览矩形（格子坐标），null = 不在框选</summary>
        public Rect2I? RectDragPreview;

        private static readonly Color TriggerFill = new(1f, 0.9f, 0.2f, 0.18f);
        private static readonly Color TriggerBorder = new(1f, 0.9f, 0.2f, 0.9f);
        private static readonly Color SpawnMarkerColor = new(0.3f, 0.9f, 1f);
        private static readonly Color MoveLineColor = new(1f, 0.55f, 0.1f, 0.9f);
        private static readonly Color CameraMarkerColor = new(0.75f, 0.5f, 1f, 0.95f);
        private static readonly Color SelectedRingColor = new(1f, 1f, 1f, 0.95f);

        private Font _font;

        public override void _Ready()
        {
            _font = ThemeDB.FallbackFont;
        }

        public Vector2 GridToWorld(Vector2I gridPos)
        {
            return new Vector2(gridPos.X * GridSize + GridSize / 2f, gridPos.Y * GridSize + GridSize / 2f);
        }

        public override void _Draw()
        {
            if (GridData == null) return;

            if (Script != null)
                DrawOverlays();

            // ---- 框选预览 ----
            if (RectDragPreview.HasValue)
            {
                var r = RectDragPreview.Value;
                var rect = new Rect2(r.Position.X * GridSize, r.Position.Y * GridSize, r.Size.X * GridSize, r.Size.Y * GridSize);
                DrawRect(rect, TriggerFill);
                DrawRect(rect, TriggerBorder, false, 2f);
            }
        }

        /// <summary>叠加演出示意：触发区 / spawn 落点 / move 走位折线 / camera_set 机位 / 选中 cue 高亮</summary>
        private void DrawOverlays()
        {
            // 触发区（只在其配置的地图上显示）
            var trigger = Script.Trigger;
            if (trigger?.Rect != null && trigger.Rect.Length == 4 && trigger.Map == CurrentMapName)
            {
                var rect = new Rect2(trigger.Rect[0] * GridSize, trigger.Rect[1] * GridSize,
                    trigger.Rect[2] * GridSize, trigger.Rect[3] * GridSize);
                DrawRect(rect, TriggerFill);
                DrawRect(rect, TriggerBorder, false, 3f);
                if (_font != null)
                    DrawString(_font, rect.Position + new Vector2(6, 20), "触发区", fontSize: 20, modulate: TriggerBorder);
            }

            // 按执行顺序遍历 cue，维护 actor 最近一次已知位置，move 之间连线
            var lastPos = new Dictionary<string, Vector2I>();
            int moveIndex = 0;
            foreach (var group in Script.BuildExecutionGroups())
            {
                foreach (var cue in group)
                {
                    string type = cue.Type.ToLowerInvariant();
                    if (type == "actor_enter" && cue.Has("x") && cue.Has("y"))
                    {
                        var pos = new Vector2I(cue.GetInt("x"), cue.GetInt("y"));
                        lastPos[cue.Actor ?? ""] = pos;
                        // 登场落点标记：圆点 + 演员 id（怪物附名字）
                        DrawCircle(GridToWorld(pos), GridSize * 0.3f, SpawnMarkerColor);
                        if (_font != null)
                            DrawString(_font, GridToWorld(pos) + new Vector2(GridSize * 0.35f, -GridSize * 0.25f),
                                ActorEnterLabel(cue), fontSize: 18, modulate: SpawnMarkerColor);
                    }
                    else if (type == "actor_leave")
                    {
                        lastPos.Remove(cue.Actor ?? "");
                    }
                    else if (type == "move" && cue.Has("x") && cue.Has("y"))
                    {
                        var target = new Vector2I(cue.GetInt("x"), cue.GetInt("y"));
                        moveIndex++;
                        if (!string.IsNullOrEmpty(cue.Actor) && lastPos.TryGetValue(cue.Actor, out var from))
                            DrawLine(GridToWorld(from), GridToWorld(target), MoveLineColor, 3f);
                        lastPos[cue.Actor ?? ""] = target;
                        // 目标点：圆点 + 顺序号
                        DrawCircle(GridToWorld(target), GridSize * 0.22f, MoveLineColor);
                        if (_font != null)
                            DrawString(_font, GridToWorld(target) + new Vector2(GridSize * 0.3f, GridSize * 0.3f),
                                moveIndex.ToString(), fontSize: 16, modulate: MoveLineColor);
                    }
                    else if (type == "camera_set" && cue.Has("x") && cue.Has("y"))
                    {
                        // 机位标记：方框 + "机位" 文字
                        var pos = new Vector2I(cue.GetInt("x"), cue.GetInt("y"));
                        float h = GridSize * 0.32f;
                        var center = GridToWorld(pos);
                        DrawRect(new Rect2(center.X - h, center.Y - h, h * 2, h * 2), CameraMarkerColor, false, 3f);
                        if (_font != null)
                            DrawString(_font, center + new Vector2(h + 4, 6), "机位", fontSize: 18, modulate: CameraMarkerColor);
                    }
                }
            }

            // 选中 cue 的坐标高亮（白色方框），便于确认正在编辑哪个点
            if (SelectedCue != null && SelectedCue.Has("x") && SelectedCue.Has("y"))
            {
                var pos = new Vector2I(SelectedCue.GetInt("x"), SelectedCue.GetInt("y"));
                DrawRect(new Rect2(pos.X * GridSize, pos.Y * GridSize, GridSize, GridSize), SelectedRingColor, false, 3f);
            }
        }

        /// <summary>登场落点标签：演员 id；怪物演员附配置名</summary>
        private string ActorEnterLabel(CutsceneCue cue)
        {
            string id = cue.Actor ?? "";
            if (!int.TryParse(id, out int actorId)) return id;
            var def = Script?.FindActor(actorId);
            if (def != null && def.type == 2)
                return $"{actorId}({CutsceneActorCatalog.ResolveMonsterName(def.monsterConfigId)})";
            if (def != null && def.type == 1)
                return $"{actorId}(玩家)";
            return id;
        }
    }
}
