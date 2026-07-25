using System.Collections.Generic;
using TMPro;
using UnityClientSharp.Map.Core;
using UnityEngine;

namespace UnityClientSharp.Map.Rendering
{
    /// <summary>
    /// 编辑态文字标注层：用 TextMeshPro 世界空间对象池渲染地形名 / 格子坐标 / UID。
    /// 移植自 Godot GridManager._Draw 中的 DrawTerrainLabels / DrawGridCoords / DrawCellUids。
    /// 显示门槛与 Godot 对齐：地形名仅编辑模式且 zoom>=0.5；坐标/UID 由各自开关控制、无 zoom 门槛。
    /// 中文地形名使用运行时创建的 OS CJK 字体资产（TMP 默认 LiberationSans 无中文字形）。
    /// </summary>
    [RequireComponent(typeof(GridManager))]
    [RequireComponent(typeof(MapRenderer))]
    public class MapLabelOverlay : MonoBehaviour
    {
        private GridManager _gm;
        private MapRenderer _renderer;
        private Transform _container;

        private readonly List<TextMeshPro> _pool = new();
        private int _activeCount;

        private const int Cap = 4000; // 坐标/UID 标注的格子数上限，避免海量文本

        private void Awake()
        {
            _gm = GetComponent<GridManager>();
            _renderer = GetComponent<MapRenderer>();
            _container = new GameObject("Labels").transform;
            _container.SetParent(transform, false);
        }

        private TextMeshPro GetLabel(int i)
        {
            if (i < _pool.Count) return _pool[i];
            var go = new GameObject("Label" + i);
            go.transform.SetParent(_container, false);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.alignment = TextAlignmentOptions.Center;
            FontUtil.SetWorldFontSize(tmp, 14);
            tmp.color = Color.white;
            FontUtil.ApplyCjkFont(tmp);
            _pool.Add(tmp);
            return tmp;
        }

        private void Update()
        {
            if (_gm == null) return;
            float zoom = MapCameraController.Instance != null ? MapCameraController.Instance.Zoom : 1f;
            // 门槛与 Godot GridManager._Draw 对齐：地形名标签仅编辑模式且 zoom>=0.5；坐标/UID 不受此限
            bool terrainLabels = _gm.IsEditMode && _gm.ShowTerrainLabels && zoom >= 0.5f;
            bool coordsOrUid = (_gm.ShowGridCoords || _gm.ShowCellUids) && _gm.GridData.Count <= Cap;
            if (!terrainLabels && !coordsOrUid)
            {
                if (_activeCount > 0) HideAll();
                return;
            }
            Rebuild(terrainLabels, coordsOrUid);
        }

        private void HideAll()
        {
            for (int i = 0; i < _activeCount; i++)
                _pool[i].gameObject.SetActive(false);
            _activeCount = 0;
        }

        private void Rebuild(bool terrainLabels, bool coordsOrUid)
        {
            int idx = 0;

            foreach (var cell in _gm.GridData.Values)
            {
                if (terrainLabels && cell.TerrainType != 0)
                {
                    string name = cell.GetTerrainName();
                    if (!string.IsNullOrEmpty(name))
                        Place(ref idx, cell, name, Color.white);
                }

                if (coordsOrUid)
                {
                    if (_gm.ShowGridCoords)
                        Place(ref idx, cell, $"x:{cell.Pos.x}\ny:{cell.Pos.y}", new Color(0.8f, 0.8f, 0.8f, 0.7f));
                    if (_gm.ShowCellUids && !string.IsNullOrEmpty(cell.Uid))
                        Place(ref idx, cell, cell.Uid, new Color(1f, 0.9f, 0.3f, 0.85f));
                }
            }

            for (int i = idx; i < _activeCount; i++)
                _pool[i].gameObject.SetActive(false);
            _activeCount = idx;
        }

        private void Place(ref int idx, GridCell cell, string text, Color color)
        {
            var tmp = GetLabel(idx);
            if (!tmp.gameObject.activeSelf) tmp.gameObject.SetActive(true);
            tmp.text = text;
            tmp.color = color;
            tmp.transform.localPosition = _renderer.CellWorldPos(cell.Pos.x, cell.Pos.y);
            idx++;
        }
    }
}
