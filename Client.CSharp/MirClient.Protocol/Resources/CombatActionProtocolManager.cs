using System;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 战士打击技能类型枚举
/// </summary>
public enum CombatSkillType
{
    NormalHit = 0,   // 普通物理攻击 (CM_HIT / CM_HIT_OPENMIR2)
    HeavyHit = 1,    // 攻杀剑术 (CM_HEAVYHIT = 3015)
    BigHit = 2,      // 刺杀剑术 (CM_BIGHIT = 3016)
    PowerHit = 3,    // 半月弯刀 (CM_POWERHIT = 3018)
    LongHit = 4,     // 逐日长距剑气 (CM_LONGHIT = 3019)
    WideHit = 5,     // 宽范围打击 (CM_WIDEHIT = 3024)
    FireHit = 6,     // 烈火剑法 (CM_FIREHIT = 3025)
    CrsHit = 7,      // 开天斩 (CM_CRSHIT = 3036)
    TwinHit = 8      // 双龙斩 (CM_TWINHIT = 3038)
}

/// <summary>
/// 盛大十周年与 OpenMir2 动作身法与战士高级打击协议管理器
/// 严格 1:1 对齐 OpenMir2/src/OpenMir2/Messages.cs 与 WorldServer.cs / PlayObject.Attack.cs
/// </summary>
public static class CombatActionProtocolManager
{
    /// <summary>
    /// 构造通用动作/打击上行封包
    /// OpenMir2 服务端标准:
    /// - Tag: 角色朝向 nDir
    /// - Recog: 目标坐标 MakeLong(nX, nY) = (nY << 16) | (nX & 0xFFFF)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateActionPacket(ushort ident, int targetX, int targetY, int dir)
    {
        int recog = ((targetY & 0xFFFF) << 16) | (targetX & 0xFFFF);
        var header = new DefaultMessage
        {
            Ident = ident,
            Recog = recog,
            Param = 0,
            Tag = (ushort)(dir & 0x7),
            Series = 0
        };
        return (header, string.Empty);
    }

    /// <summary>
    /// 解析动作封包中的坐标与朝向
    /// </summary>
    public static (int TargetX, int TargetY, int Dir) ParseActionPacket(DefaultMessage header)
    {
        int targetX = header.Recog & 0xFFFF;
        int targetY = (header.Recog >> 16) & 0xFFFF;
        int dir = header.Tag & 0x7;
        return (targetX, targetY, dir);
    }

    /// <summary>
    /// 构造打坐蹲下休息封包 (CM_SITDOWN = 3012)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateSitDownPacket(int x, int y, int dir)
        => CreateActionPacket(ProtocolConsts.CM_SITDOWN, x, y, dir);

    /// <summary>
    /// 构造骑马奔跑冲刺封包 (CM_HORSERUN = 3035)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateHorseRunPacket(int targetX, int targetY, int dir)
        => CreateActionPacket(ProtocolConsts.CM_HORSERUN, targetX, targetY, dir);

    /// <summary>
    /// 构造普通物理挥刀打击封包 (CM_HIT_OPENMIR2 = 3014)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateHitPacket(int targetX, int targetY, int dir)
        => CreateActionPacket(ProtocolConsts.CM_HIT_OPENMIR2, targetX, targetY, dir);

    /// <summary>
    /// 构造攻杀剑术重击封包 (CM_HEAVYHIT = 3015)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateHeavyHitPacket(int targetX, int targetY, int dir)
        => CreateActionPacket(ProtocolConsts.CM_HEAVYHIT, targetX, targetY, dir);

    /// <summary>
    /// 构造刺杀剑术破防打击封包 (CM_BIGHIT = 3016)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateBigHitPacket(int targetX, int targetY, int dir)
        => CreateActionPacket(ProtocolConsts.CM_BIGHIT, targetX, targetY, dir);

    /// <summary>
    /// 构造半月弯刀圆弧剑气横扫封包 (CM_POWERHIT = 3018)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreatePowerHitPacket(int targetX, int targetY, int dir)
        => CreateActionPacket(ProtocolConsts.CM_POWERHIT, targetX, targetY, dir);

    /// <summary>
    /// 构造长距剑气打击封包 (CM_LONGHIT = 3019)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateLongHitPacket(int targetX, int targetY, int dir)
        => CreateActionPacket(ProtocolConsts.CM_LONGHIT, targetX, targetY, dir);

    /// <summary>
    /// 构造宽范围剑刃斩击封包 (CM_WIDEHIT = 3024)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateWideHitPacket(int targetX, int targetY, int dir)
        => CreateActionPacket(ProtocolConsts.CM_WIDEHIT, targetX, targetY, dir);

    /// <summary>
    /// 构造烈火剑法火焰附加爆发打击封包 (CM_FIREHIT = 3025)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateFireHitPacket(int targetX, int targetY, int dir)
        => CreateActionPacket(ProtocolConsts.CM_FIREHIT, targetX, targetY, dir);

    /// <summary>
    /// 构造开天斩巨刃重斩封包 (CM_CRSHIT = 3036)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateCrsHitPacket(int targetX, int targetY, int dir)
        => CreateActionPacket(ProtocolConsts.CM_CRSHIT, targetX, targetY, dir);

    /// <summary>
    /// 构造双龙斩双刃打击封包 (CM_TWINHIT = 3038)
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateTwinHitPacket(int targetX, int targetY, int dir)
        => CreateActionPacket(ProtocolConsts.CM_TWINHIT, targetX, targetY, dir);

    /// <summary>
    /// 按照技能类型统一构造战士打击封包
    /// </summary>
    public static (DefaultMessage Header, string Payload) CreateCombatSkillPacket(CombatSkillType skillType, int targetX, int targetY, int dir)
    {
        return skillType switch
        {
            CombatSkillType.HeavyHit => CreateHeavyHitPacket(targetX, targetY, dir),
            CombatSkillType.BigHit   => CreateBigHitPacket(targetX, targetY, dir),
            CombatSkillType.PowerHit => CreatePowerHitPacket(targetX, targetY, dir),
            CombatSkillType.LongHit  => CreateLongHitPacket(targetX, targetY, dir),
            CombatSkillType.WideHit  => CreateWideHitPacket(targetX, targetY, dir),
            CombatSkillType.FireHit  => CreateFireHitPacket(targetX, targetY, dir),
            CombatSkillType.CrsHit   => CreateCrsHitPacket(targetX, targetY, dir),
            CombatSkillType.TwinHit  => CreateTwinHitPacket(targetX, targetY, dir),
            _                        => CreateHitPacket(targetX, targetY, dir)
        };
    }
}
