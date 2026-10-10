using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class InventoryTests
{
    [Fact]
    public void TestEquipItem_SwordReplacement_ShouldUpdateWeaponShapeAndDc()
    {
        InventoryManager inv = new();
        
        // 初始装备：木剑 (Shape 1, DC 2-5)
        Assert.Equal(1, inv.EquippedWeaponShape);
        int initialMaxDc = inv.TotalMaxDc;

        // 背包中第 1 格是裁决之杖 (Shape 6, MaxDc 30)
        // 查找裁决之杖索引
        int judgeIndex = -1;
        for (int i = 0; i < InventoryManager.BAG_CAPACITY; i++)
        {
            if (inv.BagItems[i]?.Name == "裁决之杖")
            {
                judgeIndex = i;
                break;
            }
        }
        Assert.True(judgeIndex >= 0);

        // 穿戴裁决之杖
        bool success = inv.EquipItem(judgeIndex);
        Assert.True(success);

        // 验证武器外观联动变为 6 (裁决之杖)
        Assert.Equal(6, inv.EquippedWeaponShape);
        // 验证原木剑被置换回背包
        Assert.Equal("木剑", inv.BagItems[judgeIndex]?.Name);
        // 验证总攻击力提升
        Assert.True(inv.TotalMaxDc > initialMaxDc);
    }

    [Fact]
    public void TestEquipAndUnequipDress_ShouldUpdateDressShapeAndAc()
    {
        InventoryManager inv = new();

        // 初始布衣 (Shape 0)
        Assert.Equal(0, inv.EquippedDressShape);

        // 查找战神盔甲索引
        int godArmorIndex = -1;
        for (int i = 0; i < InventoryManager.BAG_CAPACITY; i++)
        {
            if (inv.BagItems[i]?.Name == "战神盔甲(男)")
            {
                godArmorIndex = i;
                break;
            }
        }
        Assert.True(godArmorIndex >= 0);

        // 穿戴战神盔甲 (Shape 3)
        inv.EquipItem(godArmorIndex);
        Assert.Equal(3, inv.EquippedDressShape);
        Assert.True(inv.TotalMaxAc >= 11);

        // 脱下衣服槽位
        bool unequipSuccess = inv.UnequipItem(EquipSlot.Dress);
        Assert.True(unequipSuccess);
        // 脱下后外观回退到 0 (默认布衣外观)
        Assert.Equal(0, inv.EquippedDressShape);
    }

    [Fact]
    public void TestUsePotion_ShouldRestoreHpAndDecrementCount()
    {
        InventoryManager inv = new();

        // 查找红药
        int potionIndex = -1;
        for (int i = 0; i < InventoryManager.BAG_CAPACITY; i++)
        {
            if (inv.BagItems[i]?.Type == ItemType.Potion)
            {
                potionIndex = i;
                break;
            }
        }
        Assert.True(potionIndex >= 0);
        int initialCount = inv.BagItems[potionIndex]!.Count;

        // 使用药水
        bool used = inv.UsePotion(potionIndex, out int hp, out int mp);
        Assert.True(used);
        Assert.True(hp > 0);
        Assert.Equal(initialCount - 1, inv.BagItems[potionIndex]?.Count ?? 0);
    }
}
