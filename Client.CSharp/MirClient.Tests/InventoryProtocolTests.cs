using System;
using MirClient.Protocol;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class InventoryProtocolTests
{
    [Fact]
    public void Test_CreateEatPacket_GeneratesExactDefaultMessageAndPayload()
    {
        int itemMakeIndex = 9527;
        string itemName = "强效太阳水";

        var (msg, payload, packet) = InventoryProtocolManager.CreateEatPacket(itemMakeIndex, itemName);

        Assert.Equal(ProtocolConsts.CM_EAT, msg.Ident);
        Assert.Equal(itemMakeIndex, msg.Recog);
        Assert.Equal(0, msg.Param);
        Assert.Equal(0, msg.Tag);
        Assert.Equal(0, msg.Series);

        string decoded = EDCode.DecodeText(payload);
        Assert.Equal(itemName, decoded);
        Assert.StartsWith("#", packet);
        Assert.EndsWith("!", packet);
    }

    [Fact]
    public void Test_CreateTakeOnPacket_GeneratesExactDefaultMessageAndSlot()
    {
        int itemMakeIndex = 12345;
        EquipSlotType slot = EquipSlotType.Weapon;
        string itemName = "裁决之杖";

        var (msg, payload, packet) = InventoryProtocolManager.CreateTakeOnPacket(itemMakeIndex, slot, itemName);

        Assert.Equal(ProtocolConsts.CM_TAKEONITEM, msg.Ident);
        Assert.Equal(itemMakeIndex, msg.Recog);
        Assert.Equal((ushort)EquipSlotType.Weapon, msg.Param);
        Assert.Equal(0, msg.Tag);
        Assert.Equal(0, msg.Series);

        string decoded = EDCode.DecodeText(payload);
        Assert.Equal(itemName, decoded);
        Assert.StartsWith("#", packet);
        Assert.EndsWith("!", packet);
    }

    [Fact]
    public void Test_CreateTakeOffPacket_GeneratesExactDefaultMessageAndSlot()
    {
        int itemMakeIndex = 67890;
        EquipSlotType slot = EquipSlotType.Dress;
        string itemName = "战神盔甲(男)";

        var (msg, payload, packet) = InventoryProtocolManager.CreateTakeOffPacket(itemMakeIndex, slot, itemName);

        Assert.Equal(ProtocolConsts.CM_TAKEOFFITEM, msg.Ident);
        Assert.Equal(itemMakeIndex, msg.Recog);
        Assert.Equal((ushort)EquipSlotType.Dress, msg.Param);
        Assert.Equal(0, msg.Tag);
        Assert.Equal(0, msg.Series);

        string decoded = EDCode.DecodeText(payload);
        Assert.Equal(itemName, decoded);
        Assert.StartsWith("#", packet);
        Assert.EndsWith("!", packet);
    }

    [Fact]
    public void Test_ParseTakeOnResponse_SuccessAndFail()
    {
        // 成功包
        DefaultMessage okHeader = new(
            ident: ProtocolConsts.SM_TAKEON_OK,
            recog: 10001, // Feature
            param: 2,     // FeatureEx
            tag: 0,
            series: (ushort)EquipSlotType.Weapon
        );
        var okRes = InventoryProtocolManager.ParseTakeOnResponse(okHeader);
        Assert.True(okRes.Success);
        Assert.Equal(10001, okRes.Feature);
        Assert.Equal(2, okRes.FeatureEx);
        Assert.Equal((int)EquipSlotType.Weapon, okRes.Where);

        // 失败包
        DefaultMessage failHeader = new(
            ident: ProtocolConsts.SM_TAKEON_FAIL,
            recog: 0,
            param: 0,
            tag: 0,
            series: (ushort)EquipSlotType.Weapon
        );
        var failRes = InventoryProtocolManager.ParseTakeOnResponse(failHeader);
        Assert.False(failRes.Success);
        Assert.Contains("失败", failRes.Message);
    }

    [Fact]
    public void Test_ParseTakeOffResponse_SuccessAndFail()
    {
        // 成功包
        DefaultMessage okHeader = new(
            ident: ProtocolConsts.SM_TAKEOFF_OK,
            recog: 20002,
            param: 1,
            tag: 0,
            series: (ushort)EquipSlotType.Dress
        );
        var okRes = InventoryProtocolManager.ParseTakeOffResponse(okHeader);
        Assert.True(okRes.Success);
        Assert.Equal(20002, okRes.Feature);
        Assert.Equal(1, okRes.FeatureEx);
        Assert.Equal((int)EquipSlotType.Dress, okRes.Where);

        // 失败包
        DefaultMessage failHeader = new(
            ident: ProtocolConsts.SM_TAKEOFF_FAIL,
            recog: 0,
            param: 0,
            tag: 0,
            series: (ushort)EquipSlotType.Dress
        );
        var failRes = InventoryProtocolManager.ParseTakeOffResponse(failHeader);
        Assert.False(failRes.Success);
        Assert.Contains("失败", failRes.Message);
    }

    [Fact]
    public void Test_ParseEatResponse_SuccessAndFail()
    {
        DefaultMessage okHeader = new(
            ident: ProtocolConsts.SM_EAT_OK,
            recog: 8888,
            param: 0,
            tag: 0,
            series: 0
        );
        var okRes = InventoryProtocolManager.ParseEatResponse(okHeader);
        Assert.True(okRes.Success);
        Assert.Equal(8888, okRes.ItemMakeIndex);

        DefaultMessage failHeader = new(
            ident: ProtocolConsts.SM_EAT_FAIL,
            recog: 8888,
            param: 0,
            tag: 0,
            series: 0
        );
        var failRes = InventoryProtocolManager.ParseEatResponse(failHeader);
        Assert.False(failRes.Success);
        Assert.Contains("失败", failRes.Message);
    }

    [Fact]
    public void Test_ParseDelBagItem_ExtractsMakeIndex()
    {
        DefaultMessage delHeader = new(
            ident: ProtocolConsts.SM_DELITEM,
            recog: 77777,
            param: 0,
            tag: 0,
            series: 0
        );

        int makeIndex = InventoryProtocolManager.ParseDelBagItem(delHeader);
        Assert.Equal(77777, makeIndex);
    }

    [Fact]
    public void Test_GetPreferredSlot_MapsItemTypesAccurately()
    {
        var sword = Item.CreateByName("木剑");
        Assert.Equal(EquipSlotType.Weapon, InventoryProtocolManager.GetPreferredSlot(sword));

        var armor = Item.CreateByName("布衣(男)");
        Assert.Equal(EquipSlotType.Dress, InventoryProtocolManager.GetPreferredSlot(armor));

        var helmet = new Item { Name = "骷髅头盔", Type = ItemType.Helmet };
        Assert.Equal(EquipSlotType.Helmet, InventoryProtocolManager.GetPreferredSlot(helmet));

        var necklace = new Item { Name = "幽灵项链", Type = ItemType.Necklace };
        Assert.Equal(EquipSlotType.Necklace, InventoryProtocolManager.GetPreferredSlot(necklace));

        var bracelet = new Item { Name = "死神手套", Type = ItemType.Armring };
        Assert.Equal(EquipSlotType.ArmringL, InventoryProtocolManager.GetPreferredSlot(bracelet, isRightHand: false));
        Assert.Equal(EquipSlotType.ArmringR, InventoryProtocolManager.GetPreferredSlot(bracelet, isRightHand: true));

        var ring = new Item { Name = "珊瑚戒指", Type = ItemType.Ring };
        Assert.Equal(EquipSlotType.RingL, InventoryProtocolManager.GetPreferredSlot(ring, isRightHand: false));
        Assert.Equal(EquipSlotType.RingR, InventoryProtocolManager.GetPreferredSlot(ring, isRightHand: true));

        var badge = new Item { Name = "荣誉勋章43号", Type = ItemType.Badge };
        Assert.Equal(EquipSlotType.Medal, InventoryProtocolManager.GetPreferredSlot(badge));

        var potion = Item.CreateByName("强效太阳水");
        Assert.Null(InventoryProtocolManager.GetPreferredSlot(potion));
    }

    [Fact]
    public void Test_QueryBagItems_ConstantAlignWithOpenMir2()
    {
        Assert.Equal(81, ProtocolConsts.CM_QUERYBAGITEMS);
        Assert.Equal(201, ProtocolConsts.SM_BAGITEMS);
        Assert.Equal(615, ProtocolConsts.SM_TAKEON_OK);
        Assert.Equal(616, ProtocolConsts.SM_TAKEON_FAIL);
        Assert.Equal(619, ProtocolConsts.SM_TAKEOFF_OK);
        Assert.Equal(620, ProtocolConsts.SM_TAKEOFF_FAIL);
        Assert.Equal(635, ProtocolConsts.SM_EAT_OK);
        Assert.Equal(636, ProtocolConsts.SM_EAT_FAIL);
        Assert.Equal(600, ProtocolConsts.SM_DROPITEM_SUCCESS);
        Assert.Equal(601, ProtocolConsts.SM_DROPITEM_FAIL);
        Assert.Equal(1001, ProtocolConsts.CM_PICKUP);
    }
}
