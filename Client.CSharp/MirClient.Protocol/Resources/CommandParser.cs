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
    Level,      // @level 等级
    Exp,        // @exp 经验值
    Cooldown,   // @cd 清空技能冷却
    Mute,       // @mute 开关静音
    Sound,      // @sound 音量(0~100)
    Play,       // @play 试听音效
    Map,        // @map 地图名/ID [X] [Y]
    Protect,    // @protect on/off [阈值%]
    Kill,       // @kill 角色阵亡自尽测试
    Revive,     // @revive 原地神圣复活
    Group,      // @group 组队管理与模拟队员
    Guild,      // @guild 行会与称号管理
    Stall,      // @stall 摆摊与市场集市
    Siege,      // @siege 沙巴克攻城战与皇宫争夺
    Repair,     // @repair 装备修理与特修
    Upgrade,    // @upgrade 铁匠铺武器升级锻造
    Durability, // @dura 修改武器持久度测试
    Master,     // @master 拜师
    Apprentice, // @apprentice 收徒与门徒管理
    Graduate,   // @graduate 武馆出师典礼
    Friend,     // @friend 好友管理与上下线
    Reputation, // @reputation 江湖声望查询与调整
    Badge,      // @badge 圣域荣誉勋章兑换与声望重铸
    Hero,       // @hero 英雄伙伴召唤/收回/模式切换
    Joint,      // @joint 释放主副心神合一合击绝技
    Fury,       // @fury 真气怒气槽调试
    Boss,       // @boss 世界教主刷新/击杀/战况
    Drop,       // @drop 极品神装与大爆测试
    Mount,      // @mount 坐骑上下马与名驹切换
    Charge,     // @charge 战马冲锋破军神技
    Internal,   // @internal 内功等级/内力/经验管理
    Combo,      // @combo 职业连击绝学释放与经脉贯通
    Wolong,     // @wolong 卧龙山庄探秘与五元宝藏
    Wine,       // @wine 盟重酒馆酿酒与药力醉酒度
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
    public bool Enabled { get; set; } = true;
    public string TargetName { get; set; } = "";
    public Vector2 TargetTile { get; set; }
    public SoundEffectType? TargetSound { get; set; }
    public string SubAction { get; set; } = "";
    public CharacterJob TargetJob { get; set; } = CharacterJob.Warrior;
    public GuildRank TargetRank { get; set; } = GuildRank.Member;
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

            case "level":
            case "等级":
            {
                if (parts.Length < 2 || !int.TryParse(parts[1], out int targetLevel))
                {
                    return new ParsedCommand
                    {
                        Type = ParsedCommandType.Unknown,
                        RawInput = input,
                        Message = "格式错误！用法: @level <目标等级> (例如: @level 35)"
                    };
                }

                return new ParsedCommand
                {
                    Type = ParsedCommandType.Level,
                    RawInput = input,
                    Amount = targetLevel,
                    Message = $"等级调整指令: 目标 {targetLevel} 级"
                };
            }

            case "exp":
            case "经验":
            {
                if (parts.Length < 2 || !long.TryParse(parts[1], out long expAmount))
                {
                    return new ParsedCommand
                    {
                        Type = ParsedCommandType.Unknown,
                        RawInput = input,
                        Message = "格式错误！用法: @exp <经验数值> (例如: @exp 50000)"
                    };
                }

                return new ParsedCommand
                {
                    Type = ParsedCommandType.Exp,
                    RawInput = input,
                    Amount = (int)Math.Min(int.MaxValue, expAmount),
                    Message = $"增加经验: {expAmount:#,##0}"
                };
            }

            case "cd":
            case "冷却":
            case "重置冷却":
            {
                return new ParsedCommand
                {
                    Type = ParsedCommandType.Cooldown,
                    RawInput = input,
                    Message = "所有技能冷却已彻底清空重置，即刻就绪！"
                };
            }

            case "mute":
            case "静音":
            {
                return new ParsedCommand
                {
                    Type = ParsedCommandType.Mute,
                    RawInput = input,
                    Message = "静音状态已切换"
                };
            }

            case "sound":
            case "音量":
            {
                if (parts.Length < 2 || !int.TryParse(parts[1], out int vol))
                {
                    return new ParsedCommand
                    {
                        Type = ParsedCommandType.Unknown,
                        RawInput = input,
                        Message = "格式错误！用法: @sound <0~100> (例如: @sound 80)"
                    };
                }

                return new ParsedCommand
                {
                    Type = ParsedCommandType.Sound,
                    RawInput = input,
                    Amount = Math.Clamp(vol, 0, 100),
                    Message = $"音量调整: {vol}%"
                };
            }

            case "play":
            case "音效":
            case "播放":
            {
                if (parts.Length < 2)
                {
                    return new ParsedCommand
                    {
                        Type = ParsedCommandType.Unknown,
                        RawInput = input,
                        Message = "格式错误！用法: @play <音效名> (例如: @play gold, @play thunder, @play levelup)"
                    };
                }

                string sName = parts[1].ToLowerInvariant();
                SoundEffectType? st = sName switch
                {
                    "gold" or "金币" => SoundEffectType.PickupGold,
                    "item" or "物品" or "装备" => SoundEffectType.PickupItem,
                    "thunder" or "雷电" => SoundEffectType.Thunder,
                    "fire" or "烈火" => SoundEffectType.FireSword,
                    "fireball" or "火球" => SoundEffectType.Fireball,
                    "heal" or "治愈" => SoundEffectType.Heal,
                    "swing" or "挥刀" => SoundEffectType.AttackSwing,
                    "die" or "死亡" or "怪物死" => SoundEffectType.MonsterDie,
                    "struck" or "受击" => SoundEffectType.MonsterStruck,
                    "potion" or "喝药" => SoundEffectType.UsePotion,
                    "levelup" or "升级" => SoundEffectType.LevelUp,
                    _ => null
                };

                if (!st.HasValue)
                {
                    return new ParsedCommand
                    {
                        Type = ParsedCommandType.Unknown,
                        RawInput = input,
                        Message = $"未知音效【{parts[1]}】！可选: gold, item, thunder, fire, fireball, heal, swing, die, potion, levelup"
                    };
                }

                return new ParsedCommand
                {
                    Type = ParsedCommandType.Play,
                    RawInput = input,
                    TargetSound = st.Value,
                    Message = $"试听音效【{st.Value}】"
                };
            }

            case "map":
            case "地图":
            case "切图":
            {
                if (parts.Length < 2)
                {
                    return new ParsedCommand
                    {
                        Type = ParsedCommandType.Unknown,
                        RawInput = input,
                        Message = "格式错误！用法: @map <地图名/ID> [X] [Y] (例如: @map 兽人古墓, @map 3, @map 0 330 268)"
                    };
                }

                string mapTarget = parts[1];
                Vector2 targetTile = Vector2.Zero;
                if (parts.Length >= 4 && float.TryParse(parts[2], out float x) && float.TryParse(parts[3], out float y))
                {
                    targetTile = new Vector2(x, y);
                }

                return new ParsedCommand
                {
                    Type = ParsedCommandType.Map,
                    RawInput = input,
                    TargetName = mapTarget,
                    TargetTile = targetTile,
                    Message = $"传送前往地图【{mapTarget}】" + (targetTile != Vector2.Zero ? $" 坐标: ({targetTile.X:F0}, {targetTile.Y:F0})" : "")
                };
            }

            case "protect":
            case "保护":
            case "守护":
            case "内挂":
            {
                if (parts.Length < 2)
                {
                    return new ParsedCommand
                    {
                        Type = ParsedCommandType.Unknown,
                        RawInput = input,
                        Message = "格式错误！用法: @protect <on/off> [阈值%] (例如: @protect on 40, @protect off)"
                    };
                }

                string sub = parts[1].ToLowerInvariant();
                bool enable = sub != "off" && sub != "关" && sub != "关闭" && sub != "0";
                int threshold = 35;
                if (parts.Length >= 3 && int.TryParse(parts[2], out int t))
                {
                    threshold = Math.Clamp(t, 10, 90);
                }
                else if (int.TryParse(parts[1], out int directT))
                {
                    enable = true;
                    threshold = Math.Clamp(directT, 10, 90);
                }

                return new ParsedCommand
                {
                    Type = ParsedCommandType.Protect,
                    RawInput = input,
                    Enabled = enable,
                    Amount = threshold,
                    Message = enable ? $"智能生命保护已开启，危急阈值: {threshold}%" : "智能生命保护已关闭"
                };
            }

            case "kill":
            case "自尽":
            case "死亡":
            {
                return new ParsedCommand
                {
                    Type = ParsedCommandType.Kill,
                    RawInput = input,
                    Message = "测试指令：生命值清空，角色进入战败阵亡状态。"
                };
            }

            case "revive":
            case "原地复活":
            case "复活":
            {
                return new ParsedCommand
                {
                    Type = ParsedCommandType.Revive,
                    RawInput = input,
                    Message = "测试指令：神圣复活，生命魔法恢复至满值。"
                };
            }

            case "group":
            case "party":
            case "组队":
            case "队伍":
            {
                if (parts.Length < 2)
                {
                    return new ParsedCommand
                    {
                        Type = ParsedCommandType.Unknown,
                        RawInput = input,
                        Message = "格式错误！用法:\n" +
                                  "  @group add <名字> [等级] [职业(战士/法师/道士)] - 模拟添加队友\n" +
                                  "  @group kick <名字>                            - 踢出队员\n" +
                                  "  @group leave                                  - 退出/解散队伍\n" +
                                  "  @group allow <on/off>                         - 开关允许组队"
                    };
                }

                string sub = parts[1].ToLowerInvariant();
                if (sub == "leave" || sub == "退队" || sub == "解散")
                {
                    return new ParsedCommand
                    {
                        Type = ParsedCommandType.Group,
                        RawInput = input,
                        SubAction = "leave",
                        Message = "队伍已解散或已退出当前队伍。"
                    };
                }
                else if (sub == "kick" || sub == "踢出")
                {
                    string targetName = parts.Length >= 3 ? parts[2] : "";
                    return new ParsedCommand
                    {
                        Type = ParsedCommandType.Group,
                        RawInput = input,
                        SubAction = "kick",
                        TargetName = targetName,
                        Message = $"已将【{targetName}】移出队伍。"
                    };
                }
                else if (sub == "allow" || sub == "允许")
                {
                    bool enable = parts.Length < 3 || (parts[2].ToLowerInvariant() != "off" && parts[2] != "0" && parts[2] != "关");
                    return new ParsedCommand
                    {
                        Type = ParsedCommandType.Group,
                        RawInput = input,
                        SubAction = "allow",
                        Enabled = enable,
                        Message = enable ? "已开启允许组队" : "已关闭允许组队"
                    };
                }
                else if (sub == "add" || sub == "添加" || sub == "邀请")
                {
                    string targetName = parts.Length >= 3 ? parts[2] : "无极道圣";
                    int level = 35;
                    CharacterJob job = CharacterJob.Warrior;

                    if (parts.Length >= 4 && int.TryParse(parts[3], out int parsedLvl))
                    {
                        level = Math.Clamp(parsedLvl, 1, 99);
                    }

                    if (parts.Length >= 5)
                    {
                        string jobStr = parts[4].ToLowerInvariant();
                        if (jobStr.Contains("法") || jobStr == "wizard" || jobStr == "1") job = CharacterJob.Wizard;
                        else if (jobStr.Contains("道") || jobStr == "taoist" || jobStr == "2") job = CharacterJob.Taoist;
                        else job = CharacterJob.Warrior;
                    }

                    return new ParsedCommand
                    {
                        Type = ParsedCommandType.Group,
                        RawInput = input,
                        SubAction = "add",
                        TargetName = targetName,
                        Amount = level,
                        TargetJob = job,
                        Message = $"模拟队员【{targetName}】({level}级 {job}) 加入了队伍！"
                    };
                }

                return new ParsedCommand
                {
                    Type = ParsedCommandType.Unknown,
                    RawInput = input,
                    Message = $"未知组队子指令【{parts[1]}】！可选: add, kick, leave, allow"
                };
            }

            case "guild":
            case "行会":
            case "帮会":
            {
                if (parts.Length < 2)
                {
                    return new ParsedCommand
                    {
                        Type = ParsedCommandType.Unknown,
                        RawInput = input,
                        Message = "格式错误！用法:\n" +
                                  "  @guild create <行会名称>                        - 创建新行会并自任会长\n" +
                                  "  @guild add <名字> [等级] [职业] [职务]         - 招募成员\n" +
                                  "  @guild rank <会长/副会长/堂主/精英/帮众>       - 调整自身职务\n" +
                                  "  @guild kick <名字>                             - 逐出成员\n" +
                                  "  @guild leave                                   - 退出/解散行会"
                    };
                }

                string sub = parts[1].ToLowerInvariant();
                if (sub == "leave" || sub == "退会" || sub == "解散")
                {
                    return new ParsedCommand
                    {
                        Type = ParsedCommandType.Guild,
                        RawInput = input,
                        SubAction = "leave",
                        Message = "您已退出行会，行会关系已解除。"
                    };
                }
                else if (sub == "kick" || sub == "逐出" || sub == "开除")
                {
                    string targetName = parts.Length >= 3 ? parts[2] : "";
                    return new ParsedCommand
                    {
                        Type = ParsedCommandType.Guild,
                        RawInput = input,
                        SubAction = "kick",
                        TargetName = targetName,
                        Message = $"已将【{targetName}】逐出行会。"
                    };
                }
                else if (sub == "create" || sub == "创建" || sub == "建会")
                {
                    string gName = parts.Length >= 3 ? parts[2] : "傲视天下";
                    return new ParsedCommand
                    {
                        Type = ParsedCommandType.Guild,
                        RawInput = input,
                        SubAction = "create",
                        TargetName = gName,
                        Message = $"成功创立行会【{gName}】！"
                    };
                }
                else if (sub == "rank" || sub == "职务" || sub == "称号")
                {
                    string rankStr = parts.Length >= 3 ? parts[2] : "堂主";
                    GuildRank r = rankStr switch
                    {
                        "会长" or "0" => GuildRank.Leader,
                        "副会长" or "1" => GuildRank.ViceLeader,
                        "堂主" or "2" => GuildRank.Elder,
                        "精英" or "3" => GuildRank.Elite,
                        _ => GuildRank.Member
                    };

                    return new ParsedCommand
                    {
                        Type = ParsedCommandType.Guild,
                        RawInput = input,
                        SubAction = "rank",
                        TargetRank = r,
                        Message = $"自身行会职务已调整为: 【{r}】"
                    };
                }
                else if (sub == "add" || sub == "招纳" || sub == "招募")
                {
                    string targetName = parts.Length >= 3 ? parts[2] : "沙巴克霸主";
                    int level = 40;
                    CharacterJob job = CharacterJob.Warrior;
                    GuildRank rank = GuildRank.Member;

                    if (parts.Length >= 4 && int.TryParse(parts[3], out int parsedLvl))
                    {
                        level = Math.Clamp(parsedLvl, 1, 99);
                    }

                    if (parts.Length >= 5)
                    {
                        string jobStr = parts[4].ToLowerInvariant();
                        if (jobStr.Contains("法") || jobStr == "wizard" || jobStr == "1") job = CharacterJob.Wizard;
                        else if (jobStr.Contains("道") || jobStr == "taoist" || jobStr == "2") job = CharacterJob.Taoist;
                        else job = CharacterJob.Warrior;
                    }

                    if (parts.Length >= 6)
                    {
                        string rankStr = parts[5];
                        rank = rankStr switch
                        {
                            "副会长" => GuildRank.ViceLeader,
                            "堂主" => GuildRank.Elder,
                            "精英" => GuildRank.Elite,
                            _ => GuildRank.Member
                        };
                    }

                    return new ParsedCommand
                    {
                        Type = ParsedCommandType.Guild,
                        RawInput = input,
                        SubAction = "add",
                        TargetName = targetName,
                        Amount = level,
                        TargetJob = job,
                        TargetRank = rank,
                        Message = $"招募行会成员【{targetName}】(Lv.{level} {job} [{rank}])！"
                    };
                }

                return new ParsedCommand
                {
                    Type = ParsedCommandType.Unknown,
                    RawInput = input,
                    Message = $"未知行会子指令【{parts[1]}】！可选: create, add, rank, kick, leave"
                };
            }

            case "stall":
            case "摆摊":
            case "设摊":
            {
                if (parts.Length < 2)
                {
                    return new ParsedCommand
                    {
                        Type = ParsedCommandType.Stall,
                        SubAction = "toggle",
                        RawInput = input,
                        Message = "摆摊指令: @stall on [招牌名], @stall off, @stall add <物品名> <价格>, @stall buy <序号>, @stall shout"
                    };
                }

                string subAction = parts[1].ToLowerInvariant();
                switch (subAction)
                {
                    case "on":
                    case "open":
                    case "开":
                    case "开启":
                    {
                        string title = parts.Length >= 3 ? string.Join(' ', parts.Skip(2)) : "【极品装备】特价大甩卖";
                        return new ParsedCommand
                        {
                            Type = ParsedCommandType.Stall,
                            SubAction = "on",
                            TargetName = title,
                            RawInput = input,
                            Message = $"开启摆摊，招牌设定为: {title}"
                        };
                    }

                    case "off":
                    case "close":
                    case "关":
                    case "收摊":
                    {
                        return new ParsedCommand
                        {
                            Type = ParsedCommandType.Stall,
                            SubAction = "off",
                            RawInput = input,
                            Message = "收起摊位并取回商品"
                        };
                    }

                    case "add":
                    case "上架":
                    {
                        if (parts.Length < 4 || !int.TryParse(parts[3], out int price))
                        {
                            return new ParsedCommand
                            {
                                Type = ParsedCommandType.Unknown,
                                RawInput = input,
                                Message = "格式错误！用法: @stall add <物品名称> <售价金币> (如: @stall add 裁决之杖 50000)"
                            };
                        }

                        return new ParsedCommand
                        {
                            Type = ParsedCommandType.Stall,
                            SubAction = "add",
                            TargetName = parts[2],
                            Amount = price,
                            RawInput = input,
                            Message = $"上架物品【{parts[2]}】，标价 {price} 金币"
                        };
                    }

                    case "buy":
                    case "购买":
                    {
                        int index = 0;
                        if (parts.Length >= 3 && int.TryParse(parts[2], out int idx))
                        {
                            index = idx;
                        }

                        return new ParsedCommand
                        {
                            Type = ParsedCommandType.Stall,
                            SubAction = "buy",
                            Amount = index,
                            RawInput = input,
                            Message = $"购买货架第 {index} 号商品"
                        };
                    }

                    case "shout":
                    case "喊话":
                    case "叫卖":
                    {
                        return new ParsedCommand
                        {
                            Type = ParsedCommandType.Stall,
                            SubAction = "shout",
                            RawInput = input,
                            Message = "发起公屏摆摊叫卖喊话"
                        };
                    }

                    default:
                        return new ParsedCommand
                        {
                            Type = ParsedCommandType.Unknown,
                            RawInput = input,
                            Message = $"未知摆摊子指令【{parts[1]}】！可选: on, off, add, buy, shout"
                        };
                }
            }

            case "siege":
            case "攻城":
            case "沙巴克":
            {
                if (parts.Length < 2)
                {
                    return new ParsedCommand
                    {
                        Type = ParsedCommandType.Siege,
                        SubAction = "status",
                        RawInput = input,
                        Message = "沙巴克攻城指令: @siege start [秒数], @siege stop, @siege status, @siege palace, @siege gate <伤害>"
                    };
                }

                string subAction = parts[1].ToLowerInvariant();
                switch (subAction)
                {
                    case "start":
                    case "开启":
                    case "开战":
                    {
                        int duration = 600;
                        if (parts.Length >= 3 && int.TryParse(parts[2], out int dur))
                        {
                            duration = dur;
                        }

                        return new ParsedCommand
                        {
                            Type = ParsedCommandType.Siege,
                            SubAction = "start",
                            Amount = duration,
                            RawInput = input,
                            Message = $"发起沙巴克攻城战，战事时长: {duration} 秒！"
                        };
                    }

                    case "stop":
                    case "停止":
                    case "停战":
                    case "结算":
                    {
                        return new ParsedCommand
                        {
                            Type = ParsedCommandType.Siege,
                            SubAction = "stop",
                            RawInput = input,
                            Message = "停止沙巴克攻城战并进行终局加冕结算"
                        };
                    }

                    case "status":
                    case "战况":
                    case "战报":
                    {
                        return new ParsedCommand
                        {
                            Type = ParsedCommandType.Siege,
                            SubAction = "status",
                            RawInput = input,
                            Message = "查看沙巴克攻城即时战报"
                        };
                    }

                    case "palace":
                    case "皇宫":
                    case "夺旗":
                    {
                        string guild = parts.Length >= 3 ? parts[2] : "";
                        return new ParsedCommand
                        {
                            Type = ParsedCommandType.Siege,
                            SubAction = "palace",
                            TargetName = guild,
                            RawInput = input,
                            Message = $"突入沙巴克皇宫争夺中央控制权{(string.IsNullOrEmpty(guild) ? "" : $" (行会: {guild})")}"
                        };
                    }

                    case "gate":
                    case "城门":
                    case "攻门":
                    {
                        int dmg = 1000;
                        if (parts.Length >= 3 && int.TryParse(parts[2], out int d))
                        {
                            dmg = d;
                        }

                        string guild = parts.Length >= 4 ? parts[3] : "";
                        return new ParsedCommand
                        {
                            Type = ParsedCommandType.Siege,
                            SubAction = "gate",
                            Amount = dmg,
                            TargetName = guild,
                            RawInput = input,
                            Message = $"对沙巴克城门造成 {dmg} 点破坏{(string.IsNullOrEmpty(guild) ? "" : $" (行会: {guild})")}"
                        };
                    }

                    default:
                        return new ParsedCommand
                        {
                            Type = ParsedCommandType.Unknown,
                            RawInput = input,
                            Message = $"未知攻城子指令【{parts[1]}】！可选: start, stop, status, palace, gate"
                        };
                }
            }

            case "repair":
            case "修理":
            case "特修":
            {
                bool special = parts.Length >= 2 && (parts[1].ToLowerInvariant() == "special" || parts[1] == "特修");
                return new ParsedCommand
                {
                    Type = ParsedCommandType.Repair,
                    Enabled = special,
                    SubAction = special ? "special" : "normal",
                    RawInput = input,
                    Message = special ? "神圣特殊修理全身装备 (保持久上限)" : "普通修理全身装备"
                };
            }

            case "upgrade":
            case "升级":
            case "锻造":
            {
                int purity = 60;
                int jewelry = 10;
                if (parts.Length >= 2 && int.TryParse(parts[1], out int p))
                {
                    purity = p;
                }
                if (parts.Length >= 3 && int.TryParse(parts[2], out int j))
                {
                    jewelry = j;
                }

                return new ParsedCommand
                {
                    Type = ParsedCommandType.Upgrade,
                    Amount = purity,
                    SubAction = jewelry.ToString(),
                    RawInput = input,
                    Message = $"铁匠铺升级当前武器 (黑铁纯度: {purity}, 首饰战力: {jewelry})"
                };
            }

            case "dura":
            case "持久":
            {
                int dura = 0;
                if (parts.Length >= 2 && int.TryParse(parts[1], out int d))
                {
                    dura = d;
                }

                return new ParsedCommand
                {
                    Type = ParsedCommandType.Durability,
                    Amount = dura,
                    RawInput = input,
                    Message = $"修改当前手持武器持久度为: {dura}"
                };
            }

            case "master":
            case "拜师":
            {
                if (parts.Length < 2)
                {
                    return new ParsedCommand
                    {
                        Type = ParsedCommandType.Unknown,
                        RawInput = input,
                        Message = "格式错误！用法: @master <师父名> (例如: @master 剑圣孤鸿)"
                    };
                }

                return new ParsedCommand
                {
                    Type = ParsedCommandType.Master,
                    TargetName = parts[1].Trim(),
                    RawInput = input,
                    Message = $"申请拜入【{parts[1].Trim()}】门下"
                };
            }

            case "apprentice":
            case "收徒":
            case "收弟子":
            {
                if (parts.Length < 2)
                {
                    return new ParsedCommand
                    {
                        Type = ParsedCommandType.Unknown,
                        RawInput = input,
                        Message = "格式错误！用法: @apprentice <徒弟名> [等级] [职业] (例如: @apprentice 小师弟 20 战士)"
                    };
                }

                string apprenticeName = parts[1].Trim();
                int lvl = 18;
                if (parts.Length >= 3 && int.TryParse(parts[2], out int parsedLvl))
                {
                    lvl = Math.Clamp(parsedLvl, 1, 35);
                }

                CharacterJob job = CharacterJob.Warrior;
                if (parts.Length >= 4)
                {
                    string j = parts[3].Trim();
                    if (j.Contains("法")) job = CharacterJob.Wizard;
                    else if (j.Contains("道")) job = CharacterJob.Taoist;
                }

                return new ParsedCommand
                {
                    Type = ParsedCommandType.Apprentice,
                    TargetName = apprenticeName,
                    Amount = lvl,
                    TargetJob = job,
                    RawInput = input,
                    Message = $"收录徒弟【{apprenticeName}】(等级: {lvl}, 职业: {job})"
                };
            }

            case "graduate":
            case "出师":
            {
                string target = parts.Length >= 2 ? parts[1].Trim() : "";
                return new ParsedCommand
                {
                    Type = ParsedCommandType.Graduate,
                    TargetName = target,
                    RawInput = input,
                    Message = string.IsNullOrEmpty(target) ? "自身举行武馆出师典礼" : $"为徒弟【{target}】举行出师礼"
                };
            }

            case "friend":
            case "好友":
            {
                if (parts.Length < 2)
                {
                    return new ParsedCommand
                    {
                        Type = ParsedCommandType.Friend,
                        SubAction = "list",
                        RawInput = input,
                        Message = "查看好友列表"
                    };
                }

                string sub = parts[1].ToLowerInvariant();
                string friendName = parts.Length >= 3 ? parts[2].Trim() : "";
                int fLevel = 35;
                if (parts.Length >= 4 && int.TryParse(parts[3], out int parsedFl))
                {
                    fLevel = Math.Clamp(parsedFl, 1, 70);
                }

                CharacterJob fJob = CharacterJob.Warrior;
                if (parts.Length >= 5)
                {
                    string j = parts[4].Trim();
                    if (j.Contains("法")) fJob = CharacterJob.Wizard;
                    else if (j.Contains("道")) fJob = CharacterJob.Taoist;
                }

                return new ParsedCommand
                {
                    Type = ParsedCommandType.Friend,
                    SubAction = sub,
                    TargetName = friendName,
                    Amount = fLevel,
                    TargetJob = fJob,
                    RawInput = input,
                    Message = $"好友操作: {sub} 【{friendName}】"
                };
            }

            case "rep":
            case "reputation":
            case "声望":
            {
                int repToAdd = 0;
                string sub = "query";
                if (parts.Length >= 3 && parts[1].ToLowerInvariant() == "add" && int.TryParse(parts[2], out int r))
                {
                    sub = "add";
                    repToAdd = r;
                }

                return new ParsedCommand
                {
                    Type = ParsedCommandType.Reputation,
                    SubAction = sub,
                    Amount = repToAdd,
                    RawInput = input,
                    Message = sub == "add" ? $"增加江湖声望: {repToAdd}" : "查询当前江湖声望"
                };
            }

            case "badge":
            case "勋章":
            case "荣誉勋章":
            {
                if (parts.Length < 2)
                {
                    return new ParsedCommand
                    {
                        Type = ParsedCommandType.Badge,
                        SubAction = "list",
                        RawInput = input,
                        Message = "查看圣域荣誉勋章图鉴"
                    };
                }

                string sub = parts[1].ToLowerInvariant();
                int badgeNum = 43; // 默认43号战士勋章
                if (parts.Length >= 3 && int.TryParse(parts[2], out int parsedNum))
                {
                    badgeNum = parsedNum;
                }

                return new ParsedCommand
                {
                    Type = ParsedCommandType.Badge,
                    SubAction = sub,
                    Amount = badgeNum,
                    RawInput = input,
                    Message = sub switch
                    {
                        "exchange" or "兑换" => $"声望兑换【{badgeNum}号】荣誉勋章",
                        "reforge" or "重铸" or "洗炼" => "重铸当前佩戴的荣誉勋章",
                        _ => $"荣誉勋章操作: {sub}"
                    }
                };
            }

            case "hero":
            case "英雄":
            {
                string sub = parts.Length >= 2 ? parts[1].ToLowerInvariant() : "toggle";
                string extra = parts.Length >= 3 ? parts[2].Trim() : "";

                CharacterJob heroJob = CharacterJob.Warrior;
                if (extra.Contains("法")) heroJob = CharacterJob.Wizard;
                else if (extra.Contains("道")) heroJob = CharacterJob.Taoist;

                return new ParsedCommand
                {
                    Type = ParsedCommandType.Hero,
                    SubAction = sub,
                    TargetName = extra,
                    TargetJob = heroJob,
                    RawInput = input,
                    Message = sub switch
                    {
                        "summon" or "出战" or "召唤" => "召唤副将英雄出战",
                        "recall" or "收回" or "休息" => "召回副将英雄休养",
                        "mode" or "模式" => "切换英雄行为模式 (跟随/攻击/守护)",
                        "revive" or "复活" => "复活阵亡的副将英雄",
                        "job" or "职业" => $"变更英雄职业为【{heroJob}】",
                        _ => "英雄随从操作"
                    }
                };
            }

            case "joint":
            case "合击":
            case "合击绝技":
            {
                return new ParsedCommand
                {
                    Type = ParsedCommandType.Joint,
                    RawInput = input,
                    Message = "释放主副合击绝技 (Ctrl+S)"
                };
            }

            case "fury":
            case "怒气":
            case "真气":
            {
                int val = 100;
                if (parts.Length >= 2 && int.TryParse(parts[1], out int parsedVal))
                {
                    val = Math.Clamp(parsedVal, 0, 100);
                }

                return new ParsedCommand
                {
                    Type = ParsedCommandType.Fury,
                    Amount = val,
                    RawInput = input,
                    Message = $"调整合击真气怒气槽为: {val}/100"
                };
            }

            case "boss":
            case "教主":
            case "世界boss":
            {
                string sub = parts.Length >= 2 ? parts[1].ToLowerInvariant() : "status";
                string extra = parts.Length >= 3 ? parts[2].Trim() : "";

                return new ParsedCommand
                {
                    Type = ParsedCommandType.Boss,
                    SubAction = sub,
                    TargetName = extra,
                    RawInput = input,
                    Message = sub switch
                    {
                        "spawn" or "刷新" or "召唤" => $"召唤世界教主【{(!string.IsNullOrEmpty(extra) ? extra : "祖玛教主")}】",
                        "kill" or "击杀" or "处决" => "处决当前世界教主并触发天女散花大爆",
                        "status" or "战况" or "状态" => "查看当前世界教主状态与伤害输出榜",
                        _ => "世界教主操作"
                    }
                };
            }

            case "drop":
            case "大爆":
            case "极品":
            {
                string itemName = parts.Length >= 2 ? parts[1].Trim() : "裁决之杖";
                int bonus = 3;
                if (parts.Length >= 3 && int.TryParse(parts[2], out int b))
                {
                    bonus = Math.Clamp(b, 1, 10);
                }

                return new ParsedCommand
                {
                    Type = ParsedCommandType.Drop,
                    TargetName = itemName,
                    Amount = bonus,
                    RawInput = input,
                    Message = $"生成极品战利品【{itemName} (极品+{bonus})】"
                };
            }

            case "mount":
            case "坐骑":
            case "战马":
            case "马":
            case "ride":
            {
                string sub = parts.Length >= 2 ? parts[1].ToLowerInvariant() : "toggle";
                string extra = parts.Length >= 3 ? parts[2].Trim() : "";

                return new ParsedCommand
                {
                    Type = ParsedCommandType.Mount,
                    SubAction = sub,
                    TargetName = extra,
                    RawInput = input,
                    Message = sub switch
                    {
                        "switch" or "切换" => $"切换坐骑为【{extra}】",
                        "ride" or "上马" => "翻身上马，极速驰骋",
                        "down" or "下马" => "勒马下马，步战厮杀",
                        _ => "上下马快捷切换 (Ctrl+M / M)"
                    }
                };
            }

            case "charge":
            case "冲锋":
            case "破军":
            {
                return new ParsedCommand
                {
                    Type = ParsedCommandType.Charge,
                    RawInput = input,
                    Message = "施展铁骑破军【战马冲锋】(Ctrl+F)"
                };
            }

            case "internal":
            case "内功":
            case "neigong":
            {
                string sub = parts.Length >= 2 ? parts[1].ToLowerInvariant() : "info";
                int val = 0;
                if (parts.Length >= 3 && int.TryParse(parts[2], out int v))
                {
                    val = v;
                }

                return new ParsedCommand
                {
                    Type = ParsedCommandType.Internal,
                    SubAction = sub,
                    Amount = val,
                    RawInput = input,
                    Message = sub switch
                    {
                        "lvl" or "level" or "级" => $"设置内功等级为 Lv.{val}",
                        "force" or "内力" or "mp" => $"调整当前内力值为 {val}",
                        "exp" or "经验" => $"增加内功经验值 +{val}",
                        "pierce" or "冲穴" or "脉" => "打通指定经脉穴位",
                        "full" or "满" => "内力全部拉满，穴位全通",
                        _ => "内功系统调试状态查询"
                    }
                };
            }

            case "combo":
            case "连击":
            case "lianji":
            {
                string sub = parts.Length >= 2 ? parts[1].ToLowerInvariant() : "cast";
                return new ParsedCommand
                {
                    Type = ParsedCommandType.Combo,
                    SubAction = sub,
                    RawInput = input,
                    Message = sub switch
                    {
                        "unlock" or "解锁" => "一键打通四大经脉，解锁四重连击绝学",
                        "full" or "满" => "连击冷却清空且内力全部回满",
                        _ => "施展本职业连击绝学神技 (Ctrl+D / C)"
                    }
                };
            }

            case "wolong":
            case "卧龙":
            case "山庄":
            {
                string sub = parts.Length >= 2 ? parts[1].ToLowerInvariant() : "status";
                string extra = parts.Length >= 3 ? parts[2].Trim() : "";
                int count = 0;
                if (parts.Length >= 3 && int.TryParse(parts[2], out int c))
                {
                    count = c;
                }

                return new ParsedCommand
                {
                    Type = ParsedCommandType.Wolong,
                    SubAction = sub,
                    TargetName = extra,
                    Amount = count,
                    RawInput = input,
                    Message = sub switch
                    {
                        "enter" or "开启" or "进入" => "开启卧龙山庄 30 分钟限时探秘副本",
                        "spawn" or "召唤" => $"召唤卧龙守将【{extra}】",
                        "search" or "摸尸" or "探索" => "消耗灵符搜寻名将遗体",
                        "treasure" or "宝藏" or "开箱" => "解开并开启卧龙火龙宝藏大爆",
                        "charm" or "灵符" => $"获取卧龙灵符 +{count} 张",
                        _ => "卧龙山庄五元探秘与宝藏进度查询"
                    }
                };
            }

            case "wine":
            case "酒":
            case "酿酒":
            case "饮酒":
            {
                string sub = parts.Length >= 2 ? parts[1].ToLowerInvariant() : "status";
                string extra = parts.Length >= 3 ? parts[2].Trim() : "";
                int count = 0;
                if (parts.Length >= 3 && int.TryParse(parts[2], out int c))
                {
                    count = c;
                }

                return new ParsedCommand
                {
                    Type = ParsedCommandType.Wine,
                    SubAction = sub,
                    TargetName = extra,
                    Amount = count,
                    RawInput = input,
                    Message = sub switch
                    {
                        "brew" or "酿造" => $"开坛酿造美酒【{extra}】",
                        "drink" or "喝" => $"开坛痛饮美酒【{extra}】",
                        "spring" or "涌泉" => "探索并汲取地下天然涌泉",
                        "water" or "泉水" => $"获取天泉水 +{count} 坛",
                        _ => "盟重酒馆酿酒与药力醉酒度状态查询"
                    }
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
                              "  @wine [brew/drink/spring/status]      - 盟重酒馆泉水酿酒与药力醉酒度\n" +
                              "  @wolong [enter/spawn/search/treasure] - 卧龙山庄五元守护者与火龙宝藏\n" +
                              "  @internal [lvl/force/exp/full]- 内功等级与内力护盾调试\n" +
                              "  @combo [cast/unlock/full]     - 释放本职业多段连击绝学 (Ctrl+D / C)\n" +
                              "  @mount [ride/down/switch 名驹] - 战马坐骑上下马与切换 (Ctrl+M / M)\n" +
                              "  @charge                       - 施展战马冲锋破军神技 (Ctrl+F)\n" +
                              "  @boss spawn/kill/status       - 世界教主召唤/处决大爆/伤害战况\n" +
                              "  @drop <物品名> [极品点数] - 调试生成极品神装大爆\n" +
                              "  @make <物品名> [数量] - 刷取神装或药水 (如 @make 裁决之杖, @make 强效太阳水)\n" +
                              "  @gold <数值>          - 充值随身金币 (如 @gold 100000)\n" +
                              "  @mob <怪物名>         - 眼前刷怪 (如 @mob 白野猪)\n" +
                              "  @move <X> <Y>         - 瞬移到指定坐标 (如 @move 330 268)\n" +
                              "  @map <地图名/ID> [X Y]- 跨地图自由穿梭 (如 @map 兽人古墓, @map 3)\n" +
                              "  @protect <on/off> [HP%]- 智能生命守护/秒喝药设置 (如 @protect on 40)\n" +
                              "  @group add <名> [级] [职业] - 模拟添加队友 (如 @group add 烈火法神 38 法师)\n" +
                              "  @group leave          - 退出/解散当前队伍\n" +
                              "  @guild create <行会名>- 创立行会 (如 @guild create 傲视天下)\n" +
                              "  @guild rank <职务>    - 变更职务 (如 @guild rank 堂主)\n" +
                              "  @guild add <名> [级] [职] [阶] - 招募帮众 (如 @guild add 霸天虎 40 战士 堂主)\n" +
                              "  @master <师父名>      - 拜入名师门下 (需<=28级)\n" +
                              "  @apprentice <徒弟名> [级] [职] - 收录门徒 (需>=35级或出师)\n" +
                              "  @graduate [徒弟名]    - 举行武馆出师典礼 (徒弟>=35级)\n" +
                              "  @friend <add/del/list> <名> - 好友管理 (快捷键 F 打开社交面板)\n" +
                              "  @reputation [add <值>]- 查询或增加江湖声望\n" +
                              "  @badge exchange <编号>- 声望兑换荣誉勋章 (如 @badge exchange 43)\n" +
                              "  @badge reforge        - 消耗声望重铸佩戴勋章觉醒极品属性\n" +
                              "  @hero summon/recall/mode - 英雄随从召唤/收回/模式切换 (Ctrl+W / Ctrl+E)\n" +
                              "  @joint                - 释放主副心神合一震撼合击绝技 (Ctrl+S)\n" +
                              "  @fury <0~100>         - 充盈或调整真气怒气蓄力槽\n" +
                              "  @stall on <招牌名>    - 在主城安全区支起摊位摆摊\n" +
                              "  @stall off            - 收摊并取回未售商品\n" +
                              "  @stall add <物品> <价>- 将背包物品上架至摊位货架\n" +
                              "  @stall buy <槽位编号> - 购买货架上的指定商品\n" +
                              "  @stall shout          - 发起高亮公屏摆摊叫卖喊话\n" +
                              "  @siege start [秒数]   - 发起沙巴克攻城战 (如 @siege start 600)\n" +
                              "  @siege stop           - 终局结算沙巴克新霸主加冕\n" +
                              "  @siege palace [行会]  - 突入皇宫大殿抢占旗帜据点\n" +
                              "  @siege gate <伤害>    - 攻击破坏沙巴克城门\n" +
                              "  @repair [all/special] - 一键普通修理或特殊修理全身装备\n" +
                              "  @upgrade [纯度] [首饰]- 铁匠铺升级淬炼当前手持武器\n" +
                              "  @dura <数值>          - 修改手持武器当前持久度 (如 @dura 0 测试破损)\n" +
                              "  @kill                 - 角色自尽测试黑白死亡屏\n" +
                              "  @revive               - 原地满血满状态神圣复活\n" +
                              "  @level <等级>         - 调整等级 (如 @level 35)\n" +
                              "  @exp <数值>           - 注入经验值 (如 @exp 10000)\n" +
                              "  @cd                   - 一键清空所有技能冷却\n" +
                              "  @mute                 - 开关静音\n" +
                              "  @sound <0~100>        - 调整音量\n" +
                              "  @play <音效名>        - 试听音效 (如 @play gold, @play thunder)\n" +
                              "  @full                 - 满血满魔满状态\n" +
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
            "太阳水" or "强效太阳水" => Item.CreateSunPotion(count),
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
