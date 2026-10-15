using System.Numerics;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 坐骑名驹品阶类型
/// </summary>
public enum MountType
{
    BrownHorse,    // 黄骠马 (基础战马)
    RedHare,       // 烈焰赤兔 (传世神驹)
    SnowDragon,    // 踏雪飞龙 (极地灵兽)
    ShadowPanther  // 幽冥玄豹 (荒古战兽)
}

/// <summary>
/// 乘骑状态
/// </summary>
public enum MountState
{
    Dismounted, // 步战步行
    Mounted,    // 策马驰骋
    Charging    // 破军冲锋突进中
}

/// <summary>
/// 坐骑属性配置
/// </summary>
public record MountDefinition(
    MountType Type, 
    string Name, 
    float SpeedBonus, 
    int BonusHp, 
    int BonusAc, 
    int BonusDc, 
    int BonusMac, 
    string Description);

/// <summary>
/// 盛大热血传奇十周年：灵兽战马坐骑与马术冲刺破军管理器 (纯 C# 领域状态机)
/// </summary>
public class MountManager
{
    private static MountManager? _instance;
    public static MountManager Instance => _instance ??= new MountManager();

    public const float ChargeCooldownMax = 8.0f; // 战马冲锋 8 秒冷却
    public const int ChargeDistance = 4;        // 突进 4 格

    public MountType CurrentMount { get; private set; } = MountType.RedHare;
    public MountState State { get; private set; } = MountState.Dismounted;
    public float ChargeCooldownTimer { get; private set; } = 0f;

    public bool IsMounted => State == MountState.Mounted || State == MountState.Charging;
    public bool IsCharging => State == MountState.Charging;
    public bool CanCharge => IsMounted && ChargeCooldownTimer <= 0f;

    public MountDefinition CurrentMountDef => GetDefinition(CurrentMount);

    public float SpeedMultiplier => IsMounted ? (1.0f + CurrentMountDef.SpeedBonus) : 1.0f;
    public int BonusHp => IsMounted ? CurrentMountDef.BonusHp : 0;
    public int BonusAc => IsMounted ? CurrentMountDef.BonusAc : 0;
    public int BonusDc => IsMounted ? CurrentMountDef.BonusDc : 0;
    public int BonusMac => IsMounted ? CurrentMountDef.BonusMac : 0;

    public static MountDefinition GetDefinition(MountType type) => type switch
    {
        MountType.BrownHorse => new MountDefinition(
            MountType.BrownHorse, "黄骠马", 0.5f, 150, 2, 0, 0, "西凉健马，蹄声雷动，跑图移速 +50%"),
        MountType.RedHare => new MountDefinition(
            MountType.RedHare, "烈焰赤兔", 0.8f, 300, 4, 3, 2, "日行千里，火鬣翻飞，跑图移速 +80%，攻防兼备"),
        MountType.SnowDragon => new MountDefinition(
            MountType.SnowDragon, "踏雪飞龙", 1.0f, 500, 6, 5, 4, "昆仑极地灵兽，乘风破浪，跑图移速翻倍 (+100%)"),
        MountType.ShadowPanther => new MountDefinition(
            MountType.ShadowPanther, "幽冥玄豹", 0.9f, 400, 5, 4, 3, "九幽荒古战兽，潜影无形，跑图移速 +90%"),
        _ => new MountDefinition(MountType.BrownHorse, "普通战马", 0.5f, 100, 1, 0, 0, "普通坐骑")
    };

    public void Reset()
    {
        State = MountState.Dismounted;
        CurrentMount = MountType.RedHare;
        ChargeCooldownTimer = 0f;
    }

    /// <summary>
    /// 快捷上下马切换 (Ctrl+M / M 键)
    /// </summary>
    public (bool Success, string Message) ToggleMount()
    {
        if (IsMounted)
        {
            State = MountState.Dismounted;
            return (true, $"【勒马步战】翻身跃下【{CurrentMountDef.Name}】，重归步战厮杀姿态！");
        }
        else
        {
            State = MountState.Mounted;
            return (true, $"【上马驰骋】跃上神驹【{CurrentMountDef.Name}】！移动速度提升至 {SpeedMultiplier:P0}，气血上限 +{BonusHp}！(按 Ctrl+F 施展战马冲锋)");
        }
    }

    /// <summary>
    /// 切换骑乘坐骑名驹
    /// </summary>
    public (bool Success, string Message) SwitchMount(MountType newMount)
    {
        CurrentMount = newMount;
        var def = CurrentMountDef;
        string msg = $"坐骑已切换为【{def.Name}】！(移速加成: +{def.SpeedBonus:P0}, 生命: +{def.BonusHp}, 物攻: +{def.BonusDc})";
        return (true, msg);
    }

    /// <summary>
    /// 发动骑战神技【战马冲锋】
    /// </summary>
    public bool TryPerformCharge(
        Vector2 startTile, 
        int direction, 
        Func<int, int, bool> isBlocked, 
        out Vector2 endTile, 
        out List<Vector2> chargePath, 
        out int chargeDamage, 
        out string report)
    {
        endTile = startTile;
        chargePath = [];
        chargeDamage = 0;
        report = "";

        if (!IsMounted)
        {
            report = "您当前处于步战状态，无法施展战马冲锋！请先按 Ctrl+M 上马。";
            return false;
        }

        if (ChargeCooldownTimer > 0f)
        {
            report = $"战马冲锋尚在蓄力冷却中 (剩余 {ChargeCooldownTimer:F1} 秒)！";
            return false;
        }

        // 计算朝向单位向量 (经典传奇 8 方向)
        Vector2 dirVec = direction switch
        {
            0 => new Vector2(0, -1),
            1 => new Vector2(1, -1),
            2 => new Vector2(1, 0),
            3 => new Vector2(1, 1),
            4 => new Vector2(0, 1),
            5 => new Vector2(-1, 1),
            6 => new Vector2(-1, 0),
            _ => new Vector2(-1, -1)
        };

        // 沿直线检测 4 格位移
        Vector2 current = startTile;
        for (int step = 1; step <= ChargeDistance; step++)
        {
            Vector2 next = startTile + dirVec * step;
            int nx = (int)MathF.Round(next.X);
            int ny = (int)MathF.Round(next.Y);

            if (isBlocked(nx, ny))
            {
                break; // 撞墙停止
            }

            current = next;
            chargePath.Add(current);
        }

        endTile = current;
        ChargeCooldownTimer = ChargeCooldownMax;
        chargeDamage = 180 + (CurrentMountDef.BonusDc * 15); // 基础冲撞破甲高伤

        report = $"★★★【铁骑破军】人马合一，骑乘【{CurrentMountDef.Name}】狂暴突进冲刺！贯穿撞击造成 {chargeDamage} 点破甲物理伤害并施加眩晕击退！★★★";
        return true;
    }

    /// <summary>
    /// 受击落马判定 (受到重创时几率落马)
    /// </summary>
    public bool CheckDismountOnDamage(int incomingDamage, int maxHp)
    {
        if (!IsMounted) return false;

        // 若单次受创超过生命上限的 25%，有 30% 几率被击落受挫
        if (incomingDamage >= maxHp * 0.25f && Random.Shared.Next(100) < 30)
        {
            State = MountState.Dismounted;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 推进时间冷却
    /// </summary>
    public void Update(float dt)
    {
        if (ChargeCooldownTimer > 0f)
        {
            ChargeCooldownTimer = MathF.Max(0f, ChargeCooldownTimer - dt);
        }
    }
}
