using System;
using System.IO;
using System.Text;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 客户端单项寄售商品精简/完整信息 (严格对齐 OpenMir2 ClientItem 结构体)
/// </summary>
public class DealOffItem
{
    public string Name { get; set; } = string.Empty;
    public byte StdMode { get; set; }
    public byte Shape { get; set; }
    public byte Weight { get; set; }
    public ushort Looks { get; set; }
    public ushort Dura { get; set; }
    public ushort DuraMax { get; set; }
    public ushort AC { get; set; }
    public ushort MAC { get; set; }
    public ushort DC { get; set; }
    public ushort MC { get; set; }
    public ushort SC { get; set; }
    public int Price { get; set; }
    public int MakeIndex { get; set; }
    public byte[] Desc { get; set; } = new byte[14];
}

/// <summary>
/// 客户端元宝寄售全服挂牌单据 (严格对齐 OpenMir2 ClientDealOffInfo 结构体)
/// </summary>
public class ClientDealOffInfo
{
    public string DealChrName { get; set; } = string.Empty; // 寄售人角色名 (固定15字节)
    public string BuyChrName { get; set; } = string.Empty;  // 购买人角色名 (固定15字节)
    public double SellDateTime { get; set; }                // 挂单时间戳 (Delphi TDateTime / double)
    public int SellGold { get; set; }                       // 标价元宝数
    public DealOffItem?[] UseItems { get; set; } = new DealOffItem?[9]; // 9个在售物品槽位
    public byte N { get; set; }                             // 状态标识 (0:待售, 1:已售未结, 2:完成, 3:买家取消, 4:卖家下架)
}

/// <summary>
/// 盛大十周年与 OpenMir2 庄园全服元宝寄售行网络协议管理器
/// 严格 1:1 对齐 OpenMir2/src/OpenMir2/Messages.cs 与 PlayObject.Sale.cs 语义
/// </summary>
public static class DealOffProtocolManager
{
    private const int ItemNameLen = 14;

    #region 客户端上行发包构造 (CM_)

    /// <summary>
    /// 构造往寄售出售窗口放入物品封包 (CM_SELLOFFADDITEM = 23002)
    /// 服务端使用: processMsg.nParam1 为 MakeIndex, processMsg.Msg 为物品名
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateAddItemPacket(int makeIndex, string itemName)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_SELLOFFADDITEM,
            Recog = makeIndex, // 映射到服务端的 processMsg.nParam1
            Param = 0,
            Tag = 0,
            Series = 0
        };
        string name = itemName ?? string.Empty;
        return (header, EDCode.EncodeString(name));
    }

    /// <summary>
    /// 构造从寄售出售窗口撤回物品封包 (CM_SELLOFFDELITEM = 23007)
    /// 服务端使用: processMsg.nParam1 为 MakeIndex, processMsg.Msg 为物品名
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateDelItemPacket(int makeIndex, string itemName)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_SELLOFFDELITEM,
            Recog = makeIndex, // 映射到服务端的 processMsg.nParam1
            Param = 0,
            Tag = 0,
            Series = 0
        };
        string name = itemName ?? string.Empty;
        return (header, EDCode.EncodeString(name));
    }

    /// <summary>
    /// 构造客户端取消元宝寄售封包 (CM_SELLOFFCANCEL = 23012)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateCancelPacket()
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_SELLOFFCANCEL,
            Recog = 0,
            Param = 0,
            Tag = 0,
            Series = 0
        };
        return (header, string.Empty);
    }

    /// <summary>
    /// 构造客户端元宝寄售确认挂单封包 (CM_SELLOFFEND = 23015)
    /// 服务端使用: nParam1=sellGold, nParam2=gameDiamond, nParam3=short.MaxValue, Msg=buyChrName
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateEndPacket(int sellGold, int gameDiamond = 0, string buyChrName = "")
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_SELLOFFEND,
            Recog = sellGold,              // processMsg.nParam1: 挂牌标价元宝数
            Param = (ushort)gameDiamond,   // processMsg.nParam2: 附带金刚石数
            Tag = (ushort)short.MaxValue,  // processMsg.nParam3: short.MaxValue 校验码
            Series = 0
        };
        string targetBuyer = buyChrName ?? string.Empty;
        return (header, EDCode.EncodeString(targetBuyer));
    }

    /// <summary>
    /// 构造出售人取消正在出售中的寄售单封包 (CM_CANCELSELLOFFITEMING = 23024)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateCancelSellingPacket()
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_CANCELSELLOFFITEMING,
            Recog = 0,
            Param = 0,
            Tag = 0,
            Series = 0
        };
        return (header, string.Empty);
    }

    /// <summary>
    /// 构造购买人取消交易封包 (CM_SELLOFFBUYCANCEL = 23025)
    /// 服务端使用: processMsg.Msg 为出售人角色名
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateBuyCancelPacket(string sellerName)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_SELLOFFBUYCANCEL,
            Recog = 0,
            Param = 0,
            Tag = 0,
            Series = 0
        };
        string seller = sellerName ?? string.Empty;
        return (header, EDCode.EncodeString(seller));
    }

    /// <summary>
    /// 构造购买人确认购买寄售物品封包 (CM_SELLOFFBUY = 23026)
    /// 服务端使用: processMsg.Msg 为出售人角色名
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateBuyPacket(string sellerName)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_SELLOFFBUY,
            Recog = 0,
            Param = 0,
            Tag = 0,
            Series = 0
        };
        string seller = sellerName ?? string.Empty;
        return (header, EDCode.EncodeString(seller));
    }

    #endregion

    #region 二进制与 EDCode 解码与序列化 (ClientDealOffInfo)

    /// <summary>
    /// 将二进制字节缓冲序列化为 OpenMir2 ClientDealOffInfo 原始字节流
    /// </summary>
    public static byte[] SerializeDealOffInfo(ClientDealOffInfo info)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // 1. DealChrName (15 bytes)
        byte[] dealNameBytes = new byte[15];
        byte[] rawDeal = EDCode.GBK.GetBytes(info.DealChrName ?? string.Empty);
        Array.Copy(rawDeal, dealNameBytes, Math.Min(rawDeal.Length, 15));
        writer.Write(dealNameBytes);

        // 2. BuyChrName (15 bytes)
        byte[] buyNameBytes = new byte[15];
        byte[] rawBuy = EDCode.GBK.GetBytes(info.BuyChrName ?? string.Empty);
        Array.Copy(rawBuy, buyNameBytes, Math.Min(rawBuy.Length, 15));
        writer.Write(buyNameBytes);

        // 3. SellDateTime (8 bytes double)
        writer.Write(info.SellDateTime);

        // 4. SellGold (4 bytes int)
        writer.Write(info.SellGold);

        // 5. 9个 UseItems (每个 102 bytes)
        byte[] nullItemBuff = new byte[102];
        int itemCount = Math.Min(info.UseItems.Length, 9);
        for (int i = 0; i < 9; i++)
        {
            if (i < itemCount && info.UseItems[i] != null)
            {
                SerializeItem(writer, info.UseItems[i]!);
            }
            else
            {
                writer.Write(nullItemBuff);
            }
        }

        // 6. N (1 byte 状态标识)
        writer.Write(info.N);

        return ms.ToArray();
    }

    private static void SerializeItem(BinaryWriter writer, DealOffItem item)
    {
        // Name (14 bytes)
        byte[] nameBytes = new byte[ItemNameLen];
        byte[] raw = EDCode.GBK.GetBytes(item.Name ?? string.Empty);
        Array.Copy(raw, nameBytes, Math.Min(raw.Length, ItemNameLen));
        writer.Write(nameBytes);

        writer.Write(item.StdMode);      // 1
        writer.Write(item.Shape);        // 1
        writer.Write(item.Weight);       // 1
        writer.Write((byte)0);           // AniCount
        writer.Write((sbyte)0);          // SpecialPwr
        writer.Write((byte)0);           // ItemDesc
        writer.Write((byte)0);           // pad
        writer.Write(item.Looks);        // 2
        writer.Write(item.DuraMax);      // 2
        writer.Write(item.AC);           // 2
        writer.Write(item.MAC);          // 2
        writer.Write(item.DC);           // 2
        writer.Write(item.MC);           // 2
        writer.Write(item.SC);           // 2
        writer.Write((byte)0);           // Need
        writer.Write((byte)0);           // NeedLevel
        writer.Write((byte)0);           // NeedIdentify
        writer.Write((byte)0);           // pad
        writer.Write(item.Price);        // 4
        writer.Write(0);                 // Stock
        writer.Write((byte)0);           // AtkSpd
        writer.Write((byte)0);           // Agility
        writer.Write((byte)0);           // Accurate
        writer.Write((byte)0);           // MgAvoid
        writer.Write((byte)0);           // Strong
        writer.Write((byte)0);           // Undead
        writer.Write((byte)0);           // pad
        writer.Write((byte)0);           // pad
        writer.Write(0);                 // HpAdd
        writer.Write(0);                 // MpAdd
        writer.Write(0);                 // ExpAdd
        writer.Write((byte)0);           // EffType1
        writer.Write((byte)0);           // EffRate1
        writer.Write((byte)0);           // EffValue1
        writer.Write((byte)0);           // EffType2
        writer.Write((byte)0);           // EffRate2
        writer.Write((byte)0);           // EffValue2
        writer.Write((byte)0);           // Slowdown
        writer.Write((byte)0);           // Tox
        writer.Write((byte)0);           // ToxAvoid
        writer.Write((byte)0);           // UniqueItem
        writer.Write((byte)0);           // OverlapItem
        writer.Write((byte)0);           // Light
        writer.Write((byte)0);           // ItemType
        writer.Write((byte)0);           // pad
        writer.Write((ushort)0);         // ItemSet
        writer.Write(new byte[ItemNameLen]); // Reference (14)
        writer.Write((byte)0);           // pad
        writer.Write(item.MakeIndex);    // 4
        writer.Write(item.Dura);         // 2
        writer.Write(item.DuraMax);      // 2
        byte[] desc = item.Desc ?? new byte[14];
        if (desc.Length != 14)
        {
            byte[] fixedDesc = new byte[14];
            Array.Copy(desc, fixedDesc, Math.Min(desc.Length, 14));
            desc = fixedDesc;
        }
        writer.Write(desc);              // 14
        writer.Write((byte)0);           // pad
        writer.Write((byte)0);           // pad
    }

    /// <summary>
    /// 解码来自服务端的 SM_QUERYYBSELL 或 SM_QUERYYBDEAL 下发数据
    /// 支持直接字节流、EDCode 解密及 Base64 字符串
    /// </summary>
    public static ClientDealOffInfo? DecodeDealOffInfo(string encodedPayload)
    {
        if (string.IsNullOrWhiteSpace(encodedPayload)) return null;

        byte[] rawBytes;
        try
        {
            rawBytes = EDCode.DecodeString(encodedPayload);
        }
        catch
        {
            try
            {
                rawBytes = Convert.FromBase64String(encodedPayload);
            }
            catch
            {
                return null;
            }
        }

        return DecodeDealOffInfo(rawBytes);
    }

    /// <summary>
    /// 从原始字节流解析 ClientDealOffInfo
    /// </summary>
    public static ClientDealOffInfo? DecodeDealOffInfo(ReadOnlySpan<byte> span)
    {
        // 头部: 15 + 15 + 8 + 4 = 42 bytes
        if (span.Length < 43) return null;

        var info = new ClientDealOffInfo();
        using var ms = new MemoryStream(span.ToArray());
        using var reader = new BinaryReader(ms);

        byte[] dealNameBytes = reader.ReadBytes(15);
        info.DealChrName = EDCode.GBK.GetString(dealNameBytes).TrimEnd('\0', ' ');

        byte[] buyNameBytes = reader.ReadBytes(15);
        info.BuyChrName = EDCode.GBK.GetString(buyNameBytes).TrimEnd('\0', ' ');

        info.SellDateTime = reader.ReadDouble();
        info.SellGold = reader.ReadInt32();

        for (int i = 0; i < 9; i++)
        {
            if (ms.Position + 102 <= ms.Length)
            {
                var item = DeserializeItem(reader);
                if (item != null && (item.MakeIndex > 0 || !string.IsNullOrEmpty(item.Name)))
                {
                    info.UseItems[i] = item;
                }
            }
        }

        if (ms.Position < ms.Length)
        {
            info.N = reader.ReadByte();
        }

        return info;
    }

    private static DealOffItem? DeserializeItem(BinaryReader reader)
    {
        byte[] nameBytes = reader.ReadBytes(ItemNameLen);
        string name = EDCode.GBK.GetString(nameBytes).TrimEnd('\0', ' ');

        byte stdMode = reader.ReadByte();
        byte shape = reader.ReadByte();
        byte weight = reader.ReadByte();
        reader.ReadByte(); // AniCount
        reader.ReadSByte(); // SpecialPwr
        reader.ReadByte(); // ItemDesc
        reader.ReadByte(); // pad

        ushort looks = reader.ReadUInt16();
        ushort duraMax = reader.ReadUInt16();
        ushort ac = reader.ReadUInt16();
        ushort mac = reader.ReadUInt16();
        ushort dc = reader.ReadUInt16();
        ushort mc = reader.ReadUInt16();
        ushort sc = reader.ReadUInt16();

        reader.ReadByte(); // Need
        reader.ReadByte(); // NeedLevel
        reader.ReadByte(); // NeedIdentify
        reader.ReadByte(); // pad

        int price = reader.ReadInt32();
        reader.ReadInt32(); // Stock

        reader.ReadByte(); // AtkSpd
        reader.ReadByte(); // Agility
        reader.ReadByte(); // Accurate
        reader.ReadByte(); // MgAvoid
        reader.ReadByte(); // Strong
        reader.ReadByte(); // Undead
        reader.ReadByte(); // pad
        reader.ReadByte(); // pad

        reader.ReadInt32(); // HpAdd
        reader.ReadInt32(); // MpAdd
        reader.ReadInt32(); // ExpAdd

        reader.ReadByte(); // EffType1
        reader.ReadByte(); // EffRate1
        reader.ReadByte(); // EffValue1
        reader.ReadByte(); // EffType2
        reader.ReadByte(); // EffRate2
        reader.ReadByte(); // EffValue2
        reader.ReadByte(); // Slowdown
        reader.ReadByte(); // Tox
        reader.ReadByte(); // ToxAvoid
        reader.ReadByte(); // UniqueItem
        reader.ReadByte(); // OverlapItem
        reader.ReadByte(); // Light
        reader.ReadByte(); // ItemType
        reader.ReadByte(); // pad

        reader.ReadUInt16(); // ItemSet
        reader.ReadBytes(ItemNameLen); // Reference
        reader.ReadByte(); // pad

        int makeIndex = reader.ReadInt32();
        ushort dura = reader.ReadUInt16();
        ushort itemDuraMax2 = reader.ReadUInt16();
        byte[] desc = reader.ReadBytes(14);
        reader.ReadByte(); // pad
        reader.ReadByte(); // pad

        if (string.IsNullOrEmpty(name) && makeIndex <= 0)
        {
            return null;
        }

        return new DealOffItem
        {
            Name = name,
            StdMode = stdMode,
            Shape = shape,
            Weight = weight,
            Looks = looks,
            Dura = dura,
            DuraMax = duraMax > 0 ? duraMax : itemDuraMax2,
            AC = ac,
            MAC = mac,
            DC = dc,
            MC = mc,
            SC = sc,
            Price = price,
            MakeIndex = makeIndex,
            Desc = desc
        };
    }

    #endregion
}
