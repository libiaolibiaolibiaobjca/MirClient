namespace MirClient.Protocol;

/// <summary>
/// 严格对应 Grobal2.pas 与 OpenMir2 Messages.cs 中的经典传奇通信命令代码 (CM: Client->Server, SM: Server->Client)
/// </summary>
public static class ProtocolConsts
{
    // 客户端发往服务端的命令 (CM_)
    public const ushort CM_QUERYCHR      = 100;  // 查询角色列表
    public const ushort CM_NEWCHR        = 101;  // 创建新角色
    public const ushort CM_DELCHR        = 102;  // 删除角色
    public const ushort CM_SELCHR        = 103;  // 选择角色进入游戏
    public const ushort CM_SELECTSERVER  = 104;  // 选择游戏区服
    public const ushort CM_TURN          = 1003; // 角色原地转身
    public const ushort CM_WALK          = 1004; // 角色走步 (单格)
    public const ushort CM_RUN           = 1005; // 角色奔跑 (跨格)
    public const ushort CM_HIT           = 1006; // 角色物理攻击
    public const ushort CM_SAY           = 1007; // 玩家发言广播
    public const ushort CM_IDPASSWORD    = 2001; // 账号密码登录 (LoginGate 7000)
    public const ushort CM_ADDNEWUSER    = 2002; // 账号注册
    public const ushort CM_USERLOGON     = 2003; // 用户登录确认

    // 服务端发往客户端的响应与世界广播 (SM_)
    public const ushort SM_LOGON                = 50;   // 登录进入世界 (下发玩家属性)
    public const ushort SM_TURN                 = 51;   // 实体转身
    public const ushort SM_WALK                 = 52;   // 实体走步
    public const ushort SM_RUN                  = 53;   // 实体奔跑
    public const ushort SM_HIT                  = 54;   // 实体攻击
    public const ushort SM_POWERHIT             = 55;   // 强力攻击
    public const ushort SM_STRUCK               = 56;   // 实体受击硬直与掉血
    public const ushort SM_DEATH                = 57;   // 实体死亡倒地
    public const ushort SM_SKELETON             = 58;   // 实体遗骸化
    public const ushort SM_DISAPPEAR            = 59;   // 实体移出视口/消失
    public const ushort SM_SPURN                = 510;  // 刷新怪物或NPC实体
    public const ushort SM_NOWACTOR             = 511;  // 刷新其他玩家角色实体
    public const ushort SM_QUERYCHR             = 520;  // 下发角色列表
    public const ushort SM_NEWCHR_SUCCESS       = 521;  // 创建角色成功
    public const ushort SM_NEWCHR_FAIL          = 522;  // 创建角色失败
    public const ushort SM_STARTPLAY            = 525;  // 选角成功，开始进入游戏网关
    public const ushort SM_QUERYCHR_FAIL        = 527;  // 查询角色失败
    public const ushort SM_PASSOK_SELECTSERVER  = 529;  // 密码验证通过，请求选择服务器
    public const ushort SM_HEAR                 = 530;  // 收到聊天发言
    public const ushort SM_CHANGEMAP            = 634;  // 切换地图 (下发地图名与坐标)
}
