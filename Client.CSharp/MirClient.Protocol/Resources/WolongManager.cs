using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 卧龙山庄探秘副本阶段
/// </summary>
public enum WolongInstancePhase
{
    Idle,           // 未开启
    Active,         // 探秘激战中
    TreasureReady,  // 五元灵珠集齐，火龙宝藏解锁就绪
    Completed,      // 降服庄主，通关大圆满
    Expired         // 限时耗尽
}

/// <summary>
/// 卧龙山庄五元守护者与卧龙庄主
/// </summary>
public enum WolongGuardianType
{
    Metal, // 金之守护者 (白虎星君)
    Wood,  // 木之守护者 (青龙圣使)
    Water, // 水之守护者 (玄武真君)
    Fire,  // 火之守护者 (朱雀神尊)
    Earth, // 土之守护者 (厚土统领)
    Lord   // 终极庄主 (卧龙庄主)
}

/// <summary>
/// 守护者属性配置
/// </summary>
public record WolongGuardianDef(
    WolongGuardianType Type,
    string Name,
    string Title,
    int MaxHp,
    int Dc,
    int Ac,
    string SkillName,
    string OrbName,
    string Description);

/// <summary>
/// 守护者阵亡后倒地化作的名将遗体灵魄 (可供灵符摸尸)
/// </summary>
public class GuardianCorpse
{
    public long Id { get; set; }
    public WolongGuardianType Type { get; set; }
    public string Name { get; set; } = "";
    public Vector2 TilePos { get; set; }
    public float RemainingSeconds { get; set; } = 60f; // 60秒探索期限
    public bool IsSearched { get; set; } = false;
    public bool IsExpired => RemainingSeconds <= 0f;
}

/// <summary>
/// 灵符摸尸搜寻战利品结果
/// </summary>
public record WolongLootResult(
    bool Success,
    string Message,
    int PagesCount,
    string OrbName,
    int NightPearlsCount,
    string RareItemName);

/// <summary>
/// 盛大热血传奇十周年：卧龙山庄探秘与五元守护者副本管理器 (纯 C# 领域驱动模型)
/// </summary>
public class WolongManager
{
    private static WolongManager? _instance;
    public static WolongManager Instance => _instance ??= new WolongManager();

    public const float TotalInstanceSeconds = 1800f; // 30 分钟限时副本
    public const int CharmsCostPerSearch = 2;        // 每次摸尸消耗 2 张灵符

    // 副本状态
    public WolongInstancePhase Phase { get; private set; } = WolongInstancePhase.Idle;
    public float RemainingTime { get; private set; } = 0f;
    public int CharmsCount { get; set; } = 20;       // 玩家持有灵符
    public int AncientPages { get; set; } = 0;       // 玩家累积古书残页
    public int NightPearls { get; set; } = 0;        // 玩家累积夜明珠

    // 五元灵珠收集与击败进度
    private readonly HashSet<string> _collectedOrbs = [];
    private readonly HashSet<WolongGuardianType> _defeatedGuardians = [];
    private readonly List<GuardianCorpse> _activeCorpses = [];
    private long _corpseIdCounter = 1;

    public IReadOnlyCollection<string> CollectedOrbs => _collectedOrbs;
    public IReadOnlyCollection<WolongGuardianType> DefeatedGuardians => _defeatedGuardians;
    public IReadOnlyList<GuardianCorpse> ActiveCorpses => _activeCorpses;

    public bool IsInstanceActive => Phase == WolongInstancePhase.Active || Phase == WolongInstancePhase.TreasureReady;
    public bool HasCollectedAllOrbs => _collectedOrbs.Count >= 5;

    public static WolongGuardianDef GetGuardianDef(WolongGuardianType type) => type switch
    {
        WolongGuardianType.Metal => new WolongGuardianDef(
            WolongGuardianType.Metal, "金之守护者", "白虎星君", 6000, 160, 40, "金元破甲斩", "金灵珠", "镇守西方金庚古书，刀芒凌厉，削铁如泥"),
        WolongGuardianType.Wood => new WolongGuardianDef(
            WolongGuardianType.Wood, "木之守护者", "青龙圣使", 5500, 140, 35, "木元生生诀", "木灵珠", "镇守东方青木古书，剧毒缠绕，自愈生机"),
        WolongGuardianType.Water => new WolongGuardianDef(
            WolongGuardianType.Water, "水之守护者", "玄武真君", 7000, 120, 50, "水元冰霜盾", "水灵珠", "镇守北方玄水古书，冰甲坚不可摧，寒气彻骨"),
        WolongGuardianType.Fire => new WolongGuardianDef(
            WolongGuardianType.Fire, "火之守护者", "朱雀神尊", 5000, 180, 30, "火元焚天劫", "火灵珠", "镇守南方烈火古书，滔天炎浪，爆烈炽热"),
        WolongGuardianType.Earth => new WolongGuardianDef(
            WolongGuardianType.Earth, "土之守护者", "厚土统领", 6500, 150, 45, "土元崩山印", "土灵珠", "镇守中方后土古书，重力压制，沉稳如山"),
        WolongGuardianType.Lord => new WolongGuardianDef(
            WolongGuardianType.Lord, "卧龙庄主", "绝代剑圣", 12000, 220, 55, "卧龙破极斩", "火龙逆鳞", "卧龙山庄至尊庄主，手握卧龙神剑，独占火龙宝藏"),
        _ => new WolongGuardianDef(WolongGuardianType.Metal, "古书名将", "守将", 5000, 100, 30, "猛击", "灵珠", "神秘守将")
    };

    public void Reset()
    {
        Phase = WolongInstancePhase.Idle;
        RemainingTime = 0f;
        _collectedOrbs.Clear();
        _defeatedGuardians.Clear();
        _activeCorpses.Clear();
        _corpseIdCounter = 1;
    }

    /// <summary>
    /// 开启卧龙山庄副本
    /// </summary>
    public string StartInstance()
    {
        Phase = WolongInstancePhase.Active;
        RemainingTime = TotalInstanceSeconds;
        _collectedOrbs.Clear();
        _defeatedGuardians.Clear();
        _activeCorpses.Clear();
        if (CharmsCount < 10) CharmsCount = 10; // 保底赠予灵符

        return "【卧龙山庄】禁制轰然开启！限时 30 分钟探秘五元古书守护者！请携带【灵符】搜寻守将遗体，开启火龙宝藏！";
    }

    /// <summary>
    /// 副本主循环更新 (倒计时与遗体自然消散)
    /// </summary>
    public void Update(float dt, out string? timeEvent)
    {
        timeEvent = null;

        if (Phase == WolongInstancePhase.Active || Phase == WolongInstancePhase.TreasureReady)
        {
            RemainingTime -= dt;
            if (RemainingTime <= 0f)
            {
                RemainingTime = 0f;
                Phase = WolongInstancePhase.Expired;
                timeEvent = "【卧龙山庄】30分钟探秘时限已到！山庄护山大阵再度闭合，全员退出副本！";
                return;
            }
        }

        // 遗体存在时钟衰减
        for (int i = _activeCorpses.Count - 1; i >= 0; i--)
        {
            var corpse = _activeCorpses[i];
            corpse.RemainingSeconds -= dt;
            if (corpse.IsExpired)
            {
                _activeCorpses.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 击败守护者名将，生成名将遗体灵魄
    /// </summary>
    public GuardianCorpse DefeatGuardian(WolongGuardianType type, Vector2 tilePos)
    {
        _defeatedGuardians.Add(type);
        var def = GetGuardianDef(type);

        var corpse = new GuardianCorpse
        {
            Id = _corpseIdCounter++,
            Type = type,
            Name = def.Name,
            TilePos = tilePos,
            RemainingSeconds = 60f,
            IsSearched = false
        };

        _activeCorpses.Add(corpse);

        // 如果击败全部五位守护者且五元齐全
        if (HasCollectedAllOrbs && Phase == WolongInstancePhase.Active)
        {
            Phase = WolongInstancePhase.TreasureReady;
        }

        return corpse;
    }

    /// <summary>
    /// 消耗灵符摸尸搜寻名将遗体
    /// </summary>
    public WolongLootResult SearchCorpse(long corpseId)
    {
        var corpse = _activeCorpses.FirstOrDefault(c => c.Id == corpseId);
        if (corpse == null || corpse.IsExpired)
        {
            return new WolongLootResult(false, "眼前的名将遗体已化作清风消散！", 0, "", 0, "");
        }

        if (corpse.IsSearched)
        {
            return new WolongLootResult(false, "该守将遗体已被灵符探索一空！", 0, "", 0, "");
        }

        if (CharmsCount < CharmsCostPerSearch)
        {
            return new WolongLootResult(false, $"灵符不足！摸尸探索需要消耗 {CharmsCostPerSearch} 张【卧龙灵符】(当前持有: {CharmsCount}张)！", 0, "", 0, "");
        }

        // 扣除灵符
        CharmsCount -= CharmsCostPerSearch;
        corpse.IsSearched = true;

        var def = GetGuardianDef(corpse.Type);

        // 掉落计算：必出 2~4 张古书残页 + 专属五元灵珠
        int pages = Random.Shared.Next(2, 5);
        AncientPages += pages;

        string orb = def.OrbName;
        bool isNewOrb = _collectedOrbs.Add(orb);

        // 35% 概率摸出夜明珠
        int pearls = Random.Shared.Next(100) < 35 ? 1 : 0;
        NightPearls += pearls;

        // 20% 概率搜出稀有名将极品装备
        string rareEquip = "";
        if (Random.Shared.Next(100) < 20)
        {
            rareEquip = corpse.Type switch
            {
                WolongGuardianType.Metal => "天龙战戒(极品+3)",
                WolongGuardianType.Wood  => "圣龙道镯(极品+3)",
                WolongGuardianType.Water => "魔龙法带(极品+3)",
                WolongGuardianType.Fire  => "狂雷战靴(极品+3)",
                WolongGuardianType.Earth => "通天道冠(极品+3)",
                WolongGuardianType.Lord  => "卧龙神剑(圣神+5)",
                _ => "卧龙宝珠"
            };
        }

        // 检查五元集齐
        if (HasCollectedAllOrbs && Phase == WolongInstancePhase.Active)
        {
            Phase = WolongInstancePhase.TreasureReady;
        }

        string orbInfo = isNewOrb ? $"【{orb}】(新获得)" : $"【{orb}】(已收录)";
        string pearlInfo = pearls > 0 ? "，并巧获绝品【夜明珠】x1" : "";
        string equipInfo = !string.IsNullOrEmpty(rareEquip) ? $"！天降鸿运摸出绝世神装【{rareEquip}】" : "";
        string log = $"消耗 {CharmsCostPerSearch} 张灵符搜寻【{def.Name}】！获得【古书残页】x{pages}、五元秘宝{orbInfo}{pearlInfo}{equipInfo}！(五元进度: {_collectedOrbs.Count}/5)";

        return new WolongLootResult(true, log, pages, orb, pearls, rareEquip);
    }

    /// <summary>
    /// 解开并开启火龙宝藏大爆
    /// </summary>
    public (bool Success, string Message, List<string> Rewards) OpenFireDragonTreasure()
    {
        if (!HasCollectedAllOrbs)
        {
            return (false, $"五元未齐！尚需凑齐金木水火土五枚灵珠 (当前进度: {_collectedOrbs.Count}/5) 方可破译火龙宝藏！", []);
        }

        Phase = WolongInstancePhase.Completed;

        var rewards = new List<string>
        {
            "卧龙神印 (心法绝学秘籍)",
            "火龙宝珠 (气血上限+800)",
            "天龙圣衣 (双防+15,极品+5)",
            "巨额金币 2,000,000"
        };

        string broadcast = "★★★【卧龙探秘全服通告】有绝世豪侠集齐金木水火土五元灵珠，轰然破开【卧龙火龙宝藏】！无数奇珍异宝天降玛法！ ★★★";
        return (true, broadcast, rewards);
    }
}
