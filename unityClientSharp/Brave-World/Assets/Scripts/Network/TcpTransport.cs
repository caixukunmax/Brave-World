using System;
using System.Net.Sockets;

namespace UnityClientSharp.Net
{
    /// <summary>
    /// TCP 传输封装（System.Net.Sockets.TcpClient）。
    /// 移植自 Godot StreamPeerTcp 的用法语义：<b>完全单线程</b>——在 Update 中轮询接收，
    /// 发送同步调用；不起后台线程、无锁（AGENTS.md：网络收发只准单线程轮询）。
    /// </summary>
    public class TcpTransport
    {
        private TcpClient _client;
        private IAsyncResult _connectAr;

        /// <summary>是否已建立连接（本地视角；远端断开由 PollDisconnected 探测）。</summary>
        public bool IsConnected => _client != null && _client.Connected;

        /// <summary>开始异步连接（立即返回，由 FinishConnect 轮询结果）。</summary>
        public bool BeginConnect(string host, int port, out string error)
        {
            error = null;
            Close();
            try
            {
                _client = new TcpClient();
                _connectAr = _client.BeginConnect(host, port, null, null);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                _client = null;
                return false;
            }
        }

        /// <summary>连接尝试是否已完成（成功或失败）。</summary>
        public bool ConnectCompleted => _connectAr != null && _connectAr.IsCompleted;

        /// <summary>结束连接尝试；成功后切为非阻塞模式供 Update 轮询。</summary>
        public bool FinishConnect(out string error)
        {
            error = null;
            try
            {
                _client.EndConnect(_connectAr);
                _client.Client.Blocking = false;
                _client.NoDelay = true;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Close();
                return false;
            }
            finally
            {
                _connectAr = null;
            }
        }

        /// <summary>可读字节数。</summary>
        public int Available => IsConnected ? _client.Available : 0;

        /// <summary>
        /// 非阻塞读取到 buffer[offset..]；返回读取字节数；无数据返回 0；对端关闭返回 -1。
        /// </summary>
        public int Receive(byte[] buffer, int offset, int maxBytes)
        {
            if (!IsConnected) return -1;
            try
            {
                if (_client.Available <= 0) return 0;
                int n = _client.Client.Receive(buffer, offset, Math.Min(maxBytes, _client.Available), SocketFlags.None);
                return n == 0 ? -1 : n; // 对端正常关闭时 Receive 返回 0
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock)
            {
                return 0;
            }
            catch (Exception)
            {
                return -1; // 连接已断
            }
        }

        /// <summary>同步发送整包（小包；与 Godot PutData 同步语义一致）。</summary>
        public bool Send(byte[] data)
        {
            if (!IsConnected) return false;
            try
            {
                int sent = 0;
                while (sent < data.Length)
                {
                    int n = _client.Client.Send(data, sent, data.Length - sent, SocketFlags.None);
                    if (n <= 0) return false;
                    sent += n;
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>探测对端是否已断开（可读但无数据 = FIN）。</summary>
        public bool PollDisconnected()
        {
            if (!IsConnected) return true;
            try
            {
                return _client.Client.Poll(0, SelectMode.SelectRead) && _client.Available == 0;
            }
            catch (Exception)
            {
                return true;
            }
        }

        public void Close()
        {
            try { _client?.Close(); } catch { /* 忽略 */ }
            _client = null;
            _connectAr = null;
        }
    }
}
