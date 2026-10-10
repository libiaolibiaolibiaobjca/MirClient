using System.Numerics;
using MirClient.Protocol.Resources;
using MirClient.Runner.Graphics;
using Xunit;

namespace MirClient.Tests;

public class SkillTests
{
    [Fact]
    public void TestSkillDefinitions_RequiredMpAndPower()
    {
        var thunder = Skill.CreateThunderBolt();
        Assert.Equal(SkillType.ThunderBolt, thunder.Type);
        Assert.Equal(15, thunder.RequiredMp);
        Assert.True(thunder.MinPower >= 40 && thunder.MaxPower <= 80);

        var fireSword = Skill.CreateFireSword();
        Assert.Equal(SkillType.FireSword, fireSword.Type);
        Assert.Equal(20, fireSword.RequiredMp);

        var heal = Skill.CreateHealing();
        Assert.Equal(12, heal.RequiredMp);
        Assert.Equal(60, heal.MinPower);

        var fireball = Skill.CreateFireball();
        Assert.Equal(8, fireball.RequiredMp);
    }

    [Fact]
    public void TestSkillEffectManager_LifeCycle()
    {
        SkillEffectManager manager = new();
        Assert.Equal(0, manager.ActiveEffectCount);

        // 添加雷电特效 (时效 0.45 秒)
        ThunderEffect thunder = new(new Vector2(330, 268));
        manager.AddEffect(thunder);
        Assert.Equal(1, manager.ActiveEffectCount);

        // 步进 0.2 秒，仍存活
        manager.Update(0.2f);
        Assert.Equal(1, manager.ActiveEffectCount);
        Assert.False(thunder.IsFinished);

        // 再次步进 0.3 秒，累计 0.5 秒，应已完结并被剔除
        manager.Update(0.3f);
        Assert.Equal(0, manager.ActiveEffectCount);
        Assert.True(thunder.IsFinished);
    }

    [Fact]
    public void TestFireball_FlightAndExplodePhases()
    {
        bool hitCalled = false;
        FireballEffect fireball = new(new Vector2(330, 268), new Vector2(332, 268), () => hitCalled = true);
        Assert.False(fireball.IsFinished);
        Assert.False(hitCalled);

        // 飞行阶段 (0.2 秒 < 0.35 秒)
        fireball.Update(0.2f);
        Assert.False(fireball.IsFinished);
        Assert.False(hitCalled);

        // 命中进入爆炸阶段
        fireball.Update(0.2f);
        Assert.True(hitCalled);
        Assert.False(fireball.IsFinished);

        // 累计超过 0.6 秒特效完结
        fireball.Update(0.3f);
        Assert.True(fireball.IsFinished);
    }
}
