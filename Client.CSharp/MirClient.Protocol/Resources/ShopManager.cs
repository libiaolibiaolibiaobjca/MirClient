using System;
using System.Collections.Generic;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 商店货架模式 (购买、出售、修理)
/// </summary>
public enum ShopMode
{
    Buy,    // 购买物品
    Sell,   // 出售换金币
    Repair  // 装备修理
}

/// <summary>
/// NPC 商店货架与经济交易管理器 (单例)
/// 严格 1:1 对齐 OpenMir2 Merchant.cs 商业货架与买卖机制
/// </summary>
public class ShopManager
{
    private static readonly Lazy<ShopManager> _instance = new(() => new ShopManager());
    public static ShopManager Instance => _instance.Value;

    public int CurrentMerchantId { get; set; } = 0;
    public string MerchantName { get; set; } = "药店老板";
    public ShopMode CurrentMode { get; set; } = ShopMode.Buy;
    public bool IsOpen { get; set; } = false;

    private readonly List<ClientGoodsItem> _goods = new();
    private readonly object _lock = new();

    public event Action? OnGoodsChanged;

    public ShopManager()
    {
        InitDefaultGoods();
    }

    /// <summary>
    /// 初始化经典默认货架商品 (单机或离线测试使用)
    /// </summary>
    public void InitDefaultGoods()
    {
        lock (_lock)
        {
            _goods.Clear();
            _goods.Add(new ClientGoodsItem("金创药(小量)", 0, 88, 999));
            _goods.Add(new ClientGoodsItem("魔法药(小量)", 1, 88, 999));
            _goods.Add(new ClientGoodsItem("强效太阳水", 2, 550, 200));
            _goods.Add(new ClientGoodsItem("随机传送卷", 3, 120, 500));
            _goods.Add(new ClientGoodsItem("回城卷", 4, 300, 300));
            _goods.Add(new ClientGoodsItem("地牢逃脱卷", 5, 200, 300));
            _goods.Add(new ClientGoodsItem("乌木剑", 6, 1200, 20));
            _goods.Add(new ClientGoodsItem("布衣(男)", 7, 800, 30));
            _goods.Add(new ClientGoodsItem("布衣(女)", 8, 800, 30));
        }
    }

    /// <summary>
    /// 全量更新来自服务端的货架列表 (SM_SENDGOODSLIST)
    /// </summary>
    public void SetGoodsList(int merchantId, List<ClientGoodsItem> items)
    {
        CurrentMerchantId = merchantId;
        lock (_lock)
        {
            _goods.Clear();
            if (items != null && items.Count > 0)
            {
                _goods.AddRange(items);
            }
            else
            {
                InitDefaultGoods();
            }
        }
        IsOpen = true;
        CurrentMode = ShopMode.Buy;
        OnGoodsChanged?.Invoke();
    }

    public IReadOnlyList<ClientGoodsItem> GetGoods()
    {
        lock (_lock)
        {
            return _goods.ToArray();
        }
    }

    /// <summary>
    /// 打开指定模式的商店面板
    /// </summary>
    public void Open(int merchantId, string name, ShopMode mode = ShopMode.Buy)
    {
        CurrentMerchantId = merchantId;
        MerchantName = string.IsNullOrEmpty(name) ? "商人" : name;
        CurrentMode = mode;
        IsOpen = true;
        OnGoodsChanged?.Invoke();
    }

    public void Close()
    {
        IsOpen = false;
    }
}
