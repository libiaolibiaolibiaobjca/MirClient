namespace MirClient.Protocol.Resources;

/// <summary>
/// 角色等级与经验成长领域模型
/// </summary>
public class CharacterProgression
{
    public int Level { get; private set; } = 1;
    public long CurrentExp { get; private set; } = 0;

    // 经典热血传奇升级经验阈值表 (1~45级)
    private static readonly long[] ExpTable =
    [
        0,          // 占位
        100,        // 1 -> 2
        200,        // 2 -> 3
        300,        // 3 -> 4
        400,        // 4 -> 5
        600,        // 5 -> 6
        900,        // 6 -> 7
        1200,       // 7 -> 8
        1700,       // 8 -> 9
        2500,       // 9 -> 10
        4000,       // 10 -> 11
        6000,       // 11 -> 12
        9000,       // 12 -> 13
        13000,      // 13 -> 14
        18000,      // 14 -> 15
        24000,      // 15 -> 16
        32000,      // 16 -> 17
        42000,      // 17 -> 18
        55000,      // 18 -> 19
        70000,      // 19 -> 20
        90000,      // 20 -> 21
        120000,     // 21 -> 22
        160000,     // 22 -> 23
        220000,     // 23 -> 24
        300000,     // 24 -> 25
        400000,     // 25 -> 26
        550000,     // 26 -> 27
        750000,     // 27 -> 28
        1000000,    // 28 -> 29
        1400000,    // 29 -> 30
        2000000,    // 30 -> 31
        2800000,    // 31 -> 32
        3800000,    // 32 -> 33
        5000000,    // 33 -> 34
        6500000,    // 34 -> 35 (战神/烈火)
        8500000,    // 35 -> 36
        11000000,   // 36 -> 37
        14000000,   // 37 -> 38
        18000000,   // 38 -> 39
        23000000,   // 39 -> 40 (重装天魔)
        30000000,   // 40 -> 41
        40000000,   // 41 -> 42
        55000000,   // 42 -> 43
        75000000,   // 43 -> 44
        100000000   // 44 -> 45
    ];

    public long RequiredExp => GetRequiredExpForLevel(Level);

    public float ExpPercent
    {
        get
        {
            long req = RequiredExp;
            if (req <= 0) return 1f;
            return Math.Clamp((float)CurrentExp / req, 0f, 1f);
        }
    }

    // 动态属性成长曲线
    public int MaxHp => 80 + (Level * 18);          // 1级 98, 20级 440, 35级 710, 40级 800
    public int MaxMp => 40 + (Level * 8);           // 1级 48, 20级 200, 35级 320, 40级 360
    public int BaseMinDc => 2 + (Level / 4);        // 基础物攻下限
    public int BaseMaxDc => 5 + (Level / 2);        // 基础物攻上限
    public int BaseMinAc => 1 + (Level / 7);        // 基础物防下限
    public int BaseMaxAc => 2 + (Level / 5);        // 基础物防上限

    public CharacterProgression(int initialLevel = 1, long initialExp = 0)
    {
        SetLevel(initialLevel);
        CurrentExp = initialExp;
    }

    /// <summary>
    /// 增加经验值，支持连续跨级提升
    /// </summary>
    /// <param name="exp">获得的经验数值</param>
    /// <param name="levelsGained">本次增加导致的晋升等级数</param>
    /// <returns>是否发生了等级提升</returns>
    public bool AddExp(long exp, out int levelsGained)
    {
        levelsGained = 0;
        if (exp <= 0) return false;

        CurrentExp += exp;

        while (true)
        {
            long req = RequiredExp;
            if (req <= 0 || CurrentExp < req)
            {
                break;
            }

            CurrentExp -= req;
            Level++;
            levelsGained++;
        }

        return levelsGained > 0;
    }

    /// <summary>
    /// 直接设置指定等级（用于 GM 指令或服务端初始化同步）
    /// </summary>
    public void SetLevel(int targetLevel)
    {
        Level = Math.Clamp(targetLevel, 1, 70);
        CurrentExp = 0;
    }

    public static long GetRequiredExpForLevel(int lvl)
    {
        if (lvl <= 0) return 100;
        if (lvl < ExpTable.Length)
        {
            return ExpTable[lvl];
        }

        // 超过 45 级按经典指数级上浮
        return 100000000L + ((long)(lvl - 44) * 30000000L);
    }
}
