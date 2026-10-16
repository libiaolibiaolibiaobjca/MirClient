using System.Diagnostics;
using MirClient.Protocol.Resources;

namespace MirClient.Protocol.Network;

public enum NetworkStep
{
    Idle,
    ConnectingLoginGate,
    LoginGateSuccess,
    ConnectingSelChrGate,
    QueryingCharacters,
    CharacterReady,
    ConnectingGameGate,
    InWorld,
    Failed
}

/// <summary>
/// 盛大十周年 / OpenMir2 三阶段网络会话协调管理器 (7000 LoginGate -> 7100 SelChrGate -> 7200 GameGate)
/// </summary>
public class NetworkSession : IDisposable
{
    private readonly MirConnection _conn = new();
    private TaskCompletionSource<bool>? _stepTcs;

    public bool IsConnected => _conn.IsConnected;
    public NetworkStep Step { get; private set; } = NetworkStep.Idle;
    public string StatusText { get; private set; } = "未连接";

    // 凭证与配置
    public string Account { get; private set; } = "";
    public string Password { get; private set; } = "";
    public int Certification { get; private set; } = 0;
    public string CharacterName { get; private set; } = "逐鹿天下";
    public int CharacterJob { get; private set; } = 0;   // 0: 战士, 1: 法师, 2: 道士
    public int CharacterGender { get; private set; } = 0;// 0: 男, 1: 女
    public int CharacterHair { get; private set; } = 1;  // 1: 短发

    // 网关端点
    public string SelChrHost { get; private set; } = "192.168.0.103";
    public int SelChrPort { get; private set; } = 7100;
    public string GameGateHost { get; private set; } = "192.168.0.103";
    public int GameGatePort { get; private set; } = 7200;

    // 消息通知事件
    public event Action<string>? OnLog;
    public event Action<DefaultMessage, string>? OnGamePacket;
    public event Action<int, int, string>? OnMerchantSay;
    public event Action? OnMerchantClose;
    public event Action<GroundItemShowEvent>? OnGroundItemShow;
    public event Action<GroundItemHideEvent>? OnGroundItemHide;
    public event Action<bool>? OnDropItemResult;

    public NetworkSession()
    {
        _conn.OnPacketReceived += HandlePacket;
        _conn.OnDisconnected += (s, reason) =>
        {
            if (Step != NetworkStep.InWorld && Step != NetworkStep.Idle)
            {
                Log($"[网络] 连接断开: {reason}");
            }
        };
    }

    private void Log(string msg)
    {
        StatusText = msg;
        Console.WriteLine(msg);
        OnLog?.Invoke(msg);
    }

    /// <summary>
    /// 向当前连接的网关发送标准封包（如 CM_WALK, CM_RUN, CM_HIT 等）
    /// </summary>
    public async Task SendGatePacketAsync(DefaultMessage msg, string payload = "")
    {
        if (_conn.IsConnected)
        {
            await _conn.SendGatePacketAsync(msg, payload);
        }
    }

    /// <summary>
    /// 一键驱动完整登录与选角流水线至 7200 游戏网关
    /// </summary>
    public async Task<bool> StartLoginPipelineAsync(string host, int port, string account, string password)
    {
        Account = account;
        Password = password;

        try
        {
            // ==========================================
            // 阶段 1: 登录认证 (7000 端口 LoginGate)
            // ==========================================
            Step = NetworkStep.ConnectingLoginGate;
            Log($"[网络 1/3] 正在连接登录网关 {host}:{port} ...");
            await _conn.ConnectAsync(host, port, timeoutMs: 3000);

            _stepTcs = new TaskCompletionSource<bool>();
            DefaultMessage loginMsg = new(ProtocolConsts.CM_IDPASSWORD, 0, 0, 0, 0);
            string loginPayload = EDCode.EncodeString($"{account}/{password}");
            await _conn.SendGatePacketAsync(loginMsg, loginPayload);
            Log("[网络 1/3] 已发送账号密码验证请求...");

            // 等待阶段 1 完成 (超时 4 秒)
            using var cts1 = new CancellationTokenSource(4000);
            cts1.Token.Register(() => _stepTcs.TrySetResult(false));
            bool step1Ok = await _stepTcs.Task;
            _conn.Disconnect();

            if (!step1Ok)
            {
                Log("[网络 1/3] 登录网关验证超时或拒绝");
                Step = NetworkStep.Failed;
                return false;
            }

            // ==========================================
            // 阶段 2: 角色查询与选定 (7100 端口 SelChrGate)
            // ==========================================
            Step = NetworkStep.ConnectingSelChrGate;
            Log($"[网络 2/3] 正在连接角色网关 {SelChrHost}:{SelChrPort} ...");
            await _conn.ConnectAsync(SelChrHost, SelChrPort, timeoutMs: 3000);

            _stepTcs = new TaskCompletionSource<bool>();
            DefaultMessage queryMsg = new(ProtocolConsts.CM_QUERYCHR, 0, 0, 0, 0);
            string queryPayload = EDCode.EncodeString($"{account}/{Certification}");
            await _conn.SendGatePacketAsync(queryMsg, queryPayload);
            Log("[网络 2/3] 已发送角色查询请求...");

            using var cts2 = new CancellationTokenSource(4000);
            cts2.Token.Register(() => _stepTcs.TrySetResult(false));
            bool step2Ok = await _stepTcs.Task;
            _conn.Disconnect();

            if (!step2Ok)
            {
                Log("[网络 2/3] 角色网关查询/选定超时");
                Step = NetworkStep.Failed;
                return false;
            }

            // ==========================================
            // 阶段 3: 进入游戏世界 (7200 端口 GameGate)
            // ==========================================
            Step = NetworkStep.ConnectingGameGate;
            Log($"[网络 3/3] 正在连接游戏网关 {GameGateHost}:{GameGatePort} ...");
            await _conn.ConnectAsync(GameGateHost, GameGatePort, timeoutMs: 3000);

            // 发送游戏世界握手包 (**account/charName/cert/version/version)
            string handshake = $"**{Account}/{CharacterName}/{Certification}/10/10";
            await _conn.SendRawPacketAsync(EDCode.EncodeString(handshake));
            Log($"[网络 3/3] 已向游戏世界发送角色【{CharacterName}】登入握手！");

            Step = NetworkStep.InWorld;
            return true;
        }
        catch (Exception ex)
        {
            Log($"[网络异常] {ex.Message}");
            Step = NetworkStep.Failed;
            _conn.Disconnect();
            return false;
        }
    }

    private async void HandlePacket(object? sender, PacketReceivedEventArgs e)
    {
        try
        {
            switch (e.Header.Ident)
            {
                // 阶段 1: 密码验证成功，请求选服
                case ProtocolConsts.SM_PASSOK_SELECTSERVER:
                {
                    string decodedBody = EDCode.DecodeText(e.Payload);
                    string serverName = decodedBody.Split('/')[0];
                    Log($"[登录] 密码验证成功，选择服务器: {serverName}");

                    DefaultMessage selSvrMsg = new(ProtocolConsts.CM_SELECTSERVER, 0, 0, 0, 0);
                    await _conn.SendGatePacketAsync(selSvrMsg, EDCode.EncodeString(serverName));
                    break;
                }

                // 阶段 1 响应: 获得 SelChrGate 7100 端口与 Certification
                case ProtocolConsts.SM_QUERYCHR when Step == NetworkStep.ConnectingLoginGate:
                {
                    string decoded = EDCode.DecodeText(e.Payload);
                    var parts = decoded.Split('/');
                    if (parts.Length >= 3)
                    {
                        SelChrHost = parts[0];
                        SelChrPort = int.TryParse(parts[1], out int p) ? p : 7100;
                        Certification = int.TryParse(parts[2], out int c) ? c : 0;
                    }
                    Log($"[登录认证成功] 获得凭证 Token: {Certification}, 角色网关: {SelChrHost}:{SelChrPort}");
                    Step = NetworkStep.LoginGateSuccess;
                    _stepTcs?.TrySetResult(true);
                    break;
                }

                // 阶段 2: 收到角色列表
                case ProtocolConsts.SM_QUERYCHR when Step == NetworkStep.ConnectingSelChrGate:
                {
                    string decoded = EDCode.DecodeText(e.Payload);
                    var parts = decoded.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length >= 5)
                    {
                        CharacterName = parts[0].TrimStart('*');
                        CharacterJob = int.TryParse(parts[1], out int j) ? j : 0;
                        CharacterHair = int.TryParse(parts[2], out int h) ? h : 1;
                        CharacterGender = int.TryParse(parts[4], out int g) ? g : 0;
                        Log($"[角色查询] 找到已有角色: {CharacterName} (职业:{CharacterJob}, 性别:{CharacterGender})");

                        // 选定角色
                        DefaultMessage selMsg = new(ProtocolConsts.CM_SELCHR, 0, 0, 0, 0);
                        await _conn.SendGatePacketAsync(selMsg, EDCode.EncodeString($"{Account}/{CharacterName}"));
                    }
                    else
                    {
                        // 账号下无角色，自动创建战士
                        Log("[角色查询] 账号无角色，自动创建初始男战士...");
                        CharacterName = "逐鹿天下";
                        DefaultMessage newMsg = new(ProtocolConsts.CM_NEWCHR, 0, 0, 0, 0);
                        // uid/uname/hair/job/sex
                        string newPayload = EDCode.EncodeString($"{Account}/{CharacterName}/1/0/0");
                        await _conn.SendGatePacketAsync(newMsg, newPayload);
                    }
                    break;
                }

                // 阶段 2: 创建角色成功，立即选定
                case ProtocolConsts.SM_NEWCHR_SUCCESS:
                {
                    Log($"[角色创建成功] 正在选定角色【{CharacterName}】...");
                    DefaultMessage selMsg = new(ProtocolConsts.CM_SELCHR, 0, 0, 0, 0);
                    await _conn.SendGatePacketAsync(selMsg, EDCode.EncodeString($"{Account}/{CharacterName}"));
                    break;
                }

                // 阶段 2: 选角通过，获得 GameGate 7200 端口信息
                case ProtocolConsts.SM_STARTPLAY:
                {
                    string decoded = EDCode.DecodeText(e.Payload);
                    var parts = decoded.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        GameGateHost = parts[0];
                        GameGatePort = int.TryParse(parts[1], out int gp) ? gp : 7200;
                    }
                    Log($"[选角成功] 游戏世界网关: {GameGateHost}:{GameGatePort}");
                    Step = NetworkStep.CharacterReady;
                    _stepTcs?.TrySetResult(true);
                    break;
                }

                // NPC 对话与脚本下发
                case ProtocolConsts.SM_MERCHANTSAY:
                {
                    string decoded = EDCode.DecodeText(e.Payload);
                    NpcScriptDialogManager.Instance.ParseMerchantSay(e.Header.Recog, e.Header.Param, decoded);
                    OnMerchantSay?.Invoke(e.Header.Recog, e.Header.Param, decoded);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // NPC 对话框关闭
                case ProtocolConsts.SM_MERCHANTDLGCLOSE:
                {
                    NpcScriptDialogManager.Instance.CloseDialog();
                    OnMerchantClose?.Invoke();
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 地面物品生成广播
                case ProtocolConsts.SM_ITEMSHOW:
                {
                    var itemShow = GroundItemProtocolManager.ParseItemShow(e.Header, e.Payload);
                    GroundItemProtocolManager.Instance.SyncShowItem(itemShow);
                    OnGroundItemShow?.Invoke(itemShow);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 地面物品消失/拾取广播
                case ProtocolConsts.SM_ITEMHIDE:
                {
                    var itemHide = GroundItemProtocolManager.ParseItemHide(e.Header);
                    GroundItemProtocolManager.Instance.SyncHideItem(itemHide);
                    OnGroundItemHide?.Invoke(itemHide);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 丢弃物品结果反馈
                case ProtocolConsts.SM_DROPITEM_SUCCESS:
                {
                    Log("[物品丢弃] 成功丢弃物品到地面");
                    OnDropItemResult?.Invoke(true);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                case ProtocolConsts.SM_DROPITEM_FAIL:
                {
                    Log("[物品丢弃] 丢弃失败（周围无法放置或禁止丢弃）");
                    OnDropItemResult?.Invoke(false);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 阶段 3: 游戏世界广播数据
                default:
                {
                    if (Step == NetworkStep.InWorld)
                    {
                        OnGamePacket?.Invoke(e.Header, e.Payload);
                    }
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Log($"[包解析异常] {ex.Message}");
        }
    }

    /// <summary>
    /// 发送点击 NPC 开始对话数据包 (CM_CLICKNPC)
    /// </summary>
    public async Task SendClickNpcAsync(int npcRecogId)
    {
        var (msg, _) = NpcScriptDialogManager.CreateClickNpcPacket(npcRecogId);
        await SendGamePacketAsync(msg, "");
    }

    /// <summary>
    /// 发送选择 NPC 脚本选项数据包 (CM_MERCHANTDLGSELECT)
    /// </summary>
    public async Task SendNpcSelectAsync(int npcRecogId, string label)
    {
        var (msg, _) = NpcScriptDialogManager.CreateSelectPacket(npcRecogId, label);
        await SendGamePacketAsync(msg, EDCode.EncodeString(label));
    }

    /// <summary>
    /// 发送拾取当前脚下地面物品数据包 (CM_PICKUP)
    /// </summary>
    public async Task SendPickupAsync(int tileX, int tileY)
    {
        var (msg, payload, _) = GroundItemProtocolManager.CreatePickupPacket(tileX, tileY);
        await SendGamePacketAsync(msg, payload);
    }

    /// <summary>
    /// 发送丢弃包裹物品到脚下数据包 (CM_DROPITEM)
    /// </summary>
    public async Task SendDropItemAsync(int itemMakeIndex, int dropCount, string itemName)
    {
        var (msg, payload, _) = GroundItemProtocolManager.CreateDropItemPacket(itemMakeIndex, dropCount, itemName);
        await SendGamePacketAsync(msg, payload);
    }

    /// <summary>
    /// 在游戏世界中向 7200 网关发送行为数据包
    /// </summary>
    public async Task SendGamePacketAsync(DefaultMessage header, string payload = "")
    {
        if (_conn.IsConnected && Step == NetworkStep.InWorld)
        {
            await _conn.SendGatePacketAsync(header, payload);
        }
    }

    public void Disconnect()
    {
        _conn.Disconnect();
        Step = NetworkStep.Idle;
    }

    public void Dispose()
    {
        Disconnect();
        _conn.Dispose();
    }
}
