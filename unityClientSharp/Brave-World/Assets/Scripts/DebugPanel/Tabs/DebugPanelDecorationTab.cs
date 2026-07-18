using System.Collections.Generic;
using UnityEngine;
using UnityClientSharp.Entity;

namespace UnityClientSharp.DebugPanel
{
    /// <summary>
    /// 建筑工坊 Tab（配置编辑，移植自 Godot DebugPanelDecorationTab）。
    /// 仅编辑 decoration 类型 Profile；组件编辑与 EntityTab 同构（依赖 ComponentRegistry）。
    /// 注：本 Tab 与 MapEditor 无实质关系（只有"进入地图编辑器放建筑"一键按钮引用 MapEditor，可留 TODO）。
    /// </summary>
    public class DebugPanelDecorationTab : DebugPanelTab
    {
        public override string TabName => "建筑工坊";
        private readonly DebugPanel _host;
        private RectTransform _componentArea;
        private TMPro.TMP_Dropdown _profileDd, _addDd;
        private int[] _profileIds;
        private string[] _addNames;
        private EntityProfile _current;

        public DebugPanelDecorationTab(DebugPanel host) { _host = host; }

        protected override void BuildContent(RectTransform root)
        {
            _profileDd = DebugPanelUI.AddDropdown(root, "建筑配置", new string[0], 0, _ => SelectProfile());

            var actions = DebugPanelUI.Row(root);
            DebugPanelUI.AddButton(actions, "应用到全部(同ProfileId)", OnApplyToAll);
            DebugPanelUI.AddButton(actions, "进入地图编辑器放建筑", OnOpenMapEditor);

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
            var list = new List<EntityProfile>(EntityProfileManager.GetProfilesByType("decoration"));
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
            if (list.Count > 0) SelectProfile();
            else { _current = null; ClearComponents(); RebuildAddDropdown(); }
        }

        private void SelectProfile()
        {
            if (_profileIds == null || _profileIds.Length == 0) return;
            _current = EntityProfileManager.GetProfile(_profileIds[_profileDd.value]);
            BuildComponentRows();
        }

        private void ClearComponents()
        {
            for (int i = _componentArea.childCount - 1; i >= 0; i--)
                Object.Destroy(_componentArea.GetChild(i).gameObject);
        }

        private void BuildComponentRows()
        {
            ClearComponents();
            if (_current == null) { RebuildAddDropdown(); return; }
            foreach (var name in _current.ComponentNames) BuildComponentRow(name);
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
                DecorationConfigUtil.RefreshFromProfileManager();
            });
            DebugPanelUI.AddButton(header, "移除", () =>
            {
                _current.RemoveComponent(compName);
                _host.SaveNow();
                DecorationConfigUtil.RefreshFromProfileManager();
                BuildComponentRows();
            });

            var control = ComponentRegistry.Create(compName);
            if (control == null) return;
            control.BuildUI(row);
            control.ConnectSignals(() =>
            {
                _current.SetData(compName, control.SyncToData());
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
                foreach (var (name, display) in ComponentRegistry.GetComponentsForType("decoration"))
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
            var control = ComponentRegistry.Create(name);
            if (control == null) return;
            var tempGO = new GameObject("Temp");
            tempGO.AddComponent<RectTransform>();
            control.BuildUI(tempGO.transform);
            _current.SetData(name, control.SyncToData());
            Object.Destroy(tempGO);
            _host.SaveNow();
            DecorationConfigUtil.RefreshFromProfileManager();
            BuildComponentRows();
        }

        private void OnApplyToAll()
        {
            if (_current == null) return;
            // TODO: Godot 端此处遍历同 ProfileId 的 MapDecoration 实体刷新外观；Unity 端 MapDecorationManager
            // 应用逻辑待接（见仓库记忆 40180838：非真实放置的预览实体不得计入 decoration 组）。当前仅持久化配置。
            Debug.Log($"[DecorationTab] ApplyProfileToAll TODO: 需 MapDecorationManager 遍历 ProfileId={_current.Id} 实体刷新（Unity 端 MapEditor 尚未实现）");
            _host.SaveNow();
            DecorationConfigUtil.RefreshFromProfileManager();
        }

        private void OnOpenMapEditor()
        {
            // TODO: Unity 端尚无 MapEditor（Godot 端此按钮切到 PlaceDecoration 工具放置建筑）。先留空。
            Debug.LogWarning("[DecorationTab] MapEditor 在 Unity 端尚未实现，跳过「进入地图编辑器放建筑」");
        }
    }
}
