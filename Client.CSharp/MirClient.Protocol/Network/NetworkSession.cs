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
    public event Action<EquipActionResult>? OnTakeOnResult;
    public event Action<EquipActionResult>? OnTakeOffResult;
    public event Action<EatActionResult>? OnEatResult;
    public event Action<int>? OnDelBagItem;
    public event Action<SpellBroadcastEvent>? OnSpellBroadcast;
    public event Action<MagicFireEvent>? OnMagicFire;
    public event Action<ChatMessageEvent>? OnChatMessage;
    public event Action<IReadOnlyList<string>>? OnGroupMembersChanged;
    public event Action<bool>? OnGroupModeChanged;
    public event Action? OnGroupCancel;
    public event Action<GroupActionResult>? OnGroupActionResult;
    public event Action<string>? OnDealMenu;
    public event Action<int, string>? OnDealRemoteAddItem;
    public event Action<int>? OnDealRemoteDelItem;
    public event Action<int>? OnDealRemoteChgGold;
    public event Action<int>? OnDealLocalChgGold;
    public event Action? OnDealSuccess;
    public event Action? OnDealCancel;
    public event Action<string>? OnDealActionResult;
    public event Action<string, bool, string>? OnOpenGuildDlg;
    public event Action? OnOpenGuildDlgFail;
    public event Action<IReadOnlyList<GuildRankEntry>>? OnGuildMemberList;
    public event Action<GuildActionResult>? OnGuildActionResult;
    public event Action<int>? OnOpenStorage;
    public event Action<IReadOnlyList<StorageItemRecord>, int, int>? OnStorageItemList;
    public event Action<StorageActionResult>? OnStorageActionResult;

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

                // 装备穿戴反馈
                case ProtocolConsts.SM_TAKEON_OK:
                case ProtocolConsts.SM_TAKEON_FAIL:
                {
                    var result = InventoryProtocolManager.ParseTakeOnResponse(e.Header);
                    Log($"[装备穿戴] {result.Message}");
                    OnTakeOnResult?.Invoke(result);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 装备脱下反馈
                case ProtocolConsts.SM_TAKEOFF_OK:
                case ProtocolConsts.SM_TAKEOFF_FAIL:
                {
                    var result = InventoryProtocolManager.ParseTakeOffResponse(e.Header);
                    Log($"[装备脱下] {result.Message}");
                    OnTakeOffResult?.Invoke(result);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 物品使用反馈
                case ProtocolConsts.SM_EAT_OK:
                case ProtocolConsts.SM_EAT_FAIL:
                {
                    var result = InventoryProtocolManager.ParseEatResponse(e.Header);
                    Log($"[物品使用] {result.Message}");
                    OnEatResult?.Invoke(result);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 背包物品扣减或销毁
                case ProtocolConsts.SM_DELITEM:
                {
                    int delMakeIndex = InventoryProtocolManager.ParseDelBagItem(e.Header);
                    OnDelBagItem?.Invoke(delMakeIndex);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 实体施放技能法术广播
                case ProtocolConsts.SM_SPELL:
                {
                    var spellEvt = MagicProtocolManager.ParseSpellBroadcast(e.Header, e.Payload);
                    OnSpellBroadcast?.Invoke(spellEvt);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 法术命中与伤害结算广播
                case ProtocolConsts.SM_MAGICFIRE:
                {
                    var fireEvt = MagicProtocolManager.ParseMagicFire(e.Header);
                    OnMagicFire?.Invoke(fireEvt);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 玩家发言与普通视野广播
                case ProtocolConsts.SM_HEAR:
                case ProtocolConsts.SM_HEAR_LEGACY:
                {
                    var chatEvt = ChatProtocolManager.ParseHearMessage(e.Header, e.Payload);
                    OnChatMessage?.Invoke(chatEvt);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 系统广播与公告提示
                case ProtocolConsts.SM_SYSMESSAGE:
                {
                    var chatEvt = ChatProtocolManager.ParseSystemMessage(e.Header, e.Payload);
                    OnChatMessage?.Invoke(chatEvt);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 组队频道广播
                case ProtocolConsts.SM_GROUPMESSAGE:
                {
                    var chatEvt = ChatProtocolManager.ParseGroupMessage(e.Header, e.Payload);
                    OnChatMessage?.Invoke(chatEvt);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 行会频道广播
                case ProtocolConsts.SM_GUILDMESSAGE:
                {
                    var chatEvt = ChatProtocolManager.ParseGuildMessage(e.Header, e.Payload);
                    OnChatMessage?.Invoke(chatEvt);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 私聊密语
                case ProtocolConsts.SM_WHISPER:
                {
                    var chatEvt = ChatProtocolManager.ParseWhisperMessage(e.Header, e.Payload);
                    OnChatMessage?.Invoke(chatEvt);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 喊话频道广播
                case ProtocolConsts.SM_CRY:
                {
                    var chatEvt = new ChatMessageEvent(
                        e.Header.Recog,
                        string.Empty,
                        EDCode.DecodeText(e.Payload ?? string.Empty),
                        ChatChannel.Shout,
                        e.Header.Param);
                    OnChatMessage?.Invoke(chatEvt);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 组队成员名单下发广播 (SM_GROUPMEMBERS = 667)
                case ProtocolConsts.SM_GROUPMEMBERS:
                {
                    var members = GroupProtocolManager.ParseGroupMembers(e.Header, e.Payload);
                    OnGroupMembersChanged?.Invoke(members);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 组队模式切换通知 (SM_GROUPMODECHANGED = 659)
                case ProtocolConsts.SM_GROUPMODECHANGED:
                {
                    bool allowed = GroupProtocolManager.ParseGroupModeChanged(e.Header);
                    OnGroupModeChanged?.Invoke(allowed);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 队伍解散广播 (SM_GROUPCANCEL = 666)
                case ProtocolConsts.SM_GROUPCANCEL:
                {
                    Log("[组队] 队伍已解散");
                    OnGroupCancel?.Invoke();
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 组队操作反馈 (创建/添加/删除成功与失败)
                case ProtocolConsts.SM_CREATEGROUP_OK:
                case ProtocolConsts.SM_CREATEGROUP_FAIL:
                case ProtocolConsts.SM_GROUPADDMEM_OK:
                case ProtocolConsts.SM_GROUPADDMEM_FAIL:
                case ProtocolConsts.SM_GROUPDELMEM_OK:
                case ProtocolConsts.SM_GROUPDELMEM_FAIL:
                {
                    var actionResult = GroupProtocolManager.ParseGroupActionResult(e.Header, e.Payload, e.Header.Ident);
                    Log($"[组队反馈] {actionResult.Message}");
                    OnGroupActionResult?.Invoke(actionResult);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 打开双人交易双栏视窗 (SM_DEALMENU = 673)
                case ProtocolConsts.SM_DEALMENU:
                {
                    string targetName = DealProtocolManager.ParseDealMenu(e.Header, e.Payload);
                    DealManager.Instance.StartDeal(targetName);
                    Log($"[交易] 与【{targetName}】的面对面安全交易窗口已开启");
                    OnDealMenu?.Invoke(targetName);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 发起交易失败 (SM_DEALTRY_FAIL = 674)
                case ProtocolConsts.SM_DEALTRY_FAIL:
                {
                    Log("[交易] 交易发起失败 (目标距离过远、正忙或未开启交易模式)");
                    OnDealActionResult?.Invoke("交易发起失败 (目标距离过远、正忙或未开启交易模式)");
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 本地放入物品成功/失败
                case ProtocolConsts.SM_DEALADDITEM_OK:
                {
                    Log("[交易] 本地放入交易物品成功");
                    OnDealActionResult?.Invoke("交易物品放入成功！");
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }
                case ProtocolConsts.SM_DEALADDITEM_FAIL:
                {
                    Log("[交易] 本地放入交易物品失败");
                    OnDealActionResult?.Invoke("放入交易物品失败 (可能已锁定或背包数据异常)");
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 本地撤回物品成功/失败
                case ProtocolConsts.SM_DEALDELITEM_OK:
                {
                    Log("[交易] 撤回交易物品成功");
                    OnDealActionResult?.Invoke("交易物品已撤回。");
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }
                case ProtocolConsts.SM_DEALDELITEM_FAIL:
                {
                    Log("[交易] 撤回交易物品失败");
                    OnDealActionResult?.Invoke("撤回交易物品失败！");
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 对方放入/撤回物品同步
                case ProtocolConsts.SM_DEALREMOTEADDITEM:
                {
                    int makeIndex = e.Header.Param | (e.Header.Tag << 16);
                    string itemName = EDCode.DecodeText(e.Payload ?? string.Empty);
                    DealManager.Instance.AddRemoteItem(makeIndex, itemName);
                    Log($"[交易] 对方放入了物品: 【{itemName}】");
                    OnDealRemoteAddItem?.Invoke(makeIndex, itemName);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }
                case ProtocolConsts.SM_DEALREMOTEDELITEM:
                {
                    int makeIndex = e.Header.Param | (e.Header.Tag << 16);
                    DealManager.Instance.RemoveRemoteItem(makeIndex);
                    Log($"[交易] 对方撤回了交易物品 (Index: {makeIndex})");
                    OnDealRemoteDelItem?.Invoke(makeIndex);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 金币调整同步
                case ProtocolConsts.SM_DEALCHGGOLD_OK:
                {
                    int localGold = DealProtocolManager.ParseDealGold(e.Header);
                    DealManager.Instance.SetLocalGold(localGold);
                    Log($"[交易] 本地交易金币调整为: {localGold:N0}");
                    OnDealLocalChgGold?.Invoke(localGold);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }
                case ProtocolConsts.SM_DEALCHGGOLD_FAIL:
                {
                    Log("[交易] 调整交易金币失败");
                    OnDealActionResult?.Invoke("调整交易金币失败 (金币不足或已锁定)");
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }
                case ProtocolConsts.SM_DEALREMOTECHGGOLD:
                {
                    int remoteGold = DealProtocolManager.ParseDealGold(e.Header);
                    DealManager.Instance.SetRemoteGold(remoteGold);
                    Log($"[交易] 对方投入了交易金币: {remoteGold:N0}");
                    OnDealRemoteChgGold?.Invoke(remoteGold);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 交易圆满成交 (SM_DEALSUCCESS = 687)
                case ProtocolConsts.SM_DEALSUCCESS:
                {
                    DealManager.Instance.CompleteDeal();
                    Log("[交易] 双方确认锁定，交易圆满达成！");
                    OnDealSuccess?.Invoke();
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 交易取消中止 (SM_DEALCANCEL = 681)
                case ProtocolConsts.SM_DEALCANCEL:
                {
                    DealManager.Instance.CancelDeal();
                    Log("[交易] 交易已取消中止，物品与金币已安全退回");
                    OnDealCancel?.Invoke();
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 打开仓库保管存入界面 (SM_SENDUSERSTORAGEITEM = 700)
                case ProtocolConsts.SM_SENDUSERSTORAGEITEM:
                {
                    int merchantId = e.Header.Recog;
                    StorageManager.Instance.IsOpen = true;
                    StorageManager.Instance.CurrentMerchantId = merchantId;
                    Log($"[仓库] 仓库保管员已开启保管通道 (NPC ID: {merchantId})");
                    OnOpenStorage?.Invoke(merchantId);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 下发仓库物品名册 (SM_SAVEITEMLIST = 704)
                case ProtocolConsts.SM_SAVEITEMLIST:
                {
                    int merchantId = e.Header.Recog;
                    int curPage = e.Header.Param;
                    int totalPages = e.Header.Tag + 1;
                    var records = StorageProtocolManager.ParseStorageItemList(e.Payload);
                    StorageManager.Instance.IsOpen = true;
                    StorageManager.Instance.CurrentMerchantId = merchantId;
                    StorageManager.Instance.PageIndex = curPage;
                    StorageManager.Instance.TotalPages = totalPages;
                    StorageManager.Instance.SyncStorageRecords(records);
                    Log($"[仓库] 已同步仓库物品列表: 共 {records.Count} 件物品 (第 {curPage + 1}/{totalPages} 页)");
                    OnStorageItemList?.Invoke(records, curPage, totalPages);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 仓库物品存入/取回结果 (701, 702, 703, 705, 706, 707)
                case ProtocolConsts.SM_STORAGE_OK:
                case ProtocolConsts.SM_STORAGE_FULL:
                case ProtocolConsts.SM_STORAGE_FAIL:
                case ProtocolConsts.SM_TAKEBACKSTORAGEITEM_OK:
                case ProtocolConsts.SM_TAKEBACKSTORAGEITEM_FAIL:
                case ProtocolConsts.SM_TAKEBACKSTORAGEITEM_FULLBAG:
                {
                    var result = StorageProtocolManager.ParseActionResult(e.Header);
                    Log($"[仓库] {result.Message}");
                    OnStorageActionResult?.Invoke(result);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 打开行会主视窗成功 (SM_OPENGUILDDLG = 753)
                case ProtocolConsts.SM_OPENGUILDDLG:
                {
                    var (guildName, isMaster, notice) = GuildProtocolManager.ParseGuildDlg(e.Header, e.Payload);
                    GuildManager.Instance.SyncGuildInfo(guildName, notice);
                    Log($"[行会] 已进入【{guildName}】行会面板 (掌门人权限: {isMaster})");
                    OnOpenGuildDlg?.Invoke(guildName, isMaster, notice);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 打开行会失败 (SM_OPENGUILDDLG_FAIL = 754)
                case ProtocolConsts.SM_OPENGUILDDLG_FAIL:
                {
                    Log("[行会] 您尚未加入任何行会或行会已解散");
                    OnOpenGuildDlgFail?.Invoke();
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 下发行会全员职务与成员名册 (SM_SENDGUILDMEMBERLIST = 756)
                case ProtocolConsts.SM_SENDGUILDMEMBERLIST:
                {
                    var ranks = GuildProtocolManager.ParseGuildMembers(e.Header, e.Payload);
                    GuildManager.Instance.SyncServerRanks(ranks);
                    Log($"[行会] 行会名册已同步，共 {ranks.Count} 级职务");
                    OnGuildMemberList?.Invoke(ranks);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 招募与退会操作反馈
                case ProtocolConsts.SM_GUILDADDMEMBER_OK:
                case ProtocolConsts.SM_GUILDADDMEMBER_FAIL:
                case ProtocolConsts.SM_GUILDDELMEMBER_OK:
                case ProtocolConsts.SM_GUILDDELMEMBER_FAIL:
                {
                    var actionResult = GuildProtocolManager.ParseGuildActionResult(e.Header, e.Payload, e.Header.Ident);
                    Log($"[行会反馈] {actionResult.Message}");
                    OnGuildActionResult?.Invoke(actionResult);
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
    /// 发送使用/消耗物品数据包 (CM_EAT)
    /// </summary>
    public async Task SendEatItemAsync(int itemMakeIndex, string itemName)
    {
        var (msg, payload, _) = InventoryProtocolManager.CreateEatPacket(itemMakeIndex, itemName);
        await SendGamePacketAsync(msg, payload);
    }

    /// <summary>
    /// 发送穿戴装备数据包 (CM_TAKEONITEM)
    /// </summary>
    public async Task SendTakeOnItemAsync(int itemMakeIndex, EquipSlotType slot, string itemName)
    {
        var (msg, payload, _) = InventoryProtocolManager.CreateTakeOnPacket(itemMakeIndex, slot, itemName);
        await SendGamePacketAsync(msg, payload);
    }

    /// <summary>
    /// 发送脱下装备数据包 (CM_TAKEOFFITEM)
    /// </summary>
    public async Task SendTakeOffItemAsync(int itemMakeIndex, EquipSlotType slot, string itemName)
    {
        var (msg, payload, _) = InventoryProtocolManager.CreateTakeOffPacket(itemMakeIndex, slot, itemName);
        await SendGamePacketAsync(msg, payload);
    }

    /// <summary>
    /// 发送施放技能法术数据包 (CM_SPELL)
    /// </summary>
    public async Task SendSpellAsync(int magicId, int targetX, int targetY, int targetRecogId = 0)
    {
        var (msg, payload, _) = MagicProtocolManager.CreateSpellPacket(magicId, targetX, targetY, targetRecogId);
        await SendGamePacketAsync(msg, payload);
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
    /// 发送聊天发言数据包 (CM_SAY = 3030)
    /// </summary>
    public async Task SendSayAsync(string text, ChatChannel channel = ChatChannel.Normal, string target = "")
    {
        var (header, payload) = ChatProtocolManager.CreateSayPacket(text, channel, target);
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 开关允许组队模式 (CM_GROUPMODE = 1019)
    /// </summary>
    public async Task SendGroupModeAsync(bool allow)
    {
        var (header, payload) = GroupProtocolManager.CreateGroupModePacket(allow);
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 创建队伍并邀请首位成员 (CM_CREATEGROUP = 1020)
    /// </summary>
    public async Task SendCreateGroupAsync(string targetName)
    {
        var (header, payload) = GroupProtocolManager.CreateCreateGroupPacket(targetName);
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 邀请添加队伍成员 (CM_ADDGROUPMEMBER = 1021)
    /// </summary>
    public async Task SendAddGroupMemberAsync(string targetName)
    {
        var (header, payload) = GroupProtocolManager.CreateAddMemberPacket(targetName);
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 移出队员或主动退出队伍 (CM_DELGROUPMEMBER = 1022)
    /// </summary>
    public async Task SendDelGroupMemberAsync(string targetName)
    {
        var (header, payload) = GroupProtocolManager.CreateDelMemberPacket(targetName);
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 发起面对面交易请求 (CM_DEALTRY = 1025)
    /// </summary>
    public async Task SendDealTryAsync(string targetName)
    {
        var (header, payload) = DealProtocolManager.CreateDealTryPacket(targetName);
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 放入交易物品 (CM_DEALADDITEM = 1026)
    /// </summary>
    public async Task SendDealAddItemAsync(int makeIndex, string itemName)
    {
        var (header, payload) = DealProtocolManager.CreateDealAddItemPacket(makeIndex, itemName);
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 撤回交易物品 (CM_DEALDELITEM = 1027)
    /// </summary>
    public async Task SendDealDelItemAsync(int makeIndex, string itemName)
    {
        var (header, payload) = DealProtocolManager.CreateDealDelItemPacket(makeIndex, itemName);
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 调整交易金币数 (CM_DEALCHGGOLD = 1029)
    /// </summary>
    public async Task SendDealChgGoldAsync(int gold)
    {
        var (header, payload) = DealProtocolManager.CreateDealChgGoldPacket(gold);
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 锁定交易并确认成交 (CM_DEALEND = 1030)
    /// </summary>
    public async Task SendDealEndAsync()
    {
        var (header, payload) = DealProtocolManager.CreateDealEndPacket();
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 取消交易中止窗口 (CM_DEALCANCEL = 1028)
    /// </summary>
    public async Task SendDealCancelAsync()
    {
        var (header, payload) = DealProtocolManager.CreateDealCancelPacket();
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 请求打开行会主面板 (CM_OPENGUILDDLG = 1035)
    /// </summary>
    public async Task SendOpenGuildDlgAsync()
    {
        var (header, payload) = GuildProtocolManager.CreateOpenGuildDlgPacket();
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 查询行会主页信息 (CM_GUILDHOME = 1036)
    /// </summary>
    public async Task SendGuildHomeAsync()
    {
        var (header, payload) = GuildProtocolManager.CreateGuildHomePacket();
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 请求行会成员名册与职务列表 (CM_GUILDMEMBERLIST = 1037)
    /// </summary>
    public async Task SendGuildMemberListAsync()
    {
        var (header, payload) = GuildProtocolManager.CreateGuildMemberListPacket();
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 招募新成员入会 (CM_GUILDADDMEMBER = 1038)
    /// </summary>
    public async Task SendGuildAddMemberAsync(string targetName)
    {
        var (header, payload) = GuildProtocolManager.CreateGuildAddMemberPacket(targetName);
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 逐出成员或主动退出行会 (CM_GUILDDELMEMBER = 1039)
    /// </summary>
    public async Task SendGuildDelMemberAsync(string targetName)
    {
        var (header, payload) = GuildProtocolManager.CreateGuildDelMemberPacket(targetName);
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 会长修改行会公告 (CM_GUILDUPDATENOTICE = 1040)
    /// </summary>
    public async Task SendGuildUpdateNoticeAsync(string notice)
    {
        var (header, payload) = GuildProtocolManager.CreateGuildUpdateNoticePacket(notice);
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 请求存入物品至仓库 (CM_USERSTORAGEITEM = 1031)
    /// </summary>
    public async Task SendSaveStorageItemAsync(int merchantId, int makeIndex, string itemName)
    {
        var (header, payload) = StorageProtocolManager.CreateSaveStorageItemPacket(merchantId, makeIndex, itemName);
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 请求从仓库取回物品 (CM_USERTAKEBACKSTORAGEITEM = 1032)
    /// </summary>
    public async Task SendTakeBackStorageItemAsync(int merchantId, int makeIndex, string itemName)
    {
        var (header, payload) = StorageProtocolManager.CreateTakeBackStorageItemPacket(merchantId, makeIndex, itemName);
        await SendGamePacketAsync(header, payload);
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
