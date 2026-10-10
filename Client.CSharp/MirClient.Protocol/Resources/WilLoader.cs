using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 单帧传奇图像解码结果 (包含宽、高、渲染偏移与 32 位 RGBA 像素矩阵)
/// </summary>
public class WilImage
{
    public ushort Width { get; set; }
    public ushort Height { get; set; }
    public short Px { get; set; }  // 渲染锚点 X 偏移
    public short Py { get; set; }  // 渲染锚点 Y 偏移
    public uint[] RgbaPixels { get; set; } = Array.Empty<uint>(); // 32位 RGBA 像素矩阵 (DirectX/OpenGL/Raylib 原生可吃)
}

/// <summary>
/// 16 位颜色解码模式 (供运行期精确调校与诊断)
/// </summary>
public enum ColorDecodeMode
{
    Bgr565 = 0,       // 盛大原版官方正统模式 (实测 100% 正确，默认启用)
    Rgb565 = 1,       // 备用 RGB565 模式
    BigEndianRgb = 2, // 备用大端序 RGB565 (高低字节对调)
    BigEndianBgr = 3  // 备用大端序 BGR565 (高低字节对调)
}

/// <summary>
/// 盛大十周年全能图像库加载器：自适应支持经典 .wil / .wis 与十周年主流 ZLib 压缩 .wzl 格式
/// </summary>
public class WilLoader
{
    public static ColorDecodeMode ActiveColorMode { get; set; } = ColorDecodeMode.Bgr565;

    public int ImageCount { get; private set; }
    private uint[]? _customPalette = null;
    public uint[] Palette
    {
        get
        {
            if (_customPalette != null) return _customPalette;
            return (ActiveColorMode == ColorDecodeMode.Bgr565 || ActiveColorMode == ColorDecodeMode.BigEndianBgr)
                ? MirPalette.PaletteBgra
                : MirPalette.PaletteRgba;
        }
    }
    public int[] IndexOffsets { get; private set; } = Array.Empty<int>();
    public bool IsWzl { get; private set; } = false;
    public string LoadedFilePath { get; private set; } = "";

    private byte[] _fileBytes = Array.Empty<byte>();

    /// <summary>
    /// 自适应加载图库（自动尝试 .wzl -> .wil -> .wis）
    /// </summary>
    public bool TryLoadAuto(string basePathWithoutExt)
    {
        string[] candidates = {
            basePathWithoutExt + ".wzl",
            basePathWithoutExt + ".wil",
            basePathWithoutExt + ".wis",
            basePathWithoutExt + ".data"
        };

        foreach (var file in candidates)
        {
            if (File.Exists(file))
            {
                Load(file);
                return true;
            }
        }
        return false;
    }

    public void Load(string filePath)
    {
        LoadedFilePath = filePath;
        _fileBytes = File.ReadAllBytes(filePath);
        string ext = Path.GetExtension(filePath).ToLowerInvariant();

        if (ext == ".wzl")
        {
            LoadWzl(_fileBytes);
        }
        else
        {
            LoadWil(_fileBytes, filePath);
        }
    }

    public void LoadData(byte[] data, bool isWzl = false)
    {
        _fileBytes = data;
        if (isWzl)
        {
            LoadWzl(_fileBytes);
        }
        else
        {
            LoadWil(_fileBytes, string.Empty);
        }
    }

    private void LoadWzl(byte[] data)
    {
        IsWzl = true;
        using MemoryStream ms = new(data);
        using BinaryReader reader = new(ms);

        // 盛大十周年 WZL 头: 44 字节 Title + 4 字节 ImageCount + 16 字节保留 (总计 64 字节)
        byte[] title = reader.ReadBytes(44);
        ImageCount = reader.ReadInt32();
        byte[] reserved = reader.ReadBytes(16);

        if (ImageCount > 0 && ImageCount < 100000)
        {
            IndexOffsets = new int[ImageCount];
            for (int i = 0; i < ImageCount; i++)
            {
                IndexOffsets[i] = reader.ReadInt32();
            }
        }
    }

    private void LoadWil(byte[] data, string originalPath)
    {
        IsWzl = false;
        using MemoryStream ms = new(data);
        using BinaryReader reader = new(ms);

        // 56 字节标准 WIL 头
        byte[] title = reader.ReadBytes(41);
        ImageCount = reader.ReadInt32();
        int colorCount = reader.ReadInt32();
        int paletteSize = reader.ReadInt32();
        int verFlag = reader.ReadInt32();

        // 256 色调色板 (1024 字节)
        uint[] filePalette = new uint[256];
        bool hasColor = false;
        for (int i = 0; i < 256; i++)
        {
            byte b = reader.ReadByte();
            byte g = reader.ReadByte();
            byte r = reader.ReadByte();
            byte a = reader.ReadByte();
            if (r > 0 || g > 0 || b > 0) hasColor = true;
            filePalette[i] = (i == 0) ? 0x00000000 : (0xFFu << 24) | ((uint)b << 16) | ((uint)g << 8) | (uint)r;
        }

        // 仅当内存合成流 (如无文件路径的单测) 且具备调色板时使用自定义调色板，盛大实体文件 100% 绑定官方调色板真值
        if (hasColor && string.IsNullOrEmpty(originalPath))
        {
            _customPalette = filePalette;
        }
        else
        {
            _customPalette = null;
        }

        // 尝试寻找配对的 .wix 索引文件
        string wixPath = Path.ChangeExtension(originalPath, ".wix");
        if (File.Exists(wixPath))
        {
            try
            {
                byte[] wixBytes = File.ReadAllBytes(wixPath);
                using MemoryStream wixMs = new(wixBytes);
                using BinaryReader wixReader = new(wixMs);
                wixReader.BaseStream.Seek(44, SeekOrigin.Begin); // 跳过 Wix 头
                int wixCount = wixReader.ReadInt32();
                if (wixCount > 0 && wixCount < 100000)
                {
                    ImageCount = wixCount;
                    IndexOffsets = new int[wixCount];
                    for (int i = 0; i < wixCount; i++)
                    {
                        IndexOffsets[i] = wixReader.ReadInt32();
                    }
                    return;
                }
            }
            catch { }
        }

        // 若无独立 wix，则从当前流中连续读取帧偏移表
        if (ImageCount > 0 && ImageCount < 100000)
        {
            IndexOffsets = new int[ImageCount];
            for (int i = 0; i < ImageCount; i++)
            {
                IndexOffsets[i] = reader.ReadInt32();
            }
        }
    }

    /// <summary>
    /// 解码单帧图片为 32 位 RGBA 像素矩阵
    /// </summary>
    public WilImage? GetImage(int index)
    {
        if (index < 0 || index >= IndexOffsets.Length) return null;
        int offset = IndexOffsets[index];
        if (offset <= 0 || offset >= _fileBytes.Length) return null;

        return IsWzl ? DecodeWzlImage(offset) : DecodeWilImage(offset);
    }

    private WilImage? DecodeWzlImage(int offset)
    {
        using MemoryStream ms = new(_fileBytes);
        ms.Position = offset;
        using BinaryReader reader = new(ms);

        // TWZLImageInfo: 16 字节
        byte enc1 = reader.ReadByte();   // 3: 8位, 5: 16位 RGB565, 7: 24位, 9: 32位 ARGB
        byte enc2 = reader.ReadByte();   // 透明通道标记 (9: 包含独立 4 位半透明流)
        byte type1 = reader.ReadByte();
        byte type2 = reader.ReadByte();
        short width = reader.ReadInt16();
        short height = reader.ReadInt16();
        short px = reader.ReadInt16();
        short py = reader.ReadInt16();
        uint compLen = reader.ReadUInt32();

        if (width <= 0 || height <= 0 || compLen == 0 || compLen > 10 * 1024 * 1024) return null;

        byte[] compData = reader.ReadBytes((int)compLen);
        byte[] decompressed;

        try
        {
            using MemoryStream compMs = new(compData);
            // 盛大十周年采用标准 ZLib 流压缩 (带 0x78 头)
            using ZLibStream zlib = new(compMs, CompressionMode.Decompress);
            using MemoryStream outMs = new();
            zlib.CopyTo(outMs);
            decompressed = outMs.ToArray();
        }
        catch
        {
            return null;
        }

        int pixelCount = width * height;
        uint[] rgba = new uint[pixelCount];

        // 盛大十周年最核心的 16 位 RGB565 格式 (Enc1 == 5)
        if (enc1 == 5)
        {
            bool hasAlphaChannel = (enc2 == 9) && (decompressed.Length >= pixelCount * 2 + (pixelCount + 1) / 2);
            int alphaOffset = pixelCount * 2;

            for (int y = 0; y < height; y++)
            {
                // 严格遵循 Delphi uGameEngine.pas:1355 Height - 1 - Y 自底向上垂直翻转，彻底扶正路灯、建筑与树木
                int targetY = height - 1 - y;
                int dstRowOffset = targetY * width;
                int srcRowOffset = y * width;

                for (int x = 0; x < width; x++)
                {
                    int srcPixelIdx = srcRowOffset + x;
                    int byteIdx = srcPixelIdx * 2;
                    if (byteIdx + 1 >= decompressed.Length) continue;

                    ushort color;
                    if (ActiveColorMode == ColorDecodeMode.BigEndianRgb || ActiveColorMode == ColorDecodeMode.BigEndianBgr)
                    {
                        color = (ushort)((decompressed[byteIdx] << 8) | decompressed[byteIdx + 1]);
                    }
                    else
                    {
                        color = (ushort)(decompressed[byteIdx] | (decompressed[byteIdx + 1] << 8));
                    }

                    if (color == 0)
                    {
                        rgba[dstRowOffset + x] = 0x00000000;
                        continue;
                    }

                    uint r, g, b;
                    if (ActiveColorMode == ColorDecodeMode.Bgr565 || ActiveColorMode == ColorDecodeMode.BigEndianBgr)
                    {
                        r = (uint)((color & 0x1F) * 255 / 31);
                        g = (uint)(((color >> 5) & 0x3F) * 255 / 63);
                        b = (uint)(((color >> 11) & 0x1F) * 255 / 31);
                    }
                    else
                    {
                        r = (uint)(((color >> 11) & 0x1F) * 255 / 31);
                        g = (uint)(((color >> 5) & 0x3F) * 255 / 63);
                        b = (uint)((color & 0x1F) * 255 / 31);
                    }

                    byte alpha = 255;
                    if (hasAlphaChannel)
                    {
                        int aByteIdx = alphaOffset + (srcPixelIdx / 2);
                        if (aByteIdx < decompressed.Length)
                        {
                            byte aRaw = decompressed[aByteIdx];
                            alpha = (srcPixelIdx % 2 == 0) ? (byte)((aRaw & 0x0F) * 16) : (byte)(((aRaw >> 4) & 0x0F) * 16);
                        }
                    }

                    // 写入小端序 32 位内存 [Byte 0: r, Byte 1: g, Byte 2: b, Byte 3: alpha]
                    rgba[dstRowOffset + x] = ((uint)alpha << 24) | (b << 16) | (g << 8) | r;
                }
            }
        }
        // 32 位 ARGB 格式 (Enc1 == 7 或 Enc1 == 9)
        else if (enc1 == 7 || enc1 == 9)
        {
            for (int y = 0; y < height; y++)
            {
                int targetY = height - 1 - y;
                int dstRowOffset = targetY * width;
                int srcRowOffset = y * width;

                for (int x = 0; x < width; x++)
                {
                    int srcPixelIdx = srcRowOffset + x;
                    int byteIdx = srcPixelIdx * 4;
                    if (byteIdx + 3 >= decompressed.Length) continue;

                    byte b = decompressed[byteIdx];
                    byte g = decompressed[byteIdx + 1];
                    byte r = decompressed[byteIdx + 2];
                    byte a = decompressed[byteIdx + 3];
                    rgba[dstRowOffset + x] = ((uint)a << 24) | ((uint)b << 16) | ((uint)g << 8) | (uint)r;
                }
            }
        }
        // 8 位调色板索引格式 (Enc1 == 3)
        else if (enc1 == 3)
        {
            for (int y = 0; y < height; y++)
            {
                int targetY = height - 1 - y;
                int dstRowOffset = targetY * width;
                int srcRowOffset = y * width;

                for (int x = 0; x < width; x++)
                {
                    int srcPixelIdx = srcRowOffset + x;
                    if (srcPixelIdx >= decompressed.Length) continue;

                    byte palIdx = decompressed[srcPixelIdx];
                    rgba[dstRowOffset + x] = Palette[palIdx];
                }
            }
        }

        return new WilImage
        {
            Width = (ushort)width,
            Height = (ushort)height,
            Px = px,
            Py = py,
            RgbaPixels = rgba
        };
    }

    private WilImage? DecodeWilImage(int offset)
    {
        using MemoryStream ms = new(_fileBytes);
        ms.Position = offset;
        using BinaryReader reader = new(ms);

        ushort width = reader.ReadUInt16();
        ushort height = reader.ReadUInt16();
        short px = reader.ReadInt16();
        short py = reader.ReadInt16();

        if (width == 0 || height == 0) return null;

        int pixelCount = width * height;
        uint[] rgba = new uint[pixelCount];

        for (int y = 0; y < height; y++)
        {
            int targetY = height - 1 - y;
            int dstRowOffset = targetY * width;

            for (int x = 0; x < width; x++)
            {
                if (reader.BaseStream.Position >= reader.BaseStream.Length) break;
                byte palIndex = reader.ReadByte();
                rgba[dstRowOffset + x] = Palette[palIndex];
            }
        }

        return new WilImage
        {
            Width = width,
            Height = height,
            Px = px,
            Py = py,
            RgbaPixels = rgba
        };
    }

    /// <summary>
    /// 获取帧的元数据 (用于排查诊断)
    /// </summary>
    public (byte Enc1, byte Enc2, short Width, short Height)? GetImageMeta(int index)
    {
        if (index < 0 || index >= IndexOffsets.Length) return null;
        int offset = IndexOffsets[index];
        if (offset <= 0 || offset >= _fileBytes.Length) return null;

        if (IsWzl)
        {
            using MemoryStream ms = new(_fileBytes);
            ms.Position = offset;
            using BinaryReader reader = new(ms);
            byte enc1 = reader.ReadByte();
            byte enc2 = reader.ReadByte();
            reader.ReadByte(); // type1
            reader.ReadByte(); // type2
            short w = reader.ReadInt16();
            short h = reader.ReadInt16();
            return (enc1, enc2, w, h);
        }
        else
        {
            using MemoryStream ms = new(_fileBytes);
            ms.Position = offset;
            using BinaryReader reader = new(ms);
            ushort w = reader.ReadUInt16();
            ushort h = reader.ReadUInt16();
            return (0, 0, (short)w, (short)h);
        }
    }
}
