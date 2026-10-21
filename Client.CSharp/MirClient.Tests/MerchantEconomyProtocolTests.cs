using System;
using MirClient.Protocol;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class MerchantEconomyProtocolTests
{
    [Fact]
    public void Test_CreateQuerySellPricePacket_EncodesParamsAndName()
    {
        int merchantId = 2001;
        int makeIndex = 0x12345678;
        string itemName = "裁决之杖";

        var (header, payload) = MerchantEconomyProtocolManager.CreateQuerySellPricePacket(merchantId, makeIndex, itemName);

        Assert.Equal(ProtocolConsts.CM_MERCHANTQUERYSELLPRICE, header.Ident);
        Assert.Equal(2001, header.Recog);
        Assert.Equal((ushort)0x5678, header.Param);
        Assert.Equal((ushort)0x1234, header.Tag);
        Assert.Equal(itemName, EDCode.DecodeText(payload));
    }

    [Fact]
    public void Test_CreateQueryRepairCostPacket_EncodesParamsAndName()
    {
        int merchantId = 2002;
        int makeIndex = 0x00010002;
        string itemName = "骨玉权杖";

        var (header, payload) = MerchantEconomyProtocolManager.CreateQueryRepairCostPacket(merchantId, makeIndex, itemName);

        Assert.Equal(ProtocolConsts.CM_MERCHANTQUERYREPAIRCOST, header.Ident);
        Assert.Equal(2002, header.Recog);
        Assert.Equal((ushort)2, header.Param);
        Assert.Equal((ushort)1, header.Tag);
        Assert.Equal(itemName, EDCode.DecodeText(payload));
    }

    [Fact]
    public void Test_CreateGetDetailItemPacket_EncodesParamsAndName()
    {
        int merchantId = 2003;
        int menuIndex = 5;
        string itemName = "幽灵手套";

        var (header, payload) = MerchantEconomyProtocolManager.CreateGetDetailItemPacket(merchantId, menuIndex, itemName);

        Assert.Equal(ProtocolConsts.CM_USERGETDETAILITEM, header.Ident);
        Assert.Equal(2003, header.Recog);
        Assert.Equal((ushort)5, header.Param);
        Assert.Equal(itemName, EDCode.DecodeText(payload));
    }

    [Fact]
    public void Test_CreateDropGoldPacket_EncodesAmount()
    {
        int amount = 50000;
        var (header, payload) = MerchantEconomyProtocolManager.CreateDropGoldPacket(amount);

        Assert.Equal(ProtocolConsts.CM_DROPGOLD, header.Ident);
        Assert.Equal(50000, header.Recog);
        Assert.Empty(payload);
    }

    [Fact]
    public void Test_CreateMakeDrugPacket_EncodesFormulaName()
    {
        int merchantId = 2004;
        string drugName = "强效太阳水";
        var (header, payload) = MerchantEconomyProtocolManager.CreateMakeDrugPacket(merchantId, drugName);

        Assert.Equal(ProtocolConsts.CM_USERMAKEDRUGITEM, header.Ident);
        Assert.Equal(2004, header.Recog);
        Assert.Equal(drugName, EDCode.DecodeText(payload));
    }

    [Fact]
    public void Test_CreateCheckItemPacket_CorrectIdent()
    {
        var (header, payload) = MerchantEconomyProtocolManager.CreateCheckItemPacket();

        Assert.Equal(ProtocolConsts.CM_CHACKITEM, header.Ident);
        Assert.Empty(payload);
    }

    [Fact]
    public void Test_CreateOpenDoorPacket_EncodesCoords()
    {
        int doorId = 12;
        int x = 320;
        int y = 280;
        var (header, payload) = MerchantEconomyProtocolManager.CreateOpenDoorPacket(doorId, x, y);

        Assert.Equal(ProtocolConsts.CM_OPENDOOR, header.Ident);
        Assert.Equal(12, header.Recog);
        Assert.Equal((ushort)320, header.Param);
        Assert.Equal((ushort)280, header.Tag);
        Assert.Empty(payload);
    }

    [Fact]
    public void Test_CreateSoftCloseDoorPacket_EncodesCoords()
    {
        int doorId = 12;
        int x = 320;
        int y = 280;
        var (header, payload) = MerchantEconomyProtocolManager.CreateSoftCloseDoorPacket(doorId, x, y);

        Assert.Equal(ProtocolConsts.CM_SOFTCLOSE, header.Ident);
        Assert.Equal(12, header.Recog);
        Assert.Equal((ushort)320, header.Param);
        Assert.Equal((ushort)280, header.Tag);
        Assert.Empty(payload);
    }

    [Fact]
    public void Test_CreateQueryPackets_CorrectIdents()
    {
        var (bagHeader, _) = MerchantEconomyProtocolManager.CreateQueryBagItemsPacket();
        Assert.Equal(ProtocolConsts.CM_QUERYBAGITEMS, bagHeader.Ident);

        var (stateHeader, _) = MerchantEconomyProtocolManager.CreateQueryUserStatePacket();
        Assert.Equal(ProtocolConsts.CM_QUERYUSERSTATE, stateHeader.Ident);

        var (nameHeader, _) = MerchantEconomyProtocolManager.CreateQueryUserNamePacket(8888);
        Assert.Equal(ProtocolConsts.CM_QUERYUSERNAME, nameHeader.Ident);
        Assert.Equal(8888, nameHeader.Recog);
    }

    [Fact]
    public void Test_ParseBuyPriceResponse_ReadsCorrectPrice()
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.SM_SENDBUYPRICE,
            Param = 12800
        };
        int price = MerchantEconomyProtocolManager.ParseBuyPriceResponse(header);
        Assert.Equal(12800, price);
    }

    [Fact]
    public void Test_ParseRepairCostResponse_ReadsCostAndNegativeOne()
    {
        var header1 = new DefaultMessage
        {
            Ident = ProtocolConsts.SM_SENDREPAIRCOST,
            Param = 3500
        };
        Assert.Equal(3500, MerchantEconomyProtocolManager.ParseRepairCostResponse(header1));

        var header2 = new DefaultMessage
        {
            Ident = ProtocolConsts.SM_SENDREPAIRCOST,
            Param = unchecked((ushort)-1)
        };
        Assert.Equal(-1, MerchantEconomyProtocolManager.ParseRepairCostResponse(header2));
    }

    [Fact]
    public void Test_ParseGoodsList_ParsesValidGoodsPayload()
    {
        // 4个字段一组: 品名/子菜单/价格/库存/
        string payload = EDCode.EncodeString("金创药(小量)/0/88/100/魔法药(小量)/1/88/100/强效太阳水/2/550/50/");
        var list = MerchantEconomyProtocolManager.ParseGoodsList(payload);

        Assert.Equal(3, list.Count);
        Assert.Equal("金创药(小量)", list[0].Name);
        Assert.Equal(88, list[0].Price);
        Assert.Equal(100, list[0].Stock);

        Assert.Equal("强效太阳水", list[2].Name);
        Assert.Equal(550, list[2].Price);
        Assert.Equal(50, list[2].Stock);
    }

    [Fact]
    public void Test_CreateBuyAndSellItemPackets_MatchOpenMir2Format()
    {
        int merchantId = 8801;
        int menuIdx = 3;
        string itemName = "随机传送卷";

        var (buyHeader, buyPayload) = MerchantEconomyProtocolManager.CreateBuyItemPacket(merchantId, menuIdx, itemName, 5);
        Assert.Equal(ProtocolConsts.CM_USERBUYITEM, buyHeader.Ident);
        Assert.Equal(1014, buyHeader.Ident);
        Assert.Equal(merchantId, buyHeader.Recog);
        Assert.Equal(menuIdx, buyHeader.Param);
        Assert.Equal(5, buyHeader.Series);
        Assert.Equal(itemName, EDCode.DecodeText(buyPayload));

        int makeIdx = 0x00020005;
        var (sellHeader, sellPayload) = MerchantEconomyProtocolManager.CreateSellItemPacket(merchantId, makeIdx, "乌木剑", 1);
        Assert.Equal(ProtocolConsts.CM_USERSELLITEM, sellHeader.Ident);
        Assert.Equal(1013, sellHeader.Ident);
        Assert.Equal(merchantId, sellHeader.Recog);
        Assert.Equal((ushort)5, sellHeader.Param);
        Assert.Equal((ushort)2, sellHeader.Tag);
    }

    [Fact]
    public void Test_ShopManager_SetGoodsAndMode()
    {
        var mgr = ShopManager.Instance;
        mgr.InitDefaultGoods();
        Assert.NotEmpty(mgr.GetGoods());

        var newGoods = new System.Collections.Generic.List<ClientGoodsItem>
        {
            new ClientGoodsItem("屠龙", 0, 50000000, 1)
        };
        mgr.SetGoodsList(9901, newGoods);

        Assert.True(mgr.IsOpen);
        Assert.Equal(9901, mgr.CurrentMerchantId);
        Assert.Single(mgr.GetGoods());
        Assert.Equal("屠龙", mgr.GetGoods()[0].Name);
    }

    [Fact]
    public void Test_CreateSoftCloseGamePacket_MatchesOpenMir2Ident()
    {
        var (header, payload) = MerchantEconomyProtocolManager.CreateSoftCloseGamePacket();
        Assert.Equal(ProtocolConsts.CM_SOFTCLOSE, header.Ident);
        Assert.Equal(1009, header.Ident);
        Assert.Empty(payload);
    }
}
