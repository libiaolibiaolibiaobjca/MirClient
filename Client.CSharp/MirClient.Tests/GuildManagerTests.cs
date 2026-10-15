using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class GuildManagerTests
{
    [Fact]
    public void TestGuildManager_DefaultState()
    {
        GuildManager gm = new();
        Assert.False(gm.HasGuild);
        Assert.Equal("", gm.GuildName);
        Assert.Empty(gm.Members);
        Assert.Null(gm.Leader);
        Assert.Equal("", gm.GetTitleForPlayer("逐鹿天下"));
    }

    [Fact]
    public void TestGuildManager_CreateGuild()
    {
        GuildManager gm = new();
        bool created = gm.CreateGuild("傲视天下", "逐鹿天下", CharacterJob.Warrior, 35);
        Assert.True(created);
        Assert.True(gm.HasGuild);
        Assert.Equal("傲视天下", gm.GuildName);
        Assert.Single(gm.Members);
        Assert.NotNull(gm.Leader);
        Assert.Equal("逐鹿天下", gm.Leader.Name);
        Assert.Equal(GuildRank.Leader, gm.Leader.Rank);

        // 验证头顶称号格式
        Assert.Equal("【傲视天下】会长", gm.GetTitleForPlayer("逐鹿天下"));
        Assert.Equal("", gm.GetTitleForPlayer("非行会成员"));
    }

    [Fact]
    public void TestGuildManager_AddMembersAndRanks()
    {
        GuildManager gm = new();
        gm.CreateGuild("傲视天下", "逐鹿天下", CharacterJob.Warrior, 35);

        // 招募副会长
        bool ok1 = gm.AddMember("烈火法神", CharacterJob.Wizard, 38, GuildRank.ViceLeader);
        Assert.True(ok1);
        Assert.Equal("【傲视天下】副会长", gm.GetTitleForPlayer("烈火法神"));

        // 招募堂主
        bool ok2 = gm.AddMember("无极道圣", CharacterJob.Taoist, 36, GuildRank.Elder);
        Assert.True(ok2);
        Assert.Equal("【傲视天下】堂主", gm.GetTitleForPlayer("无极道圣"));

        // 招募精英与帮众
        bool ok3 = gm.AddMember("霸天狂战", CharacterJob.Warrior, 30, GuildRank.Elite);
        bool ok4 = gm.AddMember("初涉江湖", CharacterJob.Warrior, 15, GuildRank.Member);
        Assert.True(ok3);
        Assert.True(ok4);
        Assert.Equal("【傲视天下】精英", gm.GetTitleForPlayer("霸天狂战"));
        Assert.Equal("【傲视天下】帮众", gm.GetTitleForPlayer("初涉江湖"));

        Assert.Equal(5, gm.Members.Count);

        // 重复添加同名成员阻断
        bool duplicate = gm.AddMember("烈火法神", CharacterJob.Wizard, 38);
        Assert.False(duplicate);
    }

    [Fact]
    public void TestGuildManager_RankPromotionAndLeaderTransfer()
    {
        GuildManager gm = new();
        gm.CreateGuild("傲视天下", "逐鹿天下", CharacterJob.Warrior, 35);
        gm.AddMember("烈火法神", CharacterJob.Wizard, 38, GuildRank.ViceLeader);

        // 将烈火法神任命为新会长
        bool transferred = gm.SetMemberRank("烈火法神", GuildRank.Leader);
        Assert.True(transferred);
        Assert.Equal("烈火法神", gm.Leader?.Name);
        Assert.Equal("【傲视天下】会长", gm.GetTitleForPlayer("烈火法神"));

        // 原会长降为副会长
        Assert.Equal("【傲视天下】副会长", gm.GetTitleForPlayer("逐鹿天下"));
    }

    [Fact]
    public void TestGuildManager_RemoveMemberAndDisband()
    {
        GuildManager gm = new();
        gm.CreateGuild("傲视天下", "逐鹿天下", CharacterJob.Warrior, 35);
        gm.AddMember("叛帮盗贼", CharacterJob.Warrior, 20, GuildRank.Member);
        Assert.Equal(2, gm.Members.Count);

        // 逐出帮众
        bool kicked = gm.RemoveMember("叛帮盗贼");
        Assert.True(kicked);
        Assert.Single(gm.Members);
        Assert.Equal("", gm.GetTitleForPlayer("叛帮盗贼"));

        // 会长退出，触发自动解散
        bool leaderKicked = gm.RemoveMember("逐鹿天下");
        Assert.True(leaderKicked);
        Assert.False(gm.HasGuild);
        Assert.Empty(gm.Members);
    }

    [Fact]
    public void TestCommandParser_GuildCommands()
    {
        var cmdCreate = CommandParser.Parse("@guild create 兄弟同心");
        Assert.Equal(ParsedCommandType.Guild, cmdCreate.Type);
        Assert.Equal("create", cmdCreate.SubAction);
        Assert.Equal("兄弟同心", cmdCreate.TargetName);

        var cmdAdd = CommandParser.Parse("@guild add 霸天虎 40 战士 堂主");
        Assert.Equal(ParsedCommandType.Guild, cmdAdd.Type);
        Assert.Equal("add", cmdAdd.SubAction);
        Assert.Equal("霸天虎", cmdAdd.TargetName);
        Assert.Equal(40, cmdAdd.Amount);
        Assert.Equal(CharacterJob.Warrior, cmdAdd.TargetJob);
        Assert.Equal(GuildRank.Elder, cmdAdd.TargetRank);

        var cmdRank = CommandParser.Parse("@guild rank 精英");
        Assert.Equal(ParsedCommandType.Guild, cmdRank.Type);
        Assert.Equal("rank", cmdRank.SubAction);
        Assert.Equal(GuildRank.Elite, cmdRank.TargetRank);

        var cmdKick = CommandParser.Parse("@guild kick 霸天虎");
        Assert.Equal(ParsedCommandType.Guild, cmdKick.Type);
        Assert.Equal("kick", cmdKick.SubAction);
        Assert.Equal("霸天虎", cmdKick.TargetName);

        var cmdLeave = CommandParser.Parse("@guild leave");
        Assert.Equal(ParsedCommandType.Guild, cmdLeave.Type);
        Assert.Equal("leave", cmdLeave.SubAction);
    }
}
