using System.Numerics;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class HeroManagerTests
{
    public HeroManagerTests()
    {
        HeroManager.Instance.Reset();
    }

    [Fact]
    public void Test_Hero_Summon_And_Recall()
    {
        var hero = HeroManager.Instance;
        Assert.False(hero.IsSummoned);

        // 1. 召唤出战
        var summonRes = hero.SummonHero(new Vector2(330, 268));
        Assert.True(summonRes.Success);
        Assert.True(hero.IsSummoned);
        Assert.Equal(HeroState.Active, hero.State);

        // 2. 重复召唤
        var repeatRes = hero.SummonHero(new Vector2(330, 268));
        Assert.False(repeatRes.Success);

        // 3. 召回
        var recallRes = hero.RecallHero();
        Assert.True(recallRes.Success);
        Assert.False(hero.IsSummoned);
        Assert.Equal(HeroState.Unsummoned, hero.State);
    }

    [Fact]
    public void Test_Hero_Mode_Toggle()
    {
        var hero = HeroManager.Instance;
        Assert.Equal(HeroBehaviorMode.Follow, hero.Mode);

        // Follow -> Attack
        var m1 = hero.ToggleMode();
        Assert.Equal(HeroBehaviorMode.Attack, m1);

        // Attack -> Guard
        var m2 = hero.ToggleMode();
        Assert.Equal(HeroBehaviorMode.Guard, m2);

        // Guard -> Follow
        var m3 = hero.ToggleMode();
        Assert.Equal(HeroBehaviorMode.Follow, m3);
    }

    [Fact]
    public void Test_Hero_Damage_Death_And_Revive()
    {
        var hero = HeroManager.Instance;
        hero.SummonHero(new Vector2(100, 100));

        // 受击伤害
        var hit1 = hero.TakeDamage(100);
        Assert.False(hit1.Dead);
        Assert.Equal(550, hit1.RemHp);
        Assert.True(hero.Fury > 0); // 受击积攒怒气

        // 致命伤害
        var hitFatal = hero.TakeDamage(600);
        Assert.True(hitFatal.Dead);
        Assert.True(hero.IsDead);
        Assert.Equal(HeroState.Dead, hero.State);

        // 阵亡状态下不可召唤
        var failSummon = hero.SummonHero(new Vector2(100, 100));
        Assert.False(failSummon.Success);

        // 复活
        var reviveRes = hero.ReviveHero();
        Assert.True(reviveRes.Success);
        Assert.False(hero.IsDead);
        Assert.Equal(HeroState.Active, hero.State);
        Assert.Equal(hero.HeroMaxHp, hero.HeroHp);
    }

    [Fact]
    public void Test_Fury_Accumulation_And_Ready()
    {
        var hero = HeroManager.Instance;
        Assert.Equal(0, hero.Fury);
        Assert.False(hero.IsFuryReady);

        hero.AddFury(50);
        Assert.Equal(50, hero.Fury);
        Assert.False(hero.IsFuryReady);

        hero.AddFury(60); // 溢出截断至 100
        Assert.Equal(100, hero.Fury);
        Assert.True(hero.IsFuryReady);
    }

    [Fact]
    public void Test_JointSkill_Combinations()
    {
        var hero = HeroManager.Instance;

        // 1. 战战组合 -> 破魂斩
        hero.HeroJob = CharacterJob.Warrior;
        var s1 = hero.ResolveJointSkill(CharacterJob.Warrior);
        Assert.Equal(JointSkillType.SoulBreaker, s1.Type);
        Assert.Equal("破魂斩", s1.Name);

        // 2. 道道组合 -> 噬魂沼泽
        hero.HeroJob = CharacterJob.Taoist;
        var s2 = hero.ResolveJointSkill(CharacterJob.Taoist);
        Assert.Equal(JointSkillType.SoulSwamp, s2.Type);
        Assert.Equal("噬魂沼泽", s2.Name);

        // 3. 战法组合 -> 雷霆一击
        hero.HeroJob = CharacterJob.Wizard;
        var s3 = hero.ResolveJointSkill(CharacterJob.Warrior);
        Assert.Equal(JointSkillType.ThunderStrike, s3.Type);
        Assert.Equal("雷霆一击", s3.Name);

        // 4. 法道组合 -> 末日审判
        hero.HeroJob = CharacterJob.Taoist;
        var s4 = hero.ResolveJointSkill(CharacterJob.Wizard);
        Assert.Equal(JointSkillType.DoomJudgment, s4.Type);
        Assert.Equal("末日审判", s4.Name);
    }

    [Fact]
    public void Test_CastJointAttack_Fury_And_Damage()
    {
        var hero = HeroManager.Instance;
        hero.Reset();
        hero.HeroJob = CharacterJob.Warrior;
        hero.SummonHero(new Vector2(200, 200));

        // 怒气未满，施展合击失败
        hero.SetFury(80);
        var failCast = hero.CastJointAttack(CharacterJob.Warrior, 1000);
        Assert.False(failCast.Success);
        Assert.Contains("真气怒气未充盈", failCast.Message);

        // 满怒施展合击成功
        hero.SetFury(100);
        var succCast = hero.CastJointAttack(CharacterJob.Warrior, 1000, playerBaseDamage: 70);
        Assert.True(succCast.Success);
        Assert.Equal(0, hero.Fury); // 满怒消耗清零
        Assert.NotNull(succCast.SkillDef);
        Assert.True(succCast.FinalDamage > 500); // 爆发巨额伤害
        Assert.Contains("破魂斩", succCast.Message);
    }

    [Fact]
    public void Test_CommandParser_Hero_Joint_Commands()
    {
        // 1. 英雄出战
        var cmd1 = CommandParser.Parse("@hero summon");
        Assert.Equal(ParsedCommandType.Hero, cmd1.Type);
        Assert.Equal("summon", cmd1.SubAction);

        // 2. 英雄模式切换
        var cmd2 = CommandParser.Parse("@hero mode");
        Assert.Equal(ParsedCommandType.Hero, cmd2.Type);
        Assert.Equal("mode", cmd2.SubAction);

        // 3. 释放合击绝技
        var cmd3 = CommandParser.Parse("@joint");
        Assert.Equal(ParsedCommandType.Joint, cmd3.Type);

        // 4. 充能怒气
        var cmd4 = CommandParser.Parse("@fury 100");
        Assert.Equal(ParsedCommandType.Fury, cmd4.Type);
        Assert.Equal(100, cmd4.Amount);
    }
}
