using GameServer.Tables;

namespace GameServer.Services.Map.Combat;

/// <summary>
/// 战斗日志文本格式化器 — 从 Luban 表读取模板，替换占位符生成最终文本
/// 占位符: {actor} {target} {skill} {value} {extra}
/// </summary>
public static class CombatLogFormatter
{
    private static Dictionary<int, string> _templates = new();

    public static void Load(Dictionary<int, CombatLogTextRow> rows)
    {
        _templates = rows.ToDictionary(r => r.Key, r => r.Value.Template);
    }

    public static string Format(int logType, string? actor, string? target, string? skill, int value, string? extra)
    {
        var template = _templates.GetValueOrDefault(logType) ?? "{actor} {extra}";
        return template
            .Replace("{actor}", actor ?? "")
            .Replace("{target}", target ?? "")
            .Replace("{skill}", skill ?? "")
            .Replace("{value}", value.ToString())
            .Replace("{extra}", extra ?? "");
    }
}
