using System;
using System.Collections.Generic;
using MirClient.Protocol;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class SocialProtocolTests
{
    [Fact]
    public void Test_CreateAddFriendPacket_EncodesTargetName()
    {
        string friendName = "热血无名侠";
        var (header, payload) = SocialProtocolManager.CreateAddFriendPacket(friendName);

        Assert.Equal(ProtocolConsts.CM_ADDFRIEND, header.Ident);
        Assert.Equal("热血无名侠", payload);
    }

    [Fact]
    public void Test_CreateDelFriendPacket_EncodesTargetName()
    {
        string friendName = "绝情剑客";
        var (header, payload) = SocialProtocolManager.CreateDelFriendPacket(friendName);

        Assert.Equal(ProtocolConsts.CM_DELFRIEND, header.Ident);
        Assert.Equal("绝情剑客", payload);
    }

    [Fact]
    public void Test_CreateQueryFriendListPacket_ValidIdent()
    {
        var (header, payload) = SocialProtocolManager.CreateQueryFriendListPacket();

        Assert.Equal(ProtocolConsts.CM_QUERYFRIENDLIST, header.Ident);
        Assert.Empty(payload);
    }

    [Fact]
    public void Test_CreateMasterRequestPacket_DistinguishesApprenticeAndMaster()
    {
        // 拜对方为师
        var (appHeader, appPayload) = SocialProtocolManager.CreateMasterRequestPacket(true, "无极尊者");
        Assert.Equal(ProtocolConsts.CM_MASTER_REQUEST, appHeader.Ident);
        Assert.Equal(1, appHeader.Recog);
        Assert.Equal("无极尊者", appPayload);

        // 收对方为徒
        var (mHeader, mPayload) = SocialProtocolManager.CreateMasterRequestPacket(false, "初入江湖");
        Assert.Equal(ProtocolConsts.CM_MASTER_REQUEST, mHeader.Ident);
        Assert.Equal(2, mHeader.Recog);
        Assert.Equal("初入江湖", mPayload);
    }

    [Fact]
    public void Test_CreateMasterResponsePacket_EncodesAcceptFlag()
    {
        var (okHeader, okPayload) = SocialProtocolManager.CreateMasterResponsePacket(true, "小道童");
        Assert.Equal(ProtocolConsts.CM_MASTER_RESPONSE, okHeader.Ident);
        Assert.Equal(1, okHeader.Recog);
        Assert.Equal("小道童", okPayload);

        var (failHeader, failPayload) = SocialProtocolManager.CreateMasterResponsePacket(false, "小道童");
        Assert.Equal(ProtocolConsts.CM_MASTER_RESPONSE, failHeader.Ident);
        Assert.Equal(0, failHeader.Recog);
        Assert.Equal("小道童", failPayload);
    }

    [Fact]
    public void Test_CreateMasterGraduatePacket_EncodesIsSelfFlag()
    {
        // 自身出师礼
        var (selfHeader, selfPayload) = SocialProtocolManager.CreateMasterGraduatePacket(true, "");
        Assert.Equal(ProtocolConsts.CM_MASTER_GRADUATE, selfHeader.Ident);
        Assert.Equal(2, selfHeader.Recog);

        // 为弟子主持出师
        var (appHeader, appPayload) = SocialProtocolManager.CreateMasterGraduatePacket(false, "得力弟子");
        Assert.Equal(ProtocolConsts.CM_MASTER_GRADUATE, appHeader.Ident);
        Assert.Equal(1, appHeader.Recog);
        Assert.Equal("得力弟子", appPayload);
    }

    [Fact]
    public void Test_ParseFriendRecordAndList_ParsesEntriesCorrectly()
    {
        string recordText = "战神归来/0/42/盟重省/1";
        var friend = SocialProtocolManager.ParseFriendRecord(recordText);
        Assert.NotNull(friend);
        Assert.Equal("战神归来", friend.Name);
        Assert.Equal(CharacterJob.Warrior, friend.Job);
        Assert.Equal(42, friend.Level);
        Assert.Equal("盟重省", friend.MapName);
        Assert.True(friend.IsOnline);

        string multiList = "战神归来/0/42/盟重省/1\n雷电法王/1/38/白日门/0\n太尊道士/2/40/沃玛森林/1";
        var list = SocialProtocolManager.ParseFriendList(multiList);
        Assert.Equal(3, list.Count);
        Assert.Equal("雷电法王", list[1].Name);
        Assert.Equal(CharacterJob.Wizard, list[1].Job);
        Assert.False(list[1].IsOnline);
        Assert.Equal("太尊道士", list[2].Name);
        Assert.Equal(CharacterJob.Taoist, list[2].Job);
    }

    [Fact]
    public void Test_ParseFriendOnOff_ParsesOnlineStateAndMap()
    {
        var msgOnline = new DefaultMessage
        {
            Ident = ProtocolConsts.SM_FRIEND_ONOFF,
            Recog = 1
        };
        var (name1, isOnline1, map1) = SocialProtocolManager.ParseFriendOnOff(msgOnline, "风云再起/封魔谷");
        Assert.Equal("风云再起", name1);
        Assert.True(isOnline1);
        Assert.Equal("封魔谷", map1);

        var msgOffline = new DefaultMessage
        {
            Ident = ProtocolConsts.SM_FRIEND_ONOFF,
            Recog = 0
        };
        var (name2, isOnline2, map2) = SocialProtocolManager.ParseFriendOnOff(msgOffline, "风云再起/");
        Assert.Equal("风云再起", name2);
        Assert.False(isOnline2);
    }

    [Fact]
    public void Test_ParseMasterList_ParsesMasterAndApprentices()
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.SM_MASTER_LIST,
            Recog = 280 // 声望值
        };
        string payload = "无崖子/1\n虚竹/2/36/1\n段誉/1/32/0";

        var record = SocialProtocolManager.ParseMasterList(header, payload);
        Assert.Equal("无崖子", record.MasterName);
        Assert.True(record.IsGraduated);
        Assert.Equal(280, record.Reputation);
        Assert.Equal(2, record.Apprentices.Count);

        var a1 = record.Apprentices[0];
        Assert.Equal("虚竹", a1.Name);
        Assert.Equal(CharacterJob.Taoist, a1.Job);
        Assert.Equal(36, a1.Level);
        Assert.True(a1.IsGraduated);

        var a2 = record.Apprentices[1];
        Assert.Equal("段誉", a2.Name);
        Assert.Equal(CharacterJob.Wizard, a2.Job);
        Assert.Equal(32, a2.Level);
        Assert.False(a2.IsGraduated);
    }

    [Fact]
    public void Test_ParseApprenticeLevelUp_ParsesLevelAndRepGain()
    {
        var header = new DefaultMessage
        {
            Ident = ProtocolConsts.SM_APPRENTICE_LEVELUP,
            Recog = 29, // 弟子新等级
            Param = 10  // 师父获得声望奖励
        };
        string payload = "勤奋学徒";

        var (name, newLvl, repGain) = SocialProtocolManager.ParseApprenticeLevelUp(header, payload);
        Assert.Equal("勤奋学徒", name);
        Assert.Equal(29, newLvl);
        Assert.Equal(10, repGain);
    }

    [Fact]
    public void Test_SocialManager_SyncServerIntegrations()
    {
        var sm = SocialManager.Instance;
        sm.Reset();

        // 1. 同步好友全量列表
        var friends = new List<FriendInfo>
        {
            new("好友A", CharacterJob.Warrior, 30, "比奇城", true),
            new("好友B", CharacterJob.Taoist, 35, "盟重省", false)
        };
        sm.SyncServerFriendList(friends);
        Assert.Equal(2, sm.Friends.Count);

        // 2. 好友上线状态通知
        sm.SyncServerFriendOnOff("好友B", true, "苍月岛");
        var friendB = sm.GetFriend("好友B");
        Assert.NotNull(friendB);
        Assert.True(friendB.IsOnline);
        Assert.Equal("苍月岛", friendB.MapName);

        // 3. 师门名册与弟子同步
        var apps = new List<ApprenticeInfo>
        {
            new("弟子小张", CharacterJob.Warrior, 22, false)
        };
        sm.SyncServerMasterList("玄天道尊", false, 50, apps);
        Assert.Equal("玄天道尊", sm.MasterName);
        Assert.False(sm.IsGraduated);
        Assert.Equal(50, sm.Reputation);
        Assert.Single(sm.Apprentices);

        // 4. 弟子升级同步与师门声望加算
        sm.SyncServerApprenticeLevelUp("弟子小张", 25, 6);
        Assert.Equal(25, sm.Apprentices[0].Level);
        Assert.Equal(56, sm.Reputation);
    }
}
