using System;
using System.Numerics;
using MirClient.Protocol;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class GroundItemProtocolTests
{
    public GroundItemProtocolTests()
    {
        GroundItemProtocolManager.Instance.Clear();
    }

    [Fact]
    public void Test_CreatePickupPacket_GeneratesExactDefaultMessage()
    {
        // 构造拾取坐标 (330, 268)
        var (msg, payload, packet) = GroundItemProtocolManager.CreatePickupPacket(330, 268);

        Assert.Equal(ProtocolConsts.CM_PICKUP, msg.Ident);
        Assert.Equal(0, msg.Recog);
        Assert.Equal(330, msg.Param);
        Assert.Equal(268, msg.Tag);
        Assert.Equal(0, msg.Series);
        Assert.Empty(payload);
        Assert.StartsWith("#", packet);
        Assert.EndsWith("!", packet);
    }

    [Fact]
    public void Test_CreateDropItemPacket_GeneratesExactDefaultMessageAndEncodedPayload()
    {
        int itemMakeIndex = 12345;
        int dropCount = 3;
        string itemName = "裁决之杖";

        var (msg, payload, packet) = GroundItemProtocolManager.CreateDropItemPacket(itemMakeIndex, dropCount, itemName);

        Assert.Equal(ProtocolConsts.CM_DROPITEM, msg.Ident);
        Assert.Equal(itemMakeIndex, msg.Recog);
        Assert.Equal(dropCount, msg.Param);
        Assert.Equal(0, msg.Tag);
        Assert.Equal(0, msg.Series);

        string decoded = EDCode.DecodeText(payload);
        Assert.Equal(itemName, decoded);
        Assert.StartsWith("#", packet);
        Assert.EndsWith("!", packet);
    }

    [Fact]
    public void Test_ParseItemShow_DecodesHeaderAndItemNameCorrectly()
    {
        int groundId = 8848;
        int tileX = 250;
        int tileY = 320;
        int looks = 15;
        string itemName = "骨玉权杖";

        DefaultMessage header = new(
            ident: ProtocolConsts.SM_ITEMSHOW,
            recog: groundId,
            param: (ushort)tileX,
            tag: (ushort)tileY,
            series: (ushort)looks
        );
        string payload = EDCode.EncodeString(itemName);

        var ev = GroundItemProtocolManager.ParseItemShow(header, payload);

        Assert.Equal(groundId, ev.GroundId);
        Assert.Equal(tileX, ev.TileX);
        Assert.Equal(tileY, ev.TileY);
        Assert.Equal(looks, ev.Looks);
        Assert.Equal(itemName, ev.ItemName);
    }

    [Fact]
    public void Test_ParseItemHide_DecodesGroundIdAndPositionCorrectly()
    {
        int groundId = 9999;
        int tileX = 250;
        int tileY = 320;

        DefaultMessage header = new(
            ident: ProtocolConsts.SM_ITEMHIDE,
            recog: groundId,
            param: (ushort)tileX,
            tag: (ushort)tileY,
            series: 0
        );

        var ev = GroundItemProtocolManager.ParseItemHide(header);

        Assert.Equal(groundId, ev.GroundId);
        Assert.Equal(tileX, ev.TileX);
        Assert.Equal(tileY, ev.TileY);
    }

    [Fact]
    public void Test_SyncShowItem_And_SyncHideItem_InManager()
    {
        var mgr = GroundItemProtocolManager.Instance;
        var showEv = new GroundItemShowEvent(1001, 100, 200, 10, "屠龙");

        mgr.SyncShowItem(showEv);
        Assert.True(mgr.Items.ContainsKey(1001));
        Assert.Equal("屠龙", mgr.Items[1001].ItemName);

        var hideEv = new GroundItemHideEvent(1001, 100, 200);
        mgr.SyncHideItem(hideEv);
        Assert.False(mgr.Items.ContainsKey(1001));
    }

    [Fact]
    public void Test_CanPickup_CooldownAndDistanceCheck()
    {
        var mgr = GroundItemProtocolManager.Instance;
        long now = 10000;

        // 首次拾取
        Assert.True(mgr.CanPickup(300, 300, now));

        // 立即同坐标重复拾取 (小于 350ms 冷却) -> 应当防抖拦截
        Assert.False(mgr.CanPickup(300, 300, now + 100));

        // 换不同坐标拾取 -> 应当允许
        Assert.True(mgr.CanPickup(301, 300, now + 100));

        // 冷却时间达到后同坐标再次拾取 -> 应当允许
        Assert.True(mgr.CanPickup(300, 300, now + 400));
    }

    [Fact]
    public void Test_ItemCreateByName_FactoryCreatesCorrectItem()
    {
        var goldItem = Item.CreateByName("金币", 888);
        Assert.Equal(ItemType.Misc, goldItem.Type);
        Assert.Equal("金币", goldItem.Name);
        Assert.Equal(888, goldItem.Count);

        var weaponItem = Item.CreateByName("裁决之杖");
        Assert.Equal(ItemType.Weapon, weaponItem.Type);
        Assert.Equal("裁决之杖", weaponItem.Name);

        var potionItem = Item.CreateByName("强效太阳水");
        Assert.Equal(ItemType.Potion, potionItem.Type);
        Assert.Equal("强效太阳水", potionItem.Name);
    }

    [Fact]
    public void Test_GroundItemManager_RemoveById_RemovesItemFromList()
    {
        var groundMgr = new GroundItemManager();
        var item1 = Item.CreateByName("金币", 100);
        var item2 = Item.CreateByName("祝福油");

        groundMgr.SpawnItem(new Vector2(10, 10), item1, customId: 777);
        groundMgr.SpawnItem(new Vector2(10, 11), item2, customId: 888);

        Assert.Equal(2, groundMgr.Items.Count);

        bool removed = groundMgr.RemoveById(777);
        Assert.True(removed);
        Assert.Single(groundMgr.Items);
        Assert.Equal(888, groundMgr.Items[0].Id);

        bool removeNonExist = groundMgr.RemoveById(999);
        Assert.False(removeNonExist);
        Assert.Single(groundMgr.Items);
    }
}
