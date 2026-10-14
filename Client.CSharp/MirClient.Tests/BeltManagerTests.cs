using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class BeltManagerTests
{
    [Fact]
    public void TestBeltManager_DefaultSlots()
    {
        BeltManager belt = new();
        Assert.Equal("强效太阳水", belt.GetSlotPotion(0));
        Assert.Equal("金创药(小包)", belt.GetSlotPotion(1));
        Assert.Equal("魔法药(小包)", belt.GetSlotPotion(2));
        Assert.True(belt.IsAutoProtectEnabled);
        Assert.Equal(0.35f, belt.ProtectHpPercent);
    }

    [Fact]
    public void TestBeltManager_PotionCount_FromInventory()
    {
        BeltManager belt = new();
        InventoryManager inv = new();

        // 默认 6 格配置：0/3 为太阳水(12), 1/4 为金创药(8), 2/5 为魔法药(6)
        Assert.Equal(12, belt.GetTotalPotionCount(0, inv));
        Assert.Equal(8, belt.GetTotalPotionCount(1, inv));
        Assert.Equal(6, belt.GetTotalPotionCount(2, inv));
        Assert.Equal(12, belt.GetTotalPotionCount(3, inv));

        // 将槽位 3 清空设为空槽
        belt.AssignSlot(3, "");
        Assert.Equal(0, belt.GetTotalPotionCount(3, inv));

        // 向包裹追加强效太阳水 6 瓶与金创药 10 瓶
        inv.AddItem(Item.CreateSunPotion(6));
        inv.AddItem(Item.CreateRedPotion(10));

        Assert.Equal(18, belt.GetTotalPotionCount(0, inv)); // 强效太阳水 12 + 6 = 18
        Assert.Equal(18, belt.GetTotalPotionCount(1, inv)); // 金创药 8 + 10 = 18
    }

    [Fact]
    public void TestBeltManager_UseSlot_SuccessAndEmpty()
    {
        BeltManager belt = new();
        InventoryManager inv = new();

        // 初始槽位 0 有 12 瓶太阳水
        Assert.Equal(12, belt.GetTotalPotionCount(0, inv));

        // 第 1 次使用成功，扣减 1 瓶
        bool ok = belt.UseSlot(0, inv, out int hp, out int mp, out string name);
        Assert.True(ok);
        Assert.Equal(60, hp);
        Assert.Equal(60, mp);
        Assert.Equal("强效太阳水", name);
        Assert.Equal(11, belt.GetTotalPotionCount(0, inv));

        // 将槽位 3 设为空槽，验证使用空槽失败
        belt.AssignSlot(3, "");
        bool okEmptySlot = belt.UseSlot(3, inv, out _, out _, out _);
        Assert.False(okEmptySlot);
    }

    [Fact]
    public void TestBeltManager_AutoProtect_TriggerAndCooldown()
    {
        BeltManager belt = new();
        InventoryManager inv = new(); // 默认内含 12 瓶强效太阳水

        float maxHp = 200f;
        // 生命值处于 80% (160/200)，高于 35% 阈值，不触发
        bool triggeredHigh = belt.CheckAutoProtect(160f, maxHp, inv, 10.0, out _, out _, out _);
        Assert.False(triggeredHigh);
        Assert.Equal(12, belt.GetTotalPotionCount(0, inv));

        // 生命值骤降至 25% (50/200)，满足危急条件，成功秒喝太阳水
        bool triggeredLow = belt.CheckAutoProtect(50f, maxHp, inv, 10.5, out int hp, out int mp, out string used);
        Assert.True(triggeredLow);
        Assert.Equal(60, hp);
        Assert.Equal(60, mp);
        Assert.Equal("强效太阳水", used);
        Assert.Equal(11, belt.GetTotalPotionCount(0, inv));

        // 紧接着 0.2 秒内再次触发 (仍在 1.0 秒 CD 保护中)，防抖阻断不重复消耗
        bool triggeredCd = belt.CheckAutoProtect(50f, maxHp, inv, 10.7, out _, out _, out _);
        Assert.False(triggeredCd);
        Assert.Equal(11, belt.GetTotalPotionCount(0, inv));

        // 过了 1.2 秒后 (11.8)，CD 恢复，再次救急
        bool triggeredAfterCd = belt.CheckAutoProtect(50f, maxHp, inv, 11.8, out _, out _, out _);
        Assert.True(triggeredAfterCd);
        Assert.Equal(10, belt.GetTotalPotionCount(0, inv));
    }

    [Fact]
    public void TestCommandParser_ProtectCommand()
    {
        var cmd1 = CommandParser.Parse("@protect on 40");
        Assert.Equal(ParsedCommandType.Protect, cmd1.Type);
        Assert.True(cmd1.Enabled);
        Assert.Equal(40, cmd1.Amount);

        var cmd2 = CommandParser.Parse("@protect off");
        Assert.Equal(ParsedCommandType.Protect, cmd2.Type);
        Assert.False(cmd2.Enabled);

        var cmd3 = CommandParser.Parse("@protect 50");
        Assert.Equal(ParsedCommandType.Protect, cmd3.Type);
        Assert.True(cmd3.Enabled);
        Assert.Equal(50, cmd3.Amount);
    }
}
