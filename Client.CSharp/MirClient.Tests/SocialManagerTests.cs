using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class SocialManagerTests
{
    public SocialManagerTests()
    {
        SocialManager.Instance.Reset();
    }

    [Fact]
    public void Test_Friend_AddAndRemove()
    {
        var social = SocialManager.Instance;
        
        // 1. 添加好友成功
        var addRes1 = social.AddFriend("龙魂战神", CharacterJob.Warrior, 42, "盟重省");
        Assert.True(addRes1.Success);
        Assert.Single(social.Friends);
        Assert.Equal("龙魂战神", social.Friends[0].Name);
        Assert.Equal("战士", social.Friends[0].JobName);
        Assert.Equal("盟重省", social.Friends[0].MapName);

        // 2. 重复添加好友判定
        var addRes2 = social.AddFriend("龙魂战神", CharacterJob.Warrior, 42);
        Assert.False(addRes2.Success);
        Assert.Contains("已在您的好友列表中", addRes2.Message);

        // 3. 移除好友
        var removeRes = social.RemoveFriend("龙魂战神");
        Assert.True(removeRes.Success);
        Assert.Empty(social.Friends);

        // 4. 移除不存在的好友
        var removeFail = social.RemoveFriend("无名氏");
        Assert.False(removeFail.Success);
    }

    [Fact]
    public void Test_Friend_OnlineOfflineState()
    {
        var social = SocialManager.Instance;
        social.AddFriend("冰雪法圣", CharacterJob.Wizard, 38, "比奇城", true);

        bool eventTriggered = false;
        bool lastOnlineState = false;
        social.OnFriendOnlineStateChanged += (name, online) =>
        {
            if (name == "冰雪法圣")
            {
                eventTriggered = true;
                lastOnlineState = online;
            }
        };

        // 切换为下线
        var offRes = social.SetFriendOnline("冰雪法圣", false);
        Assert.True(offRes.Success);
        Assert.False(social.GetFriend("冰雪法圣")!.IsOnline);
        Assert.True(eventTriggered);
        Assert.False(lastOnlineState);

        // 切换为上线并更新地图
        var onRes = social.SetFriendOnline("冰雪法圣", true, "封魔谷");
        Assert.True(onRes.Success);
        Assert.True(social.GetFriend("冰雪法圣")!.IsOnline);
        Assert.Equal("封魔谷", social.GetFriend("冰雪法圣")!.MapName);
        Assert.True(lastOnlineState);
    }

    [Fact]
    public void Test_Apprentice_BecomeApprentice_Conditions()
    {
        var social = SocialManager.Instance;

        // 1. 等级 > 28 级拜师失败
        var failHighLvl = social.BecomeApprentice("一代宗师", 29);
        Assert.False(failHighLvl.Success);
        Assert.Contains("超过拜师等级限制", failHighLvl.Message);

        // 2. 等级 <= 28 级拜师成功
        var succ = social.BecomeApprentice("一代宗师", 22);
        Assert.True(succ.Success);
        Assert.Equal("一代宗师", social.MasterName);

        // 3. 已有师父不可重复拜师
        var failRepeat = social.BecomeApprentice("另一位师父", 23);
        Assert.False(failRepeat.Success);
        Assert.Contains("不可重复拜师", failRepeat.Message);

        // 4. 离开师门
        var leaveRes = social.LeaveMaster();
        Assert.True(leaveRes.Success);
        Assert.Null(social.MasterName);
    }

    [Fact]
    public void Test_Apprentice_AcceptApprentice_Conditions()
    {
        var social = SocialManager.Instance;

        // 1. 自身等级未达35级且未出师，收徒失败
        var failLvl = social.AcceptApprentice("小师弟", CharacterJob.Warrior, 15, 30);
        Assert.False(failLvl.Success);
        Assert.Contains("暂无招收门徒资格", failLvl.Message);

        // 2. 徒弟等级超过28级，收徒失败
        var failApprenticeLvl = social.AcceptApprentice("老油条", CharacterJob.Taoist, 30, 40);
        Assert.False(failApprenticeLvl.Success);
        Assert.Contains("不可收为弟子", failApprenticeLvl.Message);

        // 3. 正常收徒 (自身40级，徒弟20级)
        var succ1 = social.AcceptApprentice("小师弟", CharacterJob.Warrior, 20, 40);
        Assert.True(succ1.Success);
        Assert.Single(social.Apprentices);

        // 4. 收满3个徒弟
        social.AcceptApprentice("二师弟", CharacterJob.Wizard, 18, 40);
        social.AcceptApprentice("三师妹", CharacterJob.Taoist, 15, 40);
        Assert.Equal(3, social.Apprentices.Count);

        // 5. 超过上限失败
        var failLimit = social.AcceptApprentice("四师弟", CharacterJob.Warrior, 10, 40);
        Assert.False(failLimit.Success);
        Assert.Contains("招收已达上限", failLimit.Message);
    }

    [Fact]
    public void Test_Apprentice_LevelUp_And_Graduation()
    {
        var social = SocialManager.Instance;
        social.AcceptApprentice("勤奋徒儿", CharacterJob.Warrior, 20, 42);

        // 1. 徒弟升级回馈声望 (升到 25 级，提升 5 级 -> 增加 10 声望)
        var lvlUpRes = social.OnApprenticeLevelUp("勤奋徒儿", 25);
        Assert.True(lvlUpRes.Success);
        Assert.Equal(10, lvlUpRes.RepGain);
        Assert.Equal(10, social.Reputation);

        // 2. 未满35级不能出师
        var gradFail = social.GraduateApprentice("勤奋徒儿");
        Assert.False(gradFail.Success);
        Assert.Contains("尚未达到出师标准", gradFail.Message);

        // 3. 徒弟升至 35 级并出师
        social.OnApprenticeLevelUp("勤奋徒儿", 35);
        Assert.Equal(30, social.Reputation); // 10 + 20 = 30

        var gradSucc = social.GraduateApprentice("勤奋徒儿");
        Assert.True(gradSucc.Success);
        Assert.Equal(30, gradSucc.MasterRepReward);
        Assert.Equal(60, social.Reputation); // 30 + 30 = 60
        Assert.True(social.Apprentices[0].IsGraduated);

        // 4. 重复出师判定
        var gradRepeat = social.GraduateApprentice("勤奋徒儿");
        Assert.False(gradRepeat.Success);
        Assert.Contains("早已功德圆满出师", gradRepeat.Message);
    }

    [Fact]
    public void Test_Self_Graduation()
    {
        var social = SocialManager.Instance;
        social.BecomeApprentice("玄天道尊", 20);

        // 未满35级不可出师
        var failUnderLvl = social.SelfGraduate(32);
        Assert.False(failUnderLvl.Success);

        // 满35级顺利出师
        var succ = social.SelfGraduate(35);
        Assert.True(succ.Success);
        Assert.True(social.IsGraduated);
        Assert.Equal(15, social.Reputation);
        Assert.Equal(15, succ.RepReward);

        // 出师后，即使自身不到35级（边界case），也允许收徒
        var canMaster = social.AcceptApprentice("小门生", CharacterJob.Taoist, 15, 30);
        Assert.True(canMaster.Success);
    }

    [Fact]
    public void Test_CommandParser_Social_Commands()
    {
        // 1. 拜师指令
        var cmdMaster = CommandParser.Parse("@master 无极剑圣");
        Assert.Equal(ParsedCommandType.Master, cmdMaster.Type);
        Assert.Equal("无极剑圣", cmdMaster.TargetName);

        // 2. 收徒指令
        var cmdApprentice = CommandParser.Parse("@apprentice 菜鸟小弟 22 道士");
        Assert.Equal(ParsedCommandType.Apprentice, cmdApprentice.Type);
        Assert.Equal("菜鸟小弟", cmdApprentice.TargetName);
        Assert.Equal(22, cmdApprentice.Amount);
        Assert.Equal(CharacterJob.Taoist, cmdApprentice.TargetJob);

        // 3. 出师指令
        var cmdGrad = CommandParser.Parse("@graduate 菜鸟小弟");
        Assert.Equal(ParsedCommandType.Graduate, cmdGrad.Type);
        Assert.Equal("菜鸟小弟", cmdGrad.TargetName);

        // 4. 好友指令
        var cmdFriend = CommandParser.Parse("@friend add 昔日兄弟 40 法师");
        Assert.Equal(ParsedCommandType.Friend, cmdFriend.Type);
        Assert.Equal("add", cmdFriend.SubAction);
        Assert.Equal("昔日兄弟", cmdFriend.TargetName);
        Assert.Equal(40, cmdFriend.Amount);
        Assert.Equal(CharacterJob.Wizard, cmdFriend.TargetJob);

        // 5. 声望指令
        var cmdRep = CommandParser.Parse("@reputation add 50");
        Assert.Equal(ParsedCommandType.Reputation, cmdRep.Type);
        Assert.Equal("add", cmdRep.SubAction);
        Assert.Equal(50, cmdRep.Amount);
    }
}
