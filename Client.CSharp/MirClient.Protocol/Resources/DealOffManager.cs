using System;
using System.Collections.Generic;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 盛大十周年与 OpenMir2 庄园全服元宝寄售行客户端领域状态管理器
/// 集中管理寄售窗口、挂牌货架、全服市场货架以及在售单的生命周期
/// </summary>
public class DealOffManager
{
    private readonly List<DealOffItem> _sellingItems = new();
    private readonly List<ClientDealOffInfo> _marketList = new();
    private readonly List<ClientDealOffInfo> _mySellingList = new();

    /// <summary>
    /// 元宝寄售窗口是否处于开启状态
    /// </summary>
    public bool IsOpen { get; private set; }

    /// <summary>
    /// 当前交互的庄园寄售商人/NPC ActorId
    /// </summary>
    public int NpcActorId { get; private set; }

    /// <summary>
    /// 当前挂牌设定的出售元宝数
    /// </summary>
    public int SellGold { get; set; }

    /// <summary>
    /// 当前挂牌设定的附带金刚石数
    /// </summary>
    public int GameDiamond { get; set; }

    /// <summary>
    /// 指定购买人角色名 (留空代表全服公开选购)
    /// </summary>
    public string TargetBuyer { get; set; } = string.Empty;

    /// <summary>
    /// 当前寄售出售窗口中准备挂牌的物品列表 (最多9件)
    /// </summary>
    public IReadOnlyList<DealOffItem> SellingItems => _sellingItems;

    /// <summary>
    /// 全服正在出售的可购买货单列表
    /// </summary>
    public IReadOnlyList<ClientDealOffInfo> MarketList => _marketList;

    /// <summary>
    /// 玩家自身正在寄售中的货单列表
    /// </summary>
    public IReadOnlyList<ClientDealOffInfo> MySellingList => _mySellingList;

    #region 事件通知委托

    public event Action? OnDealOffFormOpened;
    public event Action? OnDealOffFormClosed;
    public event Action<DealOffItem>? OnItemAdded;
    public event Action<string>? OnAddItemFailed;
    public event Action<int>? OnItemRemoved;
    public event Action<string>? OnDelItemFailed;
    public event Action? OnListedSuccess;
    public event Action<string>? OnListedFailed;
    public event Action<ClientDealOffInfo>? OnMarketDealUpdated;
    public event Action<ClientDealOffInfo>? OnMySellingUpdated;
    public event Action? OnBuyItemSuccess;

    #endregion

    #region 窗口与寄售操作驱动

    /// <summary>
    /// 服务端下发 SM_SENDDEALOFFFORM，开启寄售窗口
    /// </summary>
    public void HandleOpenDealOffForm(int npcActorId)
    {
        IsOpen = true;
        NpcActorId = npcActorId;
        _sellingItems.Clear();
        SellGold = 0;
        GameDiamond = 0;
        TargetBuyer = string.Empty;
        OnDealOffFormOpened?.Invoke();
    }

    /// <summary>
    /// 服务端确认物品放入寄售窗口成功 (SM_SELLOFFADDITEM_OK)
    /// </summary>
    public void HandleAddItemSuccess(DealOffItem item)
    {
        if (item == null) return;
        if (_sellingItems.Count < 9)
        {
            _sellingItems.Add(item);
            OnItemAdded?.Invoke(item);
        }
    }

    /// <summary>
    /// 服务端通知物品放入失败 (SM_SellOffADDITEM_FAIL)
    /// </summary>
    public void HandleAddItemFail(string reason = "无法将该物品放入寄售窗口")
    {
        OnAddItemFailed?.Invoke(reason);
    }

    /// <summary>
    /// 服务端确认从寄售窗口撤下物品成功 (SM_SELLOFFDELITEM_OK)
    /// </summary>
    public void HandleDelItemSuccess(int makeIndex)
    {
        int idx = _sellingItems.FindIndex(it => it.MakeIndex == makeIndex);
        if (idx >= 0)
        {
            _sellingItems.RemoveAt(idx);
            OnItemRemoved?.Invoke(makeIndex);
        }
    }

    /// <summary>
    /// 服务端通知撤下物品失败 (SM_SELLOFFDELITEM_FAIL)
    /// </summary>
    public void HandleDelItemFail(string reason = "撤销寄售窗口物品失败")
    {
        OnDelItemFailed?.Invoke(reason);
    }

    /// <summary>
    /// 服务端确认取消元宝寄售 (SM_SellOffCANCEL)
    /// </summary>
    public void HandleCancelSuccess()
    {
        IsOpen = false;
        _sellingItems.Clear();
        OnDealOffFormClosed?.Invoke();
    }

    /// <summary>
    /// 服务端确认挂牌寄售成功 (SM_SELLOFFEND_OK)
    /// </summary>
    public void HandleEndSuccess()
    {
        IsOpen = false;
        _sellingItems.Clear();
        SellGold = 0;
        GameDiamond = 0;
        TargetBuyer = string.Empty;
        OnListedSuccess?.Invoke();
        OnDealOffFormClosed?.Invoke();
    }

    /// <summary>
    /// 服务端通知挂牌寄售失败 (SM_SELLOFFEND_FAIL)
    /// </summary>
    public void HandleEndFail(string reason = "寄售挂牌失败，请检查元宝标价或手续费")
    {
        OnListedFailed?.Invoke(reason);
    }

    /// <summary>
    /// 接收全服货架单据 (SM_QUERYYBDEAL)
    /// </summary>
    public void HandleUpdateMarket(ClientDealOffInfo info)
    {
        if (info == null) return;
        _marketList.RemoveAll(d => d.DealChrName == info.DealChrName && Math.Abs(d.SellDateTime - info.SellDateTime) < 0.00001);
        _marketList.Add(info);
        OnMarketDealUpdated?.Invoke(info);
    }

    /// <summary>
    /// 接收自身在售货架单据 (SM_QUERYYBSELL)
    /// </summary>
    public void HandleUpdateMySell(ClientDealOffInfo info)
    {
        if (info == null) return;
        _mySellingList.RemoveAll(d => d.DealChrName == info.DealChrName && Math.Abs(d.SellDateTime - info.SellDateTime) < 0.00001);
        _mySellingList.Add(info);
        OnMySellingUpdated?.Invoke(info);
    }

    /// <summary>
    /// 服务端确认购买寄售商品成功 (SM_SELLOFFBUY_OK)
    /// </summary>
    public void HandleBuySuccess(string sellerName)
    {
        _marketList.RemoveAll(d => string.Equals(d.DealChrName, sellerName, StringComparison.OrdinalIgnoreCase));
        OnBuyItemSuccess?.Invoke();
    }

    /// <summary>
    /// 清理所有市场单据缓存
    /// </summary>
    public void ClearMarketCache()
    {
        _marketList.Clear();
        _mySellingList.Clear();
    }

    #endregion
}
