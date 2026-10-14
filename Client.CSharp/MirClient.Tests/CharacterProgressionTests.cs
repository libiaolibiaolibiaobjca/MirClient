using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class CharacterProgressionTests
{
    [Fact]
    public void TestAddExp_NormalAccumulation()
    {
        CharacterProgression prog = new(1);
        Assert.Equal(1, prog.Level);
        Assert.Equal(0, prog.CurrentExp);
        Assert.Equal(100, prog.RequiredExp);
        Assert.Equal(0f, prog.ExpPercent);

        bool leveled = prog.AddExp(50, out int gained);
        Assert.False(leveled);
        Assert.Equal(0, gained);
        Assert.Equal(50, prog.CurrentExp);
        Assert.Equal(0.5f, prog.ExpPercent, precision: 2);
    }

    [Fact]
    public void TestAddExp_LevelUpSingle()
    {
        CharacterProgression prog = new(1);
        // 1 级升 2 级需 100 经验，注入 140 经验
        bool leveled = prog.AddExp(140, out int gained);
        Assert.True(leveled);
        Assert.Equal(1, gained);
        Assert.Equal(2, prog.Level);
        Assert.Equal(40, prog.CurrentExp); // 剩余 40 经验
        Assert.Equal(200, prog.RequiredExp); // 2 级升 3 级需 200 经验
        Assert.Equal(0.2f, prog.ExpPercent, precision: 2);
    }

    [Fact]
    public void TestAddExp_MultiLevelUp()
    {
        CharacterProgression prog = new(1);
        // 1->2需100, 2->3需200, 3->4需300, 4->5需400 (累计 1000 经验正好升至 5 级)
        bool leveled = prog.AddExp(1050, out int gained);
        Assert.True(leveled);
        Assert.Equal(4, gained); // 升了 4 级
        Assert.Equal(5, prog.Level);
        Assert.Equal(50, prog.CurrentExp); // 余量 50
    }

    [Fact]
    public void TestSetLevel_DirectAndClamp()
    {
        CharacterProgression prog = new();
        prog.SetLevel(35);
        Assert.Equal(35, prog.Level);
        Assert.Equal(0, prog.CurrentExp);
        Assert.True(prog.RequiredExp > 1000000);

        // 边界限制
        prog.SetLevel(999);
        Assert.Equal(70, prog.Level);
        prog.SetLevel(-5);
        Assert.Equal(1, prog.Level);
    }

    [Fact]
    public void TestStatGrowth()
    {
        CharacterProgression lvl1 = new(1);
        CharacterProgression lvl35 = new(35);

        // 生命与魔法随等级显著提升
        Assert.True(lvl35.MaxHp > lvl1.MaxHp);
        Assert.True(lvl35.MaxMp > lvl1.MaxMp);
        Assert.Equal(80 + (35 * 18), lvl35.MaxHp); // 710
        Assert.Equal(40 + (35 * 8), lvl35.MaxMp);  // 320

        // 基础物攻随等级提升
        Assert.True(lvl35.BaseMaxDc > lvl1.BaseMaxDc);
        Assert.True(lvl35.BaseMinDc >= lvl1.BaseMinDc);
    }
}
