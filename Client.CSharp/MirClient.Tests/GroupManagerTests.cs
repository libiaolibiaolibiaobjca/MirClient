using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class GroupManagerTests
{
    [Fact]
    public void TestGroupManager_DefaultState()
    {
        GroupManager gm = new();
        Assert.False(gm.HasGroup);
        Assert.Empty(gm.Members);
        Assert.True(gm.AllowGroup);
        Assert.Null(gm.Leader);
    }

    [Fact]
    public void TestGroupManager_CreateAndAddMembers()
    {
        GroupManager gm = new();
        // 创建队伍
        bool okLeader = gm.CreateGroup("逐鹿天下", CharacterJob.Warrior, 35, 710f, 710f, "比奇城");
        Assert.True(okLeader);
        Assert.True(gm.HasGroup);
        Assert.Single(gm.Members);
        Assert.NotNull(gm.Leader);
        Assert.Equal("逐鹿天下", gm.Leader.Name);
        Assert.True(gm.Leader.IsLeader);

        // 添加第 2 名队员 (法师)
        bool okMember2 = gm.AddMember("烈火法神", CharacterJob.Wizard, 38, 280f, 280f, "沃玛寺庙一层");
        Assert.True(okMember2);
        Assert.Equal(2, gm.Members.Count);

        // 添加第 3 名队员 (道士)
        bool okMember3 = gm.AddMember("无极道圣", CharacterJob.Taoist, 36, 420f, 420f, "天然洞穴一层");
        Assert.True(okMember3);
        Assert.Equal(3, gm.Members.Count);

        // 重复添加同名队员应被阻断
        bool duplicate = gm.AddMember("烈火法神", CharacterJob.Wizard, 38, 280f, 280f, "比奇城");
        Assert.False(duplicate);
        Assert.Equal(3, gm.Members.Count);
    }

    [Fact]
    public void TestGroupManager_RemoveAndAutoLeaderSuccession()
    {
        GroupManager gm = new();
        gm.CreateGroup("队长A", CharacterJob.Warrior, 40, 800f, 800f, "比奇城");
        gm.AddMember("队员B", CharacterJob.Wizard, 35, 250f, 250f, "比奇城");
        gm.AddMember("队员C", CharacterJob.Taoist, 36, 400f, 400f, "比奇城");

        Assert.Equal("队长A", gm.Leader?.Name);

        // 移交队长给 队员B
        bool transferred = gm.TransferLeader("队员B");
        Assert.True(transferred);
        Assert.Equal("队员B", gm.Leader?.Name);
        Assert.False(gm.Members[0].IsLeader); // 队长A已不再是队长

        // 踢出 队员B（当前队长），应自动顺延将第一位（队长A）重新晋升为队长
        bool kicked = gm.RemoveMember("队员B");
        Assert.True(kicked);
        Assert.Equal(2, gm.Members.Count);
        Assert.Equal("队长A", gm.Leader?.Name);

        // 退出解散队伍
        gm.LeaveGroup();
        Assert.False(gm.HasGroup);
        Assert.Empty(gm.Members);
    }

    [Fact]
    public void TestGroupManager_CalculateSharedExp()
    {
        GroupManager gm = new();

        // 1. 无队伍 / 单人队伍：无加成
        long pool1 = gm.CalculateSharedExp(1000, out long exp1);
        Assert.Equal(1000, pool1);
        Assert.Equal(1000, exp1);

        gm.CreateGroup("主角", CharacterJob.Warrior, 30, 500f, 500f, "比奇城");
        long poolSolo = gm.CalculateSharedExp(1000, out long expSolo);
        Assert.Equal(1000, poolSolo);
        Assert.Equal(1000, expSolo);

        // 2. 3 人组队：总经验池享受 120% 加成 (1000 * 1.2 = 1200)，3 人均分每人 400
        gm.AddMember("队友1", CharacterJob.Wizard, 30, 200f, 200f, "比奇城");
        gm.AddMember("队友2", CharacterJob.Taoist, 30, 300f, 300f, "比奇城");

        long pool3 = gm.CalculateSharedExp(1000, out long expPerMember);
        Assert.Equal(1200, pool3);
        Assert.Equal(400, expPerMember);
    }

    [Fact]
    public void TestCommandParser_GroupCommands()
    {
        var cmdAdd = CommandParser.Parse("@group add 烈火法神 38 法师");
        Assert.Equal(ParsedCommandType.Group, cmdAdd.Type);
        Assert.Equal("add", cmdAdd.SubAction);
        Assert.Equal("烈火法神", cmdAdd.TargetName);
        Assert.Equal(38, cmdAdd.Amount);
        Assert.Equal(CharacterJob.Wizard, cmdAdd.TargetJob);

        var cmdKick = CommandParser.Parse("@group kick 烈火法神");
        Assert.Equal(ParsedCommandType.Group, cmdKick.Type);
        Assert.Equal("kick", cmdKick.SubAction);
        Assert.Equal("烈火法神", cmdKick.TargetName);

        var cmdLeave = CommandParser.Parse("@group leave");
        Assert.Equal(ParsedCommandType.Group, cmdLeave.Type);
        Assert.Equal("leave", cmdLeave.SubAction);

        var cmdAllow = CommandParser.Parse("@group allow off");
        Assert.Equal(ParsedCommandType.Group, cmdAllow.Type);
        Assert.Equal("allow", cmdAllow.SubAction);
        Assert.False(cmdAllow.Enabled);
    }
}
