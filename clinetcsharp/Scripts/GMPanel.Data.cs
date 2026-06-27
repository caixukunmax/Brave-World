using System.Collections.Generic;
using Godot;

namespace ClinetCSharp
{
    public partial class GMPanel
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

        private const string GmConfigPath = "res://gm_panel.json";

        private void SaveGmConfig()
        {
            var groupsArray = new Godot.Collections.Array<Godot.Collections.Dictionary>();
            foreach (var group in _groups)
            {
                var commandsArray = new Godot.Collections.Array<Godot.Collections.Dictionary>();
                foreach (var cmd in group.Commands)
                {
                    commandsArray.Add(new Godot.Collections.Dictionary
                    {
                        ["label"] = cmd.Label,
                        ["cmd"] = cmd.Cmd,
                        ["description"] = cmd.Description,
                    });
                }
                groupsArray.Add(new Godot.Collections.Dictionary
                {
                    ["name"] = group.Name,
                    ["collapsed"] = group.Collapsed,
                    ["commands"] = commandsArray,
                });
            }
            var data = new Godot.Collections.Dictionary { ["groups"] = groupsArray };
            var json = Json.Stringify(data);
            using var file = FileAccess.Open(GmConfigPath, FileAccess.ModeFlags.Write);
            if (file != null)
                file.StoreString(json);
        }

        private bool LoadGmConfig()
        {
            if (!FileAccess.FileExists(GmConfigPath))
                return false;

            using var file = FileAccess.Open(GmConfigPath, FileAccess.ModeFlags.Read);
            if (file == null)
                return false;

            var json = file.GetAsText();
            var parsed = Json.ParseString(json);
            if (parsed.VariantType == Variant.Type.Nil)
                return false;

            var data = parsed.AsGodotDictionary();
            if (!data.ContainsKey("groups"))
                return false;

            _groups.Clear();
            foreach (var groupVariant in data["groups"].AsGodotArray())
            {
                var gd = groupVariant.AsGodotDictionary();
                var group = new GmGroup
                {
                    Name = gd.ContainsKey("name") ? gd["name"].AsString() : "",
                    Collapsed = gd.ContainsKey("collapsed") ? gd["collapsed"].AsBool() : false,
                };
                if (gd.ContainsKey("commands"))
                {
                    foreach (var cmdVariant in gd["commands"].AsGodotArray())
                    {
                        var cd = cmdVariant.AsGodotDictionary();
                        group.Commands.Add(new GmCommand
                        {
                            Label = cd.ContainsKey("label") ? cd["label"].AsString() : "",
                            Cmd = cd.ContainsKey("cmd") ? cd["cmd"].AsString() : "",
                            Description = cd.ContainsKey("description") ? cd["description"].AsString() : "",
                        });
                    }
                }
                _groups.Add(group);
            }
            return _groups.Count > 0;
        }

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
            AddCommandData(moveGroup, "回出生点", "teleport,25,25");
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
