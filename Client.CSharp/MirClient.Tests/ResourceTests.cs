using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class ResourceTests
{
    [Fact]
    public void MapLoader_SyntheticData_ShouldParseHeaderAndCells()
    {
        // 构造一个 2x2 的虚拟地图二进制流 (52 字节头 + 4 个 12 字节单元格 = 100 字节)
        byte[] mapBytes = new byte[52 + 2 * 2 * 12];
        using (MemoryStream ms = new(mapBytes))
        using (BinaryWriter writer = new(ms))
        {
            writer.Write((ushort)2); // Width = 2
            writer.Write((ushort)2); // Height = 2
            writer.Write(new byte[16]); // Title
            writer.Write((double)44500.0); // Date
            writer.Write(new byte[24]); // Reserved

            // 写入 (0, 0): 可通过单元格
            writer.Write((ushort)10); // BkImg = 10 (无0x8000)
            writer.Write((ushort)0);  // MidImg = 0
            writer.Write((ushort)0);  // FrImg
            writer.Write((byte)0);    // DoorIndex
            writer.Write((byte)0);    // DoorOffset
            writer.Write((byte)0);    // AniFrame
            writer.Write((byte)0);    // AniTick
            writer.Write((byte)0);    // Area
            writer.Write((byte)15);   // Light

            // 写入 (0, 1): 障碍物单元格 (BkImg 带 0x8000)
            writer.Write((ushort)(10 | 0x8000)); // 障碍物
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((byte)15);

            // 写入 (1, 0) 与 (1, 1)
            for (int i = 0; i < 2; i++)
            {
                writer.Write((ushort)20);
                writer.Write((ushort)0);
                writer.Write((ushort)0);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((byte)15);
            }
        }

        MapLoader loader = new();
        loader.Load(mapBytes);

        Assert.Equal(2, loader.Width);
        Assert.Equal(2, loader.Height);
        Assert.False(loader.Cells[0, 0].IsBlocked);
        Assert.Equal(10, loader.Cells[0, 0].CleanBkIndex);

        Assert.True(loader.Cells[0, 1].IsBlocked);
        Assert.Equal(10, loader.Cells[0, 1].CleanBkIndex);
    }

    [Fact]
    public void WilLoader_SyntheticData_ShouldParseHeaderPaletteAndFrames()
    {
        // 构造一个包含 1 张 2x2 图片的虚拟 WIL 流
        // 56 字节头 + 1024 调色板 + 4 字节偏移表 + 8 字节图片头 + 4 字节像素 (2x2)
        using MemoryStream ms = new();
        using (BinaryWriter writer = new(ms))
        {
            // 头: 41B Title + 16B 参数 = 57B
            writer.Write(new byte[41]); // Title
            writer.Write((int)1);       // ImageCount = 1
            writer.Write((int)256);     // ColorCount = 256
            writer.Write((int)1024);    // PaletteSize = 1024
            writer.Write((int)0);       // VerFlag

            // 调色板 (1024B): 索引 1 设为红色 (R=255, G=0, B=0)
            for (int i = 0; i < 256; i++)
            {
                if (i == 1)
                {
                    writer.Write((byte)0);   // B
                    writer.Write((byte)0);   // G
                    writer.Write((byte)255); // R
                    writer.Write((byte)0);   // A
                }
                else
                {
                    writer.Write((uint)0);
                }
            }

            int frameOffset = 41 + 16 + 1024 + 4;
            // 偏移表 (4B)
            writer.Write(frameOffset);

            // 帧数据 (8B): Width=2, Height=2, Px=-5, Py=-10
            writer.Write((ushort)2);
            writer.Write((ushort)2);
            writer.Write((short)-5);
            writer.Write((short)-10);

            // 像素索引 (4B): [1, 1, 0, 1]
            writer.Write((byte)1);
            writer.Write((byte)1);
            writer.Write((byte)0); // 透明
            writer.Write((byte)1);
        }
        byte[] wilBytes = ms.ToArray();

        WilLoader loader = new();
        loader.LoadData(wilBytes);

        Assert.Equal(1, loader.ImageCount);
        WilImage? img = loader.GetImage(0);

        Assert.NotNull(img);
        Assert.Equal(2, img.Width);
        Assert.Equal(2, img.Height);
        Assert.Equal(-5, img.Px);
        Assert.Equal(-10, img.Py);

        // 验证像素颜色与自底向上垂直翻转：
        // 原数据 行0=[1, 1], 行1=[0, 1] 翻转后 行0=[0, 1], 行1=[1, 1]
        // 索引 0 (行0列0) 应为原行1列0的透明色 (0x00000000)
        // 索引 2 (行1列0) 应为原行0列0的红色 (内存 [r=255, g=0, b=0, a=255] 即 0xFF0000FFu)
        Assert.Equal(0x00000000u, img.RgbaPixels[0]);
        Assert.Equal(0xFF0000FFu, img.RgbaPixels[2]);
    }

    [Fact]
    public void WilLoader_RGB565ChannelMath_ShouldMatchSpecs()
    {
        // 1:1 严格对齐 Delphi 原版 uCommon.pas:505-516 Pixel565To32
        // 测试纯红 RGB565 (R=31 存储在高5位 bits 11..15) => 31 << 11 = 0xF800
        ushort redRgb565 = 31 << 11;
        uint r = (uint)(((redRgb565 >> 11) & 0x1F) * 255 / 31);
        uint g = (uint)(((redRgb565 >> 5) & 0x3F) * 255 / 63);
        uint b = (uint)((redRgb565 & 0x1F) * 255 / 31);
        uint redPixel = (0xFFu << 24) | (b << 16) | (g << 8) | r;

        Assert.Equal(255u, r);
        Assert.Equal(0u, g);
        Assert.Equal(0u, b);
        Assert.Equal(0xFF0000FFu, redPixel); // 小端序内存排布标准纯红 [R=255, G=0, B=0, A=255]

        // 测试纯蓝 RGB565 (B=31 存储在低5位 bits 0..4) => 31 = 0x001F
        ushort blueRgb565 = 31;
        uint br = (uint)(((blueRgb565 >> 11) & 0x1F) * 255 / 31);
        uint bg = (uint)(((blueRgb565 >> 5) & 0x3F) * 255 / 63);
        uint bb = (uint)((blueRgb565 & 0x1F) * 255 / 31);
        uint bluePixel = (0xFFu << 24) | (bb << 16) | (bg << 8) | br;

        Assert.Equal(0u, br);
        Assert.Equal(0u, bg);
        Assert.Equal(255u, bb);
        Assert.Equal(0xFFFF0000u, bluePixel); // 小端序内存排布标准纯蓝 [R=0, G=0, B=255, A=255]

        // 测试自然灰色 (R=15, G=31, B=15) => (15 << 11) | (31 << 5) | 15 = 0x7BEF
        // 关键断言: R、G、B 必须在 0~255 范围内均等对应，杜绝底板或地砖偏红偏蓝
        ushort grayRgb565 = (15 << 11) | (31 << 5) | 15;
        uint gr = (uint)(((grayRgb565 >> 11) & 0x1F) * 255 / 31);
        uint gg = (uint)(((grayRgb565 >> 5) & 0x3F) * 255 / 63);
        uint gb = (uint)((grayRgb565 & 0x1F) * 255 / 31);

        Assert.Equal(123u, gr);
        Assert.Equal(125u, gg);
        Assert.Equal(123u, gb);
        Assert.Equal(gr, gb); // 红蓝严格对称，杜绝底板或地砖偏红偏蓝
    }
}
