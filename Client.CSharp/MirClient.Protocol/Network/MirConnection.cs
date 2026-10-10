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
/// 异步 TCP 网络客户端引擎 (1:1 支持 OpenMir2 标准 #...! 封包、粘包切分与双向加解密)
/// </summary>
public class MirConnection : IDisposable
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private readonly StringBuilder _receiveBuffer = new();

    public bool IsConnected => _client != null && _client.Connected;

    public event EventHandler<PacketReceivedEventArgs>? OnPacketReceived;
    public event EventHandler<string>? OnRawPacketReceived;
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
    /// 发送带有网关前缀的标准数据包 (#1<header><payload>!)
    /// </summary>
    public async Task SendGatePacketAsync(DefaultMessage header, string payload = "")
    {
        if (_stream == null || !IsConnected)
            throw new InvalidOperationException("客户端尚未连接到服务端");

        string inner = EDCode.EncodeMessage(header) + payload;
        string packet = $"#1{inner}!";
        byte[] bytes = EDCode.GBK.GetBytes(packet);

        await _stream.WriteAsync(bytes, 0, bytes.Length);
        await _stream.FlushAsync();
    }

    /// <summary>
    /// 发送原始文本封包 (#1<rawText>!)
    /// </summary>
    public async Task SendRawPacketAsync(string rawText)
    {
        if (_stream == null || !IsConnected)
            throw new InvalidOperationException("客户端尚未连接到服务端");

        string packet = $"#1{rawText}!";
        byte[] bytes = EDCode.GBK.GetBytes(packet);

        await _stream.WriteAsync(bytes, 0, bytes.Length);
        await _stream.FlushAsync();
    }

    /// <summary>
    /// 兼容旧式发送接口
    /// </summary>
    public Task SendPacketAsync(DefaultMessage header, string payload = "") => SendGatePacketAsync(header, payload);

    /// <summary>
    /// 接收流处理循环 (严格按 '!' 分包截断)
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

            // 清理多余控制字符与结束标符
            fullPacket = fullPacket.Trim('\r', '\n', '$');
            if (string.IsNullOrEmpty(fullPacket)) continue;

            OnRawPacketReceived?.Invoke(this, fullPacket);

            // 寻找包头起始符 '#'
            int startIdx = fullPacket.IndexOf('#');
            if (startIdx >= 0)
            {
                // 跳过 '#' 以及可能跟随的网关前缀（如 '#1' 或 '#+' 等）
                int headerStart = startIdx + 1;
                if (headerStart < fullPacket.Length && char.IsDigit(fullPacket[headerStart]))
                {
                    headerStart++; // 跳过前缀数字 '1'
                }

                // 检查剩余长度是否满足 16 字节头部 + 结束符 '!'
                int remaining = fullPacket.Length - headerStart;
                if (remaining >= 17) // 16 字节头部 + 至少 1 字节 '!'
                {
                    string encodedHeader = fullPacket.Substring(headerStart, 16);
                    string payload = fullPacket.Substring(headerStart + 16, remaining - 17);

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
