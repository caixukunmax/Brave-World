using Godot;
using Godot.Collections;

namespace ClinetCSharp
{
    public partial class ScreenshotTool : Node
    {
        [Export] public Key ScreenshotKey { get; set; } = Key.F12;
        private LogCollector _logCollector;

        public override async void _Ready()
        {
            GD.Print("[ScreenshotTool] _Ready() called - ScreenshotTool initializing...");
            
            var dir = DirAccess.Open("res://");
            if (dir != null && !dir.DirExists("screenshots"))
            {
                var err = dir.MakeDir("screenshots");
                GD.Print("[ScreenshotTool] Created screenshots dir, result: " + err);
            }

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            _logCollector = GetNodeOrNull<LogCollector>("/root/LogCollector");
            GD.Print("[ScreenshotTool] Screenshot tool initialized, press F12 to capture");
            GD.Print("[ScreenshotTool] Node path: " + GetPath());
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && key.Keycode == ScreenshotKey)
            {
                GD.Print("[ScreenshotTool] F12 pressed, taking screenshot...");
                TakeScreenshot();
            }
        }

        public override void _Process(double delta)
        {
            // 每60帧打印一次日志，确认脚本在运行（已关闭）
            // if (Engine.GetProcessFrames() % 60 == 0)
            // {
            //     GD.Print("[ScreenshotTool] _Process running, frames: " + Engine.GetProcessFrames());
            // }
            
            // 备选：通过 Input.IsKeyPressed 检测 F12（用于 _Input 被拦截的情况）
            if (Input.IsKeyPressed(ScreenshotKey) && !Input.IsKeyPressed(Key.Shift) && !Input.IsKeyPressed(Key.Ctrl) && !Input.IsKeyPressed(Key.Alt))
            {
                // 防止重复触发，只在按下瞬间执行
                if (!_wasF12Pressed)
                {
                    GD.Print("[ScreenshotTool] F12 detected via _Process");
                    TakeScreenshot();
                    _wasF12Pressed = true;
                }
            }
            else
            {
                _wasF12Pressed = false;
            }
        }

        private bool _wasF12Pressed = false;

        private async void TakeScreenshot()
        {
            GD.Print("[ScreenshotTool] Taking screenshot...");
            
            // 截图前隐藏调试面板
            var debugPanel = GetTree().GetFirstNodeInGroup("debug_panel");
            var debugButtonCanvas = GetNodeOrNull("/root/Main/DebugButtonCanvas");
            
            bool wasDebugPanelVisible = false;
            bool wasDebugButtonVisible = false;
            
            if (debugPanel != null)
            {
                wasDebugPanelVisible = debugPanel is CanvasLayer canvas && canvas.Visible;
                if (debugPanel is CanvasLayer canvasLayer) canvasLayer.Visible = false;
                GD.Print("[ScreenshotTool] DebugPanel hidden for screenshot");
            }
            
            if (debugButtonCanvas != null)
            {
                wasDebugButtonVisible = debugButtonCanvas is CanvasLayer btnCanvas && btnCanvas.Visible;
                if (debugButtonCanvas is CanvasLayer btnLayer) btnLayer.Visible = false;
                GD.Print("[ScreenshotTool] DebugButton hidden for screenshot");
            }

            // 等待一帧让隐藏生效
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

            var datetime = Time.GetDatetimeDictFromSystem();
            var timestamp = $"{datetime["year"]:D4}{datetime["month"]:D2}{datetime["day"]:D2}_{datetime["hour"]:D2}{datetime["minute"]:D2}{datetime["second"]:D2}";
            var folderPath = $"res://screenshots/screenshot_{timestamp}";

            var err = DirAccess.MakeDirRecursiveAbsolute(folderPath);
            if (err != Error.Ok)
            {
                GD.PushError("[ScreenshotTool] Failed to create directory: " + folderPath);
                return;
            }

            var viewport = GetViewport();
            var image = viewport.GetTexture().GetImage();
            var screenshotPath = folderPath + "/screenshot.png";

            if (image.SavePng(screenshotPath) != Error.Ok)
            {
                GD.PushError("Failed to save screenshot");
                return;
            }

            // 恢复调试面板
            if (debugPanel != null && wasDebugPanelVisible)
            {
                if (debugPanel is CanvasLayer canvasLayer) canvasLayer.Visible = true;
            }
            if (debugButtonCanvas != null && wasDebugButtonVisible)
            {
                if (debugButtonCanvas is CanvasLayer btnLayer) btnLayer.Visible = true;
            }

            var globalPath = ProjectSettings.GlobalizePath(folderPath);
            DisplayServer.ClipboardSet(globalPath);
            GD.Print("[ScreenshotTool] Screenshot saved to: " + globalPath);
            ShowNotification("Screenshot saved! (UI hidden)", Colors.Green);
        }

        private async void ShowNotification(string text, Color color)
        {
            var label = new Label();
            label.Text = text;
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.AddThemeColorOverride("font_color", color);
            label.AddThemeFontSizeOverride("font_size", 20);

            var canvasLayer = new CanvasLayer();
            canvasLayer.Layer = 100;
            canvasLayer.AddChild(label);
            AddChild(canvasLayer);

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var viewportSize = GetViewport().GetVisibleRect().Size;
            label.Position = new Vector2((viewportSize.X - label.Size.X) / 2, viewportSize.Y - 150);

            await ToSignal(GetTree().CreateTimer(2.5), Timer.SignalName.Timeout);
            canvasLayer.QueueFree();
        }

    }
}
