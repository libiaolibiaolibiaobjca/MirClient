namespace MirClient.Protocol.Resources;

/// <summary>
/// 荣誉勋章原型数据定义
/// </summary>
public class BadgeDef
{
    public int Number { get; set; }
    public string Name { get; set; } = "";
    public int RequiredReputation { get; set; } = 10;
    public int MinDc { get; set; }
    public int MaxDc { get; set; }
    public int MinMc { get; set; }
    public int MaxMc { get; set; }
    public int MinSc { get; set; }
    public int MaxSc { get; set; }
    public int MinAc { get; set; }
    public int MaxAc { get; set; }
    public int MinMac { get; set; }
    public int MaxMac { get; set; }
    public int Weight { get; set; } = 1;
    public int MaxDura { get; set; } = 50;

    public string GradeName => Number switch
    {
        >= 41 and <= 45 => "王者荣耀级",
        >= 31 and <= 35 => "黄金荣耀级",
        >= 21 and <= 25 => "白银荣耀级",
        _               => "青铜荣耀级"
    };

    public string AttrDescription
    {
        get
        {
            List<string> parts = new();
            if (MaxDc > 0) parts.Add($"攻击 {MinDc}-{MaxDc}");
            if (MaxMc > 0) parts.Add($"魔法 {MinMc}-{MaxMc}");
            if (MaxSc > 0) parts.Add($"道术 {MinSc}-{MaxSc}");
            if (MaxAc > 0) parts.Add($"防御 {MinAc}-{MaxAc}");
            if (MaxMac > 0) parts.Add($"魔御 {MinMac}-{MaxMac}");
            return parts.Count > 0 ? string.Join("  ", parts) : "无属性加成";
        }
    }
}

/// <summary>
/// 盛大传奇十周年：圣域荣誉勋章与声望锻造管理器 (纯 C# 领域状态机)
/// </summary>
public class BadgeManager
{
    private static BadgeManager? _instance;
    public static BadgeManager Instance => _instance ??= new BadgeManager();

    private readonly Dictionary<int, BadgeDef> _database = new();

    public const int ReforgeReputationCost = 5; // 每次重铸洗炼消耗声望
    public const int MaxReforgeBonus = 3;        // 重铸附加极品属性上限

    public BadgeManager()
    {
        InitDatabase();
    }

    private void InitDatabase()
    {
        // ===== 青铜级 (11~15号，需要声望 10 点) =====
        AddDef(11, "荣誉勋章11号", 10, minDc: 0, maxDc: 1, minAc: 0, maxAc: 1);
        AddDef(12, "荣誉勋章12号", 10, minMc: 0, maxMc: 1, minAc: 0, maxAc: 1);
        AddDef(13, "荣誉勋章13号", 10, minSc: 0, maxSc: 1, minAc: 0, maxAc: 1);
        AddDef(14, "荣誉勋章14号", 10, minAc: 0, maxAc: 2);
        AddDef(15, "荣誉勋章15号", 10, minMac: 0, maxMac: 2);

        // ===== 白银级 (21~25号，需要声望 20 点) =====
        AddDef(21, "荣誉勋章21号", 20, minDc: 0, maxDc: 2);
        AddDef(22, "荣誉勋章22号", 20, minMc: 0, maxMc: 2);
        AddDef(23, "荣誉勋章23号", 20, minSc: 0, maxSc: 2);
        AddDef(24, "荣誉勋章24号", 20, minAc: 0, maxAc: 3);
        AddDef(25, "荣誉勋章25号", 20, minMac: 0, maxMac: 3);

        // ===== 黄金级 (31~35号，需要声望 40 点) =====
        AddDef(31, "荣誉勋章31号", 40, minAc: 1, maxAc: 3, minMac: 1, maxMac: 3);
        AddDef(32, "荣誉勋章32号", 40, minAc: 2, maxAc: 4);
        AddDef(33, "荣誉勋章33号", 40, minDc: 1, maxDc: 3);
        AddDef(34, "荣誉勋章34号", 40, minMc: 1, maxMc: 3);
        AddDef(35, "荣誉勋章35号", 40, minSc: 1, maxSc: 3);

        // ===== 王者级 (41~45号，需要声望 80 点) =====
        AddDef(41, "荣誉勋章41号", 80, minAc: 1, maxAc: 4, minMac: 1, maxMac: 4);
        AddDef(42, "荣誉勋章42号", 80, minAc: 2, maxAc: 5);
        AddDef(43, "荣誉勋章43号", 80, minDc: 1, maxDc: 5); // 战士巅峰勋章
        AddDef(44, "荣誉勋章44号", 80, minMc: 1, maxMc: 5); // 法师巅峰勋章
        AddDef(45, "荣誉勋章45号", 80, minSc: 1, maxSc: 5); // 道士巅峰勋章
    }

    private void AddDef(
        int number, string name, int reqRep,
        int minDc = 0, int maxDc = 0,
        int minMc = 0, int maxMc = 0,
        int minSc = 0, int maxSc = 0,
        int minAc = 0, int maxAc = 0,
        int minMac = 0, int maxMac = 0)
    {
        _database[number] = new BadgeDef
        {
            Number = number,
            Name = name,
            RequiredReputation = reqRep,
            MinDc = minDc,
            MaxDc = maxDc,
            MinMc = minMc,
            MaxMc = maxMc,
            MinSc = minSc,
            MaxSc = maxSc,
            MinAc = minAc,
            MaxAc = maxAc,
            MinMac = minMac,
            MaxMac = maxMac,
            Weight = 1,
            MaxDura = 50
        };
    }

    public BadgeDef? GetBadgeDef(int number) => _database.TryGetValue(number, out var def) ? def : null;

    public IReadOnlyList<BadgeDef> GetAllBadgeDefs() => _database.Values.OrderBy(b => b.Number).ToList();

    /// <summary>
    /// 生成勋章实体物品
    /// </summary>
    public Item? CreateBadgeItem(int number)
    {
        var def = GetBadgeDef(number);
        if (def == null) return null;

        return new Item
        {
            Id = 5000 + number,
            Name = def.Name,
            Type = ItemType.Badge,
            BadgeNumber = number,
            MinDc = def.MinDc,
            MaxDc = def.MaxDc,
            MinMc = def.MinMc,
            MaxMc = def.MaxMc,
            MinSc = def.MinSc,
            MaxSc = def.MaxSc,
            MinAc = def.MinAc,
            MaxAc = def.MaxAc,
            MinMac = def.MinMac,
            MaxMac = def.MaxMac,
            Weight = def.Weight,
            Dura = def.MaxDura,
            MaxDura = def.MaxDura,
            Shape = number // 对应勋章图鉴编号
        };
    }

    /// <summary>
    /// 消耗江湖声望兑换荣誉勋章
    /// </summary>
    public (bool Success, string Message, Item? Badge) ExchangeBadge(InventoryManager inv, int badgeNumber, ref int playerReputation)
    {
        var def = GetBadgeDef(badgeNumber);
        if (def == null)
            return (false, $"未找到【{badgeNumber}号】荣誉勋章图鉴！", null);

        if (playerReputation < def.RequiredReputation)
            return (false, $"江湖声望不足！兑换【{def.Name}】需要 {def.RequiredReputation} 点声望 (当前声望: {playerReputation} 点)。", null);

        Item? badgeItem = CreateBadgeItem(badgeNumber);
        if (badgeItem == null)
            return (false, "创建勋章实例异常！", null);

        if (!inv.AddItem(badgeItem))
            return (false, "包裹已满，无法存放新兑换的勋章！", null);

        playerReputation -= def.RequiredReputation;
        return (true, $"恭喜！消耗 {def.RequiredReputation} 点江湖声望，成功兑换【{def.Name} ({def.GradeName})】！属性: {def.AttrDescription}", badgeItem);
    }

    /// <summary>
    /// 勋章声望重铸淬炼 (洗炼附加极品属性)
    /// </summary>
    public (bool Success, string Message, string BonusAttr) ReforgeBadge(Item badge, ref int playerReputation)
    {
        if (badge == null || badge.Type != ItemType.Badge)
            return (false, "目标物品不是荣誉勋章，无法进行声望重铸！", "");

        if (playerReputation < ReforgeReputationCost)
            return (false, $"声望不足！重铸勋章每次需消耗 {ReforgeReputationCost} 点江湖声望 (当前声望: {playerReputation} 点)。", "");

        int currentTotalBonus = badge.ExtraDc + badge.ExtraMc + badge.ExtraSc + badge.ExtraAc + badge.ExtraMac;
        if (currentTotalBonus >= MaxReforgeBonus)
            return (false, $"该勋章重铸极品属性已达上限 (+{MaxReforgeBonus})，器蕴天成，不可再锻！", "");

        playerReputation -= ReforgeReputationCost;
        badge.ReforgeCount++;

        // 随机洗炼赋予一项极品属性 (+1)
        int roll = Random.Shared.Next(5);
        string attrName;
        switch (roll)
        {
            case 0:
                badge.ExtraDc += 1;
                attrName = "攻击+1";
                break;
            case 1:
                badge.ExtraMc += 1;
                attrName = "魔法+1";
                break;
            case 2:
                badge.ExtraSc += 1;
                attrName = "道术+1";
                break;
            case 3:
                badge.ExtraAc += 1;
                attrName = "防御+1";
                break;
            default:
                badge.ExtraMac += 1;
                attrName = "魔御+1";
                break;
        }

        return (true, $"圣域灵火淬炼！【{badge.Name}】成功重铸，觉醒极品属性【{attrName}】！(已重铸 {badge.ReforgeCount} 次)", attrName);
    }
}
