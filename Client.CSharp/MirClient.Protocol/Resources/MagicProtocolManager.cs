using System;
using System.Collections.Generic;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 严格对应 OpenMir2 MagicConst.cs 的经典三职业核心技能编号
/// </summary>
public enum MagicType : byte
{
    Fireball = 1,        // 火球术
    Healing = 2,         // 治愈术
    OneSword = 3,        // 基本剑术
    Ilkwang = 4,         // 精神力战法
    Fireball2 = 5,       // 大火球
    Amyounsul = 6,       // 施毒术
    Yedo = 7,            // 攻杀剑法
    FireWind = 8,        // 抗拒火环
    HellFire = 9,        // 地狱火
    LightningMovie = 10, // 疾光电影
    Thunder = 11,        // 雷电术
    Assassinate = 12,    // 刺杀剑术
    SoulCharm = 13,      // 灵魂火符
    HolyShield = 16,     // 捆魔咒
    Skeleton = 17,       // 召唤骷髅
    Teleport = 21,       // 瞬息移动
    FireWall = 22,       // 火墙
    HalfMoon = 23,       // 半月弯刀
    FireSword = 25,      // 烈火剑法
    Shield = 31,         // 魔法盾
    IceRoar = 33,        // 冰咆哮
    SummonShinshu = 30   // 召唤神兽
}

/// <summary>
/// 服务端广播的实体施法事件 (SM_SPELL 17)
/// </summary>
public record SpellBroadcastEvent(
    long ActorId,
    int TargetX,
    int TargetY,
    int MagicId,
    string TargetInfo);

/// <summary>
/// 服务端下发的法术命中与伤害结算事件 (SM_MAGICFIRE 638)
/// </summary>
public record MagicFireEvent(
    long TargetId,
    int Damage,
    int MagicId);

/// <summary>
/// 玩家习得的技能法术信息
/// </summary>
public record UserMagicInfo(
    int MagicId,
    string Name,
    int Level,
    int MpCost,
    int DelayMs);

/// <summary>
/// OpenMir2 技能法术学习与施法网络封包协议管理器
/// </summary>
public class MagicProtocolManager
{
    public static MagicProtocolManager Instance { get; } = new();

    private static readonly Dictionary<int, string> _magicNames = new()
    {
        { 1, "火球术" },
        { 2, "治愈术" },
        { 3, "基本剑术" },
        { 4, "精神力战法" },
        { 5, "大火球" },
        { 6, "施毒术" },
        { 7, "攻杀剑术" },
        { 8, "抗拒火环" },
        { 9, "地狱火" },
        { 10, "疾光电影" },
        { 11, "雷电术" },
        { 12, "刺杀剑术" },
        { 13, "灵魂火符" },
        { 16, "捆魔咒" },
        { 17, "召唤骷髅" },
        { 21, "瞬息移动" },
        { 22, "火墙" },
        { 23, "半月弯刀" },
        { 25, "烈火剑法" },
        { 30, "召唤神兽" },
        { 31, "魔法盾" },
        { 33, "冰咆哮" }
    };

    /// <summary>
    /// 获取技能中文规范名称
    /// </summary>
    public static string GetMagicName(int magicId)
    {
        return _magicNames.TryGetValue(magicId, out var name) ? name : $"未知法术({magicId})";
    }

    /// <summary>
    /// 构建客户端主动施放法术封包 (CM_SPELL 3017)
    /// </summary>
    public static (DefaultMessage Msg, string Payload, string EncodedPacket) CreateSpellPacket(
        int magicId,
        int targetX,
        int targetY,
        int targetRecogId = 0)
    {
        // 严格 1:1 对齐 OpenMir2 官方 Messages.MakeMessage(CM_SPELL, MakeLong(x, y), LoWord(target), magicId, HiWord(target))
        int recog = (targetX & 0xFFFF) | ((targetY & 0xFFFF) << 16);
        ushort param = (ushort)(targetRecogId & 0xFFFF);
        ushort series = (ushort)((targetRecogId >> 16) & 0xFFFF);
        var msg = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_SPELL,
            Recog = recog,
            Param = param,
            Tag = (ushort)magicId,
            Series = series
        };

        string encodedHeader = EDCode.EncodeMessage(msg);
        string payload = string.Empty;
        string packet = $"#{encodedHeader}!";
        return (msg, payload, packet);
    }

    /// <summary>
    /// 解析服务端下发的施法广播封包 (SM_SPELL 17)
    /// </summary>
    public static SpellBroadcastEvent ParseSpellBroadcast(DefaultMessage header, string rawBody)
    {
        long actorId = header.Recog;
        int targetX = header.Param;
        int targetY = header.Tag;
        int magicId = header.Series;
        string targetInfo = EDCode.DecodeText(rawBody);

        return new SpellBroadcastEvent(actorId, targetX, targetY, magicId, targetInfo);
    }

    /// <summary>
    /// 解析服务端下发的法术命中与伤害结算封包 (SM_MAGICFIRE 638)
    /// </summary>
    public static MagicFireEvent ParseMagicFire(DefaultMessage header)
    {
        long targetId = header.Recog;
        int damage = header.Param;
        int magicId = header.Tag;

        return new MagicFireEvent(targetId, damage, magicId);
    }
}
