using MirClient.Protocol;
using MirClient.Protocol.Network;
using MirClient.Runner;

Console.WriteLine("=================================================================");
Console.WriteLine("  MirClient.CSharp (OpenMir2 跨平台图形游戏客户端引擎)");
Console.WriteLine("=================================================================");

// 1. 读取同目录 config.ini (若不存在则自动生成一份标准模板)
string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
ClientConfig config = ClientConfig.LoadOrCreate(configPath);

// 允许通过命令行参数覆盖配置 (例如 MirClient.Runner.exe 192.168.0.103 7000)
if (args.Length > 0 && !args[0].StartsWith("-")) config.ServerAddr = args[0];
if (args.Length > 1 && int.TryParse(args[1], out int p)) config.ServerPort = p;

Console.WriteLine($"\n[配置就绪] 已加载配置文件: {configPath}");
Console.WriteLine($"  目标服务器: {config.ServerAddr}:{config.ServerPort} (区服: {config.ServerName})");
Console.WriteLine($"  显示分辨率: {config.ScreenWidth} x {config.ScreenHeight} (全屏: {config.FullScreen})");

// 2. 检查启动模式：如果带 --test 或 --cli 则仅运行控制台探针，否则默认弹出高清 2D 硬件加速游戏视口
bool isCliMode = args.Any(a => a.Equals("--cli", StringComparison.OrdinalIgnoreCase) || a.Equals("--test", StringComparison.OrdinalIgnoreCase));

if (isCliMode)
{
    Console.WriteLine("\n[探针模式] 启动纯控制台网络测试...");
    RunCliProbe(config);
}
else
{
    Console.WriteLine("\n[图形引擎] 正在启动 2D 硬件加速游戏视口窗口...");
    Console.WriteLine("  (提示: 如果要在纯命令行下测试握手，可使用 --cli 参数启动)");
    
    using GameEngine engine = new(config);
    engine.Run();
    Console.WriteLine("\n[退出] 游戏窗口已关闭。");
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
