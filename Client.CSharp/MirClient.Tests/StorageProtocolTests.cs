using System;
using System.Collections.Generic;
using MirClient.Protocol;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class StorageProtocolTests
{
    [Fact]
    public void Test_CreateSaveStorageItemPacket_EncodesMerchantAndMakeIndex()
    {
        int merchantId = 1002;
        int makeIndex = 0x12345678;
        string itemName = "裁决之杖";

        var (header, payload) = StorageProtocolManager.CreateSaveStorageItemPacket(merchantId, makeIndex, itemName);

        Assert.Equal(ProtocolConsts.CM_USERSTORAGEITEM, header.Ident);
        Assert.Equal(merchantId, header.Recog);
        int reconstructedMakeIndex = header.Param | (header.Tag << 16);
        Assert.Equal(makeIndex, reconstructedMakeIndex);
        Assert.Equal("裁决之杖", EDCode.DecodeText(payload));
    }

    [Fact]
    public void Test_CreateTakeBackStorageItemPacket_EncodesMerchantAndMakeIndex()
    {
        int merchantId = 2005;
        int makeIndex = 0x000ABCDE;
        string itemName = "骨玉权杖";

        var (header, payload) = StorageProtocolManager.CreateTakeBackStorageItemPacket(merchantId, makeIndex, itemName);

        Assert.Equal(ProtocolConsts.CM_USERTAKEBACKSTORAGEITEM, header.Ident);
        Assert.Equal(merchantId, header.Recog);
        int reconstructedMakeIndex = header.Param | (header.Tag << 16);
        Assert.Equal(makeIndex, reconstructedMakeIndex);
        Assert.Equal("骨玉权杖", EDCode.DecodeText(payload));
    }

    [Fact]
    public void Test_ParseStorageItemList_ParsesStructuredTokens()
    {
        string raw = "裁决之杖/88888/3000/3000/骨玉权杖/99999/2800/2800/";
        var records = StorageProtocolManager.ParseStorageItemList(raw);

        Assert.Equal(2, records.Count);

        Assert.Equal("裁决之杖", records[0].Name);
        Assert.Equal(88888, records[0].MakeIndex);
        Assert.Equal(3000, records[0].Dura);
        Assert.Equal(3000, records[0].DuraMax);

        Assert.Equal("骨玉权杖", records[1].Name);
        Assert.Equal(99999, records[1].MakeIndex);
        Assert.Equal(2800, records[1].Dura);
        Assert.Equal(2800, records[1].DuraMax);
    }

    [Fact]
    public void Test_ParseStorageItemList_ParsesSimpleNameTokens()
    {
        string raw = "强效太阳水/祝福油/金创药/";
        var records = StorageProtocolManager.ParseStorageItemList(raw);

        Assert.Equal(3, records.Count);
        Assert.Equal("强效太阳水", records[0].Name);
        Assert.Equal("祝福油", records[1].Name);
        Assert.Equal("金创药", records[2].Name);
    }

    [Fact]
    public void Test_GetStorageFailReason_TranslatesStatusCodes()
    {
        Assert.Contains("个人仓库已经满了", StorageProtocolManager.GetStorageFailReason(ProtocolConsts.SM_STORAGE_FULL));
        Assert.Contains("无法保管该物品", StorageProtocolManager.GetStorageFailReason(ProtocolConsts.SM_STORAGE_FAIL));
        Assert.Contains("包裹已满或负重超限", StorageProtocolManager.GetStorageFailReason(ProtocolConsts.SM_TAKEBACKSTORAGEITEM_FULLBAG));
        Assert.Contains("取回物品失败", StorageProtocolManager.GetStorageFailReason(ProtocolConsts.SM_TAKEBACKSTORAGEITEM_FAIL));
        Assert.Contains("仓库操作失败", StorageProtocolManager.GetStorageFailReason(999));
    }

    [Fact]
    public void Test_StorageManager_SaveAndTakeBackItem()
    {
        StorageManager.Instance.Reset();

        var item = new Item
        {
            Id = 5001,
            Name = "龙纹剑",
            Dura = 2500,
            MaxDura = 2500,
            Type = ItemType.Weapon
        };

        bool saved = StorageManager.Instance.SaveItem(item);
        Assert.True(saved);
        Assert.Equal(1, StorageManager.Instance.Count);

        var taken = StorageManager.Instance.TakeBackItem(5001);
        Assert.NotNull(taken);
        Assert.Equal("龙纹剑", taken.Name);
        Assert.Equal(0, StorageManager.Instance.Count);
    }

    [Fact]
    public void Test_StorageManager_CapacityLimit()
    {
        StorageManager.Instance.Reset();

        for (int i = 0; i < StorageManager.MAX_CAPACITY; i++)
        {
            var it = new Item { Id = 1000 + i, Name = $"测试物品{i}" };
            Assert.True(StorageManager.Instance.SaveItem(it));
        }

        Assert.True(StorageManager.Instance.IsFull);
        Assert.Equal(StorageManager.MAX_CAPACITY, StorageManager.Instance.Count);

        // 超出上限应拒绝存入
        var overflowItem = new Item { Id = 9999, Name = "溢出物品" };
        bool savedOverflow = StorageManager.Instance.SaveItem(overflowItem);
        Assert.False(savedOverflow);
        Assert.Equal(StorageManager.MAX_CAPACITY, StorageManager.Instance.Count);
    }

    [Fact]
    public void Test_StorageManager_SyncStorageRecords()
    {
        StorageManager.Instance.Reset();

        var records = new List<StorageItemRecord>
        {
            new StorageItemRecord(7001, "战神盔甲", 2000, 2000, 40),
            new StorageItemRecord(7002, "力量手镯", 1500, 1500, 3)
        };

        StorageManager.Instance.SyncStorageRecords(records);

        Assert.Equal(2, StorageManager.Instance.Count);
        var arm = StorageManager.Instance.Items[0];
        Assert.Equal("战神盔甲", arm.Name);
        Assert.Equal(ItemType.Dress, arm.Type);

        var bangle = StorageManager.Instance.Items[1];
        Assert.Equal("力量手镯", bangle.Name);
        Assert.Equal(ItemType.Armring, bangle.Type);
    }
}
