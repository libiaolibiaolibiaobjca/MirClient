using System;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 聊天频道分类
/// </summary>
public enum ChatChannel : byte
{
    Normal = 0,   // 普通视野内喊话 (白字/普通)
    Shout = 1,    // 广播大喊 (!前缀，黄色)
    Group = 2,    // 组队频道 (!!前缀，蓝色/青色)
    Guild = 3,    // 行会频道 (!~前缀，绿色)
    Whisper = 4,  // 私聊密语 (/前缀，黄色/橙色)
    System = 5    // 系统广播/公告/GM提示 (红色/全局)
}

/// <summary>
/// 聊天与广播网络事件实体
/// </summary>
public record ChatMessageEvent(
    long SenderId,
    string SenderName,
    string Content,
    ChatChannel Channel,
    int ColorCode = 0)
{
    /// <summary>
    /// 获取在聊天框中渲染的前缀修饰字符串
    /// </summary>
    public string FormattedText
    {
        get
        {
            return Channel switch
            {
                ChatChannel.Group => $"[组队] {SenderName}: {Content}",
                ChatChannel.Guild => $"[行会] {SenderName}: {Content}",
                ChatChannel.Shout => $"[喊话] {SenderName}: {Content}",
                ChatChannel.Whisper => $"[私聊] {SenderName}: {Content}",
                ChatChannel.System => string.IsNullOrEmpty(SenderName) || SenderName == "系统" || SenderName == "系统广播" 
                    ? $"[系统] {Content}" 
                    : $"[系统] {SenderName}: {Content}",
                _ => string.IsNullOrEmpty(SenderName) ? Content : $"{SenderName}: {Content}"
            };
        }
    }
}

/// <summary>
/// 玩家发言与多频道系统广播协议管理器 (100% 对齐 OpenMir2 PlayObject.Chat.cs 与 Messages.cs)
/// </summary>
public static class ChatProtocolManager
{
    /// <summary>
    /// 根据玩家输入的字符串前缀，自动推断所属频道、目标接收人以及净消息内容
    /// </summary>
    public static (ChatChannel Channel, string CleanContent, string Target) ResolveChannelFromInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return (ChatChannel.Normal, string.Empty, string.Empty);
        }

        string trimmed = input.Trim();

        // 私聊指令: /TargetName Message
        if (trimmed.StartsWith('/'))
        {
            string body = trimmed[1..].TrimStart();
            int spaceIdx = body.IndexOf(' ');
            if (spaceIdx > 0)
            {
                string target = body[..spaceIdx];
                string content = body[(spaceIdx + 1)..].TrimStart();
                return (ChatChannel.Whisper, content, target);
            }
            return (ChatChannel.Whisper, string.Empty, body);
        }

        // 组队频道指令: !!Message
        if (trimmed.StartsWith("!!", StringComparison.Ordinal))
        {
            return (ChatChannel.Group, trimmed[2..], string.Empty);
        }

        // 行会频道指令: !~Message
        if (trimmed.StartsWith("!~", StringComparison.Ordinal))
        {
            return (ChatChannel.Guild, trimmed[2..], string.Empty);
        }

        // 区域/全服喊话指令: !Message
        if (trimmed.StartsWith('!'))
        {
            return (ChatChannel.Shout, trimmed[1..], string.Empty);
        }

        // 默认为普通视野内发言
        return (ChatChannel.Normal, trimmed, string.Empty);
    }

    /// <summary>
    /// 构建向服务端发送的聊天发言封包 (CM_SAY = 3030 或 1007)
    /// </summary>
    public static (DefaultMessage Header, string EncodedBody) CreateSayPacket(
        string message,
        ChatChannel channel = ChatChannel.Normal,
        string whisperTarget = "",
        ushort command = ProtocolConsts.CM_SAY_OPENMIR2)
    {
        string fullText = message ?? string.Empty;

        // 根据频道规范补充前缀（若输入中未手动输入前缀）
        switch (channel)
        {
            case ChatChannel.Group when !fullText.StartsWith("!!"):
                fullText = "!!" + fullText;
                break;
            case ChatChannel.Guild when !fullText.StartsWith("!~"):
                fullText = "!~" + fullText;
                break;
            case ChatChannel.Shout when !fullText.StartsWith('!'):
                fullText = "!" + fullText;
                break;
            case ChatChannel.Whisper when !fullText.StartsWith('/'):
                fullText = $"/{whisperTarget} {fullText}";
                break;
        }

        DefaultMessage header = new(ident: command, recog: 0, param: 0, tag: 0, series: 0);
        string encodedBody = EDCode.EncodeString(fullText);
        return (header, encodedBody);
    }

    /// <summary>
    /// 解析服务端下发的普通视野发言封包 (SM_HEAR = 40)
    /// </summary>
    public static ChatMessageEvent ParseHearMessage(DefaultMessage header, string rawBody)
    {
        long senderId = header.Recog;
        int color = header.Param;
        string text = EDCode.DecodeText(rawBody ?? string.Empty);

        string senderName = string.Empty;
        string content = text;

        int colonIdx = text.IndexOf(':');
        if (colonIdx > 0)
        {
            senderName = text[..colonIdx].Trim();
            content = text[(colonIdx + 1)..].TrimStart();
        }

        return new ChatMessageEvent(
            SenderId: senderId,
            SenderName: senderName,
            Content: content,
            Channel: ChatChannel.Normal,
            ColorCode: color);
    }

    /// <summary>
    /// 解析系统广播公告封包 (SM_SYSMESSAGE = 100)
    /// </summary>
    public static ChatMessageEvent ParseSystemMessage(DefaultMessage header, string rawBody)
    {
        int msgType = header.Param;
        string text = EDCode.DecodeText(rawBody ?? string.Empty);

        return new ChatMessageEvent(
            SenderId: 0,
            SenderName: "系统广播",
            Content: text,
            Channel: ChatChannel.System,
            ColorCode: msgType);
    }

    /// <summary>
    /// 解析组队频道聊天消息 (SM_GROUPMESSAGE = 101)
    /// </summary>
    public static ChatMessageEvent ParseGroupMessage(DefaultMessage header, string rawBody)
    {
        string text = EDCode.DecodeText(rawBody ?? string.Empty);
        string senderName = string.Empty;
        string content = text;

        int colonIdx = text.IndexOf(':');
        if (colonIdx > 0)
        {
            senderName = text[..colonIdx].Trim();
            content = text[(colonIdx + 1)..].TrimStart();
        }

        return new ChatMessageEvent(
            SenderId: header.Recog,
            SenderName: senderName,
            Content: content,
            Channel: ChatChannel.Group,
            ColorCode: header.Param);
    }

    /// <summary>
    /// 解析行会频道聊天消息 (SM_GUILDMESSAGE = 104)
    /// </summary>
    public static ChatMessageEvent ParseGuildMessage(DefaultMessage header, string rawBody)
    {
        string text = EDCode.DecodeText(rawBody ?? string.Empty);
        string senderName = string.Empty;
        string content = text;

        int colonIdx = text.IndexOf(':');
        if (colonIdx > 0)
        {
            senderName = text[..colonIdx].Trim();
            content = text[(colonIdx + 1)..].TrimStart();
        }

        return new ChatMessageEvent(
            SenderId: header.Recog,
            SenderName: senderName,
            Content: content,
            Channel: ChatChannel.Guild,
            ColorCode: header.Param);
    }

    /// <summary>
    /// 解析私聊密语消息 (SM_WHISPER = 103)
    /// </summary>
    public static ChatMessageEvent ParseWhisperMessage(DefaultMessage header, string rawBody)
    {
        string text = EDCode.DecodeText(rawBody ?? string.Empty);
        string senderName = string.Empty;
        string content = text;

        // 格式通常为: "SenderName=> Content" 或 "SenderName: Content"
        int arrowIdx = text.IndexOf("=>", StringComparison.Ordinal);
        if (arrowIdx > 0)
        {
            senderName = text[..arrowIdx].Trim();
            content = text[(arrowIdx + 2)..].TrimStart();
        }
        else
        {
            int colonIdx = text.IndexOf(':');
            if (colonIdx > 0)
            {
                senderName = text[..colonIdx].Trim();
                content = text[(colonIdx + 1)..].TrimStart();
            }
        }

        return new ChatMessageEvent(
            SenderId: header.Recog,
            SenderName: senderName,
            Content: content,
            Channel: ChatChannel.Whisper,
            ColorCode: header.Param);
    }
}
