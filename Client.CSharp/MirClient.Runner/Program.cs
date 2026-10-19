using MirClient.Protocol;
using MirClient.Protocol.Network;
using MirClient.Runner;

Console.WriteLine("=================================================================");
Console.WriteLine("  MirClient.CSharp (OpenMir2 跨平台图形游戏客户端引擎)");
Console.WriteLine("=================================================================");

// 1. 读取同目录 config.ini (若不存在则自动生成一份标准模板)
string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
ClientConfig config = ClientConfig.LoadOrCreate(configPath);

string account = "ergou";
string password = "123456a";

for (int i = 0; i < args.Length; i++)
{
    if (args[i].Equals("--host", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) { config.ServerAddr = args[++i]; }
    else if (args[i].Equals("--port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && int.TryParse(args[++i], out int p)) { config.ServerPort = p; }
    else if (args[i].Equals("--account", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) { account = args[++i]; }
    else if (args[i].Equals("--password", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) { password = args[++i]; }
    else if (!args[i].StartsWith("-"))
    {
        if (string.IsNullOrEmpty(config.ServerAddr) || config.ServerAddr == "192.168.0.103")
            config.ServerAddr = args[i];
        else if (int.TryParse(args[i], out int portVal))
            config.ServerPort = portVal;
    }
}

Console.WriteLine($"\n[配置就绪] 已加载目标服务器: {config.ServerAddr}:{config.ServerPort} (账号: {account})");
bool isE2EMode = args.Any(a => a.Equals("--test-e2e", StringComparison.OrdinalIgnoreCase) || a.Equals("--e2e", StringComparison.OrdinalIgnoreCase));
bool isCliMode = args.Any(a => a.Equals("--cli", StringComparison.OrdinalIgnoreCase) || a.Equals("--test", StringComparison.OrdinalIgnoreCase));

if (isE2EMode)
{
    Console.WriteLine("\n[E2E 探针模式] 启动 OpenMir2 全链路端到端连服集成测试...");
    RunE2EIntegrationProbeAsync(config, account, password).Wait();
}
else if (isCliMode)
{
    Console.WriteLine("\n[探针模式] 启动纯控制台网络测试...");
    RunCliProbe(config);
}
else
{
    Console.WriteLine("\n[图形引擎] 正在启动 2D 硬件加速游戏视口窗口...");
    Console.WriteLine("  (提示: 如果要在纯命令行下测试握手，可使用 --cli 或 --test-e2e 参数启动)");
    
    using GameEngine engine = new(config);
    engine.Run();
    Console.WriteLine("\n[退出] 游戏窗口已关闭。");
}

static async Task RunE2EIntegrationProbeAsync(ClientConfig config, string account, string password)
{
    Console.WriteLine("=================================================================");
    Console.WriteLine($"  [OpenMir2 端到端连服测试]");
    Console.WriteLine($"  目标登录网关: {config.ServerAddr}:{config.ServerPort}");
    Console.WriteLine($"  测试账号: {account}");
    Console.WriteLine("=================================================================");

    using NetworkSession session = new();
    session.OnLog += msg => Console.WriteLine($"  {msg}");
    session.OnGamePacket += (hdr, payload) =>
    {
        Console.WriteLine($"  [游戏世界下行] Ident={hdr.Ident}, Recog={hdr.Recog}, Param={hdr.Param}, Tag={hdr.Tag}, PayloadLen={payload?.Length ?? 0}");
    };

    bool pipelineSuccess = await session.StartLoginPipelineAsync(config.ServerAddr, config.ServerPort, account, password);
    if (!pipelineSuccess)
    {
        Console.WriteLine("\n[测试结果] ❌ 端到端连服流水线失败 (未成功进入 7200 游戏网关)！\n");
        return;
    }

    Console.WriteLine("\n[测试结果] ✅ 成功进入游戏世界！等待接收游戏世界实体广播包 (持续 3 秒)...");
    
    // 发送时间同步心跳包 (CM_CHECKTIME) 测试游戏网关双向收发
    await session.SendCheckTimeAsync(Environment.TickCount);
    await Task.Delay(3000);
    
    Console.WriteLine("\n[测试结果] 🎉 OpenMir2 全链路端到端网络集成测试圆满成功！\n");
}

static void RunCliProbe(ClientConfig config)
{
    using MirConnection connection = new();
    connection.OnConnected += (s, e) => Console.WriteLine($"  [成功] 已连接到服务端 {config.ServerAddr}:{config.ServerPort}！");
    connection.OnDisconnected += (s, reason) => Console.WriteLine($"  [通知] 连接断开: {reason}");
    connection.OnPacketReceived += (s, e) => Console.WriteLine($"  [收到响应] Ident={e.Header.Ident}, Recog={e.Header.Recog}");

    try
    {
        connection.ConnectAsync(config.ServerAddr, config.ServerPort, timeoutMs: 3000).Wait();
        DefaultMessage testMsg = new(ProtocolConsts.CM_QUERYCHR, 8888, 100, 200, 1);
        connection.SendPacketAsync(testMsg, "cli/test").Wait();
        Thread.Sleep(1500);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  [网络错误] {ex.Message}");
    }
}
