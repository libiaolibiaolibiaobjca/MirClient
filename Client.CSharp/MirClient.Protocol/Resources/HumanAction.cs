namespace MirClient.Protocol.Resources;

/// <summary>
/// 角色基础动作类型 (1:1 对齐 Delphi HumanActor.pas THumanAction)
/// </summary>
public enum HumanActionType
{
    Stand = 0,      // 站立待机 (4帧, skip 4, ftime 200ms)
    Walk = 1,       // 正常行走 (6帧, skip 2, ftime 90ms)
    Run = 2,        // 疾跑奔跑 (6帧, skip 2, ftime 115ms)
    WarMode = 3,    // 战斗备战 (1帧, skip 0, ftime 200ms)
    Attack = 4,     // 普通挥砍攻击 (6帧, skip 2, ftime 85ms) - 原版 ActHit
    HeavyHit = 5,   // 攻杀重击 (6帧, skip 2, ftime 90ms)
    BigHit = 6,     // 刺杀半月 (8帧, skip 0, ftime 70ms)
    Spell = 7,      // 施法召唤 (6帧, skip 2, ftime 60ms)
    Sitdown = 8,    // 打坐 (2帧, skip 0, ftime 300ms)
    Struck = 9,     // 受击硬直 (3帧, skip 5, ftime 70ms)
    Die = 10        // 倒地死亡 (4帧, skip 4, ftime 120ms)
}

/// <summary>
/// 盛大十周年原生角色动作帧、外观复合偏移与前后遮挡层级计算器
/// </summary>
public static class HumanAction
{
    public const int HUMANFRAME = 600;

    // 动作信息结构体
    public readonly record struct ActionInfo(int Start, int Frame, int Skip, int FTime);

    // 1:1 对齐 Delphi 原版 HA 常量定义
    public static readonly ActionInfo Stand    = new(0, 4, 4, 200);   // 站立: 起始0, 4帧, 步长8, 200ms
    public static readonly ActionInfo Walk     = new(64, 6, 2, 90);    // 行走: 起始64, 6帧, 步长8, 90ms
    public static readonly ActionInfo Run      = new(128, 6, 2, 115);  // 奔跑: 起始128, 6帧, 步长8, 115ms
    public static readonly ActionInfo WarMode  = new(192, 1, 0, 200);  // 战斗姿态: 起始192, 1帧, 步长1, 200ms
    public static readonly ActionInfo Attack   = new(200, 6, 2, 85);   // 普通攻击: 起始200, 6帧, 步长8, 85ms (ActHit)
    public static readonly ActionInfo HeavyHit = new(264, 6, 2, 90);   // 攻杀重击: 起始264, 6帧, 步长8, 90ms
    public static readonly ActionInfo BigHit   = new(328, 8, 0, 70);   // 刺杀半月: 起始328, 8帧, 步长8, 70ms
    public static readonly ActionInfo Spell    = new(392, 6, 2, 60);   // 施法召唤: 起始392, 6帧, 步长8, 60ms
    public static readonly ActionInfo Sitdown  = new(456, 2, 0, 300);  // 打坐静坐: 起始456, 2帧, 步长2, 300ms
    public static readonly ActionInfo Struck   = new(472, 3, 5, 70);   // 受击硬直: 起始472, 3帧, 步长8, 70ms
    public static readonly ActionInfo Die      = new(536, 4, 4, 120);  // 倒地死亡: 起始536, 4帧, 步长8, 120ms

    // 盛大原版 1:1 武器/身体前后遮挡关系表 (0: 武器在身体后方绘制; 1: 武器在身体前方绘制)
    // 索引: [性别 0=男 1=女, 基础动作帧 0..599]
    private static readonly byte[][] WORDER = new byte[][]
    {
        new byte[] {
0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 1, 1, 1, 1, 1, 1, 0, 0, 1, 1, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 0, 1, 1, 0, 0, 0, 1, 1, 0, 1, 1, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1, 0, 1, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1, 0, 0, 1, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 1, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 1, 1, 1, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 1, 1, 1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 1
        },
        new byte[] {
0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 1, 1, 1, 1, 1, 1, 0, 0, 1, 1, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 0, 1, 1, 0, 0, 0, 1, 1, 0, 1, 1, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1, 0, 1, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1, 0, 0, 1, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 1, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 1, 1, 1, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 1, 1, 1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 1
        }
    };

    public static ActionInfo GetActionInfo(HumanActionType action) => action switch
    {
        HumanActionType.Stand => Stand,
        HumanActionType.Walk => Walk,
        HumanActionType.Run => Run,
        HumanActionType.WarMode => WarMode,
        HumanActionType.Attack => Attack,
        HumanActionType.HeavyHit => HeavyHit,
        HumanActionType.BigHit => BigHit,
        HumanActionType.Spell => Spell,
        HumanActionType.Sitdown => Sitdown,
        HumanActionType.Struck => Struck,
        HumanActionType.Die => Die,
        _ => Stand
    };

    /// <summary>
    /// 计算 0..599 范围内的基础动作贴图帧 (每个动作集按 8 方向展开)
    /// </summary>
    public static int CalculateBaseFrame(HumanActionType action, int direction, int motionStep)
    {
        ActionInfo info = GetActionInfo(action);
        int validDir = Math.Clamp(direction, 0, 7);
        int validStep = motionStep % info.Frame;
        if (validStep < 0) validStep += info.Frame;

        int actionDirBase = info.Start + validDir * (info.Frame + info.Skip);
        return actionDirBase + validStep;
    }

    /// <summary>
    /// 根据动作类型、方向 (0..7)、步进帧和服装偏置计算衣服/身体实际贴图帧索引 (Data/Hum)
    /// </summary>
    public static int CalculateFrameIndex(HumanActionType action, int direction, int motionStep, int dress = 0)
    {
        int baseFrame = CalculateBaseFrame(action, direction, motionStep);
        return dress * HUMANFRAME + baseFrame;
    }

    /// <summary>
    /// 计算发型实际贴图帧索引 (Data/Hair)
    /// 1:1 对齐 Delphi HumanActor.pas: m_nHairOffset := HUMANFRAME * (m_btHair + m_btSex)
    /// </summary>
    /// <param name="hairId">发型ID (0:光头无发型, 1:经典短发, 2:长发等)</param>
    /// <param name="sex">性别 (0:男, 1:女)</param>
    /// <returns>贴图帧索引，若小于0表示无需绘制发型</returns>
    public static int CalculateHairFrameIndex(HumanActionType action, int direction, int motionStep, int hairId, int sex)
    {
        if (hairId <= 0) return -1;
        int baseFrame = CalculateBaseFrame(action, direction, motionStep);
        int hairOffset = HUMANFRAME * (hairId * 2 + (sex & 1));
        return hairOffset + baseFrame;
    }

    /// <summary>
    /// 计算武器实际贴图帧索引 (Data/Weapon)
    /// 1:1 对齐 Delphi ClMain.pas: HUMANFRAME * Weapon + nFrame
    /// </summary>
    /// <param name="weaponId">武器外观ID (0:空手, 1:木剑, 2:乌木剑, 3:青铜剑...)</param>
    /// <param name="sex">性别 (0:男, 1:女)</param>
    /// <returns>贴图帧索引，若小于0表示空手无需绘制</returns>
    public static int CalculateWeaponFrameIndex(HumanActionType action, int direction, int motionStep, int weaponId, int sex)
    {
        if (weaponId <= 0) return -1;
        int baseFrame = CalculateBaseFrame(action, direction, motionStep);
        int weaponOffset = HUMANFRAME * weaponId;
        return weaponOffset + baseFrame;
    }

    /// <summary>
    /// 获取指定帧下武器与身体的前后遮挡层级关系
    /// </summary>
    /// <param name="sex">性别 (0:男, 1:女)</param>
    /// <param name="baseFrame">基础动作帧 (0..599)</param>
    /// <returns>0: 武器在身体后方绘制; 1: 武器在身体前方绘制</returns>
    public static int GetWeaponOrder(int sex, int baseFrame)
    {
        int validSex = (sex & 1);
        int validFrame = Math.Clamp(baseFrame, 0, 599);
        return WORDER[validSex][validFrame];
    }
}
