namespace MirClient.Protocol.Resources;

public enum ItemType
{
    Weapon = 0,   // 武器 (木剑, 乌木剑, 裁决之杖等)
    Dress = 1,    // 衣服 (布衣, 轻型盔甲, 重型盔甲, 战神盔甲)
    Helmet = 2,   // 头盔
    Necklace = 3, // 项链
    Armring = 4,  // 手镯
    Ring = 5,     // 戒指
    Potion = 6,   // 消耗药水 (金创药, 魔法药)
    Misc = 7      // 杂物 / 金币
}

/// <summary>
/// 盛大热血传奇经典物品领域模型
/// </summary>
public class Item
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public ItemType Type { get; set; }
    
    /// <summary>
    /// 纸娃娃映射外观编号 (衣服对应 DressId, 武器对应 WeaponId)
    /// </summary>
    public int Shape { get; set; }

    // 属性加成
    public int MinDc { get; set; }
    public int MaxDc { get; set; }
    public int MinAc { get; set; }
    public int MaxAc { get; set; }
    public int MinMac { get; set; }
    public int MaxMac { get; set; }
    public int HpRestore { get; set; }
    public int MpRestore { get; set; }

    public int Weight { get; set; }
    public int Dura { get; set; }      // 当前持久度
    public int MaxDura { get; set; }   // 最大持久度
    public int Count { get; set; } = 1;// 堆叠数量

    // 经典铁匠铺武器升级与试刀开刃属性
    public int UpgradeLevel { get; set; } = 0;       // 已升级次数 (+1 ~ +7)
    public bool InUpgradeTrial { get; set; } = false; // 是否处于待开刃试刀状态
    public int TrialBonusDc { get; set; } = 1;       // 试刀成功预期增加 DC
    public int TrialSuccessRate { get; set; } = 60;  // 试刀成功率百分比 (30~90)
    public int TrialPurityEffect { get; set; } = 0;  // 黑铁矿石对持久的影响 (-1, 0, +1)

    // 持久归零损坏失效状态
    public bool IsBroken => MaxDura > 0 && Dura <= 0;

    public Item Clone() => (Item)MemberwiseClone();

    // 预设经典传奇经典物品原型
    public static Item CreateWoodSword() => new()
    {
        Id = 1,
        Name = "木剑",
        Type = ItemType.Weapon,
        Shape = 1, // 对应原版 WeaponLib 1号木剑
        MinDc = 2,
        MaxDc = 5,
        Weight = 7,
        Dura = 4,
        MaxDura = 4
    };

    public static Item CreateEbonySword() => new()
    {
        Id = 2,
        Name = "乌木剑",
        Type = ItemType.Weapon,
        Shape = 2, // 对应原版 WeaponLib 2号乌木剑
        MinDc = 4,
        MaxDc = 8,
        Weight = 8,
        Dura = 7,
        MaxDura = 7
    };

    public static Item CreateJudgmentMace() => new()
    {
        Id = 3,
        Name = "裁决之杖",
        Type = ItemType.Weapon,
        Shape = 6, // 对应原版 WeaponLib 6号裁决之杖
        MinDc = 0,
        MaxDc = 30,
        Weight = 50,
        Dura = 32,
        MaxDura = 32
    };

    public static Item CreateClothArmor() => new()
    {
        Id = 10,
        Name = "布衣(男)",
        Type = ItemType.Dress,
        Shape = 0, // 对应原版 HumLib 0号布衣
        MinAc = 2,
        MaxAc = 2,
        Weight = 5,
        Dura = 5,
        MaxDura = 5
    };

    public static Item CreateHeavyArmor() => new()
    {
        Id = 11,
        Name = "重型盔甲(男)",
        Type = ItemType.Dress,
        Shape = 2, // 对应原版 HumLib 2号重型盔甲
        MinAc = 4,
        MaxAc = 7,
        MinMac = 2,
        MaxMac = 3,
        Weight = 23,
        Dura = 18,
        MaxDura = 18
    };

    public static Item CreateGodArmor() => new()
    {
        Id = 12,
        Name = "战神盔甲(男)",
        Type = ItemType.Dress,
        Shape = 3, // 对应原版 HumLib 3号战神盔甲
        MinAc = 5,
        MaxAc = 9,
        MinMac = 3,
        MaxMac = 5,
        Weight = 40,
        Dura = 26,
        MaxDura = 26
    };

    public static Item CreateRedPotion(int count = 5) => new()
    {
        Id = 20,
        Name = "金创药(小包)",
        Type = ItemType.Potion,
        HpRestore = 50,
        Weight = 2,
        Count = count
    };

    public static Item CreateBluePotion(int count = 5) => new()
    {
        Id = 21,
        Name = "魔法药(小包)",
        Type = ItemType.Potion,
        MpRestore = 40,
        Weight = 2,
        Count = count
    };

    public static Item CreateSunPotion(int count = 5) => new()
    {
        Id = 22,
        Name = "强效太阳水",
        Type = ItemType.Potion,
        HpRestore = 60,
        MpRestore = 60,
        Weight = 2,
        Count = count
    };
}
