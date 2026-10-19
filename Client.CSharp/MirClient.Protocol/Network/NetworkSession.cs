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
    public event Action<WarStatusRecord>? OnWarStatusChanged;
    public event Action<CastleWallStatusRecord>? OnWallStatusChanged;
    public event Action<CastleOwnerRecord>? OnCastleOwnerChanged;
    public event Action<int>? OnCastleGold;
    public event Action<IReadOnlyList<FriendInfo>>? OnFriendListReceived;
    public event Action<FriendInfo>? OnFriendAdded;
    public event Action<bool, string>? OnFriendAddFailed;
    public event Action<string>? OnFriendRemoved;
    public event Action<string, bool, string>? OnFriendOnlineChanged;
    public event Action<bool, int, string>? OnMasterResult;
    public event Action<MasterSyncRecord>? OnMasterListReceived;
    public event Action<string, int, int>? OnApprenticeLeveledUp;
    public event Action<string>? OnStallStarted;
    public event Action<string>? OnStallStopped;
    public event Action<int, int, int, string>? OnStallItemAdded;
    public event Action<int, string>? OnStallBuyerSuccess;
    public event Action<int, string, string>? OnStallSellerSold;
    public event Action<StallSignboardRecord>? OnStallSignboardChanged;
    public event Action<IReadOnlyList<RemoteStallShelfItem>>? OnOtherStallShelvesReceived;

    // =========================================================================
    // OpenMir2 庄园全服元宝寄售行 (DealOff) 事件通知
    // =========================================================================
    public event Action<int>? OnDealOffFormOpened;
    public event Action? OnDealOffAddItemSuccess;
    public event Action? OnDealOffAddItemFail;
    public event Action? OnDealOffDelItemSuccess;
    public event Action? OnDealOffDelItemFail;
    public event Action? OnDealOffCancelSuccess;
    public event Action? OnDealOffEndSuccess;
    public event Action? OnDealOffEndFail;
    public event Action<ClientDealOffInfo>? OnDealOffQuerySellReceived;
    public event Action<ClientDealOffInfo>? OnDealOffQueryDealReceived;
    public event Action? OnDealOffBuySuccess;

    // =========================================================================
    // OpenMir2 NPC 交互经济与商人交易辅助事件通知
    // =========================================================================
    public event Action<int>? OnMerchantBuyPriceReceived;
    public event Action<int>? OnMerchantRepairCostReceived;
    public event Action<string>? OnMerchantDetailGoodsReceived;
    public event Action? OnItemCheckStateReceived;

    // =========================================================================
    // OpenMir2 行会外交与二级密码安全锁事件通知
    // =========================================================================
    public event Action? OnGuildAllySuccess;
    public event Action<int, string>? OnGuildAllyFailed;
    public event Action? OnGuildBreakAllySuccess;
    public event Action? OnGuildBreakAllyFailed;
    public event Action<bool>? OnPasswordStatusReceived;
    public event Action? OnNeedPasswordPrompt;



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

                // 沙巴克攻城战阶段广播 (SM_WARSTATUS = 765)
                case ProtocolConsts.SM_WARSTATUS:
                {
                    var record = SabukSiegeProtocolManager.ParseWarStatus(e.Header);
                    SabukSiegeManager.Instance.SyncServerWarStatus(record.Phase, record.RemainingSeconds);
                    Log($"[沙巴克] 战事阶段更新: {SabukSiegeProtocolManager.GetSiegePhaseName(record.Phase)} (剩余: {record.RemainingSeconds:F0} 秒)");
                    OnWarStatusChanged?.Invoke(record);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 沙巴克城门工事状态广播 (SM_SABUK_WALLSTATUS = 763)
                case ProtocolConsts.SM_SABUK_WALLSTATUS:
                {
                    var record = SabukSiegeProtocolManager.ParseWallStatus(e.Header);
                    SabukSiegeManager.Instance.SyncServerWallStatus(record.DamageLevel, record.HpPercent, record.IsOpen);
                    Log($"[沙城工事] 城门耐久: {record.HpPercent}%, 破损等级: {record.DamageLevel}, 状态: {(record.IsOpen ? "开启" : "紧闭")}");
                    OnWallStatusChanged?.Invoke(record);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 沙巴克城主归属全服广播 (SM_CASTLE_OWNER = 764)
                case ProtocolConsts.SM_CASTLE_OWNER:
                {
                    var record = SabukSiegeProtocolManager.ParseCastleOwner(e.Payload);
                    SabukSiegeManager.Instance.SyncServerCastleOwner(record.GuildName, record.MasterName);
                    Log($"[沙城霸主] 当前占领行会: 【{record.GuildName}】(掌门人: {record.MasterName}, 占领日期: {record.OccupyDate})");
                    OnCastleOwnerChanged?.Invoke(record);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 沙巴克城池金币税收广播 (SM_CASTLE_GOLD = 762)
                case ProtocolConsts.SM_CASTLE_GOLD:
                {
                    int gold = e.Header.Recog;
                    SabukSiegeManager.Instance.SyncServerCastleGold(gold);
                    Log($"[沙城金库] 城池累积金币税收: {gold:N0} 金币");
                    OnCastleGold?.Invoke(gold);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 下发好友全员列表 (SM_FRIENDLIST = 773)
                case ProtocolConsts.SM_FRIENDLIST:
                {
                    var friends = SocialProtocolManager.ParseFriendList(e.Payload);
                    SocialManager.Instance.SyncServerFriendList(friends);
                    Log($"[好友系统] 已同步全量好友列表，共 {friends.Count} 位江湖挚友");
                    OnFriendListReceived?.Invoke(friends);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 添加好友成功 (SM_ADDFRIEND_OK = 774)
                case ProtocolConsts.SM_ADDFRIEND_OK:
                {
                    var friend = SocialProtocolManager.ParseFriendRecord(e.Payload);
                    if (friend != null)
                    {
                        SocialManager.Instance.AddFriend(friend.Name, friend.Job, friend.Level, friend.MapName, friend.IsOnline);
                        Log($"[好友系统] 成功添加好友【{friend.Name}】(职业: {friend.JobName}, 等级: {friend.Level})");
                        OnFriendAdded?.Invoke(friend);
                    }
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 添加好友失败 (SM_ADDFRIEND_FAIL = 775)
                case ProtocolConsts.SM_ADDFRIEND_FAIL:
                {
                    string reason = !string.IsNullOrWhiteSpace(e.Payload) ? e.Payload.Trim() : "添加好友失败";
                    Log($"[好友系统] 添加好友失败: {reason}");
                    OnFriendAddFailed?.Invoke(false, reason);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 删除好友成功 (SM_DELFRIEND_OK = 776)
                case ProtocolConsts.SM_DELFRIEND_OK:
                {
                    string friendName = e.Payload?.Trim() ?? string.Empty;
                    SocialManager.Instance.RemoveFriend(friendName);
                    Log($"[好友系统] 已将【{friendName}】从好友名册中删除");
                    OnFriendRemoved?.Invoke(friendName);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 好友上下线实时广播 (SM_FRIEND_ONOFF = 777)
                case ProtocolConsts.SM_FRIEND_ONOFF:
                {
                    var (fName, isOnline, fMap) = SocialProtocolManager.ParseFriendOnOff(e.Header, e.Payload);
                    SocialManager.Instance.SyncServerFriendOnOff(fName, isOnline, fMap);
                    string statusStr = isOnline ? $"上线了 (身处 {fMap})" : "离线了";
                    Log($"[好友状态] 您的江湖好友【{fName}】{statusStr}");
                    OnFriendOnlineChanged?.Invoke(fName, isOnline, fMap);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 师徒结拜/脱离结果广播 (SM_MASTER_RESULT = 778)
                case ProtocolConsts.SM_MASTER_RESULT:
                {
                    var (success, code, message) = SocialProtocolManager.ParseMasterResult(e.Header, e.Payload);
                    Log($"[师徒武馆] {message}");
                    OnMasterResult?.Invoke(success, code, message);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 师门与门徒名册同步 (SM_MASTER_LIST = 779)
                case ProtocolConsts.SM_MASTER_LIST:
                {
                    var record = SocialProtocolManager.ParseMasterList(e.Header, e.Payload);
                    SocialManager.Instance.SyncServerMasterList(record.MasterName, record.IsGraduated, record.Reputation, record.Apprentices);
                    Log($"[师徒武馆] 师门同步: 师父【{record.MasterName ?? "无"}】, 弟子数: {record.Apprentices.Count}, 声望: {record.Reputation}");
                    OnMasterListReceived?.Invoke(record);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 门下弟子升级广播 (SM_APPRENTICE_LEVELUP = 780)
                case ProtocolConsts.SM_APPRENTICE_LEVELUP:
                {
                    var (appName, newLvl, repGain) = SocialProtocolManager.ParseApprenticeLevelUp(e.Header, e.Payload);
                    SocialManager.Instance.SyncServerApprenticeLevelUp(appName, newLvl, repGain);
                    Log($"[师门捷报] 门下弟子【{appName}】勤学苦练荣升至 {newLvl} 级！师父荣获声望奖励 {repGain} 点");
                    OnApprenticeLeveledUp?.Invoke(appName, newLvl, repGain);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 开启个人摊位成功确认 (SM_MYSHOPHAM = 9056)
                case ProtocolConsts.SM_MYSHOPHAM:
                {
                    string title = StallProtocolManager.ParseStartStallResponse(e.Header, e.Payload);
                    StallManager.Instance.SyncServerStartStall(title);
                    Log($"[摆摊系统] 您的地摊【{title}】已顺利在集市支起营业！");
                    OnStallStarted?.Invoke(title);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 收摊歇业成功确认 (SM_MYSHOPEXIT = 9054)
                case ProtocolConsts.SM_MYSHOPEXIT:
                {
                    string msg = StallProtocolManager.ParseStopStallResponse(e.Header, e.Payload);
                    StallManager.Instance.SyncServerStopStall(msg);
                    Log($"[摆摊系统] {msg}");
                    OnStallStopped?.Invoke(msg);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 商品上架成功通知 (SM_SENDSELL = 9051)
                case ProtocolConsts.SM_SENDSELL:
                {
                    var (slot, makeIndex, price, itemName) = StallProtocolManager.ParseAddItemResponse(e.Header, e.Payload);
                    StallManager.Instance.SyncServerAddItem(slot, makeIndex, price, itemName);
                    Log($"[摆摊系统] 商品【{itemName}】已上架至第 {slot + 1} 号货架，标价 {price} 金币");
                    OnStallItemAdded?.Invoke(slot, makeIndex, price, itemName);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 买家购买成功确认 (SM_HAMSHOPBUYA = 9058)
                case ProtocolConsts.SM_HAMSHOPBUYA:
                {
                    var (cost, name) = StallProtocolManager.ParseBuyerSuccess(e.Header, e.Payload);
                    Log($"[市集交易] 选购成功！您花费 {cost} 金币购得【{name}】");
                    OnStallBuyerSuccess?.Invoke(cost, name);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 卖家商品售出结算通知 (SM_HAMSHOPBUYB = 9059)
                case ProtocolConsts.SM_HAMSHOPBUYB:
                {
                    var (earn, name, buyer) = StallProtocolManager.ParseSellerSold(e.Header, e.Payload);
                    StallManager.Instance.SyncServerSoldNotification(name, earn, buyer);
                    Log($"[摊位捷报] 买家【{buyer}】购买了您的【{name}】，金币结算 +{earn} 入账！");
                    OnStallSellerSold?.Invoke(earn, name, buyer);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 地表摊位招牌广播 (SM_HAMSHOPTYPE = 9060)
                case ProtocolConsts.SM_HAMSHOPTYPE:
                {
                    var record = StallProtocolManager.ParseStallType(e.Header, e.Payload);
                    OnStallSignboardChanged?.Invoke(record);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 下发他人摊位货架列表 (SM_OPHAMSHOP = 9062)
                case ProtocolConsts.SM_OPHAMSHOP:
                {
                    var list = StallProtocolManager.ParseOtherStallShelves(e.Payload);
                    StallManager.Instance.SyncServerOtherStall(e.Header.Recog, "", list);
                    Log($"[市集查阅] 已同步摊位货架，在售商品共 {list.Count} 件");
                    OnOtherStallShelvesReceived?.Invoke(list);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // =============================================================
                // OpenMir2 庄园全服元宝寄售行系统 (DealOff) (23001 ~ 23027)
                // =============================================================
                case ProtocolConsts.SM_SENDDEALOFFFORM:
                {
                    int npcId = e.Header.Param;
                    Log($"[元宝寄售] 打开寄售出售窗口，NPC: {npcId}");
                    OnDealOffFormOpened?.Invoke(npcId);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                case ProtocolConsts.SM_SELLOFFADDITEM_OK:
                {
                    Log("[元宝寄售] 物品放入寄售窗口成功");
                    OnDealOffAddItemSuccess?.Invoke();
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                case ProtocolConsts.SM_SELLOFFADDITEM_FAIL:
                {
                    Log("[元宝寄售] 物品放入寄售窗口失败");
                    OnDealOffAddItemFail?.Invoke();
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                case ProtocolConsts.SM_SELLOFFDELITEM_OK:
                {
                    Log("[元宝寄售] 寄售物品撤回成功");
                    OnDealOffDelItemSuccess?.Invoke();
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                case ProtocolConsts.SM_SELLOFFDELITEM_FAIL:
                {
                    Log("[元宝寄售] 寄售物品撤回失败");
                    OnDealOffDelItemFail?.Invoke();
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                case ProtocolConsts.SM_SELLOFFCANCEL:
                {
                    Log("[元宝寄售] 取消寄售成功，物品已返还");
                    OnDealOffCancelSuccess?.Invoke();
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                case ProtocolConsts.SM_SELLOFFEND_OK:
                {
                    Log("[元宝寄售] 寄售挂牌成功");
                    OnDealOffEndSuccess?.Invoke();
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                case ProtocolConsts.SM_SELLOFFEND_FAIL:
                {
                    Log("[元宝寄售] 寄售挂牌失败");
                    OnDealOffEndFail?.Invoke();
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                case ProtocolConsts.SM_QUERYYBSELL:
                {
                    var dealOffInfo = DealOffProtocolManager.DecodeDealOffInfo(e.Payload);
                    if (dealOffInfo != null)
                    {
                        OnDealOffQuerySellReceived?.Invoke(dealOffInfo);
                    }
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                case ProtocolConsts.SM_QUERYYBDEAL:
                {
                    var dealOffInfo = DealOffProtocolManager.DecodeDealOffInfo(e.Payload);
                    if (dealOffInfo != null)
                    {
                        OnDealOffQueryDealReceived?.Invoke(dealOffInfo);
                    }
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                case ProtocolConsts.SM_SELLOFFBUY_OK:
                {
                    Log("[元宝寄售] 购买寄售物品成功");
                    OnDealOffBuySuccess?.Invoke();
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 商人收购估价下发 (SM_SENDBUYPRICE = 647)
                case ProtocolConsts.SM_SENDBUYPRICE:
                {
                    int price = MerchantEconomyProtocolManager.ParseBuyPriceResponse(e.Header);
                    Log($"[商人收购估价] 该物品商人出价: {price} 金币");
                    OnMerchantBuyPriceReceived?.Invoke(price);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 铁匠铺装备修理费用下发 (SM_SENDREPAIRCOST = 671)
                case ProtocolConsts.SM_SENDREPAIRCOST:
                {
                    int cost = MerchantEconomyProtocolManager.ParseRepairCostResponse(e.Header);
                    Log($"[装备修理估价] 修理费用: {cost} 金币");
                    OnMerchantRepairCostReceived?.Invoke(cost);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 货架商品极品明细下发 (SM_SENDDETAILGOODSLIST = 652)
                case ProtocolConsts.SM_SENDDETAILGOODSLIST:
                {
                    OnMerchantDetailGoodsReceived?.Invoke(e.Payload);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 装备持久与有效性核验返回 (SM_CHACKITEM = 9064)
                case ProtocolConsts.SM_CHACKITEM:
                {
                    OnItemCheckStateReceived?.Invoke();
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 行会结盟成功 (SM_GUILDMAKEALLY_OK = 768)
                case ProtocolConsts.SM_GUILDMAKEALLY_OK:
                {
                    Log("[行会外交] 双方行会联盟缔结成功！");
                    OnGuildAllySuccess?.Invoke();
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 行会结盟失败 (SM_GUILDMAKEALLY_FAIL = 769)
                case ProtocolConsts.SM_GUILDMAKEALLY_FAIL:
                {
                    var (code, desc) = GuildDiplomacyAndSecurityProtocolManager.ParseGuildAllyFail(e.Header);
                    Log($"[行会外交] 结盟失败: {desc} (代码: {code})");
                    OnGuildAllyFailed?.Invoke(code, desc);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 解除同盟成功 (SM_GUILDBREAKALLY_OK = 770)
                case ProtocolConsts.SM_GUILDBREAKALLY_OK:
                {
                    Log("[行会外交] 已成功解除行会同盟关系！");
                    OnGuildBreakAllySuccess?.Invoke();
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 解除同盟失败 (SM_GUILDBREAKALLY_FAIL = 771)
                case ProtocolConsts.SM_GUILDBREAKALLY_FAIL:
                {
                    Log("[行会外交] 解除行会同盟失败！");
                    OnGuildBreakAllyFailed?.Invoke();
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 二级密码安全锁状态 (SM_PASSWORDSTATUS = 8002)
                case ProtocolConsts.SM_PASSWORDSTATUS:
                {
                    bool isLocked = GuildDiplomacyAndSecurityProtocolManager.ParsePasswordStatus(e.Header);
                    Log($"[二级密码] 当前仓库安全锁状态: {(isLocked ? "已上锁" : "已开锁")}");
                    OnPasswordStatusReceived?.Invoke(isLocked);
                    OnGamePacket?.Invoke(e.Header, e.Payload);
                    break;
                }

                // 提示输入二级密码 (SM_NEEDPASSWORD = 8003)
                case ProtocolConsts.SM_NEEDPASSWORD:
                {
                    Log("[二级密码] 仓库已加锁，需要输入二级安全密码解锁");
                    OnNeedPasswordPrompt?.Invoke();
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
    /// 申请攻城战或行会宣战 (CM_GUILDWAR = 1042)
    /// </summary>
    public async Task SendGuildWarAsync(string targetName = "Sabuk")
    {
        var (header, payload) = SabukSiegeProtocolManager.CreateGuildWarPacket(targetName);
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 沙城城主提取城池税金 (CM_CASTLEWITHDRAWGOLD = 1043)
    /// </summary>
    public async Task SendCastleWithdrawGoldAsync(int amount)
    {
        var (header, payload) = SabukSiegeProtocolManager.CreateCastleWithdrawGoldPacket(amount);
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 发送添加好友请求 (CM_ADDFRIEND = 1050)
    /// </summary>
    public async Task SendAddFriendAsync(string friendName)
    {
        var (header, payload) = SocialProtocolManager.CreateAddFriendPacket(friendName);
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 发送删除好友请求 (CM_DELFRIEND = 1051)
    /// </summary>
    public async Task SendDelFriendAsync(string friendName)
    {
        var (header, payload) = SocialProtocolManager.CreateDelFriendPacket(friendName);
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 发送查询好友全员名册请求 (CM_QUERYFRIENDLIST = 1052)
    /// </summary>
    public async Task SendQueryFriendListAsync()
    {
        var (header, payload) = SocialProtocolManager.CreateQueryFriendListPacket();
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 发起拜师或收徒请求 (CM_MASTER_REQUEST = 1053)
    /// </summary>
    public async Task SendMasterRequestAsync(bool isApprentice, string targetName)
    {
        var (header, payload) = SocialProtocolManager.CreateMasterRequestPacket(isApprentice, targetName);
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 回应拜师或收徒申请 (CM_MASTER_RESPONSE = 1054)
    /// </summary>
    public async Task SendMasterResponseAsync(bool accept, string applicantName)
    {
        var (header, payload) = SocialProtocolManager.CreateMasterResponsePacket(accept, applicantName);
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 逐出师门或脱离师门 (CM_MASTER_EXPEL = 1055)
    /// </summary>
    public async Task SendMasterExpelAsync(bool isExpelApprentice, string targetName)
    {
        var (header, payload) = SocialProtocolManager.CreateMasterExpelPacket(isExpelApprentice, targetName);
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 举行出师典礼 (CM_MASTER_GRADUATE = 1056)
    /// </summary>
    public async Task SendMasterGraduateAsync(bool isSelf, string apprenticeName)
    {
        var (header, payload) = SocialProtocolManager.CreateMasterGraduatePacket(isSelf, apprenticeName);
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 发送开启摆摊个人商店请求 (CM_MYSHOPHAM = 9055)
    /// </summary>
    public async Task SendStartStallAsync(string title)
    {
        var (header, payload) = StallProtocolManager.CreateStartStallPacket(title);
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 发送收摊歇业请求 (CM_MYSHOPEXIT = 9053)
    /// </summary>
    public async Task SendStopStallAsync()
    {
        var (header, payload) = StallProtocolManager.CreateStopStallPacket();
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 发送上架货架商品请求 (CM_SENDSELL = 9050)
    /// </summary>
    public async Task SendAddStallItemAsync(int makeIndex, int price, string itemName)
    {
        var (header, payload) = StallProtocolManager.CreateAddItemPacket(makeIndex, price, itemName);
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 发送购买他人摊位商品请求 (CM_HAMSHOPBUY = 9057)
    /// </summary>
    public async Task SendBuyStallItemAsync(int ownerActorId, int makeIndex, int price)
    {
        var (header, payload) = StallProtocolManager.CreateBuyItemPacket(ownerActorId, makeIndex, price);
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 发送查阅他人摊位商品货架请求 (CM_OPHAMSHOP = 9061)
    /// </summary>
    public async Task SendOpenOtherStallAsync(int ownerActorId, string ownerName)
    {
        var (header, payload) = StallProtocolManager.CreateOpenOtherStallPacket(ownerActorId, ownerName);
        await SendGamePacketAsync(header, payload);
    }

    // =========================================================================
    // OpenMir2 庄园全服元宝寄售行 (DealOff) 发包 API
    // =========================================================================

    /// <summary>
    /// 发送向寄售出售窗口放入物品请求 (CM_SELLOFFADDITEM = 23002)
    /// </summary>
    public async Task SendDealOffAddItemAsync(int makeIndex, string itemName)
    {
        var (header, payload) = DealOffProtocolManager.CreateAddItemPacket(makeIndex, itemName);
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 发送从寄售出售窗口撤销物品请求 (CM_SELLOFFDELITEM = 23007)
    /// </summary>
    public async Task SendDealOffDelItemAsync(int makeIndex, string itemName)
    {
        var (header, payload) = DealOffProtocolManager.CreateDelItemPacket(makeIndex, itemName);
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 发送取消元宝寄售出售窗口请求 (CM_SELLOFFCANCEL = 23012)
    /// </summary>
    public async Task SendDealOffCancelAsync()
    {
        var (header, payload) = DealOffProtocolManager.CreateCancelPacket();
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 发送确认元宝寄售挂牌请求 (CM_SELLOFFEND = 23015)
    /// </summary>
    public async Task SendDealOffEndAsync(int sellGold, int gameDiamond = 0, string buyChrName = "")
    {
        var (header, payload) = DealOffProtocolManager.CreateEndPacket(sellGold, gameDiamond, buyChrName);
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 出售人发送取消正在出售中的寄售单请求 (CM_CANCELSELLOFFITEMING = 23024)
    /// </summary>
    public async Task SendDealOffCancelSellingAsync()
    {
        var (header, payload) = DealOffProtocolManager.CreateCancelSellingPacket();
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 购买人发送取消选购请求 (CM_SELLOFFBUYCANCEL = 23025)
    /// </summary>
    public async Task SendDealOffBuyCancelAsync(string sellerName)
    {
        var (header, payload) = DealOffProtocolManager.CreateBuyCancelPacket(sellerName);
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 购买人发送确认购买商品请求 (CM_SELLOFFBUY = 23026)
    /// </summary>
    public async Task SendDealOffBuyAsync(string sellerName)
    {
        var (header, payload) = DealOffProtocolManager.CreateBuyPacket(sellerName);
        await SendGamePacketAsync(header, payload);
    }

    // =========================================================================
    // OpenMir2 动作身法与战士高级打击发包 API
    // =========================================================================

    /// <summary>
    /// 发送打坐蹲下休息请求 (CM_SITDOWN = 3012)
    /// </summary>
    public async Task SendSitDownAsync(int x, int y, int dir)
    {
        var (header, payload) = CombatActionProtocolManager.CreateSitDownPacket(x, y, dir);
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 发送战马奔跑冲刺请求 (CM_HORSERUN = 3035)
    /// </summary>
    public async Task SendHorseRunAsync(int targetX, int targetY, int dir)
    {
        var (header, payload) = CombatActionProtocolManager.CreateHorseRunPacket(targetX, targetY, dir);
        await SendGamePacketAsync(header, payload);
    }

    /// <summary>
    /// 发送战士打击动作请求 (攻杀/刺杀/半月/烈火/开天等)
    /// </summary>
    public async Task SendCombatSkillHitAsync(CombatSkillType skillType, int targetX, int targetY, int dir)
    {
        var (header, payload) = CombatActionProtocolManager.CreateCombatSkillPacket(skillType, targetX, targetY, dir);
        await SendGamePacketAsync(header, payload);
    }

    // =========================================================================
    // OpenMir2 NPC 交互经济与辅助功能发包 API
    // =========================================================================

    public async Task SendQuerySellPriceAsync(int merchantActorId, int makeIndex, string itemName)
    {
        var (header, payload) = MerchantEconomyProtocolManager.CreateQuerySellPricePacket(merchantActorId, makeIndex, itemName);
        await SendGamePacketAsync(header, payload);
    }

    public async Task SendQueryRepairCostAsync(int merchantActorId, int makeIndex, string itemName)
    {
        var (header, payload) = MerchantEconomyProtocolManager.CreateQueryRepairCostPacket(merchantActorId, makeIndex, itemName);
        await SendGamePacketAsync(header, payload);
    }

    public async Task SendGetDetailItemAsync(int merchantActorId, int menuIndex, string itemName)
    {
        var (header, payload) = MerchantEconomyProtocolManager.CreateGetDetailItemPacket(merchantActorId, menuIndex, itemName);
        await SendGamePacketAsync(header, payload);
    }

    public async Task SendDropGoldAsync(int amount)
    {
        var (header, payload) = MerchantEconomyProtocolManager.CreateDropGoldPacket(amount);
        await SendGamePacketAsync(header, payload);
    }

    public async Task SendMakeDrugAsync(int merchantActorId, string itemName)
    {
        var (header, payload) = MerchantEconomyProtocolManager.CreateMakeDrugPacket(merchantActorId, itemName);
        await SendGamePacketAsync(header, payload);
    }

    public async Task SendCheckItemAsync()
    {
        var (header, payload) = MerchantEconomyProtocolManager.CreateCheckItemPacket();
        await SendGamePacketAsync(header, payload);
    }

    public async Task SendOpenDoorAsync(int doorId, int x, int y)
    {
        var (header, payload) = MerchantEconomyProtocolManager.CreateOpenDoorPacket(doorId, x, y);
        await SendGamePacketAsync(header, payload);
    }

    public async Task SendSoftCloseDoorAsync(int doorId, int x, int y)
    {
        var (header, payload) = MerchantEconomyProtocolManager.CreateSoftCloseDoorPacket(doorId, x, y);
        await SendGamePacketAsync(header, payload);
    }

    public async Task SendQueryBagItemsAsync()
    {
        var (header, payload) = MerchantEconomyProtocolManager.CreateQueryBagItemsPacket();
        await SendGamePacketAsync(header, payload);
    }

    public async Task SendQueryUserStateAsync()
    {
        var (header, payload) = MerchantEconomyProtocolManager.CreateQueryUserStatePacket();
        await SendGamePacketAsync(header, payload);
    }

    public async Task SendQueryUserNameAsync(int actorId)
    {
        var (header, payload) = MerchantEconomyProtocolManager.CreateQueryUserNamePacket(actorId);
        await SendGamePacketAsync(header, payload);
    }

    // =========================================================================
    // OpenMir2 行会外交与二级密码发包 API
    // =========================================================================

    public async Task SendGuildAllyAsync()
    {
        var (header, payload) = GuildDiplomacyAndSecurityProtocolManager.CreateGuildAllyPacket();
        await SendGamePacketAsync(header, payload);
    }

    public async Task SendGuildBreakAllyAsync(string targetGuildName)
    {
        var (header, payload) = GuildDiplomacyAndSecurityProtocolManager.CreateGuildBreakAllyPacket(targetGuildName);
        await SendGamePacketAsync(header, payload);
    }

    public async Task SendVerifyPasswordAsync(string password)
    {
        var (header, payload) = GuildDiplomacyAndSecurityProtocolManager.CreateVerifyPasswordPacket(password);
        await SendGamePacketAsync(header, payload);
    }

    public async Task SendChangePasswordAsync(string oldPassword, string newPassword)
    {
        var (header, payload) = GuildDiplomacyAndSecurityProtocolManager.CreateChangePasswordPacket(oldPassword, newPassword);
        await SendGamePacketAsync(header, payload);
    }

    public async Task SendSetPasswordAsync(string password)
    {
        var (header, payload) = GuildDiplomacyAndSecurityProtocolManager.CreateSetPasswordPacket(password);
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
