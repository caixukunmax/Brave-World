using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace ClinetCSharp.Editor.Remote
{
    /// <summary>
    /// 场景操作 API 处理器。
    /// 获取场景树、打开/保存场景、增删节点等。
    /// </summary>
    public static class SceneHandler
    {
        public static void RegisterAll(EditorHttpServer server)
        {
            server.RegisterHandler("GET", "/api/scene/tree", GetSceneTree);
            server.RegisterHandler("GET", "/api/scene/current", GetCurrentScene);
            server.RegisterHandler("GET", "/api/scene/open", OpenScene);
            server.RegisterHandler("POST", "/api/scene/save", SaveScene);
            server.RegisterHandler("POST", "/api/scene/new", NewScene);
            server.RegisterHandler("POST", "/api/scene/node/add", AddNode);
            server.RegisterHandler("DELETE", "/api/scene/node/remove", RemoveNode);
            server.RegisterHandler("POST", "/api/scene/node/reparent", ReparentNode);
            server.RegisterHandler("POST", "/api/scene/node/rename", RenameNode);
        }

        // =============== GET /api/scene/tree ===============
        private static object GetSceneTree(JsonDocument _)
        {
            var root = EditorInterface.Singleton.GetEditedSceneRoot();
            if (root == null)
            {
                return new { tree = (object?)null, message = "No scene open" };
            }
            return new { tree = SerializeNodeTree(root, 0, 5) };
        }

        // =============== GET /api/scene/current ===============
        private static object GetCurrentScene(JsonDocument _)
        {
            var root = EditorInterface.Singleton.GetEditedSceneRoot();
            var scenePath = root?.SceneFilePath ?? "";
            return new
            {
                path = scenePath,
                rootName = root?.Name ?? "",
                rootType = root?.GetClass() ?? "",
                hasScene = root != null
            };
        }

        // =============== GET /api/scene/open ===============
        private static object OpenScene(JsonDocument doc)
        {
            var path = doc.RootElement.GetProperty("path").GetString();
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Missing 'path' parameter");

            EditorInterface.Singleton.OpenSceneFromPath(path);
            var root = EditorInterface.Singleton.GetEditedSceneRoot();
            return new { path, ok = root != null, root_name = root?.Name ?? "" };
        }

        // =============== POST /api/scene/save ===============
        private static object SaveScene(JsonDocument _)
        {
            var root = EditorInterface.Singleton.GetEditedSceneRoot();
            if (root == null)
                return new { ok = false, error = "No scene open" };

            var scenePath = root.SceneFilePath;
            if (string.IsNullOrEmpty(scenePath))
                return new { ok = false, error = "Scene has no file path (unsaved new scene)" };

            var packed = new PackedScene();
            var packResult = packed.Pack(root);
            if (packResult != Error.Ok)
                return new { ok = false, error = $"Failed to pack scene: {packResult}" };

            var err = ResourceSaver.Save(packed, scenePath);
            if (err == Error.Ok)
            {
                // 保存成功后通知编辑器资源已变更
                EditorInterface.Singleton.GetResourceFilesystem()?.UpdateFile(scenePath);
            }

            return new { path = scenePath, error = err.ToString(), ok = err == Error.Ok };
        }

        // =============== POST /api/scene/new ===============
        /// <summary>
        /// 创建新场景（通过创建一个空的 Node 作为根节点，当前场景会被替换）。
        /// 注意：EditorInterface 没有 CreateNewScene 方法，这里用 AddRootNode 模拟。
        /// </summary>
        private static object NewScene(JsonDocument _)
        {
            var err = EditorInterface.Singleton.CloseScene();
            var newRoot = new Node { Name = "Node" };
            EditorInterface.Singleton.AddRootNode(newRoot);
            return new { ok = true, close_error = err.ToString(), root_name = newRoot.Name };
        }

        // =============== POST /api/scene/node/add ===============
        private static object AddNode(JsonDocument doc)
        {
            var root = doc.RootElement;
            var parentPath = root.GetProperty("parent_path").GetString() ?? "/root";
            var nodeType = root.GetProperty("type").GetString() ?? "Node";
            var nodeName = root.GetProperty("name").GetString() ?? "NewNode";

            var parent = FindNodeByPath(parentPath);
            if (parent == null)
                throw new Exception($"Parent node not found: {parentPath}");

            var type = Type.GetType($"Godot.{nodeType}, GodotSharp");
            Node? newNode = null;

            if (type != null && typeof(Node).IsAssignableFrom(type))
            {
                newNode = (Node)Activator.CreateInstance(type)!;
            }
            else if (ClassDB.CanInstantiate(nodeType))
            {
                var instance = ClassDB.Instantiate(nodeType);
                newNode = instance.As<Node>();
            }

            if (newNode == null)
                throw new Exception($"Unknown node type: {nodeType}");

            newNode.Name = nodeName;
            parent.AddChild(newNode);
            newNode.Owner = EditorInterface.Singleton.GetEditedSceneRoot();

            return new
            {
                name = newNode.Name,
                type = newNode.GetClass(),
                path = newNode.GetPath().ToString()
            };
        }

        // =============== DELETE /api/scene/node/remove ===============
        private static object RemoveNode(JsonDocument doc)
        {
            var nodePath = doc.RootElement.GetProperty("path").GetString();
            if (string.IsNullOrEmpty(nodePath))
                throw new ArgumentException("Missing 'path' parameter");

            var node = FindNodeByPath(nodePath);
            if (node == null)
                throw new Exception($"Node not found: {nodePath}");

            var parent = node.GetParent();
            var name = node.Name;
            node.QueueFree();

            return new { removed = name, parent_path = parent?.GetPath().ToString() ?? "" };
        }

        // =============== POST /api/scene/node/reparent ===============
        private static object ReparentNode(JsonDocument doc)
        {
            var nodePath = doc.RootElement.GetProperty("path").GetString();
            var newParentPath = doc.RootElement.GetProperty("new_parent_path").GetString();

            var node = FindNodeByPath(nodePath);
            var newParent = FindNodeByPath(newParentPath);
            if (node == null) throw new Exception($"Node not found: {nodePath}");
            if (newParent == null) throw new Exception($"Parent not found: {newParentPath}");

            var oldParent = node.GetParent();
            oldParent?.RemoveChild(node);
            newParent.AddChild(node);
            node.Owner = EditorInterface.Singleton.GetEditedSceneRoot();

            return new
            {
                node = node.Name,
                old_parent = oldParent?.Name ?? "",
                new_parent = newParent.Name
            };
        }

        // =============== POST /api/scene/node/rename ===============
        private static object RenameNode(JsonDocument doc)
        {
            var nodePath = doc.RootElement.GetProperty("path").GetString();
            var newName = doc.RootElement.GetProperty("name").GetString();

            var node = FindNodeByPath(nodePath);
            if (node == null) throw new Exception($"Node not found: {nodePath}");

            var oldName = node.Name;
            node.Name = newName;

            return new { old_name = oldName, new_name = node.Name };
        }

        // =============== 工具方法 ===============

        internal static Node? FindNodeByPath(string path)
        {
            var root = EditorInterface.Singleton.GetEditedSceneRoot();
            if (root == null) return null;
            if (path == "/" || path == "/root" || path == "") return root;

            var cleanPath = path.StartsWith("/root/") ? path[6..] :
                            path.StartsWith("/") ? path[1..] : path;
            return root.GetNodeOrNull(cleanPath);
        }

        private static Dictionary<string, object> SerializeNodeTree(Node node, int depth, int maxDepth)
        {
            var result = new Dictionary<string, object>
            {
                ["name"] = node.Name,
                ["type"] = node.GetClass(),
                ["path"] = node.GetPath().ToString(),
                ["children_count"] = node.GetChildCount()
            };

            if (depth < maxDepth && node.GetChildCount() > 0)
            {
                var children = new List<Dictionary<string, object>>();
                foreach (var child in node.GetChildren())
                {
                    children.Add(SerializeNodeTree(child, depth + 1, maxDepth));
                }
                result["children"] = children;
            }

            return result;
        }
    }
}
