using System.Numerics;

namespace MirClient.Protocol.Resources;

/// <summary>
/// NPC 对话超链接选项
/// </summary>
public record NpcOption(string Label, string ActionCode);

/// <summary>
/// 商店货架商品项
/// </summary>
public record ShopItem(Item Item, int Price);

/// <summary>
/// NPC 配置与业务数据
/// </summary>
public class NpcProfile
{
    public string Name { get; set; } = "";
    public string Title { get; set; } = "";
    public string Greeting { get; set; } = "";
    public List<NpcOption> Options { get; set; } = new();
    public List<ShopItem> Goods { get; set; } = new();

    public static NpcProfile CreateBlacksmith() => new()
    {
        Name = "铁匠",
        Title = "比奇城高级铸剑大师",
        Greeting = "客官，需要点什么称手的兵器？我这里熔铸的刀剑盔甲，皆是百炼精钢，锋利无比！",
        Options = new()
        {
            new("[购买武器装备]", "@buy"),
            new("[出售闲置装备]", "@sell"),
            new("[普通修理装备]", "@repair"),
            new("[神圣特殊修理]", "@repair_special"),
            new("[黑铁淬炼武器升级]", "@upgrade"),
            new("[离开]", "@close")
        },
        Goods = new()
        {
            new(Item.CreateJudgmentMace(), 25000),
            new(Item.CreateHeavyArmor(), 12000),
            new(Item.CreateEbonySword(), 3000)
        }
    };

    public static NpcProfile CreateAlchemist() => new()
    {
        Name = "药铺老板",
        Title = "回春堂首席医师",
        Greeting = "少侠，出城除魔凶险万分，切莫大意！我这配制的金创药与回魔秘水，可助你化险为夷。",
        Options = new()
        {
            new("[购买救命药水]", "@buy"),
            new("[出售背包草药]", "@sell"),
            new("[离开]", "@close")
        },
        Goods = new()
        {
            new(Item.CreateRedPotion(5), 1500),
            new(new Item
            {
                Name = "魔法药(中)",
                Type = ItemType.Potion,
                MpRestore = 60,
                Weight = 2,
                Dura = 1,
                MaxDura = 1,
                Count = 5
            }, 1500)
        }
    };

    public static NpcProfile CreateVeteran() => new()
    {
        Name = "比奇老兵",
        Title = "沙巴克退役百夫长",
        Greeting = "年轻人，你身上有当年沙城勇士的锐气！玛法大陆辽阔无垠，老夫可为你指引前往各省的捷径。",
        Options = new()
        {
            new("[传送: 兽人古墓一层 (180, 200)]", "@tele_tomb"),
            new("[传送: 盟重省土城 (330, 330)]", "@tele_mengzhong"),
            new("[传送: 毒蛇山谷隘口 (280, 180)]", "@tele_snake"),
            new("[传送: 比奇王城安全区 (330, 268)]", "@tele_biqi"),
            new("[打听: 沙巴克攻城秘史]", "@legend"),
            new("[离开]", "@close")
        },
        Goods = new()
    };

    public static NpcProfile CreateMartialInstructor() => new()
    {
        Name = "武馆教头",
        Title = "比奇城天下第一武馆教头",
        Greeting = "学武修心，尊师重道！凡玛法少年侠士，皆可在此寻觅良师或开山立派，传承绝艺！",
        Options = new()
        {
            new("[拜师学艺 (28级以下)]", "@master_menu"),
            new("[招收门徒 (35级或已出师)]", "@apprentice_menu"),
            new("[武馆出师典礼 (徒弟35级)]", "@graduate_ceremony"),
            new("[师门与声望名录 (按 F 键)]", "@social_info"),
            new("[离开]", "@close")
        }
    };
}

/// <summary>
/// NPC 商店交易与逻辑处理器
/// </summary>
public static class NpcShop
{
    public const int MAX_CARRY_WEIGHT = 280;

    /// <summary>
    /// 购买货架商品
    /// </summary>
    public static bool TryBuyItem(
        InventoryManager inv, 
        ref int playerGold, 
        ShopItem goods, 
        out string errorMsg)
    {
        errorMsg = "";

        if (playerGold < goods.Price)
        {
            errorMsg = $"金币不足！需要 {goods.Price} 金币 (当前金币: {playerGold})";
            return false;
        }

        if (inv.TotalWeight + goods.Item.Weight > MAX_CARRY_WEIGHT)
        {
            errorMsg = $"随身负重不足！当前负重: {inv.TotalWeight}/{MAX_CARRY_WEIGHT}";
            return false;
        }

        // 复制商品实例
        Item purchased = goods.Item.Clone();

        if (!inv.AddItem(purchased))
        {
            errorMsg = "背包已满，无法存放更多物品！";
            return false;
        }

        playerGold -= goods.Price;
        return true;
    }

    /// <summary>
    /// 出售包裹物品
    /// </summary>
    public static bool TrySellItem(
        InventoryManager inv,
        ref int playerGold,
        int slotIndex,
        out string soldName,
        out int gainGold,
        out string errorMsg)
    {
        soldName = "";
        gainGold = 0;
        errorMsg = "";

        Item? item = inv.GetItem(slotIndex);
        if (item == null)
        {
            errorMsg = "该格位没有物品！";
            return false;
        }

        soldName = item.Name;

        // 根据物品类型与品质估价回收
        gainGold = item.Type switch
        {
            ItemType.Weapon => (item.MaxDc > 25 ? 12000 : 1500),
            ItemType.Dress  => (item.MaxAc >= 7 ? 6000 : 1000),
            ItemType.Potion => 200 * Math.Max(1, item.Count),
            _               => 500
        };

        inv.RemoveItem(slotIndex);
        playerGold += gainGold;
        return true;
    }

    /// <summary>
    /// 根据 NPC 名字获取配置原型
    /// </summary>
    public static NpcProfile ResolveProfile(string npcName)
    {
        if (npcName.Contains("铁匠")) return NpcProfile.CreateBlacksmith();
        if (npcName.Contains("药")) return NpcProfile.CreateAlchemist();
        if (npcName.Contains("武馆") || npcName.Contains("教头")) return NpcProfile.CreateMartialInstructor();
        return NpcProfile.CreateVeteran();
    }
}
