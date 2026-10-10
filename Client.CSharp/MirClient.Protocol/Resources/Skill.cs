namespace MirClient.Protocol.Resources;

public enum SkillType
{
    ThunderBolt = 1, // 雷电术 (法师天降雷霆)
    FireSword = 2,   // 烈火剑法 (战士火焰爆发)
    Healing = 3,     // 治愈术 (道士神圣光环)
    Fireball = 4     // 火球术 (法师飞行弹道)
}

/// <summary>
/// 传奇经典技能法术定义
/// </summary>
public class Skill
{
    public SkillType Type { get; set; }
    public string Name { get; set; } = "";
    public int RequiredMp { get; set; }
    public float CooldownSeconds { get; set; }
    public int MinPower { get; set; }
    public int MaxPower { get; set; }

    public static Skill CreateThunderBolt() => new()
    {
        Type = SkillType.ThunderBolt,
        Name = "雷电术",
        RequiredMp = 15,
        CooldownSeconds = 0.8f,
        MinPower = 45,
        MaxPower = 75
    };

    public static Skill CreateFireSword() => new()
    {
        Type = SkillType.FireSword,
        Name = "烈火剑法",
        RequiredMp = 20,
        CooldownSeconds = 3.0f,
        MinPower = 35,
        MaxPower = 60
    };

    public static Skill CreateHealing() => new()
    {
        Type = SkillType.Healing,
        Name = "治愈术",
        RequiredMp = 12,
        CooldownSeconds = 1.0f,
        MinPower = 60,
        MaxPower = 60
    };

    public static Skill CreateFireball() => new()
    {
        Type = SkillType.Fireball,
        Name = "火球术",
        RequiredMp = 8,
        CooldownSeconds = 0.5f,
        MinPower = 25,
        MaxPower = 40
    };

    public static Skill GetSkill(SkillType type) => type switch
    {
        SkillType.ThunderBolt => CreateThunderBolt(),
        SkillType.FireSword => CreateFireSword(),
        SkillType.Healing => CreateHealing(),
        SkillType.Fireball => CreateFireball(),
        _ => CreateFireball()
    };
}
