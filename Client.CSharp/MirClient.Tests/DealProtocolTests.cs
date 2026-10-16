using System;
using MirClient.Protocol;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class DealProtocolTests
{
    [Fact]
    public void Test_CreateDealTryPacket_EncodesTargetName()
    {
        string target = "昔日法神";
        var (header, payload) = DealProtocolManager.CreateDealTryPacket(target);

        Assert.Equal(ProtocolConsts.CM_DEALTRY, header.Ident);
        string decoded = EDCode.DecodeText(payload);
        Assert.Equal("昔日法神", decoded);
    }

    [Fact]
    public void Test_CreateDealAddItemPacket_EncodesMakeIndexAndName()
    {
        int makeIndex = 88888;
        string itemName = "裁决之杖";

        var (header, payload) = DealProtocolManager.CreateDealAddItemPacket(makeIndex, itemName);

        Assert.Equal(ProtocolConsts.CM_DEALADDITEM, header.Ident);
        int reconstructedIdx = header.Param | (header.Tag << 16);
        Assert.Equal(makeIndex, reconstructedIdx);
        Assert.Equal("裁决之杖", EDCode.DecodeText(payload));
    }

    [Fact]
    public void Test_CreateDealDelItemPacket_EncodesMakeIndexAndName()
    {
        int makeIndex = 99999;
        string itemName = "骨玉权杖";

        var (header, payload) = DealProtocolManager.CreateDealDelItemPacket(makeIndex, itemName);

        Assert.Equal(ProtocolConsts.CM_DEALDELITEM, header.Ident);
        int reconstructedIdx = header.Param | (header.Tag << 16);
        Assert.Equal(makeIndex, reconstructedIdx);
        Assert.Equal("骨玉权杖", EDCode.DecodeText(payload));
    }

    [Fact]
    public void Test_CreateDealChgGoldPacket_SetsGoldAmount()
    {
        int gold = 200000;
        var (header, _) = DealProtocolManager.CreateDealChgGoldPacket(gold);

        Assert.Equal(ProtocolConsts.CM_DEALCHGGOLD, header.Ident);
        Assert.Equal(gold, header.Recog);
    }

    [Fact]
    public void Test_CreateDealEndAndCancelPackets()
    {
        var (endHeader, _) = DealProtocolManager.CreateDealEndPacket();
        Assert.Equal(ProtocolConsts.CM_DEALEND, endHeader.Ident);

        var (cancelHeader, _) = DealProtocolManager.CreateDealCancelPacket();
        Assert.Equal(ProtocolConsts.CM_DEALCANCEL, cancelHeader.Ident);
    }

    [Fact]
    public void Test_ParseDealMenu_DecodesTargetName()
    {
        DefaultMessage header = new(
            ident: ProtocolConsts.SM_DEALMENU,
            recog: 0,
            param: 0,
            tag: 0,
            series: 0);

        string rawText = "小虾米";
        string payload = EDCode.EncodeString(rawText);

        string target = DealProtocolManager.ParseDealMenu(header, payload);
        Assert.Equal("小虾米", target);
    }

    [Fact]
    public void Test_ParseDealGold_ExtractsGoldAmount()
    {
        DefaultMessage header = new(
            ident: ProtocolConsts.SM_DEALCHGGOLD_OK,
            recog: 666888,
            param: 0,
            tag: 0,
            series: 0);

        int gold = DealProtocolManager.ParseDealGold(header);
        Assert.Equal(666888, gold);
    }

    [Fact]
    public void Test_DealManager_LifecycleAndLock()
    {
        var dm = DealManager.Instance;
        dm.Reset();

        Assert.False(dm.IsDealing);
        Assert.Equal(DealState.None, dm.State);

        // 1. 开启交易
        dm.StartDeal("无极道人");
        Assert.True(dm.IsDealing);
        Assert.Equal("无极道人", dm.RemoteTargetName);
        Assert.Equal(DealState.Trading, dm.State);

        // 2. 放入本地物品与金币
        Assert.True(dm.AddLocalItem(1001, "屠龙"));
        dm.SetLocalGold(50000);
        Assert.Single(dm.LocalItems);
        Assert.Equal(50000, dm.LocalGold);

        // 3. 接收对方放入物品与金币
        dm.AddRemoteItem(2001, "嗜魂法杖");
        dm.SetRemoteGold(100000);
        Assert.Single(dm.RemoteItems);
        Assert.Equal(100000, dm.RemoteGold);

        // 4. 锁定确认
        dm.SetLocalLock(true);
        Assert.True(dm.LocalLocked);
        Assert.Equal(DealState.Locked, dm.State);

        // 锁定后不可再放入或撤回本地物品
        Assert.False(dm.AddLocalItem(1002, "麻痹戒指"));

        // 5. 达成交易
        dm.CompleteDeal();
        Assert.Equal(DealState.Success, dm.State);
        Assert.False(dm.IsDealing);
    }
}
