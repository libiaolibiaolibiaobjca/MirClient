using System.Numerics;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class WolongVillaTests
{
    public WolongVillaTests()
    {
        WolongManager.Instance.Reset();
    }

    [Fact]
    public void Test_InitialState_And_DefaultValues()
    {
        var mgr = WolongManager.Instance;
        Assert.Equal(WolongInstancePhase.Idle, mgr.Phase);
        Assert.Equal(0f, mgr.RemainingTime);
        Assert.Empty(mgr.CollectedOrbs);
        Assert.Empty(mgr.DefeatedGuardians);
        Assert.Empty(mgr.ActiveCorpses);
        Assert.False(mgr.IsInstanceActive);
    }

    [Fact]
    public void Test_StartInstance_ActivatesTimer_And_Phase()
    {
        var mgr = WolongManager.Instance;
        string msg = mgr.StartInstance();

        Assert.Equal(WolongInstancePhase.Active, mgr.Phase);
        Assert.Equal(1800f, mgr.RemainingTime);
        Assert.True(mgr.IsInstanceActive);
        Assert.True(mgr.CharmsCount >= 10);
        Assert.Contains("卧龙山庄", msg);
    }

    [Fact]
    public void Test_DefeatGuardian_GeneratesCorpse()
    {
        var mgr = WolongManager.Instance;
        mgr.StartInstance();

        var corpse = mgr.DefeatGuardian(WolongGuardianType.Metal, new Vector2(330, 268));
        Assert.NotNull(corpse);
        Assert.Equal("金之守护者", corpse.Name);
        Assert.Equal(new Vector2(330, 268), corpse.TilePos);
        Assert.False(corpse.IsSearched);
        Assert.Single(mgr.ActiveCorpses);
        Assert.Contains(WolongGuardianType.Metal, mgr.DefeatedGuardians);
    }

    [Fact]
    public void Test_SearchCorpse_ConsumesCharms_And_AwardsLoot()
    {
        var mgr = WolongManager.Instance;
        mgr.StartInstance();
        mgr.CharmsCount = 10;

        var corpse = mgr.DefeatGuardian(WolongGuardianType.Fire, new Vector2(330, 268));
        var loot = mgr.SearchCorpse(corpse.Id);

        Assert.True(loot.Success);
        Assert.Equal(8, mgr.CharmsCount); // 消耗 2 张灵符
        Assert.True(corpse.IsSearched);
        Assert.True(loot.PagesCount >= 2);
        Assert.Equal("火灵珠", loot.OrbName);
        Assert.Contains("火灵珠", mgr.CollectedOrbs);

        // 重复探索应被拦截
        var secondSearch = mgr.SearchCorpse(corpse.Id);
        Assert.False(secondSearch.Success);
        Assert.Contains("已被灵符探索", secondSearch.Message);
    }

    [Fact]
    public void Test_InsufficientCharms_BlocksSearch()
    {
        var mgr = WolongManager.Instance;
        mgr.StartInstance();
        mgr.CharmsCount = 1; // 仅剩 1 张灵符 (不足2张)

        var corpse = mgr.DefeatGuardian(WolongGuardianType.Water, new Vector2(330, 268));
        var loot = mgr.SearchCorpse(corpse.Id);

        Assert.False(loot.Success);
        Assert.Contains("灵符不足", loot.Message);
        Assert.False(corpse.IsSearched);
    }

    [Fact]
    public void Test_CollectAllFiveOrbs_UnlocksTreasure()
    {
        var mgr = WolongManager.Instance;
        mgr.StartInstance();
        mgr.CharmsCount = 50;

        var types = new[]
        {
            WolongGuardianType.Metal,
            WolongGuardianType.Wood,
            WolongGuardianType.Water,
            WolongGuardianType.Fire,
            WolongGuardianType.Earth
        };

        foreach (var t in types)
        {
            var corpse = mgr.DefeatGuardian(t, new Vector2(330, 268));
            var loot = mgr.SearchCorpse(corpse.Id);
            Assert.True(loot.Success);
        }

        Assert.Equal(5, mgr.CollectedOrbs.Count);
        Assert.True(mgr.HasCollectedAllOrbs);
        Assert.Equal(WolongInstancePhase.TreasureReady, mgr.Phase);
    }

    [Fact]
    public void Test_OpenFireDragonTreasure_AwardsRewards_And_Broadcast()
    {
        var mgr = WolongManager.Instance;
        mgr.StartInstance();

        // 未集齐五元时尝试开启
        var (failSuccess, failMsg, _) = mgr.OpenFireDragonTreasure();
        Assert.False(failSuccess);
        Assert.Contains("五元未齐", failMsg);

        // 模拟集齐五元灵珠
        mgr.CharmsCount = 50;
        foreach (var t in new[] { WolongGuardianType.Metal, WolongGuardianType.Wood, WolongGuardianType.Water, WolongGuardianType.Fire, WolongGuardianType.Earth })
        {
            var c = mgr.DefeatGuardian(t, new Vector2(330, 268));
            mgr.SearchCorpse(c.Id);
        }

        var (success, broadcast, rewards) = mgr.OpenFireDragonTreasure();
        Assert.True(success);
        Assert.Equal(WolongInstancePhase.Completed, mgr.Phase);
        Assert.NotEmpty(rewards);
        Assert.Contains("通告", broadcast);
    }

    [Fact]
    public void Test_InstanceTimeout_ExpiresPhase()
    {
        var mgr = WolongManager.Instance;
        mgr.StartInstance();

        // 推进 1801 秒
        mgr.Update(1801f, out string? timeEvent);
        Assert.Equal(WolongInstancePhase.Expired, mgr.Phase);
        Assert.NotNull(timeEvent);
        Assert.Contains("时限已到", timeEvent);
    }

    [Fact]
    public void Test_WolongCommandParsing()
    {
        var cmd1 = CommandParser.Parse("@wolong enter");
        Assert.Equal(ParsedCommandType.Wolong, cmd1.Type);
        Assert.Equal("enter", cmd1.SubAction);

        var cmd2 = CommandParser.Parse("@wolong spawn 金之守护者");
        Assert.Equal(ParsedCommandType.Wolong, cmd2.Type);
        Assert.Equal("spawn", cmd2.SubAction);
        Assert.Equal("金之守护者", cmd2.TargetName);

        var cmd3 = CommandParser.Parse("@wolong search");
        Assert.Equal(ParsedCommandType.Wolong, cmd3.Type);
        Assert.Equal("search", cmd3.SubAction);

        var cmd4 = CommandParser.Parse("@wolong treasure");
        Assert.Equal(ParsedCommandType.Wolong, cmd4.Type);
        Assert.Equal("treasure", cmd4.SubAction);

        var cmd5 = CommandParser.Parse("@wolong charm 50");
        Assert.Equal(ParsedCommandType.Wolong, cmd5.Type);
        Assert.Equal("charm", cmd5.SubAction);
        Assert.Equal(50, cmd5.Amount);
    }
}
