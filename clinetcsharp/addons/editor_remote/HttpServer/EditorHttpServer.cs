using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Godot;

namespace ClinetCSharp.Editor.Remote
{
    /// <summary>
    /// HTTP 服务器核心。
    /// 使用 System.Net.HttpListener 在后台线程监听请求，
    /// 通过 _Process 轮询队列把请求调度回编辑器主线程执行（Godot API 必须在主线程调用）。
    /// </summary>
    public partial class EditorHttpServer : Node
    {
        public int Port { get; private set; } = -1;
        public bool IsRunning => _listener != null && _listener.IsListening;

        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private Task _loopTask;
        private readonly Dictionary<string, Func<JsonDocument, object>> _handlers = new();

        // 后台线程 → 主线程 的请求队列
        private readonly ConcurrentQueue<PendingRequest> _pendingRequests = new();

        private class PendingRequest
        {
            public string Key = "";
            public JsonDocument Body = null!;
            public TaskCompletionSource<object> Tcs = null!;
        }

        public override void _EnterTree()
        {
            ProcessMode = ProcessModeEnum.Always;
            Name = "EditorHttpServer";
            SetProcess(true);
        }

        public override void _ExitTree()
        {
            Stop();
        }

        public override void _Process(double delta)
        {
            // 每帧处理队列中的请求（都在主线程上执行）
            int processed = 0;
            const int maxPerFrame = 20;

            while (processed < maxPerFrame && _pendingRequests.TryDequeue(out var req))
            {
                processed++;
                DispatchToHandler(req);
            }
        }

        // =============== 注册路由 ===============

        public void RegisterHandler(string method, string path, Func<JsonDocument, object> handler)
        {
            var key = $"{method.ToUpperInvariant()} {path}";
            _handlers[key] = handler;
        }

        // =============== 启动/停止 ===============

        public bool Start(int port)
        {
            if (IsRunning) return true;

            try
            {
                Port = port;
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{port}/");
                _listener.Start();
                _cts = new CancellationTokenSource();
                _loopTask = RunLoopAsync(_cts.Token);
                return true;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[EditorRemote] Start failed: {ex.Message}");
                _listener = null;
                return false;
            }
        }

        public void Stop()
        {
            try
            {
                _cts?.Cancel();
                _listener?.Stop();
                _listener?.Close();
            }
            catch
            {
                // 忽略停止时的异常
            }
            _listener = null;
            _cts = null;
            Port = -1;

            // 清空队列，取消所有待处理请求
            while (_pendingRequests.TryDequeue(out var req))
            {
                req.Tcs.TrySetCanceled();
            }
        }

        // =============== 主循环（后台线程） ===============

        private async Task RunLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _listener != null && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync().WaitAsync(ct);
                    _ = Task.Run(() => HandleRequestAsync(context, ct), ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[EditorRemote] Loop error: {ex.Message}");
                }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken ct)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;

                // CORS
                response.Headers["Access-Control-Allow-Origin"] = "*";
                response.Headers["Access-Control-Allow-Methods"] = "GET,POST,PUT,DELETE,OPTIONS";
                response.Headers["Access-Control-Allow-Headers"] = "Content-Type,Authorization";

                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 204;
                    response.Close();
                    return;
                }

                // 读取 body
                string bodyText = null;
                if (request.HasEntityBody)
                {
                    using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                    bodyText = await reader.ReadToEndAsync(ct);
                }

                // 解析 JSON body（如果有）
                JsonDocument bodyDoc = null;
                if (!string.IsNullOrEmpty(bodyText) && request.ContentType?.Contains("application/json") == true)
                {
                    try
                    {
                        bodyDoc = JsonDocument.Parse(bodyText);
                    }
                    catch (JsonException)
                    {
                        await WriteJsonResponseAsync(response, 400, new { error = "Invalid JSON body" });
                        return;
                    }
                }

                // 合并 query string 到 JSON 中（GET 请求）
                if (bodyDoc == null && request.HttpMethod == "GET" && request.QueryString.Count > 0)
                {
                    var dict = new Dictionary<string, JsonElement>();
                    foreach (var key in request.QueryString.AllKeys)
                    {
                        if (key == null) continue;
                        var val = request.QueryString[key];
                        dict[key] = JsonSerializer.SerializeToElement(val);
                    }
                    bodyDoc = JsonDocument.Parse(JsonSerializer.Serialize(dict));
                }

                // 查找 handler
                var reqPath = request.Url.AbsolutePath;
                var reqMethod = request.HttpMethod;
                var handlerKey = $"{reqMethod} {reqPath}";

                if (!_handlers.ContainsKey(handlerKey))
                {
                    await WriteJsonResponseAsync(response, 404, new
                    {
                        error = "Not found",
                        method = reqMethod,
                        path = reqPath,
                        available_endpoints = _handlers.Keys
                    });
                    return;
                }

                // 把请求排入主线程队列
                var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pendingRequests.Enqueue(new PendingRequest
                {
                    Key = handlerKey,
                    Body = bodyDoc ?? JsonDocument.Parse("{}"),
                    Tcs = tcs
                });

                // 等待主线程处理结果
                var handlerResult = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
                await WriteJsonResponseAsync(response, 200, new { ok = true, data = handlerResult });
            }
            catch (Exception ex)
            {
                try
                {
                    GD.PrintErr($"[EditorRemote] Handle error: {ex.Message}");
                    await WriteJsonResponseAsync(context.Response, 500, new
                    {
                        error = ex.Message,
                        stack_trace = ex.StackTrace
                    });
                }
                catch
                {
                    // 无法写入响应
                }
            }
        }

        // =============== 在主线程上执行 handler ===============

        private void DispatchToHandler(PendingRequest req)
        {
            try
            {
                if (!_handlers.TryGetValue(req.Key, out var handler))
                {
                    req.Tcs.TrySetException(new Exception("Handler not found"));
                    return;
                }

                var result = handler(req.Body);
                req.Tcs.TrySetResult(result ?? new { });
            }
            catch (Exception ex)
            {
                req.Tcs.TrySetException(ex);
            }
            finally
            {
                req.Body?.Dispose();
            }
        }

        // =============== 工具 ===============

        private static async Task WriteJsonResponseAsync(HttpListenerResponse response, int statusCode, object body)
        {
            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";

            var json = JsonSerializer.Serialize(body, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var bytes = Encoding.UTF8.GetBytes(json);
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            response.Close();
        }
    }
}
