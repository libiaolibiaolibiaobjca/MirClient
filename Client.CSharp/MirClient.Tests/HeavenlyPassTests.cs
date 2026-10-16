using System;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class HeavenlyPassTests
{
    public HeavenlyPassTests()
    {
        HeavenlyPassManager.Instance.Reset();
    }

    [Fact]
    public void Test_InitialState_And_ExchangeTalismans()
    {
        var mgr = HeavenlyPassManager.Instance;
        Assert.Equal(3, mgr.Talismans);
        Assert.Equal(0, mgr.CurrentFloor);
        Assert.False(mgr.IsActive);
        Assert.False(mgr.IsGuardDefeated);
        Assert.False(mgr.HasOpenedChest);

        // 尝试金币不足兑换
        int gold = 30000;
        var (s1, msg1, count1) = mgr.ExchangeTalismans(ref gold, 1);
        Assert.False(s1);
        Assert.Equal(0, count1);
        Assert.Equal(30000, gold);
        Assert.Contains("金币不足", msg1);

        // 充值 200,000 金币兑换 3 张灵符 (每张 50,000)
        gold = 200000;
        var (s2, msg2, count2) = mgr.ExchangeTalismans(ref gold, 3);
        Assert.True(s2);
        Assert.Equal(3, count2);
        Assert.Equal(50000, gold); // 剩余 50,000
        Assert.Equal(6, mgr.Talismans); // 3 + 3 = 6
        Assert.Contains("成功消耗", msg2);
    }

    [Fact]
    public void Test_EnterTrial_Floor1_ConsumesTalisman()
    {
        var mgr = HeavenlyPassManager.Instance;
        int initialTalismans = mgr.Talismans;

        var (s, msg) = mgr.EnterTrial();
        Assert.True(s);
        Assert.Equal(1, mgr.CurrentFloor);
        Assert.True(mgr.IsActive);
        Assert.False(mgr.IsGuardDefeated);
        Assert.False(mgr.HasOpenedChest);
        Assert.Equal(initialTalismans - 1, mgr.Talismans);
        Assert.Contains("天关第 1 层", msg);

        // 重复进入应被拦截
        var (sDup, msgDup) = mgr.EnterTrial();
        Assert.False(sDup);
        Assert.Contains("已身处天关", msgDup);
    }

    [Fact]
    public void Test_DefeatCurrentGuard_Floor1()
    {
        var mgr = HeavenlyPassManager.Instance;
        mgr.EnterTrial();

        // 击破守卫
        var (s, msg) = mgr.DefeatCurrentGuard();
        Assert.True(s);
        Assert.True(mgr.IsGuardDefeated);
        Assert.Equal(1, mgr.TotalClearedFloors);
        Assert.Contains("已被成功斩杀", msg);

        // 守卫已被消灭后不能重复消灭
        var (sDup, msgDup) = mgr.DefeatCurrentGuard();
        Assert.False(sDup);
        Assert.Contains("已被击破", msgDup);
    }

    [Fact]
    public void Test_OpenChest_SilverChest_Reward()
    {
        var mgr = HeavenlyPassManager.Instance;
        mgr.EnterTrial();

        // 未击败守卫时开箱应被拒绝
        var (sBlocked, _) = mgr.OpenChest(PassChestType.Silver);
        Assert.False(sBlocked);

        // 击破守卫
        mgr.DefeatCurrentGuard();

        int talismansBefore = mgr.Talismans;
        var (sSuccess, reward) = mgr.OpenChest(PassChestType.Silver);

        Assert.True(sSuccess);
        Assert.NotNull(reward);
        Assert.Equal(PassChestType.Silver, reward!.ChestType);
        Assert.True(reward.ExpAwarded >= 60000);
        Assert.Equal(talismansBefore - 1, mgr.Talismans);
        Assert.True(mgr.HasOpenedChest);

        // 同一层不能重复开箱
        var (sDup, _) = mgr.OpenChest(PassChestType.Gold);
        Assert.False(sDup);
    }

    [Fact]
    public void Test_OpenChest_GoldChest_DiamondsAndWeapon()
    {
        var mgr = HeavenlyPassManager.Instance;
        mgr.AddTalismans(5);
        mgr.EnterTrial();
        mgr.DefeatCurrentGuard();

        int talismansBefore = mgr.Talismans;
        var (s, reward) = mgr.OpenChest(PassChestType.Gold);

        Assert.True(s);
        Assert.NotNull(reward);
        Assert.Equal(PassChestType.Gold, reward!.ChestType);
        Assert.True(reward.Diamonds >= 1); // 金宝箱保底金刚石
        Assert.True(mgr.TotalDiamondsEarned >= 1);
        Assert.Equal(talismansBefore - 2, mgr.Talismans); // 金宝箱消耗 2 张灵符
        Assert.Contains("天关Gold宝箱", reward.Message);
    }

    [Fact]
    public void Test_NextFloor_Progression_Floor1ToFloor2()
    {
        var mgr = HeavenlyPassManager.Instance;
        mgr.AddTalismans(5);
        mgr.EnterTrial();

        // 未消灭守卫禁止前往下一层
        var (sFail, _, _) = mgr.NextFloor();
        Assert.False(sFail);

        // 斩杀守卫并进入下一层
        mgr.DefeatCurrentGuard();
        int talismansBefore = mgr.Talismans;

        var (sNext, msgNext, crown) = mgr.NextFloor();
        Assert.True(sNext);
        Assert.False(crown);
        Assert.Equal(2, mgr.CurrentFloor);
        Assert.False(mgr.IsGuardDefeated);
        Assert.Equal(talismansBefore - 1, mgr.Talismans);
        Assert.Contains("天关第 2 层", msgNext);
    }

    [Fact]
    public void Test_KingsHall_Floor5_BossDefeat_And_CrownAchieved()
    {
        var mgr = HeavenlyPassManager.Instance;
        mgr.AddTalismans(15);
        mgr.EnterTrial(); // Floor 1

        // 连续通关 1 -> 2 -> 3 -> 4
        for (int f = 1; f <= 4; f++)
        {
            mgr.DefeatCurrentGuard();
            mgr.NextFloor();
        }

        Assert.Equal(5, mgr.CurrentFloor); // 到达第 5 层王者神殿
        Assert.False(mgr.IsGuardDefeated);

        // 击杀王者神殿天关魔王
        var (sBoss, msgBoss) = mgr.DefeatCurrentGuard();
        Assert.True(sBoss);
        Assert.Contains("王者神殿", msgBoss);

        // 开启王者金宝箱
        var (sChest, reward) = mgr.OpenChest(PassChestType.Gold);
        Assert.True(sChest);
        Assert.NotNull(reward);
        Assert.Equal(5, reward!.Diamonds); // 第 5 层保底 5 颗金刚石
        Assert.Contains("王者神石", reward.ItemAwarded);

        // 登顶结算加冕
        var (sCrown, msgCrown, isCrown) = mgr.NextFloor();
        Assert.True(sCrown);
        Assert.True(isCrown);
        Assert.Equal(1, mgr.KingsHallClears);
        Assert.Equal(0, mgr.CurrentFloor); // 离开天关
        Assert.False(mgr.IsActive);
        Assert.Contains("荣耀加冕王者神殿", msgCrown);
    }

    [Fact]
    public void Test_ExitTrial_PreservesStats_And_ResetsState()
    {
        var mgr = HeavenlyPassManager.Instance;
        mgr.EnterTrial();
        mgr.DefeatCurrentGuard();
        Assert.True(mgr.IsActive);

        var (sExit, msgExit) = mgr.ExitTrial();
        Assert.True(sExit);
        Assert.False(mgr.IsActive);
        Assert.Equal(0, mgr.CurrentFloor);
        Assert.Contains("安全退出天关试炼", msgExit);

        // 未在试炼中退出应返回错误
        var (sFail, _) = mgr.ExitTrial();
        Assert.False(sFail);
    }
}
