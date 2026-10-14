using System.Numerics;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class MapManagerTests
{
    [Fact]
    public void TestMapManager_InitializationAndLookup()
    {
        MapManager mgr = new();
        var maps = mgr.GetAllMaps();
        Assert.True(maps.Count >= 4);

        // 验证默认活动地图
        Assert.Equal("0", mgr.ActiveMapId);
        Assert.Equal("比奇省", mgr.ActiveMap.Name);

        // 精确与模糊查找
        var biqi = mgr.FindMap("0");
        Assert.NotNull(biqi);
        Assert.Equal("比奇省", biqi.Name);

        var tomb = mgr.FindMap("D021");
        Assert.NotNull(tomb);
        Assert.Equal("兽人古墓", tomb.Name);

        var tombByName = mgr.FindMap("骷髅洞");
        Assert.NotNull(tombByName);
        Assert.Equal("D021", tombByName.Id);

        var snake = mgr.FindMap("毒蛇山谷");
        Assert.NotNull(snake);
        Assert.Equal("2", snake.Id);

        var desert = mgr.FindMap("盟重省");
        Assert.NotNull(desert);
        Assert.Equal("3", desert.Id);

        var desertByShort = mgr.FindMap("土城");
        Assert.NotNull(desertByShort);
        Assert.Equal("3", desertByShort.Id);
    }

    [Fact]
    public void TestMapManager_GatewaysRegistration()
    {
        MapManager mgr = new();
        var biqiGates = mgr.GetGateways("0");
        Assert.True(biqiGates.Count >= 2); // 至少包含去古墓与去毒蛇山谷

        var tombGates = mgr.GetGateways("D021");
        Assert.True(tombGates.Count >= 1); // 至少包含回比奇
        Assert.Equal("0", tombGates[0].TargetMapId);
    }

    [Fact]
    public void TestMapManager_CheckGateway_Trigger()
    {
        MapManager mgr = new();
        // 比奇 -> 古墓的传送门位于 (338, 275)
        Vector2 onGatePos = new(338f, 275f);
        bool hit = mgr.CheckGateway("0", onGatePos, out var gw);
        Assert.True(hit);
        Assert.NotNull(gw);
        Assert.Equal("D021", gw.TargetMapId);

        // 稍微偏离 0.5 格依然在 1.25f 容差范围内
        Vector2 nearGatePos = new(338.4f, 275.2f);
        Assert.True(mgr.CheckGateway("0", nearGatePos, out _));

        // 距离 5 格则不会触发
        Vector2 farPos = new(330f, 268f);
        Assert.False(mgr.CheckGateway("0", farPos, out _));
    }

    [Fact]
    public void TestMapManager_SwitchMap_SuccessAndEvent()
    {
        MapManager mgr = new();
        bool eventFired = false;
        MapDefinition? eventMap = null;
        Vector2 eventSpawn = Vector2.Zero;

        mgr.OnMapChanged = (map, spawn) =>
        {
            eventFired = true;
            eventMap = map;
            eventSpawn = spawn;
        };

        bool ok = mgr.SwitchMap("D021", new Vector2(185, 205), out var spawnPos, out var err);
        Assert.True(ok);
        Assert.Empty(err);
        Assert.Equal("D021", mgr.ActiveMapId);
        Assert.Equal(new Vector2(185, 205), spawnPos);

        Assert.True(eventFired);
        Assert.NotNull(eventMap);
        Assert.Equal("兽人古墓", eventMap.Name);
        Assert.Equal(new Vector2(185, 205), eventSpawn);
    }

    [Fact]
    public void TestMapManager_SwitchMap_NotFound()
    {
        MapManager mgr = new();
        bool ok = mgr.SwitchMap("NonExistentMap_999", null, out _, out var err);
        Assert.False(ok);
        Assert.Contains("未找到地图", err);
    }

    [Fact]
    public void TestCommandParser_MapCommand()
    {
        // 测试 @map 兽人古墓
        var cmd1 = CommandParser.Parse("@map 兽人古墓");
        Assert.Equal(ParsedCommandType.Map, cmd1.Type);
        Assert.Equal("兽人古墓", cmd1.TargetName);
        Assert.Equal(Vector2.Zero, cmd1.TargetTile);

        // 测试 @map 3 330 330 (带目标坐标)
        var cmd2 = CommandParser.Parse("@map 3 330 330");
        Assert.Equal(ParsedCommandType.Map, cmd2.Type);
        Assert.Equal("3", cmd2.TargetName);
        Assert.Equal(new Vector2(330, 330), cmd2.TargetTile);

        // 测试格式错误
        var cmd3 = CommandParser.Parse("@map");
        Assert.Equal(ParsedCommandType.Unknown, cmd3.Type);
    }
}
