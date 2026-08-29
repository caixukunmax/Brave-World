using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 开发期服务器托管启动器（仅编辑器/dev 构建生效）。
    ///
    /// 目标：客户端启动时确保有一个可用的 GameServer，实现"客户端一开服务器自动起、
    /// 客户端随便重启服务器不动"的开发体验。不改架构、不碰传输层。
    ///
    /// 两种模式：
    /// - 附加模式：探测到 8889 已被监听（你手动 restart.bat 起的调试实例）→ 直接复用，
    ///   不 spawn、退出时也绝不杀它。
    /// - 托管模式：8889 空闲 → 隐藏拉起 GameServer.exe（stdout/stderr 落盘到 .godot/dev-server.log），
    ///   记录 PID，客户端退出时只清理自己拉起的这个进程。
    ///
    /// 实际"连上"由 NetworkManager 的自动重连负责（服务器启动需几秒，重连循环会自然等到它就绪）。
    /// </summary>
    public partial class DevServerLauncher : Node
    {
        /// <summary>是否在客户端启动时自动拉起服务器（false 时永远走附加模式）。</summary>
        [Export] public bool AutoSpawnServer { get; set; } = true;

        /// <summary>托管模式下是否弹出可见控制台窗口（true=盯日志方便；false=隐藏后台跑，日志落盘）。</summary>
        [Export] public bool ShowServerConsole { get; set; } = false;

        /// <summary>探测已有服务器时的连接超时（毫秒）。</summary>
        [Export] public int ProbeTimeoutMs { get; set; } = 250;

        private const int ServerPort = 8889;
        private const string ServerHost = "127.0.0.1";

        private Process? _serverProcess;
        private bool _spawnedByUs;
        private readonly object _logLock = new object();

        public override void _Ready()
        {
            // 仅在"编辑器构建"下启用（按 F5 从编辑器跑游戏时该二进制带 editor_build 特性；
            // 导出发布版用的是不含该特性的模板，连真实远端服务器，不该本地 spawn）。
            // 注意：不能用 OS.HasFeature("editor")——那是编辑器进程自身的特性，
            // F5 启动的游戏进程不带它，会导致本启动器在开发运行场景里永不触发。
            if (!OS.HasFeature("editor_build"))
            {
                GD.Print("[DevServerLauncher] 非编辑器构建环境，跳过服务器托管。");
                return;
            }

            if (IsPortOpen(ServerHost, ServerPort, ProbeTimeoutMs))
            {
                GD.Print($"[DevServerLauncher] 端口 {ServerPort} 已有服务器监听 → 附加模式，复用现有实例（不会重复启动，退出也不杀它）。");
                return;
            }

            if (!AutoSpawnServer)
            {
                GD.Print($"[DevServerLauncher] 端口 {ServerPort} 空闲，但 AutoSpawnServer=false，不自动拉起。");
                return;
            }

            SpawnServer();
        }

        private void SpawnServer()
        {
            string clientDir = ProjectSettings.GlobalizePath("res://");
            string repoRoot = Path.GetFullPath(Path.Combine(clientDir, ".."));
            string serverExe = Path.Combine(repoRoot, "servercsharp", "src", "GameServer", "bin", "Debug", "net8.0", "GameServer.exe");

            if (!File.Exists(serverExe))
            {
                GD.PushError($"[DevServerLauncher] 找不到 GameServer.exe：{serverExe}\n请先构建服务器（servercsharp/restart.bat 或 scripts/build.ps1 dev）。NetworkManager 会自动重连，等你把服务器跑起来即可。");
                return;
            }

            try
            {
                bool hidden = !ShowServerConsole;
                var psi = new ProcessStartInfo
                {
                    FileName = serverExe,
                    WorkingDirectory = repoRoot, // 与 build.ps1 的 cwd 一致；服务器内部有路径回退，稳
                    UseShellExecute = false,
                    CreateNoWindow = hidden,
                    RedirectStandardOutput = hidden,
                    RedirectStandardError = hidden,
                };

                _serverProcess = Process.Start(psi);
                _spawnedByUs = _serverProcess != null;

                if (_serverProcess == null)
                {
                    GD.PushError("[DevServerLauncher] Process.Start 返回 null，未能启动服务器。");
                    return;
                }

                if (hidden)
                {
                    string logPath = Path.Combine(clientDir, ".godot", "dev-server.log");
                    _serverProcess.OutputDataReceived += (_, e) => AppendLog(logPath, e.Data);
                    _serverProcess.ErrorDataReceived += (_, e) => AppendLog(logPath, e.Data);
                    _serverProcess.BeginOutputReadLine();
                    _serverProcess.BeginErrorReadLine();
                    GD.Print($"[DevServerLauncher] 已隐藏拉起 GameServer (PID={_serverProcess.Id})，服务器日志 → {logPath}");
                }
                else
                {
                    GD.Print($"[DevServerLauncher] 已拉起 GameServer 可见控制台 (PID={_serverProcess.Id})");
                }
            }
            catch (Exception ex)
            {
                GD.PushError($"[DevServerLauncher] 启动服务器失败：{ex.Message}");
            }
        }

        private void AppendLog(string logPath, string? line)
        {
            if (line == null)
                return;
            try
            {
                lock (_logLock)
                {
                    File.AppendAllText(logPath, line + System.Environment.NewLine);
                }
            }
            catch
            {
                // 日志写入失败不影响主流程
            }
        }

        private static bool IsPortOpen(string host, int port, int timeoutMs)
        {
            try
            {
                using var client = new TcpClient();
                IAsyncResult ar = client.BeginConnect(host, port, null, null);
                bool ok = ar.AsyncWaitHandle.WaitOne(timeoutMs);
                if (ok && client.Connected)
                {
                    client.EndConnect(ar);
                    return true;
                }
                client.Close();
                return false;
            }
            catch
            {
                return false;
            }
        }

        public override void _ExitTree()
        {
            // 只清理我们自己拉起的进程；附加模式下绝不碰别人的服务器。
            if (!_spawnedByUs || _serverProcess == null)
                return;

            try
            {
                if (!_serverProcess.HasExited)
                {
                    _serverProcess.Kill(entireProcessTree: true);
                    try { _serverProcess.WaitForExit(3000); } catch { /* 忽略等待失败 */ }
                    GD.Print($"[DevServerLauncher] 客户端退出，已终止托管的 GameServer (PID={_serverProcess.Id})。");
                }
            }
            catch (Exception ex)
            {
                GD.PushWarning($"[DevServerLauncher] 终止服务器进程失败：{ex.Message}");
            }
            finally
            {
                _serverProcess?.Dispose();
                _serverProcess = null;
            }
        }
    }
}
