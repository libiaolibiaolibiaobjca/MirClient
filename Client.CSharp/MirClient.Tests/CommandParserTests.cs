using System.Numerics;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class CommandParserTests
{
    [Fact]
    public void TestParse_MakeCommand()
    {
        // 刷武器
        var cmd1 = CommandParser.Parse("@make 裁决之杖");
        Assert.Equal(ParsedCommandType.Make, cmd1.Type);
        Assert.NotNull(cmd1.CreatedItem);
        Assert.Equal("裁决之杖", cmd1.CreatedItem.Name);
        Assert.Equal(ItemType.Weapon, cmd1.CreatedItem.Type);

        // 刷药水带数量
        var cmd2 = CommandParser.Parse("@make 金创药 8");
        Assert.Equal(ParsedCommandType.Make, cmd2.Type);
        Assert.NotNull(cmd2.CreatedItem);
        Assert.Equal("金创药(小包)", cmd2.CreatedItem.Name);
        Assert.Equal(8, cmd2.CreatedItem.Count);

        // 刷不存在的物品返回 Unknown 提示
        var cmdFail = CommandParser.Parse("@make 倚天剑");
        Assert.Equal(ParsedCommandType.Unknown, cmdFail.Type);
        Assert.Contains("未找到指定物品", cmdFail.Message);
    }

    [Fact]
    public void TestParse_GoldCommand()
    {
        var cmd = CommandParser.Parse("@gold 88888");
        Assert.Equal(ParsedCommandType.Gold, cmd.Type);
        Assert.Equal(88888, cmd.Amount);

        var cmdFail = CommandParser.Parse("@gold abc");
        Assert.Equal(ParsedCommandType.Unknown, cmdFail.Type);
        Assert.Contains("格式错误", cmdFail.Message);
    }

    [Fact]
    public void TestParse_MobCommand()
    {
        // 经典白野猪
        var cmd1 = CommandParser.Parse("@mob 白野猪");
        Assert.Equal(ParsedCommandType.Mob, cmd1.Type);
        Assert.Equal("白野猪", cmd1.TargetName);

        // 别名“猫”自动解析为“钉耙猫”
        var cmd2 = CommandParser.Parse("/mob 猫");
        Assert.Equal(ParsedCommandType.Mob, cmd2.Type);
        Assert.Equal("钉耙猫", cmd2.TargetName);
    }

    [Fact]
    public void TestParse_MoveCommand()
    {
        var cmd = CommandParser.Parse("@move 330 268");
        Assert.Equal(ParsedCommandType.Move, cmd.Type);
        Assert.Equal(new Vector2(330, 268), cmd.TargetTile);

        var cmdFail = CommandParser.Parse("@move 330");
        Assert.Equal(ParsedCommandType.Unknown, cmdFail.Type);
        Assert.Contains("格式错误", cmdFail.Message);
    }

    [Fact]
    public void TestParse_FullCommand()
    {
        var cmd = CommandParser.Parse("@full");
        Assert.Equal(ParsedCommandType.Full, cmd.Type);
        Assert.Contains("状态全满", cmd.Message);
    }

    [Fact]
    public void TestParse_LevelAndExpCommand()
    {
        var cmdLvl = CommandParser.Parse("@level 35");
        Assert.Equal(ParsedCommandType.Level, cmdLvl.Type);
        Assert.Equal(35, cmdLvl.Amount);

        var cmdExp = CommandParser.Parse("@exp 50000");
        Assert.Equal(ParsedCommandType.Exp, cmdExp.Type);
        Assert.Equal(50000, cmdExp.Amount);
    }

    [Fact]
    public void TestParse_ChatAndHelp()
    {
        // 普通公屏消息
        var chatCmd = CommandParser.Parse("组队去猪洞七层打白野猪！");
        Assert.Equal(ParsedCommandType.Chat, chatCmd.Type);
        Assert.Equal("组队去猪洞七层打白野猪！", chatCmd.Message);

        // @help 帮助指令
        var helpCmd = CommandParser.Parse("@help");
        Assert.Equal(ParsedCommandType.Help, helpCmd.Type);
        Assert.Contains("GM调试指令一览", helpCmd.Message);
    }
}
