namespace MirClient.Protocol.Resources;

/// <summary>
/// 角色基础动作类型 (1:1 对齐 Delphi HumanActor.pas THumanAction)
/// </summary>
public enum HumanActionType
{
    Stand = 0,
    Walk = 1,
    Run = 2
}

/// <summary>
/// 盛大十周年原生角色动作帧与外观计算器
/// </summary>
public static class HumanAction
{
    public const int HUMANFRAME = 600;

    // 动作信息结构体
    public readonly record struct ActionInfo(int Start, int Frame, int Skip, int FTime);

    // 1:1 对齐 Delphi 原版 HA 常量定义
    public static readonly ActionInfo Stand = new(0, 4, 4, 200);   // 站立: 起始0, 4帧, 步长8, 200ms
    public static readonly ActionInfo Walk  = new(64, 6, 2, 90);    // 行走: 起始64, 6帧, 步长8, 90ms
    public static readonly ActionInfo Run   = new(128, 6, 2, 115);  // 奔跑: 起始128, 6帧, 步长8, 115ms

    public static ActionInfo GetActionInfo(HumanActionType action) => action switch
    {
        HumanActionType.Stand => Stand,
        HumanActionType.Walk => Walk,
        HumanActionType.Run => Run,
        _ => Stand
    };

    /// <summary>
    /// 根据动作类型、方向 (0..7)、步进帧和服装偏置计算 WZL/WIL 图库中的实际贴图帧索引
    /// </summary>
    public static int CalculateFrameIndex(HumanActionType action, int direction, int motionStep, int dress = 0)
    {
        ActionInfo info = GetActionInfo(action);
        int validDir = Math.Clamp(direction, 0, 7);
        int validStep = motionStep % info.Frame;
        if (validStep < 0) validStep += info.Frame;

        int bodyOffset = dress * HUMANFRAME;
        int actionDirBase = info.Start + validDir * (info.Frame + info.Skip);
        return bodyOffset + actionDirBase + validStep;
    }
}
