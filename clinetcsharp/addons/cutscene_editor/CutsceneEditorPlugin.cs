using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 导演模式（演出）编辑器插件入口。在 Godot 编辑器底部挂载 Dock 面板，
    /// 可视化编排 data/cutscenes/*.json 演出脚本（对照 addons/map_editor_editor 先例）。
    /// 编辑逻辑全部在主面板 CutsceneDock 中，插件只负责 Dock 的注册与卸载。
    /// </summary>
    [Tool]
    public partial class CutsceneEditorPlugin : EditorPlugin
    {
        private EditorDock _dock;

        public override void _EnterTree()
        {
            _dock = new EditorDock
            {
                Title = "演出编辑器",
                DefaultSlot = EditorDock.DockSlot.Bottom,
            };
            _dock.AddChild(new CutsceneDock());
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
