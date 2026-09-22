using System.Collections.Generic;
using UnityClientSharp.Map.Core;
using UnityEditor;
using UnityEngine;

namespace BraveWorld.Editor.MapEditing
{
    /// <summary>
    /// Scene 视图交互层：双工具鼠标逻辑（刷地形拖框框刷 / 放建筑拖放）、
    /// Handles 高亮（悬停格/选区/footprint 幽灵）、快捷键（Ctrl+Z/Y、Delete、Esc）。
    /// 鼠标→格子换算：世界 y 向上、逻辑 y 向下，翻转统一走 GridMath.LogicToWorld（AGENTS.md 第 1 条）。
    /// </summary>
    [InitializeOnLoad]
    public static class MapEditSceneGui
    {
        private enum DragState { None, Selecting, MovingDecoration }

        private static DragState _drag = DragState.None;
        private static Vector2Int _selStart, _selEnd;
        private static Vector2Int _moveSrc;
        private static int _moveType;

        static MapEditSceneGui()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        [InitializeOnLoadMethod]
        private static void Init()
        {
            // 域重载后窗口字段失效，HideAndDontSave 对象可能残留 → 延迟清理
            EditorApplication.delayCall += MapEditSession.CleanupOrphans;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            // 隔离编辑对象进 Play 会执行 Awake（污染 Walkability 静态），进 Play 前强制退出
            if (state != PlayModeStateChange.ExitingEditMode) return;
            var win = MapEditorWindow.Active;
            if (win != null && win.Session.IsActive)
            {
                Debug.LogWarning("[MapEditor] 进入 Play 前自动退出地图编辑（未保存的修改已丢弃，请先保存）");
                win.ExitSessionSilently();
            }
        }

        private static void OnSceneGui(SceneView view)
        {
            var win = MapEditorWindow.Active;
            if (win == null || !win.Session.IsActive) return;
            var grid = win.Session.Grid;
            var e = Event.current;

            int controlId = GUIUtility.GetControlID("BraveWorldMapEdit".GetHashCode(), FocusType.Passive);
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(controlId);

            // 鼠标→世界（z=0 平面求交）→格子（世界 y 向上 → 逻辑 y 向下，取负）
            Vector3 world = Vector3.zero;
            bool hit = false;
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (Mathf.Abs(ray.direction.z) > 1e-6f)
            {
                world = ray.origin + ray.direction * (-ray.origin.z / ray.direction.z);
                hit = true;
            }
            var hover = new Vector2Int(
                Mathf.FloorToInt(world.x / grid.GridSize),
                Mathf.FloorToInt(-world.y / grid.GridSize));
            if (hit) win.HoverGrid = hover;

            switch (e.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (!hit) break;
                    if (e.button == 0) { OnLeftDown(win, hover); e.Use(); }
                    else if (e.button == 1) { OnRightDown(win, hover); e.Use(); }
                    break;
                case EventType.MouseDrag:
                    if (e.button == 0 && _drag != DragState.None) { OnLeftDrag(win, hover); e.Use(); }
                    break;
                case EventType.MouseUp:
                    if (e.button == 0 && _drag != DragState.None) { OnLeftUp(win, hover); e.Use(); }
                    break;
                case EventType.KeyDown:
                    OnKey(win, e);
                    break;
            }

            if (hit) DrawHandles(win, hover, grid.GridSize);
            if (e.type == EventType.MouseMove) { win.Repaint(); view.Repaint(); }
        }

        // ============ 左键 ============

        private static void OnLeftDown(MapEditorWindow win, Vector2Int hover)
        {
            switch (win.CurrentTool)
            {
                case MapEditTool.PaintTerrain:
                    // 刷地形 = 框刷：拖出矩形，松开时把当前装饰刷子应用到框内格子
                    _drag = DragState.Selecting;
                    _selStart = _selEnd = hover;
                    break;

                case MapEditTool.PlaceDecoration:
                    var dec = win.Session.Decorations.GetDecorationAt(hover);
                    if (dec != null)
                    {
                        _drag = DragState.MovingDecoration;
                        _moveSrc = new Vector2Int(dec.GridX, dec.GridY);
                        _moveType = win.Session.Grid.GetCell(_moveSrc)?.DecorationType ?? 0;
                    }
                    else if (win.PaletteDecoId > 0)
                    {
                        if (MapEditOps.TryPlace(win.Session.Grid, hover, win.PaletteDecoId, out string err))
                            win.Session.RefreshAll();
                        else if (err != null)
                            win.ShowNotification(new GUIContent(err));
                    }
                    break;
            }
        }

        private static void OnLeftDrag(MapEditorWindow win, Vector2Int hover)
        {
            switch (_drag)
            {
                case DragState.Selecting:
                    _selEnd = hover;
                    break;
                case DragState.MovingDecoration:
                    break; // 幽灵跟随悬停格，见 DrawHandles
            }
        }

        private static void OnLeftUp(MapEditorWindow win, Vector2Int hover)
        {
            switch (_drag)
            {
                case DragState.Selecting:
                    FinishSelection(win);
                    int n = MapEditOps.ApplyPaintToSelection(win.Session.Grid, win.Selection, win.PaintDecorationId);
                    if (n > 0) win.Session.RefreshAll(); // 地形装饰变化需重建装饰摆件
                    break;

                case DragState.MovingDecoration:
                    if (hover != _moveSrc && _moveType > 0)
                    {
                        if (MapEditOps.TryMove(win.Session.Grid, _moveSrc, hover, _moveType, out string err))
                            win.Session.RefreshAll();
                        else if (err != null)
                            win.ShowNotification(new GUIContent(err));
                    }
                    break;
            }
            _drag = DragState.None;
        }

        private static void FinishSelection(MapEditorWindow win)
        {
            var sel = win.Selection;
            sel.Clear();
            if (_selStart == _selEnd)
            {
                sel.Add(_selStart);
            }
            else
            {
                // 框选含界外格（开辟地图的前提），不过滤 IsInBounds
                int minX = Mathf.Min(_selStart.x, _selEnd.x), maxX = Mathf.Max(_selStart.x, _selEnd.x);
                int minY = Mathf.Min(_selStart.y, _selEnd.y), maxY = Mathf.Max(_selStart.y, _selEnd.y);
                for (int x = minX; x <= maxX; x++)
                    for (int y = minY; y <= maxY; y++)
                        sel.Add(new Vector2Int(x, y));
            }
            win.Repaint();
        }

        // ============ 右键 ============

        private static void OnRightDown(MapEditorWindow win, Vector2Int hover)
        {
            switch (win.CurrentTool)
            {
                case MapEditTool.PlaceDecoration:
                    if (MapEditOps.TryDelete(win.Session.Grid, win.Session.Decorations, hover))
                        win.Session.RefreshAll();
                    break;
                case MapEditTool.PaintTerrain:
                    win.Selection.Clear();
                    win.Repaint();
                    break;
            }
        }

        // ============ 快捷键 ============

        private static void OnKey(MapEditorWindow win, Event e)
        {
            if (e.control && e.keyCode == KeyCode.Z) { win.DoUndo(); e.Use(); }
            else if (e.control && e.keyCode == KeyCode.Y) { win.DoRedo(); e.Use(); }
            else if ((e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
                     && win.CurrentTool == MapEditTool.PlaceDecoration)
            {
                if (MapEditOps.TryDelete(win.Session.Grid, win.Session.Decorations, win.HoverGrid))
                    win.Session.RefreshAll();
                e.Use();
            }
            else if (e.keyCode == KeyCode.Escape)
            {
                _drag = DragState.None;
                e.Use();
            }
        }

        // ============ Handles 高亮 ============

        private static void DrawHandles(MapEditorWindow win, Vector2Int hover, int gs)
        {
            // 悬停格
            DrawCell(hover, gs, new Color(1f, 1f, 0f, 0.06f), new Color(1f, 1f, 0f, 0.7f));

            // 已选格
            foreach (var pos in win.Selection)
                DrawCell(pos, gs, new Color(0f, 0.8f, 1f, 0.10f), new Color(0f, 0.8f, 1f, 0.5f));

            // 框选中的矩形
            if (_drag == DragState.Selecting)
            {
                int minX = Mathf.Min(_selStart.x, _selEnd.x), maxX = Mathf.Max(_selStart.x, _selEnd.x);
                int minY = Mathf.Min(_selStart.y, _selEnd.y), maxY = Mathf.Max(_selStart.y, _selEnd.y);
                var a = GridMath.LogicToWorld(minX * gs, minY * gs);
                var b = GridMath.LogicToWorld((maxX + 1) * gs, (maxY + 1) * gs);
                Handles.color = new Color(0f, 0.8f, 1f, 0.9f);
                Handles.DrawAAPolyLine(3f,
                    new Vector3(a.x, a.y, 0), new Vector3(b.x, a.y, 0),
                    new Vector3(b.x, b.y, 0), new Vector3(a.x, b.y, 0),
                    new Vector3(a.x, a.y, 0));
            }

            // 放建筑：footprint 幽灵（绿=可放，红=非法；移动中原 footprint 变暗）
            if (win.CurrentTool == MapEditTool.PlaceDecoration)
            {
                bool moving = _drag == DragState.MovingDecoration;
                int type = moving ? _moveType : win.PaletteDecoId;
                if (type > 0)
                {
                    var (sx, sy) = MapEditOps.GetDecoSize(type);
                    var exclude = moving ? _moveSrc : (Vector2Int?)null;
                    bool ok = MapEditOps.ValidatePlacement(win.Session.Grid, hover, sx, sy, exclude) == null;
                    var fill = ok ? new Color(0f, 1f, 0f, 0.18f) : new Color(1f, 0f, 0f, 0.18f);
                    var outline = ok ? new Color(0f, 1f, 0f, 0.8f) : new Color(1f, 0f, 0f, 0.8f);
                    foreach (var pos in MapEditOps.Footprint(hover, sx, sy))
                        DrawCell(pos, gs, fill, outline);

                    if (moving)
                        foreach (var pos in MapEditOps.Footprint(_moveSrc, sx, sy))
                            DrawCell(pos, gs, new Color(1f, 1f, 1f, 0.04f), new Color(1f, 1f, 1f, 0.3f));
                }
            }
        }

        /// <summary>画一个格子的世界矩形（逻辑 y 向下 → 世界 y 向上，翻转只走 GridMath.LogicToWorld）。</summary>
        private static void DrawCell(Vector2Int g, int gs, Color fill, Color outline)
        {
            var a = GridMath.LogicToWorld(g.x * gs, g.y * gs);
            var b = GridMath.LogicToWorld((g.x + 1) * gs, (g.y + 1) * gs);
            var verts = new[]
            {
                new Vector3(a.x, a.y, 0), new Vector3(b.x, a.y, 0),
                new Vector3(b.x, b.y, 0), new Vector3(a.x, b.y, 0),
            };
            Handles.DrawSolidRectangleWithOutline(verts, fill, outline);
        }
    }
}
