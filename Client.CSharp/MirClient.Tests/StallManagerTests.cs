using System.Numerics;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class StallManagerTests
{
    [Fact]
    public void TestStallManager_DefaultState()
    {
        StallManager sm = new();
        Assert.False(sm.IsStalling);
        Assert.Equal("", sm.StallTitle);
        Assert.Equal("逐鹿天下", sm.OwnerName);
        Assert.Empty(sm.Shelves);
        Assert.Equal(0, sm.TotalEarnings);
        Assert.Contains("并未在摆摊", sm.GenerateShoutMessage());
    }

    [Fact]
    public void TestStallManager_SafeZoneCheck()
    {
        // 1. 比奇城安全区：老兵中心 (330, 268)
        Assert.True(StallManager.IsInSafeZone("0", new Vector2(330, 268)));
        Assert.True(StallManager.IsInSafeZone("比奇省", new Vector2(340, 275))); // 距离约 12 格
        Assert.False(StallManager.IsInSafeZone("0", new Vector2(400, 400))); // 距离极远，野外

        // 2. 盟重省安全区：中心 (330, 330)
        Assert.True(StallManager.IsInSafeZone("3", new Vector2(330, 330)));
        Assert.True(StallManager.IsInSafeZone("盟重省", new Vector2(345, 345))); // 距离约 21 格
        Assert.False(StallManager.IsInSafeZone("3", new Vector2(100, 100)));

        // 3. 洞穴或未知地图不可摆摊
        Assert.False(StallManager.IsInSafeZone("D021", new Vector2(180, 200)));
        Assert.False(StallManager.IsInSafeZone("", new Vector2(330, 268)));
    }

    [Fact]
    public void TestStallManager_StartAndStopStall()
    {
        StallManager sm = new();
        InventoryManager inv = new();

        // 1. 在野外尝试摆摊应失败
        bool failOutSafeZone = sm.StartStall("神兵小铺", "测试摊主", "0", new Vector2(500, 500), out string errMsg);
        Assert.False(failOutSafeZone);
        Assert.False(sm.IsStalling);
        Assert.Contains("唯有在主城安全区集市", errMsg);

        // 2. 在安全区成功开启摆摊
        bool ok = sm.StartStall("【神兵阁】极品甩卖", "大财主", "0", new Vector2(330, 268), out string msg);
        Assert.True(ok);
        Assert.True(sm.IsStalling);
        Assert.Equal("【神兵阁】极品甩卖", sm.StallTitle);
        Assert.Equal("大财主", sm.OwnerName);

        // 3. 重复开启失败
        bool duplicate = sm.StartStall("新招牌", "大财主", "0", new Vector2(330, 268), out string dupMsg);
        Assert.False(duplicate);
        Assert.Contains("当前已在摆摊中", dupMsg);

        // 4. 收摊
        bool stopped = sm.StopStall(inv, out string stopMsg);
        Assert.True(stopped);
        Assert.False(sm.IsStalling);
        Assert.Contains("已顺利收摊", stopMsg);
    }

    [Fact]
    public void TestStallManager_ShelfManagement()
    {
        StallManager sm = new();
        InventoryManager inv = new();
        inv.ClearBag();

        // 放入测试物品
        var sword = Item.CreateJudgmentMace();
        var armor = Item.CreateGodArmor();
        inv.AddItem(sword);
        inv.AddItem(armor);

        // 未摆摊时上架应被阻断
        bool blocked = sm.AddItemToShelf(inv, 0, 10000, out string blockMsg);
        Assert.False(blocked);
        Assert.Contains("请先开启摆摊", blockMsg);

        // 开启摆摊
        sm.StartStall("特惠集市", "传奇商人", "0", new Vector2(330, 268), out _);

        // 标价 <= 0 阻断
        bool badPrice = sm.AddItemToShelf(inv, 0, 0, out string priceMsg);
        Assert.False(badPrice);
        Assert.Contains("定价必须大于 0", priceMsg);

        // 成功上架裁决之杖
        bool addOk1 = sm.AddItemToShelf(inv, 0, 88888, out string addMsg1);
        Assert.True(addOk1);
        Assert.Single(sm.Shelves);
        Assert.Equal("裁决之杖", sm.Shelves[0].Item.Name);
        Assert.Equal(88888, sm.Shelves[0].Price);
        Assert.Equal(0, sm.Shelves[0].SlotIndex);

        // 背包中裁决之杖已被扣除，剩余战神盔甲
        Assert.Equal(1, inv.ItemCount);

        // 下架物品放回背包
        bool removeOk = sm.RemoveItemFromShelf(inv, 0, out string remMsg);
        Assert.True(removeOk);
        Assert.Empty(sm.Shelves);
        Assert.Equal(2, inv.ItemCount); // 重新回到背包
    }

    [Fact]
    public void TestStallManager_PurchaseTransaction()
    {
        StallManager sellerStall = new();
        InventoryManager sellerInv = new();
        sellerInv.ClearBag();
        sellerInv.AddItem(Item.CreateJudgmentMace()); // 裁决之杖

        // 卖家开摊上架
        sellerStall.StartStall("神兵专卖", "神装掌柜", "0", new Vector2(330, 268), out _);
        sellerStall.AddItemToShelf(sellerInv, 0, 66666, out _);
        Assert.Single(sellerStall.Shelves);

        // 买家准备
        InventoryManager buyerInv = new();
        buyerInv.ClearBag();
        int buyerGold = 50000; // 金币不足

        // 1. 金币不足购买失败
        bool failNoMoney = sellerStall.BuyItem(buyerInv, ref buyerGold, 0, out string noMoneyMsg);
        Assert.False(failNoMoney);
        Assert.Equal(50000, buyerGold);
        Assert.Contains("金币不足", noMoneyMsg);
        Assert.Single(sellerStall.Shelves); // 商品仍在

        // 2. 补足金币，成功购买
        buyerGold = 100000;
        bool buyOk = sellerStall.BuyItem(buyerInv, ref buyerGold, 0, out string buyMsg);
        Assert.True(buyOk);
        Assert.Equal(33334, buyerGold); // 100000 - 66666
        Assert.Equal(66666, sellerStall.TotalEarnings); // 卖家累计收益
        Assert.Empty(sellerStall.Shelves); // 货架清空
        Assert.Equal(1, buyerInv.ItemCount);
        Assert.Equal("裁决之杖", buyerInv.GetItem(0)?.Name);
    }

    [Fact]
    public void TestStallManager_CommandParserAndShout()
    {
        // 1. 测试指令解析
        var cmd1 = CommandParser.Parse("@stall on 【特价神装】全服最低");
        Assert.Equal(ParsedCommandType.Stall, cmd1.Type);
        Assert.Equal("on", cmd1.SubAction);
        Assert.Equal("【特价神装】全服最低", cmd1.TargetName);

        var cmd2 = CommandParser.Parse("@stall off");
        Assert.Equal(ParsedCommandType.Stall, cmd2.Type);
        Assert.Equal("off", cmd2.SubAction);

        var cmd3 = CommandParser.Parse("@stall add 裁决之杖 58888");
        Assert.Equal(ParsedCommandType.Stall, cmd3.Type);
        Assert.Equal("add", cmd3.SubAction);
        Assert.Equal("裁决之杖", cmd3.TargetName);
        Assert.Equal(58888, cmd3.Amount);

        var cmd4 = CommandParser.Parse("@stall buy 0");
        Assert.Equal(ParsedCommandType.Stall, cmd4.Type);
        Assert.Equal("buy", cmd4.SubAction);
        Assert.Equal(0, cmd4.Amount);

        var cmd5 = CommandParser.Parse("@stall shout");
        Assert.Equal(ParsedCommandType.Stall, cmd5.Type);
        Assert.Equal("shout", cmd5.SubAction);

        // 2. 测试喊话文本
        StallManager sm = new();
        sm.StartStall("沙巴克福利摊", "战神", "0", new Vector2(330, 268), out _);
        string shout = sm.GenerateShoutMessage();
        Assert.Contains("[摆摊叫卖]", shout);
        Assert.Contains("沙巴克福利摊", shout);
        Assert.Contains("战神", shout);
        Assert.Contains("330, 268", shout);
    }
}
