using MirClient.Protocol;
using MirClient.Protocol.Network;
using Xunit;

namespace MirClient.Tests;

public class NetworkTests
{
    [Fact]
    public void TestLoginPacket_EncodeAndDecode()
    {
        string account = "mir2_hero";
        string password = "secret_password";

        DefaultMessage msg = new(ProtocolConsts.CM_IDPASSWORD, 0, 0, 0, 0);
        string payload = EDCode.EncodeString($"{account}/{password}");

        // 构造网关传输封包
        string packet = $"#1{EDCode.EncodeMessage(msg)}{payload}!";
        Assert.StartsWith("#1", packet);
        Assert.EndsWith("!", packet);

        // 模拟解密
        int start = 2; // 跳过 #1
        string encHeader = packet.Substring(start, 16);
        string encPayload = packet.Substring(start + 16, packet.Length - start - 17);

        DefaultMessage decodedMsg = EDCode.DecodeMessage(encHeader);
        Assert.Equal(ProtocolConsts.CM_IDPASSWORD, decodedMsg.Ident);

        string decodedPayload = EDCode.DecodeText(encPayload);
        Assert.Equal($"{account}/{password}", decodedPayload);
    }

    [Fact]
    public void TestSelectChrPacket_EncodeAndDecode()
    {
        string account = "mir2_hero";
        string chrName = "逐鹿天下";

        DefaultMessage msg = new(ProtocolConsts.CM_SELCHR, 0, 0, 0, 0);
        string payload = EDCode.EncodeString($"{account}/{chrName}");

        string packet = $"#1{EDCode.EncodeMessage(msg)}{payload}!";

        int start = 2;
        string encHeader = packet.Substring(start, 16);
        string encPayload = packet.Substring(start + 16, packet.Length - start - 17);

        DefaultMessage decodedMsg = EDCode.DecodeMessage(encHeader);
        Assert.Equal(ProtocolConsts.CM_SELCHR, decodedMsg.Ident);

        string decodedPayload = EDCode.DecodeText(encPayload);
        Assert.Equal($"{account}/{chrName}", decodedPayload);
    }

    [Fact]
    public void TestActualQueryChrPayload()
    {
        string payload = "^o=r`a?k`_Yh_`X^_`?_ccPf`_Z`_a?_a`?g`_H";
        string decoded = EDCode.DecodeText(payload);
        Assert.Contains("gm01", decoded);
    }

    [Fact]
    public void TestGameHandshakePacket_Format()
    {
        string handshake = "**mir2_hero/逐鹿天下/888888/10/10";
        string encoded = EDCode.EncodeString(handshake);
        string fullPacket = $"#1{encoded}!";

        Assert.StartsWith("#1", fullPacket);
        Assert.EndsWith("!", fullPacket);

        string body = fullPacket.Substring(2, fullPacket.Length - 3);
        string decoded = EDCode.DecodeText(body);
        Assert.Equal(handshake, decoded);
    }

    [Fact]
    public void TestServerSpurnPacket_ParseActorEntity()
    {
        // 模拟服务端下发生成怪物实体 (ID: 10086, X: 330, Y: 270, Dir: 4, 钉耙猫)
        DefaultMessage msg = new(
            ident: ProtocolConsts.SM_SPURN,
            recog: 10086,
            param: 330,
            tag: 270,
            series: 4
        );
        string payload = EDCode.EncodeString("钉耙猫/1/100/100"); // 名称/Appr/HP/MaxHP
        string packet = $"#1{EDCode.EncodeMessage(msg)}{payload}!";

        int start = 2;
        DefaultMessage decoded = EDCode.DecodeMessage(packet.Substring(start, 16));
        string decPayload = EDCode.DecodeText(packet.Substring(start + 16, packet.Length - start - 17));

        Assert.Equal(ProtocolConsts.SM_SPURN, decoded.Ident);
        Assert.Equal(10086, decoded.Recog);
        Assert.Equal(330, decoded.Param);
        Assert.Equal(270, decoded.Tag);
        Assert.Equal(4, decoded.Series);

        var parts = decPayload.Split('/');
        Assert.Equal("钉耙猫", parts[0]);
        Assert.Equal("100", parts[2]);
    }

    [Fact]
    public void TestServerWalkAndTurnPacket_UpdateEntityState()
    {
        // 模拟实体从 330, 270 走到 331, 271，方向 2
        DefaultMessage walkMsg = new(
            ident: ProtocolConsts.SM_WALK,
            recog: 10086,
            param: 331,
            tag: 271,
            series: 2
        );
        string encoded = EDCode.EncodeMessage(walkMsg);
        DefaultMessage decoded = EDCode.DecodeMessage(encoded);

        Assert.Equal(ProtocolConsts.SM_WALK, decoded.Ident);
        Assert.Equal(10086, decoded.Recog);
        Assert.Equal(331, decoded.Param);
        Assert.Equal(271, decoded.Tag);
        Assert.Equal(2, decoded.Series);
    }

    [Fact]
    public void TestServerStruckPacket_DamageAndDeathFlow()
    {
        // 模拟受击：Recog 10086 受到 35 点伤害，剩余血量 65，上限 100
        DefaultMessage struckMsg = new(
            ident: ProtocolConsts.SM_STRUCK,
            recog: 10086,
            param: 65,  // 当前血量
            tag: 100,  // 最大血量
            series: 35 // 本次掉血量
        );
        string encStruck = EDCode.EncodeMessage(struckMsg);
        DefaultMessage decStruck = EDCode.DecodeMessage(encStruck);

        Assert.Equal(ProtocolConsts.SM_STRUCK, decStruck.Ident);
        Assert.Equal(10086, decStruck.Recog);
        Assert.Equal(65, decStruck.Param);
        Assert.Equal(35, decStruck.Series);
    }
}
