using System;
using System.Collections.Generic;
using MirClient.Protocol;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class StallProtocolTests
{
    [Fact]
    public void Test_CreateStartStallPacket_EncodesTitle()
    {
        string title = "【神兵利器】天下第一摊";
        var (header, payload) = StallProtocolManager.CreateStartStallPacket(title);

        Assert.Equal(ProtocolConsts.CM_MYSHOPHAM, header.Ident);
        Assert.Equal("【神兵利器】天下第一摊", payload);
    }

    [Fact]
    public void Test_CreateStopStallPacket_ValidIdent()
    {
        var (header, payload) = StallProtocolManager.CreateStopStallPacket();

        Assert.Equal(ProtocolConsts.CM_MYSHOPEXIT, header.Ident);
        Assert.Empty(payload);
    }

    [Fact]
    public void Test_CreateAddItemPacket_EncodesMakeIndexAndPrice()
    {
        int makeIndex = 8848;
        int price = 50000;
        string itemName = "裁决之杖";
        var (header, payload) = StallProtocolManager.CreateAddItemPacket(makeIndex, price, itemName);

        Assert.Equal(ProtocolConsts.CM_SENDSELL, header.Ident);
        Assert.Equal(8848, header.Param);
        Assert.Equal(50000, header.Tag);
        Assert.Equal("裁决之杖", payload);
    }

    [Fact]
    public void Test_CreateBuyItemPacket_EncodesParams()
    {
        int ownerId = 10086;
        int makeIndex = 8848;
        int price = 50000;
        var (header, payload) = StallProtocolManager.CreateBuyItemPacket(ownerId, makeIndex, price);

        Assert.Equal(ProtocolConsts.CM_HAMSHOPBUY, header.Ident);
        Assert.Equal(10086, header.Recog);
        Assert.Equal(8848, header.Param);
        Assert.Equal(50000, header.Tag);
        Assert.Empty(payload);
    }

    [Fact]
    public void Test_CreateOpenOtherStallPacket_EncodesOwnerId()
    {
        int ownerId = 10086;
        string ownerName = "神秘商贾";
        var (header, payload) = StallProtocolManager.CreateOpenOtherStallPacket(ownerId, ownerName);

        Assert.Equal(ProtocolConsts.CM_OPHAMSHOP, header.Ident);
        Assert.Equal(10086, header.Recog);
        Assert.Equal("神秘商贾", payload);
    }

    [Fact]
    public void Test_ParseStartStallResponse_ParsesTitle()
    {
        var header = new DefaultMessage { Ident = ProtocolConsts.SM_MYSHOPHAM };
        string title = StallProtocolManager.ParseStartStallResponse(header, "比奇老字号百货");
        Assert.Equal("比奇老字号百货", title);
    }

    [Fact]
    public void Test_ParseAddItemResponse_ParsesSlotAndPrice()
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.SM_SENDSELL,
            Recog = 2,     // 第 3 槽位 (0-indexed)
            Param = 1024,  // MakeIndex
            Tag = 30000    // Price
        };
        var (slot, makeIndex, price, name) = StallProtocolManager.ParseAddItemResponse(header, "龙纹剑");

        Assert.Equal(2, slot);
        Assert.Equal(1024, makeIndex);
        Assert.Equal(30000, price);
        Assert.Equal("龙纹剑", name);
    }

    [Fact]
    public void Test_ParseBuyerSuccessAndSellerSold_ParsesGoldAndNames()
    {
        // 买家确认
        var buyHeader = new DefaultMessage
        {
            Ident = ProtocolConsts.SM_HAMSHOPBUYA,
            Recog = 60000
        };
        var (cost, buyItem) = StallProtocolManager.ParseBuyerSuccess(buyHeader, "骨玉权杖");
        Assert.Equal(60000, cost);
        Assert.Equal("骨玉权杖", buyItem);

        // 卖家结算
        var sellHeader = new DefaultMessage
        {
            Ident = ProtocolConsts.SM_HAMSHOPBUYB,
            Recog = 60000
        };
        var (earn, sellItem, buyer) = StallProtocolManager.ParseSellerSold(sellHeader, "骨玉权杖/豪爽散人");
        Assert.Equal(60000, earn);
        Assert.Equal("骨玉权杖", sellItem);
        Assert.Equal("豪爽散人", buyer);
    }

    [Fact]
    public void Test_ParseStallType_ParsesCoordinatesAndTitle()
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.SM_HAMSHOPTYPE,
            Recog = 9999
        };
        string payload = "盟重清仓大甩卖/商界巨擘/335/270";
        var record = StallProtocolManager.ParseStallType(header, payload);

        Assert.Equal(9999, record.OwnerActorId);
        Assert.Equal("盟重清仓大甩卖", record.Title);
        Assert.Equal("商界巨擘", record.OwnerName);
        Assert.Equal(335, record.TileX);
        Assert.Equal(270, record.TileY);
    }

    [Fact]
    public void Test_ParseOtherStallShelves_ParsesMultiItems()
    {
        string raw = "0/1001/井中月/20000/25/30\n1/1002/黑铁头盔/15000/10/10";
        var shelves = StallProtocolManager.ParseOtherStallShelves(raw);

        Assert.Equal(2, shelves.Count);
        Assert.Equal("井中月", shelves[0].ItemName);
        Assert.Equal(20000, shelves[0].Price);
        Assert.Equal(25, shelves[0].Dura);
        Assert.Equal(30, shelves[0].MaxDura);

        Assert.Equal("黑铁头盔", shelves[1].ItemName);
        Assert.Equal(15000, shelves[1].Price);
    }

    [Fact]
    public void Test_StallManager_SyncServerIntegrations()
    {
        var sm = StallManager.Instance;
        sm.Reset();

        // 1. 同步开启摊位
        sm.SyncServerStartStall("比奇聚宝阁");
        Assert.True(sm.IsStalling);
        Assert.Equal("比奇聚宝阁", sm.StallTitle);

        // 2. 同步上架商品
        sm.SyncServerAddItem(0, 501, 80000, "力量戒指");
        Assert.Single(sm.Shelves);
        Assert.Equal("力量戒指", sm.Shelves[0].Item.Name);
        Assert.Equal(80000, sm.Shelves[0].Price);

        // 3. 同步售出到账
        sm.SyncServerSoldNotification("力量戒指", 80000, "买家小李");
        Assert.Empty(sm.Shelves);
        Assert.Equal(80000, sm.TotalEarnings);

        // 4. 同步收摊歇业
        sm.SyncServerStopStall("已收摊");
        Assert.False(sm.IsStalling);
    }
}
