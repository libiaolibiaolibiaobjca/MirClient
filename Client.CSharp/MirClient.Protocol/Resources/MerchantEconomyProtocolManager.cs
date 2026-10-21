using System;
using System.Collections.Generic;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 商人货架商品信息定义
/// </summary>
public record ClientGoodsItem(string Name, int SubMenu, int Price, int Stock);

/// <summary>
/// 盛大十周年与 OpenMir2 NPC 交互经济、估价、特修与门禁协议管理器
/// 严格 1:1 对齐 OpenMir2/src/BotSrv/Player/RobotPlayer.cs 与 GameSrv/Npc/Merchant.cs
/// </summary>
public static class MerchantEconomyProtocolManager
{
    #region 客户端发包构造 (CM_)

    /// <summary>
    /// 构造购买商品封包 (CM_USERBUYITEM = 1014)
    /// 1:1 对齐 OpenMir2: Recog=merchantId, Param=LoWord(menuIndex), Tag=HiWord(menuIndex), Series=count, Payload=EncodeString(itemName)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateBuyItemPacket(int merchantActorId, int menuIndex, string itemName, ushort count = 1)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_USERBUYITEM,
            Recog = merchantActorId,
            Param = (ushort)(menuIndex & 0xFFFF),
            Tag = (ushort)((menuIndex >> 16) & 0xFFFF),
            Series = count
        };
        return (header, EDCode.EncodeString(itemName ?? string.Empty));
    }

    /// <summary>
    /// 构造向商人出售背包物品封包 (CM_USERSELLITEM = 1013)
    /// 1:1 对齐 OpenMir2: Recog=merchantId, Param=LoWord(makeIndex), Tag=HiWord(makeIndex), Series=count, Payload=EncodeString(itemName)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateSellItemPacket(int merchantActorId, int makeIndex, string itemName, ushort count = 1)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_USERSELLITEM,
            Recog = merchantActorId,
            Param = (ushort)(makeIndex & 0xFFFF),
            Tag = (ushort)((makeIndex >> 16) & 0xFFFF),
            Series = count
        };
        return (header, EDCode.EncodeString(itemName ?? string.Empty));
    }

    /// <summary>
    /// 构造修理装备封包 (CM_USERREPAIRITEM = 1023)
    /// 1:1 对齐 OpenMir2: Recog=merchantId, Param=LoWord(makeIndex), Tag=HiWord(makeIndex), Series=0, Payload=EncodeString(itemName)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateRepairItemPacket(int merchantActorId, int makeIndex, string itemName)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_USERREPAIRITEM,
            Recog = merchantActorId,
            Param = (ushort)(makeIndex & 0xFFFF),
            Tag = (ushort)((makeIndex >> 16) & 0xFFFF),
            Series = 0
        };
        return (header, EDCode.EncodeString(itemName ?? string.Empty));
    }

    /// <summary>
    /// 构造安全小退注销封包 (CM_SOFTCLOSE = 1009)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateSoftCloseGamePacket()
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_SOFTCLOSE,
            Recog = 0,
            Param = 0,
            Tag = 0,
            Series = 0
        };
        return (header, string.Empty);
    }

    /// <summary>
    /// 构造向商人询问背包物品收购价封包 (CM_MERCHANTQUERYSELLPRICE = 1012)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateQuerySellPricePacket(int merchantActorId, int makeIndex, string itemName)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_MERCHANTQUERYSELLPRICE,
            Recog = merchantActorId,
            Param = (ushort)(makeIndex & 0xFFFF),
            Tag = (ushort)((makeIndex >> 16) & 0xFFFF),
            Series = 0
        };
        return (header, EDCode.EncodeString(itemName ?? string.Empty));
    }

    /// <summary>
    /// 构造向铁匠铺商人询问装备修理费用封包 (CM_MERCHANTQUERYREPAIRCOST = 1024)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateQueryRepairCostPacket(int merchantActorId, int makeIndex, string itemName)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_MERCHANTQUERYREPAIRCOST,
            Recog = merchantActorId,
            Param = (ushort)(makeIndex & 0xFFFF),
            Tag = (ushort)((makeIndex >> 16) & 0xFFFF),
            Series = 0
        };
        return (header, EDCode.EncodeString(itemName ?? string.Empty));
    }

    /// <summary>
    /// 构造查询货架商品极品详细属性封包 (CM_USERGETDETAILITEM = 1015)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateGetDetailItemPacket(int merchantActorId, int menuIndex, string itemName)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_USERGETDETAILITEM,
            Recog = merchantActorId,
            Param = (ushort)menuIndex,
            Tag = 0,
            Series = 0
        };
        return (header, EDCode.EncodeString(itemName ?? string.Empty));
    }

    /// <summary>
    /// 构造玩家向地面丢弃金币封包 (CM_DROPGOLD = 1016)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateDropGoldPacket(int amount)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_DROPGOLD,
            Recog = amount,
            Param = 0,
            Tag = 0,
            Series = 0
        };
        return (header, string.Empty);
    }

    /// <summary>
    /// 构造在药店制作药剂封包 (CM_USERMAKEDRUGITEM = 1034)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateMakeDrugPacket(int merchantActorId, string itemName)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_USERMAKEDRUGITEM,
            Recog = merchantActorId,
            Param = 0,
            Tag = 0,
            Series = 0
        };
        return (header, EDCode.EncodeString(itemName ?? string.Empty));
    }

    /// <summary>
    /// 构造装备持久度与有效性校验封包 (CM_CHACKITEM = 9063)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateCheckItemPacket()
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_CHACKITEM,
            Recog = 0,
            Param = 0,
            Tag = 0,
            Series = 0
        };
        return (header, string.Empty);
    }

    /// <summary>
    /// 构造推开大门封包 (CM_OPENDOOR = 1002)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateOpenDoorPacket(int doorId, int x, int y)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_OPENDOOR,
            Recog = doorId,
            Param = (ushort)x,
            Tag = (ushort)y,
            Series = 0
        };
        return (header, string.Empty);
    }

    /// <summary>
    /// 构造关门封包 (CM_SOFTCLOSE = 1009)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateSoftCloseDoorPacket(int doorId, int x, int y)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_SOFTCLOSE,
            Recog = doorId,
            Param = (ushort)x,
            Tag = (ushort)y,
            Series = 0
        };
        return (header, string.Empty);
    }

    /// <summary>
    /// 构造主动刷新背包封包 (CM_QUERYBAGITEMS = 81)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateQueryBagItemsPacket()
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_QUERYBAGITEMS,
            Recog = 0,
            Param = 0,
            Tag = 0,
            Series = 0
        };
        return (header, string.Empty);
    }

    /// <summary>
    /// 构造主动查询角色状态与属性封包 (CM_QUERYUSERSTATE = 82)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateQueryUserStatePacket()
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_QUERYUSERSTATE,
            Recog = 0,
            Param = 0,
            Tag = 0,
            Series = 0
        };
        return (header, string.Empty);
    }

    /// <summary>
    /// 构造主动查询指定角色名封包 (CM_QUERYUSERNAME = 80)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateQueryUserNamePacket(int actorId)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_QUERYUSERNAME,
            Recog = actorId,
            Param = 0,
            Tag = 0,
            Series = 0
        };
        return (header, string.Empty);
    }

    #endregion

    #region 服务端响应解析 (SM_)

    /// <summary>
    /// 解析商人商品货架列表 (SM_SENDGOODSLIST = 645)
    /// 每 4 项为一组: 0:品名, 1:子菜单, 2:售价, 3:库存
    /// </summary>
    public static List<ClientGoodsItem> ParseGoodsList(string rawPayload)
    {
        var list = new List<ClientGoodsItem>();
        if (string.IsNullOrWhiteSpace(rawPayload)) return list;

        string decoded = EDCode.DecodeText(rawPayload);
        string[] parts = decoded.Split('/', StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i + 3 < parts.Length; i += 4)
        {
            string gName = parts[i].Trim();
            _ = int.TryParse(parts[i + 1], out int gSub);
            _ = int.TryParse(parts[i + 2], out int gPrice);
            _ = int.TryParse(parts[i + 3], out int gStock);

            if (!string.IsNullOrEmpty(gName))
            {
                list.Add(new ClientGoodsItem(gName, gSub, gPrice, gStock));
            }
        }
        return list;
    }

    /// <summary>
    /// 解析商人返回的物品收购估价 (SM_SENDBUYPRICE = 647)
    /// </summary>
    public static int ParseBuyPriceResponse(DefaultMessage header)
    {
        return header.Param > 0 ? header.Param : header.Recog;
    }

    /// <summary>
    /// 解析铁匠铺返回的修理费用 (SM_SENDREPAIRCOST = 671)
    /// </summary>
    public static int ParseRepairCostResponse(DefaultMessage header)
    {
        return (short)header.Param != 0 ? (short)header.Param : header.Recog;
    }

    #endregion
}
