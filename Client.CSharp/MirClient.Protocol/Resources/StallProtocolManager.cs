using System;
using System.Collections.Generic;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 他人摊位货架条目快照模型
/// </summary>
public record RemoteStallShelfItem(
    int SlotIndex,
    int MakeIndex,
    string ItemName,
    int Price,
    int Dura = 10,
    int MaxDura = 10
);

/// <summary>
/// 地表摊位招牌广播数据
/// </summary>
public record StallSignboardRecord(
    int OwnerActorId,
    string Title,
    string OwnerName,
    int TileX,
    int TileY
);

/// <summary>
/// 盛大十周年与 OpenMir2 摊位摆摊与个人商店网络协议管理器
/// </summary>
public static class StallProtocolManager
{
    #region 客户端发包构造 (CM_)

    /// <summary>
    /// 构造开启摆摊个人商店封包 (CM_MYSHOPHAM = 9055)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateStartStallPacket(string title)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_MYSHOPHAM,
            Recog = 0,
            Param = 0,
            Tag = 0,
            Series = 0
        };
        string stallTitle = string.IsNullOrWhiteSpace(title) ? "【极品装备】特价大甩卖" : title.Trim();
        return (header, stallTitle);
    }

    /// <summary>
    /// 构造收摊退出个人商店封包 (CM_MYSHOPEXIT = 9053)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateStopStallPacket()
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_MYSHOPEXIT,
            Recog = 0,
            Param = 0,
            Tag = 0,
            Series = 0
        };
        return (header, string.Empty);
    }

    /// <summary>
    /// 构造上架货架商品封包 (CM_SENDSELL = 9050)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateAddItemPacket(int makeIndex, int price, string itemName)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_SENDSELL,
            Recog = 0,
            Param = (ushort)(makeIndex & 0xFFFF),
            Tag = (ushort)Math.Clamp(price, 1, ushort.MaxValue),
            Series = 0
        };
        return (header, itemName?.Trim() ?? string.Empty);
    }

    /// <summary>
    /// 构造购买他人摊位商品封包 (CM_HAMSHOPBUY = 9057)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateBuyItemPacket(int ownerActorId, int makeIndex, int price)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_HAMSHOPBUY,
            Recog = ownerActorId,
            Param = (ushort)(makeIndex & 0xFFFF),
            Tag = (ushort)Math.Clamp(price, 1, ushort.MaxValue),
            Series = 0
        };
        return (header, string.Empty);
    }

    /// <summary>
    /// 构造查阅他人摊位商品货架封包 (CM_OPHAMSHOP = 9061)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateOpenOtherStallPacket(int ownerActorId, string ownerName)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_OPHAMSHOP,
            Recog = ownerActorId,
            Param = 0,
            Tag = 0,
            Series = 0
        };
        return (header, ownerName?.Trim() ?? string.Empty);
    }

    #endregion

    #region 服务端下发解析 (SM_)

    /// <summary>
    /// 解析摊位开启成功确认 (SM_MYSHOPHAM = 9056)
    /// </summary>
    public static string ParseStartStallResponse(DefaultMessage header, string rawPayload)
    {
        return !string.IsNullOrWhiteSpace(rawPayload) ? rawPayload.Trim() : "【极品装备】特价大甩卖";
    }

    /// <summary>
    /// 解析商品上架成功通知 (SM_SENDSELL = 9051)
    /// Recog: SlotIndex, Param: MakeIndex, Tag: Price, Payload: ItemName
    /// </summary>
    public static (int SlotIndex, int MakeIndex, int Price, string ItemName) ParseAddItemResponse(DefaultMessage header, string rawPayload)
    {
        int slot = header.Recog;
        int makeIndex = header.Param;
        int price = header.Tag;
        string name = rawPayload?.Trim() ?? "未知宝物";
        return (slot, makeIndex, price, name);
    }

    /// <summary>
    /// 解析收摊成功通知 (SM_MYSHOPEXIT = 9054)
    /// </summary>
    public static string ParseStopStallResponse(DefaultMessage header, string rawPayload)
    {
        return !string.IsNullOrWhiteSpace(rawPayload) ? rawPayload.Trim() : "摊位已收，货物已入包。";
    }

    /// <summary>
    /// 解析买家购得商品通知 (SM_HAMSHOPBUYA = 9058)
    /// Recog: 扣除金币数, Payload: 购得物品名
    /// </summary>
    public static (int CostGold, string ItemName) ParseBuyerSuccess(DefaultMessage header, string rawPayload)
    {
        int cost = header.Recog;
        string name = rawPayload?.Trim() ?? "珍稀宝物";
        return (cost, name);
    }

    /// <summary>
    /// 解析卖家商品售出结算通知 (SM_HAMSHOPBUYB = 9059)
    /// Recog: 获得金币, Payload: 格式 "ItemName/BuyerName"
    /// </summary>
    public static (int EarnGold, string ItemName, string BuyerName) ParseSellerSold(DefaultMessage header, string rawPayload)
    {
        int earn = header.Recog;
        string itemName = "商品";
        string buyerName = "热心买家";

        if (!string.IsNullOrWhiteSpace(rawPayload))
        {
            var parts = rawPayload.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0) itemName = parts[0].Trim();
            if (parts.Length > 1) buyerName = parts[1].Trim();
        }

        return (earn, itemName, buyerName);
    }

    /// <summary>
    /// 解析地表摊位招牌广播 (SM_HAMSHOPTYPE = 9060)
    /// Recog: 摊主ActorId, Payload: "Title/OwnerName/TileX/TileY"
    /// </summary>
    public static StallSignboardRecord ParseStallType(DefaultMessage header, string rawPayload)
    {
        int ownerId = header.Recog;
        string title = "极品地摊";
        string owner = "逐鹿天下";
        int x = 330;
        int y = 268;

        if (!string.IsNullOrWhiteSpace(rawPayload))
        {
            var parts = rawPayload.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0) title = parts[0].Trim();
            if (parts.Length > 1) owner = parts[1].Trim();
            if (parts.Length > 2 && int.TryParse(parts[2], out int px)) x = px;
            if (parts.Length > 3 && int.TryParse(parts[3], out int py)) y = py;
        }

        return new StallSignboardRecord(ownerId, title, owner, x, y);
    }

    /// <summary>
    /// 解析他人摊位货架列表 (SM_OPHAMSHOP = 9062)
    /// 格式: 多行或分号分隔 "Slot/MakeIndex/ItemName/Price/Dura/MaxDura"
    /// </summary>
    public static List<RemoteStallShelfItem> ParseOtherStallShelves(string rawPayload)
    {
        var list = new List<RemoteStallShelfItem>();
        if (string.IsNullOrWhiteSpace(rawPayload)) return list;

        string[] lines = rawPayload.Contains('\n')
            ? rawPayload.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            : rawPayload.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var parts = line.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) continue;

            int slot = int.TryParse(parts[0], out int sVal) ? sVal : list.Count;
            int makeIndex = int.TryParse(parts[1], out int mVal) ? mVal : 0;
            string name = parts[2].Trim();
            int price = parts.Length > 3 && int.TryParse(parts[3], out int pVal) ? pVal : 0;
            int dura = parts.Length > 4 && int.TryParse(parts[4], out int dVal) ? dVal : 10;
            int maxDura = parts.Length > 5 && int.TryParse(parts[5], out int mdVal) ? mdVal : 10;

            list.Add(new RemoteStallShelfItem(slot, makeIndex, name, price, dura, maxDura));
        }

        return list;
    }

    #endregion
}
