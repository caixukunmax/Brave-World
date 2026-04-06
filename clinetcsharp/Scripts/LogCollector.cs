using Godot;
using Godot.Collections;

namespace ClinetCSharp
{
    /// <summary>
    /// 全局日志收集器
    /// 通过读取 Godot 文件日志捕获所有 print() 和引擎输出
    /// 在 screenshot_tool 中用于生成带日志的截图
    /// </summary>
    public partial class LogCollector : Node
    {
        // 日志条目
        public class LogEntry
        {
            public string Timestamp { get; set; }
            public string Level { get; set; }  // INFO, WARNING, ERROR
            public string Message { get; set; }

            public LogEntry(string message, string level = "INFO")
            {
                Timestamp = Time.GetTimeStringFromSystem();
                Level = level;
                Message = message;
            }

            public override string ToString()
            {
                return $"[{Timestamp}] [{Level}] {Message}";
            }
        }

        // 存储的日志
        private System.Collections.Generic.List<LogEntry> _logs = new();
        [Export] public int MaxLogs { get; set; } = 100;

        // 文件日志读取
        private string _logFilePath = "user://logs/godot.log";
        private FileAccess _logFile;
        private ulong _fileReadPos;
        private bool _fileLoggingEnabled = false;

        public override void _Ready()
        {
            // 设置自动加载名称便于查找
            Name = "LogCollector";

            // 开始读取文件日志
            SetupFileLogReader();

            GD.Print("[LogCollector] 日志收集器已启动");
        }

        public override void _Process(double _delta)
        {
            if (_fileLoggingEnabled && _logFile != null)
            {
                ReadNewLogs();
            }
            else
            {
                // 如果还没连上，持续尝试
                TryOpenLogFile();
            }
        }

        private void SetupFileLogReader()
        {
            // 确保日志目录存在
            var logDir = _logFilePath.GetBaseDir();
            var dir = DirAccess.Open("user://");
            if (dir != null)
            {
                var relativeDir = logDir.Replace("user://", "");
                if (!dir.DirExists(relativeDir))
                {
                    dir.MakeDirRecursive(relativeDir);
                }
            }

            // 尝试打开日志文件
            TryOpenLogFile();

            if (!_fileLoggingEnabled)
            {
                GD.PushWarning("[LogCollector] 无法读取 Godot 文件日志，将只收集主动添加的日志");
            }
        }

        private void TryOpenLogFile()
        {
            if (_logFile != null)
                return;

            _logFile = FileAccess.Open(_logFilePath, FileAccess.ModeFlags.Read);
            if (_logFile != null)
            {
                _fileLoggingEnabled = true;
                _fileReadPos = _logFile.GetLength();
                GD.Print("[LogCollector] 已连接到 Godot 文件日志: " + _logFilePath);
            }
            // 文件可能还没创建，下次 _Process 再试
        }

        private void ReadNewLogs()
        {
            if (_logFile == null)
                return;

            var currentLength = _logFile.GetLength();
            if (currentLength < _fileReadPos)
            {
                // 文件被截断了（比如重启了），从头读
                _fileReadPos = 0;
                _logFile.Seek(0);
            }

            if (currentLength == _fileReadPos)
                return;

            _logFile.Seek(_fileReadPos);
            while (_logFile.GetPosition() < currentLength)
            {
                var line = _logFile.GetLine();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var level = DetectLevel(line);
                var entry = new LogEntry(line, level);
                _logs.Add(entry);

                // 限制日志数量
                if (_logs.Count > MaxLogs)
                    _logs.RemoveAt(0);
            }

            _fileReadPos = _logFile.GetPosition();
        }

        private string DetectLevel(string line)
        {
            var lower = line.ToLower();
            if (lower.Contains("error") || lower.Contains("script error") || lower.Contains("exception") || lower.Contains("fatal"))
                return "ERROR";
            if (lower.Contains("warning") || lower.Contains("warn"))
                return "WARNING";
            return "INFO";
        }

        /// <summary>
        /// 添加日志条目（供其他脚本主动调用）
        /// </summary>
        public void AddLog(string message, string level = "INFO")
        {
            var entry = new LogEntry(message, level);
            _logs.Add(entry);

            // 限制日志数量
            if (_logs.Count > MaxLogs)
                _logs.RemoveAt(0);
        }

        /// <summary>
        /// 获取最近的日志（用于截图）
        /// </summary>
        public string GetRecentLogs(int count = 20, bool includeErrorsOnly = false)
        {
            if (_logs.Count == 0)
                return "暂无日志";

            var result = new System.Collections.Generic.List<string>();
            var startIdx = Mathf.Max(0, _logs.Count - count);

            for (int i = startIdx; i < _logs.Count; i++)
            {
                var entry = _logs[i];
                if (includeErrorsOnly && entry.Level != "ERROR")
                    continue;
                result.Add(entry.ToString());
            }

            return string.Join("\n", result);
        }

        /// <summary>
        /// 获取所有错误日志
        /// </summary>
        public string GetErrorLogs()
        {
            var errors = new System.Collections.Generic.List<string>();
            foreach (var entry in _logs)
            {
                if (entry.Level == "ERROR")
                    errors.Add(entry.ToString());
            }

            if (errors.Count == 0)
                return "";
            return string.Join("\n", errors);
        }

        /// <summary>
        /// 是否有错误
        /// </summary>
        public bool HasErrors()
        {
            foreach (var entry in _logs)
            {
                if (entry.Level == "ERROR")
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 清空日志
        /// </summary>
        public void ClearLogs()
        {
            _logs.Clear();
        }

        public override void _ExitTree()
        {
            if (_logFile != null)
            {
                _logFile.Close();
            }
            // 离开时打印统计
            GD.Print($"[LogCollector] 会话结束，共收集 {_logs.Count} 条日志");
        }
    }
}
