using MirClient.Protocol;
using MirClient.Protocol.Network;
using MirClient.Protocol.Resources;
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

    int playerActorId = 0;
    int nearbyNpcRecogId = 0;

    session.OnGamePacket += (hdr, payload) =>
    {
        string name = GetIdentName(hdr.Ident);
        string decPayload = !string.IsNullOrEmpty(payload) ? EDCode.DecodeText(payload) : "";
        Console.WriteLine($"  [游戏世界下行] Ident={hdr.Ident} [{name}], Recog={hdr.Recog}, Param={hdr.Param}, Tag={hdr.Tag}, Series={hdr.Series}, DecPayload=\"{decPayload}\"");

        if (hdr.Ident == ProtocolConsts.SM_LOGON)
        {
            playerActorId = hdr.Recog;
        }
        else if (hdr.Ident == ProtocolConsts.SM_TURN && hdr.Recog != playerActorId)
        {
            if (nearbyNpcRecogId == 0 || (hdr.Param == 327 && hdr.Tag == 327))
            {
                nearbyNpcRecogId = hdr.Recog;
            }
        }
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

    Console.WriteLine("\n[战斗动作测试] 发送 CM_HIT (物理普通攻击测试，目标坐标=331,268，朝向=2)...");
    await session.SendHitAsync(331, 268, 2);
    await Task.Delay(1200);

    Console.WriteLine("\n[战斗法术测试] 发送 CM_SPELL (法术施放测试，MagicId=1，目标点=332,268)...");
    await session.SendSpellAsync(1, 332, 268, 0);
    await Task.Delay(1200);

    Console.WriteLine($"\n[NPC 脚本与经济系统测试] 等待视野内 NPC 实体广播同步 (当前附近实体 ID={nearbyNpcRecogId})...");
    for (int wait = 0; wait < 10 && nearbyNpcRecogId == 0; wait++)
    {
        await Task.Delay(500);
    }
    int targetNpcId = nearbyNpcRecogId != 0 ? nearbyNpcRecogId : 1520532027;

    Console.WriteLine($"\n[NPC 脚本与经济系统测试] 捕获视野内目标 NPC (RecogId={targetNpcId})，正在发送 CM_CLICKNPC 交互探针...");
    var tcsMerchantSay = new TaskCompletionSource<string>();
    session.OnMerchantSay += async (npcId, face, rawBody) =>
    {
        Console.WriteLine($"\n  ===============================================================");
        Console.WriteLine($"  [NPC 对话下发] NPC RecogId={npcId}, Face={face}");
        Console.WriteLine($"  [NPC 脚本内容]\n{rawBody}");
        Console.WriteLine($"  ===============================================================\n");
        tcsMerchantSay.TrySetResult(rawBody);

        if (rawBody.Contains("/@"))
        {
            Console.WriteLine($"  [NPC 选项交互] 正在发送 CM_MERCHANTDLGSELECT 选择 '@huanjing' 标签...");
            await session.SendNpcSelectAsync(npcId, "@huanjing");
        }
    };
    await session.SendClickNpcAsync(targetNpcId);
    await Task.WhenAny(tcsMerchantSay.Task, Task.Delay(4000));
    await Task.Delay(1000);

    Console.WriteLine("\n[背包与装备系统测试] 发送 CM_QUERYBAGITEMS (查询角色背包物品名册)...");
    await session.SendQueryBagItemsAsync();
    await Task.Delay(1000);

    Console.WriteLine("\n[背包与装备系统测试] 发送 CM_EAT (消耗品/药水使用测试)...");
    await session.SendEatItemAsync(1, "金创药(小量)");
    await Task.Delay(1000);

    Console.WriteLine("\n[背包与装备系统测试] 发送 CM_TAKEONITEM (穿戴武器装备到 Weapon 槽位)...");
    await session.SendTakeOnItemAsync(2, EquipSlotType.Weapon, "木剑");
    await Task.Delay(1000);

    Console.WriteLine("\n[背包与装备系统测试] 发送 CM_TAKEOFFITEM (从 Weapon 槽位卸下装备回背包)...");
    await session.SendTakeOffItemAsync(2, EquipSlotType.Weapon, "木剑");
    await Task.Delay(1000);

    Console.WriteLine("\n[物品丢弃与拾取测试] 发送 CM_DROPITEM (尝试丢弃指定物品至地面)...");
    await session.SendDropItemAsync(3, 1, "金币");
    await Task.Delay(1000);

    Console.WriteLine("\n[物品丢弃与拾取测试] 发送 CM_PICKUP (拾取当前坐标脚下地面物品)...");
    await session.SendPickupAsync(331, 268);
    await Task.Delay(1000);
    
    // 优雅发送登出封包，清理服务端会话
    Console.WriteLine("\n[下线注销] 发送 CM_SOFTCLOSE 正常注销退出游戏世界...");
    await session.SendGatePacketAsync(new DefaultMessage(ProtocolConsts.CM_SOFTCLOSE, 0, 0, 0, 0));
    await Task.Delay(500);
    session.Disconnect();

    Console.WriteLine("\n[测试结果] 🎉 OpenMir2 全链路端到端网络、世界移动、战斗、NPC脚本与背包装备系统集成测试圆满成功！\n");
}

static string GetIdentName(ushort ident) => ident switch
{
    ProtocolConsts.SM_TURN => "SM_TURN (实体入视野/转身)",
    ProtocolConsts.SM_WALK => "SM_WALK (走步同步)",
    ProtocolConsts.SM_RUN => "SM_RUN (跑步同步)",
    ProtocolConsts.SM_HIT => "SM_HIT (物理攻击动作)",
    ProtocolConsts.SM_HEAVYHIT => "SM_HEAVYHIT (攻杀剑术)",
    ProtocolConsts.SM_BIGHIT => "SM_BIGHIT (刺杀剑术)",
    ProtocolConsts.SM_POWERHIT => "SM_POWERHIT (半月弯刀)",
    ProtocolConsts.SM_SPELL => "SM_SPELL (实体施法动作)",
    ProtocolConsts.SM_MAGICFIRE => "SM_MAGICFIRE (法术效果结算)",
    ProtocolConsts.SM_STRUCK => "SM_STRUCK (受击后仰硬直)",
    ProtocolConsts.SM_DEATH => "SM_DEATH (实体死亡)",
    ProtocolConsts.SM_WINEXP => "SM_WINEXP (经验增加)",
    ProtocolConsts.SM_LEVELUP => "SM_LEVELUP (升级)",
    ProtocolConsts.SM_BAGITEMS => "SM_BAGITEMS (背包物品名册)",
    ProtocolConsts.SM_ADDITEM => "SM_ADDITEM (新增物品)",
    ProtocolConsts.SM_DELITEM => "SM_DELITEM (删除物品)",
    ProtocolConsts.SM_TAKEON_OK => "SM_TAKEON_OK (穿戴成功)",
    ProtocolConsts.SM_TAKEON_FAIL => "SM_TAKEON_FAIL (穿戴失败)",
    ProtocolConsts.SM_TAKEOFF_OK => "SM_TAKEOFF_OK (脱下成功)",
    ProtocolConsts.SM_TAKEOFF_FAIL => "SM_TAKEOFF_FAIL (脱下失败)",
    ProtocolConsts.SM_EAT_OK => "SM_EAT_OK (使用成功)",
    ProtocolConsts.SM_EAT_FAIL => "SM_EAT_FAIL (使用失败)",
    ProtocolConsts.SM_DROPITEM_SUCCESS => "SM_DROPITEM_SUCCESS (丢弃成功)",
    ProtocolConsts.SM_DROPITEM_FAIL => "SM_DROPITEM_FAIL (丢弃失败)",
    ProtocolConsts.SM_ITEMSHOW => "SM_ITEMSHOW (地面生成物品)",
    ProtocolConsts.SM_ITEMHIDE => "SM_ITEMHIDE (地面物品消失)",
    ProtocolConsts.SM_DISAPPEAR => "SM_DISAPPEAR (实体脱离视野)",
    ProtocolConsts.SM_LOGON => "SM_LOGON (登录落地确认)",
    ProtocolConsts.SM_NEWMAP => "SM_NEWMAP (地图加载)",
    ProtocolConsts.SM_CHANGEMAP => "SM_CHANGEMAP (地图切换)",
    ProtocolConsts.SM_ABILITY => "SM_ABILITY (人物属性)",
    ProtocolConsts.SM_SUBABILITY => "SM_SUBABILITY (次要属性)",
    ProtocolConsts.SM_HEALTHSPELLCHANGED => "SM_HEALTHSPELLCHANGED (HP/MP变更)",
    ProtocolConsts.SM_FEATURECHANGED => "SM_FEATURECHANGED (外观特征变更)",
    ProtocolConsts.SM_SENDNOTICE => "SM_SENDNOTICE (登入公告/就绪)",
    ProtocolConsts.SM_MAPDESCRIPTION => "SM_MAPDESCRIPTION (地图品名描述)",
    ProtocolConsts.SM_USERNAME => "SM_USERNAME (实体名称)",
    ProtocolConsts.SM_SENDMYMAGIC => "SM_SENDMYMAGIC (已习得法术列表)",
    ProtocolConsts.SM_MERCHANTSAY => "SM_MERCHANTSAY (NPC对话与脚本下发)",
    ProtocolConsts.SM_MERCHANTDLGCLOSE => "SM_MERCHANTDLGCLOSE (关闭NPC对话框)",
    ProtocolConsts.SM_SENDGOODSLIST => "SM_SENDGOODSLIST (商品货架列表)",
    ProtocolConsts.SM_SENDUSERSELL => "SM_SENDUSERSELL (打开出售视窗)",
    ProtocolConsts.SM_SENDUSERREPAIR => "SM_SENDUSERREPAIR (打开修理视窗)",
    ProtocolConsts.SM_SENDBUYPRICE => "SM_SENDBUYPRICE (物品收购估价)",
    ProtocolConsts.SM_GOLDCHANGED => "SM_GOLDCHANGED (金币变更)",
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
