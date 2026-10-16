using System;
using System.Collections.Generic;
using MirClient.Protocol;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class GuildProtocolTests
{
    [Fact]
    public void Test_CreateOpenGuildDlgAndMemberListPackets()
    {
        var (openHeader, openPayload) = GuildProtocolManager.CreateOpenGuildDlgPacket();
        Assert.Equal(ProtocolConsts.CM_OPENGUILDDLG, openHeader.Ident);
        Assert.Empty(openPayload);

        var (listHeader, listPayload) = GuildProtocolManager.CreateGuildMemberListPacket();
        Assert.Equal(ProtocolConsts.CM_GUILDMEMBERLIST, listHeader.Ident);
        Assert.Empty(listPayload);
    }

    [Fact]
    public void Test_CreateGuildAddMemberPacket_EncodesTargetName()
    {
        string memberName = "昔日法神";
        var (header, payload) = GuildProtocolManager.CreateGuildAddMemberPacket(memberName);

        Assert.Equal(ProtocolConsts.CM_GUILDADDMEMBER, header.Ident);
        string decoded = EDCode.DecodeText(payload);
        Assert.Equal("昔日法神", decoded);
    }

    [Fact]
    public void Test_CreateGuildDelMemberPacket_EncodesTargetName()
    {
        string memberName = "叛逃小卒";
        var (header, payload) = GuildProtocolManager.CreateGuildDelMemberPacket(memberName);

        Assert.Equal(ProtocolConsts.CM_GUILDDELMEMBER, header.Ident);
        string decoded = EDCode.DecodeText(payload);
        Assert.Equal("叛逃小卒", decoded);
    }

    [Fact]
    public void Test_CreateGuildUpdateNoticePacket_EncodesNoticeLines()
    {
        var noticeLines = new List<string> { "今晚八点攻占沙巴克！", "全体行会成员药水自备！" };
        var (header, payload) = GuildProtocolManager.CreateGuildUpdateNoticePacket(noticeLines);

        Assert.Equal(ProtocolConsts.CM_GUILDUPDATENOTICE, header.Ident);
        string decoded = EDCode.DecodeText(payload);
        Assert.Equal("今晚八点攻占沙巴克！\r全体行会成员药水自备！", decoded);
    }

    [Fact]
    public void Test_ParseGuildDlg_ExtractsMasterAndNotice()
    {
        string rawData = EDCode.EncodeString("傲视天下\r \r1\r沙城争霸，号令天下！");
        var (guildName, isMaster, notice) = GuildProtocolManager.ParseGuildDlg(rawData);

        Assert.Equal("傲视天下", guildName);
        Assert.True(isMaster);
        Assert.Equal("沙城争霸，号令天下！", notice);
    }

    [Fact]
    public void Test_ParseGuildMembers_ParsesRankHierarchyAndNames()
    {
        // 格式: #RankNo/*RankName/Member1/Member2/.../
        string rawData = EDCode.EncodeString("#0/*掌门人/小虾米/昔日法神/#1/*白虎堂主/烈火刀客/");
        var ranks = GuildProtocolManager.ParseGuildMembers(rawData);

        Assert.Equal(2, ranks.Count);
        Assert.Equal(0, ranks[0].RankNo);
        Assert.Equal("掌门人", ranks[0].RankName);
        Assert.Equal(2, ranks[0].Members.Count);
        Assert.Contains("小虾米", ranks[0].Members);
        Assert.Contains("昔日法神", ranks[0].Members);

        Assert.Equal(1, ranks[1].RankNo);
        Assert.Equal("白虎堂主", ranks[1].RankName);
        Assert.Single(ranks[1].Members);
        Assert.Contains("烈火刀客", ranks[1].Members);
    }

    [Fact]
    public void Test_GetGuildFailReason_TranslatesErrorCodes()
    {
        Assert.Contains("掌门人", GuildProtocolManager.GetGuildFailReason(1));
        Assert.Contains("面对面", GuildProtocolManager.GetGuildFailReason(2));
        Assert.Contains("已经加入", GuildProtocolManager.GetGuildFailReason(3));
        Assert.Contains("其他行会", GuildProtocolManager.GetGuildFailReason(4));
        Assert.Contains("加入行会", GuildProtocolManager.GetGuildFailReason(5));
        Assert.Contains("失败", GuildProtocolManager.GetGuildFailReason(99));
    }

    [Fact]
    public void Test_SyncServerRanks_UpdatesGuildManager()
    {
        GuildManager.Instance.CreateGuild("沙巴克第一行会", "测试掌门", CharacterJob.Warrior, 50);

        var serverRanks = new List<GuildRankEntry>
        {
            new GuildRankEntry(0, "总舵主", new List<string> { "测试掌门" }),
            new GuildRankEntry(1, "副舵主", new List<string> { "副帮主" }),
            new GuildRankEntry(2, "青龙堂众", new List<string> { "先锋一号", "先锋二号" })
        };

        GuildManager.Instance.SyncServerRanks(serverRanks);

        Assert.Equal(4, GuildManager.Instance.Members.Count);
        Assert.True(GuildManager.Instance.IsMember("测试掌门"));
        Assert.True(GuildManager.Instance.IsMember("副帮主"));
        Assert.True(GuildManager.Instance.IsMember("先锋一号"));
        Assert.True(GuildManager.Instance.IsMember("先锋二号"));
        Assert.Equal(GuildRank.Leader, GuildManager.Instance.GetMemberRank("测试掌门"));
        Assert.Equal(GuildRank.ViceLeader, GuildManager.Instance.GetMemberRank("副帮主"));
        Assert.Equal(GuildRank.Elder, GuildManager.Instance.GetMemberRank("先锋一号"));
    }
}
