namespace GameServer.Common;

/// <summary>
/// 服务器日志配置的单一可信源。
/// host（Program.cs）在初始化 Serilog 时设置 <see cref="LogDirectory"/>，
/// GameLogic 层（如 ServerLogPathHandler）据此计算当前正在写入的日志文件绝对路径。
/// 计算逻辑与 Serilog 的 RollingInterval.Day + 文件名模板 "server-.log" 完全对齐。
/// </summary>
public static class ServerLogConfig
{
    /// <summary>日志目录（相对进程工作目录或绝对路径均可）。默认 "logs"。</summary>
    public static string LogDirectory { get; set; } = "logs";

    /// <summary>
    /// 计算当前（当天）正在写入的日志文件绝对路径。
    /// Serilog 配置为 WriteTo.File("logs/server-.log", RollingInterval.Day)，
    /// 因此实际文件名为 "server-yyyyMMdd.log"。用 DateTime.Now 取当天日期，兼容跨午夜滚动。
    /// </summary>
    public static string GetCurrentLogFilePath()
        => Path.Combine(Path.GetFullPath(LogDirectory), "server-" + DateTime.Now.ToString("yyyyMMdd") + ".log");
}
