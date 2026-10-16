using System;
using System.Collections.Generic;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 服务端下发的一级行会职务与其成员清单 (严格对齐 OpenMir2 GuildRank)
/// </summary>
public record GuildRankEntry(
    int RankNo,
    string RankName,
    List<string> Members);

/// <summary>
/// 行会管理操作结果
/// </summary>
public record GuildActionResult(
    bool Success,
    string TargetName,
    string Message,
    int ErrorCode = 0);

/// <summary>
/// OpenMir2 经典传奇行会公会网络管理协议管理器 (100% 对齐 PlayObject.Operate.cs 与 Messages.cs)
/// </summary>
public static class GuildProtocolManager
{
    /// <summary>
    /// 将服务端返回的行会错误码转为可读中文信息
    /// </summary>
    public static string GetGuildFailReason(int errorCode)
    {
        return errorCode switch
        {
            1 => "您没有权利使用该行会管理命令 (必须为行会掌门人)",
            2 => "想加入行会的成员必须与掌门人面对面站立",
            3 => "该玩家已经加入了我们的行会",
            4 => "对方已经加入了其他行会",
            5 => "对方未开启允许加入行会模式 (@letguild)",
            _ => $"行会操作失败 (错误码: {errorCode})"
        };
    }

    /// <summary>
    /// 构造请求打开行会面板数据包 (CM_OPENGUILDDLG = 1035)
    /// </summary>
    public static (DefaultMessage Header, string EncodedPayload) CreateOpenGuildDlgPacket()
    {
        DefaultMessage header = new(
            ident: ProtocolConsts.CM_OPENGUILDDLG,
            recog: 0,
            param: 0,
            tag: 0,
            series: 0);
        return (header, string.Empty);
    }

    /// <summary>
    /// 构造请求行会主页数据包 (CM_GUILDHOME = 1036)
    /// </summary>
    public static (DefaultMessage Header, string EncodedPayload) CreateGuildHomePacket()
    {
        DefaultMessage header = new(
            ident: ProtocolConsts.CM_GUILDHOME,
            recog: 0,
            param: 0,
            tag: 0,
            series: 0);
        return (header, string.Empty);
    }

    /// <summary>
    /// 构造请求行会全员名册数据包 (CM_GUILDMEMBERLIST = 1037)
    /// </summary>
    public static (DefaultMessage Header, string EncodedPayload) CreateGuildMemberListPacket()
    {
        DefaultMessage header = new(
            ident: ProtocolConsts.CM_GUILDMEMBERLIST,
            recog: 0,
            param: 0,
            tag: 0,
            series: 0);
        return (header, string.Empty);
    }

    /// <summary>
    /// 构造招募新成员入会数据包 (CM_GUILDADDMEMBER = 1038)
    /// </summary>
    public static (DefaultMessage Header, string EncodedPayload) CreateGuildAddMemberPacket(string targetName)
    {
        DefaultMessage header = new(
            ident: ProtocolConsts.CM_GUILDADDMEMBER,
            recog: 0,
            param: 0,
            tag: 0,
            series: 0);
        string payload = EDCode.EncodeString(targetName ?? string.Empty);
        return (header, payload);
    }

    /// <summary>
    /// 构造逐出成员或退出行会数据包 (CM_GUILDDELMEMBER = 1039)
    /// </summary>
    public static (DefaultMessage Header, string EncodedPayload) CreateGuildDelMemberPacket(string targetName)
    {
        DefaultMessage header = new(
            ident: ProtocolConsts.CM_GUILDDELMEMBER,
            recog: 0,
            param: 0,
            tag: 0,
            series: 0);
        string payload = EDCode.EncodeString(targetName ?? string.Empty);
        return (header, payload);
    }

    /// <summary>
    /// 构造会长修改行会公告数据包 (CM_GUILDUPDATENOTICE = 1040)
    /// </summary>
    public static (DefaultMessage Header, string EncodedPayload) CreateGuildUpdateNoticePacket(string notice)
    {
        DefaultMessage header = new(
            ident: ProtocolConsts.CM_GUILDUPDATENOTICE,
            recog: 0,
            param: 0,
            tag: 0,
            series: 0);
        string payload = EDCode.EncodeString(notice ?? string.Empty);
        return (header, payload);
    }

    /// <summary>
    /// 构造会长修改行会公告数据包 (多行公告)
    /// </summary>
    public static (DefaultMessage Header, string EncodedPayload) CreateGuildUpdateNoticePacket(IEnumerable<string> noticeLines)
    {
        return CreateGuildUpdateNoticePacket(string.Join("\r", noticeLines ?? Array.Empty<string>()));
    }

    /// <summary>
    /// 解析行会面板主信息封包 (SM_OPENGUILDDLG = 753)
    /// </summary>
    public static (string GuildName, bool IsMaster, string Notice) ParseGuildDlg(DefaultMessage header, string rawBody)
    {
        string text = EDCode.DecodeText(rawBody ?? string.Empty);
        if (string.IsNullOrWhiteSpace(text))
        {
            return (string.Empty, false, string.Empty);
        }

        string[] lines = text.Split('\r', StringSplitOptions.TrimEntries);
        string guildName = lines.Length > 0 ? lines[0] : string.Empty;
        bool isMaster = lines.Length > 2 && lines[2] == "1";
        string notice = lines.Length > 3 ? string.Join("\n", lines[3..]) : string.Empty;

        return (guildName, isMaster, notice);
    }

    /// <summary>
    /// 解析行会面板主信息封包
    /// </summary>
    public static (string GuildName, bool IsMaster, string Notice) ParseGuildDlg(string rawBody)
    {
        return ParseGuildDlg(default, rawBody);
    }

    /// <summary>
    /// 解析服务端下发全行会职务层级与成员名册 (SM_SENDGUILDMEMBERLIST = 756)
    /// 格式: #RankNo/*RankName/Member1/Member2/.../#RankNo/*RankName/...
    /// </summary>
    public static IReadOnlyList<GuildRankEntry> ParseGuildMembers(DefaultMessage header, string rawBody)
    {
        string text = EDCode.DecodeText(rawBody ?? string.Empty);
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<GuildRankEntry>();
        }

        List<GuildRankEntry> results = new();
        string[] tokens = text.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        GuildRankEntry? currentRank = null;

        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];

            if (token.StartsWith('#'))
            {
                // 新职务起始: 形如 "#0" 接着下一个 token 为 "*会长"
                int rankNo = 0;
                string rankNoStr = token[1..];
                int.TryParse(rankNoStr, out rankNo);

                string rankName = "帮众";
                if (i + 1 < tokens.Length && tokens[i + 1].StartsWith('*'))
                {
                    rankName = tokens[i + 1][1..];
                    i++; // 跳过职务名 token
                }

                currentRank = new GuildRankEntry(rankNo, rankName, new List<string>());
                results.Add(currentRank);
            }
            else if (currentRank != null)
            {
                // 该职务下的成员人名
                currentRank.Members.Add(token);
            }
        }

        return results;
    }

    /// <summary>
    /// 解析服务端下发全行会职务层级与成员名册
    /// </summary>
    public static IReadOnlyList<GuildRankEntry> ParseGuildMembers(string rawBody)
    {
        return ParseGuildMembers(default, rawBody);
    }

    /// <summary>
    /// 解析行会增删成员操作反馈
    /// </summary>
    public static GuildActionResult ParseGuildActionResult(DefaultMessage header, string rawBody, ushort ident)
    {
        string targetName = EDCode.DecodeText(rawBody ?? string.Empty);

        switch (ident)
        {
            case ProtocolConsts.SM_GUILDADDMEMBER_OK:
                return new GuildActionResult(true, targetName, string.IsNullOrEmpty(targetName) ? "新成员已成功加入行会！" : $"【{targetName}】已正式加入行会！");

            case ProtocolConsts.SM_GUILDADDMEMBER_FAIL:
                return new GuildActionResult(false, targetName, GetGuildFailReason(header.Recog), header.Recog);

            case ProtocolConsts.SM_GUILDDELMEMBER_OK:
                return new GuildActionResult(true, targetName, string.IsNullOrEmpty(targetName) ? "成员已移出或退出行会。" : $"【{targetName}】已离开行会。");

            case ProtocolConsts.SM_GUILDDELMEMBER_FAIL:
                return new GuildActionResult(false, targetName, GetGuildFailReason(header.Recog), header.Recog);

            default:
                return new GuildActionResult(false, targetName, "未知行会操作结果");
        }
    }
}
