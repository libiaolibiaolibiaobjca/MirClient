using System;
using System.Collections.Generic;
using System.Linq;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 客户端角色个人仓库管理中心 (单机沙盒存取与联机状态机)
/// </summary>
public class StorageManager
{
    public static StorageManager Instance { get; } = new();

    public const int MAX_CAPACITY = 50;

    private readonly List<Item> _items = new();

    public IReadOnlyList<Item> Items => _items;

    public bool IsOpen { get; set; } = false;

    public int CurrentMerchantId { get; set; } = 0;

    public int PageIndex { get; set; } = 0;

    public int TotalPages { get; set; } = 1;

    public bool IsLocked { get; set; } = false;

    public bool IsFull => _items.Count >= MAX_CAPACITY;

    public int Count => _items.Count;

    /// <summary>
    /// 本地存入物品至仓库
    /// </summary>
    public bool SaveItem(Item item)
    {
        if (item == null) return false;
        if (_items.Count >= MAX_CAPACITY) return false;

        _items.Add(item);
        return true;
    }

    /// <summary>
    /// 从仓库取回物品
    /// </summary>
    public Item? TakeBackItem(int makeIndex)
    {
        int index = _items.FindIndex(it => it.Id == makeIndex);
        if (index < 0) return null;

        Item item = _items[index];
        _items.RemoveAt(index);
        return item;
    }

    /// <summary>
    /// 通过名字从仓库取回物品
    /// </summary>
    public Item? TakeBackItemByName(string name)
    {
        int index = _items.FindIndex(it => it.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return null;

        Item item = _items[index];
        _items.RemoveAt(index);
        return item;
    }

    /// <summary>
    /// 从网络下发的记录同步仓库列表
    /// </summary>
    public void SyncStorageRecords(IEnumerable<StorageItemRecord> records)
    {
        if (records == null) return;

        _items.Clear();
        foreach (var r in records)
        {
            var item = new Item
            {
                Id = r.MakeIndex,
                Name = r.Name,
                Dura = r.Dura,
                MaxDura = r.DuraMax,
                Weight = r.Weight,
                Type = InferItemType(r.Name)
            };
            _items.Add(item);
        }
    }

    /// <summary>
    /// 模拟推导物品类型
    /// </summary>
    private static ItemType InferItemType(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return ItemType.Misc;

        if (name.Contains("剑") || name.Contains("刀") || name.Contains("杖") || name.Contains("斧") || name.Contains("枪"))
            return ItemType.Weapon;
        if (name.Contains("甲") || name.Contains("衣") || name.Contains("袍"))
            return ItemType.Dress;
        if (name.Contains("盔") || name.Contains("冠"))
            return ItemType.Helmet;
        if (name.Contains("链"))
            return ItemType.Necklace;
        if (name.Contains("镯"))
            return ItemType.Armring;
        if (name.Contains("戒"))
            return ItemType.Ring;
        if (name.Contains("药") || name.Contains("水") || name.Contains("卷"))
            return ItemType.Potion;
        if (name.Contains("勋章"))
            return ItemType.Badge;

        return ItemType.Misc;
    }

    /// <summary>
    /// 重置仓库状态
    /// </summary>
    public void Reset()
    {
        _items.Clear();
        IsOpen = false;
        CurrentMerchantId = 0;
        PageIndex = 0;
        TotalPages = 1;
        IsLocked = false;
    }
}
