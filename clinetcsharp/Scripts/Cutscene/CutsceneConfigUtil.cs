using System.Collections.Generic;
using Godot;

namespace ClinetCSharp.Cutscene
{
    /// <summary>
    /// 演出脚本配置仓库 — 扫描 res://data/cutscenes/*.json 加载全部演出。
    /// 启动时加载一次；GM 命令触发时调用 Reload() 强制热重载（插件保存后立即生效）。
    /// </summary>
    public static class CutsceneConfigUtil
    {
        private const string CutsceneDir = "res://data/cutscenes";

        private static readonly Dictionary<int, CutsceneScript> _scripts = new();
        private static bool _loaded;

        /// <summary>已加载的全部演出（id -> 脚本）</summary>
        public static IReadOnlyDictionary<int, CutsceneScript> Scripts => _scripts;

        /// <summary>最近一次 Reload 的加载错误（供 GM 命令/屏幕提示显示）</summary>
        public static readonly List<string> LastErrors = new();

        public static void Load()
        {
            if (_loaded) return;
            Reload();
        }

        /// <summary>重新扫描目录加载全部演出脚本（热重载）</summary>
        public static void Reload()
        {
            _loaded = true;
            _scripts.Clear();
            LastErrors.Clear();

            var dir = DirAccess.Open(CutsceneDir);
            if (dir == null)
            {
                GD.Print($"[CutsceneConfigUtil] 目录不存在：{CutsceneDir}（尚无演出脚本）");
                return;
            }

            dir.ListDirBegin();
            string fileName = dir.GetNext();
            while (!string.IsNullOrEmpty(fileName))
            {
                if (!dir.CurrentIsDir() && fileName.EndsWith(".json"))
                    LoadOne(CutsceneDir + "/" + fileName, fileName);
                fileName = dir.GetNext();
            }
            dir.ListDirEnd();

            GD.Print($"[CutsceneConfigUtil] 已加载 {_scripts.Count} 场演出，{LastErrors.Count} 个错误");
        }

        private static void LoadOne(string path, string fileName)
        {
            try
            {
                using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
                if (file == null)
                {
                    LastErrors.Add($"{fileName}：无法打开文件");
                    return;
                }

                var script = CutsceneScriptIO.Parse(file.GetAsText());
                var errors = CutsceneScriptIO.Validate(script, fileName);
                if (errors.Count > 0)
                {
                    LastErrors.Add($"{fileName}：{string.Join("；", errors)}");
                    return;
                }

                if (_scripts.ContainsKey(script.Id))
                {
                    LastErrors.Add($"{fileName}：演出 id {script.Id} 重复");
                    return;
                }

                _scripts[script.Id] = script;
            }
            catch (CutsceneScriptException ex)
            {
                LastErrors.Add($"{fileName}：{ex.Message}");
            }
            catch (System.Exception ex)
            {
                LastErrors.Add($"{fileName}：加载异常 {ex.Message}");
            }
        }

        public static bool TryGet(int id, out CutsceneScript script)
        {
            Load();
            return _scripts.TryGetValue(id, out script);
        }
    }
}
