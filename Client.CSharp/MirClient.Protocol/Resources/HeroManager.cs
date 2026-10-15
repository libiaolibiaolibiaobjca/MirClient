using System.Numerics;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 英雄状态
/// </summary>
public enum HeroState
{
    Unsummoned = 0, // 庇护收回中
    Active = 1,     // 出战并肩作战
    Dead = 2        // 阵亡
}

/// <summary>
/// 英雄随从行为模式
/// </summary>
public enum HeroBehaviorMode
{
    Follow = 0, // 跟随模式 (紧贴主人，寸步不离)
    Attack = 1, // 攻击模式 (主动集火主人锁定的强敌)
    Guard = 2   // 守护模式 (驻守原地警惕周围)
}

/// <summary>
/// 盛大传奇十周年经典合击绝技类型
/// </summary>
public enum JointSkillType
{
    SoulBreaker,  // 破魂斩 (战战)
    SoulSwamp,    // 噬魂沼泽 (道道)
    ThunderStrike,// 雷霆一击 (战法 / 法战)
    StarCutter,   // 劈星斩 (战道 / 道战)
    DoomJudgment, // 末日审判 (法道 / 道法)
    DragonFlame   // 火龙气焰 (法法)
}

/// <summary>
/// 合击绝技详细属性
/// </summary>
public record JointSkillDef(JointSkillType Type, string Name, float Multiplier, string Description);

/// <summary>
/// 盛大传奇十周年：英雄随从伙伴与怒气合击状态管理器 (纯 C# 领域状态机)
/// </summary>
public class HeroManager
{
    private static HeroManager? _instance;
    public static HeroManager Instance => _instance ??= new HeroManager();

    public const int MaxFury = 100;

    public string HeroName { get; set; } = "武魂战将";
    public CharacterJob HeroJob { get; set; } = CharacterJob.Warrior;
    public int HeroLevel { get; set; } = 35;
    public int HeroHp { get; set; } = 650;
    public int HeroMaxHp { get; set; } = 650;
    public int HeroMp { get; set; } = 300;
    public int HeroMaxMp { get; set; } = 300;
    public Vector2 HeroTilePos { get; set; } = new(332, 269);
    public int HeroDirection { get; set; } = 4;

    public HeroState State { get; private set; } = HeroState.Unsummoned;
    public HeroBehaviorMode Mode { get; private set; } = HeroBehaviorMode.Follow;
    public int Fury { get; private set; } = 0;

    public bool IsSummoned => State == HeroState.Active;
    public bool IsDead => State == HeroState.Dead;
    public bool IsFuryReady => Fury >= MaxFury;

    public event Action<HeroState>? OnHeroStateChanged;
    public event Action<HeroBehaviorMode>? OnHeroModeChanged;
    public event Action<int>? OnFuryChanged;

    public void Reset()
    {
        State = HeroState.Unsummoned;
        Mode = HeroBehaviorMode.Follow;
        Fury = 0;
        HeroName = "武魂战将";
        HeroJob = CharacterJob.Warrior;
        HeroLevel = 35;
        HeroHp = HeroMaxHp;
        HeroMp = HeroMaxMp;
    }

    public (bool Success, string Message) SummonHero(Vector2 playerTile)
    {
        if (State == HeroState.Dead)
            return (false, "英雄已在激战中阵亡，请先在武馆处复活或施展还魂秘药！");

        if (State == HeroState.Active)
            return (false, "英雄已在身边并肩作战。");

        State = HeroState.Active;
        HeroTilePos = playerTile + new Vector2(1, 0); // 召唤于主人身侧
        OnHeroStateChanged?.Invoke(State);
        return (true, $"【{HeroName}】应召踏碎虚空降临！与主人并肩征战玛法！");
    }

    public (bool Success, string Message) RecallHero()
    {
        if (State == HeroState.Unsummoned)
            return (false, "英雄当前尚未召唤出战。");

        State = HeroState.Unsummoned;
        OnHeroStateChanged?.Invoke(State);
        return (true, $"【{HeroName}】已化作真气流光隐入元神空间休养生息。");
    }

    public HeroBehaviorMode ToggleMode()
    {
        Mode = Mode switch
        {
            HeroBehaviorMode.Follow => HeroBehaviorMode.Attack,
            HeroBehaviorMode.Attack => HeroBehaviorMode.Guard,
            _                       => HeroBehaviorMode.Follow
        };

        OnHeroModeChanged?.Invoke(Mode);
        return Mode;
    }

    public void SetMode(HeroBehaviorMode newMode)
    {
        Mode = newMode;
        OnHeroModeChanged?.Invoke(Mode);
    }

    public void AddFury(int amount)
    {
        if (amount == 0) return;
        Fury = Math.Clamp(Fury + amount, 0, MaxFury);
        OnFuryChanged?.Invoke(Fury);
    }

    public void SetFury(int amount)
    {
        Fury = Math.Clamp(amount, 0, MaxFury);
        OnFuryChanged?.Invoke(Fury);
    }

    public (bool Dead, int RemHp) TakeDamage(int damage)
    {
        if (State != HeroState.Active) return (false, HeroHp);

        HeroHp = Math.Max(0, HeroHp - damage);
        AddFury(3); // 受击增加怒气

        if (HeroHp <= 0)
        {
            State = HeroState.Dead;
            OnHeroStateChanged?.Invoke(State);
            return (true, 0);
        }

        return (false, HeroHp);
    }

    public (bool Success, string Message) ReviveHero()
    {
        HeroHp = HeroMaxHp;
        HeroMp = HeroMaxMp;
        State = HeroState.Active;
        OnHeroStateChanged?.Invoke(State);
        return (true, $"神圣还魂符生效！英雄【{HeroName}】满血复苏，重返玛法战场！");
    }

    public JointSkillDef ResolveJointSkill(CharacterJob playerJob)
    {
        // 根据主角与副将英雄职业组合裁定
        if (playerJob == CharacterJob.Warrior && HeroJob == CharacterJob.Warrior)
        {
            return new JointSkillDef(JointSkillType.SoulBreaker, "破魂斩", 3.8f, "战战合一，劈天裂地！爆发380%毁天灭地极限物理斩击！");
        }
        if (playerJob == CharacterJob.Taoist && HeroJob == CharacterJob.Taoist)
        {
            return new JointSkillDef(JointSkillType.SoulSwamp, "噬魂沼泽", 3.2f, "道道共鸣，幽冥降临！全屏绿毒剧毒沼泽持续侵蚀！");
        }
        if ((playerJob == CharacterJob.Warrior && HeroJob == CharacterJob.Wizard) ||
            (playerJob == CharacterJob.Wizard && HeroJob == CharacterJob.Warrior))
        {
            return new JointSkillDef(JointSkillType.ThunderStrike, "雷霆一击", 3.5f, "战法交融，万钧天雷击碎护甲并眩晕震退强敌！");
        }
        if ((playerJob == CharacterJob.Warrior && HeroJob == CharacterJob.Taoist) ||
            (playerJob == CharacterJob.Taoist && HeroJob == CharacterJob.Warrior))
        {
            return new JointSkillDef(JointSkillType.StarCutter, "劈星斩", 3.4f, "刀气附着玄门九字真言，化作星芒贯穿敌阵！");
        }
        if ((playerJob == CharacterJob.Wizard && HeroJob == CharacterJob.Taoist) ||
            (playerJob == CharacterJob.Taoist && HeroJob == CharacterJob.Wizard))
        {
            return new JointSkillDef(JointSkillType.DoomJudgment, "末日审判", 3.6f, "冰火两重天与乾坤借法，降下裁决天火天罚！");
        }

        // 法法
        return new JointSkillDef(JointSkillType.DragonFlame, "火龙气焰", 3.7f, "双法聚气化出灭世火龙，席卷八荒焚尽万物！");
    }

    /// <summary>
    /// 触发合击绝技释放 (消耗 100 点怒气)
    /// </summary>
    public (bool Success, string Message, int FinalDamage, JointSkillDef? SkillDef) CastJointAttack(
        CharacterJob playerJob, 
        int targetCurrentHp, 
        int playerBaseDamage = 60)
    {
        if (State != HeroState.Active)
            return (false, "英雄随从尚未召唤出战，无法施展二人合击绝技！", 0, null);

        if (Fury < MaxFury)
            return (false, $"真气怒气未充盈！当前怒气: {Fury}/{MaxFury} (按 Ctrl+S 施展需满怒)", 0, null);

        var skill = ResolveJointSkill(playerJob);
        Fury = 0; // 消耗全部怒气
        OnFuryChanged?.Invoke(Fury);

        int baseDmg = playerBaseDamage + (HeroLevel * 8);
        int finalDamage = (int)(baseDmg * skill.Multiplier);

        string msg = $"【合击绝技】主将与英雄心神合一，震撼施展【{skill.Name}】！造成 {finalDamage} 点毁灭暴击伤害！({skill.Description})";
        return (true, msg, finalDamage, skill);
    }

    /// <summary>
    /// 英雄随从自主 AI 推进 (跟随、攻击、护卫)
    /// </summary>
    public bool UpdateAi(
        float dt, 
        Vector2 playerPos, 
        Vector2? targetEnemyPos, 
        out Vector2 moveStep, 
        out bool attackTriggered, 
        out int attackDmg)
    {
        moveStep = Vector2.Zero;
        attackTriggered = false;
        attackDmg = 0;

        if (State != HeroState.Active) return false;

        float distToPlayer = Vector2.Distance(HeroTilePos, playerPos);

        // 1. 若距离主人过远 (> 10格)，强行瞬移拉回身侧
        if (distToPlayer > 10.0f)
        {
            HeroTilePos = playerPos + new Vector2(1, 0);
            return true;
        }

        // 2. 行为模式分支决策
        if (Mode == HeroBehaviorMode.Follow || targetEnemyPos == null)
        {
            // 跟随模式：保持在主人身旁 1.5 ~ 2 格之内
            if (distToPlayer > 2.0f)
            {
                Vector2 dir = Vector2.Normalize(playerPos - HeroTilePos);
                moveStep = dir * 2.5f * dt;
                HeroTilePos += moveStep;
            }
        }
        else if (Mode == HeroBehaviorMode.Attack && targetEnemyPos.HasValue)
        {
            // 攻击模式：向目标敌人靠近并挥刀
            Vector2 enemyPos = targetEnemyPos.Value;
            float distToEnemy = Vector2.Distance(HeroTilePos, enemyPos);
            if (distToEnemy > 1.2f)
            {
                Vector2 dir = Vector2.Normalize(enemyPos - HeroTilePos);
                moveStep = dir * 3.0f * dt;
                HeroTilePos += moveStep;
            }
            else
            {
                // 贴身普攻
                attackTriggered = true;
                attackDmg = Random.Shared.Next(25, 45);
                AddFury(4); // 英雄挥刀蓄积怒气
            }
        }

        return true;
    }
}
