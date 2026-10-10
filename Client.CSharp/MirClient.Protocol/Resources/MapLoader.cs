using System.Runtime.InteropServices;
using System.Text;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 对应 Delphi MapUnit.pas 的地图文件头 (52 字节 packed)
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct MapHeader
{
    public ushort Width;                    // 0x00, 2 Bytes: 地图宽度
    public ushort Height;                   // 0x02, 2 Bytes: 地图高度
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    public byte[] Title;                    // 0x04, 16 Bytes: 地图标题
    public double UpdateDate;               // 0x14, 8 Bytes: Delphi 日期时间
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24)]
    public byte[] Reserved;                 // 0x1C, 24 Bytes: 保留字段
}

/// <summary>
/// 地图单个单元格瓦片结构 (兼容经典 12 字节与扩展 14 字节)
/// </summary>
public struct MapCell
{
    public ushort BkImg;        // 背景瓦片索引 (最高位 0x8000 为不能行走障碍)
    public ushort MidImg;       // 中间层瓦片索引 (最高位 0x8000 为障碍)
    public ushort FrImg;        // 前景物体建筑索引 (最高位 0x8000 为遮挡)
    public byte DoorIndex;      // 门索引
    public byte DoorOffset;     // 门偏移
    public byte AniFrame;       // 动画帧
    public byte AniTick;        // 动画跳帧间隔
    public byte Area;           // 所属区域
    public byte Light;          // 光照等级
    public byte TileIndex;      // 扩展背景库编号
    public byte SmTileIndex;    // 扩展前景库编号

    /// <summary>
    /// 是否阻挡人物行走 (基于 0x8000 掩码)
    /// </summary>
    public bool IsBlocked => (BkImg & 0x8000) != 0 || (MidImg & 0x8000) != 0;

    /// <summary>
    /// 获取去除掩码后的纯背景瓦片编号
    /// </summary>
    public int CleanBkIndex => BkImg & 0x7FFF;

    /// <summary>
    /// 获取去除掩码后的纯中景瓦片编号
    /// </summary>
    public int CleanMidIndex => MidImg & 0x7FFF;

    /// <summary>
    /// 获取去除掩码后的纯前景物体编号
    /// </summary>
    public int CleanFrIndex => FrImg & 0x7FFF;
}

/// <summary>
/// 盛大传奇 .map 地图文件加载器
/// </summary>
public class MapLoader
{
    public MapHeader Header { get; private set; }
    public int Width => Header.Width;
    public int Height => Header.Height;
    public MapCell[,] Cells { get; private set; } = new MapCell[0, 0];

    /// <summary>
    /// 从二进制字节数组加载地图
    /// </summary>
    public void Load(byte[] data)
    {
        using MemoryStream ms = new(data);
        using BinaryReader reader = new(ms);
        Load(reader, data.Length);
    }

    /// <summary>
    /// 从文件路径加载地图
    /// </summary>
    public void Load(string filePath)
    {
        byte[] data = File.ReadAllBytes(filePath);
        Load(data);
    }

    private void Load(BinaryReader reader, long totalLength)
    {
        // 1. 读取 52 字节包头
        Header = new MapHeader
        {
            Width = reader.ReadUInt16(),
            Height = reader.ReadUInt16(),
            Title = reader.ReadBytes(16),
            UpdateDate = reader.ReadDouble(),
            Reserved = reader.ReadBytes(24)
        };

        if (Width == 0 || Height == 0) return;

        Cells = new MapCell[Width, Height];

        // 2. 根据剩余字节长度判断单元格尺寸 (12 字节旧版 或 14 字节新版)
        long bodySize = totalLength - 52;
        int totalCells = Width * Height;
        int cellBytes = (int)(bodySize / totalCells);
        if (cellBytes < 12) cellBytes = 12;

        // 3. 逐瓦片读取 (先 X 后 Y，盛大标准排布)
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                MapCell cell = new MapCell
                {
                    BkImg = reader.ReadUInt16(),
                    MidImg = reader.ReadUInt16(),
                    FrImg = reader.ReadUInt16(),
                    DoorIndex = reader.ReadByte(),
                    DoorOffset = reader.ReadByte(),
                    AniFrame = reader.ReadByte(),
                    AniTick = reader.ReadByte(),
                    Area = reader.ReadByte(),
                    Light = reader.ReadByte()
                };

                if (cellBytes >= 14)
                {
                    cell.TileIndex = reader.ReadByte();
                    cell.SmTileIndex = reader.ReadByte();
                }

                // 略过超出部分的填充字节 (若有)
                if (cellBytes > 14)
                {
                    reader.ReadBytes(cellBytes - 14);
                }

                Cells[x, y] = cell;
            }
        }
    }
}
