using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 地图编辑器编辑器插件。在 Godot 编辑器底部挂载一个大画布面板，
    /// 无需运行游戏即可编辑地图配置（地形 / 装饰 / 多地图）。
    /// 编辑逻辑全部走共享的 MapEditController，与运行时 MapEditor 行为一致。
    /// </summary>
    [Tool]
    public partial class MapEditorPlugin : EditorPlugin
    {
        private EditorDock _dock;

        public override void _EnterTree()
        {
            _dock = new EditorDock
            {
                Title = "地图编辑器",
                DefaultSlot = EditorDock.DockSlot.Bottom,
            };
            _dock.AddChild(new MapEditorBottomPanel());
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
