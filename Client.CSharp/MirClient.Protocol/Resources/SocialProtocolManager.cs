using System;
using System.Collections.Generic;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 师门网络信息下发记录
/// </summary>
public record MasterSyncRecord(
    string? MasterName,
    bool IsGraduated,
    int Reputation,
    List<ApprenticeInfo> Apprentices
);

/// <summary>
/// 盛大十周年与 OpenMir2 好友与师徒社交网络协议管理器
/// </summary>
public static class SocialProtocolManager
{
    #region 客户端发包构造 (CM_)

    /// <summary>
    /// 构造添加好友数据包 (CM_ADDFRIEND = 1050)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateAddFriendPacket(string friendName)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_ADDFRIEND,
            Recog = 0,
            Param = 0,
            Tag = 0,
            Series = 0
        };
        return (header, friendName?.Trim() ?? string.Empty);
    }

    /// <summary>
    /// 构造删除好友数据包 (CM_DELFRIEND = 1051)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateDelFriendPacket(string friendName)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_DELFRIEND,
            Recog = 0,
            Param = 0,
            Tag = 0,
            Series = 0
        };
        return (header, friendName?.Trim() ?? string.Empty);
    }

    /// <summary>
    /// 构造查询好友列表数据包 (CM_QUERYFRIENDLIST = 1052)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateQueryFriendListPacket()
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_QUERYFRIENDLIST,
            Recog = 0,
            Param = 0,
            Tag = 0,
            Series = 0
        };
        return (header, string.Empty);
    }

    /// <summary>
    /// 构造拜师或收徒请求包 (CM_MASTER_REQUEST = 1053)
    /// </summary>
    /// <param name="isApprentice">true=请求拜对方为师; false=请求收对方为徒</param>
    /// <param name="targetName">目标侠士名字</param>
    public static (DefaultMessage Header, string Payload) CreateMasterRequestPacket(bool isApprentice, string targetName)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_MASTER_REQUEST,
            Recog = (ushort)(isApprentice ? 1 : 2),
            Param = 0,
            Tag = 0,
            Series = 0
        };
        return (header, targetName?.Trim() ?? string.Empty);
    }

    /// <summary>
    /// 构造答复拜师或收徒请求包 (CM_MASTER_RESPONSE = 1054)
    /// </summary>
    /// <param name="accept">true=同意; false=拒绝</param>
    /// <param name="applicantName">申请人名字</param>
    public static (DefaultMessage Header, string Payload) CreateMasterResponsePacket(bool accept, string applicantName)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_MASTER_RESPONSE,
            Recog = (ushort)(accept ? 1 : 0),
            Param = 0,
            Tag = 0,
            Series = 0
        };
        return (header, applicantName?.Trim() ?? string.Empty);
    }

    /// <summary>
    /// 构造逐出或脱离师门数据包 (CM_MASTER_EXPEL = 1055)
    /// </summary>
    /// <param name="isExpelApprentice">true=师父逐出徒弟; false=徒弟主动叛离师门</param>
    /// <param name="targetName">徒弟或师父名字</param>
    public static (DefaultMessage Header, string Payload) CreateMasterExpelPacket(bool isExpelApprentice, string targetName)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_MASTER_EXPEL,
            Recog = (ushort)(isExpelApprentice ? 1 : 2),
            Param = 0,
            Tag = 0,
            Series = 0
        };
        return (header, targetName?.Trim() ?? string.Empty);
    }

    /// <summary>
    /// 构造出师典礼数据包 (CM_MASTER_GRADUATE = 1056)
    /// </summary>
    /// <param name="isSelf">true=徒弟自身举行出师礼; false=师父为徒弟主持出师礼</param>
    /// <param name="apprenticeName">弟子名字</param>
    public static (DefaultMessage Header, string Payload) CreateMasterGraduatePacket(bool isSelf, string apprenticeName)
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_MASTER_GRADUATE,
            Recog = (ushort)(isSelf ? 2 : 1),
            Param = 0,
            Tag = 0,
            Series = 0
        };
        return (header, apprenticeName?.Trim() ?? string.Empty);
    }

    #endregion

    #region 服务端下发解析 (SM_)

    /// <summary>
    /// 解析单条好友记录
    /// 格式: Name/Job/Level/MapName/IsOnline
    /// </summary>
    public static FriendInfo? ParseFriendRecord(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var parts = text.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 1) return null;

        string name = parts[0].Trim();
        CharacterJob job = CharacterJob.Warrior;
        if (parts.Length > 1 && int.TryParse(parts[1], out int jobVal))
        {
            job = (CharacterJob)jobVal;
        }

        int level = 1;
        if (parts.Length > 2 && int.TryParse(parts[2], out int lvlVal))
        {
            level = Math.Max(1, lvlVal);
        }

        string mapName = parts.Length > 3 ? parts[3].Trim() : "比奇城";
        bool isOnline = true;
        if (parts.Length > 4 && int.TryParse(parts[4], out int onlineVal))
        {
            isOnline = onlineVal == 1;
        }

        return new FriendInfo(name, job, level, mapName, isOnline);
    }

    /// <summary>
    /// 解析好友列表全员下发 (SM_FRIENDLIST = 773)
    /// 支持换行符与正斜杠两种协议切分格式
    /// </summary>
    public static List<FriendInfo> ParseFriendList(string rawPayload)
    {
        var list = new List<FriendInfo>();
        if (string.IsNullOrWhiteSpace(rawPayload)) return list;

        string[] entries = rawPayload.Contains('\n')
            ? rawPayload.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            : rawPayload.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var entry in entries)
        {
            var info = ParseFriendRecord(entry);
            if (info != null)
            {
                list.Add(info);
            }
        }

        return list;
    }

    /// <summary>
    /// 解析好友上下线实时广播 (SM_FRIEND_ONOFF = 777)
    /// Recog: 1=上线, 0=下线
    /// Payload: Name/MapName
    /// </summary>
    public static (string Name, bool IsOnline, string MapName) ParseFriendOnOff(DefaultMessage header, string rawPayload)
    {
        bool isOnline = header.Recog == 1;
        string name = "未知好友";
        string mapName = "比奇城";

        if (!string.IsNullOrWhiteSpace(rawPayload))
        {
            var parts = rawPayload.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0) name = parts[0].Trim();
            if (parts.Length > 1) mapName = parts[1].Trim();
        }

        return (name, isOnline, mapName);
    }

    /// <summary>
    /// 解析师门全量下发 (SM_MASTER_LIST = 779)
    /// Recog: 师门声望值
    /// Payload 格式: 
    /// 第一行: MasterName/IsGraduated
    /// 后续行: ApprenticeName/Job/Level/IsGraduated
    /// </summary>
    public static MasterSyncRecord ParseMasterList(DefaultMessage header, string rawPayload)
    {
        int reputation = (int)header.Recog;
        string? masterName = null;
        bool isGraduated = false;
        var apprentices = new List<ApprenticeInfo>();

        if (string.IsNullOrWhiteSpace(rawPayload))
        {
            return new MasterSyncRecord(masterName, isGraduated, reputation, apprentices);
        }

        var lines = rawPayload.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length > 0)
        {
            var masterParts = lines[0].Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (masterParts.Length > 0 && !string.IsNullOrWhiteSpace(masterParts[0]))
            {
                masterName = masterParts[0].Trim();
                if (masterName.Equals("null", StringComparison.OrdinalIgnoreCase) || masterName == "0")
                {
                    masterName = null;
                }
            }

            if (masterParts.Length > 1 && int.TryParse(masterParts[1], out int gradVal))
            {
                isGraduated = gradVal == 1;
            }

            for (int i = 1; i < lines.Length; i++)
            {
                var appParts = lines[i].Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (appParts.Length < 1) continue;

                string appName = appParts[0].Trim();
                CharacterJob job = CharacterJob.Warrior;
                if (appParts.Length > 1 && int.TryParse(appParts[1], out int jobVal))
                {
                    job = (CharacterJob)jobVal;
                }

                int level = 1;
                if (appParts.Length > 2 && int.TryParse(appParts[2], out int lvlVal))
                {
                    level = Math.Max(1, lvlVal);
                }

                bool appGrad = false;
                if (appParts.Length > 3 && int.TryParse(appParts[3], out int appGradVal))
                {
                    appGrad = appGradVal == 1;
                }

                apprentices.Add(new ApprenticeInfo(appName, job, level, appGrad));
            }
        }

        return new MasterSyncRecord(masterName, isGraduated, reputation, apprentices);
    }

    /// <summary>
    /// 解析弟子升级广播 (SM_APPRENTICE_LEVELUP = 780)
    /// Recog: 弟子新等级
    /// Param: 师父获得声望奖励
    /// Payload: 弟子名
    /// </summary>
    public static (string ApprenticeName, int NewLevel, int RepGain) ParseApprenticeLevelUp(DefaultMessage header, string rawPayload)
    {
        string name = rawPayload?.Trim() ?? "弟子";
        int newLevel = (int)header.Recog;
        int repGain = (int)header.Param;
        return (name, newLevel, repGain);
    }

    /// <summary>
    /// 解析师徒交互结果通知 (SM_MASTER_RESULT = 778)
    /// Recog: 1=拜师成功, 2=收徒成功, 3=出师成功, 4=脱离/逐出成功, 0=操作失败
    /// </summary>
    public static (bool Success, int StatusCode, string Message) ParseMasterResult(DefaultMessage header, string rawPayload)
    {
        int code = (int)header.Recog;
        bool success = code > 0;
        string msg = !string.IsNullOrWhiteSpace(rawPayload) 
            ? rawPayload.Trim() 
            : code switch
            {
                1 => "拜师仪式圆满成功！",
                2 => "门下再添良徒，收徒成功！",
                3 => "恭贺弟子功德圆满，正式出师！",
                4 => "已脱离或逐出师门。",
                _ => "师徒仪式操作失败。"
            };
        return (success, code, msg);
    }

    #endregion
}
