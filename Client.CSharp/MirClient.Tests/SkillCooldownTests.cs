using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class SkillCooldownTests
{
    [Fact]
    public void TestInitialState_CanCast()
    {
        SkillCooldownManager cd = new();
        Assert.True(cd.CanCast(SkillType.FireSword, out float rem));
        Assert.Equal(0f, rem);
        Assert.Equal(0f, cd.GetCooldownProgress(SkillType.FireSword));
        Assert.Equal(0f, cd.GlobalCooldown);
    }

    [Fact]
    public void TestTriggerCooldown_And_RemainTime()
    {
        SkillCooldownManager cd = new();
        cd.TriggerCooldown(SkillType.FireSword); // 10.0s 冷却

        Assert.False(cd.CanCast(SkillType.FireSword, out float rem));
        Assert.Equal(10.0f, rem, precision: 2);
        Assert.Equal(1.0f, cd.GetCooldownProgress(SkillType.FireSword), precision: 2);
        Assert.Equal(SkillCooldownManager.GCD_DURATION, cd.GlobalCooldown, precision: 2);
    }

    [Fact]
    public void TestUpdate_CooldownDecay_And_Ready()
    {
        SkillCooldownManager cd = new();
        cd.TriggerCooldown(SkillType.ThunderBolt); // 1.2s 冷却

        // 推进 0.6 秒
        cd.Update(0.6f);
        Assert.False(cd.CanCast(SkillType.ThunderBolt, out float rem1));
        Assert.Equal(0.6f, rem1, precision: 2);
        Assert.Equal(0.5f, cd.GetCooldownProgress(SkillType.ThunderBolt), precision: 2);

        // 再推进 0.7 秒 (总计 1.3 秒，已超过 1.2 秒)
        cd.Update(0.7f);
        Assert.True(cd.CanCast(SkillType.ThunderBolt, out float rem2));
        Assert.Equal(0f, rem2);
        Assert.Equal(0f, cd.GetCooldownProgress(SkillType.ThunderBolt));
    }

    [Fact]
    public void TestGlobalCooldown_GCD()
    {
        SkillCooldownManager cd = new();
        cd.TriggerCooldown(SkillType.ThunderBolt); // 带来 0.4s GCD

        // 虽然治愈术自身没有独立冷却，但在 GCD (0.4s) 内同样被拦截
        Assert.False(cd.CanCast(SkillType.Healing, out float gcdRem));
        Assert.True(gcdRem > 0f && gcdRem <= SkillCooldownManager.GCD_DURATION);

        // 推进 0.45 秒 (GCD 结束)
        cd.Update(0.45f);
        Assert.True(cd.CanCast(SkillType.Healing, out _));
        // 但雷电术自身独立冷却还剩 1.2 - 0.45 = 0.75s，依然不能放
        Assert.False(cd.CanCast(SkillType.ThunderBolt, out _));
    }

    [Fact]
    public void TestResetAll_InstantReady()
    {
        SkillCooldownManager cd = new();
        cd.TriggerCooldown(SkillType.FireSword);
        cd.TriggerCooldown(SkillType.Healing);

        Assert.False(cd.CanCast(SkillType.FireSword, out _));
        Assert.False(cd.CanCast(SkillType.Healing, out _));

        // 一键清空重置
        cd.ResetAll();

        Assert.True(cd.CanCast(SkillType.FireSword, out _));
        Assert.True(cd.CanCast(SkillType.Healing, out _));
        Assert.Equal(0f, cd.GlobalCooldown);
    }
}
