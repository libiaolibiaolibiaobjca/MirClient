using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class NpcShopTests
{
    [Fact]
    public void TestBlacksmithShop_BuyJudgmentMace_Success()
    {
        InventoryManager inv = new();
        inv.ClearBag();
        int gold = 50000;
        var smith = NpcProfile.CreateBlacksmith();
        var maceGoods = smith.Goods[0]; // 裁决之杖, 25000 金币

        bool ok = NpcShop.TryBuyItem(inv, ref gold, maceGoods, out string error);

        Assert.True(ok);
        Assert.Empty(error);
        Assert.Equal(25000, gold);
        Assert.Equal(1, inv.ItemCount);
        Assert.Equal("裁决之杖", inv.GetItem(0)?.Name);
        Assert.Equal(62, inv.TotalWeight);
    }

    [Fact]
    public void TestShop_BuyFailed_InsufficientGold()
    {
        InventoryManager inv = new();
        inv.ClearBag();
        int gold = 5000; // 只有 5000 金币
        var smith = NpcProfile.CreateBlacksmith();
        var armorGoods = smith.Goods[1]; // 重盔甲, 12000 金币

        bool ok = NpcShop.TryBuyItem(inv, ref gold, armorGoods, out string error);

        Assert.False(ok);
        Assert.Contains("金币不足", error);
        Assert.Equal(5000, gold);
        Assert.Equal(0, inv.ItemCount);
    }

    [Fact]
    public void TestShop_SellItem_GainGold()
    {
        InventoryManager inv = new();
        inv.ClearBag();
        inv.AddItem(Item.CreateHeavyArmor());
        int gold = 10000;
        int initWeight = inv.TotalWeight;

        bool ok = NpcShop.TrySellItem(inv, ref gold, 0, out string soldName, out int gain, out string error);

        Assert.True(ok);
        Assert.Empty(error);
        Assert.Equal("重型盔甲(男)", soldName);
        Assert.Equal(6000, gain);
        Assert.Equal(16000, gold);
        Assert.Equal(0, inv.ItemCount);
        Assert.True(inv.TotalWeight < initWeight);
    }

    [Fact]
    public void TestNpcProfileResolution()
    {
        var smith = NpcShop.ResolveProfile("比奇城铁匠");
        Assert.Equal("铁匠", smith.Name);
        Assert.NotEmpty(smith.Goods);

        var alchemist = NpcShop.ResolveProfile("回春堂药铺老板");
        Assert.Equal("药铺老板", alchemist.Name);
        Assert.NotEmpty(alchemist.Goods);

        var veteran = NpcShop.ResolveProfile("比奇老兵");
        Assert.Equal("比奇老兵", veteran.Name);
        Assert.Contains(veteran.Options, o => o.ActionCode == "@tele_mengzhong");
    }
}
