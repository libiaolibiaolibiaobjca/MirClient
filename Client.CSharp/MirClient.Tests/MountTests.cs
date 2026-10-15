using System.Numerics;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class MountTests
{
    public MountTests()
    {
        MountManager.Instance.Reset();
    }

    [Fact]
    public void Test_Mount_Toggle_Mount_And_Dismount()
    {
        var mount = MountManager.Instance;
        Assert.False(mount.IsMounted);
        Assert.Equal(MountState.Dismounted, mount.State);
        Assert.Equal(1.0f, mount.SpeedMultiplier);

        // 1. 翻身上马
        var (s1, msg1) = mount.ToggleMount();
        Assert.True(s1);
        Assert.True(mount.IsMounted);
        Assert.Equal(MountState.Mounted, mount.State);
        Assert.True(mount.SpeedMultiplier > 1.0f);
        Assert.Contains("上马驰骋", msg1);

        // 2. 勒马下马
        var (s2, msg2) = mount.ToggleMount();
        Assert.True(s2);
        Assert.False(mount.IsMounted);
        Assert.Equal(MountState.Dismounted, mount.State);
        Assert.Equal(1.0f, mount.SpeedMultiplier);
        Assert.Contains("勒马步战", msg2);
    }

    [Fact]
    public void Test_Mount_Definitions_And_Attributes()
    {
        var mount = MountManager.Instance;

        // 1. 踏雪飞龙 (移速翻倍 +100%)
        mount.SwitchMount(MountType.SnowDragon);
        mount.ToggleMount();
        Assert.Equal("踏雪飞龙", mount.CurrentMountDef.Name);
        Assert.Equal(2.0f, mount.SpeedMultiplier);
        Assert.Equal(500, mount.BonusHp);
        Assert.Equal(5, mount.BonusDc);
        Assert.Equal(6, mount.BonusAc);

        // 2. 黄骠马 (+50%)
        mount.SwitchMount(MountType.BrownHorse);
        Assert.Equal(1.5f, mount.SpeedMultiplier);
        Assert.Equal(150, mount.BonusHp);

        // 3. 烈焰赤兔 (+80%)
        mount.SwitchMount(MountType.RedHare);
        Assert.Equal(1.8f, mount.SpeedMultiplier);
        Assert.Equal(300, mount.BonusHp);
    }

    [Fact]
    public void Test_Mount_Switch()
    {
        var mount = MountManager.Instance;
        var (s, msg) = mount.SwitchMount(MountType.ShadowPanther);
        Assert.True(s);
        Assert.Equal(MountType.ShadowPanther, mount.CurrentMount);
        Assert.Equal("幽冥玄豹", mount.CurrentMountDef.Name);
        Assert.Contains("幽冥玄豹", msg);
    }

    [Fact]
    public void Test_Mount_Charge_Success_And_Path()
    {
        var mount = MountManager.Instance;
        mount.ToggleMount(); // 上马

        // 朝东突进 (direction = 2, vec = (1, 0))
        Vector2 start = new(100, 100);
        bool res = mount.TryPerformCharge(start, 2, (x, y) => false, out Vector2 end, out var path, out int dmg, out string report);

        Assert.True(res);
        Assert.Equal(new Vector2(104, 100), end); // 直线突进 4 格
        Assert.Equal(4, path.Count);
        Assert.True(dmg >= 180);
        Assert.Contains("铁骑破军", report);
        Assert.False(mount.CanCharge); // 进入 CD
        Assert.Equal(MountManager.ChargeCooldownMax, mount.ChargeCooldownTimer);
    }

    [Fact]
    public void Test_Mount_Charge_Blocked_By_Wall()
    {
        var mount = MountManager.Instance;
        mount.ToggleMount();

        // 朝东突进，前方第 2 格 (102, 100) 有城墙阻挡
        Vector2 start = new(100, 100);
        bool res = mount.TryPerformCharge(start, 2, (x, y) => (x == 102 && y == 100), out Vector2 end, out var path, out _, out _);

        Assert.True(res);
        Assert.Equal(new Vector2(101, 100), end); // 撞墙前停止在第 1 格
        Assert.Single(path);
    }

    [Fact]
    public void Test_Mount_Charge_Cooldown()
    {
        var mount = MountManager.Instance;
        mount.ToggleMount();

        // 首次冲锋
        mount.TryPerformCharge(new Vector2(50, 50), 0, (x, y) => false, out _, out _, out _, out _);
        Assert.False(mount.CanCharge);

        // CD 未结束不可施展
        bool fail = mount.TryPerformCharge(new Vector2(50, 50), 0, (x, y) => false, out _, out _, out _, out string repFail);
        Assert.False(fail);
        Assert.Contains("冷却中", repFail);

        // 时间推进 8.5 秒后冷却结束
        mount.Update(8.5f);
        Assert.True(mount.CanCharge);
        Assert.Equal(0f, mount.ChargeCooldownTimer);
    }

    [Fact]
    public void Test_CommandParser_Mount_And_Charge()
    {
        // 1. @mount
        var cmd1 = CommandParser.Parse("@mount");
        Assert.Equal(ParsedCommandType.Mount, cmd1.Type);
        Assert.Equal("toggle", cmd1.SubAction);

        // 2. @mount switch
        var cmd2 = CommandParser.Parse("@mount switch 踏雪飞龙");
        Assert.Equal(ParsedCommandType.Mount, cmd2.Type);
        Assert.Equal("switch", cmd2.SubAction);
        Assert.Equal("踏雪飞龙", cmd2.TargetName);

        // 3. @charge
        var cmd3 = CommandParser.Parse("@charge");
        Assert.Equal(ParsedCommandType.Charge, cmd3.Type);
    }
}
