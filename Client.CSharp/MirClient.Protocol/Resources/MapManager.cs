using System.Numerics;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 地图元数据定义
/// </summary>
public record MapDefinition(
    string Id,
    string Name,
    string FileName,
    Vector2 DefaultSpawnTile,
    string Ambience = "",
    int Width = 800,
    int Height = 600
);

/// <summary>
/// 传送门（网关）定义
/// </summary>
public record MapGateway(
    string Id,
    string DisplayName,
    string SourceMapId,
    Vector2 SourceTile,
    string TargetMapId,
    Vector2 TargetTile,
    float TriggerRadius = 1.25f
);

/// <summary>
/// 多地图生命周期与传送门管理器 (纯 C# 领域状态机)
/// </summary>
public class MapManager
{
    private static MapManager? _instance;
    public static MapManager Instance => _instance ??= new MapManager();

    private readonly Dictionary<string, MapDefinition> _maps = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<MapGateway> _gateways = new();

    public string ActiveMapId { get; private set; } = "0";
    public MapDefinition ActiveMap => _maps.TryGetValue(ActiveMapId, out var m) ? m : _maps["0"];

    /// <summary>
    /// 地图切换事件委托：(新地图定义, 目标落点坐标)
    /// </summary>
    public Action<MapDefinition, Vector2>? OnMapChanged { get; set; }

    public MapManager()
    {
        InitializeDefaultMaps();
    }

    private void InitializeDefaultMaps()
    {
        _maps.Clear();
        _gateways.Clear();

        // 1. 注册盛大经典四大地图
        RegisterMap(new MapDefinition("0", "比奇省", "0.map", new Vector2(330, 268), "风吹绿草，王城繁华", 800, 600));
        RegisterMap(new MapDefinition("D021", "兽人古墓", "D021.map", new Vector2(180, 200), "幽暗石室，骷髅咆哮", 400, 400));
        RegisterMap(new MapDefinition("2", "毒蛇山谷", "2.map", new Vector2(280, 180), "荒野深谷，毒雾弥漫", 500, 500));
        RegisterMap(new MapDefinition("3", "盟重省", "3.map", new Vector2(330, 330), "大漠孤烟，黄沙漫天", 800, 800));

        // 2. 注册双向空间传送门
        // 比奇省 <--> 兽人古墓 (骷髅洞)
        RegisterGateway(new MapGateway(
            "gw_biqi_to_tomb", "古墓幽径传送门", "0", new Vector2(338, 275), "D021", new Vector2(180, 200)
        ));
        RegisterGateway(new MapGateway(
            "gw_tomb_to_biqi", "返回比奇省传送门", "D021", new Vector2(178, 198), "0", new Vector2(336, 273)
        ));

        // 比奇省 <--> 毒蛇山谷
        RegisterGateway(new MapGateway(
            "gw_biqi_to_snake", "毒蛇山谷关隘", "0", new Vector2(345, 265), "2", new Vector2(280, 180)
        ));
        RegisterGateway(new MapGateway(
            "gw_snake_to_biqi", "返回比奇省关隘", "2", new Vector2(278, 178), "0", new Vector2(343, 265)
        ));

        // 毒蛇山谷 <--> 盟重省 (土城)
        RegisterGateway(new MapGateway(
            "gw_snake_to_mengzhong", "前往盟重土城通道", "2", new Vector2(285, 185), "3", new Vector2(330, 330)
        ));
        RegisterGateway(new MapGateway(
            "gw_mengzhong_to_snake", "返回毒蛇山谷通道", "3", new Vector2(328, 328), "2", new Vector2(283, 183)
        ));
    }

    public void RegisterMap(MapDefinition def)
    {
        _maps[def.Id] = def;
    }

    public void RegisterGateway(MapGateway gateway)
    {
        _gateways.Add(gateway);
    }

    public IReadOnlyCollection<MapDefinition> GetAllMaps() => _maps.Values;

    public IReadOnlyList<MapGateway> GetGateways(string mapId)
    {
        return _gateways.Where(g => string.Equals(g.SourceMapId, mapId, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>
    /// 模糊查找地图 (支持按 ID 或名称)
    /// </summary>
    public MapDefinition? FindMap(string idOrName)
    {
        if (string.IsNullOrWhiteSpace(idOrName)) return null;

        idOrName = idOrName.Trim();
        if (_maps.TryGetValue(idOrName, out var exact))
        {
            return exact;
        }

        // 模糊匹配别名
        foreach (var map in _maps.Values)
        {
            if (map.Name.Contains(idOrName, StringComparison.OrdinalIgnoreCase) ||
                idOrName.Contains(map.Name, StringComparison.OrdinalIgnoreCase))
            {
                return map;
            }
        }

        // 常见简称兼容
        if (idOrName.Contains("古墓") || idOrName.Contains("骷髅洞") || idOrName.Equals("tomb", StringComparison.OrdinalIgnoreCase))
            return _maps["D021"];
        if (idOrName.Contains("毒蛇") || idOrName.Contains("山谷") || idOrName.Equals("snake", StringComparison.OrdinalIgnoreCase))
            return _maps["2"];
        if (idOrName.Contains("盟重") || idOrName.Contains("土城") || idOrName.Equals("desert", StringComparison.OrdinalIgnoreCase))
            return _maps["3"];
        if (idOrName.Contains("比奇") || idOrName.Equals("biqi", StringComparison.OrdinalIgnoreCase))
            return _maps["0"];

        return null;
    }

    /// <summary>
    /// 检查玩家当前坐标是否踩中传送门
    /// </summary>
    public bool CheckGateway(string currentMapId, Vector2 playerTilePos, out MapGateway? hitGateway)
    {
        hitGateway = null;
        var list = GetGateways(currentMapId);
        foreach (var gw in list)
        {
            if (Vector2.Distance(playerTilePos, gw.SourceTile) <= gw.TriggerRadius)
            {
                hitGateway = gw;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 执行地图切换
    /// </summary>
    public bool SwitchMap(string targetMapIdOrName, Vector2? targetTile, out Vector2 spawnPos, out string error)
    {
        spawnPos = Vector2.Zero;
        error = "";

        var map = FindMap(targetMapIdOrName);
        if (map == null)
        {
            error = $"未找到地图【{targetMapIdOrName}】！";
            return false;
        }

        ActiveMapId = map.Id;
        spawnPos = targetTile ?? map.DefaultSpawnTile;

        try
        {
            OnMapChanged?.Invoke(map, spawnPos);
        }
        catch
        {
            // 隔离异常
        }

        return true;
    }

    public bool SwitchMap(string targetMapIdOrName, out Vector2 spawnPos, out string error) =>
        SwitchMap(targetMapIdOrName, null, out spawnPos, out error);

    /// <summary>
    /// 重置为初始比奇省状态
    /// </summary>
    public void Reset()
    {
        ActiveMapId = "0";
    }
}
