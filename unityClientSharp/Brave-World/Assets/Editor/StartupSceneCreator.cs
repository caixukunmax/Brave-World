using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityClientSharp.Map.Rendering;

namespace BraveWorld.Editor
{
    /// <summary>
    /// 保底工具：当手写场景 YAML 不被识别、或需重新生成启动场景时，
    /// 通过引擎 API 创建一个只挂 MapBootstrap 的空场景（运行时自举相机/渲染/标注），并加入 Build Settings。
    /// 菜单：BraveWorld > Create Startup Scene
    /// 注：必须用引擎 API 生成场景——手写 .scene YAML 的 base64 guid 无法被团结引擎的 YAML 解析器识别
    /// （报 "Could not extract GUID"），这也是本工具存在的原因。
    /// </summary>
    public static class StartupSceneCreator
    {
        [MenuItem("BraveWorld/Create Startup Scene")]
        public static void Create()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var go = new GameObject("Bootstrap");
            var bootstrap = go.AddComponent<MapBootstrap>();
            bootstrap.MapName = "落叶乡";
            bootstrap.EditMode = false;
            bootstrap.ShowOutsideMapGray = true;

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            string path = "Assets/Scenes/Startup.scene";
            EditorSceneManager.SaveScene(scene, path);
            AddSceneToBuildSettings(path);
            AssetDatabase.Refresh();

            Debug.Log("[StartupSceneCreator] 已创建/覆盖启动场景: " + path);
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes)
                if (s.path == path) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
