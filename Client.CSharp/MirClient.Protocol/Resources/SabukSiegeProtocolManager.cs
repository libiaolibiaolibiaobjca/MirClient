using System;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 沙巴克城门与工事防御实时快照 (对齐 OpenMir2 CastleDoor 与 DefenseUnit)
/// </summary>
public record CastleWallStatusRecord(
    int DamageLevel, // 0: 完好, 1: 轻度受损, 2: 严重受损, 3: 坍塌损毁
    int HpPercent,   // 0 ~ 100
    bool IsOpen);    // true: 开启通畅, false: 紧闭阻挡

/// <summary>
/// 沙巴克霸主与归属权快照
/// </summary>
public record CastleOwnerRecord(
    string GuildName,
    string MasterName,
    string OccupyDate);

/// <summary>
/// 沙巴克攻城战战事阶段快照
/// </summary>
public record WarStatusRecord(
    SiegePhase Phase,
    float RemainingSeconds);

/// <summary>
/// OpenMir2 沙巴克攻城战与防御工事网络协议管理器
/// </summary>
public static class SabukSiegeProtocolManager
{
    /// <summary>
    /// 构造申请攻城战或行会宣战数据包 (CM_GUILDWAR = 1042)
    /// </summary>
    public static (DefaultMessage Header, string EncodedPayload) CreateGuildWarPacket(string targetName)
    {
        DefaultMessage header = new(
            ident: ProtocolConsts.CM_GUILDWAR,
            recog: 0,
            param: 0,
            tag: 0,
            series: 0);

        string payload = EDCode.EncodeString(targetName ?? "Sabuk");
        return (header, payload);
    }

    /// <summary>
    /// 构造提取城池资金数据包 (CM_CASTLEWITHDRAWGOLD = 1043)
    /// </summary>
    public static (DefaultMessage Header, string EncodedPayload) CreateCastleWithdrawGoldPacket(int amount)
    {
        DefaultMessage header = new(
            ident: ProtocolConsts.CM_CASTLEWITHDRAWGOLD,
            recog: amount,
            param: 0,
            tag: 0,
            series: 0);

        return (header, string.Empty);
    }

    /// <summary>
    /// 解析攻城战阶段与时钟广播 (SM_WARSTATUS = 765)
    /// </summary>
    public static WarStatusRecord ParseWarStatus(DefaultMessage header)
    {
        SiegePhase phase = header.Recog switch
        {
            1 => SiegePhase.Prepare,
            2 => SiegePhase.Fighting,
            3 => SiegePhase.Settled,
            _ => SiegePhase.Peace
        };

        float remainingSec = Math.Max(0, (float)header.Param);
        return new WarStatusRecord(phase, remainingSec);
    }

    /// <summary>
    /// 解析沙巴克城门防御工事状态广播 (SM_SABUK_WALLSTATUS = 763)
    /// </summary>
    public static CastleWallStatusRecord ParseWallStatus(DefaultMessage header)
    {
        int damageLevel = Math.Clamp(header.Recog, 0, 3);
        int hpPercent = Math.Clamp((int)header.Param, 0, 100);
        bool isOpen = header.Tag == 1;

        return new CastleWallStatusRecord(damageLevel, hpPercent, isOpen);
    }

    /// <summary>
    /// 解析沙巴克霸主行会与掌门人全服广播 (SM_CASTLE_OWNER = 764)
    /// 格式: GuildName/MasterName/OccupyDate/
    /// </summary>
    public static CastleOwnerRecord ParseCastleOwner(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return new CastleOwnerRecord("傲视天下", "沙巴克城主", DateTime.Now.ToString("yyyy-MM-dd"));
        }

        string text = payload;
        if (!payload.Any(c => c > 127))
        {
            string decoded = EDCode.DecodeText(payload);
            if (!string.IsNullOrWhiteSpace(decoded) && decoded.Any(c => c > 127))
            {
                text = decoded;
            }
        }

        string[] tokens = text.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string guild = tokens.Length > 0 ? tokens[0] : "傲视天下";
        string master = tokens.Length > 1 ? tokens[1] : "沙巴克城主";
        string date = tokens.Length > 2 ? tokens[2] : DateTime.Now.ToString("yyyy-MM-dd");

        return new CastleOwnerRecord(guild, master, date);
    }

    /// <summary>
    /// 获取攻城战阶段的中文描述
    /// </summary>
    public static string GetSiegePhaseName(SiegePhase phase)
    {
        return phase switch
        {
            SiegePhase.Peace => "和平休战期",
            SiegePhase.Prepare => "号角备战期",
            SiegePhase.Fighting => "战火交锋期",
            SiegePhase.Settled => "加冕结算期",
            _ => "未知阶段"
        };
    }
}
