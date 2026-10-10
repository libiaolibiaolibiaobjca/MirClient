using System.Text;
using MirClient.Protocol;
using Xunit;

namespace MirClient.Tests;

public class EDCodeTests
{
    [Fact]
    public void EncodeAndDecode_RoundTrip_ShouldMatchOriginalBytes()
    {
        // 测试随机数据往返
        byte[] original = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C };
        string encoded = EDCode.EncodeBytes(original);

        Assert.Equal(16, encoded.Length); // 12字节 -> 16字符

        byte[] decoded = EDCode.DecodeString(encoded);
        Assert.Equal(original, decoded);
    }

    [Fact]
    public void EncodeMessage_ShouldOutputExactly16Chars()
    {
        DefaultMessage msg = new DefaultMessage(
            ident: 1001,
            recog: 123456,
            param: 330,
            tag: 270,
            series: 3
        );

        string encoded = EDCode.EncodeMessage(msg);
        Assert.Equal(16, encoded.Length);

        DefaultMessage decoded = EDCode.DecodeMessage(encoded);
        Assert.Equal(msg.Ident, decoded.Ident);
        Assert.Equal(msg.Recog, decoded.Recog);
        Assert.Equal(msg.Param, decoded.Param);
        Assert.Equal(msg.Tag, decoded.Tag);
        Assert.Equal(msg.Series, decoded.Series);
    }

    [Fact]
    public void MakePacket_ShouldFormatCorrectlyWithDelimiters()
    {
        DefaultMessage msg = new DefaultMessage(ident: 100);
        string packet = EDCode.MakePacket(msg, "TestPayload");

        Assert.StartsWith("#", packet);
        Assert.EndsWith("!", packet);
        Assert.Contains("TestPayload", packet);
        Assert.Equal(1 + 16 + "TestPayload".Length + 1, packet.Length);
    }
}
