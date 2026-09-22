using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace ClinetCSharp.Editor.Remote
{
    /// <summary>
    /// 运行控制 API 处理器。
    /// Play / Stop / 截图 / 获取控制台输出。
    /// </summary>
    public static class PlaybackHandler
    {
        private static readonly List<string> _consoleBuffer = new();
        private const int MaxBufferLines = 500;

        public static void RegisterAll(EditorHttpServer server)
        {
            server.RegisterHandler("POST", "/api/play/start", PlayStart);
            server.RegisterHandler("POST", "/api/play/stop", PlayStop);
            server.RegisterHandler("POST", "/api/play/start_custom", PlayStartCustom);
            server.RegisterHandler("GET", "/api/play/status", PlayStatus);
            server.RegisterHandler("GET", "/api/play/screenshot", Screenshot);
            server.RegisterHandler("GET", "/api/console", GetConsole);
            server.RegisterHandler("DELETE", "/api/console", ClearConsole);
        }

        public static void Log(string message)
        {
            lock (_consoleBuffer)
            {
                _consoleBuffer.Add(message);
                if (_consoleBuffer.Count > MaxBufferLines)
                {
                    _consoleBuffer.RemoveRange(0, _consoleBuffer.Count - MaxBufferLines);
                }
            }
        }

        // =============== POST /api/play/start ===============
        private static object PlayStart(JsonDocument _)
        {
            if (EditorInterface.Singleton.IsPlayingScene())
            {
                return new { playing = true, message = "Already playing" };
            }
            EditorInterface.Singleton.PlayCurrentScene();
            return new { playing = true, message = "Started playing current scene" };
        }

        // =============== POST /api/play/stop ===============
        private static object PlayStop(JsonDocument _)
        {
            if (!EditorInterface.Singleton.IsPlayingScene())
            {
                return new { playing = false, message = "Not playing" };
            }
            EditorInterface.Singleton.StopPlayingScene();
            return new { playing = false, message = "Stopped" };
        }

        // =============== POST /api/play/start_custom ===============
        /// <summary>
        /// 运行指定场景。Body: { "scene": "res://path/to/scene.tscn" }
        /// </summary>
        private static object PlayStartCustom(JsonDocument doc)
        {
            var scene = doc.RootElement.GetProperty("scene").GetString();
            if (string.IsNullOrEmpty(scene))
                throw new ArgumentException("Missing 'scene' parameter");

            EditorInterface.Singleton.PlayCustomScene(scene);
            return new { playing = true, scene };
        }

        // =============== GET /api/play/status ===============
        private static object PlayStatus(JsonDocument _)
        {
            var isPlaying = EditorInterface.Singleton.IsPlayingScene();
            var editedRoot = EditorInterface.Singleton.GetEditedSceneRoot();
            var playingScene = EditorInterface.Singleton.GetPlayingScene();
            return new
            {
                is_playing = isPlaying,
                playing_scene = playingScene.ToString(),
                edited_scene = editedRoot?.SceneFilePath ?? "",
                edited_scene_root = editedRoot?.Name ?? ""
            };
        }

        // =============== GET /api/play/screenshot ===============
        /// <summary>
        /// 截取当前编辑器视口或运行中游戏的截图。
        /// 游戏运行中截游戏画面，否则截编辑器当前场景视口。
        /// 返回 base64 编码的 PNG 图像。
        /// </summary>
        private static object Screenshot(JsonDocument _)
        {
            var vp = Engine.GetMainLoop() is SceneTree tree ? tree.Root.GetViewport() : null;
            if (vp == null) throw new Exception("No viewport available");

            var tex = vp.GetTexture();
            var img = tex.GetImage();
            if (img == null) throw new Exception("Failed to get viewport image");

            // 转成 PNG
            var pngData = img.SavePngToBuffer();
            var base64 = Convert.ToBase64String(pngData);

            return new
            {
                format = "png",
                width = img.GetWidth(),
                height = img.GetHeight(),
                data_base64 = base64,
                size = pngData.Length
            };
        }

        // =============== GET /api/console ===============
        private static object GetConsole(JsonDocument doc)
        {
            int limit = 100;
            if (doc.RootElement.TryGetProperty("limit", out var limitElem))
            {
                limit = limitElem.GetInt32();
            }

            string[] lines;
            lock (_consoleBuffer)
            {
                if (_consoleBuffer.Count <= limit)
                {
                    lines = _consoleBuffer.ToArray();
                }
                else
                {
                    lines = _consoleBuffer.GetRange(_consoleBuffer.Count - limit, limit).ToArray();
                }
            }

            return new { total = lines.Length, lines = lines };
        }

        // =============== DELETE /api/console ===============
        private static object ClearConsole(JsonDocument _)
        {
            lock (_consoleBuffer)
            {
                _consoleBuffer.Clear();
            }
            return new { ok = true, message = "Console cleared" };
        }
    }
}
