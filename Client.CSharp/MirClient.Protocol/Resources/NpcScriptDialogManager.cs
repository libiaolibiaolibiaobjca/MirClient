using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace MirClient.Protocol.Resources;

/// <summary>
/// NPC 脚本超链接选项定义
/// </summary>
public record NpcScriptLink(
    int Index,
    string Text,
    string Tag,
    bool IsExit);

/// <summary>
/// 对话文本中的单个图文片段 (普通文本或可点击链接)
/// </summary>
public record NpcDialogSegment(
    string Text,
    bool IsLink,
    NpcScriptLink? LinkInfo = null);

/// <summary>
/// 对话框的一行内容 (包含若干文本与超链接片段)
/// </summary>
public class NpcDialogLine
{
    public List<NpcDialogSegment> Segments { get; } = new();
}

/// <summary>
/// 盛大传奇 / OpenMir2 标准 NPC 脚本对话框协议与解析状态机
/// </summary>
public class NpcScriptDialogManager
{
    public static NpcScriptDialogManager Instance { get; } = new();

    // 经典传奇脚本超链接正则: <显示文本/@跳转标签> 或 <显示文本/@标签(参数)>
    private static readonly Regex ScriptLinkRegex = new(@"<([^/@>]+)/@([^>]+)>", RegexOptions.Compiled);

    public int CurrentNpcRecogId { get; private set; } = 0;
    public string CurrentNpcName { get; private set; } = "";
    public int CurrentFaceIndex { get; private set; } = 0;
    public string RawSaying { get; private set; } = "";
    public bool IsOpen { get; private set; } = false;

    public List<NpcDialogLine> Lines { get; } = new();
    public List<NpcScriptLink> Links { get; } = new();

    /// <summary>
    /// 解析服务端下发的 SM_MERCHANTSAY 消息体
    /// </summary>
    /// <param name="npcRecogId">NPC 唯一实体ID</param>
    /// <param name="face">NPC 头像编号</param>
    /// <param name="rawBody">包含 NPC名字/正文内容 的原始文本</param>
    public void ParseMerchantSay(int npcRecogId, int face, string rawBody)
    {
        CurrentNpcRecogId = npcRecogId;
        CurrentFaceIndex = face;
        RawSaying = rawBody ?? "";
        Lines.Clear();
        Links.Clear();

        if (string.IsNullOrEmpty(rawBody))
        {
            IsOpen = false;
            return;
        }

        // 1. 切割 NPC 名字与正文 (以第一个 '/' 为界)
        int slashIdx = rawBody.IndexOf('/');
        string sayingContent;
        if (slashIdx >= 0)
        {
            CurrentNpcName = rawBody[..slashIdx].Trim();
            sayingContent = rawBody[(slashIdx + 1)..];
        }
        else
        {
            CurrentNpcName = "神秘隐士";
            sayingContent = rawBody;
        }

        // 2. 传奇脚本以 '\' 代表换行符
        string[] rawLines = sayingContent.Split('\\');
        int linkCounter = 1;

        foreach (var rawLine in rawLines)
        {
            var lineObj = new NpcDialogLine();
            if (string.IsNullOrEmpty(rawLine))
            {
                lineObj.Segments.Add(new NpcDialogSegment("", false));
                Lines.Add(lineObj);
                continue;
            }

            int lastIdx = 0;
            var matches = ScriptLinkRegex.Matches(rawLine);

            foreach (Match match in matches)
            {
                // 添加链接前的普通文本
                if (match.Index > lastIdx)
                {
                    string preText = rawLine[lastIdx..match.Index];
                    lineObj.Segments.Add(new NpcDialogSegment(preText, false));
                }

                string linkText = match.Groups[1].Value.Trim();
                string tagStr = match.Groups[2].Value.Trim();
                string fullTag = tagStr.StartsWith('@') ? tagStr : "@" + tagStr;
                bool isExit = fullTag.Equals("@exit", StringComparison.OrdinalIgnoreCase) ||
                              fullTag.Equals("@closewin", StringComparison.OrdinalIgnoreCase);

                var linkObj = new NpcScriptLink(linkCounter++, linkText, fullTag, isExit);
                Links.Add(linkObj);
                lineObj.Segments.Add(new NpcDialogSegment(linkText, true, linkObj));

                lastIdx = match.Index + match.Length;
            }

            // 添加行末剩余文本
            if (lastIdx < rawLine.Length)
            {
                string tailText = rawLine[lastIdx..];
                lineObj.Segments.Add(new NpcDialogSegment(tailText, false));
            }

            Lines.Add(lineObj);
        }

        IsOpen = true;
    }

    /// <summary>
    /// 选择指定序号的超链接 (序号 1 起始)
    /// </summary>
    public (bool Success, string TargetTag, bool ShouldClose) SelectLink(int linkIndex)
    {
        if (!IsOpen || linkIndex < 1 || linkIndex > Links.Count)
        {
            return (false, "", false);
        }

        var link = Links[linkIndex - 1];
        if (link.IsExit)
        {
            CloseDialog();
            return (true, link.Tag, true);
        }

        return (true, link.Tag, false);
    }

    /// <summary>
    /// 根据标签名进行选择
    /// </summary>
    public (bool Success, string TargetTag, bool ShouldClose) SelectLinkByTag(string tag)
    {
        if (!IsOpen || string.IsNullOrEmpty(tag))
        {
            return (false, "", false);
        }

        string normalized = tag.StartsWith('@') ? tag : "@" + tag;
        var link = Links.Find(l => l.Tag.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (link == null)
        {
            return (false, "", false);
        }

        if (link.IsExit)
        {
            CloseDialog();
            return (true, link.Tag, true);
        }

        return (true, link.Tag, false);
    }

    /// <summary>
    /// 关闭对话框
    /// </summary>
    public void CloseDialog()
    {
        IsOpen = false;
        CurrentNpcRecogId = 0;
        CurrentNpcName = "";
        CurrentFaceIndex = 0;
        RawSaying = "";
        Lines.Clear();
        Links.Clear();
    }

    /// <summary>
    /// 构建客户端发往服务端的 CM_CLICKNPC 封包
    /// </summary>
    public static (DefaultMessage Msg, string EncodedPacket) CreateClickNpcPacket(int npcRecogId)
    {
        var msg = new DefaultMessage
        {
            Recog = npcRecogId,
            Ident = ProtocolConsts.CM_CLICKNPC,
            Param = 0,
            Tag = 0,
            Series = 0
        };

        string encodedHeader = EDCode.EncodeMessage(msg);
        string packet = $"#{encodedHeader}!";
        return (msg, packet);
    }

    /// <summary>
    /// 构建客户端发往服务端的 CM_MERCHANTDLGSELECT 封包
    /// </summary>
    public static (DefaultMessage Msg, string EncodedPacket) CreateSelectPacket(int npcRecogId, string label)
    {
        var msg = new DefaultMessage
        {
            Recog = npcRecogId,
            Ident = ProtocolConsts.CM_MERCHANTDLGSELECT,
            Param = 0,
            Tag = 0,
            Series = 0
        };

        string encodedHeader = EDCode.EncodeMessage(msg);
        string encodedBody = EDCode.EncodeString(label);
        string packet = $"#{encodedHeader}{encodedBody}!";
        return (msg, packet);
    }

    /// <summary>
    /// 获取针对标准 NPC 的模拟对话脚本 (供单机沙盒与离线演示测试)
    /// </summary>
    public static string GetMockNpcScript(string npcName, string? subTag = null)
    {
        return npcName switch
        {
            "比奇老兵" => subTag switch
            {
                "@move_mengzhong" => "比奇老兵/你已经成功踏上前往盟重省的征程！\\盟重土城狂沙漫天，强者如林，万望勇士保重！\\<返回老兵主页/@main>\\<离开/@exit>",
                "@move_snake" => "比奇老兵/毒蛇山谷密林幽深，蛇群出没，前行务必备足解毒药水！\\<返回老兵主页/@main>\\<离开/@exit>",
                "@tianguan" => "比奇老兵/天雷天关试炼乃玛法天神设立之无上登天塔！\\每过一层皆有金银宝箱抽取金刚石与绝世神兵。\\前往盟重安全区可找天关守卫直接登塔！\\<返回主页/@main>\\<离开/@exit>",
                _ => "比奇老兵/年轻人，欢迎来到繁华的比奇大城！\\岁月悠悠，玛法大陆风云变幻，你欲前往何方闯荡？\\<前往盟重土城/@move_mengzhong>\\<前往毒蛇山谷/@move_snake>\\<打听天关试炼/@tianguan>\\<离开/@exit>"
            },
            "药店老板" => subTag switch
            {
                "@buy" => "药店老板/本店金创药与魔法药皆采用雪山纯正灵草配制，疗效显著！\\<购买中量金创药/@buy_hp>\\<购买中量魔法药/@buy_mp>\\<购买强效太阳水/@buy_sun>\\<返回/@main>",
                "@cure" => "药店老板/已为你精心包扎伤口并驱散毒素，气血充盈，生龙活虎！\\<返回/@main>",
                _ => "药店老板/客官请了！出门在外闯荡江湖，怎能不备足金创灵药与还魂神水？\\<购买药水/@buy>\\<疗伤祛毒/@cure>\\<离开/@exit>"
            },
            "铁匠铺老板" => subTag switch
            {
                "@repair" => "铁匠铺老板/将需要修理的神兵利刃交由老夫，管保锋芒如初！\\<普通修理/@repair_normal>\\<特殊修理/@repair_special>\\<返回/@main>",
                "@upgrade" => "铁匠铺老板/玄铁为骨，黑铁为精，配以珍稀首饰方能淬炼神兵！\\<开启武器升级/@start_upgrade>\\<返回/@main>",
                _ => "铁匠铺老板/叮当叮当！好刀需配好汉，百炼方成钢！\\<修理装备/@repair>\\<升级武器/@upgrade>\\<离开/@exit>"
            },
            "天关守卫" => subTag switch
            {
                "@enter" => "天关守卫/天门已开，雷霆咆哮！消耗天关灵符踏入试炼！\\<进入天关第一层/@enter_pass>\\<返回/@main>",
                "@exchange" => "天关守卫/天关灵符每张 50,000 金币，童叟无欺。\\<兑换一张天关灵符/@buy_1>\\<兑换三张天关灵符/@buy_3>\\<返回/@main>",
                _ => "天关守卫/天雷滚滚，王者神殿屹立于九天之上！\\唯有真正的玛法勇士方能叩开天关石门！\\<踏入天关试炼/@enter>\\<金币兑换灵符/@exchange>\\<离开/@exit>"
            },
            _ => $"{npcName}/玛法大陆岁月漫长，你我相逢即是有缘。\\<打听玛法秘闻/@rumor>\\<离开/@exit>"
        };
    }
}
