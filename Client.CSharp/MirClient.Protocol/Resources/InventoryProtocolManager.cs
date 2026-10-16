using System;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 严格对应 OpenMir2 与经典热血传奇人物装备插槽索引代码 (0~12)
/// </summary>
public enum EquipSlotType : byte
{
    Dress = 0,      // 衣服
    Weapon = 1,     // 武器
    Medal = 2,      // 勋章 / 蜡烛照明
    Necklace = 3,   // 项链
    Helmet = 4,     // 头盔
    ArmringL = 5,   // 左手镯
    ArmringR = 6,   // 右手镯
    RingL = 7,      // 左戒指
    RingR = 8,      // 右戒指
    Amulet = 9,     // 护身符 / 毒药
    Belt = 10,      // 腰带
    Boots = 11,     // 鞋子
    Charm = 12      // 宝石
}

/// <summary>
/// 装备穿脱成功网络回执
/// </summary>
public record EquipActionResult(bool Success, int Where, long Feature, int FeatureEx, string Message);

/// <summary>
/// 物品使用成功网络回执
/// </summary>
public record EatActionResult(bool Success, int ItemMakeIndex, string Message);

/// <summary>
/// OpenMir2 玩家背包物品使用、装备穿脱与同步网络封包协议管理器
/// </summary>
public class InventoryProtocolManager
{
    public static InventoryProtocolManager Instance { get; } = new();

    /// <summary>
    /// 构建客户端使用/消耗物品封包 (CM_EAT 1006)
    /// </summary>
    public static (DefaultMessage Msg, string Payload, string EncodedPacket) CreateEatPacket(int itemMakeIndex, string itemName)
    {
        var msg = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_EAT,
            Recog = itemMakeIndex,
            Param = 0,
            Tag = 0,
            Series = 0
        };

        string encodedHeader = EDCode.EncodeMessage(msg);
        string payload = EDCode.EncodeString(itemName ?? "");
        string packet = $"#{encodedHeader}{payload}!";
        return (msg, payload, packet);
    }

    /// <summary>
    /// 构建客户端穿戴装备封包 (CM_TAKEONITEM 1003)
    /// </summary>
    public static (DefaultMessage Msg, string Payload, string EncodedPacket) CreateTakeOnPacket(int itemMakeIndex, EquipSlotType slot, string itemName)
    {
        var msg = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_TAKEONITEM,
            Recog = itemMakeIndex,
            Param = (ushort)slot,
            Tag = 0,
            Series = 0
        };

        string encodedHeader = EDCode.EncodeMessage(msg);
        string payload = EDCode.EncodeString(itemName ?? "");
        string packet = $"#{encodedHeader}{payload}!";
        return (msg, payload, packet);
    }

    /// <summary>
    /// 构建客户端脱下装备封包 (CM_TAKEOFFITEM 1004)
    /// </summary>
    public static (DefaultMessage Msg, string Payload, string EncodedPacket) CreateTakeOffPacket(int itemMakeIndex, EquipSlotType slot, string itemName)
    {
        var msg = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_TAKEOFFITEM,
            Recog = itemMakeIndex,
            Param = (ushort)slot,
            Tag = 0,
            Series = 0
        };

        string encodedHeader = EDCode.EncodeMessage(msg);
        string payload = EDCode.EncodeString(itemName ?? "");
        string packet = $"#{encodedHeader}{payload}!";
        return (msg, payload, packet);
    }

    /// <summary>
    /// 解析穿戴装备回执 (SM_TAKEON_OK / SM_TAKEON_FAIL)
    /// </summary>
    public static EquipActionResult ParseTakeOnResponse(DefaultMessage header)
    {
        bool success = header.Ident == ProtocolConsts.SM_TAKEON_OK;
        return new EquipActionResult(
            Success: success,
            Where: header.Series,
            Feature: header.Recog,
            FeatureEx: header.Param,
            Message: success ? "装备穿戴成功" : "穿戴失败：等级/负重/职业不满足要求"
        );
    }

    /// <summary>
    /// 解析脱下装备回执 (SM_TAKEOFF_OK / SM_TAKEOFF_FAIL)
    /// </summary>
    public static EquipActionResult ParseTakeOffResponse(DefaultMessage header)
    {
        bool success = header.Ident == ProtocolConsts.SM_TAKEOFF_OK;
        return new EquipActionResult(
            Success: success,
            Where: header.Series,
            Feature: header.Recog,
            FeatureEx: header.Param,
            Message: success ? "装备已成功卸下" : "脱下失败：背包空间已满或装备被锁定"
        );
    }

    /// <summary>
    /// 解析物品使用回执 (SM_EAT_OK / SM_EAT_FAIL)
    /// </summary>
    public static EatActionResult ParseEatResponse(DefaultMessage header)
    {
        bool success = header.Ident == ProtocolConsts.SM_EAT_OK;
        return new EatActionResult(
            Success: success,
            ItemMakeIndex: header.Recog,
            Message: success ? "物品使用成功" : "使用失败：冷却中或无法在此处使用"
        );
    }

    /// <summary>
    /// 解析删除背包物品广播 (SM_DELITEM 202)
    /// </summary>
    public static int ParseDelBagItem(DefaultMessage header)
    {
        return header.Recog;
    }

    /// <summary>
    /// 根据物品类型推导其推荐穿戴插槽
    /// </summary>
    public static EquipSlotType? GetPreferredSlot(Item item, bool isRightHand = false)
    {
        return item.Type switch
        {
            ItemType.Weapon => EquipSlotType.Weapon,
            ItemType.Dress => EquipSlotType.Dress,
            ItemType.Helmet => EquipSlotType.Helmet,
            ItemType.Necklace => EquipSlotType.Necklace,
            ItemType.Armring => isRightHand ? EquipSlotType.ArmringR : EquipSlotType.ArmringL,
            ItemType.Ring => isRightHand ? EquipSlotType.RingR : EquipSlotType.RingL,
            ItemType.Badge => EquipSlotType.Medal,
            _ => null
        };
    }
}
