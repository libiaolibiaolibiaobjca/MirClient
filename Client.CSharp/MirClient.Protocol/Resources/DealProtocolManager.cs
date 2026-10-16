using System;
using System.Collections.Generic;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 交易流程状态
/// </summary>
public enum DealState
{
    None,       // 未交易
    Trading,    // 交易窗口进行中 (双方摆放物品与金币)
    Locked,     // 已点击确认锁定 (等待对方或完成)
    Success,    // 交易成功完成
    Cancelled   // 交易被取消中止
}

/// <summary>
/// 交易栏内单件物品槽位数据
/// </summary>
public record DealItemSlot(
    int MakeIndex,
    string Name,
    int Dura = 10000,
    int DuraMax = 10000);

/// <summary>
/// 玩家面对面交易领域状态机
/// </summary>
public class DealManager
{
    private static DealManager? _instance;
    public static DealManager Instance => _instance ??= new DealManager();

    public const int MAX_DEAL_ITEMS = 12; // 盛大经典 3x4 交易槽上限

    public DealState State { get; private set; } = DealState.None;
    public string RemoteTargetName { get; private set; } = "";
    public int LocalGold { get; private set; } = 0;
    public int RemoteGold { get; private set; } = 0;
    public bool LocalLocked { get; private set; } = false;
    public bool RemoteLocked { get; private set; } = false;

    private readonly List<DealItemSlot> _localItems = new();
    private readonly List<DealItemSlot> _remoteItems = new();

    public IReadOnlyList<DealItemSlot> LocalItems => _localItems;
    public IReadOnlyList<DealItemSlot> RemoteItems => _remoteItems;
    public bool IsDealing => State != DealState.None && State != DealState.Cancelled && State != DealState.Success;

    public void Reset()
    {
        State = DealState.None;
        RemoteTargetName = "";
        LocalGold = 0;
        RemoteGold = 0;
        LocalLocked = false;
        RemoteLocked = false;
        _localItems.Clear();
        _remoteItems.Clear();
    }

    public void StartDeal(string remotePlayer)
    {
        Reset();
        RemoteTargetName = remotePlayer ?? "";
        State = DealState.Trading;
    }

    public bool AddLocalItem(int makeIndex, string name)
    {
        if (_localItems.Count >= MAX_DEAL_ITEMS || LocalLocked) return false;
        if (_localItems.Exists(i => i.MakeIndex == makeIndex)) return false;

        _localItems.Add(new DealItemSlot(makeIndex, name));
        return true;
    }

    public bool RemoveLocalItem(int makeIndex)
    {
        if (LocalLocked) return false;
        int idx = _localItems.FindIndex(i => i.MakeIndex == makeIndex);
        if (idx >= 0)
        {
            _localItems.RemoveAt(idx);
            return true;
        }
        return false;
    }

    public void AddRemoteItem(int makeIndex, string name)
    {
        if (_remoteItems.Count < MAX_DEAL_ITEMS && !_remoteItems.Exists(i => i.MakeIndex == makeIndex))
        {
            _remoteItems.Add(new DealItemSlot(makeIndex, name));
        }
    }

    public void RemoveRemoteItem(int makeIndex)
    {
        _remoteItems.RemoveAll(i => i.MakeIndex == makeIndex);
    }

    public void SetLocalGold(int gold)
    {
        if (!LocalLocked)
        {
            LocalGold = Math.Max(0, gold);
        }
    }

    public void SetRemoteGold(int gold)
    {
        RemoteGold = Math.Max(0, gold);
    }

    public void SetLocalLock(bool locked)
    {
        LocalLocked = locked;
        if (LocalLocked)
        {
            State = DealState.Locked;
        }
    }

    public void SetRemoteLock(bool locked)
    {
        RemoteLocked = locked;
    }

    public void CompleteDeal()
    {
        State = DealState.Success;
    }

    public void CancelDeal()
    {
        State = DealState.Cancelled;
    }
}

/// <summary>
/// OpenMir2 玩家面对面安全交易网络协议管理器
/// </summary>
public static class DealProtocolManager
{
    /// <summary>
    /// 构造发起交易请求数据包 (CM_DEALTRY = 1025)
    /// </summary>
    public static (DefaultMessage Header, string EncodedPayload) CreateDealTryPacket(string targetName)
    {
        DefaultMessage header = new(
            ident: ProtocolConsts.CM_DEALTRY,
            recog: 0,
            param: 0,
            tag: 0,
            series: 0);
        string payload = EDCode.EncodeString(targetName ?? string.Empty);
        return (header, payload);
    }

    /// <summary>
    /// 构造放入交易物品数据包 (CM_DEALADDITEM = 1026)
    /// </summary>
    public static (DefaultMessage Header, string EncodedPayload) CreateDealAddItemPacket(int makeIndex, string itemName)
    {
        DefaultMessage header = new(
            ident: ProtocolConsts.CM_DEALADDITEM,
            recog: 0,
            param: (ushort)(makeIndex & 0xFFFF),
            tag: (ushort)((makeIndex >> 16) & 0xFFFF),
            series: 0);
        string payload = EDCode.EncodeString(itemName ?? string.Empty);
        return (header, payload);
    }

    /// <summary>
    /// 构造撤回交易物品数据包 (CM_DEALDELITEM = 1027)
    /// </summary>
    public static (DefaultMessage Header, string EncodedPayload) CreateDealDelItemPacket(int makeIndex, string itemName)
    {
        DefaultMessage header = new(
            ident: ProtocolConsts.CM_DEALDELITEM,
            recog: 0,
            param: (ushort)(makeIndex & 0xFFFF),
            tag: (ushort)((makeIndex >> 16) & 0xFFFF),
            series: 0);
        string payload = EDCode.EncodeString(itemName ?? string.Empty);
        return (header, payload);
    }

    /// <summary>
    /// 构造修改交易金币数据包 (CM_DEALCHGGOLD = 1029)
    /// </summary>
    public static (DefaultMessage Header, string EncodedPayload) CreateDealChgGoldPacket(int gold)
    {
        DefaultMessage header = new(
            ident: ProtocolConsts.CM_DEALCHGGOLD,
            recog: gold,
            param: (ushort)(gold & 0xFFFF),
            tag: (ushort)((gold >> 16) & 0xFFFF),
            series: 0);
        return (header, string.Empty);
    }

    /// <summary>
    /// 构造确认锁定与成交数据包 (CM_DEALEND = 1030)
    /// </summary>
    public static (DefaultMessage Header, string EncodedPayload) CreateDealEndPacket()
    {
        DefaultMessage header = new(
            ident: ProtocolConsts.CM_DEALEND,
            recog: 0,
            param: 0,
            tag: 0,
            series: 0);
        return (header, string.Empty);
    }

    /// <summary>
    /// 构造取消交易数据包 (CM_DEALCANCEL = 1028)
    /// </summary>
    public static (DefaultMessage Header, string EncodedPayload) CreateDealCancelPacket()
    {
        DefaultMessage header = new(
            ident: ProtocolConsts.CM_DEALCANCEL,
            recog: 0,
            param: 0,
            tag: 0,
            series: 0);
        return (header, string.Empty);
    }

    /// <summary>
    /// 解析打开交易视窗下行包 (SM_DEALMENU = 673)
    /// </summary>
    public static string ParseDealMenu(DefaultMessage header, string rawBody)
    {
        return EDCode.DecodeText(rawBody ?? string.Empty);
    }

    /// <summary>
    /// 解析同步的金币数值 (SM_DEALCHGGOLD_OK / SM_DEALREMOTECHGGOLD)
    /// </summary>
    public static int ParseDealGold(DefaultMessage header)
    {
        return header.Recog > 0 ? header.Recog : (header.Param | (header.Tag << 16));
    }
}
