using Godot;

namespace ClinetCSharp.Editor
{
    /// <summary>
    /// 编辑器插件入口：在编辑器底部 Dock 嵌入实体配置面板（与地图编辑器、演出编辑器同排标签）。
    /// 不开游戏即可编辑 EntityProfile / 组件，读写与运行时面板同一份 res://debug_panel_config.cfg。
    /// 数据 I/O 通过 ProfileConfigIO（不依赖 Node / 运行场景）。
    /// </summary>
    [Tool]
    public partial class DebugPanelEditorPlugin : EditorPlugin
    {
        private EditorDock _dock;

        public override void _EnterTree()
        {
            _dock = new EditorDock
            {
                Title = "实体显示配置",
                DefaultSlot = EditorDock.DockSlot.Bottom,
            };
            _dock.AddChild(new EditorProfilePanel());
            AddDock(_dock);
        }

        public override void _ExitTree()
        {
            if (_dock != null)
            {
                RemoveDock(_dock);
                _dock.QueueFree();
                _dock = null;
            }
        }
    }
}
