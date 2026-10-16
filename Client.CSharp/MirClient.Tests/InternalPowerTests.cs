using System.Collections.Generic;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class InternalPowerTests
{
    public InternalPowerTests()
    {
        InternalPowerManager.Instance.Reset();
    }

    [Fact]
    public void Test_InitialState_And_DefaultValues()
    {
        var mgr = InternalPowerManager.Instance;
        Assert.Equal(1, mgr.InternalLevel);
        Assert.Equal(100, mgr.CurrentForce);
        Assert.Equal(100, mgr.MaxForce);
        Assert.Equal(0f, mgr.ComboCooldownTimer);
        Assert.True(mgr.CanCastCombo);
        Assert.Equal(1, mgr.UnlockedComboStages);
        Assert.True(mgr.AbsorptionRatio >= 0.25f);
    }

    [Fact]
    public void Test_DamageAbsorption_ReducesForce_And_ProtectsHp()
    {
        var mgr = InternalPowerManager.Instance;
        mgr.CurrentForce = 100;

        // 受到 100 点伤害，吸收率约 25% (25点由内力化解，75点扣血)
        var (damageToHp, absorbed) = mgr.AbsorbDamage(100);

        Assert.Equal(25, absorbed);
        Assert.Equal(75, damageToHp);
        Assert.Equal(75, mgr.CurrentForce);

        // 当内力仅剩 10 点时受到 100 点伤害，最多只能吸收 10 点
        mgr.CurrentForce = 10;
        var (damageToHp2, absorbed2) = mgr.AbsorbDamage(100);
        Assert.Equal(10, absorbed2);
        Assert.Equal(90, damageToHp2);
        Assert.Equal(0, mgr.CurrentForce);
    }

    [Fact]
    public void Test_ZeroForce_NoAbsorption()
    {
        var mgr = InternalPowerManager.Instance;
        mgr.CurrentForce = 0;

        var (damageToHp, absorbed) = mgr.AbsorbDamage(200);
        Assert.Equal(0, absorbed);
        Assert.Equal(200, damageToHp);
    }

    [Fact]
    public void Test_GainExp_And_LevelUp()
    {
        var mgr = InternalPowerManager.Instance;
        int expNeeded = mgr.MaxExp;

        var (leveled, msg) = mgr.AddExp(expNeeded + 50);
        Assert.True(leveled);
        Assert.Equal(2, mgr.InternalLevel);
        Assert.True(mgr.MaxForce > 100);
        Assert.Equal(mgr.MaxForce, mgr.CurrentForce); // 升级内力回满
        Assert.Contains("突破", msg);
    }

    [Fact]
    public void Test_PierceAcupoint_UnlocksComboStages()
    {
        var mgr = InternalPowerManager.Instance;
        Assert.Equal(1, mgr.UnlockedComboStages);

        // 督脉打通 5 处大穴
        for (int i = 0; i < 5; i++)
        {
            mgr.PierceAcupoint(MeridianType.Du);
        }
        Assert.Equal(2, mgr.UnlockedComboStages);

        // 任脉打通 5 处大穴
        for (int i = 0; i < 5; i++)
        {
            mgr.PierceAcupoint(MeridianType.Ren);
        }
        Assert.Equal(3, mgr.UnlockedComboStages);

        // 带脉打通 5 处大穴
        for (int i = 0; i < 5; i++)
        {
            mgr.PierceAcupoint(MeridianType.Dai);
        }
        Assert.Equal(4, mgr.UnlockedComboStages);
    }

    [Fact]
    public void Test_CastCombo_Warrior_Wizard_Taoist()
    {
        var mgr = InternalPowerManager.Instance;
        mgr.UnlockAllMeridians(); // 解锁全部 4 段连击
        mgr.CurrentForce = 200;

        // 战士连击测试
        var (wSuccess, wMsg, wHits, wDmg) = mgr.CastCombo(CharacterJob.Warrior, 100, true);
        Assert.True(wSuccess);
        Assert.Equal(4, wHits.Count);
        Assert.Equal("断岳斩", wHits[0].SkillName);
        Assert.Equal("横扫千军", wHits[1].SkillName);
        Assert.Equal("破盾神斩", wHits[2].SkillName);
        Assert.Equal("追魂刺", wHits[3].SkillName);
        Assert.True(wDmg > 0);

        // 冷却中再次施放应被拦截
        var (failSuccess, failMsg, _, _) = mgr.CastCombo(CharacterJob.Warrior, 100, true);
        Assert.False(failSuccess);
        Assert.Contains("调息中", failMsg);

        // 清空冷却后测试法师
        mgr.ComboCooldownTimer = 0f;
        mgr.CurrentForce = 200;
        var (mSuccess, _, mHits, _) = mgr.CastCombo(CharacterJob.Wizard, 100, true);
        Assert.True(mSuccess);
        Assert.Equal("双龙破", mHits[0].SkillName);
        Assert.Equal("极光炼狱", mHits[3].SkillName);

        // 清空冷却后测试道士
        mgr.ComboCooldownTimer = 0f;
        mgr.CurrentForce = 200;
        var (tSuccess, _, tHits, _) = mgr.CastCombo(CharacterJob.Taoist, 100, true);
        Assert.True(tSuccess);
        Assert.Equal("虎啸诀", tHits[0].SkillName);
        Assert.Equal("万剑归宗", tHits[3].SkillName);
    }

    [Fact]
    public void Test_ComboCooldown_And_Update()
    {
        var mgr = InternalPowerManager.Instance;
        mgr.UnlockAllMeridians();
        mgr.CurrentForce = 100;

        var (success, _, _, _) = mgr.CastCombo(CharacterJob.Warrior, 100, true);
        Assert.True(success);
        Assert.True(mgr.ComboCooldownTimer > 0f);
        Assert.False(mgr.CanCastCombo);

        // 时间流逝 15 秒
        mgr.Update(15.0f);
        Assert.Equal(0f, mgr.ComboCooldownTimer);
        // 且内力在 update 中自然恢复
        Assert.True(mgr.CurrentForce > 0);
    }

    [Fact]
    public void Test_InternalCommandParsing()
    {
        var cmd1 = CommandParser.Parse("@internal lvl 35");
        Assert.Equal(ParsedCommandType.Internal, cmd1.Type);
        Assert.Equal("lvl", cmd1.SubAction);
        Assert.Equal(35, cmd1.Amount);

        var cmd2 = CommandParser.Parse("@internal force 300");
        Assert.Equal(ParsedCommandType.Internal, cmd2.Type);
        Assert.Equal("force", cmd2.SubAction);
        Assert.Equal(300, cmd2.Amount);

        var cmd3 = CommandParser.Parse("@combo cast");
        Assert.Equal(ParsedCommandType.Combo, cmd3.Type);
        Assert.Equal("cast", cmd3.SubAction);

        var cmd4 = CommandParser.Parse("@combo unlock");
        Assert.Equal(ParsedCommandType.Combo, cmd4.Type);
        Assert.Equal("unlock", cmd4.SubAction);
    }
}
