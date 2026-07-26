using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace ClinetCSharp.Editor.Remote
{
    /// <summary>
    /// 资源 / 文件操作 API 处理器。
    /// 加载/保存资源、列出目录、读写文件等。
    /// </summary>
    public static class ResourceHandler
    {
        public static void RegisterAll(EditorHttpServer server)
        {
            server.RegisterHandler("GET", "/api/resource/load", LoadResource);
            server.RegisterHandler("POST", "/api/resource/save", SaveResource);
            server.RegisterHandler("GET", "/api/filesystem/list", ListDirectory);
            server.RegisterHandler("GET", "/api/filesystem/read", ReadFile);
            server.RegisterHandler("POST", "/api/filesystem/write", WriteFile);
            server.RegisterHandler("GET", "/api/editor/info", EditorInfo);
        }

        // =============== GET /api/resource/load ===============
        /// <summary>
        /// 加载一个资源并返回其基本信息（类型、属性等）。
        /// Query: path=res://xxx.tres
        /// </summary>
        private static object LoadResource(JsonDocument doc)
        {
            var path = doc.RootElement.GetProperty("path").GetString();
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Missing 'path' parameter");

            var res = GD.Load(path);
            if (res == null) throw new Exception($"Failed to load resource: {path}");

            var props = new Dictionary<string, object>();
            foreach (var prop in res.GetPropertyList())
            {
                var name = prop["name"].AsString();
                var usage = (PropertyUsageFlags)prop["usage"].AsInt32();
                if (!usage.HasFlag(PropertyUsageFlags.Storage) && !usage.HasFlag(PropertyUsageFlags.Editor))
                    continue;
                try
                {
                    var val = res.Get(name);
                    props[name.ToString()] = val.ToString();
                }
                catch
                {
                    props[name.ToString()] = null;
                }
            }

            return new
            {
                path,
                type = res.GetClass(),
                resource_path = res.ResourcePath,
                properties = props
            };
        }

        // =============== POST /api/resource/save ===============
        /// <summary>
        /// 保存资源到指定路径。
        /// Body: { "path": "res://xxx.tres" } — 保存已加载（或已修改）的资源
        /// 注意：这个接口主要是把编辑器中已修改的资源落盘。
        /// </summary>
        private static object SaveResource(JsonDocument doc)
        {
            var path = doc.RootElement.GetProperty("path").GetString();
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Missing 'path' parameter");

            var res = GD.Load(path);
            if (res == null) throw new Exception($"Resource not found: {path}");

            var err = ResourceSaver.Save(res, path);
            return new { path, error = err.ToString(), ok = err == Error.Ok };
        }

        // =============== GET /api/filesystem/list ===============
        /// <summary>
        /// 列出指定 res:// 路径下的文件和目录。
        /// Query: path=res://scripts, recursive=false
        /// </summary>
        private static object ListDirectory(JsonDocument doc)
        {
            var path = doc.RootElement.TryGetProperty("path", out var p) ? p.GetString() ?? "res://" : "res://";
            var recursive = doc.RootElement.TryGetProperty("recursive", out var r) && r.GetBoolean();

            if (!DirAccess.DirExistsAbsolute(path))
                throw new Exception($"Directory not found: {path}");

            var dir = DirAccess.Open(path);
            if (dir == null) throw new Exception($"Failed to open dir: {path}");

            var files = new List<string>();
            var dirs = new List<string>();

            dir.ListDirBegin();
            string entry;
            while ((entry = dir.GetNext()) != "")
            {
                if (entry == "." || entry == "..") continue;
                var fullPath = path + (path.EndsWith("/") ? "" : "/") + entry;
                if (dir.CurrentIsDir())
                {
                    dirs.Add(entry);
                }
                else
                {
                    files.Add(entry);
                }
            }
            dir.ListDirEnd();

            return new
            {
                path,
                directories = dirs,
                files = files,
                total = dirs.Count + files.Count
            };
        }

        // =============== GET /api/filesystem/read ===============
        /// <summary>
        /// 读取文本文件内容。
        /// Query: path=res://Scripts/Example.cs
        /// </summary>
        private static object ReadFile(JsonDocument doc)
        {
            var path = doc.RootElement.GetProperty("path").GetString();
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Missing 'path' parameter");

            if (!FileAccess.FileExists(path))
                throw new Exception($"File not found: {path}");

            var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null) throw new Exception($"Failed to open file: {path} (error: {FileAccess.GetOpenError()})");

            var content = file.GetAsText();
            file.Close();

            return new { path, content, length = content.Length };
        }

        // =============== POST /api/filesystem/write ===============
        /// <summary>
        /// 写入文本文件。
        /// Body: { "path": "res://...", "content": "..." }
        /// 注意：覆盖写入。建议先 read 再 write。
        /// </summary>
        private static object WriteFile(JsonDocument doc)
        {
            var root = doc.RootElement;
            var path = root.GetProperty("path").GetString();
            var content = root.GetProperty("content").GetString() ?? "";

            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Missing 'path' parameter");

            // 确保目录存在
            var dirPath = path.Substring(0, path.LastIndexOf('/'));
            if (!DirAccess.DirExistsAbsolute(dirPath))
            {
                DirAccess.MakeDirRecursiveAbsolute(dirPath);
            }

            var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            if (file == null) throw new Exception($"Failed to write file: {path} (error: {FileAccess.GetOpenError()})");

            file.StoreString(content);
            file.Close();

            return new { path, bytes_written = content.Length, ok = true };
        }

        // =============== GET /api/editor/info ===============
        /// <summary>
        /// 返回编辑器信息和所有可用 API 端点列表。
        /// </summary>
        private static object EditorInfo(JsonDocument _)
        {
            var endpoints = new[]
            {
                // 场景
                new { method = "GET", path = "/api/scene/tree", desc = "获取当前编辑场景的节点树（深度5）" },
                new { method = "GET", path = "/api/scene/current", desc = "获取当前场景信息" },
                new { method = "GET", path = "/api/scene/open", desc = "打开场景 ?path=res://xxx.tscn" },
                new { method = "POST", path = "/api/scene/save", desc = "保存当前场景" },
                new { method = "POST", path = "/api/scene/new", desc = "新建空场景" },
                new { method = "POST", path = "/api/scene/node/add", desc = "添加节点 {parent_path, type, name}" },
                new { method = "DELETE", path = "/api/scene/node/remove", desc = "删除节点 ?path=/root/NodeName" },
                new { method = "POST", path = "/api/scene/node/reparent", desc = "重新挂接 {path, new_parent_path}" },
                new { method = "POST", path = "/api/scene/node/rename", desc = "重命名 {path, name}" },
                // 属性
                new { method = "GET", path = "/api/node/properties", desc = "读取节点属性 ?path=&names=a,b" },
                new { method = "PUT", path = "/api/node/property", desc = "设置节点属性 {path, name, value}" },
                new { method = "GET", path = "/api/node/property_list", desc = "节点全部属性定义 ?path=" },
                // 运行
                new { method = "POST", path = "/api/play/start", desc = "运行当前场景" },
                new { method = "POST", path = "/api/play/stop", desc = "停止运行" },
                new { method = "POST", path = "/api/play/start_custom", desc = "运行指定场景 {scene}" },
                new { method = "GET", path = "/api/play/status", desc = "运行状态" },
                new { method = "GET", path = "/api/play/screenshot", desc = "截图（base64 PNG）" },
                // 控制台
                new { method = "GET", path = "/api/console", desc = "控制台输出 ?limit=100" },
                new { method = "DELETE", path = "/api/console", desc = "清空控制台" },
                // 资源 / 文件
                new { method = "GET", path = "/api/resource/load", desc = "加载资源信息 ?path=" },
                new { method = "POST", path = "/api/resource/save", desc = "保存资源 {path}" },
                new { method = "GET", path = "/api/filesystem/list", desc = "列出目录 ?path=&recursive=" },
                new { method = "GET", path = "/api/filesystem/read", desc = "读文本文件 ?path=" },
                new { method = "POST", path = "/api/filesystem/write", desc = "写文本文件 {path, content}" },
                // 元数据
                new { method = "GET", path = "/api/editor/info", desc = "编辑器信息 + API 列表" },
            };

            return new
            {
                godot_version = Engine.GetVersionInfo()["string"].ToString(),
                project = ProjectSettings.GetSetting("application/config/name").ToString(),
                main_scene = ProjectSettings.GetSetting("run/main_scene").ToString(),
                current_scene = EditorInterface.Singleton.GetEditedSceneRoot()?.SceneFilePath ?? "",
                is_playing = EditorInterface.Singleton.IsPlayingScene(),
                endpoints = endpoints
            };
        }
    }
}
