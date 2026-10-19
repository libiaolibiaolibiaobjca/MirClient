using System;
using MirClient.Protocol;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class MinimapAndSystemProtocolTests
{
    [Fact]
    public void Test_CreateWantMinimapPacket_CorrectIdent()
    {
        var (header, payload) = MinimapAndSystemProtocolManager.CreateWantMinimapPacket();

        Assert.Equal(ProtocolConsts.CM_WANTMINIMAP, header.Ident);
        Assert.Empty(payload);
    }

    [Fact]
    public void Test_ParseMinimapResponse_ReadsMinMapId()
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.SM_READMINIMAP_OK,
            Param = 102
        };
        int minMapId = MinimapAndSystemProtocolManager.ParseMinimapResponse(header);
        Assert.Equal(102, minMapId);
    }

    [Fact]
    public void Test_CreateQueryValueResponsePacket_EncodesNpcAndValue()
    {
        int npcId = 8008;
        string value = "盟重省传送";
        var (header, payload) = MinimapAndSystemProtocolManager.CreateQueryValueResponsePacket(npcId, value);

        Assert.Equal(ProtocolConsts.CM_QUERYVAL, header.Ident);
        Assert.Equal(8008, header.Recog);
        Assert.Equal(value, EDCode.DecodeText(payload));
    }

    [Fact]
    public void Test_CreateMagicKeyChangePacket_EncodesMagicAndSlot()
    {
        ushort magicId = 25; // 烈火剑法
        byte keySlot = 3;    // F4
        var (header, payload) = MinimapAndSystemProtocolManager.CreateMagicKeyChangePacket(magicId, keySlot);

        Assert.Equal(ProtocolConsts.CM_MAGICKEYCHANGE, header.Ident);
        Assert.Equal((ushort)25, header.Param);
        Assert.Equal((ushort)3, header.Tag);
        Assert.Empty(payload);
    }

    [Fact]
    public void Test_CreateQueryUserSetPacket_CorrectIdent()
    {
        var (header, payload) = MinimapAndSystemProtocolManager.CreateQueryUserSetPacket();

        Assert.Equal(ProtocolConsts.CM_QUERYUSERSET, header.Ident);
        Assert.Empty(payload);
    }

    [Fact]
    public void Test_CreateAdjustBonusPacket_EncodesBonusTypeAndCount()
    {
        byte bonusType = 1; // 力量/攻击
        ushort count = 5;   // 加 5 点
        var (header, payload) = MinimapAndSystemProtocolManager.CreateAdjustBonusPacket(bonusType, count);

        Assert.Equal(ProtocolConsts.CM_ADJUST_BONUS, header.Ident);
        Assert.Equal((ushort)1, header.Param);
        Assert.Equal((ushort)5, header.Tag);
        Assert.Empty(payload);
    }

    [Fact]
    public void Test_CreateLoginNoticeOkPacket_CorrectIdent()
    {
        var (header, payload) = MinimapAndSystemProtocolManager.CreateLoginNoticeOkPacket();

        Assert.Equal(ProtocolConsts.CM_LOGINNOTICEOK, header.Ident);
        Assert.Empty(payload);
    }

    [Fact]
    public void Test_CreateCheckTimePacket_EncodesTick()
    {
        int tick = 998877;
        var (header, payload) = MinimapAndSystemProtocolManager.CreateCheckTimePacket(tick);

        Assert.Equal(ProtocolConsts.CM_CHECKTIME, header.Ident);
        Assert.Equal(998877, header.Recog);
        Assert.Empty(payload);
    }
}
