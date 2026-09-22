using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClinetCSharp.Cutscene
{
    /// <summary>
    /// 演出脚本 JSON 读写与结构校验（纯 C#，不依赖 Godot 节点）。
    /// 运行时（CutsceneConfigUtil）与将来的编辑器插件（addons/cutscene_editor）共用本文件，
    /// 保证"插件写、运行时读"的是同一份格式与同一份校验逻辑。
    /// 文件格式见 docs/design/导演模式设计.md §4。
    /// </summary>
    public static class CutsceneScriptIO
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true,
        };

        /// <summary>第一期支持的 cue 类型集合（§4.3）</summary>
        public static readonly HashSet<string> KnownCueTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "wait", "story_play", "move", "face",
            "camera_focus", "camera_set", "camera_reset", "camera_shake",
            "fade", "actor_enter", "actor_leave", "sfx", "bgm",
        };

        /// <summary>指令中文显示名 + 用法说明（编辑器 UI 用），key 为指令 type（忽略大小写）</summary>
        private static readonly Dictionary<string, (string Name, string Help)> CueMeta = new(StringComparer.OrdinalIgnoreCase)
        {
            ["wait"]          = ("等待",        "暂停演出指定秒数，用于节奏控制。参数：sec（秒）。"),
            ["story_play"]    = ("播放剧情",    "按 GM story 方式播放一段剧情对白。参数：chapter（章节 id）、dialogue_start（对话id起始，含）、dialogue_end（对话id结束，含）。播放该章节中对话id在 [start,end] 范围内的全部对白（按 sequence 排序）。"),
            ["move"]          = ("移动",        "让角色移动到目标格子。参数：actor（演员 id）、x、y（目标格子）、sec（秒）。"),
            ["face"]          = ("朝向",        "设置角色朝向。参数：actor（演员 id）、dir（up/down/left/right）。"),
            ["camera_focus"]  = ("镜头聚焦",    "镜头聚焦到坐标或角色并缩放。参数：x、y（可选，不带则聚焦 actor）、zoom（缩放）、sec（秒）。"),
            ["camera_set"]    = ("镜头机位",    "直接设置镜头机位格子与缩放。参数：x、y（机位格子）、zoom（0=不变）。"),
            ["camera_reset"]  = ("镜头复位",    "镜头平滑复位到默认位置。参数：sec（秒）。"),
            ["camera_shake"]  = ("镜头震动",    "镜头震动效果。参数：strength（强度）、sec（秒）。"),
            ["fade"]          = ("淡入淡出",    "黑场淡入/淡出。参数：dir（out=黑场 / in=恢复）、sec（秒）。"),
            ["actor_enter"]   = ("登场",        "让某演员登场到指定格子。参数：actor（演员 id）、x、y（落点格子）、dir（可选，默认 down；up/down/left/right 角色朝向）。"),
            ["actor_leave"]   = ("退场",        "让某演员退场消失。参数：actor（演员 id）。"),
            ["sfx"]           = ("音效",        "播放一个音效（第一期仅记录日志，尚未接入音频系统）。参数：name（资源名）。"),
            ["bgm"]           = ("背景音乐",    "播放背景音乐（第一期仅记录日志，尚未接入音频系统）。参数：name（资源名）。"),
        };

        /// <summary>指令的中文显示名（纯中文，便于阅读）；未知类型原样返回</summary>
        public static string GetCueDisplayName(string type)
            => CueMeta.TryGetValue(type, out var m) ? m.Name : type;

        /// <summary>指令的用法说明；未知类型返回空串</summary>
        public static string GetCueHelp(string type)
            => CueMeta.TryGetValue(type, out var m) ? m.Help : "";

        /// <summary>解析 JSON 文本为演出脚本，格式非法时抛 CutsceneScriptException</summary>
        public static CutsceneScript Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new CutsceneScriptException("演出脚本内容为空");

            CutsceneScript script;
            try
            {
                script = JsonSerializer.Deserialize<CutsceneScript>(json, Options);
            }
            catch (JsonException ex)
            {
                throw new CutsceneScriptException($"JSON 解析失败：{ex.Message}");
            }

            if (script == null)
                throw new CutsceneScriptException("JSON 解析结果为空");
            script.Cues ??= new List<CutsceneCue>();
            return script;
        }

        /// <summary>序列化演出脚本为 JSON 文本（编辑器插件保存用）</summary>
        public static string Serialize(CutsceneScript script)
        {
            return JsonSerializer.Serialize(script, Options);
        }

        /// <summary>
        /// 结构校验：返回错误列表（空 = 通过）。
        /// fileName 用于校验"id 与文件名一致"（§4.1），传 null 则跳过该项。
        /// 坐标是否在地图范围内属于运行时校验（需要 GridManager），由 cue 执行时检查。
        /// </summary>
        public static List<string> Validate(CutsceneScript script, string fileName)
        {
            var errors = new List<string>();
            if (script == null)
            {
                errors.Add("脚本为空");
                return errors;
            }

            if (script.Id <= 0)
                errors.Add($"id 非法：{script.Id}");

            if (!string.IsNullOrEmpty(fileName))
            {
                // fileName 形如 "1.json"
                string baseName = fileName;
                int dot = baseName.LastIndexOf('.');
                if (dot >= 0)
                    baseName = baseName.Substring(0, dot);
                if (!int.TryParse(baseName, out int fileId) || fileId != script.Id)
                    errors.Add($"id({script.Id}) 与文件名({fileName}) 不一致");
            }

            if (script.Trigger != null)
            {
                if (script.Trigger.Rect == null || script.Trigger.Rect.Length != 4)
                    errors.Add("trigger.rect 必须是 [x, y, w, h] 四元数组");
                else if (script.Trigger.Rect[2] <= 0 || script.Trigger.Rect[3] <= 0)
                    errors.Add("trigger.rect 宽高必须为正数");
            }

            // 先校验演员表本身
            var seenActorIds = new HashSet<int>();
            foreach (var a in script.Actors ?? Enumerable.Empty<CutsceneActorDef>())
            {
                if (a.id <= 0)
                    errors.Add($"演员表：id 必须为正整数（当前 {a.id}）");
                else if (!seenActorIds.Add(a.id))
                    errors.Add($"演员表：id {a.id} 重复");
                if (a.type == 2 && a.monsterConfigId <= 0)
                    errors.Add($"演员表：怪物演员 id={a.id} 必须填写 monsterConfigId");
                if (a.type is not (1 or 2))
                    errors.Add($"演员表：id={a.id} 的 type={a.type} 暂不支持（仅 1=玩家 / 2=怪物）");
            }

            // 按执行顺序检查：演员必须先 actor_enter 登场、再被引用
            var onStage = new HashSet<int>();
            var ordered = new List<CutsceneCue>(script.Cues);
            ordered.Sort((a, b) => a.Seq != b.Seq ? a.Seq.CompareTo(b.Seq) : a.Group.CompareTo(b.Group));

            bool ValidActorId(CutsceneCue c, out int id)
            {
                id = -1;
                return !string.IsNullOrWhiteSpace(c.Actor)
                    && int.TryParse(c.Actor, out id)
                    && script.FindActor(id) != null;
            }

            foreach (var cue in ordered)
            {
                string where = $"seq={cue.Seq} type={cue.Type}";

                if (string.IsNullOrWhiteSpace(cue.Type))
                {
                    errors.Add($"{where}：type 为空");
                    continue;
                }
                if (!KnownCueTypes.Contains(cue.Type))
                {
                    errors.Add($"{where}：未知/已废弃的 cue 类型「{cue.Type}」（spawn_actor/despawn_actor 已移除，请用 actor_enter/actor_leave）");
                    continue;
                }

                switch (cue.Type.ToLowerInvariant())
                {
                    case "move":
                    case "face":
                        if (!ValidActorId(cue, out int id1))
                            errors.Add($"{where}：actor 必须是演员表中存在的 id");
                        else if (!onStage.Contains(id1))
                            errors.Add($"{where}：演员 {id1} 尚未登场（需先 actor_enter）");
                        break;
                    case "actor_enter":
                        if (!ValidActorId(cue, out int id2))
                            errors.Add($"{where}：actor 必须是演员表中存在的 id");
                        else if (onStage.Contains(id2))
                            errors.Add($"{where}：演员 {id2} 已登场（重复 actor_enter）");
                        else if (!cue.Has("x") || !cue.Has("y"))
                            errors.Add($"{where}：actor_enter 缺少落点坐标 x/y");
                        else
                        {
                            string dir = cue.GetString("dir", "").ToLowerInvariant();
                            if (dir.Length > 0 && dir != "up" && dir != "down" && dir != "left" && dir != "right")
                                errors.Add($"{where}：actor_enter 的 dir 非法（应为 up/down/left/right）");
                            onStage.Add(id2);
                        }
                        break;
                    case "actor_leave":
                        if (!ValidActorId(cue, out int id3))
                            errors.Add($"{where}：actor 必须是演员表中存在的 id");
                        else
                            onStage.Remove(id3);
                        break;
                    case "camera_focus":
                        if (!string.IsNullOrWhiteSpace(cue.Actor))
                        {
                            if (!ValidActorId(cue, out int id4))
                                errors.Add($"{where}：actor 必须是演员表中存在的 id");
                            else if (!onStage.Contains(id4))
                                errors.Add($"{where}：演员 {id4} 尚未登场（需先 actor_enter）");
                        }
                        if (string.IsNullOrWhiteSpace(cue.Actor)
                            && (!cue.Has("x") || !cue.Has("y")))
                            errors.Add($"{where}：camera_focus 需要坐标 x/y 或 actor");
                        break;
                    case "story_play":
                        if (!cue.Has("chapter"))
                            errors.Add($"{where}：story_play 缺少 chapter");
                        else if (!cue.Has("dialogue_start") || !cue.Has("dialogue_end"))
                            errors.Add($"{where}：story_play 缺少 dialogue_start / dialogue_end");
                        else if (cue.GetInt("dialogue_start") > cue.GetInt("dialogue_end"))
                            errors.Add($"{where}：story_play 的 dialogue_start 不能大于 dialogue_end");
                        break;
                    case "camera_set":
                        if (!cue.Has("x") || !cue.Has("y"))
                            errors.Add($"{where}：camera_set 缺少坐标 x/y");
                        break;
                }
            }

            return errors;
        }

        /// <summary>把 cue 里的 actor 引用解析为演员 id；返回 false 表示引用无效</summary>
        public static bool TryParseActorId(string actorRef, out int id)
        {
            id = -1;
            return !string.IsNullOrWhiteSpace(actorRef) && int.TryParse(actorRef.Trim(), out id);
        }
    }

    /// <summary>演出脚本解析/校验异常</summary>
    public class CutsceneScriptException : Exception
    {
        public CutsceneScriptException(string message) : base(message) { }
    }

    /// <summary>单条 cue 执行失败异常，携带 seq 便于错误提示定位</summary>
    public class CutsceneCueException : Exception
    {
        public int Seq { get; }
        public CutsceneCueException(int seq, string message) : base(message) { Seq = seq; }
    }

    /// <summary>触发配置（§4.1 trigger）</summary>
    public class CutsceneTriggerConfig
    {
        public string map { get; set; } = "";
        public int[] rect { get; set; }
        public bool once { get; set; } = true;

        // 以下为便捷访问器，不参与 JSON 序列化（PropertyNameCaseInsensitive 下会与上面的小写属性撞名）
        [JsonIgnore] public string Map => map ?? "";
        [JsonIgnore] public int[] Rect => rect;
        [JsonIgnore] public bool Once => once;
    }

    /// <summary>单条演出指令（§4.2）</summary>
    public class CutsceneCue
    {
        public int seq { get; set; }
        public int group { get; set; }
        public string type { get; set; } = "";
        public string actor { get; set; } = "";
        public Dictionary<string, JsonElement> @params { get; set; } = new();

        // 便捷访问器不参与 JSON 序列化（同 CutsceneTriggerConfig 的撞名问题）
        [JsonIgnore] public int Seq => seq;
        [JsonIgnore] public int Group => group;
        [JsonIgnore] public string Type => type ?? "";
        [JsonIgnore] public string Actor => actor ?? "";

        // ========== params 读取辅助 ==========

        public bool Has(string key) =>
            @params != null && @params.TryGetValue(key, out var v) && v.ValueKind != JsonValueKind.Null && v.ValueKind != JsonValueKind.Undefined;

        public float GetFloat(string key, float defaultValue = 0f)
        {
            if (@params != null && @params.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetSingle(out float f))
                return f;
            return defaultValue;
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            if (@params != null && @params.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int i))
                return i;
            return defaultValue;
        }

        public string GetString(string key, string defaultValue = "")
        {
            if (@params != null && @params.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString() ?? defaultValue;
            return defaultValue;
        }

        public bool GetBool(string key, bool defaultValue = false)
        {
            if (@params != null && @params.TryGetValue(key, out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False))
                return v.GetBoolean();
            return defaultValue;
        }

        // ========== params 写入辅助（编辑器插件用，运行时不用） ==========

        public void SetFloat(string key, float value) => (@params ??= new())[key] = JsonSerializer.SerializeToElement(value);

        public void SetInt(string key, int value) => (@params ??= new())[key] = JsonSerializer.SerializeToElement(value);

        public void SetString(string key, string value) => (@params ??= new())[key] = JsonSerializer.SerializeToElement(value ?? "");

        public void SetBool(string key, bool value) => (@params ??= new())[key] = JsonSerializer.SerializeToElement(value);

        /// <summary>移除一个参数（如 camera_focus 取消坐标目标时移除 x/y）</summary>
        public void RemoveParam(string key) => @params?.Remove(key);
    }

    /// <summary>演员表条目（§4.1 actors）：演出开头声明，后续 cue 用 id 引用。默认不在场，落点由 actor_enter 决定。</summary>
    public class CutsceneActorDef
    {
        public int id { get; set; }                 // 演出内递增、分配后固定、删除不回填
        public int type { get; set; } = 1;          // 1=玩家 2=怪物 3=NPC(预留)
        public int monsterConfigId { get; set; }    // type=2 时有效，指向 monster_config.json 的 monsterId

        [JsonIgnore] public int Id => id;
        [JsonIgnore] public int Type => type;
        [JsonIgnore] public int MonsterConfigId => monsterConfigId;

        /// <summary>是否为怪物演员（外观绑定 monster_config.json）</summary>
        [JsonIgnore] public bool IsMonster => type == 2;
    }

    /// <summary>一场演出的完整脚本（§4.1）</summary>
    public class CutsceneScript
    {
        public int id { get; set; }
        public string name { get; set; } = "";
        public bool letterbox { get; set; } = true;
        public bool skippable { get; set; } = true;
        public CutsceneTriggerConfig trigger { get; set; }
        public List<CutsceneActorDef> actors { get; set; } = new();
        public List<CutsceneCue> cues { get; set; } = new();

        // 便捷访问器不参与 JSON 序列化（同上撞名问题）
        [JsonIgnore] public int Id => id;
        [JsonIgnore] public string Name => name ?? "";
        [JsonIgnore] public bool Letterbox => letterbox;
        [JsonIgnore] public bool Skippable => skippable;
        [JsonIgnore] public CutsceneTriggerConfig Trigger => trigger;
        [JsonIgnore] public List<CutsceneCue> Cues { get => cues; set => cues = value; }
        [JsonIgnore] public List<CutsceneActorDef> Actors { get => actors; set => actors = value; }

        /// <summary>在演员表中按 id 查找（招幕/引用校验用）</summary>
        public CutsceneActorDef FindActor(int id)
            => (actors ??= new()).FirstOrDefault(a => a.id == id);

        /// <summary>按执行顺序展开：(seq, group) 分组，组内 cue 并行</summary>
        public List<List<CutsceneCue>> BuildExecutionGroups()
        {
            var sorted = new List<CutsceneCue>(Cues ?? new List<CutsceneCue>());
            sorted.Sort((a, b) => a.Seq != b.Seq ? a.Seq.CompareTo(b.Seq) : a.Group.CompareTo(b.Group));

            var result = new List<List<CutsceneCue>>();
            foreach (var cue in sorted)
            {
                if (result.Count == 0)
                {
                    result.Add(new List<CutsceneCue> { cue });
                    continue;
                }
                var lastGroup = result[result.Count - 1];
                var first = lastGroup[0];
                if (first.Seq == cue.Seq && first.Group == cue.Group)
                    lastGroup.Add(cue);
                else
                    result.Add(new List<CutsceneCue> { cue });
            }
            return result;
        }
    }
}
