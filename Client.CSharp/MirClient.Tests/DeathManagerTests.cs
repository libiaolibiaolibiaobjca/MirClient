using System.Numerics;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class DeathManagerTests
{
    [Fact]
    public void TestDeathManager_DefaultState()
    {
        DeathManager dm = new();
        Assert.False(dm.IsDead);
        Assert.Equal(0, dm.DeathCount);
        Assert.Equal(Vector2.Zero, dm.LastDeathTile);
    }

    [Fact]
    public void TestDeathManager_TriggerDeath()
    {
        DeathManager dm = new();
        Vector2 deathPos = new(150, 200);

        bool success = dm.TriggerDeath(deathPos, "1", "沃玛寺庙一层", 100.0);
        Assert.True(success);
        Assert.True(dm.IsDead);
        Assert.Equal(1, dm.DeathCount);
        Assert.Equal(deathPos, dm.LastDeathTile);
        Assert.Equal("1", dm.LastDeathMapId);
        Assert.Equal("沃玛寺庙一层", dm.LastDeathMapName);
        Assert.Equal(100.0, dm.LastDeathTime);

        // 已处于死亡状态，再次触发不重复叠加
        bool secondTrigger = dm.TriggerDeath(deathPos, "1", "沃玛寺庙一层", 100.5);
        Assert.False(secondTrigger);
        Assert.Equal(1, dm.DeathCount);
    }

    [Fact]
    public void TestDeathManager_CanRespawnDelay()
    {
        DeathManager dm = new();
        dm.MinRespawnDelaySec = 1.0;
        dm.TriggerDeath(new Vector2(100, 100), "0", "比奇城", 50.0);

        // 死亡 0.5 秒内，不可立即回城复活 (防误触防抖)
        Assert.False(dm.CanRespawn(50.5));

        // 死亡 1.2 秒后，允许回城复活
        Assert.True(dm.CanRespawn(51.2));
    }

    [Fact]
    public void TestDeathManager_RespawnAtSafeZone()
    {
        DeathManager dm = new();
        // 在骷髅洞 (地图 D001) 阵亡
        dm.TriggerDeath(new Vector2(88, 120), "D001", "天然洞穴一层", 20.0);
        Assert.True(dm.IsDead);

        // 回城复活
        bool respawned = dm.RespawnAtSafeZone(out string mapId, out string mapName, out Vector2 tile);
        Assert.True(respawned);
        Assert.False(dm.IsDead);

        // 验证正规回传至主城比奇城安全区
        Assert.Equal(DeathManager.DEFAULT_SAFE_MAP_ID, mapId);
        Assert.Equal(DeathManager.DEFAULT_SAFE_MAP_NAME, mapName);
        Assert.Equal(DeathManager.DEFAULT_SAFE_TILE, tile);
    }

    [Fact]
    public void TestDeathManager_ReviveInPlace()
    {
        DeathManager dm = new();
        dm.TriggerDeath(new Vector2(300, 300), "0", "比奇城", 10.0);
        Assert.True(dm.IsDead);

        bool revived = dm.ReviveInPlace();
        Assert.True(revived);
        Assert.False(dm.IsDead);
    }

    [Fact]
    public void TestCommandParser_KillAndReviveCommands()
    {
        var cmdKill = CommandParser.Parse("@kill");
        Assert.Equal(ParsedCommandType.Kill, cmdKill.Type);

        var cmdKillCn = CommandParser.Parse("@自尽");
        Assert.Equal(ParsedCommandType.Kill, cmdKillCn.Type);

        var cmdRevive = CommandParser.Parse("@revive");
        Assert.Equal(ParsedCommandType.Revive, cmdRevive.Type);

        var cmdReviveCn = CommandParser.Parse("@原地复活");
        Assert.Equal(ParsedCommandType.Revive, cmdReviveCn.Type);
    }
}
