using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class ItemUpgradeTests
{
    [Fact]
    public void TestItemUpgrade_DefaultStateAndDurabilityWear()
    {
        ItemUpgradeManager um = new();
        InventoryManager inv = new();
        um.Reset();

        // 默认佩戴木剑 (Dura: 4, MaxDura: 4, MinDc: 2, MaxDc: 5)
        var weapon = inv.EquippedItems[(int)EquipSlot.Weapon]!;
        Assert.NotNull(weapon);
        Assert.Equal(4, weapon.Dura);
        Assert.False(weapon.IsBroken);

        // 挥刀攻击 4 次，未达 5 次阈值，持久仍为 4
        for (int i = 0; i < 4; i++)
        {
            um.WearWeaponOnAttack(inv, out _);
        }
        Assert.Equal(4, weapon.Dura);

        // 第 5 次攻击，持久扣减为 3
        um.WearWeaponOnAttack(inv, out _);
        Assert.Equal(3, weapon.Dura);

        // 人工将持久置 0，验证损坏与属性失效
        weapon.Dura = 0;
        Assert.True(weapon.IsBroken);

        // 损坏的武器不叠加攻击力，总 MaxDc 应只剩下基础 BaseMaxDc (9)
        Assert.Equal(9, inv.TotalMaxDc);

        // 攻击时弹出损坏警报
        bool warned = um.WearWeaponOnAttack(inv, out string warnMsg);
        Assert.True(warned);
        Assert.Contains("损坏", warnMsg);
    }

    [Fact]
    public void TestItemUpgrade_RepairNormal()
    {
        ItemUpgradeManager um = new();
        Item sword = Item.CreateWoodSword();
        sword.Dura = 1; // 损耗 3 点持久
        int gold = 10000;

        // 金币不足时修理阻断
        int lowGold = 50;
        bool failNoGold = um.RepairItemNormal(sword, ref lowGold, out string failMsg);
        Assert.False(failNoGold);
        Assert.Contains("资金不足", failMsg);

        // 成功普修
        bool repaired = um.RepairItemNormal(sword, ref gold, out string repMsg);
        Assert.True(repaired);
        Assert.Equal(sword.MaxDura, sword.Dura); // 恢复至满
        Assert.True(gold < 10000); // 消耗了金币
        Assert.Contains("普修完成", repMsg);
    }

    [Fact]
    public void TestItemUpgrade_RepairSpecial()
    {
        ItemUpgradeManager um = new();
        Item sword = Item.CreateJudgmentMace(); // 裁决之杖 (MaxDura: 32)
        sword.Dura = 2; // 磨损严重，仅剩 2 点
        int gold = 100000;

        int originalMax = sword.MaxDura;
        bool repOk = um.RepairItemSpecial(sword, ref gold, out string specMsg);
        Assert.True(repOk);
        Assert.Equal(originalMax, sword.MaxDura); // 上限绝对不损耗
        Assert.Equal(sword.MaxDura, sword.Dura);
        Assert.Contains("神圣特修", specMsg);
    }

    [Fact]
    public void TestItemUpgrade_RepairAll()
    {
        ItemUpgradeManager um = new();
        InventoryManager inv = new();
        inv.ClearBag();

        // 故意让武器和衣服产生磨损
        var weapon = inv.EquippedItems[(int)EquipSlot.Weapon]!;
        var dress = inv.EquippedItems[(int)EquipSlot.Dress]!;
        weapon.Dura = 1;
        dress.Dura = 2;

        int gold = 50000;
        int repairedCount = um.RepairAll(inv, ref gold, special: true, out string summary);

        Assert.Equal(2, repairedCount);
        Assert.Equal(weapon.MaxDura, weapon.Dura);
        Assert.Equal(dress.MaxDura, dress.Dura);
        Assert.Contains("一键全修", summary);
    }

    [Fact]
    public void TestItemUpgrade_WeaponUpgradeAndTrial()
    {
        ItemUpgradeManager um = new();
        Item weapon = Item.CreateJudgmentMace(); // 裁决之杖 0-30

        // 1. 提交升级锻造 (纯度 65, 首饰总攻 12)
        bool started = um.StartWeaponUpgrade(weapon, 65, 12, out string upMsg);
        Assert.True(started);
        Assert.True(weapon.InUpgradeTrial);
        Assert.Equal(1, weapon.TrialPurityEffect); // 纯度 >= 60 预期提升持久上限
        Assert.Contains("淬炼就绪", upMsg);

        // 处于试刀中，重复升级被阻断
        bool duplicate = um.StartWeaponUpgrade(weapon, 65, 12, out string dupMsg);
        Assert.False(duplicate);
        Assert.Contains("待试刀开刃", dupMsg);

        // 2. 模拟试刀开刃 (赋予 100% 成功率测试成功分支)
        weapon.TrialSuccessRate = 100;
        int oldMaxDc = weapon.MaxDc;
        bool trialOk = um.TryTrialBlade(weapon, out bool success, out string trialMsg);

        Assert.True(trialOk);
        Assert.True(success);
        Assert.False(weapon.InUpgradeTrial); // 状态已消费
        Assert.InRange(weapon.UpgradeLevel, 1, 2); // 成功升至 +1 (或人品爆发跳点 +2)
        Assert.True(weapon.MaxDc > oldMaxDc); // 攻击上限增加
        Assert.Contains("开刃", trialMsg);

        // 3. 达到 7 次上限后阻断
        weapon.UpgradeLevel = ItemUpgradeManager.MAX_UPGRADE_LEVEL;
        bool capped = um.StartWeaponUpgrade(weapon, 65, 12, out string capMsg);
        Assert.False(capped);
        Assert.Contains("极境", capMsg);
    }

    [Fact]
    public void TestItemUpgrade_CommandParser()
    {
        var cmd1 = CommandParser.Parse("@repair special");
        Assert.Equal(ParsedCommandType.Repair, cmd1.Type);
        Assert.Equal("special", cmd1.SubAction);
        Assert.True(cmd1.Enabled);

        var cmd2 = CommandParser.Parse("@repair normal");
        Assert.Equal(ParsedCommandType.Repair, cmd2.Type);
        Assert.Equal("normal", cmd2.SubAction);
        Assert.False(cmd2.Enabled);

        var cmd3 = CommandParser.Parse("@upgrade 65 15");
        Assert.Equal(ParsedCommandType.Upgrade, cmd3.Type);
        Assert.Equal(65, cmd3.Amount);
        Assert.Equal("15", cmd3.SubAction);

        var cmd4 = CommandParser.Parse("@dura 0");
        Assert.Equal(ParsedCommandType.Durability, cmd4.Type);
        Assert.Equal(0, cmd4.Amount);
    }
}
