using System;
using MirClient.Protocol;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class MagicProtocolTests
{
    [Fact]
    public void Test_CreateSpellPacket_GeneratesExactDefaultMessageAndFields()
    {
        int magicId = (int)MagicType.Thunder; // 11: 雷电术
        int targetX = 330;
        int targetY = 268;
        int targetRecogId = 8888;

        var (msg, payload, packet) = MagicProtocolManager.CreateSpellPacket(magicId, targetX, targetY, targetRecogId);

        int expectedRecog = (targetX & 0xFFFF) | ((targetY & 0xFFFF) << 16);
        Assert.Equal(ProtocolConsts.CM_SPELL, msg.Ident);
        Assert.Equal(expectedRecog, msg.Recog);
        Assert.Equal((ushort)(targetRecogId & 0xFFFF), msg.Param);
        Assert.Equal(magicId, msg.Tag);
        Assert.Equal((ushort)((targetRecogId >> 16) & 0xFFFF), msg.Series);
        Assert.Empty(payload);
        Assert.StartsWith("#", packet);
        Assert.EndsWith("!", packet);
    }

    [Fact]
    public void Test_CreateSpellPacket_NoTarget_DefaultsTargetRecogIdToZero()
    {
        int magicId = (int)MagicType.Healing; // 2: 治愈术
        int targetX = 150;
        int targetY = 180;

        var (msg, payload, packet) = MagicProtocolManager.CreateSpellPacket(magicId, targetX, targetY);

        int expectedRecog = (targetX & 0xFFFF) | ((targetY & 0xFFFF) << 16);
        Assert.Equal(ProtocolConsts.CM_SPELL, msg.Ident);
        Assert.Equal(expectedRecog, msg.Recog);
        Assert.Equal(0, msg.Param);
        Assert.Equal(magicId, msg.Tag);
        Assert.Equal(0, msg.Series);
        Assert.StartsWith("#", packet);
        Assert.EndsWith("!", packet);
    }

    [Fact]
    public void Test_ParseSpellBroadcast_DecodesHeaderAndPayloadCorrectly()
    {
        int actorId = 10086;
        int targetX = 220;
        int targetY = 340;
        int magicId = (int)MagicType.Fireball; // 1: 火球术
        string targetInfo = "白野猪-01";

        DefaultMessage header = new(
            ident: ProtocolConsts.SM_SPELL,
            recog: actorId,
            param: (ushort)targetX,
            tag: (ushort)targetY,
            series: (ushort)magicId
        );
        string rawBody = EDCode.EncodeString(targetInfo);

        var ev = MagicProtocolManager.ParseSpellBroadcast(header, rawBody);

        Assert.Equal(actorId, ev.ActorId);
        Assert.Equal(targetX, ev.TargetX);
        Assert.Equal(targetY, ev.TargetY);
        Assert.Equal(magicId, ev.MagicId);
        Assert.Equal(targetInfo, ev.TargetInfo);
    }

    [Fact]
    public void Test_ParseMagicFire_DecodesTargetAndDamageCorrectly()
    {
        int targetId = 7788;
        int damage = 185;
        int magicId = (int)MagicType.Thunder;

        DefaultMessage header = new(
            ident: ProtocolConsts.SM_MAGICFIRE,
            recog: targetId,
            param: (ushort)damage,
            tag: (ushort)magicId,
            series: 0
        );

        var ev = MagicProtocolManager.ParseMagicFire(header);

        Assert.Equal(targetId, ev.TargetId);
        Assert.Equal(damage, ev.Damage);
        Assert.Equal(magicId, ev.MagicId);
    }

    [Fact]
    public void Test_GetMagicName_ReturnsCorrectStandardNames()
    {
        Assert.Equal("火球术", MagicProtocolManager.GetMagicName((int)MagicType.Fireball));
        Assert.Equal("治愈术", MagicProtocolManager.GetMagicName((int)MagicType.Healing));
        Assert.Equal("雷电术", MagicProtocolManager.GetMagicName((int)MagicType.Thunder));
        Assert.Equal("刺杀剑术", MagicProtocolManager.GetMagicName((int)MagicType.Assassinate));
        Assert.Equal("灵魂火符", MagicProtocolManager.GetMagicName((int)MagicType.SoulCharm));
        Assert.Equal("火墙", MagicProtocolManager.GetMagicName((int)MagicType.FireWall));
        Assert.Equal("烈火剑法", MagicProtocolManager.GetMagicName((int)MagicType.FireSword));
        Assert.Equal("召唤神兽", MagicProtocolManager.GetMagicName((int)MagicType.SummonShinshu));
        Assert.Equal("冰咆哮", MagicProtocolManager.GetMagicName((int)MagicType.IceRoar));
    }

    [Fact]
    public void Test_MagicType_EnumValues_StrictlyAlignWithOpenMir2()
    {
        Assert.Equal(1, (byte)MagicType.Fireball);
        Assert.Equal(2, (byte)MagicType.Healing);
        Assert.Equal(6, (byte)MagicType.Amyounsul);
        Assert.Equal(11, (byte)MagicType.Thunder);
        Assert.Equal(12, (byte)MagicType.Assassinate);
        Assert.Equal(13, (byte)MagicType.SoulCharm);
        Assert.Equal(22, (byte)MagicType.FireWall);
        Assert.Equal(25, (byte)MagicType.FireSword);
        Assert.Equal(30, (byte)MagicType.SummonShinshu);
        Assert.Equal(31, (byte)MagicType.Shield);
        Assert.Equal(33, (byte)MagicType.IceRoar);
    }

    [Fact]
    public void Test_ProtocolConsts_MagicCommands_StrictlyMatchMessagesCs()
    {
        Assert.Equal(3017, ProtocolConsts.CM_SPELL);
        Assert.Equal(17, ProtocolConsts.SM_SPELL);
        Assert.Equal(638, ProtocolConsts.SM_MAGICFIRE);
        Assert.Equal(639, ProtocolConsts.SM_MAGICFIREFAIL);
        Assert.Equal(210, ProtocolConsts.SM_ADDMAGIC);
        Assert.Equal(211, ProtocolConsts.SM_SENDMYMAGIC);
    }

    [Fact]
    public void Test_SpellBroadcast_RoundtripSerialization()
    {
        var (msg, _, packet) = MagicProtocolManager.CreateSpellPacket(
            (int)MagicType.FireSword, 500, 600, 12345
        );

        Assert.True(packet.StartsWith("#") && packet.EndsWith("!"));
        int expectedRecog = (500 & 0xFFFF) | ((600 & 0xFFFF) << 16);
        Assert.Equal(expectedRecog, msg.Recog);
        Assert.Equal(ProtocolConsts.CM_SPELL, msg.Ident);
        Assert.Equal((ushort)(12345 & 0xFFFF), msg.Param);
        Assert.Equal((ushort)MagicType.FireSword, msg.Tag);
        Assert.Equal((ushort)((12345 >> 16) & 0xFFFF), msg.Series);
    }

    [Fact]
    public void Test_CombatConstants_AlignWithOpenMir2()
    {
        Assert.Equal(14, ProtocolConsts.SM_HIT);
        Assert.Equal(15, ProtocolConsts.SM_HEAVYHIT);
        Assert.Equal(16, ProtocolConsts.SM_BIGHIT);
        Assert.Equal(17, ProtocolConsts.SM_SPELL);
        Assert.Equal(18, ProtocolConsts.SM_POWERHIT);
        Assert.Equal(19, ProtocolConsts.SM_LONGHIT);
        Assert.Equal(24, ProtocolConsts.SM_WIDEHIT);
        Assert.Equal(31, ProtocolConsts.SM_STRUCK);
        Assert.Equal(32, ProtocolConsts.SM_DEATH);
        Assert.Equal(44, ProtocolConsts.SM_WINEXP);
        Assert.Equal(45, ProtocolConsts.SM_LEVELUP);
        Assert.Equal(3014, ProtocolConsts.CM_HIT);
        Assert.Equal(3017, ProtocolConsts.CM_SPELL);
    }
}
