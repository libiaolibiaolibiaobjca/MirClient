using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 严格 1:1 对齐 OpenMir2 MagicConst.cs 的经典三职业核心技能编号
/// </summary>
public enum MagicType : byte
{
    // 经典 1~33 基础法术
    Fireball = 1,        // 火球术 (法)
    Healing = 2,         // 治愈术 (道)
    OneSword = 3,        // 基本剑术 (战)
    Ilkwang = 4,         // 精神力战法 (道)
    Fireball2 = 5,       // 大火球 (法)
    Amyounsul = 6,       // 施毒术 (道)
    Yedo = 7,            // 攻杀剑法 (战)
    FireWind = 8,        // 抗拒火环 (法)
    HellFire = 9,        // 地狱火 (法)
    LightningMovie = 10, // 疾光电影 (法)
    Thunder = 11,        // 雷电术 (法)
    Assassinate = 12,    // 刺杀剑术 (战)
    SoulCharm = 13,      // 灵魂火符 (道)
    Hangmajinbub = 14,   // 幽灵盾 (道)
    Dejiwonho = 15,      // 神圣战甲术 (道)
    HolyShield = 16,     // 困魔咒 (道)
    Skeleton = 17,       // 召唤骷髅 (道)
    Cloak = 18,          // 隐身术 (道)
    BigCloak = 19,       // 集体隐身术 (道)
    Tamming = 20,        // 诱惑之光 (法)
    Teleport = 21,       // 瞬息移动 (法)
    FireWall = 22,       // 火墙 (法)
    FireBoom = 23,       // 爆裂火焰 (法)
    LightFlower = 24,    // 地狱雷光 (法)
    HalfMoon = 25,       // 半月弯刀 (战)
    FireSword = 26,      // 烈火剑法 (战)
    Mootebo = 27,        // 野蛮冲撞 (战)
    ShowHp = 28,         // 心灵启示 (道)
    BigHealing = 29,     // 群体治疗术 (道)
    SummonShinshu = 30,  // 召唤神兽 (道)
    Shield = 31,         // 魔法盾 (法)
    KillUndead = 32,     // 圣言术 (法)
    IceRoar = 33,        // 冰咆哮 (法)

    // 经典进阶与新技能
    CrossMoon = 34,      // 疾风斩
    WindTebo = 35,       // 气波功
    UEnhancer = 36,      // 无极真气 (道)
    EnergyRepulsor = 37, // 气功波 (道)
    TwinBlade = 38,      // 双龙斩
    GroupDeDing = 39,    // 地钉
    UnAmyounsul = 40,    // 解毒术 (道)
    Angel = 41,          // 狮子吼 (战)
    GroupLightning = 42, // 群体雷电术
    OpenSky = 43,        // 开天斩 (战)
    IcePalm = 44,        // 寒冰掌 (法)
    MeteoFire = 45,      // 灭天火 (法)
    Clone = 46,          // 分身术
    DragonFire = 47,     // 火龙气焰 / 地狱烈焰 (法)
    GroupAmyounsul = 48, // 群体施毒术 (道)
    Purify = 49,         // 净化术 / 云寂术 (道)
    Mabe = 50,           // 冰焰
    MeteorFireStorm = 58,// 流星火雨 (法)
    BloodDrain = 59,     // 噬血术 (道)
    ShieldFour = 66,     // 四级魔法盾 (法)
    SummonMoonSpirit = 72,// 召唤月灵 (道)
    ChasingSun = 74,     // 逐日剑法 (战)
    HolyShieldDef = 75,  // 护体神盾
    LightningFour = 91   // 四级雷电术
}

/// <summary>
/// 严格 1:1 对齐 OpenMir2 Data.MagicInfo 的技能详细配置结构
/// </summary>
public class MagicInfoData
{
    public ushort MagicId { get; set; }
    public string MagicName { get; set; } = string.Empty;
    public byte EffectType { get; set; }
    public byte Effect { get; set; }
    public ushort Spell { get; set; }          // 施法所需 MP 消耗
    public ushort Power { get; set; }          // 基础伤害威力
    public byte[] TrainLevel { get; set; } = new byte[4]; // 0~3 级所需角色等级
    public int[] MaxTrain { get; set; } = new int[4];     // 0~3 级所需最大修炼点
    public byte TrainLv { get; set; }
    public byte Job { get; set; }              // 职业: 0-战士, 1-法师, 2-道士, 99-通用
    public int DelayTime { get; set; }         // 技能后摇冷却延时 (毫秒)
    public byte DefSpell { get; set; }
    public byte DefPower { get; set; }
    public ushort MaxPower { get; set; }
    public byte DefMaxPower { get; set; }
    public string Desc { get; set; } = string.Empty;

    public static MagicInfoData ReadFrom(BinaryReader reader)
    {
        var info = new MagicInfoData();
        info.MagicId = reader.ReadUInt16();

        byte[] nameBytes = reader.ReadBytes(14);
        info.MagicName = DecodeAsciiOrGbk(nameBytes);

        info.EffectType = reader.ReadByte();
        info.Effect = reader.ReadByte();
        _ = reader.ReadByte(); // padding

        info.Spell = reader.ReadUInt16();
        info.Power = reader.ReadUInt16();

        for (int i = 0; i < 4; i++) info.TrainLevel[i] = reader.ReadByte();
        for (int i = 0; i < 4; i++) info.MaxTrain[i] = reader.ReadInt32();

        info.TrainLv = reader.ReadByte();
        info.Job = reader.ReadByte();
        _ = reader.ReadByte(); // padding
        _ = reader.ReadByte(); // padding

        info.DelayTime = reader.ReadInt32();
        info.DefSpell = reader.ReadByte();
        info.DefPower = reader.ReadByte();
        info.MaxPower = reader.ReadUInt16();
        info.DefMaxPower = reader.ReadByte();

        byte[] descBytes = reader.ReadBytes(15);
        info.Desc = DecodeAsciiOrGbk(descBytes);

        _ = reader.ReadByte(); // padding
        _ = reader.ReadByte(); // padding
        _ = reader.ReadByte(); // padding

        return info;
    }

    private static string DecodeAsciiOrGbk(byte[] bytes)
    {
        int len = 0;
        while (len < bytes.Length && bytes[len] != 0) len++;
        if (len == 0) return string.Empty;

        try
        {
            Encoding gbk = Encoding.GetEncoding("GB18030");
            return gbk.GetString(bytes, 0, len).Trim();
        }
        catch
        {
            return Encoding.UTF8.GetString(bytes, 0, len).Trim();
        }
    }
}

/// <summary>
/// 严格 1:1 对齐 OpenMir2 Packets.ClientPackets.ClientMagic 结构
/// 每个角色掌握的技能实例 (包含快捷键与当前修炼度)
/// </summary>
public class ClientMagicData
{
    public char Key { get; set; }         // 绑定的快捷键位 (如 '0'~'7' 对应 F1~F8, 0 代表无)
    public byte Level { get; set; }       // 当前技能修炼等级 (0~3 级)
    public int CurTrain { get; set; }     // 当前修炼熟练度点数
    public MagicInfoData Def { get; set; } = new();

    public static ClientMagicData ReadFrom(BinaryReader reader)
    {
        var magic = new ClientMagicData();
        magic.Key = (char)reader.ReadByte();
        magic.Level = reader.ReadByte();
        _ = reader.ReadByte(); // padding
        _ = reader.ReadByte(); // padding
        magic.CurTrain = reader.ReadInt32();
        magic.Def = MagicInfoData.ReadFrom(reader);
        return magic;
    }
}

/// <summary>
/// 服务端广播的实体施法事件 (SM_SPELL 17)
/// </summary>
public record SpellBroadcastEvent(
    long ActorId,
    int TargetX,
    int TargetY,
    int MagicId,
    string TargetInfo);

/// <summary>
/// 服务端下发的法术命中与伤害结算事件 (SM_MAGICFIRE 638)
/// </summary>
public record MagicFireEvent(
    long TargetId,
    int Damage,
    int MagicId);

/// <summary>
/// OpenMir2 技能法术学习与施法网络封包协议管理器
/// </summary>
public class MagicProtocolManager
{
    public static MagicProtocolManager Instance { get; } = new();

    private static readonly Dictionary<int, string> _magicNames = new()
    {
        { 1, "火球术" },
        { 2, "治愈术" },
        { 3, "基本剑术" },
        { 4, "精神力战法" },
        { 5, "大火球" },
        { 6, "施毒术" },
        { 7, "攻杀剑术" },
        { 8, "抗拒火环" },
        { 9, "地狱火" },
        { 10, "疾光电影" },
        { 11, "雷电术" },
        { 12, "刺杀剑术" },
        { 13, "灵魂火符" },
        { 14, "幽灵盾" },
        { 15, "神圣战甲术" },
        { 16, "困魔咒" },
        { 17, "召唤骷髅" },
        { 18, "隐身术" },
        { 19, "集体隐身术" },
        { 20, "诱惑之光" },
        { 21, "瞬息移动" },
        { 22, "火墙" },
        { 23, "爆裂火焰" },
        { 24, "地狱雷光" },
        { 25, "半月弯刀" },
        { 26, "烈火剑法" },
        { 27, "野蛮冲撞" },
        { 28, "心灵启示" },
        { 29, "群体治疗术" },
        { 30, "召唤神兽" },
        { 31, "魔法盾" },
        { 32, "圣言术" },
        { 33, "冰咆哮" },
        { 34, "疾风斩" },
        { 35, "气波功" },
        { 36, "无极真气" },
        { 37, "气功波" },
        { 38, "双龙斩" },
        { 39, "地钉" },
        { 40, "解毒术" },
        { 41, "狮子吼" },
        { 42, "群体雷电术" },
        { 43, "开天斩" },
        { 44, "寒冰掌" },
        { 45, "灭天火" },
        { 46, "分身术" },
        { 47, "火龙气焰" },
        { 48, "群体施毒术" },
        { 49, "云寂术" },
        { 50, "无极真气" },
        { 58, "流星火雨" },
        { 59, "噬血术" },
        { 66, "四级魔法盾" },
        { 72, "召唤月灵" },
        { 74, "逐日剑法" },
        { 75, "护体神盾" },
        { 91, "四级雷电术" }
    };

    /// <summary>
    /// 获取技能规范中文名称
    /// </summary>
    public static string GetMagicName(int magicId)
    {
        return _magicNames.TryGetValue(magicId, out var name) ? name : $"未知法术({magicId})";
    }

    /// <summary>
    /// 解析 SM_SENDMYMAGIC 下发的多技能列表数据包
    /// 服务端格式为每个 ClientMagic 结构体经 EDCode.EncodeBuffer 编码，并用 '/' 分隔
    /// </summary>
    public static List<ClientMagicData> ParseSendMyMagic(string payload)
    {
        var list = new List<ClientMagicData>();
        if (string.IsNullOrWhiteSpace(payload)) return list;

        string[] tokens = payload.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (string token in tokens)
        {
            if (string.IsNullOrWhiteSpace(token)) continue;
            try
            {
                byte[] raw = EDCode.DecodeString(token);
                if (raw != null && raw.Length >= 78)
                {
                    using var ms = new MemoryStream(raw);
                    using var br = new BinaryReader(ms);
                    var item = ClientMagicData.ReadFrom(br);
                    list.Add(item);
                }
            }
            catch
            {
                // 忽略异常包，保障客户端鲁棒性
            }
        }
        return list;
    }

    /// <summary>
    /// 解析 SM_ADDMAGIC 新学法术数据包
    /// </summary>
    public static ClientMagicData? ParseAddMagic(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return null;
        try
        {
            byte[] raw = EDCode.DecodeString(payload);
            if (raw != null && raw.Length >= 78)
            {
                using var ms = new MemoryStream(raw);
                using var br = new BinaryReader(ms);
                return ClientMagicData.ReadFrom(br);
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// 构建客户端主动施放法术封包 (CM_SPELL 3017)
    /// </summary>
    public static (DefaultMessage Msg, string Payload, string EncodedPacket) CreateSpellPacket(
        int magicId,
        int targetX,
        int targetY,
        int targetRecogId = 0)
    {
        int recog = (targetX & 0xFFFF) | ((targetY & 0xFFFF) << 16);
        ushort param = (ushort)(targetRecogId & 0xFFFF);
        ushort series = (ushort)((targetRecogId >> 16) & 0xFFFF);
        var msg = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_SPELL,
            Recog = recog,
            Param = param,
            Tag = (ushort)magicId,
            Series = series
        };

        string encodedHeader = EDCode.EncodeMessage(msg);
        string payload = string.Empty;
        string packet = $"#{encodedHeader}!";
        return (msg, payload, packet);
    }

    /// <summary>
    /// 构建设置/变更技能快捷键封包 (CM_MAGICKEYCHANGE 1008)
    /// 1:1 对齐 OpenMir2: Param=MagicId, Tag=(ushort)Key
    /// </summary>
    public static (DefaultMessage Msg, string Payload, string EncodedPacket) CreateMagicKeyChangePacket(
        ushort magicId,
        char key)
    {
        var msg = new DefaultMessage
        {
            Ident = ProtocolConsts.CM_MAGICKEYCHANGE,
            Recog = 0,
            Param = magicId,
            Tag = (ushort)key,
            Series = 0
        };

        string encodedHeader = EDCode.EncodeMessage(msg);
        string packet = $"#{encodedHeader}!";
        return (msg, string.Empty, packet);
    }

    /// <summary>
    /// 解析服务端下发的施法广播封包 (SM_SPELL 17)
    /// </summary>
    public static SpellBroadcastEvent ParseSpellBroadcast(DefaultMessage header, string rawBody)
    {
        long actorId = header.Recog;
        int targetX = header.Param;
        int targetY = header.Tag;
        int magicId = header.Series;
        string targetInfo = EDCode.DecodeText(rawBody);

        return new SpellBroadcastEvent(actorId, targetX, targetY, magicId, targetInfo);
    }

    /// <summary>
    /// 解析服务端下发的法术命中与伤害结算封包 (SM_MAGICFIRE 638)
    /// </summary>
    public static MagicFireEvent ParseMagicFire(DefaultMessage header)
    {
        long targetId = header.Recog;
        int damage = header.Param;
        int magicId = header.Tag;

        return new MagicFireEvent(targetId, damage, magicId);
    }
}
