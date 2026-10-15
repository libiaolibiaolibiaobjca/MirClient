using System.Numerics;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 玩家摊位货架商品条目
/// </summary>
public class StallItem
{
    public int SlotIndex { get; set; }
    public Item Item { get; set; } = null!;
    public int Price { get; set; }
    public DateTime AddedTime { get; set; } = DateTime.UtcNow;

    public StallItem() { }

    public StallItem(int slotIndex, Item item, int price)
    {
        SlotIndex = slotIndex;
        Item = item;
        Price = price;
        AddedTime = DateTime.UtcNow;
    }
}

/// <summary>
/// 盛大传奇十周年：主城安全区玩家摆摊与自由交易市场管理器 (纯 C# 领域状态机)
/// </summary>
public class StallManager
{
    private static StallManager? _instance;
    public static StallManager Instance => _instance ??= new StallManager();

    public const int MAX_SHELVES = 10; // 摊位货架最大容纳 10 件商品

    public bool IsStalling { get; private set; } = false;
    public string StallTitle { get; private set; } = "";
    public string OwnerName { get; private set; } = "逐鹿天下";
    public string StallMapId { get; private set; } = "0";
    public Vector2 StallTile { get; private set; } = new(330, 268);
    public int TotalEarnings { get; private set; } = 0;

    private readonly List<StallItem> _shelves = new();
    public IReadOnlyList<StallItem> Shelves => _shelves;

    public StallManager() { }

    /// <summary>
    /// 重置摆摊状态 (单元测试复位)
    /// </summary>
    public void Reset()
    {
        IsStalling = false;
        StallTitle = "";
        OwnerName = "逐鹿天下";
        StallMapId = "0";
        StallTile = new(330, 268);
        TotalEarnings = 0;
        _shelves.Clear();
    }

    /// <summary>
    /// 判定坐标是否位于主城安全区 (比奇省 330, 268 / 盟重省 330, 330)
    /// </summary>
    public static bool IsInSafeZone(string mapId, Vector2 tile)
    {
        if (string.IsNullOrWhiteSpace(mapId)) return false;

        string id = mapId.Trim().ToLowerInvariant();

        // 1. 比奇省 (0) 安全区：以老兵 (330, 268) 为中心，半径 25 格
        if (id == "0" || id.Contains("比奇") || id.Contains("biqi"))
        {
            float dist = Vector2.Distance(tile, new Vector2(330, 268));
            return dist <= 25.0f;
        }

        // 2. 盟重省 (3) 安全区：以红名村外老兵 (330, 330) 为中心，半径 30 格
        if (id == "3" || id.Contains("盟重") || id.Contains("mengzhong"))
        {
            float dist = Vector2.Distance(tile, new Vector2(330, 330));
            return dist <= 30.0f;
        }

        return false;
    }

    /// <summary>
    /// 在主城安全区开启摆摊
    /// </summary>
    public bool StartStall(string title, string ownerName, string mapId, Vector2 tile, out string message)
    {
        if (IsStalling)
        {
            message = "当前已在摆摊中，无需重复开启！";
            return false;
        }

        title = title?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(title))
        {
            title = "【极品装备】特价大甩卖";
        }
        else if (title.Length > 20)
        {
            title = title[..20];
        }

        if (!IsInSafeZone(mapId, tile))
        {
            message = "玛法行商令：唯有在主城安全区集市方可设摊营业！";
            return false;
        }

        IsStalling = true;
        StallTitle = title;
        OwnerName = string.IsNullOrWhiteSpace(ownerName) ? "逐鹿天下" : ownerName.Trim();
        StallMapId = mapId;
        StallTile = tile;

        message = $"摊位【{StallTitle}】已在安全区支起！可随时上架商品或开启公屏叫卖。";
        return true;
    }

    /// <summary>
    /// 收摊并将货架未售出物品全数放回背包
    /// </summary>
    public bool StopStall(InventoryManager inventory, out string message)
    {
        if (!IsStalling)
        {
            message = "当前未处于摆摊状态！";
            return false;
        }

        int returnedCount = 0;
        foreach (var shelf in _shelves)
        {
            if (inventory.AddItem(shelf.Item))
            {
                returnedCount++;
            }
        }

        _shelves.Clear();
        IsStalling = false;

        message = $"已顺利收摊！取回未售商品 {returnedCount} 件，本次摆摊累计金币收益: {TotalEarnings} 金币。";
        return true;
    }

    /// <summary>
    /// 从背包上架物品到摊位货架
    /// </summary>
    public bool AddItemToShelf(InventoryManager inventory, int bagSlot, int price, out string message)
    {
        if (!IsStalling)
        {
            message = "请先开启摆摊再进行商品上架！";
            return false;
        }

        if (_shelves.Count >= MAX_SHELVES)
        {
            message = $"摊位货架已满 (上限 {MAX_SHELVES} 件)！";
            return false;
        }

        if (price <= 0)
        {
            message = "上架商品定价必须大于 0 金币！";
            return false;
        }

        Item? item = inventory.GetItem(bagSlot);
        if (item == null)
        {
            message = "指定的背包槽位为空，无法上架！";
            return false;
        }

        // 从背包移除
        inventory.RemoveItem(bagSlot);

        int nextSlot = _shelves.Count;
        StallItem stallItem = new(nextSlot, item, price);
        _shelves.Add(stallItem);

        message = $"成功上架【{item.Name}】，单价标为: {price} 金币！";
        return true;
    }

    /// <summary>
    /// 从摊位货架下架物品放回背包
    /// </summary>
    public bool RemoveItemFromShelf(InventoryManager inventory, int shelfIndex, out string message)
    {
        if (shelfIndex < 0 || shelfIndex >= _shelves.Count)
        {
            message = "指定的货架商品不存在！";
            return false;
        }

        StallItem target = _shelves[shelfIndex];

        // 尝试放回背包
        if (!inventory.AddItem(target.Item))
        {
            message = "背包空间不足，无法取回该商品！";
            return false;
        }

        _shelves.RemoveAt(shelfIndex);
        ReindexShelves();

        message = $"已下架【{target.Item.Name}】并收纳至随身背包。";
        return true;
    }

    /// <summary>
    /// 模拟买家选购摊位商品并完成原子性交割结算
    /// </summary>
    public bool BuyItem(InventoryManager buyerInventory, ref int buyerGold, int shelfIndex, out string message)
    {
        if (shelfIndex < 0 || shelfIndex >= _shelves.Count)
        {
            message = "该商品已售罄或已被下架！";
            return false;
        }

        StallItem target = _shelves[shelfIndex];

        if (buyerGold < target.Price)
        {
            message = $"金币不足！购买【{target.Item.Name}】需要 {target.Price} 金币，您仅有 {buyerGold} 金币。";
            return false;
        }

        // 背包空位预检
        if (buyerInventory.ItemCount >= InventoryManager.BAG_CAPACITY)
        {
            message = "买家背包已满，无法容纳新物品！";
            return false;
        }

        // 扣除金币与转移物品
        buyerGold -= target.Price;
        TotalEarnings += target.Price;

        buyerInventory.AddItem(target.Item);
        _shelves.RemoveAt(shelfIndex);
        ReindexShelves();

        message = $"成交！买家成功购得【{target.Item.Name}】，支付 {target.Price} 金币！";
        return true;
    }

    /// <summary>
    /// 生成摆摊叫卖喊话文本 (用于公屏高亮广播)
    /// </summary>
    public string GenerateShoutMessage()
    {
        if (!IsStalling) return "当前并未在摆摊营业！";

        string itemCountStr = _shelves.Count > 0 ? $"货架在售 {_shelves.Count} 件珍宝" : "货架筹备补货中";
        return $"[摆摊叫卖] 【{StallTitle}】摊主 {OwnerName} 热销中！{itemCountStr}，物美价廉童叟无欺！(坐标: {(int)StallTile.X}, {(int)StallTile.Y})";
    }

    private void ReindexShelves()
    {
        for (int i = 0; i < _shelves.Count; i++)
        {
            _shelves[i].SlotIndex = i;
        }
    }
}
