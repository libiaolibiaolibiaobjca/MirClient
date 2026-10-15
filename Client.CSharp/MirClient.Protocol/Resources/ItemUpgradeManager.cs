namespace MirClient.Protocol.Resources;

/// <summary>
/// 盛大热血传奇十周年：装备持久磨损、特修与铁匠铺升级锻造系统管理器 (纯 C# 领域状态机)
/// </summary>
public class ItemUpgradeManager
{
    private static ItemUpgradeManager? _instance;
    public static ItemUpgradeManager Instance => _instance ??= new ItemUpgradeManager();

    public const int MAX_UPGRADE_LEVEL = 7; // 盛大经典单件武器最高淬炼升级 +7
    public const int ATTACK_WEAR_INTERVAL = 5; // 每 5 次挥砍攻击磨损 1 点武器持久
    public const int STRUCK_WEAR_INTERVAL = 8; // 每 8 次受击格挡磨损 1 点防具持久

    public const int NORMAL_REPAIR_UNIT_COST = 100; // 普通修理每点持久金币消耗
    public const int SPECIAL_REPAIR_UNIT_COST = 300; // 神圣特修每点持久金币消耗

    private int _attackCounter = 0;
    private int _struckCounter = 0;

    public ItemUpgradeManager() { }

    /// <summary>
    /// 重置磨损计数器与状态 (用于单元测试)
    /// </summary>
    public void Reset()
    {
        _attackCounter = 0;
        _struckCounter = 0;
    }

    /// <summary>
    /// 角色挥刀攻击时的武器持久磨损处理
    /// </summary>
    public bool WearWeaponOnAttack(InventoryManager inv, out string wearMsg)
    {
        wearMsg = "";
        Item? weapon = inv.EquippedItems[(int)EquipSlot.Weapon];
        if (weapon == null || weapon.MaxDura <= 0) return false;

        if (weapon.Dura <= 0)
        {
            wearMsg = $"【警告】手持神兵【{weapon.Name}】持久已耗尽损坏，神力失效！请尽快前往铁匠铺修理！";
            return true;
        }

        _attackCounter++;
        if (_attackCounter >= ATTACK_WEAR_INTERVAL)
        {
            _attackCounter = 0;
            weapon.Dura = Math.Max(0, weapon.Dura - 1);

            if (weapon.Dura <= 0)
            {
                wearMsg = $"【破损警报】手持神兵【{weapon.Name}】在激战中持久归零损坏！基础攻击属性暂时失效！";
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 角色受击格挡时的防具衣服持久磨损处理
    /// </summary>
    public bool WearArmorOnStruck(InventoryManager inv, out string wearMsg)
    {
        wearMsg = "";
        Item? dress = inv.EquippedItems[(int)EquipSlot.Dress];
        if (dress == null || dress.MaxDura <= 0) return false;

        if (dress.Dura <= 0)
        {
            wearMsg = $"【警告】所穿战甲【{dress.Name}】持久已耗尽破损，防御庇护失效！";
            return true;
        }

        _struckCounter++;
        if (_struckCounter >= STRUCK_WEAR_INTERVAL)
        {
            _struckCounter = 0;
            dress.Dura = Math.Max(0, dress.Dura - 1);

            if (dress.Dura <= 0)
            {
                wearMsg = $"【破损警报】所穿战甲【{dress.Name}】受重创破损！防御与魔御加成暂时失效！";
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 铁匠铺普通修理 (耗费金币少，但有 20% 几率损伤 1 点持久上限)
    /// </summary>
    public bool RepairItemNormal(Item item, ref int gold, out string msg)
    {
        if (item.MaxDura <= 0 || item.Dura >= item.MaxDura)
        {
            msg = $"【{item.Name}】光洁如新，无需修理！";
            return false;
        }

        int missing = item.MaxDura - item.Dura;
        int cost = missing * NORMAL_REPAIR_UNIT_COST;

        if (gold < cost)
        {
            msg = $"修理资金不足！普修【{item.Name}】需要 {cost} 金币，您仅有 {gold} 金币。";
            return false;
        }

        gold -= cost;

        // 20% 概率损伤 1 点持久上限
        bool dropped = Random.Shared.Next(100) < 20;
        if (dropped && item.MaxDura > 1)
        {
            item.MaxDura -= 1;
        }

        item.Dura = item.MaxDura;

        msg = dropped
            ? $"【普修完成】铁匠技艺生疏，【{item.Name}】持久恢复至满，但持久上限磨损 -1 (当前: {item.Dura}/{item.MaxDura})，消耗金币: {cost}！"
            : $"【普修完成】【{item.Name}】修复如初 (持久: {item.Dura}/{item.MaxDura})，消耗金币: {cost}。";

        return true;
    }

    /// <summary>
    /// 铁匠铺神圣特修 / 战神油特修 (耗费 3 倍金币，100% 保留持久上限)
    /// </summary>
    public bool RepairItemSpecial(Item item, ref int gold, out string msg)
    {
        if (item.MaxDura <= 0 || item.Dura >= item.MaxDura)
        {
            msg = $"【{item.Name}】完好无损，无需特修！";
            return false;
        }

        int missing = item.MaxDura - item.Dura;
        int cost = missing * SPECIAL_REPAIR_UNIT_COST;

        if (gold < cost)
        {
            msg = $"特修资金不足！神圣特修【{item.Name}】需要 {cost} 金币，您仅有 {gold} 金币。";
            return false;
        }

        gold -= cost;
        item.Dura = item.MaxDura;

        msg = $"【神圣特修】神匠秘法淬炼！【{item.Name}】完美复原至巅峰持久 ({item.Dura}/{item.MaxDura})，上限丝毫未损，消耗金币: {cost}！";
        return true;
    }

    /// <summary>
    /// 一键全修身上及背包内全部破损装备
    /// </summary>
    public int RepairAll(InventoryManager inv, ref int gold, bool special, out string summary)
    {
        int repairedCount = 0;
        int totalSpent = 0;
        int initialGold = gold;

        // 1. 修理已穿戴装备
        foreach (var eq in inv.EquippedItems)
        {
            if (eq != null && eq.MaxDura > 0 && eq.Dura < eq.MaxDura)
            {
                if (special ? RepairItemSpecial(eq, ref gold, out _) : RepairItemNormal(eq, ref gold, out _))
                {
                    repairedCount++;
                }
            }
        }

        // 2. 修理背包内装备
        foreach (var item in inv.BagItems)
        {
            if (item != null && item.MaxDura > 0 && item.Dura < item.MaxDura)
            {
                if (special ? RepairItemSpecial(item, ref gold, out _) : RepairItemNormal(item, ref gold, out _))
                {
                    repairedCount++;
                }
            }
        }

        totalSpent = initialGold - gold;
        string typeStr = special ? "神圣特殊修理" : "普通修理";
        summary = $"【一键全修】已对全身及包裹内 {repairedCount} 件破损装备完成{typeStr}，累计耗资 {totalSpent:N0} 金币！";
        return repairedCount;
    }

    /// <summary>
    /// 铁匠铺经典黑铁矿石与首饰淬炼武器升级
    /// </summary>
    /// <param name="weapon">目标武器</param>
    /// <param name="orePurityTotal">黑铁矿石总纯度 (>=60 升持久，<30 掉持久)</param>
    /// <param name="jewelryTotalDc">首饰垫刀属性总和 (影响成功率)</param>
    /// <param name="msg">返回信息</param>
    public bool StartWeaponUpgrade(Item weapon, int orePurityTotal, int jewelryTotalDc, out string msg)
    {
        if (weapon.Type != ItemType.Weapon)
        {
            msg = "唯有神兵利器方可放入铁匠炉进行属性升级淬炼！";
            return false;
        }

        if (weapon.UpgradeLevel >= MAX_UPGRADE_LEVEL)
        {
            msg = $"该神兵已历经 {MAX_UPGRADE_LEVEL} 次神力觉醒达至人界极境 (+{weapon.UpgradeLevel})，无法继续升级！";
            return false;
        }

        if (weapon.InUpgradeTrial)
        {
            msg = $"【{weapon.Name}】炉火淬炼已就绪，正处于待试刀开刃状态！请持此剑斩怪开刃！";
            return false;
        }

        // 经典升级算法：
        // 成功率 = 55% + (首饰总攻击 * 3)% - (已升级次数 * 5)%，上下限钳制在 35% ~ 85%
        int successRate = 55 + (jewelryTotalDc * 3) - (weapon.UpgradeLevel * 5);
        successRate = Math.Clamp(successRate, 35, 85);

        // 黑铁矿石对持久影响：总纯度 >= 60 则持久上限 +1；总纯度 < 30 则持久上限 -1；其余不变
        int purityEffect = 0;
        if (orePurityTotal >= 60) purityEffect = 1;
        else if (orePurityTotal < 30) purityEffect = -1;

        weapon.InUpgradeTrial = true;
        weapon.TrialSuccessRate = successRate;
        weapon.TrialBonusDc = 1;
        weapon.TrialPurityEffect = purityEffect;

        msg = $"【淬炼就绪】神匠已将玄铁灵气注入【{weapon.Name}】！当前预估开刃成功率: {successRate}%！\n请速速持此武器挥刃斩妖进行【开刃试刀】判定！";
        return true;
    }

    /// <summary>
    /// 挥刀斩怪/攻击时触发开刃试刀判定
    /// </summary>
    public bool TryTrialBlade(Item weapon, out bool isSuccess, out string eventMsg)
    {
        isSuccess = false;
        eventMsg = "";

        if (weapon.Type != ItemType.Weapon || !weapon.InUpgradeTrial) return false;

        weapon.InUpgradeTrial = false; // 消费试刀状态
        int roll = Random.Shared.Next(100);

        if (roll < weapon.TrialSuccessRate)
        {
            // 试刀成功！
            isSuccess = true;
            weapon.UpgradeLevel++;
            int bonusDc = weapon.TrialBonusDc;

            // 5% 概率触发人品爆发“跳点 +2”
            if (roll < 5 && weapon.UpgradeLevel < MAX_UPGRADE_LEVEL)
            {
                bonusDc = 2;
                weapon.UpgradeLevel++;
            }

            weapon.MaxDc += bonusDc;

            // 黑铁矿石持久加成生效
            if (weapon.TrialPurityEffect != 0)
            {
                weapon.MaxDura = Math.Max(1, weapon.MaxDura + weapon.TrialPurityEffect);
                weapon.Dura = Math.Min(weapon.Dura, weapon.MaxDura);
            }

            eventMsg = bonusDc > 1
                ? $"★★★【天道顿悟·极品跳点】伴随一声震天龙吟，【{weapon.Name}】开刃大圆满！攻击上限飙升 +{bonusDc} (当前觉醒: +{weapon.UpgradeLevel}，攻击: {weapon.MinDc}-{weapon.MaxDc})！ ★★★"
                : $"★★【神兵开刃成功】灵光四溢！【{weapon.Name}】淬火开刃大功告成！攻击上限 +{bonusDc} (当前觉醒: +{weapon.UpgradeLevel}，攻击: {weapon.MinDc}-{weapon.MaxDc})！ ★★";
        }
        else
        {
            // 试刀失败：经典碎刀或属性未增
            isSuccess = false;
            // 50% 碎刀（当前持久置 0 破损），50% 虽未破损但升级失败
            bool isShattered = Random.Shared.Next(100) < 50;
            if (isShattered)
            {
                weapon.Dura = 0;
                eventMsg = $"【试刀碎刃】轰然一声悲鸣！【{weapon.Name}】未能承受玄铁狂暴灵力，剑身碎裂破损！需立即特修！";
            }
            else
            {
                eventMsg = $"【试刀未遂】剑气微弱，【{weapon.Name}】开刃未能觉醒神力，属性未发生改变！";
            }
        }

        return true;
    }
}
