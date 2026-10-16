using System;
using System.Collections.Generic;
using System.Numerics;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 奇经八脉核心经络类型
/// </summary>
public enum MeridianType
{
    Chong, // 冲脉 (贯通领悟第1连击，提升暴击率与内功穿透)
    Du,    // 督脉 (贯通领悟第2连击，提升内功攻击上限)
    Ren,   // 任脉 (贯通领悟第3连击，提升内功防御与最大内力)
    Dai    // 带脉 (贯通领悟第4连击，大幅提升终结爆发斩杀)
}

/// <summary>
/// 连击绝学技能定义
/// </summary>
public record ComboSkillDef(
    int Stage, 
    string Name, 
    int CostForce, 
    int DamageMultiplierPercent, 
    string Description);

/// <summary>
/// 连击单段命中结算结果
/// </summary>
public record ComboHitResult(
    int Stage, 
    string SkillName, 
    int Damage, 
    bool IsCritical, 
    string FxName);

/// <summary>
/// 盛大热血传奇十周年：内功经络与连击绝学管理器 (纯 C# 领域驱动模型)
/// </summary>
public class InternalPowerManager
{
    private static InternalPowerManager? _instance;
    public static InternalPowerManager Instance => _instance ??= new InternalPowerManager();

    public const int MaxLevel = 99;
    public const int AcupointsPerMeridian = 5;
    public const float ComboCooldownMax = 15.0f; // 连击绝学全套 15 秒冷却

    // 内功基础属性
    public int InternalLevel { get; set; } = 1;
    public int InternalExp { get; set; } = 0;
    public int CurrentForce { get; set; } = 100;
    public float ComboCooldownTimer { get; set; } = 0f;

    // 四大经脉冲穴打通计数 (0 ~ 5)
    private readonly Dictionary<MeridianType, int> _meridians = new()
    {
        [MeridianType.Chong] = 5, // 默认开启冲脉
        [MeridianType.Du]    = 0,
        [MeridianType.Ren]   = 0,
        [MeridianType.Dai]   = 0
    };

    public IReadOnlyDictionary<MeridianType, int> Meridians => _meridians;

    // 最大内力值 (基础 100 + 每级 15 + 任脉穴位加成)
    public int MaxForce
    {
        get
        {
            int baseForce = 100 + (InternalLevel - 1) * 15;
            int renBonus = _meridians[MeridianType.Ren] * 30;
            return baseForce + renBonus;
        }
    }

    // 内力护盾伤害吸收比例 (默认 30% 伤害由内力化解)
    public float AbsorptionRatio => Math.Clamp(0.25f + (InternalLevel * 0.002f), 0.25f, 0.45f);

    // 内功攻击力加成 (督脉与等级)
    public int InternalAttack => (InternalLevel * 3) + (_meridians[MeridianType.Du] * 12);

    // 内功防御力加成 (任脉与等级)
    public int InternalDefense => (InternalLevel * 2) + (_meridians[MeridianType.Ren] * 8);

    // 升级所需内功经验
    public int MaxExp => InternalLevel * 1000 + (InternalLevel * InternalLevel * 100);

    // 连击施放条件判定
    public bool CanCastCombo => ComboCooldownTimer <= 0f && CurrentForce >= 40 && UnlockedComboStages >= 1;

    // 当前已解锁的最高连击段数 (1 ~ 4)
    public int UnlockedComboStages
    {
        get
        {
            int stages = 0;
            if (_meridians[MeridianType.Chong] >= AcupointsPerMeridian) stages++;
            if (_meridians[MeridianType.Du] >= AcupointsPerMeridian) stages++;
            if (_meridians[MeridianType.Ren] >= AcupointsPerMeridian) stages++;
            if (_meridians[MeridianType.Dai] >= AcupointsPerMeridian) stages++;
            return Math.Max(1, stages); // 初始保底第 1 段
        }
    }

    public void Reset()
    {
        InternalLevel = 1;
        InternalExp = 0;
        CurrentForce = 100;
        ComboCooldownTimer = 0f;
        _meridians[MeridianType.Chong] = 5;
        _meridians[MeridianType.Du] = 0;
        _meridians[MeridianType.Ren] = 0;
        _meridians[MeridianType.Dai] = 0;
    }

    /// <summary>
    /// 时间更新 (冷却流逝与脱战内力恢复)
    /// </summary>
    public void Update(float dt)
    {
        if (ComboCooldownTimer > 0f)
        {
            ComboCooldownTimer = Math.Max(0f, ComboCooldownTimer - dt);
        }

        // 每秒自发恢复少量内力
        if (CurrentForce < MaxForce)
        {
            CurrentForce = Math.Min(MaxForce, CurrentForce + (int)(dt * 4));
        }
    }

    /// <summary>
    /// 核心内力护盾机制：受到外部创伤时，优先由内力吸收抵消一部分气血伤害
    /// </summary>
    public (int DamageToHp, int AbsorbedByForce) AbsorbDamage(int rawDamage)
    {
        if (rawDamage <= 0 || CurrentForce <= 0)
        {
            return (rawDamage, 0);
        }

        int targetAbsorb = (int)MathF.Round(rawDamage * AbsorptionRatio);
        int actualAbsorb = Math.Min(targetAbsorb, CurrentForce);

        CurrentForce -= actualAbsorb;
        int remainingDamage = Math.Max(0, rawDamage - actualAbsorb);

        return (remainingDamage, actualAbsorb);
    }

    /// <summary>
    /// 获得内功经验值与升级判定
    /// </summary>
    public (bool LeveledUp, string Message) AddExp(int exp)
    {
        if (InternalLevel >= MaxLevel)
        {
            return (false, "内功境界已臻至九九归一巅峰圆满！");
        }

        InternalExp += exp;
        bool leveled = false;
        while (InternalExp >= MaxExp && InternalLevel < MaxLevel)
        {
            InternalExp -= MaxExp;
            InternalLevel++;
            CurrentForce = MaxForce; // 升级内力全部盈满
            leveled = true;
        }

        string msg = leveled 
            ? $"【内功突破】内力激荡突破至 Lv.{InternalLevel}！内力上限提升至 {MaxForce}，吸收抵伤率达 {AbsorptionRatio:P0}！"
            : $"获得内功经验 +{exp} ({InternalExp}/{MaxExp})";

        return (leveled, msg);
    }

    /// <summary>
    /// 冲穴通络：按顺序打通穴位
    /// </summary>
    public (bool Success, string Message) PierceAcupoint(MeridianType meridian)
    {
        int cur = _meridians[meridian];
        if (cur >= AcupointsPerMeridian)
        {
            return (false, $"【{GetMeridianName(meridian)}】已经全脉贯通，穴位已达大圆满！");
        }

        _meridians[meridian] = cur + 1;
        bool completed = _meridians[meridian] == AcupointsPerMeridian;
        string mName = GetMeridianName(meridian);

        if (completed)
        {
            int unlockedStage = UnlockedComboStages;
            return (true, $"【经脉大圆满】轰然通达【{mName}】全脉！成功领悟第 {unlockedStage} 重连击绝学！");
        }
        else
        {
            return (true, $"【冲穴通脉】成功打通【{mName}】第 {_meridians[meridian]}/{AcupointsPerMeridian} 处大穴，内功属性大幅强化！");
        }
    }

    /// <summary>
    /// GM 指令：一键打通全部奇经八脉并解锁四重连击
    /// </summary>
    public void UnlockAllMeridians()
    {
        _meridians[MeridianType.Chong] = AcupointsPerMeridian;
        _meridians[MeridianType.Du] = AcupointsPerMeridian;
        _meridians[MeridianType.Ren] = AcupointsPerMeridian;
        _meridians[MeridianType.Dai] = AcupointsPerMeridian;
        CurrentForce = MaxForce;
    }

    public static string GetMeridianName(MeridianType meridian) => meridian switch
    {
        MeridianType.Chong => "冲脉 (极速破军)",
        MeridianType.Du    => "督脉 (纯阳杀伐)",
        MeridianType.Ren   => "任脉 (气海护体)",
        MeridianType.Dai   => "带脉 (终极贯穿)",
        _ => "无名经脉"
    };

    /// <summary>
    /// 获取战法道三大职业对应的 4 段连击绝学定义
    /// </summary>
    public static IReadOnlyList<ComboSkillDef> GetComboSkillTree(CharacterJob job) => job switch
    {
        CharacterJob.Warrior =>
        [
            new ComboSkillDef(1, "断岳斩", 10, 150, "运浑厚真气聚于刀锋，开山断岳"),
            new ComboSkillDef(2, "横扫千军", 10, 180, "周身 360 度烈火狂卷，横扫敌群并击退目标"),
            new ComboSkillDef(3, "破盾神斩", 10, 220, "无视一切魔法护盾与护甲，造成致命贯穿伤"),
            new ComboSkillDef(4, "追魂刺", 10, 300, "战意滔天突进刺穿，造成巨额内功斩杀神圣伤害")
        ],
        CharacterJob.Wizard =>
        [
            new ComboSkillDef(1, "双龙破", 10, 160, "左右双掌引动两条烈焰神龙轰击强敌"),
            new ComboSkillDef(2, "惊雷暴", 10, 190, "九天玄雷自虚空急坠，轰顶震撼全场"),
            new ComboSkillDef(3, "冰霜雪雨", 10, 240, "极寒霜雪大范围覆盖降临，暴雪急冻"),
            new ComboSkillDef(4, "极光炼狱", 10, 320, "召唤炼狱极光破空终结爆轰，天翻地覆")
        ],
        CharacterJob.Taoist =>
        [
            new ComboSkillDef(1, "虎啸诀", 10, 150, "凝聚神圣白虎真灵发出震颤百兽之啸"),
            new ComboSkillDef(2, "八卦掌", 10, 180, "阴阳二气化作无极八卦虚影击飞敌群"),
            new ComboSkillDef(3, "三清伏魔", 10, 220, "三清神符破空疾落，引动天雷道火连环伏魔"),
            new ComboSkillDef(4, "万剑归宗", 10, 310, "以气驭剑，化生万道纯阳真剑贯穿斩杀")
        ],
        _ =>
        [
            new ComboSkillDef(1, "连环猛击", 10, 150, "基础连招第一式"),
            new ComboSkillDef(2, "狂澜连击", 10, 180, "基础连招第二式"),
            new ComboSkillDef(3, "霸道重锤", 10, 220, "基础连招第三式"),
            new ComboSkillDef(4, "终结一击", 10, 300, "基础连招第四式")
        ]
    };

    /// <summary>
    /// 施展职业连击绝学，依次触发当前已解锁的所有连击段数
    /// </summary>
    public (bool Success, string Message, List<ComboHitResult> Hits, int TotalDamage) CastCombo(
        CharacterJob job, 
        int basePower, 
        bool isTargetAlive = true)
    {
        if (!isTargetAlive)
        {
            return (false, "眼前无有效敌对目标，无法发动连击绝学！", [], 0);
        }

        if (ComboCooldownTimer > 0f)
        {
            return (false, $"连击绝学尚在回气调息中，剩余 {ComboCooldownTimer:F1} 秒！", [], 0);
        }

        int maxStages = UnlockedComboStages;
        int requiredForce = maxStages * 10;
        if (CurrentForce < requiredForce)
        {
            return (false, $"内力不足！施展 {maxStages} 段连击需要 {requiredForce} 点内力 (当前内力: {CurrentForce}/{MaxForce})！", [], 0);
        }

        // 消耗内力并启动全套连招冷却
        CurrentForce -= requiredForce;
        ComboCooldownTimer = ComboCooldownMax;

        var skills = GetComboSkillTree(job);
        var hits = new List<ComboHitResult>();
        int totalDamage = 0;

        for (int i = 0; i < maxStages && i < skills.Count; i++)
        {
            var def = skills[i];
            // 伤害计算：基础威力 * 倍率 + 额外内功攻击加成
            int rawDmg = (int)(basePower * (def.DamageMultiplierPercent / 100.0f)) + InternalAttack;
            // 暴击判定：随着连击段数推进递增，第 4 招 50% 暴击
            bool isCrit = (Random.Shared.Next(100) < (15 + i * 12));
            int hitDmg = isCrit ? (int)(rawDmg * 1.5f) : rawDmg;

            totalDamage += hitDmg;
            string fx = $"Skill_{job}_Combo{i + 1}";
            hits.Add(new ComboHitResult(i + 1, def.Name, hitDmg, isCrit, fx));
        }

        string summary = $"【连击爆发】势若奔雷施展 {hits.Count} 重绝学！总计轰出 {totalDamage} 点内功贯穿伤害！";
        return (true, summary, hits, totalDamage);
    }
}
