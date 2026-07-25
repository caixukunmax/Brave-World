using Godot;
using System.Collections.Generic;
using System.Linq;
using ClinetCSharp;

namespace ClinetCSharp.Editor
{
    /// <summary>
    /// 组件编辑区：复用运行时的 IEntityTabComponent 框架 + CollapsibleContainer。
    /// 不调用运行时的 ApplyProfileToAll / SyncPreviewEntity（编辑器内无运行实体）。
    /// 仅读写当前 EntityProfile 数据并防抖落盘。
    /// </summary>
    [Tool]
    public partial class EditorProfileComponentList : VBoxContainer
    {
        public EditorProfilePanel OwnerPanel { get; set; }

        /// <summary>标记本组件编辑器自身 _Ready 是否已完成。父面板首次 _Ready 中 AddChild 本控件后，
        /// 子控件的 _Ready 是延迟执行的，在此之前调用 Bind 会因 _componentHolder 为 null 而失败；
        /// 只有 IsReady 为 true 才允许 Bind。</summary>
        public bool IsReady { get; private set; }

        /// <summary>组件数据发生任何变更（字段编辑/增删/启用）时触发，供预览实时刷新。</summary>
        public event System.Action Changed;

        private EntityProfile _profile;
        private readonly Dictionary<string, IEntityTabComponent> _activeComponents = new();
        private readonly Dictionary<string, CollapsibleContainer> _containers = new();
        private VBoxContainer _componentHolder;
        private OptionButton _addOption;

        public override void _Ready()
        {
            BuildUi();
            // 自身 _Ready 完成后再允许父面板 Bind，并主动通知父面板绑定当前选中的 Profile，
            // 避免首次打开时父面板在子控件尚未 Ready 时就调用 Bind（导致组件数值停留旧值）。
            IsReady = true;
            OwnerPanel?.OnComponentEditorReady();
        }

        private void BuildUi()
        {
            var toolbar = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var addBtn = new Button { Text = "添加组件" };
            addBtn.Pressed += OnAddPressed;
            toolbar.AddChild(addBtn);
            _addOption = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            toolbar.AddChild(_addOption);
            AddChild(toolbar);

            // 组件可能很多，用 ScrollContainer 包裹，否则溢出后无法滚动/拖动查看
            var scroll = new ScrollContainer
            {
                Name = "ComponentScroll",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            };
            _componentHolder = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _componentHolder.AddThemeConstantOverride("separation", 8);
            scroll.AddChild(_componentHolder);
            AddChild(scroll);
        }

        public void Bind(EntityProfile profile)
        {
            // 重建/切换前，先把当前组件 UI 的改动写回内存 profile，
            // 否则改名等触发整页重建时会从旧数据重绘、吞掉未落盘的组件编辑。
            Flush();
            _profile = profile;
            Rebuild();
        }

        private void Rebuild()
        {
            // 释放旧组件实例（避免内存泄漏 / 重复 BuildUI）
            foreach (var c in _activeComponents.Values)
            {
                c.DisconnectSignals();
                c.Dispose();
            }
            _activeComponents.Clear();
            foreach (var c in _containers.Values)
                if (GodotObject.IsInstanceValid(c))
                    c.QueueFree();
            _containers.Clear();
            ClearChildren(_componentHolder);
            _addOption.Clear();

            if (_profile == null)
                return;

            foreach (string name in _profile.ComponentNames)
                AddComponentUI(name);

            // 可添加组件：当前实体类型的默认白名单中尚未拥有的
            var allowed = ComponentRegistry.GetComponentsForType(_profile.EntityType).ToList();
            foreach (var (name, displayName) in allowed)
                if (!_profile.HasComponent(name))
                    AddAddOption(name, displayName);
        }

        private void AddComponentUI(string name)
        {
            var component = ComponentRegistry.Create(name);
            if (component == null)
                return;

            var meta = ComponentRegistry.GetComponentMeta(name);
            var container = new CollapsibleContainer(meta.DisplayName)
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            container.SetAccentColor(meta.AccentColor);
            container.SetHeaderIcon(meta.Icon);

            bool isDisabled = _profile.IsComponentDisabled(name);
            if (isDisabled)
            {
                container.Modulate = new Color(0.6f, 0.6f, 0.6f);
                container.SetCollapsedSilent(true);
            }

            // 启用 / 停用（结构变更，立即保存）
            var enableCheck = new CheckBox
            {
                Text = "启用",
                ButtonPressed = !isDisabled,
                SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
            };
            enableCheck.Toggled += on =>
            {
                _profile.SetComponentDisabled(name, !on);
                container.Modulate = on ? Colors.White : new Color(0.6f, 0.6f, 0.6f);
                OwnerPanel?.SaveNow();
                Changed?.Invoke();
            };
            container.HeaderRow.AddChild(enableCheck);

            // 移除（结构变更，立即保存）
            var removeBtn = new Button { Text = "移除", SizeFlagsHorizontal = SizeFlags.ShrinkEnd };
            removeBtn.Pressed += () => OnRemovePressed(name);
            container.HeaderRow.AddChild(removeBtn);

            component.BuildUI(container.Content);
            var data = _profile.GetData(name);
            if (data != null)
                component.SyncFromData(data);
            component.ConnectSignals(() =>
            {
                OwnerPanel?.ScheduleSave();
                Changed?.Invoke();
            });

            _activeComponents[name] = component;
            _containers[name] = container;
            _componentHolder.AddChild(container);
        }

        /// <summary>把所有组件 UI 当前值写回内存 profile（内存始终最新，落盘由 SaveNow 决定）。</summary>
        public void Flush()
        {
            if (_profile == null)
                return;
            foreach (var kv in _activeComponents)
                _profile.SetData(kv.Key, kv.Value.SyncToData());
        }

        private void OnAddPressed()
        {
            int index = _addOption.Selected;
            if (index < 0 || index >= _addOption.GetItemCount())
                return;
            string name = (string)_addOption.GetItemMetadata(index);
            if (_profile.HasComponent(name))
                return;

            Flush(); // 先把现有组件改动写回内存
            var component = ComponentRegistry.Create(name);
            if (component == null)
                return;
            // 必须先 BuildUI，否则 SyncToData 会访问未初始化的控件。
            // 临时容器不必加入场景树：Godot 控件属性在脱离树时也可读写，各组件 BuildUI 不依赖 _Ready。
            var tmp = new VBoxContainer();
            component.BuildUI(tmp);
            _profile.SetData(name, component.SyncToData());
            component.Dispose();
            tmp.QueueFree();

            Rebuild();
            OwnerPanel?.SaveNow();
            Changed?.Invoke();
        }

        private void OnRemovePressed(string name)
        {
            Flush(); // 写回现有组件，避免丢失未落盘的字段改动
            _profile.RemoveComponent(name);
            Rebuild();
            OwnerPanel?.SaveNow();
            Changed?.Invoke();
        }

        private void AddAddOption(string name, string displayName)
        {
            int idx = _addOption.GetItemCount();
            _addOption.AddItem(displayName);
            _addOption.SetItemMetadata(idx, name);
        }

        private static void ClearChildren(VBoxContainer parent)
        {
            foreach (var child in parent.GetChildren())
                if (child is Node n)
                    n.QueueFree();
        }
    }
}
