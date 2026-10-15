using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class SabukSiegeTests
{
    [Fact]
    public void TestSabukSiege_DefaultState()
    {
        SabukSiegeManager sm = new();
        Assert.Equal(SiegePhase.Peace, sm.CurrentPhase);
        Assert.Equal("傲视天下", sm.CurrentLordGuild);
        Assert.Equal("傲视天下", sm.PalaceOccupierGuild);
        Assert.Equal(SabukSiegeManager.DEFAULT_GATE_MAX_HP, sm.GateCurrentHp);
        Assert.False(sm.IsGateDestroyed);
        Assert.Equal(0f, sm.RemainingBattleTime);
        Assert.True(sm.IsSabukLordGuild("傲视天下"));
        Assert.False(sm.IsSabukLordGuild("散人行会"));
    }

    [Fact]
    public void TestSabukSiege_StartAndGateCombat()
    {
        SabukSiegeManager sm = new();

        // 1. 成功开启攻城战
        bool started = sm.StartSiege(300f, out string startMsg);
        Assert.True(started);
        Assert.Equal(SiegePhase.Fighting, sm.CurrentPhase);
        Assert.Equal(300f, sm.RemainingBattleTime);
        Assert.Contains("号角吹响", startMsg);

        // 重复开启被阻断
        bool duplicate = sm.StartSiege(300f, out string dupMsg);
        Assert.False(duplicate);
        Assert.Contains("正在激烈交火中", dupMsg);

        // 2. 行会“兄弟连”攻击沙巴克城门
        bool hit1 = sm.DamageGate("兄弟连", 20000, out string hitMsg1);
        Assert.True(hit1);
        Assert.Equal(30000, sm.GateCurrentHp);
        Assert.False(sm.IsGateDestroyed);
        Assert.Contains("20000", hitMsg1);

        // 3. 再次攻击攻破城门
        bool hit2 = sm.DamageGate("兄弟连", 30000, out string hitMsg2);
        Assert.True(hit2);
        Assert.Equal(0, sm.GateCurrentHp);
        Assert.True(sm.IsGateDestroyed);
        Assert.Contains("猛烈击溃", hitMsg2);

        // 城门已破，再次攻击阻断
        bool hit3 = sm.DamageGate("兄弟连", 1000, out string hitMsg3);
        Assert.False(hit3);
        Assert.Contains("倒塌", hitMsg3);

        // 验证行会伤害积分
        Assert.True(sm.GuildScores.ContainsKey("兄弟连"));
        Assert.Equal(50000, sm.GuildScores["兄弟连"].GateDamage);
    }

    [Fact]
    public void TestSabukSiege_PalaceOccupationAndHoldTimer()
    {
        SabukSiegeManager sm = new();
        sm.StartSiege(300f, out _);

        // 1. “义薄云天”行会突入皇宫大殿夺得旗帜
        bool occupied = sm.OccupyPalace("义薄云天", out string occMsg);
        Assert.True(occupied);
        Assert.Equal("义薄云天", sm.PalaceOccupierGuild);
        Assert.Contains("夺得皇宫大殿旗帜控制权", occMsg);

        // 重复占领同一行会提示防守中
        bool reOccupy = sm.OccupyPalace("义薄云天", out string reMsg);
        Assert.False(reOccupy);
        Assert.Contains("誓死把守", reMsg);

        // 2. 模拟经过 3.5 秒战斗
        sm.Update(3.5f);
        var score = sm.GuildScores["义薄云天"];
        Assert.Equal(3, score.PalaceHoldSeconds); // 经过 3 整秒
        Assert.Equal(296.5f, sm.RemainingBattleTime);

        // 3. 记录击杀战绩
        sm.RecordKill("义薄云天");
        sm.RecordKill("义薄云天");
        Assert.Equal(2, score.KillCount);
        // 总积分 = 0/100 + 2*50 + 3*10 = 130
        Assert.Equal(130, score.TotalScore);
    }

    [Fact]
    public void TestSabukSiege_SettlementAndNewLordCoronation()
    {
        SabukSiegeManager sm = new();
        sm.StartSiege(300f, out _);

        // “龙腾四海”攻占皇宫
        sm.OccupyPalace("龙腾四海", out _);
        sm.Update(5.0f);

        // 结算攻城战
        bool stopped = sm.StopSiege(out string report);
        Assert.True(stopped);
        Assert.Equal(SiegePhase.Peace, sm.CurrentPhase);
        Assert.Equal("龙腾四海", sm.CurrentLordGuild); // 成功易主
        Assert.Contains("沙城易主", report);
        Assert.Contains("龙腾四海", report);
    }

    [Fact]
    public void TestSabukSiege_SabukTitleFormatting()
    {
        SabukSiegeManager sm = new();
        // 当前沙城霸主是“傲视天下”
        string leaderTitle = sm.FormatSabukTitle("傲视天下", "会长");
        string memberTitle = sm.FormatSabukTitle("傲视天下", "堂主");
        string nonMemberTitle = sm.FormatSabukTitle("散人行会", "会长");

        Assert.Equal("【沙巴克·城主】", leaderTitle);
        Assert.Equal("【沙巴克】", memberTitle);
        Assert.Equal("", nonMemberTitle);
    }

    [Fact]
    public void TestSabukSiege_CommandParsing()
    {
        var cmd1 = CommandParser.Parse("@siege start 450");
        Assert.Equal(ParsedCommandType.Siege, cmd1.Type);
        Assert.Equal("start", cmd1.SubAction);
        Assert.Equal(450, cmd1.Amount);

        var cmd2 = CommandParser.Parse("@siege stop");
        Assert.Equal(ParsedCommandType.Siege, cmd2.Type);
        Assert.Equal("stop", cmd2.SubAction);

        var cmd3 = CommandParser.Parse("@siege status");
        Assert.Equal(ParsedCommandType.Siege, cmd3.Type);
        Assert.Equal("status", cmd3.SubAction);

        var cmd4 = CommandParser.Parse("@siege palace 战神盟");
        Assert.Equal(ParsedCommandType.Siege, cmd4.Type);
        Assert.Equal("palace", cmd4.SubAction);
        Assert.Equal("战神盟", cmd4.TargetName);

        var cmd5 = CommandParser.Parse("@siege gate 6666 战神盟");
        Assert.Equal(ParsedCommandType.Siege, cmd5.Type);
        Assert.Equal("gate", cmd5.SubAction);
        Assert.Equal(6666, cmd5.Amount);
        Assert.Equal("战神盟", cmd5.TargetName);
    }
}
