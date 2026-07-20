using UnityClientSharp.Entity;
using UnityClientSharp.Map.Core;
using UnityClientSharp.Map.Rendering;
using UnityEditor;
using UnityEngine;

namespace BraveWorld.Editor.MapEditing
{
    /// <summary>
    /// 地图编辑会话：在当前场景创建 HideAndDontSave 的隔离编辑根（不进 Hierarchy、不落场景）。
    /// Edit 模式 MonoBehaviour.Awake 不执行（AGENTS.md），全部手动初始化。
    /// 域重载后残留由 MapEditSceneGui 的 [InitializeOnLoadMethod] 清理。
    /// </summary>
    public class MapEditSession
    {
        public const string RootName = "MapEditRoot";

        public GridManager Grid { get; private set; }
        public MapRenderer Renderer { get; private set; }
        public MapDecorationManager Decorations { get; private set; }
        public bool IsActive => Grid != null;
        public string MapName => IsActive ? Grid.CurrentMapName : "";

        /// <summary>进入编辑：加载地图并创建隔离渲染/装饰对象。</summary>
        public void Enter(string mapName)
        {
            Exit();
            TerrainConfigUtil.Load();
            EntityProfileManager.EnsureInitialized(); // 装饰 palette/外观数据

            var root = new GameObject(RootName) { hideFlags = HideFlags.HideAndDontSave };

            Grid = root.AddComponent<GridManager>();
            Grid.IsEditMode = true;
            Grid.ShowOutsideMapGray = true;
            Grid.LoadMap(mapName);

            Renderer = root.AddComponent<MapRenderer>();
            Renderer.EnsureInit();
            Renderer.RefreshFrame(1f);

            Decorations = root.AddComponent<MapDecorationManager>();
            Decorations.GridSize = Grid.GridSize;
            Decorations.SpawnEditable = true; // 编辑器中出生点也可见
            Decorations.SpawnDecorations(Grid.GridData);

            ApplyHideFlags();
            MapEditUndoStack.Clear();
            SceneView.RepaintAll();
            Debug.Log($"[MapEditSession] 进入编辑: {mapName} 格子数={Grid.GridData.Count} bounds={Grid.MapBounds}");
        }

        /// <summary>轻量刷新：只重建地形遮罩并推给材质（刷地形过程中每格调用）。</summary>
        public void RefreshMask()
        {
            if (!IsActive) return;
            Grid.NotifyTerrainChanged();
            Renderer.RefreshFrame(1f);
            SceneView.RepaintAll();
        }

        /// <summary>全量刷新：遮罩 + 重建装饰摆件（装饰变化或操作结束时调用）。</summary>
        public void RefreshAll()
        {
            if (!IsActive) return;
            RefreshMask();
            Decorations.ClearDecorations();
            Decorations.SpawnDecorations(Grid.GridData);
            ApplyHideFlags(); // 新生成的装饰默认无隐藏标记
        }

        /// <summary>保存当前地图到 map.json（保留 LoadMap 时的 display_name/spawn）。</summary>
        public bool Save() => IsActive && Grid.SaveCurrentMap();

        /// <summary>退出编辑：销毁隔离对象（保存与否由调用方决定）。</summary>
        public void Exit()
        {
            if (!IsActive) return;
            Object.DestroyImmediate(Grid.gameObject); // 级联销毁 MapQuad/装饰子物体
            Grid = null;
            Renderer = null;
            Decorations = null;
            MapEditUndoStack.Clear();
            SceneView.RepaintAll();
        }

        /// <summary>整棵子树打 HideAndDontSave（MapQuad/装饰均为运行期新建，默认无标记）。</summary>
        private void ApplyHideFlags()
        {
            if (Grid == null) return;
            foreach (var t in Grid.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.HideAndDontSave;
        }

        /// <summary>域重载/异常退出后残留的同名隔离根清理（EditorApplication.delayCall 中调用）。</summary>
        public static void CleanupOrphans()
        {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.name == RootName && go.hideFlags == HideFlags.HideAndDontSave)
                    Object.DestroyImmediate(go);
            }
        }
    }
}
