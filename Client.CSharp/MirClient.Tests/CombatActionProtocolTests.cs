using System;
using MirClient.Protocol;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class CombatActionProtocolTests
{
    [Fact]
    public void Test_CreateSitDownPacket_EncodesPositionAndDir()
    {
        int x = 330;
        int y = 260;
        int dir = 4;
        var (header, payload) = CombatActionProtocolManager.CreateSitDownPacket(x, y, dir);

        Assert.Equal(ProtocolConsts.CM_SITDOWN, header.Ident);
        Assert.Equal((ushort)4, header.Tag);
        Assert.Empty(payload);

        var (parsedX, parsedY, parsedDir) = CombatActionProtocolManager.ParseActionPacket(header);
        Assert.Equal(330, parsedX);
        Assert.Equal(260, parsedY);
        Assert.Equal(4, parsedDir);
    }

    [Fact]
    public void Test_CreateHorseRunPacket_EncodesTargetAndDir()
    {
        int targetX = 350;
        int targetY = 320;
        int dir = 2;
        var (header, payload) = CombatActionProtocolManager.CreateHorseRunPacket(targetX, targetY, dir);

        Assert.Equal(ProtocolConsts.CM_HORSERUN, header.Ident);
        Assert.Equal((ushort)2, header.Tag);
        Assert.Empty(payload);

        var (parsedX, parsedY, parsedDir) = CombatActionProtocolManager.ParseActionPacket(header);
        Assert.Equal(350, parsedX);
        Assert.Equal(320, parsedY);
        Assert.Equal(2, parsedDir);
    }

    [Fact]
    public void Test_CreateHitPacket_EncodesTargetAndDir()
    {
        var (header, _) = CombatActionProtocolManager.CreateHitPacket(100, 100, 0);
        Assert.Equal(ProtocolConsts.CM_HIT_OPENMIR2, header.Ident);
        Assert.Equal((ushort)0, header.Tag);

        var (x, y, dir) = CombatActionProtocolManager.ParseActionPacket(header);
        Assert.Equal(100, x);
        Assert.Equal(100, y);
        Assert.Equal(0, dir);
    }

    [Fact]
    public void Test_CreateHeavyHitPacket_EncodesTargetAndDir()
    {
        var (header, _) = CombatActionProtocolManager.CreateHeavyHitPacket(150, 160, 1);
        Assert.Equal(ProtocolConsts.CM_HEAVYHIT, header.Ident);
        Assert.Equal((ushort)1, header.Tag);

        var (x, y, dir) = CombatActionProtocolManager.ParseActionPacket(header);
        Assert.Equal(150, x);
        Assert.Equal(160, y);
        Assert.Equal(1, dir);
    }

    [Fact]
    public void Test_CreateBigHitPacket_EncodesTargetAndDir()
    {
        var (header, _) = CombatActionProtocolManager.CreateBigHitPacket(152, 162, 3);
        Assert.Equal(ProtocolConsts.CM_BIGHIT, header.Ident);
        Assert.Equal((ushort)3, header.Tag);

        var (x, y, dir) = CombatActionProtocolManager.ParseActionPacket(header);
        Assert.Equal(152, x);
        Assert.Equal(162, y);
        Assert.Equal(3, dir);
    }

    [Fact]
    public void Test_CreatePowerHitPacket_EncodesTargetAndDir()
    {
        var (header, _) = CombatActionProtocolManager.CreatePowerHitPacket(200, 200, 5);
        Assert.Equal(ProtocolConsts.CM_POWERHIT, header.Ident);
        Assert.Equal((ushort)5, header.Tag);

        var (x, y, dir) = CombatActionProtocolManager.ParseActionPacket(header);
        Assert.Equal(200, x);
        Assert.Equal(200, y);
        Assert.Equal(5, dir);
    }

    [Fact]
    public void Test_CreateLongHitPacket_EncodesTargetAndDir()
    {
        var (header, _) = CombatActionProtocolManager.CreateLongHitPacket(205, 205, 6);
        Assert.Equal(ProtocolConsts.CM_LONGHIT, header.Ident);
        Assert.Equal((ushort)6, header.Tag);

        var (x, y, dir) = CombatActionProtocolManager.ParseActionPacket(header);
        Assert.Equal(205, x);
        Assert.Equal(205, y);
        Assert.Equal(6, dir);
    }

    [Fact]
    public void Test_CreateWideHitPacket_EncodesTargetAndDir()
    {
        var (header, _) = CombatActionProtocolManager.CreateWideHitPacket(210, 210, 7);
        Assert.Equal(ProtocolConsts.CM_WIDEHIT, header.Ident);
        Assert.Equal((ushort)7, header.Tag);

        var (x, y, dir) = CombatActionProtocolManager.ParseActionPacket(header);
        Assert.Equal(210, x);
        Assert.Equal(210, y);
        Assert.Equal(7, dir);
    }

    [Fact]
    public void Test_CreateFireHitPacket_EncodesTargetAndDir()
    {
        var (header, _) = CombatActionProtocolManager.CreateFireHitPacket(300, 300, 4);
        Assert.Equal(ProtocolConsts.CM_FIREHIT, header.Ident);
        Assert.Equal((ushort)4, header.Tag);

        var (x, y, dir) = CombatActionProtocolManager.ParseActionPacket(header);
        Assert.Equal(300, x);
        Assert.Equal(300, y);
        Assert.Equal(4, dir);
    }

    [Fact]
    public void Test_CreateCrsHitPacket_EncodesTargetAndDir()
    {
        var (header, _) = CombatActionProtocolManager.CreateCrsHitPacket(400, 400, 0);
        Assert.Equal(ProtocolConsts.CM_CRSHIT, header.Ident);
        Assert.Equal((ushort)0, header.Tag);

        var (x, y, dir) = CombatActionProtocolManager.ParseActionPacket(header);
        Assert.Equal(400, x);
        Assert.Equal(400, y);
        Assert.Equal(0, dir);
    }

    [Fact]
    public void Test_CreateTwinHitPacket_EncodesTargetAndDir()
    {
        var (header, _) = CombatActionProtocolManager.CreateTwinHitPacket(450, 450, 2);
        Assert.Equal(ProtocolConsts.CM_TWINHIT, header.Ident);
        Assert.Equal((ushort)2, header.Tag);

        var (x, y, dir) = CombatActionProtocolManager.ParseActionPacket(header);
        Assert.Equal(450, x);
        Assert.Equal(450, y);
        Assert.Equal(2, dir);
    }

    [Theory]
    [InlineData(CombatSkillType.HeavyHit, ProtocolConsts.CM_HEAVYHIT)]
    [InlineData(CombatSkillType.BigHit, ProtocolConsts.CM_BIGHIT)]
    [InlineData(CombatSkillType.PowerHit, ProtocolConsts.CM_POWERHIT)]
    [InlineData(CombatSkillType.LongHit, ProtocolConsts.CM_LONGHIT)]
    [InlineData(CombatSkillType.WideHit, ProtocolConsts.CM_WIDEHIT)]
    [InlineData(CombatSkillType.FireHit, ProtocolConsts.CM_FIREHIT)]
    [InlineData(CombatSkillType.CrsHit, ProtocolConsts.CM_CRSHIT)]
    [InlineData(CombatSkillType.TwinHit, ProtocolConsts.CM_TWINHIT)]
    [InlineData(CombatSkillType.NormalHit, ProtocolConsts.CM_HIT_OPENMIR2)]
    public void Test_CreateCombatSkillPacket_DispatchesCorrectly(CombatSkillType skillType, ushort expectedIdent)
    {
        var (header, _) = CombatActionProtocolManager.CreateCombatSkillPacket(skillType, 120, 130, 3);
        Assert.Equal(expectedIdent, header.Ident);
        Assert.Equal((ushort)3, header.Tag);

        var (x, y, dir) = CombatActionProtocolManager.ParseActionPacket(header);
        Assert.Equal(120, x);
        Assert.Equal(130, y);
        Assert.Equal(3, dir);
    }

    [Fact]
    public void Test_ParseActionPacket_BoundaryCoordinates()
    {
        int x = 1000;
        int y = 1000;
        int dir = 7;
        var (header, _) = CombatActionProtocolManager.CreateActionPacket(ProtocolConsts.CM_HIT_OPENMIR2, x, y, dir);
        var (parsedX, parsedY, parsedDir) = CombatActionProtocolManager.ParseActionPacket(header);

        Assert.Equal(1000, parsedX);
        Assert.Equal(1000, parsedY);
        Assert.Equal(7, parsedDir);
    }
}
