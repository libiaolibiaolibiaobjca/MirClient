using System.Numerics;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 地表掉落物品实体
/// </summary>
public class GroundItem
{
    public long Id { get; set; }
    public Item? Item { get; set; }
    public int GoldAmount { get; set; }
    public Vector2 TilePos { get; set; }
    public float RemainSeconds { get; set; } = 120f; // 存活 120 秒

    public bool IsGold => GoldAmount > 0;
    public string DisplayName => IsGold ? $"金币 ({GoldAmount})" : (Item?.Name ?? "未知物品");

    public GroundItem(long id, Vector2 tilePos, int goldAmount)
    {
        Id = id;
        TilePos = tilePos;
        GoldAmount = goldAmount;
        Item = null;
    }

    public GroundItem(long id, Vector2 tilePos, Item item)
    {
        Id = id;
        TilePos = tilePos;
        GoldAmount = 0;
        Item = item;
    }
}

/// <summary>
/// 地表掉落与拾取管理系统
/// </summary>
public class GroundItemManager
{
    private long _nextId = 1;
    private readonly List<GroundItem> _items = [];

    public IReadOnlyList<GroundItem> Items => _items;

    public void Clear() => _items.Clear();

    /// <summary>
    /// 在指定瓦片生成金币掉落
    /// </summary>
    public GroundItem SpawnGold(Vector2 tilePos, int amount)
    {
        GroundItem ground = new(_nextId++, tilePos, amount);
        _items.Add(ground);
        return ground;
    }

    /// <summary>
    /// 在指定瓦片生成物品掉落
    /// </summary>
    public GroundItem SpawnItem(Vector2 tilePos, Item item)
    {
        GroundItem ground = new(_nextId++, tilePos, item);
        _items.Add(ground);
        return ground;
    }

    /// <summary>
    /// 怪物死亡掉落算法：根据怪物名字产生配置化的掉落物，并向周围有效瓦片散射（大爆/普通散落）
    /// </summary>
    public List<GroundItem> GenerateMonsterDrops(string monsterName, Vector2 centerTile, Func<int, int, bool> isBlocked)
    {
        List<GroundItem> dropped = [];
        List<(Item? item, int gold)> lootToSpawn = [];

        // 依据怪物类型生成经典掉落配比
        switch (monsterName)
        {
            case "稻草人":
                lootToSpawn.Add((null, Random.Shared.Next(15, 60))); // 金币
                if (Random.Shared.Next(100) < 60)
                    lootToSpawn.Add((Item.CreateRedPotion(1), 0));
                break;

            case "钉耙猫":
                lootToSpawn.Add((null, Random.Shared.Next(25, 90)));
                if (Random.Shared.Next(100) < 70)
                    lootToSpawn.Add((Item.CreateRedPotion(2), 0));
                if (Random.Shared.Next(100) < 40)
                    lootToSpawn.Add((Item.CreateBluePotion(1), 0));
                break;

            case "森林雪人":
                lootToSpawn.Add((null, Random.Shared.Next(50, 150)));
                if (Random.Shared.Next(100) < 80)
                    lootToSpawn.Add((Item.CreateRedPotion(3), 0));
                if (Random.Shared.Next(100) < 50)
                    lootToSpawn.Add((Item.CreateWoodSword(), 0));
                break;

            case "半兽人":
                lootToSpawn.Add((null, Random.Shared.Next(80, 220)));
                if (Random.Shared.Next(100) < 75)
                    lootToSpawn.Add((Item.CreateHeavyArmor(), 0));
                if (Random.Shared.Next(100) < 45)
                    lootToSpawn.Add((Item.CreateEbonySword(), 0));
                break;

            case "白野猪": // 首领怪经典大爆！
                // 爆多堆金币
                lootToSpawn.Add((null, Random.Shared.Next(300, 800)));
                lootToSpawn.Add((null, Random.Shared.Next(300, 800)));
                lootToSpawn.Add((null, Random.Shared.Next(300, 800)));
                // 爆多捆药水
                lootToSpawn.Add((Item.CreateRedPotion(5), 0));
                lootToSpawn.Add((Item.CreateBluePotion(5), 0));
                lootToSpawn.Add((Item.CreateRedPotion(5), 0));
                // 经典极品神装
                lootToSpawn.Add((Item.CreateJudgmentMace(), 0)); // 裁决之杖
                lootToSpawn.Add((Item.CreateGodArmor(), 0));     // 战神盔甲
                break;

            default:
                lootToSpawn.Add((null, Random.Shared.Next(10, 50)));
                break;
        }

        // 计算散落坐标：以中心瓦片为基准，按顺时针/螺旋九宫格向四周扩散
        int[] dx = [0, 1, 0, -1, 1, -1, 1, -1, 2, -2, 0, 0];
        int[] dy = [0, 0, 1, 0, 1, 1, -1, -1, 0, 0, 2, -2];
        int posIndex = 0;

        foreach (var (item, gold) in lootToSpawn)
        {
            Vector2 spawnPos = centerTile;
            // 寻找无阻挡瓦片
            for (int tryCount = 0; tryCount < dx.Length; tryCount++)
            {
                int curIdx = (posIndex + tryCount) % dx.Length;
                int tx = (int)centerTile.X + dx[curIdx];
                int ty = (int)centerTile.Y + dy[curIdx];
                if (!isBlocked(tx, ty))
                {
                    spawnPos = new Vector2(tx, ty);
                    posIndex = curIdx + 1;
                    break;
                }
            }

            if (item != null)
            {
                dropped.Add(SpawnItem(spawnPos, item));
            }
            else if (gold > 0)
            {
                dropped.Add(SpawnGold(spawnPos, gold));
            }
        }

        return dropped;
    }

    /// <summary>
    /// 推进时间生命周期，超时（RemainSeconds <= 0）自动清理
    /// </summary>
    public void Update(float dt)
    {
        for (int i = _items.Count - 1; i >= 0; i--)
        {
            _items[i].RemainSeconds -= dt;
            if (_items[i].RemainSeconds <= 0f)
            {
                _items.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 尝试在角色当前位置或周围拾取一件掉落物
    /// </summary>
    /// <param name="playerPos">玩家瓦片坐标</param>
    /// <param name="inventory">玩家背包</param>
    /// <param name="currentGold">当前金币引用</param>
    /// <param name="message">提示消息</param>
    /// <param name="maxPickupDist">最大允许拾取距离（默认1.25格）</param>
    /// <returns>是否成功拾取了至少一件物品</returns>
    public bool TryPickupNearest(Vector2 playerPos, InventoryManager inventory, ref int currentGold, out string message, float maxPickupDist = 1.25f)
    {
        message = "";
        if (_items.Count == 0)
        {
            return false;
        }

        // 寻找距离玩家最近的掉落物
        GroundItem? target = null;
        float minDist = float.MaxValue;

        foreach (var item in _items)
        {
            float dist = Vector2.Distance(playerPos, item.TilePos);
            if (dist <= maxPickupDist && dist < minDist)
            {
                minDist = dist;
                target = item;
            }
        }

        if (target == null)
        {
            return false;
        }

        // 拾取金币：直接入账，不占用背包空间
        if (target.IsGold)
        {
            currentGold += target.GoldAmount;
            message = $"拾得金币: {target.GoldAmount}";
            _items.Remove(target);
            return true;
        }

        // 拾取道具：检查背包是否有空位
        if (target.Item != null)
        {
            bool added = inventory.AddItem(target.Item);
            if (!added)
            {
                message = "包裹已满，无法拾取！";
                return false;
            }

            message = $"拾得物品: {target.Item.Name}";
            _items.Remove(target);
            return true;
        }

        return false;
    }
}
