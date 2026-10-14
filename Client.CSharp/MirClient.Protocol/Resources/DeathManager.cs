using System.Numerics;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 玩家阵亡历史记录
/// </summary>
public record DeathRecord(Vector2 Tile, string MapId, string MapName, double DeathTime);

/// <summary>
/// 盛大传奇十周年：玩家死亡状态机与安全区复活管理器 (纯 C# 领域状态机)
/// </summary>
public class DeathManager
{
    private static DeathManager? _instance;
    public static DeathManager Instance => _instance ??= new DeathManager();

    // 默认主城安全区复活坐标 (比奇城银杏山谷/比奇大城经典安全区)
    public const string DEFAULT_SAFE_MAP_ID = "0";
    public const string DEFAULT_SAFE_MAP_NAME = "比奇城";
    public static readonly Vector2 DEFAULT_SAFE_TILE = new(330, 268);

    // 状态定义
    public bool IsDead { get; private set; } = false;
    public double LastDeathTime { get; private set; } = 0.0;
    public Vector2 LastDeathTile { get; private set; } = Vector2.Zero;
    public string LastDeathMapId { get; private set; } = "";
    public string LastDeathMapName { get; private set; } = "";
    public int DeathCount { get; private set; } = 0;

    // 阵亡后防抖保护时钟 (避免刚死瞬间误按回车键闪退回城)
    public double MinRespawnDelaySec { get; set; } = 1.0;

    /// <summary>
    /// 触发玩家死亡
    /// </summary>
    public bool TriggerDeath(Vector2 tile, string mapId, string mapName, double currentTime)
    {
        if (IsDead) return false;

        IsDead = true;
        LastDeathTime = currentTime;
        LastDeathTile = tile;
        LastDeathMapId = mapId;
        LastDeathMapName = mapName;
        DeathCount++;
        return true;
    }

    /// <summary>
    /// 检查是否已达到最小复活等待延迟
    /// </summary>
    public bool CanRespawn(double currentTime)
    {
        if (!IsDead) return false;
        return (currentTime - LastDeathTime) >= MinRespawnDelaySec;
    }

    /// <summary>
    /// 申请安全区回城复活：计算目标主城安全区并重置死亡状态
    /// </summary>
    public bool RespawnAtSafeZone(out string targetMapId, out string targetMapName, out Vector2 targetTile)
    {
        if (!IsDead)
        {
            targetMapId = "";
            targetMapName = "";
            targetTile = Vector2.Zero;
            return false;
        }

        IsDead = false;
        targetMapId = DEFAULT_SAFE_MAP_ID;
        targetMapName = DEFAULT_SAFE_MAP_NAME;
        targetTile = DEFAULT_SAFE_TILE;
        return true;
    }

    /// <summary>
    /// 原地神圣复活 (GM 调试或特权指令)
    /// </summary>
    public bool ReviveInPlace()
    {
        if (!IsDead) return false;
        IsDead = false;
        return true;
    }

    /// <summary>
    /// 重置状态（测试或重新登入时使用）
    /// </summary>
    public void Reset()
    {
        IsDead = false;
        LastDeathTime = 0.0;
        LastDeathTile = Vector2.Zero;
        LastDeathMapId = "";
        LastDeathMapName = "";
        DeathCount = 0;
    }
}
