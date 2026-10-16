using System;
using MirClient.Protocol;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class NpcScriptProtocolTests
{
    public NpcScriptProtocolTests()
    {
        NpcScriptDialogManager.Instance.CloseDialog();
    }

    [Fact]
    public void Test_ParseMerchantSay_HeaderAndName_SplitsCorrectly()
    {
        var mgr = NpcScriptDialogManager.Instance;
        string script = "比奇老兵/年轻人，欢迎来到繁华的比奇大城！\\玛法大陆风云变幻，你欲前往何方闯荡？";

        mgr.ParseMerchantSay(1001, 1, script);

        Assert.True(mgr.IsOpen);
        Assert.Equal(1001, mgr.CurrentNpcRecogId);
        Assert.Equal("比奇老兵", mgr.CurrentNpcName);
        Assert.Equal(2, mgr.Lines.Count);
        Assert.Equal("年轻人，欢迎来到繁华的比奇大城！", mgr.Lines[0].Segments[0].Text);
        Assert.Equal("玛法大陆风云变幻，你欲前往何方闯荡？", mgr.Lines[1].Segments[0].Text);
    }

    [Fact]
    public void Test_ParseMerchantSay_SlashNewline_CreatesLines()
    {
        var mgr = NpcScriptDialogManager.Instance;
        string script = "药店老板/第一行内容\\第二行内容\\第三行内容";

        mgr.ParseMerchantSay(2002, 2, script);

        Assert.Equal(3, mgr.Lines.Count);
        Assert.Equal("第一行内容", mgr.Lines[0].Segments[0].Text);
        Assert.Equal("第二行内容", mgr.Lines[1].Segments[0].Text);
        Assert.Equal("第三行内容", mgr.Lines[2].Segments[0].Text);
    }

    [Fact]
    public void Test_ParseMerchantSay_Hyperlinks_ExtractsTextAndTags()
    {
        var mgr = NpcScriptDialogManager.Instance;
        string script = "铁匠铺老板/欢迎光临！\\<修理装备/@repair>\\<升级武器/@upgrade>\\<离开/@exit>";

        mgr.ParseMerchantSay(3003, 3, script);

        Assert.Equal(4, mgr.Lines.Count);
        Assert.Equal(3, mgr.Links.Count);

        // 验证各选项提取
        Assert.Equal("修理装备", mgr.Links[0].Text);
        Assert.Equal("@repair", mgr.Links[0].Tag);
        Assert.False(mgr.Links[0].IsExit);

        Assert.Equal("升级武器", mgr.Links[1].Text);
        Assert.Equal("@upgrade", mgr.Links[1].Tag);
        Assert.False(mgr.Links[1].IsExit);

        Assert.Equal("离开", mgr.Links[2].Text);
        Assert.Equal("@exit", mgr.Links[2].Tag);
        Assert.True(mgr.Links[2].IsExit);
    }

    [Fact]
    public void Test_SelectLink_TriggersCorrectTag()
    {
        var mgr = NpcScriptDialogManager.Instance;
        string script = "比奇老兵/请选择传送地点：\\<前往盟重土城/@move_mengzhong>\\<前往毒蛇山谷/@move_snake>";

        mgr.ParseMerchantSay(1001, 1, script);

        // 选择序号 1
        var (s1, tag1, close1) = mgr.SelectLink(1);
        Assert.True(s1);
        Assert.Equal("@move_mengzhong", tag1);
        Assert.False(close1);
        Assert.True(mgr.IsOpen);

        // 选择序号 2
        var (s2, tag2, close2) = mgr.SelectLink(2);
        Assert.True(s2);
        Assert.Equal("@move_snake", tag2);
        Assert.False(close2);

        // 越界序号
        var (sOut, _, _) = mgr.SelectLink(99);
        Assert.False(sOut);
    }

    [Fact]
    public void Test_SelectLink_ExitTag_ClosesDialog()
    {
        var mgr = NpcScriptDialogManager.Instance;
        string script = "隐士/闲谈莫论人非。\\<离开/@exit>";

        mgr.ParseMerchantSay(5005, 0, script);
        Assert.True(mgr.IsOpen);

        var (s, tag, shouldClose) = mgr.SelectLink(1);
        Assert.True(s);
        Assert.Equal("@exit", tag);
        Assert.True(shouldClose);
        Assert.False(mgr.IsOpen); // 确认对话框已关闭
    }

    [Fact]
    public void Test_CreateClickNpcPacket_GeneratesValidFormat()
    {
        int npcId = 12345;
        var (msg, packet) = NpcScriptDialogManager.CreateClickNpcPacket(npcId);

        Assert.Equal(ProtocolConsts.CM_CLICKNPC, msg.Ident);
        Assert.Equal(npcId, msg.Recog);
        Assert.StartsWith("#", packet);
        Assert.EndsWith("!", packet);

        // 验证封包解密还原
        string inner = packet.Trim('#', '!');
        var decodedMsg = EDCode.DecodeMessage(inner);
        Assert.Equal(ProtocolConsts.CM_CLICKNPC, decodedMsg.Ident);
        Assert.Equal(npcId, decodedMsg.Recog);
    }

    [Fact]
    public void Test_CreateSelectPacket_GeneratesValidFormat()
    {
        int npcId = 98765;
        string label = "@buy_potions";
        var (msg, packet) = NpcScriptDialogManager.CreateSelectPacket(npcId, label);

        Assert.Equal(ProtocolConsts.CM_MERCHANTDLGSELECT, msg.Ident);
        Assert.Equal(npcId, msg.Recog);
        Assert.StartsWith("#", packet);
        Assert.EndsWith("!", packet);

        // 提取并解包
        string inner = packet.Trim('#', '!');
        string headerPart = inner[..16]; // 12字节对应 16个 base64 字符
        string bodyPart = inner[16..];

        var decodedHeader = EDCode.DecodeMessage(headerPart);
        string decodedLabel = EDCode.DecodeText(bodyPart);

        Assert.Equal(ProtocolConsts.CM_MERCHANTDLGSELECT, decodedHeader.Ident);
        Assert.Equal(npcId, decodedHeader.Recog);
        Assert.Equal(label, decodedLabel);
    }

    [Fact]
    public void Test_MockNpcScript_MultiLevelNavigation()
    {
        // 初始主页
        string mainScript = NpcScriptDialogManager.GetMockNpcScript("比奇老兵");
        Assert.Contains("前往盟重土城", mainScript);
        Assert.Contains("前往毒蛇山谷", mainScript);
        Assert.Contains("打听天关试炼", mainScript);

        // 子分支页面
        string subScript = NpcScriptDialogManager.GetMockNpcScript("比奇老兵", "@move_mengzhong");
        Assert.Contains("踏上前往盟重省的征程", subScript);
        Assert.Contains("返回老兵主页", subScript);
    }
}
