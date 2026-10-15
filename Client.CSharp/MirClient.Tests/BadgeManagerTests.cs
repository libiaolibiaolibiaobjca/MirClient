using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class BadgeManagerTests
{
    [Fact]
    public void Test_Badge_Database_Coverage()
    {
        var bm = BadgeManager.Instance;
        var all = bm.GetAllBadgeDefs();
        Assert.NotEmpty(all);

        // 校验 43 号战士巅峰勋章
        var b43 = bm.GetBadgeDef(43);
        Assert.NotNull(b43);
        Assert.Equal("荣誉勋章43号", b43.Name);
        Assert.Equal(80, b43.RequiredReputation);
        Assert.Equal(1, b43.MinDc);
        Assert.Equal(5, b43.MaxDc);
        Assert.Equal("王者荣耀级", b43.GradeName);

        // 校验 44 号法师巅峰勋章
        var b44 = bm.GetBadgeDef(44);
        Assert.NotNull(b44);
        Assert.Equal(1, b44.MinMc);
        Assert.Equal(5, b44.MaxMc);

        // 校验 11 号青铜勋章
        var b11 = bm.GetBadgeDef(11);
        Assert.NotNull(b11);
        Assert.Equal(10, b11.RequiredReputation);
        Assert.Equal("青铜荣耀级", b11.GradeName);
    }

    [Fact]
    public void Test_Badge_Exchange_Success_And_Deduction()
    {
        var bm = BadgeManager.Instance;
        InventoryManager inv = new();
        int rep = 100;

        // 兑换 43 号勋章 (需要 80 点声望)
        var res = bm.ExchangeBadge(inv, 43, ref rep);
        Assert.True(res.Success);
        Assert.Equal(20, rep); // 100 - 80 = 20
        Assert.NotNull(res.Badge);
        Assert.Equal(ItemType.Badge, res.Badge.Type);
        Assert.Equal(43, res.Badge.BadgeNumber);
        Assert.Equal(1, res.Badge.MinDc);
        Assert.Equal(5, res.Badge.MaxDc);

        // 校验背包中已存在该勋章
        bool foundInBag = inv.BagItems.Any(i => i != null && i.Type == ItemType.Badge && i.BadgeNumber == 43);
        Assert.True(foundInBag);
    }

    [Fact]
    public void Test_Badge_Exchange_Insufficient_Reputation()
    {
        var bm = BadgeManager.Instance;
        InventoryManager inv = new();
        int rep = 50;

        // 兑换 43 号勋章 (需要 80 点声望，此时 50 点应失败)
        var res = bm.ExchangeBadge(inv, 43, ref rep);
        Assert.False(res.Success);
        Assert.Equal(50, rep); // 声望不应扣除
        Assert.Contains("江湖声望不足", res.Message);
    }

    [Fact]
    public void Test_Badge_Equip_And_Unequip()
    {
        InventoryManager inv = new();
        var badge = BadgeManager.Instance.CreateBadgeItem(43)!;
        Assert.NotNull(badge);

        // 存入背包
        Assert.True(inv.AddItem(badge));

        // 找到背包索引并穿戴
        int bagIdx = -1;
        for (int i = 0; i < InventoryManager.BAG_CAPACITY; i++)
        {
            if (inv.BagItems[i]?.Type == ItemType.Badge)
            {
                bagIdx = i;
                break;
            }
        }
        Assert.True(bagIdx >= 0);

        // 穿戴勋章
        Assert.True(inv.EquipItem(bagIdx));
        Assert.NotNull(inv.EquippedItems[(int)EquipSlot.Badge]);
        Assert.Equal(43, inv.EquippedItems[(int)EquipSlot.Badge]!.BadgeNumber);

        // 脱下勋章
        Assert.True(inv.UnequipItem(EquipSlot.Badge));
        Assert.Null(inv.EquippedItems[(int)EquipSlot.Badge]);
    }

    [Fact]
    public void Test_Badge_Stats_Stacking()
    {
        InventoryManager inv = new();
        int originMinDc = inv.TotalMinDc;
        int originMaxDc = inv.TotalMaxDc;

        // 穿戴 43 号王者勋章 (DC 1-5)
        var badge43 = BadgeManager.Instance.CreateBadgeItem(43)!;
        inv.AddItem(badge43);

        int bagIdx = -1;
        for (int i = 0; i < InventoryManager.BAG_CAPACITY; i++)
        {
            if (inv.BagItems[i]?.Type == ItemType.Badge) { bagIdx = i; break; }
        }
        inv.EquipItem(bagIdx);

        // 验证战力叠加
        Assert.Equal(originMinDc + 1, inv.TotalMinDc);
        Assert.Equal(originMaxDc + 5, inv.TotalMaxDc);
    }

    [Fact]
    public void Test_Badge_Reforge_Bonus_And_Limit()
    {
        var bm = BadgeManager.Instance;
        var badge = bm.CreateBadgeItem(43)!;
        int rep = 50;

        // 1. 第一次重铸
        var res1 = bm.ReforgeBadge(badge, ref rep);
        Assert.True(res1.Success);
        Assert.Equal(45, rep); // 50 - 5 = 45
        Assert.Equal(1, badge.ReforgeCount);
        int totalBonus1 = badge.ExtraDc + badge.ExtraMc + badge.ExtraSc + badge.ExtraAc + badge.ExtraMac;
        Assert.Equal(1, totalBonus1);

        // 2. 第二次重铸
        var res2 = bm.ReforgeBadge(badge, ref rep);
        Assert.True(res2.Success);
        Assert.Equal(40, rep);
        Assert.Equal(2, badge.ReforgeCount);

        // 3. 第三次重铸 (达到上限 3)
        var res3 = bm.ReforgeBadge(badge, ref rep);
        Assert.True(res3.Success);
        Assert.Equal(35, rep);
        Assert.Equal(3, badge.ReforgeCount);

        // 4. 第四次重铸 (应被上限拦截)
        var res4 = bm.ReforgeBadge(badge, ref rep);
        Assert.False(res4.Success);
        Assert.Equal(35, rep); // 不扣声望
        Assert.Contains("已达上限", res4.Message);
    }

    [Fact]
    public void Test_CommandParser_Badge_Commands()
    {
        // 1. 兑换指令
        var cmdExchange = CommandParser.Parse("@badge exchange 43");
        Assert.Equal(ParsedCommandType.Badge, cmdExchange.Type);
        Assert.Equal("exchange", cmdExchange.SubAction);
        Assert.Equal(43, cmdExchange.Amount);

        // 2. 重铸指令
        var cmdReforge = CommandParser.Parse("@badge reforge");
        Assert.Equal(ParsedCommandType.Badge, cmdReforge.Type);
        Assert.Equal("reforge", cmdReforge.SubAction);

        // 3. 中文别名
        var cmdCn = CommandParser.Parse("@勋章 兑换 44");
        Assert.Equal(ParsedCommandType.Badge, cmdCn.Type);
        Assert.Equal("兑换", cmdCn.SubAction);
        Assert.Equal(44, cmdCn.Amount);
    }
}
