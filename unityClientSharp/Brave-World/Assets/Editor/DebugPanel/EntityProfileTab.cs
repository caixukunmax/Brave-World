using System.Collections.Generic;
using System.Linq;
using UnityClientSharp.Entity;
using UnityClientSharp.Map.Core;
using UnityEditor;
using UnityEngine;

namespace BraveWorld.Editor
{
    /// <summary>
    /// 实体配置 Tab：Profile 列表（类型筛选/增删复制）+ 组件检查器。
    /// 对应已删除 UGUI 版的「实体」+「建筑工坊」两个 Tab（decoration 只是类型筛选的一项）。
    /// UI 始终代表 Profile 配置数据，禁止用实体运行时状态覆盖（仓库记忆 79121038）。
    /// </summary>
    public class EntityProfileTab
    {
        private static readonly string[] TypeNames = { "全部", "player", "monster", "npc", "decoration" };

        private readonly DebugPanelWindow _host;
        private int _typeIdx;
        private List<EntityProfile> _profiles = new();
        private int _selectedId = -1;
        private EntityProfile _current;
        private Vector2 _listScroll, _detailScroll;
        private readonly Dictionary<string, bool> _foldouts = new();
        private int _addIdx;
        private bool _needRefresh = true;

        public EntityProfileTab(DebugPanelWindow host) { _host = host; }

        /// <summary>Play 切换/磁盘重载后调用：丢弃全部缓存引用。</summary>
        public void Invalidate()
        {
            _needRefresh = true;
            _current = null;
            _selectedId = -1;
        }

        public void RefreshIfNeeded()
        {
            if (!_needRefresh) return;
            _needRefresh = false;
            RefreshList();
        }

        private void RefreshList()
        {
            string type = TypeNames[_typeIdx];
            _profiles = (type == "全部"
                ? EntityProfileManager.GetAllProfiles()
                : EntityProfileManager.GetProfilesByType(type)).ToList();

            if (_profiles.Count > 0)
            {
                if (_profiles.All(p => p.Id != _selectedId))
                    _selectedId = _profiles[0].Id;
                _current = EntityProfileManager.GetProfile(_selectedId);
            }
            else
            {
                _current = null;
                _selectedId = -1;
            }
        }

        public void OnGUI()
        {
            RefreshIfNeeded();
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawList();
                DrawDetail();
            }
        }

        private void DrawList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(220)))
            {
                EditorGUI.BeginChangeCheck();
                _typeIdx = EditorGUILayout.Popup("类型筛选", _typeIdx, TypeNames);
                if (EditorGUI.EndChangeCheck())
                {
                    _selectedId = -1;
                    RefreshList();
                }

                _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));
                foreach (var p in _profiles)
                {
                    bool selected = p.Id == _selectedId;
                    string text = (selected ? "● " : "　") + $"[{p.Id}] {p.DisplayName}";
                    if (GUILayout.Button(text, EditorStyles.label))
                    {
                        _selectedId = p.Id;
                        _current = p;
                    }
                }
                EditorGUILayout.EndScrollView();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("新增")) OnNew();
                    using (new EditorGUI.DisabledScope(_current == null))
                    {
                        if (GUILayout.Button("复制")) OnClone();
                        if (GUILayout.Button("删除")) OnDelete();
                    }
                }
            }
        }

        private void DrawDetail()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                if (_current == null)
                {
                    EditorGUILayout.LabelField("无 Profile（左下角「新增」创建）", EditorStyles.centeredGreyMiniLabel);
                    return;
                }

                _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);

                EditorGUI.BeginChangeCheck();
                string newName = EditorGUILayout.TextField("名称", _current.Name);
                if (EditorGUI.EndChangeCheck())
                {
                    _current.Name = newName;
                    _host.MarkDirty();
                }
                EditorGUILayout.LabelField("ID", _current.Id.ToString());
                EditorGUILayout.LabelField("实体类型", _current.EntityType);

                DrawPreview();

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("组件", EditorStyles.boldLabel);
                // ToList：遍历中可能移除组件
                foreach (string compName in _current.ComponentNames.ToList())
                    DrawComponent(compName);

                DrawAddComponent();
                EditorGUILayout.EndScrollView();
            }
        }

        /// <summary>
        /// 编辑模式外观预览：贴图走 EntityBodySprite、尺寸/字号/行位置走 EntityAppearanceLayout，
        /// 与运行时 MapDecoration / EntityVisualBase 完全同源，改字段即时可见，无需进 Play。
        /// 字体也用游戏同款内置字体资产（NotoSansSC-VF/SimHei），避免预览与运行时字形不一致。
        /// </summary>
        private static Font s_previewFont;
        private static Font PreviewFont
        {
            get
            {
                if (s_previewFont == null)
                    s_previewFont = Resources.Load<Font>("Fonts/NotoSansSC-VF") ?? Resources.Load<Font>("Fonts/SimHei");
                return s_previewFont;
            }
        }

        private void DrawPreview()
        {
            var app = _current.GetData<AppearanceData>("appearance");
            if (app == null || _current.IsComponentDisabled("appearance")) return;
            var labels = _current.GetData<LabelGroupData>("labels");
            bool labelsOn = labels != null && !_current.IsComponentDisabled("labels");

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("预览", EditorStyles.boldLabel);
            Rect box = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.Height(200), GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(box, new Color(0.16f, 0.16f, 0.16f));

            const int grid = EntityAppearanceLayout.DefaultGridSize;
            EntityAppearanceLayout.ComputeBody(grid, Mathf.Max(1, app.SizeX), Mathf.Max(1, app.SizeY),
                app.VisualSizeScale, app.BorderWidthScale, out int outerW, out int outerH, out int border);

            var sprite = EntityBodySprite.Get(outerW, outerH, border, app.CornerRadius,
                app.BorderColor, app.BgColor, app.BgOpacity);

            float fit = Mathf.Min((box.width - 16f) / outerW, (box.height - 16f) / outerH);
            var bodyRect = new Rect(box.center.x - outerW * fit / 2f, box.center.y - outerH * fit / 2f,
                outerW * fit, outerH * fit);
            GUI.DrawTexture(bodyRect, sprite.texture, ScaleMode.StretchToFill, true);

            if (!labelsOn) return;

            for (int i = 0; i < labels.Count; i++)
            {
                if (!labels.Visible[i]) continue;
                string text = labels.ContentPreview[i];
                if (string.IsNullOrEmpty(text)) continue;
                int fs = EntityAppearanceLayout.RowFontSize(grid, labels, i, app.VisualSizeScale, app.BorderWidthScale);
                float lh = EntityAppearanceLayout.LineHeight(fs) * fit;
                // 行中心：实体中心 - 世界Y偏移（上为正）× 缩放；GUI y 向下
                float centerX = bodyRect.center.x + (labels.CenterX[i] ? 0f : labels.XOffset[i] * fit);
                float centerY = bodyRect.center.y - EntityAppearanceLayout.LineWorldOffsetY(i, fs, labels.YOffset[i], labels.Count) * fit;
                var style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Max(6, Mathf.RoundToInt(fs * fit)),
                    fontStyle = (labels.Bold ? FontStyle.Bold : FontStyle.Normal)
                                | (labels.Italic ? FontStyle.Italic : FontStyle.Normal),
                };
                if (PreviewFont != null) style.font = PreviewFont;
                style.normal.textColor = EntityAppearanceLayout.RowTextColor(labels, i);
                var lineRect = new Rect(centerX - box.width / 2f, centerY - lh / 2f, box.width, lh);
                if (labels.Shadow)
                {
                    var shadowStyle = new GUIStyle(style);
                    shadowStyle.normal.textColor = new Color(0, 0, 0, 0.8f);
                    GUI.Label(new Rect(lineRect.x + 1, lineRect.y + 1, lineRect.width, lineRect.height), text, shadowStyle);
                }
                GUI.Label(lineRect, text, style);
            }
        }

        private void DrawComponent(string compName)
        {
            var entry = EditorComponentRegistry.Get(compName);
            if (!_foldouts.TryGetValue(compName, out bool open)) open = true;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    open = EditorGUILayout.Foldout(open, entry.DisplayName, true);
                    _foldouts[compName] = open;
                    GUILayout.FlexibleSpace();

                    EditorGUI.BeginChangeCheck();
                    bool disabled = GUILayout.Toggle(_current.IsComponentDisabled(compName), "停用",
                        EditorStyles.miniButton, GUILayout.Width(44));
                    if (EditorGUI.EndChangeCheck())
                    {
                        _current.SetComponentDisabled(compName, disabled);
                        _host.MarkDirty();
                    }
                    if (GUILayout.Button("移除", EditorStyles.miniButton, GUILayout.Width(44)))
                    {
                        _current.RemoveComponent(compName);
                        _host.MarkDirty();
                        return; // 集合已变，等下一帧重画
                    }
                }

                if (open)
                {
                    var data = _current.GetData(compName);
                    EditorGUI.BeginChangeCheck();
                    entry.Draw(data);
                    if (EditorGUI.EndChangeCheck()) _host.MarkDirty();
                }
            }
        }

        private void DrawAddComponent()
        {
            var available = EditorComponentRegistry.GetAvailableFor(_current);
            if (available.Count == 0)
            {
                EditorGUILayout.LabelField("（无可用组件）", EditorStyles.miniLabel);
                return;
            }

            _addIdx = Mathf.Clamp(_addIdx, 0, available.Count - 1);
            string[] names = available.Select(e => e.DisplayName).ToArray();
            using (new EditorGUILayout.HorizontalScope())
            {
                _addIdx = EditorGUILayout.Popup("添加组件", _addIdx, names);
                if (GUILayout.Button("添加", GUILayout.Width(50)))
                {
                    var e = available[_addIdx];
                    _current.SetData(e.Name, e.CreateDefault());
                    _host.MarkDirty();
                }
            }
        }

        private void OnNew()
        {
            string type = TypeNames[_typeIdx] == "全部" ? "decoration" : TypeNames[_typeIdx];
            int id = EntityProfileManager.AllocateNextId();
            EntityProfile p = type switch
            {
                "player" => EntityProfile.CreatePlayerDefault(id),
                "monster" => EntityProfile.CreateMonsterDefault(id),
                "npc" => EntityProfile.CreateNpcDefault(id),
                _ => EntityProfile.CreateDecorationDefault(id, "New" + id, "新建筑", BuildingType.House,
                    new Color(0.5f, 0.5f, 0.5f, 0.9f), new Color(0.3f, 0.3f, 0.3f), true, 1, 1, "Building"),
            };
            EntityProfileManager.AddProfile(p);
            _host.MarkDirty();
            _selectedId = id;
            RefreshList();
        }

        private void OnClone()
        {
            if (_current == null) return;
            int id = EntityProfileManager.AllocateNextId();
            var clone = EntityProfileManager.CloneProfile(_current, id);
            EntityProfileManager.AddProfile(clone);
            _host.MarkDirty();
            _selectedId = id;
            RefreshList();
        }

        private void OnDelete()
        {
            if (_current == null) return;
            EntityProfileManager.RemoveProfile(_current.Id);
            _host.MarkDirty();
            _current = null;
            _selectedId = -1;
            RefreshList();
        }
    }
}
