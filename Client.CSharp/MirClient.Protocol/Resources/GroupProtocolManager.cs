using System;
using System.Collections.Generic;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 玩家组队协同操作结果
/// </summary>
public record GroupActionResult(
    bool Success,
    string TargetName,
    string Message,
    int ErrorCode = 0);

/// <summary>
/// OpenMir2 经典传奇玩家组队网络协议管理器 (100% 对齐 PlayObject.Operate.cs 与 Messages.cs)
/// </summary>
public static class GroupProtocolManager
{
    /// <summary>
    /// 将服务端返回的组队失败错误码转为玩家可读的中文提示信息
    /// </summary>
    public static string GetGroupFailReason(int errorCode)
    {
        return errorCode switch
        {
            -1 => "您不是队长，无权操作或队伍不存在",
            -2 => "目标玩家不存在、已离线或已阵亡",
            -3 => "目标玩家已经加入了其他队伍",
            -4 => "目标玩家未开启允许组队模式",
            -5 => "队伍人数已达上限，无法继续招纳队员",
            _ => $"组队操作失败 (错误码: {errorCode})"
        };
    }

    /// <summary>
    /// 构造切换允许组队模式的数据包 (CM_GROUPMODE = 1019)
    /// </summary>
    public static (DefaultMessage Header, string EncodedPayload) CreateGroupModePacket(bool allow)
    {
        DefaultMessage header = new(
            ident: ProtocolConsts.CM_GROUPMODE,
            recog: 0,
            param: (ushort)(allow ? 1 : 0),
            tag: 0,
            series: 0);
        return (header, string.Empty);
    }

    /// <summary>
    /// 构造创建队伍数据包 (CM_CREATEGROUP = 1020)
    /// </summary>
    public static (DefaultMessage Header, string EncodedPayload) CreateCreateGroupPacket(string targetName)
    {
        DefaultMessage header = new(
            ident: ProtocolConsts.CM_CREATEGROUP,
            recog: 0,
            param: 0,
            tag: 0,
            series: 0);
        string payload = EDCode.EncodeString(targetName ?? string.Empty);
        return (header, payload);
    }

    /// <summary>
    /// 构造向队伍添加队员数据包 (CM_ADDGROUPMEMBER = 1021)
    /// </summary>
    public static (DefaultMessage Header, string EncodedPayload) CreateAddMemberPacket(string targetName)
    {
        DefaultMessage header = new(
            ident: ProtocolConsts.CM_ADDGROUPMEMBER,
            recog: 0,
            param: 0,
            tag: 0,
            series: 0);
        string payload = EDCode.EncodeString(targetName ?? string.Empty);
        return (header, payload);
    }

    /// <summary>
    /// 构造移出队员或退出队伍数据包 (CM_DELGROUPMEMBER = 1022)
    /// </summary>
    public static (DefaultMessage Header, string EncodedPayload) CreateDelMemberPacket(string targetName)
    {
        DefaultMessage header = new(
            ident: ProtocolConsts.CM_DELGROUPMEMBER,
            recog: 0,
            param: 0,
            tag: 0,
            series: 0);
        string payload = EDCode.EncodeString(targetName ?? string.Empty);
        return (header, payload);
    }

    /// <summary>
    /// 解析服务端下发全队成员名单封包 (SM_GROUPMEMBERS = 667, Payload: Name1/Name2/.../)
    /// </summary>
    public static IReadOnlyList<string> ParseGroupMembers(DefaultMessage header, string rawBody)
    {
        string text = EDCode.DecodeText(rawBody ?? string.Empty);
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        string[] tokens = text.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens;
    }

    /// <summary>
    /// 解析组队模式切换响应 (SM_GROUPMODECHANGED = 659)
    /// </summary>
    public static bool ParseGroupModeChanged(DefaultMessage header)
    {
        return header.Param == 1;
    }

    /// <summary>
    /// 解析创建或加入队伍操作响应反馈
    /// </summary>
    public static GroupActionResult ParseGroupActionResult(DefaultMessage header, string rawBody, ushort ident)
    {
        string targetName = EDCode.DecodeText(rawBody ?? string.Empty);

        switch (ident)
        {
            case ProtocolConsts.SM_CREATEGROUP_OK:
                return new GroupActionResult(true, targetName, "队伍创建成功！您已成为队长。");

            case ProtocolConsts.SM_CREATEGROUP_FAIL:
                return new GroupActionResult(false, targetName, GetGroupFailReason(header.Recog), header.Recog);

            case ProtocolConsts.SM_GROUPADDMEM_OK:
                return new GroupActionResult(true, targetName, string.IsNullOrEmpty(targetName) ? "新队员已成功入队！" : $"队员【{targetName}】已成功加入队伍！");

            case ProtocolConsts.SM_GROUPADDMEM_FAIL:
                return new GroupActionResult(false, targetName, GetGroupFailReason(header.Recog), header.Recog);

            case ProtocolConsts.SM_GROUPDELMEM_OK:
                return new GroupActionResult(true, targetName, string.IsNullOrEmpty(targetName) ? "队员已移出队伍！" : $"队员【{targetName}】已退出或被移出队伍。");

            case ProtocolConsts.SM_GROUPDELMEM_FAIL:
                return new GroupActionResult(false, targetName, GetGroupFailReason(header.Recog), header.Recog);

            default:
                return new GroupActionResult(false, targetName, "未知组队操作结果");
        }
    }
}
