using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityClientSharp.Entity;
using UnityClientSharp.Map.Core;
using UnityEditor;
using UnityEngine;

namespace BraveWorld.Editor.MapEditing
{
    public enum MapEditTool { PaintTerrain, PlaceDecoration, Select }
    public enum TerrainPaintMode { TerrainType, TerrainDecoration }

    /// <summary>
    /// 地图编辑器（编辑器原生形态，取代 Godot 游戏内 MapEditor）：
    /// Edit 模式直接编辑 StreamingAssets/Data/maps/&lt;图&gt;/map.json，Scene 视图为画布。
    /// 菜单：Window > BraveWorld > 地图编辑器。交互细节见 MapEditSceneGui / MapEditOps。
    /// </summary>
    public class MapEditorWindow : EditorWindow
    {
        [MenuItem("Window/BraveWorld/地图编辑器")]
        public static void Open() => GetWindow<MapEditorWindow>("地图编辑器");

        public static MapEditorWindow Active { get; private set; }

        private static readonly string[] ToolNames = { "刷地形", "放建筑", "框选" };
        private static readonly string[] PaintModeNames = { "地形类型", "地形装饰" };
        private const string MapsAssetRoot = "Assets/StreamingAssets/Data/maps";

        // ---- 会话与工具状态（SceneGui 读写）----
        private readonly MapEditSession _session = new();
        public MapEditSession Session => _session;
        public MapEditTool CurrentTool = MapEditTool.PaintTerrain;
        public TerrainPaintMode TerrainPaintMode = TerrainPaintMode.TerrainType;
        public int PaintTerrainType;
        public int PaintDecorationId;
        public int PaletteDecoId;
        public readonly HashSet<Vector2Int> Selection = new();
        public Vector2Int HoverGrid = new(-9999, -9999);

        // ---- 窗口私有状态 ----
        private List<string> _maps = new();
        private int _mapIdx;
        private Vector2 _mainScroll, _paletteScroll;
        private string _paletteSearch = "";
        private bool _showCreate, _showRename;
        private string _newMapName = "";
        private int _newMapW = 50, _newMapH = 50;
        private string _renameTo = "";
        private List<EntityProfile> _decoProfiles = new();
        private int[] _terrainTypeIds = new int[0];
        private string[] _terrainTypeNames = new string[0];
        private int _terrainTypeIdx;
        private int[] _terrainDecoIds = new int[0];
        private string[] _terrainDecoNames = new string[0];
        private int _terrainDecoIdx;

        // ============ 生命周期 ============

        private void OnEnable()
        {
            Active = this;
            RefreshMaps();
            RefreshPalettes();
        }

        private void OnDisable()
        {
            if (Active == this) Active = null;
            // 关窗即退出编辑（隔离对象随窗口销毁，不留隐藏残留）
            if (_session.IsActive) _session.Exit();
        }

        private void OnFocus()
        {
            RefreshMaps();
            RefreshPalettes(); // DebugPanel 编辑器可能改过 Profile
        }

        private void RefreshMaps()
        {
            _maps = MapDataManager.GetMapList();
            _maps.Sort();
            if (_session.IsActive)
                _mapIdx = Mathf.Max(0, _maps.IndexOf(_session.MapName));
            _mapIdx = Mathf.Clamp(_mapIdx, 0, Mathf.Max(0, _maps.Count - 1));
        }

        private void RefreshPalettes()
        {
            TerrainConfigUtil.Load();
            EntityProfileManager.EnsureInitialized();

            _terrainTypeIds = TerrainConfigUtil.Configs.Keys.OrderBy(k => k).ToArray();
            _terrainTypeNames = _terrainTypeIds.Select(id => $"{id} {TerrainConfigUtil.GetName(id)}").ToArray();
            _terrainTypeIdx = Mathf.Max(0, Array.IndexOf(_terrainTypeIds, PaintTerrainType));
            if (_terrainTypeIds.Length > 0) PaintTerrainType = _terrainTypeIds[_terrainTypeIdx];

            _decoProfiles = EntityProfileManager.GetProfilesByType("decoration").OrderBy(p => p.Id).ToList();

            var terrainProfiles = _decoProfiles
                .Where(p => p.GetData<CategoryData>("category")?.Category == "Terrain").ToList();
            _terrainDecoIds = new[] { 0 }.Concat(terrainProfiles.Select(p => p.Id)).ToArray();
            _terrainDecoNames = new[] { "清除" }.Concat(terrainProfiles.Select(p => $"[{p.Id}] {p.Name}")).ToArray();
            int defaultDeco = PaintDecorationId > 0 ? PaintDecorationId : BuildingType.GetConfigBaseId(BuildingType.Tree);
            _terrainDecoIdx = Mathf.Max(0, Array.IndexOf(_terrainDecoIds, defaultDeco));
            if (_terrainDecoIds.Length > 0) PaintDecorationId = _terrainDecoIds[_terrainDecoIdx];

            if (_decoProfiles.All(p => p.Id != PaletteDecoId))
            {
                int houseId = BuildingType.GetConfigBaseId(BuildingType.House) + 1;
                PaletteDecoId = _decoProfiles.Any(p => p.Id == houseId)
                    ? houseId
                    : (_decoProfiles.Count > 0 ? _decoProfiles[0].Id : 0);
            }
        }

        // ============ 对外操作（SceneGui 调用）============

        public void DoUndo()
        {
            var cmd = MapEditUndoStack.PopUndo();
            if (cmd == null || !_session.IsActive) return;
            cmd.Undo(_session.Grid);
            _session.RefreshAll();
            Repaint();
        }

        public void DoRedo()
        {
            var cmd = MapEditUndoStack.PopRedo();
            if (cmd == null || !_session.IsActive) return;
            cmd.Redo(_session.Grid);
            _session.RefreshAll();
            Repaint();
        }

        public void SelectAll()
        {
            if (!_session.IsActive) return;
            Selection.Clear();
            foreach (var pos in _session.Grid.GridData.Keys) Selection.Add(pos);
            Repaint();
        }

        /// <summary>Play 前守卫等场景下的静默退出（不弹保存框）。</summary>
        public void ExitSessionSilently()
        {
            _session.Exit();
            Selection.Clear();
            Repaint();
        }

        // ============ OnGUI ============

        private void OnGUI()
        {
            DrawMapManagement();
            EditorGUILayout.Space(4);

            if (!_session.IsActive)
            {
                EditorGUILayout.HelpBox("未在编辑状态。选择地图后点「进入编辑」。", MessageType.Info);
                using (new EditorGUI.DisabledScope(_maps.Count == 0 || Application.isPlaying))
                {
                    if (GUILayout.Button("进入编辑", GUILayout.Height(30)))
                    {
                        _session.Enter(_maps[_mapIdx]);
                        Selection.Clear();
                    }
                }
                if (Application.isPlaying)
                    EditorGUILayout.LabelField("Play 模式下不可编辑，请先退出 Play。", EditorStyles.miniLabel);
                return;
            }

            _mainScroll = EditorGUILayout.BeginScrollView(_mainScroll);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"编辑中: {_session.MapName}（{_session.Grid.GridData.Count} 格）",
                    EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("退出编辑", GUILayout.Width(80))) ExitEditWithPrompt();
            }

            CurrentTool = (MapEditTool)GUILayout.Toolbar((int)CurrentTool, ToolNames);
            EditorGUILayout.Space(4);
            switch (CurrentTool)
            {
                case MapEditTool.PaintTerrain: DrawPaintSection(); break;
                case MapEditTool.PlaceDecoration: DrawPlaceSection(); break;
                case MapEditTool.Select: DrawSelectSection(); break;
            }

            EditorGUILayout.EndScrollView();
            DrawBottomBar();
        }

        // ============ 地图管理 ============

        private void DrawMapManagement()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                if (_maps.Count == 0)
                    EditorGUILayout.Popup("地图", 0, new[] { "（无地图）" });
                else
                    _mapIdx = EditorGUILayout.Popup("地图", _mapIdx, _maps.ToArray());
                if (EditorGUI.EndChangeCheck() && _session.IsActive) SwitchMap();

                if (GUILayout.Button("新建", GUILayout.Width(40))) { _showCreate = !_showCreate; _showRename = false; }
                using (new EditorGUI.DisabledScope(_maps.Count == 0))
                {
                    if (GUILayout.Button("重命名", GUILayout.Width(50)))
                    {
                        _showRename = !_showRename;
                        _showCreate = false;
                        _renameTo = _maps.Count > 0 ? _maps[_mapIdx] : "";
                    }
                    if (GUILayout.Button("删除", GUILayout.Width(40))) DeleteCurrentMap();
                }
            }

            if (_showCreate) DrawCreatePanel();
            if (_showRename) DrawRenamePanel();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!_session.IsActive))
                {
                    if (GUILayout.Button("保存", GUILayout.Width(50)))
                    {
                        if (_session.Save())
                            ShowNotification(new GUIContent($"已保存 {_session.MapName}/map.json"));
                        else
                            ShowNotification(new GUIContent("保存失败，见 Console"));
                    }
                    if (GUILayout.Button("导出", GUILayout.Width(50))) ExportJson();
                    if (GUILayout.Button("同步到服务器", GUILayout.Width(90))) SyncToServer();
                }
                if (GUILayout.Button("导入", GUILayout.Width(50))) ImportJson();
            }
        }

        private void DrawCreatePanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _newMapName = EditorGUILayout.TextField("地图名称", _newMapName);
                _newMapW = EditorGUILayout.IntSlider("宽度", _newMapW, 10, 200);
                _newMapH = EditorGUILayout.IntSlider("高度", _newMapH, 10, 200);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("创建")) CreateMap();
                    if (GUILayout.Button("取消")) _showCreate = false;
                }
            }
        }

        private void DrawRenamePanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _renameTo = EditorGUILayout.TextField("新名称", _renameTo);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("确认重命名")) RenameCurrentMap();
                    if (GUILayout.Button("取消")) _showRename = false;
                }
            }
        }

        private static bool ValidateMapName(string name, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(name)) { error = "名称不能为空"; return false; }
            if (System.Text.RegularExpressions.Regex.IsMatch(name, @"[\\/:*?""<>|]"))
            { error = "名称含非法字符"; return false; }
            if (MapDataManager.MapExists(name)) { error = $"地图 '{name}' 已存在"; return false; }
            return true;
        }

        private void CreateMap()
        {
            string name = _newMapName.Trim();
            if (!ValidateMapName(name, out string error))
            {
                ShowNotification(new GUIContent(error));
                return;
            }
            if (!MapDataManager.CreateNewMap(name, _newMapW, _newMapH))
            {
                ShowNotification(new GUIContent("创建失败，见 Console"));
                return;
            }
            AssetDatabase.Refresh();
            _showCreate = false;
            RefreshMaps();
            _mapIdx = Mathf.Max(0, _maps.IndexOf(name));
            if (_session.IsActive) _session.Enter(name);
            ShowNotification(new GUIContent($"地图 '{name}' 已创建"));
        }

        private void RenameCurrentMap()
        {
            string oldName = _maps[_mapIdx];
            string newName = _renameTo.Trim();
            if (newName == oldName) { _showRename = false; return; }
            if (!ValidateMapName(newName, out string error))
            {
                ShowNotification(new GUIContent(error));
                return;
            }

            // AssetDatabase 移动（连带 .meta），再回写 display_name
            string moveErr = AssetDatabase.MoveAsset($"{MapsAssetRoot}/{oldName}", $"{MapsAssetRoot}/{newName}");
            if (!string.IsNullOrEmpty(moveErr))
            {
                ShowNotification(new GUIContent("重命名失败: " + moveErr));
                return;
            }
            var data = MapDataManager.LoadMapFromJson(newName, out var b, out var s, out _);
            MapDataManager.SaveMapToJson(newName, data, newName, b, s);

            bool wasEditing = _session.IsActive && _session.MapName == oldName;
            _showRename = false;
            RefreshMaps();
            _mapIdx = Mathf.Max(0, _maps.IndexOf(newName));
            if (wasEditing) _session.Enter(newName);
            ShowNotification(new GUIContent($"已重命名: {oldName} → {newName}"));
        }

        private void DeleteCurrentMap()
        {
            if (_maps.Count == 0) return;
            string name = _maps[_mapIdx];
            if (_maps.Count <= 1)
            {
                ShowNotification(new GUIContent("至少保留一张地图"));
                return;
            }
            if (!EditorUtility.DisplayDialog("删除地图", $"确定删除地图 '{name}' 吗？此操作不可撤销！", "删除", "取消"))
                return;

            bool wasEditing = _session.IsActive && _session.MapName == name;
            if (wasEditing) _session.Exit();
            AssetDatabase.DeleteAsset($"{MapsAssetRoot}/{name}");
            RefreshMaps();
            if (wasEditing && _maps.Count > 0) _session.Enter(_maps[_mapIdx]);
            ShowNotification(new GUIContent($"地图 '{name}' 已删除（服务器目录请另行清理）"));
        }

        private void SwitchMap()
        {
            string name = _maps[_mapIdx];
            if (name == _session.MapName) return;
            if (!EditorUtility.DisplayDialog("切换地图", $"切换到 '{name}'？未保存的修改将丢失。", "切换", "取消"))
            {
                _mapIdx = Mathf.Max(0, _maps.IndexOf(_session.MapName));
                return;
            }
            _session.Enter(name);
            Selection.Clear();
        }

        private void ExitEditWithPrompt()
        {
            int r = EditorUtility.DisplayDialogComplex("退出地图编辑",
                $"是否保存对 '{_session.MapName}' 的修改？", "保存并退出", "取消", "放弃修改");
            if (r == 0) { _session.Save(); ExitSessionSilently(); }
            else if (r == 2) { ExitSessionSilently(); }
        }

        private void ExportJson()
        {
            string src = Path.Combine(MapDataManager.MapsRoot, _session.MapName, MapDataManager.JsonFilename);
            string dst = EditorUtility.SaveFilePanel("导出 map.json", "", _session.MapName + ".json", "json");
            if (string.IsNullOrEmpty(dst)) return;
            File.Copy(src, dst, true);
            ShowNotification(new GUIContent("已导出: " + dst));
        }

        private void ImportJson()
        {
            string src = EditorUtility.OpenFilePanel("导入 map.json", "", "json");
            if (string.IsNullOrEmpty(src)) return;
            string name = _session.IsActive ? _session.MapName : (_maps.Count > 0 ? _maps[_mapIdx] : null);
            if (name == null) return;
            if (!EditorUtility.DisplayDialog("导入地图", $"将用 '{Path.GetFileName(src)}' 覆盖地图 '{name}'？", "覆盖", "取消"))
                return;
            File.Copy(src, Path.Combine(MapDataManager.MapsRoot, name, MapDataManager.JsonFilename), true);
            if (_session.IsActive) _session.Enter(name);
            ShowNotification(new GUIContent("导入完成"));
        }

        /// <summary>拷贝当前地图到 servercsharp/data/maps 并更新 map_registry.json（服务器重启后生效）。</summary>
        private void SyncToServer()
        {
            try
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
                string name = _session.MapName;
                var g = _session.Grid;

                string serverMapDir = Path.Combine(repoRoot, "servercsharp", "data", "maps", name);
                Directory.CreateDirectory(serverMapDir);
                File.Copy(Path.Combine(MapDataManager.MapsRoot, name, MapDataManager.JsonFilename),
                    Path.Combine(serverMapDir, MapDataManager.JsonFilename), true);

                string regPath = Path.Combine(repoRoot, "servercsharp", "data", "map_registry.json");
                var arr = File.Exists(regPath) ? JArray.Parse(File.ReadAllText(regPath)) : new JArray();
                var obj = new JObject
                {
                    ["map_name"] = name,
                    ["display_name"] = g.DisplayName,
                    ["width"] = g.MapBounds.width,
                    ["height"] = g.MapBounds.height,
                    ["spawn_x"] = g.Spawn.x,
                    ["spawn_y"] = g.Spawn.y,
                };
                var existing = arr.FirstOrDefault(t => (string)t["map_name"] == name);
                if (existing != null) existing.Replace(obj);
                else arr.Add(obj);
                File.WriteAllText(regPath, arr.ToString(Newtonsoft.Json.Formatting.Indented), new UTF8Encoding(false));

                ShowNotification(new GUIContent("已同步到服务器（需重启 GameServer 生效）"));
            }
            catch (Exception ex)
            {
                Debug.LogError("[MapEditor] 同步到服务器失败: " + ex);
                ShowNotification(new GUIContent("同步失败，见 Console"));
            }
        }

        // ============ 工具区 ============

        private void DrawPaintSection()
        {
            TerrainPaintMode = (TerrainPaintMode)GUILayout.Toolbar((int)TerrainPaintMode, PaintModeNames);
            if (TerrainPaintMode == TerrainPaintMode.TerrainType)
            {
                if (_terrainTypeIds.Length > 0)
                {
                    _terrainTypeIdx = EditorGUILayout.Popup("类型", _terrainTypeIdx, _terrainTypeNames);
                    PaintTerrainType = _terrainTypeIds[_terrainTypeIdx];
                }
            }
            else
            {
                if (_terrainDecoIds.Length > 0)
                {
                    _terrainDecoIdx = EditorGUILayout.Popup("装饰", _terrainDecoIdx, _terrainDecoNames);
                    PaintDecorationId = _terrainDecoIds[_terrainDecoIdx];
                }
            }
            EditorGUILayout.LabelField("左键按住直接刷；一次笔画 = 一步撤销；「框选」里可批量应用。", EditorStyles.miniLabel);
        }

        private void DrawPlaceSection()
        {
            _paletteSearch = EditorGUILayout.TextField("搜索", _paletteSearch);
            _paletteScroll = EditorGUILayout.BeginScrollView(_paletteScroll, GUILayout.MinHeight(120), GUILayout.MaxHeight(300));
            foreach (var p in _decoProfiles)
            {
                if (!string.IsNullOrEmpty(_paletteSearch)
                    && !p.Name.Contains(_paletteSearch, StringComparison.OrdinalIgnoreCase)
                    && !p.Id.ToString().Contains(_paletteSearch))
                    continue;

                bool sel = p.Id == PaletteDecoId;
                using (new EditorGUILayout.HorizontalScope())
                {
                    var rect = GUILayoutUtility.GetRect(16, 16, GUILayout.Width(16));
                    var app = p.GetData<AppearanceData>("appearance");
                    EditorGUI.DrawRect(rect, app?.BgColor ?? Color.gray);
                    var (sx, sy) = MapEditOps.GetDecoSize(p.Id);
                    string label = (sel ? "● " : "　") + $"{p.Name} [{p.Id}] {sx}x{sy}";
                    if (GUILayout.Button(label, EditorStyles.label)) PaletteDecoId = p.Id;
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.LabelField("左键点击放置；拖动已放置建筑移动；右键/Delete 删除。", EditorStyles.miniLabel);
        }

        private void DrawSelectSection()
        {
            EditorGUILayout.LabelField($"已选中 {Selection.Count} 格", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("全选")) SelectAll();
                if (GUILayout.Button("反选"))
                {
                    var inv = new HashSet<Vector2Int>();
                    foreach (var pos in _session.Grid.GridData.Keys)
                        if (!Selection.Contains(pos)) inv.Add(pos);
                    Selection.Clear();
                    Selection.UnionWith(inv);
                }
                if (GUILayout.Button("清除")) Selection.Clear();
            }
            using (new EditorGUI.DisabledScope(Selection.Count == 0))
            {
                if (GUILayout.Button("应用当前刷子到选区"))
                {
                    int n = MapEditOps.ApplyPaintToSelection(_session.Grid, Selection,
                        TerrainPaintMode == TerrainPaintMode.TerrainType, PaintTerrainType, PaintDecorationId);
                    if (n > 0) _session.RefreshAll();
                    ShowNotification(new GUIContent($"已应用到 {n} 格"));
                }
                if (GUILayout.Button("开辟地图（选区界外格并入）"))
                {
                    int outCount = Selection.Count(pos => !_session.Grid.IsInBounds(pos));
                    if (outCount == 0)
                    {
                        ShowNotification(new GUIContent("选区内没有界外格"));
                    }
                    else if (EditorUtility.DisplayDialog("开辟地图", $"将把选区内 {outCount} 个界外格并入地图（默认普通地形）。", "开辟", "取消"))
                    {
                        int n = MapEditOps.ExtendBySelection(_session.Grid, Selection);
                        if (n > 0)
                        {
                            Selection.Clear();
                            _session.RefreshAll();
                        }
                        ShowNotification(new GUIContent($"已扩展 {n} 格，bounds={_session.Grid.MapBounds}"));
                    }
                }
            }
            EditorGUILayout.LabelField("左键拖框选（Ctrl 加选）；右键清除选区。", EditorStyles.miniLabel);
        }

        private void DrawBottomBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                using (new EditorGUI.DisabledScope(MapEditUndoStack.UndoCount == 0))
                    if (GUILayout.Button($"撤销({MapEditUndoStack.UndoCount})", GUILayout.Width(70))) DoUndo();
                using (new EditorGUI.DisabledScope(MapEditUndoStack.RedoCount == 0))
                    if (GUILayout.Button($"重做({MapEditUndoStack.RedoCount})", GUILayout.Width(70))) DoRedo();
                GUILayout.FlexibleSpace();
                string hover = HoverGrid.x < -999 ? "—" : $"({HoverGrid.x},{HoverGrid.y})";
                GUILayout.Label("悬停: " + hover, EditorStyles.miniLabel);
            }
        }
    }
}
