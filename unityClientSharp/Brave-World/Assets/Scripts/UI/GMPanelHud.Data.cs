using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityClientSharp.UI
{
    /// <summary>
    /// GM 面板分组数据与持久化 — 移植自 Godot GMPanel.Data.cs。
    /// JSON schema 与 Godot 的 gm_panel.json 完全一致（groups/name/collapsed/commands/label/cmd/description），
    /// 文件可互换。
    /// 路径差异（运行时沙盒适配）：Godot 读写 res://gm_panel.json（工程目录），Unity 运行时无法写
    /// StreamingAssets，改为：用户配置存 <see cref="Application.persistentDataPath"/>/gm_panel.json；
    /// 首次启动无用户配置时读 StreamingAssets/Data/gm_panel.json 出厂默认（与 Godot 仓库内同一份），
    /// 都没有则用内置默认分组。
    /// </summary>
    public partial class GMPanelHud
    {
        private class GmCommand
        {
            public string Label;
            public string Cmd;
            public string Description;
        }

        private class GmGroup
        {
            public string Name;
            public List<GmCommand> Commands = new();
            public bool Collapsed;
        }

        private readonly List<GmGroup> _groups = new();

        private const string GmConfigFileName = "gm_panel.json";

        private static string UserConfigPath =>
            Path.Combine(Application.persistentDataPath, GmConfigFileName);

        private static string DefaultConfigPath =>
            Path.Combine(Application.streamingAssetsPath, "Data", GmConfigFileName);

        private void SaveGmConfig()
        {
            try
            {
                var groupsArray = new JArray();
                foreach (var group in _groups)
                {
                    var commandsArray = new JArray();
                    foreach (var cmd in group.Commands)
                    {
                        commandsArray.Add(new JObject
                        {
                            ["label"] = cmd.Label,
                            ["cmd"] = cmd.Cmd,
                            ["description"] = cmd.Description,
                        });
                    }
                    groupsArray.Add(new JObject
                    {
                        ["name"] = group.Name,
                        ["collapsed"] = group.Collapsed,
                        ["commands"] = commandsArray,
                    });
                }
                var data = new JObject { ["groups"] = groupsArray };
                File.WriteAllText(UserConfigPath, data.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GMPanel] Failed to save gm config: {ex.Message}");
            }
        }

        /// <summary>加载分组配置：用户配置 → 出厂默认 → false（调用方补内置默认分组）。</summary>
        private bool LoadGmConfig()
        {
            if (File.Exists(UserConfigPath) && LoadGmConfigFrom(UserConfigPath))
                return true;
            if (File.Exists(DefaultConfigPath) && LoadGmConfigFrom(DefaultConfigPath))
                return true;
            return false;
        }

        private bool LoadGmConfigFrom(string path)
        {
            try
            {
                var data = JObject.Parse(File.ReadAllText(path));
                if (data["groups"] is not JArray groupsArray)
                    return false;

                _groups.Clear();
                foreach (var groupToken in groupsArray)
                {
                    var gd = (JObject)groupToken;
                    var group = new GmGroup
                    {
                        Name = (string)gd["name"] ?? "",
                        Collapsed = (bool?)gd["collapsed"] ?? false,
                    };
                    if (gd["commands"] is JArray commandsArray)
                    {
                        foreach (var cmdToken in commandsArray)
                        {
                            var cd = (JObject)cmdToken;
                            group.Commands.Add(new GmCommand
                            {
                                Label = (string)cd["label"] ?? "",
                                Cmd = (string)cd["cmd"] ?? "",
                                Description = (string)cd["description"] ?? "",
                            });
                        }
                    }
                    _groups.Add(group);
                }
                return _groups.Count > 0;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[GMPanel] Failed to load gm config from {path}: {ex.Message}");
                return false;
            }
        }

        /// <summary>内置默认分组（逐行移植 Godot AddDefaultGroups）。</summary>
        private void AddDefaultGroups()
        {
            var itemGroup = AddGroupData("道具");
            itemGroup.Collapsed = false;
            AddCommandData(itemGroup, "药水x1", "additem,1001,1");
            AddCommandData(itemGroup, "药水x10", "additem,1001,10");
            AddCommandData(itemGroup, "药水x99", "additem,1001,99");
            AddCommandData(itemGroup, "矿石x10", "additem,1002,10");
            AddCommandData(itemGroup, "矿石x100", "additem,1002,100");
            AddCommandData(itemGroup, "金币券x1", "additem,2001,1");
            AddCommandData(itemGroup, "测试道具x10", "addtestitems,10");

            var moveGroup = AddGroupData("移动");
            AddCommandData(moveGroup, "回出生点", "return");
            AddCommandData(moveGroup, "传送坐标(25,25)", "teleport,25,25");
            AddCommandData(moveGroup, "宝箱1", "teleport,20,20");
            AddCommandData(moveGroup, "宝箱2", "teleport,30,15");
            AddCommandData(moveGroup, "宝箱3", "teleport,40,30");

            var chestGroup = AddGroupData("宝箱");
            AddCommandData(chestGroup, "添加宝箱1类(20,20)", "addchest,1,20,20");
            AddCommandData(chestGroup, "添加宝箱2类(25,30)", "addchest,2,25,30");
            AddCommandData(chestGroup, "添加宝箱3类(35,25)", "addchest,3,35,25");

            var skillGroup = AddGroupData("技能");
            AddCommandData(skillGroup, "学习烈斩(2)", "learnskill,2");
            AddCommandData(skillGroup, "学习盾击(3)", "learnskill,3");
            AddCommandData(skillGroup, "学习旋风斩(4)", "learnskill,4");

            var buffGroup = AddGroupData("Buff");
            AddCommandData(buffGroup, "中毒(1)", "addbuff,1");
            AddCommandData(buffGroup, "冰冻(2)", "addbuff,2");
            AddCommandData(buffGroup, "战吼(3)", "addbuff,3");
            AddCommandData(buffGroup, "护盾(4)", "addbuff,4");
            AddCommandData(buffGroup, "石肤(5)", "addbuff,5");
            AddCommandData(buffGroup, "减速(6)", "addbuff,6");
            AddCommandData(buffGroup, "灼烧(7)", "addbuff,7");
            AddCommandData(buffGroup, "祝福(8)", "addbuff,8");
            AddCommandData(buffGroup, "破甲(9)", "addbuff,9");
            AddCommandData(buffGroup, "眩晕(10)", "addbuff,10");
            AddCommandData(buffGroup, "狂暴(11)", "addbuff,11");
            AddCommandData(buffGroup, "移除中毒", "removebuff,1");
            AddCommandData(buffGroup, "移除冰冻", "removebuff,2");
            AddCommandData(buffGroup, "移除战吼", "removebuff,3");
            AddCommandData(buffGroup, "移除护盾", "removebuff,4");

            RebuildGroupUI();
        }

        private GmGroup AddGroupData(string name)
        {
            var group = new GmGroup
            {
                Name = name,
                Collapsed = true,
            };
            _groups.Add(group);
            return group;
        }

        private void AddCommandData(GmGroup group, string label, string cmd, string description = "")
        {
            group.Commands.Add(new GmCommand
            {
                Label = label,
                Cmd = cmd,
                Description = description,
            });
        }
    }
}
