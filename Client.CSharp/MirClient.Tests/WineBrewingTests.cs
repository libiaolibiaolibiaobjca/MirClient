using System.Numerics;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class WineBrewingTests
{
    public WineBrewingTests()
    {
        WineManager.Instance.Reset();
    }

    [Fact]
    public void Test_InitialState_And_DefaultInventories()
    {
        var mgr = WineManager.Instance;
        Assert.Equal(5, mgr.SpringWaterJars);
        Assert.Equal(0f, mgr.CurrentDrunkenness);
        Assert.Equal(50f, mgr.AlcoholTolerance);
        Assert.Equal(0, mgr.WinePotency);
        Assert.Equal(0, mgr.PotencyLevel);
        Assert.False(mgr.IsDrunk);
        Assert.False(mgr.HasActiveBuff);
        Assert.True(mgr.WineInventory[WineType.Sorghum] >= 1);
    }

    [Fact]
    public void Test_SpawnSpring_And_GatherWater()
    {
        var mgr = WineManager.Instance;
        var sp = mgr.SpawnSpringPoint(new Vector2(330, 268));

        Assert.Equal(5, sp.RemainingWater);
        Assert.False(sp.IsDry);

        // 汲取泉水
        var (s1, msg1) = mgr.GatherSpringWater(sp.Id);
        Assert.True(s1);
        Assert.Equal(6, mgr.SpringWaterJars);
        Assert.Equal(4, sp.RemainingWater);
        Assert.Contains("天泉水", msg1);

        // 连续汲取至干涸
        for (int i = 0; i < 4; i++)
        {
            mgr.GatherSpringWater(sp.Id);
        }
        Assert.Equal(0, sp.RemainingWater);
        Assert.True(sp.IsDry);

        // 干涸后汲取应被拦截
        var (sFail, msgFail) = mgr.GatherSpringWater(sp.Id);
        Assert.False(sFail);
        Assert.Contains("干涸", msgFail);
    }

    [Fact]
    public void Test_BrewWine_ConsumesSpringWater_And_AddsStock()
    {
        var mgr = WineManager.Instance;
        mgr.SpringWaterJars = 2;
        int initialStock = mgr.WineInventory[WineType.Huadiao];

        // 酿造花雕酒
        var (s1, msg1) = mgr.BrewWine(WineType.Huadiao);
        Assert.True(s1);
        Assert.Equal(1, mgr.SpringWaterJars);
        Assert.Equal(initialStock + 1, mgr.WineInventory[WineType.Huadiao]);
        Assert.Contains("花雕酒", msg1);

        // 再次酿造高粱酒
        var (s2, _) = mgr.BrewWine(WineType.Sorghum);
        Assert.True(s2);
        Assert.Equal(0, mgr.SpringWaterJars);

        // 泉水耗尽后尝试酿造应被拦截
        var (sFail, msgFail) = mgr.BrewWine(WineType.Fruit);
        Assert.False(sFail);
        Assert.Contains("原料不足", msgFail);
    }

    [Fact]
    public void Test_DrinkWine_GrantsBuff_And_IncreasesDrunkenness()
    {
        var mgr = WineManager.Instance;
        mgr.Reset();

        // 痛饮随身高粱酒 (库存初始有2坛)
        var (s, msg) = mgr.DrinkWine(WineType.Sorghum);
        Assert.True(s);
        Assert.True(mgr.CurrentDrunkenness > 0f);
        Assert.True(mgr.HasActiveBuff);
        Assert.NotNull(mgr.CurrentBuff);
        Assert.Equal("高粱酒", mgr.CurrentBuff.Name);
        Assert.True(mgr.CurrentBuff.BonusDc > 0);
        Assert.Contains("开坛畅饮", msg);
    }

    [Fact]
    public void Test_DrinkWine_AccumulatesPotency_And_Breakthrough()
    {
        var mgr = WineManager.Instance;
        mgr.Reset();
        // 赋予 5 坛花雕酒 (每坛 +50 药力)
        mgr.BrewWine(WineType.Huadiao);
        mgr.BrewWine(WineType.Huadiao);

        Assert.Equal(0, mgr.PotencyLevel);

        // 喝第一坛花雕酒 (+50 药力)
        mgr.DrinkWine(WineType.Huadiao);
        Assert.Equal(50, mgr.WinePotency);
        Assert.Equal(0, mgr.PotencyLevel);

        // 喝第二坛花雕酒 (总计 100 药力，突破第1重)
        var (_, msg2) = mgr.DrinkWine(WineType.Huadiao);
        Assert.Equal(100, mgr.WinePotency);
        Assert.Equal(1, mgr.PotencyLevel);
        Assert.True(mgr.PermanentBonusHp >= 40);
        Assert.True(mgr.PermanentBonusAc >= 3);
        Assert.Contains("突破", msg2);
    }

    [Fact]
    public void Test_Drunkenness_NaturalDecay_And_BuffExpiry()
    {
        var mgr = WineManager.Instance;
        mgr.DrinkWine(WineType.Sorghum);
        float initDrunk = mgr.CurrentDrunkenness;

        // 时间流逝 10 秒
        mgr.Update(10f);
        Assert.True(mgr.CurrentDrunkenness < initDrunk);

        // 时间流逝 310 秒 (Buff 持续 300 秒)
        mgr.Update(310f);
        Assert.False(mgr.HasActiveBuff);
        Assert.Null(mgr.CurrentBuff);
        Assert.Equal(0f, mgr.CurrentDrunkenness);
    }

    [Fact]
    public void Test_DrunkState_WhenExceedingTolerance()
    {
        var mgr = WineManager.Instance;
        mgr.AlcoholTolerance = 30f;
        mgr.CurrentDrunkenness = 40f;

        Assert.True(mgr.IsDrunk);
    }

    [Fact]
    public void Test_WineCommandParsing()
    {
        var cmd1 = CommandParser.Parse("@wine brew 高粱酒");
        Assert.Equal(ParsedCommandType.Wine, cmd1.Type);
        Assert.Equal("brew", cmd1.SubAction);
        Assert.Equal("高粱酒", cmd1.TargetName);

        var cmd2 = CommandParser.Parse("@wine drink 花雕酒");
        Assert.Equal(ParsedCommandType.Wine, cmd2.Type);
        Assert.Equal("drink", cmd2.SubAction);
        Assert.Equal("花雕酒", cmd2.TargetName);

        var cmd3 = CommandParser.Parse("@wine spring");
        Assert.Equal(ParsedCommandType.Wine, cmd3.Type);
        Assert.Equal("spring", cmd3.SubAction);

        var cmd4 = CommandParser.Parse("@wine water 20");
        Assert.Equal(ParsedCommandType.Wine, cmd4.Type);
        Assert.Equal("water", cmd4.SubAction);
        Assert.Equal(20, cmd4.Amount);
    }
}
