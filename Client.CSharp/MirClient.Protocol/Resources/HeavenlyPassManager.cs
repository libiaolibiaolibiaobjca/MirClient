using System;
using System.Collections.Generic;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 天关宝箱类型
/// </summary>
public enum PassChestType
{
    None,
    Silver, // 银宝箱 (消耗 1 张灵符)
    Gold    // 金宝箱 (消耗 2 张灵符)
}

/// <summary>
/// 天关开箱奖励明细
/// </summary>
public record PassRewardResult(
    PassChestType ChestType,
    int ExpAwarded,
    bool IsRainOfBlessing,
    int Diamonds,
    int BlessingOils,
    int GoldCoins,
    string ItemAwarded,
    string Message);

/// <summary>
/// 天关关卡配置
/// </summary>
public record HeavenlyPassFloorConfig(
    int Floor,
    string BossName,
    int BossHp,
    int BossDc,
    int BossAc,
    int BaseExp);

/// <summary>
/// 盛大热血传奇十周年：天雷天关试炼与王者神殿副本管理器 (纯 C# 领域驱动模型)
/// </summary>
public class HeavenlyPassManager
{
    public static HeavenlyPassManager Instance { get; } = new();

    public const int MaxFloors = 5;
    public const int GoldPerTalisman = 50000; // 5万金币兑换一张天关灵符

    // 各层守卫配置
    public static readonly Dictionary<int, HeavenlyPassFloorConfig> FloorConfigs = new()
    {
        { 1, new HeavenlyPassFloorConfig(1, "天关骷髅魔将", 1500, 35, 15, 60000) },
        { 2, new HeavenlyPassFloorConfig(2, "天关沃玛守卫", 3000, 55, 25, 120000) },
        { 3, new HeavenlyPassFloorConfig(3, "天关白野猪王", 6000, 80, 40, 200000) },
        { 4, new HeavenlyPassFloorConfig(4, "天关双头巨魔", 12000, 110, 60, 350000) },
        { 5, new HeavenlyPassFloorConfig(5, "王者神殿·天关魔王", 30000, 160, 85, 800000) }
    };

    // 运行状态
    public int Talismans { get; private set; } = 3; // 初始赠送 3 张灵符
    public int CurrentFloor { get; private set; } = 0; // 0 表示不在试炼中
    public bool IsActive => CurrentFloor >= 1 && CurrentFloor <= MaxFloors;
    public bool IsGuardDefeated { get; private set; } = false;
    public bool HasOpenedChest { get; private set; } = false;

    // 统计记录
    public int TotalClearedFloors { get; private set; } = 0;
    public int TotalDiamondsEarned { get; private set; } = 0;
    public int KingsHallClears { get; private set; } = 0;
    public float FloorTimeElapsed { get; private set; } = 0f;

    /// <summary>
    /// 使用金币兑换天关灵符
    /// </summary>
    public (bool Success, string Message, int ExchangedCount) ExchangeTalismans(ref int playerGold, int count)
    {
        if (count <= 0)
        {
            return (false, "兑换灵符数量必须大于0！", 0);
        }

        long totalCost = (long)count * GoldPerTalisman;
        if (playerGold < totalCost)
        {
            return (false, $"金币不足！兑换 {count} 张天关灵符需要 {totalCost} 金币，当前仅有 {playerGold} 金币。", 0);
        }

        playerGold -= (int)totalCost;
        Talismans += count;
        return (true, $"成功消耗 {totalCost} 金币兑换了 {count} 张天关灵符！当前灵符总数: {Talismans} 张。", count);
    }

    /// <summary>
    /// 直接增加天关灵符
    /// </summary>
    public void AddTalismans(int count)
    {
        if (count > 0)
        {
            Talismans += count;
        }
    }

    /// <summary>
    /// 进入天关试炼第 1 层
    /// </summary>
    public (bool Success, string Message) EnterTrial()
    {
        if (IsActive)
        {
            return (false, $"您已身处天关第 {CurrentFloor} 层试炼中，请击破当前守卫或前往下一层！");
        }

        if (Talismans < 1)
        {
            return (false, "天关灵符不足！进入天关试炼需要消耗 1 张天关灵符。");
        }

        Talismans -= 1;
        CurrentFloor = 1;
        IsGuardDefeated = false;
        HasOpenedChest = false;
        FloorTimeElapsed = 0f;

        var config = FloorConfigs[1];
        return (true, $"消耗 1 张天关灵符，成功进入天关第 1 层！当前守卫: 【{config.BossName}】(生命值:{config.BossHp})。剩余灵符: {Talismans}。");
    }

    /// <summary>
    /// 击败当前关卡守卫
    /// </summary>
    public (bool Success, string Message) DefeatCurrentGuard()
    {
        if (!IsActive)
        {
            return (false, "您当前未处于任何天关试炼中！");
        }

        if (IsGuardDefeated)
        {
            return (false, $"天关第 {CurrentFloor} 层的守卫已被击破，请开启天关金银宝箱或前往下一层！");
        }

        IsGuardDefeated = true;
        HasOpenedChest = false;
        TotalClearedFloors += 1;

        var config = FloorConfigs[CurrentFloor];
        string floorName = CurrentFloor == MaxFloors ? "【王者神殿】" : $"天关第 {CurrentFloor} 层";
        return (true, $"{floorName} 守卫【{config.BossName}】已被成功斩杀！中央涌现出天关金宝箱与银宝箱，请选择开启！");
    }

    /// <summary>
    /// 开启天关金宝箱或银宝箱
    /// </summary>
    public (bool Success, PassRewardResult? Reward) OpenChest(PassChestType chestType, Random? rng = null)
    {
        if (!IsActive)
        {
            return (false, new PassRewardResult(chestType, 0, false, 0, 0, 0, "", "您当前不在天关试炼中！"));
        }

        if (!IsGuardDefeated)
        {
            return (false, new PassRewardResult(chestType, 0, false, 0, 0, 0, "", "当前层守卫尚未被消灭，无法开启宝箱！"));
        }

        if (HasOpenedChest)
        {
            return (false, new PassRewardResult(chestType, 0, false, 0, 0, 0, "", "当前层的天关宝箱已被开启过，请前往下一层或离开试炼！"));
        }

        rng ??= Random.Shared;

        int cost = chestType == PassChestType.Gold ? 2 : 1;
        if (Talismans < cost)
        {
            return (false, new PassRewardResult(chestType, 0, false, 0, 0, 0, "", $"天关灵符不足！开启 {chestType} 宝箱需要 {cost} 张灵符，当前仅有 {Talismans} 张。"));
        }

        Talismans -= cost;
        HasOpenedChest = true;

        var config = FloorConfigs[CurrentFloor];
        int baseExp = config.BaseExp;
        int expAwarded;
        bool isRainOfBlessing = false;
        int diamonds = 0;
        int blessingOils = 0;
        int goldCoins = 0;
        string itemName = "";

        if (chestType == PassChestType.Silver)
        {
            // 银宝箱：20% 几率触发天降甘霖双倍经验
            isRainOfBlessing = rng.Next(100) < 20;
            expAwarded = isRainOfBlessing ? baseExp * 2 : baseExp;
            goldCoins = 50000 * CurrentFloor;

            // 40% 概率获得祝福油，30% 技能书页
            int roll = rng.Next(100);
            if (roll < 40)
            {
                blessingOils = 1;
                itemName = "天关祝福油";
            }
            else if (roll < 70)
            {
                itemName = "天书残页";
            }
        }
        else // PassChestType.Gold
        {
            // 金宝箱：经验 2.5 倍基准，25% 几率触发天降甘霖
            isRainOfBlessing = rng.Next(100) < 25;
            expAwarded = (int)(baseExp * 2.5f);
            if (isRainOfBlessing)
            {
                expAwarded *= 2;
            }

            goldCoins = 150000 * CurrentFloor;

            // 保底 1~2 颗金刚石，终极层保底 5 颗
            if (CurrentFloor == MaxFloors)
            {
                diamonds = 5;
                blessingOils = 2;
                itemName = "王者神石·赤金斩刃";
            }
            else
            {
                diamonds = rng.Next(1, 3);
                blessingOils = 1;
                itemName = CurrentFloor >= 3 ? "赤月雷霆战戒" : "祖玛圣战项链";
            }

            TotalDiamondsEarned += diamonds;
        }

        string rainMsg = isRainOfBlessing ? "【天降甘霖·暴击双倍经验!】" : "";
        string diamondMsg = diamonds > 0 ? $" 金刚石 x{diamonds}!" : "";
        string oilMsg = blessingOils > 0 ? $" 祝福油 x{blessingOils}!" : "";
        string itemMsg = !string.IsNullOrEmpty(itemName) ? $" 极品获得: 【{itemName}】!" : "";

        string resultMsg = $"成功开启天关{chestType}宝箱！{rainMsg} 荣获经验 +{expAwarded:#,0}，金币 +{goldCoins:#,0}{diamondMsg}{oilMsg}{itemMsg} 剩余灵符: {Talismans}。";

        var result = new PassRewardResult(
            chestType,
            expAwarded,
            isRainOfBlessing,
            diamonds,
            blessingOils,
            goldCoins,
            itemName,
            resultMsg);

        return (true, result);
    }

    /// <summary>
    /// 前往天关下一层或登顶王者神殿
    /// </summary>
    public (bool Success, string Message, bool IsCrownAchieved) NextFloor()
    {
        if (!IsActive)
        {
            return (false, "您当前未处于天关试炼中！", false);
        }

        if (!IsGuardDefeated)
        {
            return (false, $"天关第 {CurrentFloor} 层守卫尚未消灭，无法开启传送门！", false);
        }

        // 检查是否已经是最后一层（第5层王者神殿）
        if (CurrentFloor >= MaxFloors)
        {
            KingsHallClears += 1;
            int clearedFloor = CurrentFloor;
            CurrentFloor = 0; // 通关离开
            IsGuardDefeated = false;
            HasOpenedChest = false;

            return (true, $"【荣耀登顶】恭喜勇士成功通关天关全部 {clearedFloor} 层试炼，斩灭王者魔王，荣耀加冕王者神殿！累积通关次数: {KingsHallClears} 次。", true);
        }

        // 前往下一层需要 1 张灵符
        if (Talismans < 1)
        {
            return (false, $"进入下一层需要 1 张天关灵符！当前灵符不足，请退出试炼补充灵符后再战。", false);
        }

        Talismans -= 1;
        CurrentFloor += 1;
        IsGuardDefeated = false;
        HasOpenedChest = false;
        FloorTimeElapsed = 0f;

        var config = FloorConfigs[CurrentFloor];
        string floorTitle = CurrentFloor == MaxFloors ? "【终极·王者神殿】" : $"天关第 {CurrentFloor} 层";
        return (true, $"消耗 1 张灵符，成功跨入 {floorTitle}！守卫【{config.BossName}】(生命值:{config.BossHp}) 正在殿前驻守！剩余灵符: {Talismans}。", false);
    }

    /// <summary>
    /// 中途离开天关试炼
    /// </summary>
    public (bool Success, string Message) ExitTrial()
    {
        if (!IsActive)
        {
            return (false, "您当前未在天关试炼中。");
        }

        int exitedFloor = CurrentFloor;
        CurrentFloor = 0;
        IsGuardDefeated = false;
        HasOpenedChest = false;

        return (true, $"已安全退出天关试炼第 {exitedFloor} 层，返回盟重省安全区。剩余灵符: {Talismans} 张。");
    }

    /// <summary>
    /// 驱动天关内部计时
    /// </summary>
    public void Update(float deltaTime)
    {
        if (IsActive)
        {
            FloorTimeElapsed += deltaTime;
        }
    }

    /// <summary>
    /// 重置状态（测试使用）
    /// </summary>
    public void Reset()
    {
        Talismans = 3;
        CurrentFloor = 0;
        IsGuardDefeated = false;
        HasOpenedChest = false;
        TotalClearedFloors = 0;
        TotalDiamondsEarned = 0;
        KingsHallClears = 0;
        FloorTimeElapsed = 0f;
    }
}
