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
}
