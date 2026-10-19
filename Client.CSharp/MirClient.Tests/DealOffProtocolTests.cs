using System;
using System.Collections.Generic;
using MirClient.Protocol;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class DealOffProtocolTests
{
    [Fact]
    public void Test_CreateAddItemPacket_CorrectIdentAndRecog()
    {
        int makeIndex = 12345;
        string itemName = "裁决之杖";
        var (header, payload) = DealOffProtocolManager.CreateAddItemPacket(makeIndex, itemName);

        Assert.Equal(ProtocolConsts.CM_SELLOFFADDITEM, header.Ident);
        Assert.Equal(12345, header.Recog); // 服务端读取 processMsg.nParam1 = Recog
        Assert.Equal(itemName, EDCode.DecodeText(payload));
    }

    [Fact]
    public void Test_CreateDelItemPacket_CorrectIdentAndRecog()
    {
        int makeIndex = 67890;
        string itemName = "骨玉权杖";
        var (header, payload) = DealOffProtocolManager.CreateDelItemPacket(makeIndex, itemName);

        Assert.Equal(ProtocolConsts.CM_SELLOFFDELITEM, header.Ident);
        Assert.Equal(67890, header.Recog);
        Assert.Equal(itemName, EDCode.DecodeText(payload));
    }

    [Fact]
    public void Test_CreateCancelPacket_CorrectIdent()
    {
        var (header, payload) = DealOffProtocolManager.CreateCancelPacket();

        Assert.Equal(ProtocolConsts.CM_SELLOFFCANCEL, header.Ident);
        Assert.Empty(payload);
    }

    [Fact]
    public void Test_CreateEndPacket_CorrectIdentAndParams()
    {
        int sellGold = 500;
        int gameDiamond = 20;
        string buyer = "沙巴克城主";
        var (header, payload) = DealOffProtocolManager.CreateEndPacket(sellGold, gameDiamond, buyer);

        Assert.Equal(ProtocolConsts.CM_SELLOFFEND, header.Ident);
        Assert.Equal(500, header.Recog);
        Assert.Equal((ushort)20, header.Param);
        Assert.Equal((ushort)short.MaxValue, header.Tag); // short.MaxValue 校验码
        Assert.Equal(buyer, EDCode.DecodeText(payload));
    }

    [Fact]
    public void Test_CreateCancelSellingPacket_CorrectIdent()
    {
        var (header, payload) = DealOffProtocolManager.CreateCancelSellingPacket();

        Assert.Equal(ProtocolConsts.CM_CANCELSELLOFFITEMING, header.Ident);
        Assert.Empty(payload);
    }

    [Fact]
    public void Test_CreateBuyCancelPacket_CorrectIdentAndMsg()
    {
        string seller = "风云行者";
        var (header, payload) = DealOffProtocolManager.CreateBuyCancelPacket(seller);

        Assert.Equal(ProtocolConsts.CM_SELLOFFBUYCANCEL, header.Ident);
        Assert.Equal(seller, EDCode.DecodeText(payload));
    }

    [Fact]
    public void Test_CreateBuyPacket_CorrectIdentAndMsg()
    {
        string seller = "风云行者";
        var (header, payload) = DealOffProtocolManager.CreateBuyPacket(seller);

        Assert.Equal(ProtocolConsts.CM_SELLOFFBUY, header.Ident);
        Assert.Equal(seller, EDCode.DecodeText(payload));
    }

    [Fact]
    public void Test_SerializeAndDeserializeDealOffInfo_RoundTrip()
    {
        var original = new ClientDealOffInfo
        {
            DealChrName = "传奇神豪",
            BuyChrName = "指定买家",
            SellDateTime = 45200.5,
            SellGold = 1888,
            N = 0 // 待售
        };

        original.UseItems[0] = new DealOffItem
        {
            Name = "裁决之杖",
            StdMode = 19,
            Shape = 1,
            Weight = 50,
            Looks = 100,
            Dura = 32,
            DuraMax = 32,
            DC = 3000,
            Price = 100000,
            MakeIndex = 99999,
            Desc = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 }
        };

        byte[] rawBytes = DealOffProtocolManager.SerializeDealOffInfo(original);
        Assert.NotNull(rawBytes);
        Assert.True(rawBytes.Length > 42);

        // 经由 6-bit 编码传输模拟
        string encoded = EDCode.EncodeBytes(rawBytes);
        ClientDealOffInfo? decoded = DealOffProtocolManager.DecodeDealOffInfo(encoded);

        Assert.NotNull(decoded);
        Assert.Equal("传奇神豪", decoded!.DealChrName);
        Assert.Equal("指定买家", decoded.BuyChrName);
        Assert.Equal(1888, decoded.SellGold);
        Assert.Equal(0, decoded.N);

        var firstItem = decoded.UseItems[0];
        Assert.NotNull(firstItem);
        Assert.Equal("裁决之杖", firstItem!.Name);
        Assert.Equal(99999, firstItem.MakeIndex);
        Assert.Equal(32, firstItem.Dura);
        Assert.Equal(3000, firstItem.DC);
    }

    [Fact]
    public void Test_DealOffManager_OpenAndAddItems_Lifecycle()
    {
        var manager = new DealOffManager();
        bool openedFired = false;
        bool closedFired = false;
        bool addedFired = false;

        manager.OnDealOffFormOpened += () => openedFired = true;
        manager.OnDealOffFormClosed += () => closedFired = true;
        manager.OnItemAdded += it => addedFired = true;

        manager.HandleOpenDealOffForm(888);
        Assert.True(manager.IsOpen);
        Assert.Equal(888, manager.NpcActorId);
        Assert.True(openedFired);

        var testItem = new DealOffItem { MakeIndex = 111, Name = "屠龙刀" };
        manager.HandleAddItemSuccess(testItem);
        Assert.Single(manager.SellingItems);
        Assert.True(addedFired);

        // 撤下物品
        manager.HandleDelItemSuccess(111);
        Assert.Empty(manager.SellingItems);

        // 再次添加并完成挂单
        manager.HandleAddItemSuccess(testItem);
        manager.HandleEndSuccess();
        Assert.False(manager.IsOpen);
        Assert.Empty(manager.SellingItems);
        Assert.True(closedFired);
    }

    [Fact]
    public void Test_DealOffManager_MarketListAndBuyFlow()
    {
        var manager = new DealOffManager();
        var deal1 = new ClientDealOffInfo
        {
            DealChrName = "卖家A",
            SellGold = 100,
            SellDateTime = 1000
        };
        var deal2 = new ClientDealOffInfo
        {
            DealChrName = "卖家B",
            SellGold = 200,
            SellDateTime = 1001
        };

        manager.HandleUpdateMarket(deal1);
        manager.HandleUpdateMarket(deal2);
        Assert.Equal(2, manager.MarketList.Count);

        // 购买卖家A的物品
        bool buyFired = false;
        manager.OnBuyItemSuccess += () => buyFired = true;
        manager.HandleBuySuccess("卖家A");

        Assert.True(buyFired);
        Assert.Single(manager.MarketList);
        Assert.Equal("卖家B", manager.MarketList[0].DealChrName);
    }

    [Fact]
    public void Test_DecodeDealOffInfo_HandlesEmptyAndCorruptedInput()
    {
        Assert.Null(DealOffProtocolManager.DecodeDealOffInfo(string.Empty));
        Assert.Null(DealOffProtocolManager.DecodeDealOffInfo("!@@#$%^&*()"));
    }
}
