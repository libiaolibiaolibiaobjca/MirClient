using System.Numerics;

namespace MirClient.Protocol.Resources;

public enum ParsedCommandType
{
    Chat,       // 普通公屏聊天
    Make,       // @make 物品名称 [数量]
    Gold,       // @gold 数量
    Mob,        // @mob 怪物名称 [数量]
    Move,       // @move X Y
    Full,       // @full 满血满魔
    Help,       // @help 指令帮助
    Unknown     // 未知指令
}

public class ParsedCommand
{
    public ParsedCommandType Type { get; set; }
    public string RawInput { get; set; } = "";
    public string Message { get; set; } = "";
    public Item? CreatedItem { get; set; }
    public int Amount { get; set; }
    public string TargetName { get; set; } = "";
    public Vector2 TargetTile { get; set; }
}

/// <summary>
/// 盛大热血传奇经典聊天与 GM 指令解析引擎
/// </summary>
public static class CommandParser
{
    /// <summary>
    /// 解析输入字符串为结构化指令
    /// </summary>
    public static ParsedCommand Parse(string input)
    {
        input = input.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            return new ParsedCommand { Type = ParsedCommandType.Chat, RawInput = "", Message = "" };
        }

        // 判断是否为指令前缀 (@ 或 /)
        if (!input.StartsWith('@') && !input.StartsWith('/'))
        {
            return new ParsedCommand
            {
                Type = ParsedCommandType.Chat,
                RawInput = input,
                Message = input
            };
        }

        string cmdContent = input[1..].Trim();
        string[] parts = cmdContent.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return new ParsedCommand { Type = ParsedCommandType.Unknown, RawInput = input, Message = "无效的空指令！" };
        }

        string cmdName = parts[0].ToLowerInvariant();

        switch (cmdName)
        {
            case "make":
            case "制造":
            case "刷物品":
            {
                if (parts.Length < 2)
                {
                    return new ParsedCommand
                    {
                        Type = ParsedCommandType.Unknown,
                        RawInput = input,
                        Message = "格式错误！用法: @make <物品名称> [数量] (例如: @make 裁决之杖)"
                    };
                }

                string itemName = parts[1];
                int count = 1;
                if (parts.Length >= 3 && int.TryParse(parts[2], out int c))
                {
                    count = Math.Clamp(c, 1, 99);
                }

                Item? item = ResolveItemByName(itemName, count);
                if (item == null)
                {
                    return new ParsedCommand
                    {
                        Type = ParsedCommandType.Unknown,
                        RawInput = input,
                        Message = $"未找到指定物品【{itemName}】！可尝试: 木剑, 乌木剑, 裁决之杖, 布衣, 重型盔甲, 战神盔甲, 金创药, 魔法药"
                    };
                }

                return new ParsedCommand
                {
                    Type = ParsedCommandType.Make,
                    RawInput = input,
                    CreatedItem = item,
                    Amount = count,
                    TargetName = item.Name,
                    Message = $"成功制造物品【{item.Name}】(数量: {count})"
                };
            }

            case "gold":
            case "金币":
            case "钱":
            {
                if (parts.Length < 2 || !int.TryParse(parts[1], out int goldDelta))
                {
                    return new ParsedCommand
                    {
                        Type = ParsedCommandType.Unknown,
                        RawInput = input,
                        Message = "格式错误！用法: @gold <数量> (例如: @gold 50000)"
                    };
                }

                return new ParsedCommand
                {
                    Type = ParsedCommandType.Gold,
                    RawInput = input,
                    Amount = goldDelta,
                    Message = $"金币调整指令: {goldDelta:+#,##0;-#,##0;0}"
                };
            }

            case "mob":
            case "刷怪":
            case "召唤":
            {
                if (parts.Length < 2)
                {
                    return new ParsedCommand
                    {
                        Type = ParsedCommandType.Unknown,
                        RawInput = input,
                        Message = "格式错误！用法: @mob <怪物名称> (例如: @mob 白野猪)"
                    };
                }

                string mobName = ResolveMonsterName(parts[1]);
                return new ParsedCommand
                {
                    Type = ParsedCommandType.Mob,
                    RawInput = input,
                    TargetName = mobName,
                    Amount = 1,
                    Message = $"召唤怪物【{mobName}】"
                };
            }

            case "move":
            case "传送":
            case "瞬移":
            {
                if (parts.Length < 3 || 
                    !float.TryParse(parts[1], out float tx) || 
                    !float.TryParse(parts[2], out float ty))
                {
                    return new ParsedCommand
                    {
                        Type = ParsedCommandType.Unknown,
                        RawInput = input,
                        Message = "格式错误！用法: @move <X> <Y> (例如: @move 330 268)"
                    };
                }

                return new ParsedCommand
                {
                    Type = ParsedCommandType.Move,
                    RawInput = input,
                    TargetTile = new Vector2(tx, ty),
                    Message = $"传送至坐标 ({tx:F0}, {ty:F0})"
                };
            }

            case "full":
            case "满血":
            case "恢复":
            {
                return new ParsedCommand
                {
                    Type = ParsedCommandType.Full,
                    RawInput = input,
                    Message = "状态全满！生命值与魔法值已彻底恢复。"
                };
            }

            case "help":
            case "?":
            case "帮助":
            case "指令":
            {
                return new ParsedCommand
                {
                    Type = ParsedCommandType.Help,
                    RawInput = input,
                    Message = "GM调试指令一览:\n" +
                              "  @make <物品名> [数量] - 刷取神装或药水 (如 @make 裁决之杖)\n" +
                              "  @gold <数值>          - 充值随身金币 (如 @gold 100000)\n" +
                              "  @mob <怪物名>         - 眼前刷怪 (如 @mob 白野猪)\n" +
                              "  @move <X> <Y>         - 瞬移到指定坐标 (如 @move 330 268)\n" +
                              "  @full                 - 满血满魔\n" +
                              "  直接输入普通文本可进行公屏聊天发言！"
                };
            }

            default:
                return new ParsedCommand
                {
                    Type = ParsedCommandType.Unknown,
                    RawInput = input,
                    Message = $"未知 GM 指令【@{cmdName}】！输入 @help 可查看指令一览。"
                };
        }
    }

    private static Item? ResolveItemByName(string name, int count)
    {
        string n = name.Trim();
        return n switch
        {
            "木剑" => Item.CreateWoodSword(),
            "乌木剑" => Item.CreateEbonySword(),
            "裁决" or "裁决之杖" => Item.CreateJudgmentMace(),
            "布衣" or "布衣(男)" => Item.CreateClothArmor(),
            "重盔" or "重型盔甲" or "重型盔甲(男)" => Item.CreateHeavyArmor(),
            "战神" or "战神盔甲" or "战神盔甲(男)" => Item.CreateGodArmor(),
            "金创药" or "红药" or "金创药(小包)" => Item.CreateRedPotion(count),
            "魔法药" or "蓝药" or "魔法药(小包)" => Item.CreateBluePotion(count),
            _ => null
        };
    }

    private static string ResolveMonsterName(string name)
    {
        string n = name.Trim();
        return n switch
        {
            "小白" or "野猪" or "白野猪" => "白野猪",
            "雪人" or "森林雪人" => "森林雪人",
            "半兽人" => "半兽人",
            "猫" or "钉耙猫" => "钉耙猫",
            "草人" or "稻草人" => "稻草人",
            _ => n
        };
    }
}
