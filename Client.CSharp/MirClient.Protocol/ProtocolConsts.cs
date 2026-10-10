namespace MirClient.Protocol;

/// <summary>
/// 严格对应 Grobal2.pas 中的经典传奇通信命令代码 (CM: Client->Server, SM: Server->Client)
/// </summary>
public static class ProtocolConsts
{
    // 客户端发往服务端的命令 (CM_)
    public const ushort CM_QUERYCHR      = 100;  // 查询角色列表
    public const ushort CM_NEWCHR        = 101;  // 创建新角色
    public const ushort CM_DELCHR        = 102;  // 删除角色
    public const ushort CM_SELCHR        = 103;  // 选择角色进入游戏
    public const ushort CM_ADDNEWUSER    = 2002; // 账号注册
    public const ushort CM_USERLOGON     = 2003; // 账号登录

    // 服务端发往客户端的响应 (SM_)
    public const ushort SM_QUERYCHR      = 520;  // 下发角色列表
    public const ushort SM_QUERYCHR_FAIL = 527;  // 查询角色失败
    public const ushort SM_NEWCHR_SUCCESS= 521;  // 创建角色成功
    public const ushort SM_NEWCHR_FAIL   = 522;  // 创建角色失败
    public const ushort SM_STARTPLAY     = 524;  // 开始进入游戏世界
}
