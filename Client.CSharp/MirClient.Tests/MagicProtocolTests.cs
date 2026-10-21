using System;
using System.IO;
using System.Text;
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
    public void Test_CreateMagicKeyChangePacket_MatchesOpenMir2Format()
    {
        ushort magicId = 11; // 雷电术
        char key = '0';      // F1 槽位

        var (msg, _, packet) = MagicProtocolManager.CreateMagicKeyChangePacket(magicId, key);

        Assert.Equal(ProtocolConsts.CM_MAGICKEYCHANGE, msg.Ident);
        Assert.Equal(1008, msg.Ident);
        Assert.Equal(magicId, msg.Param);
        Assert.Equal((ushort)key, msg.Tag);
        Assert.StartsWith("#", packet);
        Assert.EndsWith("!", packet);
    }

    [Fact]
    public void Test_ClientMagicData_BinaryRoundtrip()
    {
        // 构建 78 字节的标准 ClientMagic 结构体
        byte[] buffer;
        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write((byte)'1'); // Key = '1' (F2)
            bw.Write((byte)2);   // Level = 2
            bw.Write((byte)0);   // pad
            bw.Write((byte)0);   // pad
            bw.Write(350);       // CurTrain = 350

            // MagicInfo (70 bytes)
            bw.Write((ushort)11); // MagicId = 11 (雷电术)
            byte[] nameBytes = new byte[14];
            Encoding.ASCII.GetBytes("ThunderBolt").CopyTo(nameBytes, 0);
            bw.Write(nameBytes);
            bw.Write((byte)1);   // EffectType
            bw.Write((byte)2);   // Effect
            bw.Write((byte)0);   // pad
            bw.Write((ushort)15); // Spell = 15 MP
            bw.Write((ushort)45); // Power = 45
            bw.Write((byte)17);  // TrainLevel[0]
            bw.Write((byte)20);  // TrainLevel[1]
            bw.Write((byte)23);  // TrainLevel[2]
            bw.Write((byte)26);  // TrainLevel[3]
            bw.Write(200);       // MaxTrain[0]
            bw.Write(500);       // MaxTrain[1]
            bw.Write(1000);      // MaxTrain[2]
            bw.Write(2000);      // MaxTrain[3]
            bw.Write((byte)2);   // TrainLv
            bw.Write((byte)1);   // Job = 1 (法师)
            bw.Write((byte)0);   // pad
            bw.Write((byte)0);   // pad
            bw.Write(1200);      // DelayTime = 1200ms
            bw.Write((byte)2);   // DefSpell
            bw.Write((byte)5);   // DefPower
            bw.Write((ushort)75);// MaxPower = 75
            bw.Write((byte)10);  // DefMaxPower
            byte[] descBytes = new byte[15];
            Encoding.ASCII.GetBytes("ClassicThunder").CopyTo(descBytes, 0);
            bw.Write(descBytes);
            bw.Write((byte)0);   // pad
            bw.Write((byte)0);   // pad
            bw.Write((byte)0);   // pad
            bw.Flush();
            buffer = ms.ToArray();
        }

        using (var msRead = new MemoryStream(buffer))
        using (var br = new BinaryReader(msRead))
        {
            var magic = ClientMagicData.ReadFrom(br);
            Assert.Equal('1', magic.Key);
            Assert.Equal(2, magic.Level);
            Assert.Equal(350, magic.CurTrain);
            Assert.Equal(11, magic.Def.MagicId);
            Assert.Equal("ThunderBolt", magic.Def.MagicName);
            Assert.Equal(15, magic.Def.Spell);
            Assert.Equal(45, magic.Def.Power);
            Assert.Equal(1, magic.Def.Job);
            Assert.Equal(1200, magic.Def.DelayTime);
            Assert.Equal(75, magic.Def.MaxPower);
        }
    }

    [Fact]
    public void Test_CharacterMagicManager_KeyBindingAndLookup()
    {
        var mgr = CharacterMagicManager.Instance;
        mgr.InitDefaultPreset();

        // 默认预设有雷电术(11)、烈火剑法(26)、治愈术(2)、火球术(1)
        var f1Magic = mgr.GetMagicBySlot(0); // F1
        Assert.NotNull(f1Magic);
        Assert.Equal(11, f1Magic.Def.MagicId);

        // 重新绑定 F1 为火墙(22)
        mgr.BindKey(0, 22);
        f1Magic = mgr.GetMagicBySlot(0);
        Assert.NotNull(f1Magic);
        Assert.Equal(22, f1Magic.Def.MagicId);
        Assert.Equal(0, mgr.GetBoundSlot(22));

        // 解绑 F1
        mgr.UnbindSlot(0);
        Assert.Null(mgr.GetMagicBySlot(0));
        Assert.Equal(-1, mgr.GetBoundSlot(22));
    }

    [Fact]
    public void Test_CharacterMagicManager_CooldownLogic()
    {
        var mgr = CharacterMagicManager.Instance;
        double now = 1000.0;

        Assert.True(mgr.CanCast(11, now, out _));

        // 触发雷电术冷却 (1.2 秒)
        mgr.TriggerCooldown(11, now);

        // 0.5 秒后仍在冷却
        Assert.False(mgr.CanCast(11, now + 0.5, out float remain1));
        Assert.True(remain1 > 0f);

        // 1.5 秒后冷却完毕
        Assert.True(mgr.CanCast(11, now + 1.5, out _));
    }
}
