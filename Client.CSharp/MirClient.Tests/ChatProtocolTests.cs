using System;
using MirClient.Protocol;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class ChatProtocolTests
{
    [Fact]
    public void Test_ResolveChannelFromInput_Normal_ReturnsNormalChannel()
    {
        string input = "大家好，我是新手战士！";
        var (channel, clean, target) = ChatProtocolManager.ResolveChannelFromInput(input);

        Assert.Equal(ChatChannel.Normal, channel);
        Assert.Equal("大家好，我是新手战士！", clean);
        Assert.Empty(target);
    }

    [Fact]
    public void Test_ResolveChannelFromInput_Shout_ReturnsShoutChannel()
    {
        string input = "!重金收购裁决之杖，有的密！";
        var (channel, clean, target) = ChatProtocolManager.ResolveChannelFromInput(input);

        Assert.Equal(ChatChannel.Shout, channel);
        Assert.Equal("重金收购裁决之杖，有的密！", clean);
        Assert.Empty(target);
    }

    [Fact]
    public void Test_ResolveChannelFromInput_Group_ReturnsGroupChannel()
    {
        string input = "!!道士加防，法师铺火，集火沃玛教主！";
        var (channel, clean, target) = ChatProtocolManager.ResolveChannelFromInput(input);

        Assert.Equal(ChatChannel.Group, channel);
        Assert.Equal("道士加防，法师铺火，集火沃玛教主！", clean);
        Assert.Empty(target);
    }

    [Fact]
    public void Test_ResolveChannelFromInput_Guild_ReturnsGuildChannel()
    {
        string input = "!~今晚8点准时沙巴克集合攻城，请大家备齐药水！";
        var (channel, clean, target) = ChatProtocolManager.ResolveChannelFromInput(input);

        Assert.Equal(ChatChannel.Guild, channel);
        Assert.Equal("今晚8点准时沙巴克集合攻城，请大家备齐药水！", clean);
        Assert.Empty(target);
    }

    [Fact]
    public void Test_ResolveChannelFromInput_Whisper_ExtractsTargetAndContent()
    {
        string input = "/昔日法神 兄弟，来土城药店汇合。";
        var (channel, clean, target) = ChatProtocolManager.ResolveChannelFromInput(input);

        Assert.Equal(ChatChannel.Whisper, channel);
        Assert.Equal("昔日法神", target);
        Assert.Equal("兄弟，来土城药店汇合。", clean);
    }

    [Fact]
    public void Test_CreateSayPacket_EncodesPayloadAndPrefixCorrectly()
    {
        var (header, encodedBody) = ChatProtocolManager.CreateSayPacket(
            message: "今晚沙巴克见",
            channel: ChatChannel.Guild,
            command: ProtocolConsts.CM_SAY_OPENMIR2);

        Assert.Equal(ProtocolConsts.CM_SAY_OPENMIR2, header.Ident);
        string decoded = EDCode.DecodeText(encodedBody);
        Assert.Equal("!~今晚沙巴克见", decoded);
    }

    [Fact]
    public void Test_ParseHearMessage_DecodesSpeakerAndContent()
    {
        DefaultMessage header = new(
            ident: ProtocolConsts.SM_HEAR,
            recog: 10086,
            param: 1, // 颜色代码
            tag: 0,
            series: 0);

        string rawPayload = EDCode.EncodeString("无敌战神: 谁在比奇城门口PK？");
        var chatEvt = ChatProtocolManager.ParseHearMessage(header, rawPayload);

        Assert.Equal(10086, chatEvt.SenderId);
        Assert.Equal("无敌战神", chatEvt.SenderName);
        Assert.Equal("谁在比奇城门口PK？", chatEvt.Content);
        Assert.Equal(ChatChannel.Normal, chatEvt.Channel);
        Assert.Equal("无敌战神: 谁在比奇城门口PK？", chatEvt.FormattedText);
    }

    [Fact]
    public void Test_ParseSystemAndWhisperMessages_FormatsChannelTextProperly()
    {
        // 1. 系统消息测试
        DefaultMessage sysHeader = new(
            ident: ProtocolConsts.SM_SYSMESSAGE,
            recog: 0,
            param: 2,
            tag: 0,
            series: 0);
        string sysPayload = EDCode.EncodeString("怪物攻城活动将于 10 分钟后开启！");
        var sysEvt = ChatProtocolManager.ParseSystemMessage(sysHeader, sysPayload);

        Assert.Equal(ChatChannel.System, sysEvt.Channel);
        Assert.Equal("[系统] 怪物攻城活动将于 10 分钟后开启！", sysEvt.FormattedText);

        // 2. 密语消息测试
        DefaultMessage whisperHeader = new(
            ident: ProtocolConsts.SM_WHISPER,
            recog: 20001,
            param: 0,
            tag: 0,
            series: 0);
        string whisperPayload = EDCode.EncodeString("神秘商人=> 我有一本《烈火剑法》秘籍，要吗？");
        var whisperEvt = ChatProtocolManager.ParseWhisperMessage(whisperHeader, whisperPayload);

        Assert.Equal(ChatChannel.Whisper, whisperEvt.Channel);
        Assert.Equal("神秘商人", whisperEvt.SenderName);
        Assert.Equal("我有一本《烈火剑法》秘籍，要吗？", whisperEvt.Content);
        Assert.Equal("[私聊] 神秘商人: 我有一本《烈火剑法》秘籍，要吗？", whisperEvt.FormattedText);

        // 3. 组队消息测试
        DefaultMessage groupHeader = new(
            ident: ProtocolConsts.SM_GROUPMESSAGE,
            recog: 20002,
            param: 0,
            tag: 0,
            series: 0);
        string groupPayload = EDCode.EncodeString("队长: 大家跟紧，准备进祖玛七层！");
        var groupEvt = ChatProtocolManager.ParseGroupMessage(groupHeader, groupPayload);

        Assert.Equal(ChatChannel.Group, groupEvt.Channel);
        Assert.Equal("[组队] 队长: 大家跟紧，准备进祖玛七层！", groupEvt.FormattedText);
    }
}
