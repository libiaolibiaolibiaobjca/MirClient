using System.Numerics;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class GroundItemTests
{
    [Fact]
    public void TestSpawnGoldAndItem()
    {
        GroundItemManager manager = new();
        Vector2 pos = new(330, 268);

        GroundItem gold = manager.SpawnGold(pos, 150);
        Assert.True(gold.IsGold);
        Assert.Equal(150, gold.GoldAmount);
        Assert.Equal("金币 (150)", gold.DisplayName);

        Item sword = Item.CreateWoodSword();
        GroundItem item = manager.SpawnItem(pos, sword);
        Assert.False(item.IsGold);
        Assert.Equal("木剑", item.DisplayName);
        Assert.Equal(2, manager.Items.Count);
    }

    [Fact]
    public void TestMonsterDrops_WhitePig_Burst()
    {
        GroundItemManager manager = new();
        Vector2 center = new(330, 268);

        // 模拟阻挡：假设 (331, 268) 是障碍物
        bool IsBlocked(int x, int y) => (x == 331 && y == 268);

        var drops = manager.GenerateMonsterDrops("白野猪", center, IsBlocked);

        // 白野猪掉落至少包含 3 堆金币、3 捆药水、裁决之杖与战神盔甲，总计 8 件
        Assert.Equal(8, drops.Count);

        // 确保没有掉落在阻挡点 (331, 268)
        Assert.DoesNotContain(drops, d => (int)d.TilePos.X == 331 && (int)d.TilePos.Y == 268);

        // 包含极品装备 "裁决之杖"
        Assert.Contains(drops, d => d.Item?.Name == "裁决之杖");
        // 包含神装 "战神盔甲(男)"
        Assert.Contains(drops, d => d.Item?.Name == "战神盔甲(男)");
    }

    [Fact]
    public void TestPickup_Gold()
    {
        GroundItemManager manager = new();
        InventoryManager inv = new();
        inv.ClearBag(); // 清空背包

        Vector2 playerPos = new(330, 268);
        manager.SpawnGold(playerPos, 500);

        int currentGold = 1000;
        bool picked = manager.TryPickupNearest(playerPos, inv, ref currentGold, out string msg);

        Assert.True(picked);
        Assert.Equal(1500, currentGold);
        Assert.Contains("500", msg);
        Assert.Empty(manager.Items); // 地表已被捡空
    }

    [Fact]
    public void TestPickup_Item_And_BagFull()
    {
        GroundItemManager manager = new();
        InventoryManager inv = new();
        inv.ClearBag();

        Vector2 playerPos = new(330, 268);
        manager.SpawnItem(playerPos, Item.CreateEbonySword());

        // 1. 正常入包
        int gold = 0;
        bool picked = manager.TryPickupNearest(playerPos, inv, ref gold, out string msg);
        Assert.True(picked);
        Assert.Contains("乌木剑", msg);
        Assert.Contains(inv.BagItems, item => item?.Name == "乌木剑");
        Assert.Empty(manager.Items);

        // 2. 塞满背包测试拦截
        for (int i = 0; i < 40; i++)
        {
            inv.AddItem(Item.CreateWoodSword());
        }

        manager.SpawnItem(playerPos, Item.CreateJudgmentMace());
        picked = manager.TryPickupNearest(playerPos, inv, ref gold, out msg);

        Assert.False(picked);
        Assert.Equal("包裹已满，无法拾取！", msg);
        Assert.Single(manager.Items); // 依然保留在地面上
    }

    [Fact]
    public void TestGroundItem_Lifetime_Timeout()
    {
        GroundItemManager manager = new();
        Vector2 pos = new(330, 268);
        manager.SpawnGold(pos, 100);

        Assert.Single(manager.Items);

        // 模拟时间推移 60 秒（未超时）
        manager.Update(60f);
        Assert.Single(manager.Items);

        // 模拟再推移 61 秒（总计 121 秒，超过 120 秒）
        manager.Update(61f);
        Assert.Empty(manager.Items); // 超时被自动清理
    }
}
