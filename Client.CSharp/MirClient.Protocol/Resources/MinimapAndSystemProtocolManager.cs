using System;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 盛大十周年与 OpenMir2 小地图雷达、输入框变量响应、按键设置与心跳协议管理器
/// 严格 1:1 对齐 OpenMir2/src/OpenMir2/Messages.cs 与 M2Server/Player/PlayObject.Operate.cs
/// </summary>
public static class MinimapAndSystemProtocolManager
{
    #region 客户端发包构造 (CM_)

    /// <summary>
    /// 构造请求小地图雷达资源封包 (CM_WANTMINIMAP = 1033)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateWantMinimapPacket()
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_WANTMINIMAP,
            Recog = 0,
            Param = 0,
            Tag = 0,
            Series = 0
        };
        return (header, string.Empty);
    }

    /// <summary>
    /// 构造响应 NPC 输入框变量值封包 (CM_QUERYVAL = 1065)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateQueryValueResponsePacket(int npcActorId, string value)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_QUERYVAL,
            Recog = npcActorId,
            Param = 0,
            Tag = 0,
            Series = 0
        };
        return (header, EDCode.EncodeString(value ?? string.Empty));
    }

    /// <summary>
    /// 构造快捷键变更封包 (CM_MAGICKEYCHANGE = 1008)
    /// Param: 技能 MagicId, Tag: 快捷键位 0~11 (对应 F1~F8, F9~F12)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateMagicKeyChangePacket(ushort magicId, byte keySlot)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_MAGICKEYCHANGE,
            Recog = 0,
            Param = magicId,
            Tag = keySlot,
            Series = 0
        };
        return (header, string.Empty);
    }

    /// <summary>
    /// 构造查询/同步个性化用户设置封包 (CM_QUERYUSERSET = 3040)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateQueryUserSetPacket()
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_QUERYUSERSET,
            Recog = 0,
            Param = 0,
            Tag = 0,
            Series = 0
        };
        return (header, string.Empty);
    }

    /// <summary>
    /// 构造属性加点分配封包 (CM_ADJUST_BONUS = 1043)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateAdjustBonusPacket(byte bonusType, ushort count)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_ADJUST_BONUS,
            Recog = 0,
            Param = bonusType,
            Tag = count,
            Series = 0
        };
        return (header, string.Empty);
    }

    /// <summary>
    /// 构造确认阅读登录公告封包 (CM_LOGINNOTICEOK = 1018)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateLoginNoticeOkPacket()
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_LOGINNOTICEOK,
            Recog = 0,
            Param = 0,
            Tag = 0,
            Series = 0
        };
        return (header, string.Empty);
    }

    /// <summary>
    /// 构造客户端与服务端网络心跳与时间戳同步封包 (CM_CHECKTIME = 15999)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateCheckTimePacket(int clientTick)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_CHECKTIME,
            Recog = clientTick,
            Param = 0,
            Tag = 0,
            Series = 0
        };
        return (header, string.Empty);
    }

    #endregion

    #region 服务端响应解析 (SM_)

    /// <summary>
    /// 解析小地图资源索引编号 (SM_READMINIMAP_OK = 710)
    /// 服务端下发: SendDefMessage(SM_READMINIMAP_OK, 0, Envir.MinMap, 0, 0) -> Param 为编号
    /// </summary>
    public static int ParseMinimapResponse(DefaultMessage header)
    {
        return header.Param > 0 ? header.Param : header.Recog;
    }

    #endregion
}
