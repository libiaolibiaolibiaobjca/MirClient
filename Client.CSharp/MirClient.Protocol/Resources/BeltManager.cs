namespace MirClient.Protocol.Resources;

/// <summary>
/// 快捷腰带槽位定义
/// </summary>
public record BeltSlot(int Index, string PotionName);

/// <summary>
/// 盛大热血传奇底栏 1~6 快捷腰带与智能生命保护状态机 (纯 C# 领域状态机)
/// </summary>
public class BeltManager
{
    private static BeltManager? _instance;
    public static BeltManager Instance => _instance ??= new BeltManager();

    public const int SLOT_COUNT = 6;
    private readonly BeltSlot[] _slots = new BeltSlot[SLOT_COUNT];

    // 智能生命保护 (内挂守护) 配置
    public bool IsAutoProtectEnabled { get; set; } = true;
    public float ProtectHpPercent { get; set; } = 0.35f; // 默认低于 35% 自动救急
    public double ProtectCooldownSec { get; set; } = 1.0; // 自动喝药 1 秒 CD 防抖
    private double _lastProtectTime = 0.0;

    public BeltManager()
    {
        ResetToDefaults();
    }

    public void ResetToDefaults()
    {
        // 经典传奇 6 格腰带默认推荐配置 (快捷键 1~6)
        _slots[0] = new BeltSlot(0, "强效太阳水");
        _slots[1] = new BeltSlot(1, "金创药(小包)");
        _slots[2] = new BeltSlot(2, "魔法药(小包)");
        _slots[3] = new BeltSlot(3, "强效太阳水");
        _slots[4] = new BeltSlot(4, "金创药(小包)");
        _slots[5] = new BeltSlot(5, "魔法药(小包)");
    }

    public void AssignSlot(int slotIndex, string potionName)
    {
        if (slotIndex < 0 || slotIndex >= SLOT_COUNT) return;
        _slots[slotIndex] = new BeltSlot(slotIndex, potionName);
    }

    public string GetSlotPotion(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SLOT_COUNT) return "";
        return _slots[slotIndex]?.PotionName ?? "";
    }

    /// <summary>
    /// 计算指定槽位绑定的药品在背包中的累计总数
    /// </summary>
    public int GetTotalPotionCount(int slotIndex, InventoryManager inv)
    {
        if (slotIndex < 0 || slotIndex >= SLOT_COUNT || inv == null) return 0;
        string target = _slots[slotIndex].PotionName;
        if (string.IsNullOrWhiteSpace(target)) return 0;

        int total = 0;
        for (int i = 0; i < InventoryManager.BAG_CAPACITY; i++)
        {
            var item = inv.BagItems[i];
            if (item != null && item.Type == ItemType.Potion && IsPotionMatch(item.Name, target))
            {
                total += item.Count;
            }
        }
        return total;
    }

    /// <summary>
    /// 快捷使用指定槽位的药品
    /// </summary>
    public bool UseSlot(int slotIndex, InventoryManager inv, out int hpRestore, out int mpRestore, out string usedName)
    {
        hpRestore = 0;
        mpRestore = 0;
        usedName = "";

        if (slotIndex < 0 || slotIndex >= SLOT_COUNT || inv == null) return false;
        string target = _slots[slotIndex].PotionName;
        if (string.IsNullOrWhiteSpace(target)) return false;

        // 在包裹中按序查找首个匹配药水
        for (int i = 0; i < InventoryManager.BAG_CAPACITY; i++)
        {
            var item = inv.BagItems[i];
            if (item != null && item.Type == ItemType.Potion && IsPotionMatch(item.Name, target))
            {
                usedName = item.Name;
                return inv.UsePotion(i, out hpRestore, out mpRestore);
            }
        }

        return false;
    }

    /// <summary>
    /// 智能生命保护判定：当主角生命值危急时，自动从包裹寻找高优先级救急药品秒喝
    /// </summary>
    public bool CheckAutoProtect(
        float currentHp, 
        float maxHp, 
        InventoryManager inv, 
        double currentTime, 
        out int hpRestore, 
        out int mpRestore, 
        out string usedName)
    {
        hpRestore = 0;
        mpRestore = 0;
        usedName = "";

        if (!IsAutoProtectEnabled || maxHp <= 0 || inv == null) return false;

        float hpRate = currentHp / maxHp;
        if (hpRate > ProtectHpPercent || currentHp <= 0) return false;

        if (currentTime - _lastProtectTime < ProtectCooldownSec) return false;

        // 救急优先级: 强效太阳水 (双回) -> 金创药 (红药)
        string[] priorityPotions = { "太阳水", "金创药" };

        foreach (var keyword in priorityPotions)
        {
            for (int i = 0; i < InventoryManager.BAG_CAPACITY; i++)
            {
                var item = inv.BagItems[i];
                if (item != null && item.Type == ItemType.Potion && item.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    usedName = item.Name;
                    if (inv.UsePotion(i, out hpRestore, out mpRestore))
                    {
                        _lastProtectTime = currentTime;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool IsPotionMatch(string itemName, string targetName)
    {
        if (string.Equals(itemName, targetName, StringComparison.OrdinalIgnoreCase))
            return true;

        // 容错模糊匹配 (例如 "金创药" 匹配 "金创药(小包)")
        if (itemName.Contains(targetName, StringComparison.OrdinalIgnoreCase) ||
            targetName.Contains(itemName, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
