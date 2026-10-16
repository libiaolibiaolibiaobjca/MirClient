using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 盟重酒馆美酒品阶类型
/// </summary>
public enum WineType
{
    Sorghum,  // 高粱酒 (烈性醇酒，提升物理与魔法攻击)
    RedYeast, // 红曲酒 (温润甘洌，大幅强化物理与魔法双防)
    Fruit,    // 果露酒 (清香宜人，增加生命回复与暴击率)
    Huadiao   // 花雕酒 (十年陈酿极品，全属性综合大幅激增)
}

/// <summary>
/// 美酒基础定义
/// </summary>
public record WineDefinition(
    WineType Type,
    string Name,
    int AlcoholDegree, // 酒精烈度 (增加醉酒度)
    int PotencyGain,   // 转化为药力值
    int BonusDc,       // 临时攻击加成
    int BonusAc,       // 临时双防加成
    int BonusCrit,     // 临时暴击加成
    string Description);

/// <summary>
/// 地下涌泉采集点
/// </summary>
public class SpringWaterPoint
{
    public long Id { get; set; }
    public Vector2 TilePos { get; set; }
    public int RemainingWater { get; set; } = 5; // 每眼泉眼可汲水 5 次
    public float RespawnTimer { get; set; } = 0f;
    public bool IsDry => RemainingWater <= 0;
}

/// <summary>
/// 饮酒激发的战斗 Buff
/// </summary>
public class ActiveWineBuff
{
    public WineType Type { get; set; }
    public string Name { get; set; } = "";
    public int BonusDc { get; set; }
    public int BonusAc { get; set; }
    public int BonusCrit { get; set; }
    public float Duration { get; set; } = 300f; // 持续 300 秒 (5分钟)
    public bool IsExpired => Duration <= 0f;
}

/// <summary>
/// 盛大热血传奇十周年：盟重酒馆泉水酿酒与药力醉酒度管理器 (纯 C# 领域驱动模型)
/// </summary>
public class WineManager
{
    private static WineManager? _instance;
    public static WineManager Instance => _instance ??= new WineManager();

    // 饮酒与成长属性
    public int SpringWaterJars { get; set; } = 5;      // 持有天泉水坛数
    public float CurrentDrunkenness { get; set; } = 0f;// 当前醉酒度
    public float AlcoholTolerance { get; set; } = 50f; // 酒量上限 (初始50，饮酒可锻炼提升至200)
    public int WinePotency { get; set; } = 0;          // 累积药力值
    public int PotencyLevel { get; set; } = 0;         // 药力境界 (0~10重)

    // 已酿造的美酒随身库存 (WineType -> Count)
    private readonly Dictionary<WineType, int> _wineInventory = new()
    {
        [WineType.Sorghum]  = 2,
        [WineType.RedYeast] = 1,
        [WineType.Fruit]    = 1,
        [WineType.Huadiao]  = 0
    };

    public IReadOnlyDictionary<WineType, int> WineInventory => _wineInventory;

    // 当前激活的美酒 Buff
    public ActiveWineBuff? CurrentBuff { get; private set; }

    // 场景中分布的地下涌泉点
    private readonly List<SpringWaterPoint> _springPoints = [];
    public IReadOnlyList<SpringWaterPoint> SpringPoints => _springPoints;

    // 永久药力属性增益 (易筋洗髓突破所得)
    public int PermanentBonusHp => PotencyLevel * 40;
    public int PermanentBonusAc => PotencyLevel * 3;
    public int PermanentBonusDc => PotencyLevel * 2;

    // 醉酒状态判定
    public bool IsDrunk => CurrentDrunkenness > AlcoholTolerance;
    public bool HasActiveBuff => CurrentBuff != null && !CurrentBuff.IsExpired;

    public static WineDefinition GetWineDef(WineType type) => type switch
    {
        WineType.Sorghum => new WineDefinition(
            WineType.Sorghum, "高粱酒", 20, 25, 15, 5, 0, "烈火纯阳高粱酿造，饮后浑身燥热，攻击力大幅提升"),
        WineType.RedYeast => new WineDefinition(
            WineType.RedYeast, "红曲酒", 15, 30, 5, 12, 0, "千载红曲古法封坛，酒香醇和，固本培元强化双防"),
        WineType.Fruit => new WineDefinition(
            WineType.Fruit, "果露酒", 10, 20, 8, 4, 8, "采摘天山灵果慢酿，清冽甘甜，通畅经络提升暴击"),
        WineType.Huadiao => new WineDefinition(
            WineType.Huadiao, "花雕酒", 25, 50, 25, 16, 12, "十载陈酿花雕极品，琼浆玉液，战力与抗性全方位飞跃"),
        _ => new WineDefinition(WineType.Sorghum, "米酒", 10, 10, 5, 2, 0, "普通水酒")
    };

    public void Reset()
    {
        SpringWaterJars = 5;
        CurrentDrunkenness = 0f;
        AlcoholTolerance = 50f;
        WinePotency = 0;
        PotencyLevel = 0;
        CurrentBuff = null;
        _wineInventory[WineType.Sorghum]  = 2;
        _wineInventory[WineType.RedYeast] = 1;
        _wineInventory[WineType.Fruit]    = 1;
        _wineInventory[WineType.Huadiao]  = 0;
        _springPoints.Clear();
    }

    /// <summary>
    /// 初始化或在指定坐标激发出地下涌泉
    /// </summary>
    public SpringWaterPoint SpawnSpringPoint(Vector2 tilePos)
    {
        var point = new SpringWaterPoint
        {
            Id = _springPoints.Count + 1,
            TilePos = tilePos,
            RemainingWater = 5,
            RespawnTimer = 0f
        };
        _springPoints.Add(point);
        return point;
    }

    /// <summary>
    /// 走近地下涌泉汲取泉水
    /// </summary>
    public (bool Success, string Message) GatherSpringWater(long springId)
    {
        var sp = _springPoints.FirstOrDefault(p => p.Id == springId);
        if (sp == null)
        {
            return (false, "此处未探寻到地下涌泉！");
        }

        if (sp.IsDry)
        {
            return (false, "这处地下涌泉已被汲取干涸，需待地脉甘泉重新涌现！");
        }

        sp.RemainingWater--;
        SpringWaterJars++;

        string status = sp.RemainingWater > 0 ? $"剩余出水量: {sp.RemainingWater}坛" : "泉眼已暂时汲干";
        return (true, $"【汲取泉水】成功舀取一坛清洌甘甜的【天泉水】！(持有天泉水: {SpringWaterJars}坛，{status})");
    }

    /// <summary>
    /// 投入天泉水与酒曲进行酿酒
    /// </summary>
    public (bool Success, string Message) BrewWine(WineType type)
    {
        if (SpringWaterJars <= 0)
        {
            return (false, "酿酒原料不足！缺少核心主料【天泉水】，请前往盟重荒野涌泉汲水！");
        }

        var def = GetWineDef(type);
        SpringWaterJars--;
        _wineInventory[type] = _wineInventory.GetValueOrDefault(type, 0) + 1;

        return (true, $"【酒馆封坛】消耗天泉水一坛与精选曲药，成功酿造出陈香四溢的【{def.Name}】！(库存: {_wineInventory[type]}坛)");
    }

    /// <summary>
    /// 痛饮美酒：获得增益、增加醉酒度并沉淀药力
    /// </summary>
    public (bool Success, string Message) DrinkWine(WineType type)
    {
        int count = _wineInventory.GetValueOrDefault(type, 0);
        if (count <= 0)
        {
            var defNone = GetWineDef(type);
            return (false, $"随身包裹中已无【{defNone.Name}】，请前往酒馆酿造！");
        }

        _wineInventory[type] = count - 1;
        var def = GetWineDef(type);

        // 增加醉酒度
        CurrentDrunkenness += def.AlcoholDegree;

        // 酒量锻炼成长 (每次饮酒酒量上限微增 +0.5)
        if (AlcoholTolerance < 200f)
        {
            AlcoholTolerance = Math.Min(200f, AlcoholTolerance + 0.5f);
        }

        // 积累药力值并判定药力境界突破 (每 100 药力突破一重)
        WinePotency += def.PotencyGain;
        int targetLevel = Math.Clamp(WinePotency / 100, 0, 10);
        bool breakthrough = false;
        if (targetLevel > PotencyLevel)
        {
            PotencyLevel = targetLevel;
            breakthrough = true;
        }

        // 激活美酒 Buff (300秒)
        CurrentBuff = new ActiveWineBuff
        {
            Type = type,
            Name = def.Name,
            BonusDc = def.BonusDc,
            BonusAc = def.BonusAc,
            BonusCrit = def.BonusCrit,
            Duration = 300f
        };

        string drunkTag = IsDrunk ? " [微醺踉跄，步伐飘忽!]" : " [豪气干云，酒意正酣!]";
        string breakMsg = breakthrough ? $" ★★★ 药力激荡突破至【第{PotencyLevel}重境界】！永久气血+{PermanentBonusHp}、双防+{PermanentBonusAc}！ ★★★" : "";
        string msg = $"【开坛畅饮】痛饮一坛【{def.Name}】！物攻+{def.BonusDc}、双防+{def.BonusAc}、暴击+{def.BonusCrit}%(持续5分钟)！醉酒度: {(int)CurrentDrunkenness}/{(int)AlcoholTolerance}{drunkTag}{breakMsg}";

        return (true, msg);
    }

    /// <summary>
    /// 时间更新 (醉酒度消退自然醒酒、Buff衰减、涌泉泉眼复苏)
    /// </summary>
    public void Update(float dt)
    {
        // 1. 自然醒酒：每秒衰减 0.4 点醉酒度
        if (CurrentDrunkenness > 0f)
        {
            CurrentDrunkenness = Math.Max(0f, CurrentDrunkenness - dt * 0.4f);
        }

        // 2. 美酒 Buff 计时消退
        if (CurrentBuff != null)
        {
            CurrentBuff.Duration -= dt;
            if (CurrentBuff.IsExpired)
            {
                CurrentBuff = null;
            }
        }

        // 3. 干涸涌泉复苏计时
        foreach (var p in _springPoints)
        {
            if (p.IsDry)
            {
                p.RespawnTimer += dt;
                if (p.RespawnTimer >= 60f) // 60秒后泉水重新汩汩涌出
                {
                    p.RemainingWater = 5;
                    p.RespawnTimer = 0f;
                }
            }
        }
    }
}
