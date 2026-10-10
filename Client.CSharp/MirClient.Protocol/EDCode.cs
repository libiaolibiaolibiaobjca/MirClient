using System.Runtime.InteropServices;
using System.Text;

namespace MirClient.Protocol;

/// <summary>
/// 严格对应 Delphi EDcode.pas 的 6-Bit 编码与解码实现
/// </summary>
public static class EDCode
{
    private const byte Seed = 0xAC;
    private const byte Base = 0x3C; // '<' (ASCII 60)

    static EDCode()
    {
        // 注册 GBK (代码页 936) 编码提供程序
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// 获取 GBK 编码（传奇网络通信强制编码）
    /// </summary>
    public static Encoding GBK => Encoding.GetEncoding(936);

    /// <summary>
    /// 将字节数组编码为 6-bit ASCII 字符串 (对应 EncodeBuf)
    /// </summary>
    public static string EncodeBytes(ReadOnlySpan<byte> src)
    {
        if (src.IsEmpty) return string.Empty;

        int len = src.Length;
        int maxDstLen = (len + 2) / 3 * 4 + 4;
        Span<byte> dst = stackalloc byte[maxDstLen];

        int dstPos = 0;
        int no = 2;
        byte remainder = 0;

        for (int i = 0; i < len; i++)
        {
            byte c = (byte)(src[i] ^ Seed);
            if (no == 6)
            {
                dst[dstPos++] = (byte)((c & 0x3F) + Base);
                remainder = (byte)(remainder | ((c >> 2) & 0x30));
                dst[dstPos++] = (byte)(remainder + Base);
                remainder = 0;
            }
            else
            {
                byte temp = (byte)(c >> 2);
                dst[dstPos++] = (byte)(((temp & 0x3C) | (c & 0x3)) + Base);
                remainder = (byte)((remainder << 2) | (temp & 0x3));
            }
            no = (no % 6) + 2;
        }

        if (no != 2)
        {
            dst[dstPos++] = (byte)(remainder + Base);
        }

        return Encoding.ASCII.GetString(dst[..dstPos]);
    }

    /// <summary>
    /// 将普通文本（按 GBK 编码）编码为 6-bit ASCII 字符串 (对应 Delphi EncodeString)
    /// </summary>
    public static string EncodeString(string str)
    {
        if (string.IsNullOrEmpty(str)) return string.Empty;
        return EncodeBytes(GBK.GetBytes(str));
    }

    /// <summary>
    /// 将 6-bit ASCII 字符串解码并按 GBK 还原为明文字符串 (对应 Delphi DeCodeString)
    /// </summary>
    public static string DecodeText(string asciiStr)
    {
        if (string.IsNullOrEmpty(asciiStr)) return string.Empty;
        byte[] bytes = DecodeString(asciiStr);
        return GBK.GetString(bytes);
    }

    /// <summary>
    /// 将 6-bit ASCII 字符串解码为原始字节数组 (对应 DeCodeBuf)
    /// </summary>
    public static byte[] DecodeString(string asciiStr)
    {
        if (string.IsNullOrEmpty(asciiStr)) return Array.Empty<byte>();

        byte[] asciiBytes = Encoding.ASCII.GetBytes(asciiStr);
        int len = asciiBytes.Length;
        int nCycles = len / 4;
        int nBytesLeft = len % 4;

        List<byte> result = new(nCycles * 3 + nBytesLeft);

        for (int i = 0; i < nCycles; i++)
        {
            int begin = i * 4;
            byte remainder = (byte)(asciiBytes[begin + 3] - Base);

            byte temp0 = (byte)(asciiBytes[begin] - Base);
            byte c0 = (byte)(((temp0 << 2) & 0xF0) | (remainder & 0x0C) | (temp0 & 0x03));
            result.Add((byte)(c0 ^ Seed));

            byte temp1 = (byte)(asciiBytes[begin + 1] - Base);
            byte c1 = (byte)(((temp1 << 2) & 0xF0) | ((remainder << 2) & 0x0C) | (temp1 & 0x03));
            result.Add((byte)(c1 ^ Seed));

            byte temp2 = (byte)(asciiBytes[begin + 2] - Base);
            byte c2 = (byte)(temp2 | ((remainder << 2) & 0xC0));
            result.Add((byte)(c2 ^ Seed));
        }

        if (nBytesLeft == 2)
        {
            byte remainder = (byte)(asciiBytes[len - 1] - Base);
            byte temp = (byte)(asciiBytes[len - 2] - Base);
            byte c = (byte)(((temp << 2) & 0xF0) | ((remainder << 2) & 0x0C) | (temp & 0x03));
            result.Add((byte)(c ^ Seed));
        }
        else if (nBytesLeft == 3)
        {
            byte remainder = (byte)(asciiBytes[len - 1] - Base);

            byte temp0 = (byte)(asciiBytes[len - 3] - Base);
            byte c0 = (byte)(((temp0 << 2) & 0xF0) | (remainder & 0x0C) | (temp0 & 0x03));
            result.Add((byte)(c0 ^ Seed));

            byte temp1 = (byte)(asciiBytes[len - 2] - Base);
            byte c1 = (byte)(((temp1 << 2) & 0xF0) | ((remainder << 2) & 0x0C) | (temp1 & 0x03));
            result.Add((byte)(c1 ^ Seed));
        }

        return result.ToArray();
    }

    /// <summary>
    /// 将 DefaultMessage 结构编码为 16 字符的 ASCII 字符串 (对应 EncodeMessage)
    /// </summary>
    public static string EncodeMessage(DefaultMessage msg)
    {
        Span<byte> bytes = stackalloc byte[Marshal.SizeOf<DefaultMessage>()];
        MemoryMarshal.Write(bytes, in msg);
        return EncodeBytes(bytes);
    }

    /// <summary>
    /// 将 16 字符的 ASCII 字符串解码为 DefaultMessage 结构 (对应 DecodeMessage)
    /// </summary>
    public static DefaultMessage DecodeMessage(string encodedMsg)
    {
        byte[] bytes = DecodeString(encodedMsg);
        if (bytes.Length < Marshal.SizeOf<DefaultMessage>())
        {
            throw new ArgumentException("解码后的数据长度不足以解析 DefaultMessage (需12字节)");
        }
        return MemoryMarshal.Read<DefaultMessage>(bytes);
    }

    /// <summary>
    /// 打包标准传奇封包：# + 16位EncodedHeader + (可选Payload) + !
    /// </summary>
    public static string MakePacket(DefaultMessage header, string payload = "")
    {
        string encHeader = EncodeMessage(header);
        return $"#{encHeader}{payload}!";
    }
}
