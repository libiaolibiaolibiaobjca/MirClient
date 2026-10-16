using System;
using System.Collections.Generic;
using MirClient.Protocol;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class GroupProtocolTests
{
    [Fact]
    public void Test_CreateGroupModePacket_AllowAndDisallow()
    {
        var (headerOn, _) = GroupProtocolManager.CreateGroupModePacket(true);
        Assert.Equal(ProtocolConsts.CM_GROUPMODE, headerOn.Ident);
        Assert.Equal(1, headerOn.Param);

        var (headerOff, _) = GroupProtocolManager.CreateGroupModePacket(false);
        Assert.Equal(ProtocolConsts.CM_GROUPMODE, headerOff.Ident);
        Assert.Equal(0, headerOff.Param);
    }

    [Fact]
    public void Test_CreateCreateGroupPacket_EncodesTargetName()
    {
        string target = "风云法王";
        var (header, payload) = GroupProtocolManager.CreateCreateGroupPacket(target);

        Assert.Equal(ProtocolConsts.CM_CREATEGROUP, header.Ident);
        string decoded = EDCode.DecodeText(payload);
        Assert.Equal("风云法王", decoded);
    }

    [Fact]
    public void Test_CreateAddMemberPacket_EncodesTargetName()
    {
        string target = "无量道人";
        var (header, payload) = GroupProtocolManager.CreateAddMemberPacket(target);

        Assert.Equal(ProtocolConsts.CM_ADDGROUPMEMBER, header.Ident);
        string decoded = EDCode.DecodeText(payload);
        Assert.Equal("无量道人", decoded);
    }

    [Fact]
    public void Test_CreateDelMemberPacket_EncodesTargetName()
    {
        string target = "逐鹿天下";
        var (header, payload) = GroupProtocolManager.CreateDelMemberPacket(target);

        Assert.Equal(ProtocolConsts.CM_DELGROUPMEMBER, header.Ident);
        string decoded = EDCode.DecodeText(payload);
        Assert.Equal("逐鹿天下", decoded);
    }

    [Fact]
    public void Test_ParseGroupMembers_SplitsDelimitedNamesCorrectly()
    {
        DefaultMessage header = new(
            ident: ProtocolConsts.SM_GROUPMEMBERS,
            recog: 0,
            param: 0,
            tag: 0,
            series: 0);

        string rawText = "小虾米/昔日法神/无极道尊/";
        string payload = EDCode.EncodeString(rawText);

        var members = GroupProtocolManager.ParseGroupMembers(header, payload);

        Assert.Equal(3, members.Count);
        Assert.Equal("小虾米", members[0]);
        Assert.Equal("昔日法神", members[1]);
        Assert.Equal("无极道尊", members[2]);
    }

    [Fact]
    public void Test_ParseGroupModeChanged_ExtractsBoolean()
    {
        DefaultMessage headerOn = new(
            ident: ProtocolConsts.SM_GROUPMODECHANGED,
            recog: 0,
            param: 1,
            tag: 0,
            series: 0);
        Assert.True(GroupProtocolManager.ParseGroupModeChanged(headerOn));

        DefaultMessage headerOff = new(
            ident: ProtocolConsts.SM_GROUPMODECHANGED,
            recog: 0,
            param: 0,
            tag: 0,
            series: 0);
        Assert.False(GroupProtocolManager.ParseGroupModeChanged(headerOff));
    }

    [Fact]
    public void Test_ParseGroupActionResult_TranslatesErrorCodes()
    {
        // 1. 成功测试
        DefaultMessage okHeader = new(
            ident: ProtocolConsts.SM_GROUPADDMEM_OK,
            recog: 0,
            param: 0,
            tag: 0,
            series: 0);
        var okResult = GroupProtocolManager.ParseGroupActionResult(okHeader, EDCode.EncodeString("新战友"), ProtocolConsts.SM_GROUPADDMEM_OK);
        Assert.True(okResult.Success);
        Assert.Contains("新战友", okResult.Message);

        // 2. 失败测试：未开启允许组队 (-4)
        DefaultMessage failHeader = new(
            ident: ProtocolConsts.SM_GROUPADDMEM_FAIL,
            recog: -4,
            param: 0,
            tag: 0,
            series: 0);
        var failResult = GroupProtocolManager.ParseGroupActionResult(failHeader, "", ProtocolConsts.SM_GROUPADDMEM_FAIL);
        Assert.False(failResult.Success);
        Assert.Equal(-4, failResult.ErrorCode);
        Assert.Equal("目标玩家未开启允许组队模式", failResult.Message);
    }

    [Fact]
    public void Test_SyncServerMembers_UpdatesGroupManagerMembers()
    {
        var gm = GroupManager.Instance;
        gm.LeaveGroup();

        List<string> serverMembers = new() { "战神阿修罗", "冰系魔导师", "天尊道袍" };
        gm.SyncServerMembers(serverMembers, "战神阿修罗");

        Assert.True(gm.HasGroup);
        Assert.Equal(3, gm.Members.Count);
        Assert.NotNull(gm.Leader);
        Assert.Equal("战神阿修罗", gm.Leader.Name);
        Assert.True(gm.Leader.IsLeader);
        Assert.False(gm.Members[1].IsLeader);
        Assert.Equal("冰系魔导师", gm.Members[1].Name);
    }
}
