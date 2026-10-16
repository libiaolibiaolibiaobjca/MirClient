using System;
using System.Collections.Generic;
using System.Numerics;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 服务端下发的地面物品生成事件 (SM_ITEMSHOW 610)
/// </summary>
public record GroundItemShowEvent(
    long Id,
    int X,
    int Y,
    int Looks,
    string Name)
{
    public int TileX => X;
    public int TileY => Y;
    public long GroundId => Id;
    public string ItemName => Name;
}

/// <summary>
/// 服务端下发的地面物品消失事件 (SM_ITEMHIDE 611)
/// </summary>
public record GroundItemHideEvent(
    long Id,
    int X,
    int Y)
{
    public int TileX => X;
    public int TileY => Y;
    public long GroundId => Id;
}

/// <summary>
/// OpenMir2 标准地面物品网络封包序列化与实体同步管理器
/// </summary>
public class GroundItemProtocolManager
{
    public static GroundItemProtocolManager Instance { get; } = new();

    private readonly Dictionary<long, GroundItemShowEvent> _items = new();
    private readonly Dictionary<(int X, int Y), long> _lastPickupTimeByCoord = new();

    public IReadOnlyDictionary<long, GroundItemShowEvent> Items => _items;

    public void Clear()
    {
        _items.Clear();
        _lastPickupTimeByCoord.Clear();
    }

    /// <summary>
    /// 构建客户端请求拾取地面物品的封包 (CM_PICKUP 1001)
    /// </summary>
    public static (DefaultMessage Msg, string Payload, string EncodedPacket) CreatePickupPacket(int tileX, int tileY)
    {
        var msg = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_PICKUP,
            Recog = 0,
            Param = (ushort)tileX,
            Tag = (ushort)tileY,
            Series = 0
        };

        string encodedHeader = EDCode.EncodeMessage(msg);
        string packet = $"#{encodedHeader}!";
        return (msg, string.Empty, packet);
    }

    /// <summary>
    /// 构建客户端请求丢弃物品的封包 (CM_DROPITEM 1000)
    /// </summary>
    public static (DefaultMessage Msg, string Payload, string EncodedPacket) CreateDropItemPacket(int itemMakeIndex, int dropCount, string itemName)
    {
        var msg = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_DROPITEM,
            Recog = itemMakeIndex,
            Param = (ushort)dropCount,
            Tag = 0,
            Series = 0
        };

        string encodedHeader = EDCode.EncodeMessage(msg);
        string encodedBody = EDCode.EncodeString(itemName ?? "");
        string packet = $"#{encodedHeader}{encodedBody}!";
        return (msg, encodedBody, packet);
    }

    /// <summary>
    /// 解析服务端下发的地面物品展示封包 (SM_ITEMSHOW 610)
    /// </summary>
    public static GroundItemShowEvent ParseItemShow(DefaultMessage header, string rawBody)
    {
        long id = header.Recog;
        int x = header.Param;
        int y = header.Tag;
        int looks = header.Series;
        string itemName = EDCode.DecodeText(rawBody);

        return new GroundItemShowEvent(id, x, y, looks, itemName);
    }

    /// <summary>
    /// 解析服务端下发的地面物品移除封包 (SM_ITEMHIDE 611)
    /// </summary>
    public static GroundItemHideEvent ParseItemHide(DefaultMessage header)
    {
        long id = header.Recog;
        int x = header.Param;
        int y = header.Tag;

        return new GroundItemHideEvent(id, x, y);
    }

    /// <summary>
    /// 将地面物品记录同步至管理器缓存
    /// </summary>
    public void SyncShowItem(GroundItemShowEvent evt)
    {
        _items[evt.Id] = evt;
    }

    /// <summary>
    /// 将服务端网络事件应用并同步至本地 GroundItemManager
    /// </summary>
    public void SyncShowItem(GroundItemManager manager, GroundItemShowEvent evt)
    {
        _items[evt.Id] = evt;
        if (manager == null) return;

        // 查重：避免重复添加相同 ID 的地面物品
        foreach (var exist in manager.Items)
        {
            if (exist.Id == evt.Id)
            {
                return;
            }
        }

        Vector2 tilePos = new(evt.X, evt.Y);

        if (evt.Name.Contains("金币") || evt.Name.Equals("金币", StringComparison.OrdinalIgnoreCase))
        {
            int gold = 100;
            manager.SpawnGold(tilePos, gold);
        }
        else
        {
            Item item = Item.CreateByName(evt.Name);
            manager.SpawnItem(tilePos, item, evt.Id);
        }
    }

    /// <summary>
    /// 从缓存中移除已被他人拾取或超时的地面物品
    /// </summary>
    public bool SyncHideItem(GroundItemHideEvent evt)
    {
        return _items.Remove(evt.Id);
    }

    /// <summary>
    /// 从本地 GroundItemManager 移除已被他人拾取或超时的地面物品
    /// </summary>
    public bool SyncHideItem(GroundItemManager manager, GroundItemHideEvent evt)
    {
        _items.Remove(evt.Id);
        if (manager == null) return false;
        return manager.RemoveById(evt.Id);
    }

    /// <summary>
    /// 拾取冷却与防抖判定 (默认 350ms 冷却时间)
    /// </summary>
    public bool CanPickup(int tileX, int tileY, long nowMs, int cooldownMs = 350)
    {
        var key = (tileX, tileY);
        if (_lastPickupTimeByCoord.TryGetValue(key, out long lastTime))
        {
            if (nowMs - lastTime < cooldownMs)
            {
                return false;
            }
        }

        _lastPickupTimeByCoord[key] = nowMs;
        return true;
    }
}
