namespace MirClient.Protocol.Resources;

public enum EquipSlot
{
    Dress = 0,    // 衣服
    Weapon = 1,   // 武器
    Helmet = 2,   // 头盔
    Necklace = 3, // 项链
    ArmringL = 4, // 左手镯
    ArmringR = 5, // 右手镯
    RingL = 6,    // 左戒指
    RingR = 7,    // 右戒指
    Badge = 8     // 圣域荣誉勋章
}

/// <summary>
/// 玩家背包与装备系统管理器
/// </summary>
public class InventoryManager
{
    public const int BAG_CAPACITY = 40; // 经典 40 格背包
    public const int EQUIP_SLOTS_COUNT = 9;

    // 背包槽位
    private readonly Item?[] _bag = new Item?[BAG_CAPACITY];
    // 装备槽位
    private readonly Item?[] _equipment = new Item?[EQUIP_SLOTS_COUNT];

    public IReadOnlyList<Item?> BagItems => _bag;
    public IReadOnlyList<Item?> EquippedItems => _equipment;

    // 当前纸娃娃外观映射 (无装备时默认外观: 布衣 0, 空手 0)
    public int EquippedDressShape => _equipment[(int)EquipSlot.Dress]?.Shape ?? 0;
    public int EquippedWeaponShape => _equipment[(int)EquipSlot.Weapon]?.Shape ?? 0;

    // 基础角色属性 (战士模板)
    public int BaseMinDc { get; set; } = 4;
    public int BaseMaxDc { get; set; } = 9;
    public int BaseMinAc { get; set; } = 1;
    public int BaseMaxAc { get; set; } = 2;

    // 装备与勋章叠加总属性 (含重铸附加极品)
    public int TotalMinDc => BaseMinDc + GetEquippedSum(i => i.MinDc + i.ExtraDc);
    public int TotalMaxDc => BaseMaxDc + GetEquippedSum(i => i.MaxDc + i.ExtraDc);
    public int TotalMinMc => GetEquippedSum(i => i.MinMc + i.ExtraMc);
    public int TotalMaxMc => GetEquippedSum(i => i.MaxMc + i.ExtraMc);
    public int TotalMinSc => GetEquippedSum(i => i.MinSc + i.ExtraSc);
    public int TotalMaxSc => GetEquippedSum(i => i.MaxSc + i.ExtraSc);
    public int TotalMinAc => BaseMinAc + GetEquippedSum(i => i.MinAc + i.ExtraAc);
    public int TotalMaxAc => BaseMaxAc + GetEquippedSum(i => i.MaxAc + i.ExtraAc);
    public int TotalWeight => GetEquippedSum(i => i.Weight) + GetBagSum(i => i.Weight * i.Count);

    public InventoryManager()
    {
        // 初始给玩家装备一件布衣和一柄木剑
        _equipment[(int)EquipSlot.Dress] = Item.CreateClothArmor();
        _equipment[(int)EquipSlot.Weapon] = Item.CreateWoodSword();

        // 背包默认赠送一组新手备用补给与高阶神兵 (便于测试即时换装)
        AddItem(Item.CreateEbonySword());    // 乌木剑
        AddItem(Item.CreateJudgmentMace());  // 裁决之杖
        AddItem(Item.CreateHeavyArmor());    // 重型盔甲
        AddItem(Item.CreateGodArmor());      // 战神盔甲
        AddItem(Item.CreateRedPotion(8));    // 金创药 x 8
        AddItem(Item.CreateBluePotion(6));   // 魔法药 x 6
        AddItem(Item.CreateSunPotion(12));   // 强效太阳水 x 12
    }

    private int GetEquippedSum(Func<Item, int> selector)
    {
        int sum = 0;
        foreach (var eq in _equipment)
        {
            if (eq != null && !eq.IsBroken) sum += selector(eq);
        }
        return sum;
    }

    private int GetBagSum(Func<Item, int> selector)
    {
        int sum = 0;
        foreach (var item in _bag)
        {
            if (item != null) sum += selector(item);
        }
        return sum;
    }

    /// <summary>
    /// 将物品放入背包空位
    /// </summary>
    public bool AddItem(Item item)
    {
        // 如果是可堆叠药水，尝试合包
        if (item.Type == ItemType.Potion)
        {
            for (int i = 0; i < BAG_CAPACITY; i++)
            {
                if (_bag[i] != null && _bag[i]!.Id == item.Id)
                {
                    _bag[i]!.Count += item.Count;
                    return true;
                }
            }
        }

        // 寻找首个空槽位
        for (int i = 0; i < BAG_CAPACITY; i++)
        {
            if (_bag[i] == null)
            {
                _bag[i] = item.Clone();
                return true;
            }
        }
        return false; // 背包已满
    }

    /// <summary>
    /// 双击穿戴背包内的物品
    /// </summary>
    public bool EquipItem(int bagIndex)
    {
        if (bagIndex < 0 || bagIndex >= BAG_CAPACITY) return false;
        Item? item = _bag[bagIndex];
        if (item == null) return false;

        EquipSlot targetSlot = item.Type switch
        {
            ItemType.Weapon => EquipSlot.Weapon,
            ItemType.Dress => EquipSlot.Dress,
            ItemType.Helmet => EquipSlot.Helmet,
            ItemType.Necklace => EquipSlot.Necklace,
            ItemType.Armring => _equipment[(int)EquipSlot.ArmringL] == null ? EquipSlot.ArmringL : EquipSlot.ArmringR,
            ItemType.Ring => _equipment[(int)EquipSlot.RingL] == null ? EquipSlot.RingL : EquipSlot.RingR,
            ItemType.Badge => EquipSlot.Badge,
            _ => (EquipSlot)(-1)
        };

        if ((int)targetSlot < 0) return false;

        // 如果对应槽位已有装备，执行对调
        Item? oldEquip = _equipment[(int)targetSlot];
        _equipment[(int)targetSlot] = item;
        _bag[bagIndex] = oldEquip; // 旧装备放回背包该格子 (若无则为 null 腾空)
        return true;
    }

    /// <summary>
    /// 双击脱下已穿戴的装备
    /// </summary>
    public bool UnequipItem(EquipSlot slot)
    {
        int slotIdx = (int)slot;
        if (slotIdx < 0 || slotIdx >= EQUIP_SLOTS_COUNT) return false;
        Item? equip = _equipment[slotIdx];
        if (equip == null) return false;

        // 寻找背包空位
        for (int i = 0; i < BAG_CAPACITY; i++)
        {
            if (_bag[i] == null)
            {
                _bag[i] = equip;
                _equipment[slotIdx] = null;
                return true;
            }
        }
        return false; // 背包无空位，无法卸下
    }

    /// <summary>
    /// 使用药水道具
    /// </summary>
    public bool UsePotion(int bagIndex, out int hpRestore, out int mpRestore)
    {
        hpRestore = 0;
        mpRestore = 0;
        if (bagIndex < 0 || bagIndex >= BAG_CAPACITY) return false;
        Item? item = _bag[bagIndex];
        if (item == null || item.Type != ItemType.Potion) return false;

        hpRestore = item.HpRestore;
        mpRestore = item.MpRestore;

        item.Count--;
        if (item.Count <= 0)
        {
            _bag[bagIndex] = null;
        }
        return true;
    }

    public Item? GetItem(int bagIndex) => (bagIndex >= 0 && bagIndex < BAG_CAPACITY) ? _bag[bagIndex] : null;

    public bool RemoveItem(int bagIndex)
    {
        if (bagIndex < 0 || bagIndex >= BAG_CAPACITY || _bag[bagIndex] == null) return false;
        _bag[bagIndex] = null;
        return true;
    }

    public int ItemCount => _bag.Count(i => i != null);

    public void ClearBag()
    {
        for (int i = 0; i < BAG_CAPACITY; i++) _bag[i] = null;
    }
}
