using System.Collections.Generic;
using UnityEngine;
using UnityClientSharp.Entity;
using UnityClientSharp.Map.Core;

namespace UnityClientSharp.DebugPanel
{
    /// <summary>
    /// 实体 Tab（配置编辑，移植自 Godot DebugPanelEntityTab）。
    /// 偏配置编辑、与运行时世界对象解耦：UI 始终代表 Profile 配置数据，
    /// 禁止用实体运行时状态覆盖（仓库记忆 79121038）。
    /// 依赖 ComponentRegistry（第二轮决策已含入迁移）做组件增删/启停/编辑。
    /// </summary>
    public class DebugPanelEntityTab : DebugPanelTab
    {
        public override string TabName => "实体";
        private readonly DebugPanel _host;
        private RectTransform _root;
        private RectTransform _componentArea;
        private TMPro.TMP_Dropdown _typeDd, _profileDd, _addDd;
        private readonly string[] _entityTypes = { "全部", "player", "monster", "npc", "decoration" };
        private int[] _profileIds;
        private string[] _addNames;
        private EntityProfile _current;

        public DebugPanelEntityTab(DebugPanel host) { _host = host; }

        protected override void BuildContent(RectTransform root)
        {
            _root = root;
            _typeDd = DebugPanelUI.AddDropdown(root, "类型筛选", _entityTypes, 0, _ => RebuildProfileList());
            _profileDd = DebugPanelUI.AddDropdown(root, "配置", new string[0], 0, _ => SelectProfile());

            var crud = DebugPanelUI.Row(root);
            DebugPanelUI.AddButton(crud, "新增", OnNew);
            DebugPanelUI.AddButton(crud, "复制", OnClone);
            DebugPanelUI.AddButton(crud, "删除", OnDelete);

            _addDd = DebugPanelUI.AddDropdown(root, "添加组件", new string[0], 0, _ => OnAddComponent());
            _componentArea = DebugPanelUI.Section(root, "组件");

            RebuildProfileList();
        }

        private static List<TMPro.TMP_Dropdown.OptionData> ToOpts(string[] arr)
        {
            var l = new List<TMPro.TMP_Dropdown.OptionData>();
            foreach (var a in arr) l.Add(new TMPro.TMP_Dropdown.OptionData(a));
            return l;
        }

        private void RebuildProfileList()
        {
            string type = _entityTypes[_typeDd.value];
            var profiles = type == "全部" ? EntityProfileManager.GetAllProfiles() : EntityProfileManager.GetProfilesByType(type);
            var list = new List<EntityProfile>(profiles);
            _profileIds = new int[list.Count];
            var opts = new string[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                _profileIds[i] = list[i].Id;
                opts[i] = $"[{list[i].Id}] {list[i].Name}";
            }
            _profileDd.options = ToOpts(opts);
            _profileDd.value = 0;
            _profileDd.RefreshShownValue();
            if (list.Count > 0) SelectProfileById(_profileIds[0]);
            else { _current = null; ClearComponents(); RebuildAddDropdown(); }
        }

        private void SelectProfile() => SelectProfileById(_profileIds[_profileDd.value]);

        private void SelectProfileById(int id)
        {
            _current = EntityProfileManager.GetProfile(id);
            BuildComponentRows();
        }

        private void ClearComponents()
        {
            if (_componentArea == null) return;
            for (int i = _componentArea.childCount - 1; i >= 0; i--)
                Object.Destroy(_componentArea.GetChild(i).gameObject);
        }

        private void BuildComponentRows()
        {
            ClearComponents();
            if (_current == null) { RebuildAddDropdown(); return; }
            foreach (var name in _current.ComponentNames)
                BuildComponentRow(name);
            RebuildAddDropdown();
        }

        private void BuildComponentRow(string compName)
        {
            var meta = ComponentRegistry.GetComponentMeta(compName);
            var row = DebugPanelUI.Section(_componentArea, $"{meta.Icon} {meta.DisplayName}");

            var header = DebugPanelUI.Row(row);
            DebugPanelUI.AddToggle(header, "停用", _current.IsComponentDisabled(compName), isOn =>
            {
                _current.SetComponentDisabled(compName, isOn);
                _host.SaveNow();
            });
            DebugPanelUI.AddButton(header, "移除", () =>
            {
                _current.RemoveComponent(compName);
                _host.SaveNow();
                BuildComponentRows();
            });

            var control = ComponentRegistry.Create(compName);
            if (control == null) return;
            control.BuildUI(row);
            control.ConnectSignals(() =>
            {
                var data = control.SyncToData();
                _current.SetData(compName, data);
                _host.ScheduleSave();
            });
            var existing = _current.GetData(compName);
            if (existing != null) control.SyncFromData(existing);
        }

        private void RebuildAddDropdown()
        {
            var names = new List<string>();
            _addNames = new string[0];
            if (_current != null)
            {
                foreach (var (name, display) in ComponentRegistry.GetComponentsForType(_current.EntityType))
                {
                    if (_current.HasComponent(name)) continue;
                    names.Add(display);
                }
                _addNames = names.ToArray();
            }
            _addDd.options = ToOpts(names.Count > 0 ? names.ToArray() : new[] { "(无可用组件)" });
            _addDd.value = 0;
            _addDd.RefreshShownValue();
        }

        private void OnAddComponent()
        {
            if (_current == null || _addNames.Length == 0) return;
            string name = _addNames[_addDd.value];
            if (string.IsNullOrEmpty(name) || _current.HasComponent(name)) return;
            // 用控件默认初始值作为新组件的初始数据
            var control = ComponentRegistry.Create(name);
            if (control == null) return;
            var tempGO = new GameObject("Temp");
            tempGO.AddComponent<RectTransform>();
            control.BuildUI(tempGO.transform);
            _current.SetData(name, control.SyncToData());
            Object.Destroy(tempGO);
            _host.SaveNow();
            BuildComponentRows();
        }

        private void OnNew()
        {
            string type = _entityTypes[_typeDd.value] == "全部" ? "decoration" : _entityTypes[_typeDd.value];
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
            _host.SaveNow();
            RebuildProfileList();
        }

        private void OnClone()
        {
            if (_current == null) return;
            int id = EntityProfileManager.AllocateNextId();
            var clone = EntityProfileManager.CloneProfile(_current, id);
            EntityProfileManager.AddProfile(clone);
            _host.SaveNow();
            RebuildProfileList();
        }

        private void OnDelete()
        {
            if (_current == null) return;
            EntityProfileManager.RemoveProfile(_current.Id);
            _host.SaveNow();
            _current = null;
            RebuildProfileList();
        }
    }
}
