using System.Runtime.InteropServices;
using System.Text;

namespace MirClient.Protocol;

/// <summary>
/// 对应 Delphi Grobal2.pas 中的 TDefaultMessage / TCmdPack (12 字节 packed 结构)
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct DefaultMessage
{
    public int Recog;       // 0x00, 4 Bytes: UID / 实体句柄 / 交互标识
    public ushort Ident;    // 0x04, 2 Bytes: Cmd 命令字 (例如 CM_QUERYCHR, SM_QUERYCHR)
    public ushort Param;    // 0x06, 2 Bytes: 参数 1 / 坐标 X
    public ushort Tag;      // 0x08, 2 Bytes: 参数 2 / 坐标 Y
    public ushort Series;   // 0x0A, 2 Bytes: 参数 3 / 朝向 Direct

    public DefaultMessage(ushort ident, int recog = 0, ushort param = 0, ushort tag = 0, ushort series = 0)
    {
        Ident = ident;
        Recog = recog;
        Param = param;
        Tag = tag;
        Series = series;
    }
}
