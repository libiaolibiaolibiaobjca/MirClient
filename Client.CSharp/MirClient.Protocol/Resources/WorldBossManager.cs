using System.Numerics;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 玛法世界教主 Boss 类型
/// </summary>
public enum WorldBossType
{
    WomaKing,      // 沃玛教主 (Lv.55, 沃玛寺庙三层)
    BullDemonKing, // 牛魔王 (Lv.60, 牛魔寺庙七层)
    ZumaKing,      // 祖玛教主 (Lv.65, 祖玛教主之家)
    MoonDevil      // 赤月恶魔 (Lv.70, 赤月巢穴)
}

/// <summary>
/// 世界 Boss 战斗阶段
/// </summary>
public enum WorldBossPhase
{
    Idle,       // 休眠未刷新
    Alive,      // 降临激战中
    Enraged,    // 狂暴形态 (生命值低于 50%，伤害与攻速暴增)
    Dead        // 气血归零，触发天女散花大爆
}

/// <summary>
/// 装备极品品质阶位
/// </summary>
public enum ItemRarity
{
    Normal = 0, // 普通 (白色)
    Fine = 1,   // 精品 (绿色，极品属性 1~2 点)
    Rare = 2,   // 极品 (金色，极品属性 3~4 点)
    Epic = 3    // 神圣 (紫色，极品属性 5 点及以上)
}

/// <summary>
/// Boss 专属技能定义
/// </summary>
public record BossSkillDef(string Name, int Damage, float Range, string Description);

/// <summary>
/// 具备归属保护与极品品质的地表战利品
/// </summary>
public class BossLootDrop
{
    public long Id { get; set; }
    public Item? Item { get; set; }
    public int GoldAmount { get; set; }
    public Vector2 TilePos { get; set; }
    public string OwnerName { get; set; } = "";
    public float ProtectSeconds { get; set; } = 60f; // 60秒独享归属拾取权
    public ItemRarity Rarity { get; set; } = ItemRarity.Normal;

    public bool IsGold => GoldAmount > 0;
    public bool IsProtected => ProtectSeconds > 0f && !string.IsNullOrEmpty(OwnerName);

    public string DisplayName
    {
        get
        {
            if (IsGold) return $"金币 ({GoldAmount})";
            if (Item == null) return "未知宝物";

            int bonus = Item.ExtraDc + Item.ExtraMc + Item.ExtraSc + Item.ExtraAc + Item.ExtraMac;
            string rarityTag = Rarity switch
            {
                ItemRarity.Epic => "[神圣] ",
                ItemRarity.Rare => "[极品] ",
                ItemRarity.Fine => "[精品] ",
                _ => ""
            };
            string bonusStr = bonus > 0 ? $" (+{bonus})" : "";
            return $"{rarityTag}{Item.Name}{bonusStr}";
        }
    }
}

/// <summary>
/// 盛大热血传奇十周年：玛法四大世界 Boss 副本与专属掉落爆装归属管理器
/// </summary>
public class WorldBossManager
{
    private static WorldBossManager? _instance;
    public static WorldBossManager Instance => _instance ??= new WorldBossManager();

    // 当前活动的世界 Boss 属性
    public WorldBossType BossType { get; private set; } = WorldBossType.ZumaKing;
    public string BossName { get; private set; } = "祖玛教主";
    public int Level { get; private set; } = 65;
    public int CurrentHp { get; private set; } = 12000;
    public int MaxHp { get; private set; } = 12000;
    public Vector2 TilePos { get; set; } = new(330, 270);
    public WorldBossPhase Phase { get; private set; } = WorldBossPhase.Idle;

    public float SkillCooldownTimer { get; private set; } = 0f;
    public float EnrageThreshold => MaxHp * 0.5f; // 生命低于 50% 狂暴
    public bool IsEnraged => Phase == WorldBossPhase.Enraged;
    public bool IsAlive => Phase == WorldBossPhase.Alive || Phase == WorldBossPhase.Enraged;

    // 伤害统计榜 (PlayerName -> TotalDamage)
    private readonly Dictionary<string, int> _damageMeter = new();
    public IReadOnlyDictionary<string, int> DamageMeter => _damageMeter;

    public string? FirstAttacker { get; private set; }
    public string HighestDamageDealer
    {
        get
        {
            if (_damageMeter.Count == 0) return "无名大侠";
            return _damageMeter.OrderByDescending(kv => kv.Value).First().Key;
        }
    }

    // 掉落战利品列表
    private long _nextLootId = 1;
    private readonly List<BossLootDrop> _activeLoots = [];
    public IReadOnlyList<BossLootDrop> ActiveLoots => _activeLoots;

    // 全服广播队列
    private readonly List<string> _worldBroadcasts = [];
    public IReadOnlyList<string> WorldBroadcasts => _worldBroadcasts;

    public void Reset()
    {
        Phase = WorldBossPhase.Idle;
        _damageMeter.Clear();
        _activeLoots.Clear();
        _worldBroadcasts.Clear();
        FirstAttacker = null;
        SkillCooldownTimer = 0f;
    }

    /// <summary>
    /// 刷新/召唤指定世界教主
    /// </summary>
    public string SpawnBoss(WorldBossType type, Vector2 spawnPos)
    {
        BossType = type;
        TilePos = spawnPos;
        _damageMeter.Clear();
        FirstAttacker = null;
        SkillCooldownTimer = 6f;

        switch (type)
        {
            case WorldBossType.WomaKing:
                BossName = "沃玛教主";
                Level = 55;
                MaxHp = 8000;
                break;

            case WorldBossType.BullDemonKing:
                BossName = "牛魔王";
                Level = 60;
                MaxHp = 10000;
                break;

            case WorldBossType.ZumaKing:
                BossName = "祖玛教主";
                Level = 65;
                MaxHp = 12000;
                break;

            case WorldBossType.MoonDevil:
                BossName = "赤月恶魔";
                Level = 70;
                MaxHp = 15000;
                break;
        }

        CurrentHp = MaxHp;
        Phase = WorldBossPhase.Alive;

        string bc = $"【传世战报】封印震动，万古凶魔【{BossName}】(Lv.{Level} HP:{MaxHp}) 已降临在坐标 ({spawnPos.X:F0}, {spawnPos.Y:F0})！击杀必大爆绝世神兵！";
        _worldBroadcasts.Add(bc);
        return bc;
    }

    /// <summary>
    /// 获取当前 Boss 对应的专属绝技定义
    /// </summary>
    public BossSkillDef GetSkillDefinition() => BossType switch
    {
        WorldBossType.WomaKing      => new BossSkillDef("万钧雷暴", IsEnraged ? 280 : 180, 5.0f, "天雷滚滚，击碎地面并附带眩晕震退"),
        WorldBossType.BullDemonKing => new BossSkillDef("旋风狂焰斩", IsEnraged ? 350 : 220, 4.0f, "烈焰刀锋席卷四方，焚尽周遭一切生灵"),
        WorldBossType.ZumaKing      => new BossSkillDef("石化破空印", IsEnraged ? 420 : 260, 6.0f, "祖玛神力撼动虚空，召唤祖玛卫士并轰击大范围强敌"),
        WorldBossType.MoonDevil     => new BossSkillDef("九幽地刺穿心", IsEnraged ? 500 : 320, 8.0f, "全屏地刺穿膛而过，附带极烈剧毒腐蚀效果"),
        _                           => new BossSkillDef("魔王猛击", 150, 3.0f, "普通猛烈轰击")
    };

    /// <summary>
    /// 记录玩家对 Boss 造成的伤害
    /// </summary>
    public void ApplyDamage(string attackerName, int damage, out bool triggeredEnrage, out bool killed)
    {
        triggeredEnrage = false;
        killed = false;

        if (!IsAlive) return;

        FirstAttacker ??= attackerName;
        _damageMeter[attackerName] = _damageMeter.GetValueOrDefault(attackerName, 0) + damage;
        CurrentHp = Math.Max(0, CurrentHp - damage);

        // 检验是否触发生命低于 50% 的嗜血狂暴
        if (Phase == WorldBossPhase.Alive && CurrentHp <= EnrageThreshold && CurrentHp > 0)
        {
            Phase = WorldBossPhase.Enraged;
            triggeredEnrage = true;
            string enrageBc = $"【天地变色】世界Boss【{BossName}】生命垂危，激发嗜血狂暴态！攻击力激增 50%，大招施法频率加倍！";
            _worldBroadcasts.Add(enrageBc);
        }

        // 检验是否阵亡
        if (CurrentHp <= 0)
        {
            Phase = WorldBossPhase.Dead;
            killed = true;
        }
    }

    /// <summary>
    /// 推进 Boss 战斗逻辑、技能冷却与归属保护倒计时
    /// </summary>
    public bool Update(float dt, Vector2 playerPos, out string? castSkillReport)
    {
        castSkillReport = null;

        // 1. 推进掉落物保护倒计时
        for (int i = _activeLoots.Count - 1; i >= 0; i--)
        {
            var loot = _activeLoots[i];
            if (loot.ProtectSeconds > 0f)
            {
                loot.ProtectSeconds -= dt;
            }
        }

        if (!IsAlive) return false;

        // 2. 推进技能冷却计时
        SkillCooldownTimer -= dt;
        float coolDownMax = IsEnraged ? 5.0f : 8.0f;

        if (SkillCooldownTimer <= 0f)
        {
            SkillCooldownTimer = coolDownMax;
            var skill = GetSkillDefinition();
            float dist = Vector2.Distance(TilePos, playerPos);
            if (dist <= skill.Range)
            {
                castSkillReport = $"【Boss狂怒】{BossName} 施展绝技【{skill.Name}】！({skill.Description})，造成 {skill.Damage} 点范围伤害！";
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 摇点生成极品属性词条
    /// </summary>
    public static (Item Item, ItemRarity Rarity, int TotalBonus) RollRareAffix(Item item, int forceBonus = 0)
    {
        int bonus = forceBonus;
        if (bonus <= 0)
        {
            // 经典极品爆率分段
            int roll = Random.Shared.Next(100);
            if (roll < 50) bonus = 1;
            else if (roll < 80) bonus = 2;
            else if (roll < 95) bonus = 3;
            else if (roll < 99) bonus = 4;
            else bonus = 5;
        }

        // 分配到攻击/魔攻/道术或防御
        switch (item.Type)
        {
            case ItemType.Weapon:
                item.ExtraDc += bonus;
                break;
            case ItemType.Dress:
                item.ExtraAc += (bonus + 1) / 2;
                item.ExtraDc += bonus / 2;
                break;
            case ItemType.Helmet:
            case ItemType.Armring:
            case ItemType.Ring:
                item.ExtraDc += bonus;
                break;
            default:
                item.ExtraDc += bonus;
                break;
        }

        ItemRarity rarity = bonus switch
        {
            >= 5 => ItemRarity.Epic,
            >= 3 => ItemRarity.Rare,
            >= 1 => ItemRarity.Fine,
            _    => ItemRarity.Normal
        };

        return (item, rarity, bonus);
    }

    /// <summary>
    /// Boss 阵亡触发经典 7x7 天女散花满屏大爆
    /// </summary>
    public List<BossLootDrop> TriggerBigExplosion(
        Vector2 centerTile, 
        string killerName, 
        Func<int, int, bool> isBlocked)
    {
        Phase = WorldBossPhase.Dead;
        string highestDealer = HighestDamageDealer;
        string protectedOwner = !string.IsNullOrEmpty(highestDealer) && highestDealer != "无名大侠" 
            ? highestDealer 
            : killerName;

        List<BossLootDrop> newDrops = [];
        List<(Item? item, int gold, bool isGodGear)> pendingLoot = [];

        // 1. 爆 6 堆巨额金币 (每堆 3000 ~ 8000 金币)
        for (int i = 0; i < 6; i++)
        {
            pendingLoot.Add((null, Random.Shared.Next(3000, 8000), false));
        }

        // 2. 爆 8 捆顶级补给 (强效太阳水、万年雪霜)
        for (int i = 0; i < 8; i++)
        {
            pendingLoot.Add((Item.CreateSunPotion(5), 0, false));
        }

        // 3. 爆 Boss 专属神兵宝甲
        switch (BossType)
        {
            case WorldBossType.WomaKing:
                pendingLoot.Add((Item.CreateEbonySword(), 0, true)); // 极品乌木神剑
                pendingLoot.Add((Item.CreateHeavyArmor(), 0, true));
                break;

            case WorldBossType.BullDemonKing:
                pendingLoot.Add((Item.CreateJudgmentMace(), 0, true)); // 裁决之杖
                pendingLoot.Add((Item.CreateHeavyArmor(), 0, true));
                break;

            case WorldBossType.ZumaKing:
                pendingLoot.Add((Item.CreateJudgmentMace(), 0, true)); // 极品裁决之杖
                pendingLoot.Add((Item.CreateGodArmor(), 0, true));     // 战神盔甲
                break;

            case WorldBossType.MoonDevil:
                pendingLoot.Add((Item.CreateJudgmentMace(), 0, true)); // 裁决神兵
                pendingLoot.Add((Item.CreateGodArmor(), 0, true));     // 战神天衣
                break;
        }

        // 4. 经典螺旋花瓣扩散算法 (从中心向周围 7x7 瓦片散落)
        int[] dx = [0,  1,  0, -1,  1, -1,  1, -1,  2, -2,  0,  0,  2, -2,  2, -2,  3, -3,  0,  0];
        int[] dy = [0,  0,  1,  0,  1,  1, -1, -1,  0,  0,  2, -2,  2,  2, -2, -2,  0,  0,  3, -3];
        int posIndex = 0;

        foreach (var (rawItem, gold, isGodGear) in pendingLoot)
        {
            Vector2 spawnTile = centerTile;
            for (int tryCount = 0; tryCount < dx.Length; tryCount++)
            {
                int curIdx = (posIndex + tryCount) % dx.Length;
                int tx = (int)centerTile.X + dx[curIdx];
                int ty = (int)centerTile.Y + dy[curIdx];
                if (!isBlocked(tx, ty))
                {
                    spawnTile = new Vector2(tx, ty);
                    posIndex = curIdx + 1;
                    break;
                }
            }

            Item? finalItem = rawItem;
            ItemRarity rarity = ItemRarity.Normal;

            if (finalItem != null && isGodGear)
            {
                // 神装随机附带极品属性词条
                var rolled = RollRareAffix(finalItem);
                finalItem = rolled.Item;
                rarity = rolled.Rarity;
            }

            BossLootDrop loot = new()
            {
                Id = _nextLootId++,
                Item = finalItem,
                GoldAmount = gold,
                TilePos = spawnTile,
                OwnerName = protectedOwner,
                ProtectSeconds = 60f, // 60秒归属保护
                Rarity = rarity
            };

            _activeLoots.Add(loot);
            newDrops.Add(loot);
        }

        // 5. 生成全服世界击杀广播
        var godLoot = newDrops.FirstOrDefault(d => d.Item != null && d.Rarity >= ItemRarity.Rare);
        string godLootStr = godLoot != null ? $"，引动天降神物【{godLoot.DisplayName}】" : "，满屏大爆万金神药";
        string killBc = $"★★★【全服通告】世界教主【{BossName}】已被【{killerName}】率部合力斩杀！最高伤害归属者【{protectedOwner}】享专属拾取权{godLootStr}！★★★";
        _worldBroadcasts.Add(killBc);

        return newDrops;
    }

    /// <summary>
    /// 尝试拾取世界 Boss 掉落物 (包含 60 秒归属权检验)
    /// </summary>
    public bool TryPickupBossLoot(
        Vector2 playerPos, 
        string pickerName, 
        InventoryManager inventory, 
        ref int currentGold, 
        out string message, 
        float maxDist = 1.25f)
    {
        message = "";
        BossLootDrop? nearest = null;
        float minDist = float.MaxValue;

        foreach (var loot in _activeLoots)
        {
            float d = Vector2.Distance(playerPos, loot.TilePos);
            if (d <= maxDist && d < minDist)
            {
                minDist = d;
                nearest = loot;
            }
        }

        if (nearest == null) return false;

        // 检验归属保护权
        if (nearest.IsProtected && nearest.OwnerName != pickerName)
        {
            message = $"【归属保护】此物属于【{nearest.OwnerName}】，保护倒计时剩余 {(int)nearest.ProtectSeconds} 秒！";
            return false;
        }

        if (nearest.IsGold)
        {
            currentGold += nearest.GoldAmount;
            message = $"[拾取] 获得世界 Boss 掉落金币: {nearest.GoldAmount:N0}！";
            _activeLoots.Remove(nearest);
            return true;
        }

        if (nearest.Item != null)
        {
            if (!inventory.AddItem(nearest.Item))
            {
                message = "包裹已满，无法拾取该宝物！";
                return false;
            }

            string rarityColor = nearest.Rarity switch
            {
                ItemRarity.Epic => "神圣绝品",
                ItemRarity.Rare => "黄金极品",
                ItemRarity.Fine => "精工良品",
                _ => "珍稀战利品"
            };
            message = $"[拾取] 成功缴获【{nearest.DisplayName}】({rarityColor})！";
            _activeLoots.Remove(nearest);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 自定义生成测试用极品掉落物
    /// </summary>
    public BossLootDrop SpawnCustomLoot(Vector2 tilePos, Item item, string owner, ItemRarity rarity)
    {
        BossLootDrop loot = new()
        {
            Id = _nextLootId++,
            Item = item,
            GoldAmount = 0,
            TilePos = tilePos,
            OwnerName = owner,
            ProtectSeconds = 60f,
            Rarity = rarity
        };
        _activeLoots.Add(loot);
        return loot;
    }
}
