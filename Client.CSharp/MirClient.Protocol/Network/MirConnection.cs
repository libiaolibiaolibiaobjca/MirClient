using System.Net.Sockets;
using System.Text;

namespace MirClient.Protocol.Network;

/// <summary>
/// 封包接收事件参数
/// </summary>
public class PacketReceivedEventArgs : EventArgs
{
    public DefaultMessage Header { get; }
    public string Payload { get; }
    public string RawPacket { get; }

    public PacketReceivedEventArgs(DefaultMessage header, string payload, string rawPacket)
    {
        Header = header;
        Payload = payload;
        RawPacket = rawPacket;
    }
}

/// <summary>
/// 异步 TCP 网络客户端引擎 (处理传奇标准 #...! 封包、粘包断包与状态通知)
/// </summary>
public class MirConnection : IDisposable
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private readonly StringBuilder _receiveBuffer = new();

    public bool IsConnected => _client != null && _client.Connected;

    public event EventHandler<PacketReceivedEventArgs>? OnPacketReceived;
    public event EventHandler? OnConnected;
    public event EventHandler<string>? OnDisconnected;

    /// <summary>
    /// 异步连接目标服务端
    /// </summary>
    public async Task ConnectAsync(string host, int port, int timeoutMs = 5000)
    {
        Disconnect();

        _client = new TcpClient();
        _cts = new CancellationTokenSource();

        using var timeoutCts = new CancellationTokenSource(timeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);

        await _client.ConnectAsync(host, port, linkedCts.Token);
        _stream = _client.GetStream();

        OnConnected?.Invoke(this, EventArgs.Empty);

        // 启动异步接收循环
        _ = ReceiveLoopAsync(_cts.Token);
    }

    /// <summary>
    /// 发送标准封包
    /// </summary>
    public async Task SendPacketAsync(DefaultMessage header, string payload = "")
    {
        if (_stream == null || !IsConnected)
        {
            throw new InvalidOperationException("客户端尚未连接到服务端");
        }

        string packet = EDCode.MakePacket(header, payload);
        byte[] bytes = EDCode.GBK.GetBytes(packet);

        await _stream.WriteAsync(bytes, 0, bytes.Length);
        await _stream.FlushAsync();
    }

    /// <summary>
    /// 接收流处理循环 (严格按 '!' 分包)
    /// </summary>
    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        byte[] buffer = new byte[4096];

        try
        {
            while (!token.IsCancellationRequested && _stream != null)
            {
                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, token);
                if (bytesRead == 0) break; // 对端关闭

                string chunk = EDCode.GBK.GetString(buffer, 0, bytesRead);
                _receiveBuffer.Append(chunk);

                // 处理分包截断
                ProcessPackets();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            OnDisconnected?.Invoke(this, ex.Message);
            return;
        }

        OnDisconnected?.Invoke(this, "连接已断开");
    }

    private void ProcessPackets()
    {
        while (true)
        {
            string content = _receiveBuffer.ToString();
            int endIdx = content.IndexOf('!');
            if (endIdx < 0) break; // 尚无完整封包，等待更多数据

            string fullPacket = content[..(endIdx + 1)];
            _receiveBuffer.Remove(0, endIdx + 1);

            // 清理多余控制字符
            fullPacket = fullPacket.Trim('\r', '\n');
            if (string.IsNullOrEmpty(fullPacket)) continue;

            // 标准包以 '#' 起始且至少拥有 16 字节包头 + 结束符 '!'
            int startIdx = fullPacket.IndexOf('#');
            if (startIdx >= 0 && (fullPacket.Length - startIdx) >= 18)
            {
                string encodedHeader = fullPacket.Substring(startIdx + 1, 16);
                string payload = fullPacket.Substring(startIdx + 17, fullPacket.Length - (startIdx + 18));

                try
                {
                    DefaultMessage header = EDCode.DecodeMessage(encodedHeader);
                    OnPacketReceived?.Invoke(this, new PacketReceivedEventArgs(header, payload, fullPacket));
                }
                catch
                {
                    // 忽略格式损坏的脏包
                }
            }
        }
    }

    public void Disconnect()
    {
        _cts?.Cancel();
        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;
        _receiveBuffer.Clear();
    }

    public void Dispose()
    {
        Disconnect();
        _cts?.Dispose();
    }
}
