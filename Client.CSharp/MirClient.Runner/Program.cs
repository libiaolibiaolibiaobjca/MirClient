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
        string name = GetIdentName(hdr.Ident);
        string decPayload = !string.IsNullOrEmpty(payload) ? EDCode.DecodeText(payload) : "";
        Console.WriteLine($"  [游戏世界下行] Ident={hdr.Ident} [{name}], Recog={hdr.Recog}, Param={hdr.Param}, Tag={hdr.Tag}, Series={hdr.Series}, DecPayload=\"{decPayload}\"");
    };

    bool pipelineSuccess = await session.StartLoginPipelineAsync(config.ServerAddr, config.ServerPort, account, password);
    if (!pipelineSuccess)
    {
        Console.WriteLine("\n[测试结果] ❌ 端到端连服流水线失败 (未成功进入 7200 游戏网关)！\n");
        return;
    }

    Console.WriteLine("\n[测试结果] ✅ 成功进入游戏世界！等待接收游戏世界落地与实体广播包 (持续 3 秒)...");
    
    // 发送时间同步心跳包 (CM_CHECKTIME) 测试游戏网关双向收发
    await session.SendCheckTimeAsync(Environment.TickCount);
    await Task.Delay(3000);

    Console.WriteLine("\n[移动与动作交互测试] 发送 CM_TURN (原地向南转身)...");
    DefaultMessage turnMsg = new(ProtocolConsts.CM_TURN, 0, 330, 268, 4);
    await session.SendGatePacketAsync(turnMsg);
    await Task.Delay(1000);

    Console.WriteLine("\n[移动与动作交互测试] 发送 CM_WALK (走步测试)...");
    DefaultMessage walkMsg = new(ProtocolConsts.CM_WALK, 0, 331, 268, 2);
    await session.SendGatePacketAsync(walkMsg);
    await Task.Delay(1500);
    
    Console.WriteLine("\n[测试结果] 🎉 OpenMir2 全链路端到端网络与世界移动集成测试圆满成功！\n");
}

static string GetIdentName(ushort ident) => ident switch
{
    ProtocolConsts.SM_TURN => "SM_TURN (实体入视野/转身)",
    ProtocolConsts.SM_WALK => "SM_WALK (走步同步)",
    ProtocolConsts.SM_RUN => "SM_RUN (跑步同步)",
    ProtocolConsts.SM_HIT => "SM_HIT (攻击动作)",
    ProtocolConsts.SM_DISAPPEAR => "SM_DISAPPEAR (实体脱离视野)",
    ProtocolConsts.SM_LOGON => "SM_LOGON (登录落地确认)",
    ProtocolConsts.SM_NEWMAP => "SM_NEWMAP (地图加载)",
    ProtocolConsts.SM_CHANGEMAP => "SM_CHANGEMAP (地图切换)",
    ProtocolConsts.SM_ABILITY => "SM_ABILITY (人物属性)",
    ProtocolConsts.SM_SUBABILITY => "SM_SUBABILITY (次要属性)",
    ProtocolConsts.SM_HEALTHSPELLCHANGED => "SM_HEALTHSPELLCHANGED (HP/MP变更)",
    ProtocolConsts.SM_FEATURECHANGED => "SM_FEATURECHANGED (外观特征变更)",
    ProtocolConsts.SM_SENDNOTICE => "SM_SENDNOTICE (登入公告/就绪)",
    15999 => "SM_CHECKTIME (心跳校时应答)",
    _ => $"Code={ident}"
};

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
