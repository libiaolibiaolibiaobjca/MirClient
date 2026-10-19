using System;
using MirClient.Protocol;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class GuildDiplomacyAndSecurityProtocolTests
{
    [Fact]
    public void Test_CreateGuildAllyPacket_CorrectIdent()
    {
        var (header, payload) = GuildDiplomacyAndSecurityProtocolManager.CreateGuildAllyPacket();

        Assert.Equal(ProtocolConsts.CM_GUILDALLY, header.Ident);
        Assert.Empty(payload);
    }

    [Fact]
    public void Test_CreateGuildBreakAllyPacket_EncodesTargetGuild()
    {
        string targetGuild = "邪恶之眼";
        var (header, payload) = GuildDiplomacyAndSecurityProtocolManager.CreateGuildBreakAllyPacket(targetGuild);

        Assert.Equal(ProtocolConsts.CM_GUILDBREAKALLY, header.Ident);
        Assert.Equal(targetGuild, EDCode.DecodeText(payload));
    }

    [Fact]
    public void Test_CreateVerifyPasswordPacket_EncodesPasswordAndSeries()
    {
        string pwd = "mypassword123";
        var (header, payload) = GuildDiplomacyAndSecurityProtocolManager.CreateVerifyPasswordPacket(pwd);

        Assert.Equal(ProtocolConsts.CM_PASSWORD, header.Ident);
        Assert.Equal((ushort)1, header.Series);
        Assert.Equal(pwd, EDCode.DecodeText(payload));
    }

    [Fact]
    public void Test_CreateChangePasswordPacket_EncodesCombinedPasswords()
    {
        string oldPwd = "old123";
        string newPwd = "new456";
        var (header, payload) = GuildDiplomacyAndSecurityProtocolManager.CreateChangePasswordPacket(oldPwd, newPwd);

        Assert.Equal(ProtocolConsts.CM_CHGPASSWORD, header.Ident);
        Assert.Equal((ushort)1, header.Series);
        Assert.Equal("old123/new456", EDCode.DecodeText(payload));
    }

    [Fact]
    public void Test_CreateSetPasswordPacket_EncodesPassword()
    {
        string pwd = "initialpwd";
        var (header, payload) = GuildDiplomacyAndSecurityProtocolManager.CreateSetPasswordPacket(pwd);

        Assert.Equal(ProtocolConsts.CM_SETPASSWORD, header.Ident);
        Assert.Equal((ushort)1, header.Series);
        Assert.Equal(pwd, EDCode.DecodeText(payload));
    }

    [Theory]
    [InlineData(1, 0, true)]
    [InlineData(0, 1, true)]
    [InlineData(0, 0, false)]
    public void Test_ParsePasswordStatus_LockedAndUnlocked(int recog, ushort param, bool expectedLocked)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.SM_PASSWORDSTATUS,
            Recog = recog,
            Param = param
        };
        bool isLocked = GuildDiplomacyAndSecurityProtocolManager.ParsePasswordStatus(header);
        Assert.Equal(expectedLocked, isLocked);
    }

    [Theory]
    [InlineData(-1, "面对")]
    [InlineData(-2, "交战")]
    [InlineData(-3, "掌门")]
    [InlineData(-4, "授权")]
    [InlineData(-99, "结盟失败")]
    public void Test_ParseGuildAllyFail_AllErrorCodes(int errorCode, string expectedKeyword)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.SM_GUILDMAKEALLY_FAIL,
            Recog = errorCode
        };
        var (code, desc) = GuildDiplomacyAndSecurityProtocolManager.ParseGuildAllyFail(header);
        Assert.Equal(errorCode, code);
        Assert.Contains(expectedKeyword, desc);
    }
}
