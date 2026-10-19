using System;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 盛大十周年与 OpenMir2 行会外交结盟与二级密码安全锁网络协议管理器
/// 严格 1:1 对齐 OpenMir2/src/M2Server/Player/PlayObject.Operate.cs 与 PlayObject.Base.cs
/// </summary>
public static class GuildDiplomacyAndSecurityProtocolManager
{
    #region 客户端发包构造 (CM_)

    /// <summary>
    /// 构造双方会长面对面结盟请求封包 (CM_GUILDALLY = 1044)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateGuildAllyPacket()
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_GUILDALLY,
            Recog = 0,
            Param = 0,
            Tag = 0,
            Series = 0
        };
        return (header, string.Empty);
    }

    /// <summary>
    /// 构造解除与指定行会的同盟关系封包 (CM_GUILDBREAKALLY = 1045)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateGuildBreakAllyPacket(string targetGuildName)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_GUILDBREAKALLY,
            Recog = 0,
            Param = 0,
            Tag = 0,
            Series = 0
        };
        return (header, EDCode.EncodeString(targetGuildName ?? string.Empty));
    }

    /// <summary>
    /// 构造输入/验证二级密码进行仓库解锁封包 (CM_PASSWORD = 2001)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateVerifyPasswordPacket(string password)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_PASSWORD,
            Recog = 0,
            Param = 0,
            Tag = 0,
            Series = 1 // Series=1 代表携带有效用户输入密码
        };
        return (header, EDCode.EncodeString(password ?? string.Empty));
    }

    /// <summary>
    /// 构造修改二级密码封包 (CM_CHGPASSWORD = 2002)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateChangePasswordPacket(string oldPassword, string newPassword)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_CHGPASSWORD,
            Recog = 0,
            Param = 0,
            Tag = 0,
            Series = 1
        };
        string combined = $"{oldPassword ?? string.Empty}/{newPassword ?? string.Empty}";
        return (header, EDCode.EncodeString(combined));
    }

    /// <summary>
    /// 构造首次设置二级密码封包 (CM_SETPASSWORD = 2004)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateSetPasswordPacket(string password)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_SETPASSWORD,
            Recog = 0,
            Param = 0,
            Tag = 0,
            Series = 1
        };
        return (header, EDCode.EncodeString(password ?? string.Empty));
    }

    #endregion

    #region 服务端响应解析 (SM_)

    /// <summary>
    /// 解析二级密码锁定状态 (SM_PASSWORDSTATUS = 8002)
    /// Recog: 1=已上锁, 0=未锁定/已开锁
    /// </summary>
    public static bool ParsePasswordStatus(DefaultMessage header)
    {
        return header.Recog == 1 || header.Param == 1;
    }

    /// <summary>
    /// 解析行会结盟失败原因错误码 (SM_GUILDMAKEALLY_FAIL = 769)
    /// -1: 未面对对方会长, -2: 处于行会战中, -3: 自身或对方非行会掌门, -4: 对方未开启联盟授权
    /// </summary>
    public static (int Code, string Description) ParseGuildAllyFail(DefaultMessage header)
    {
        int code = header.Recog;
        string desc = code switch
        {
            -1 => "请面对对方行会掌门发起结盟请求",
            -2 => "双方行会当前正处于敌对交战状态，无法结盟",
            -3 => "您或对方不是行会掌门人，无权发起结盟",
            -4 => "对方行会未开启允许结盟授权开关",
            _ => "行会结盟失败"
        };
        return (code, desc);
    }

    #endregion
}
