using System;
using MirClient.Protocol;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class SabukSiegeProtocolTests
{
    [Fact]
    public void Test_CreateGuildWarPacket_EncodesTargetGuild()
    {
        string targetGuild = "沙巴克霸主行会";
        var (header, payload) = SabukSiegeProtocolManager.CreateGuildWarPacket(targetGuild);

        Assert.Equal(ProtocolConsts.CM_GUILDWAR, header.Ident);
        string decoded = EDCode.DecodeText(payload);
        Assert.Equal("沙巴克霸主行会", decoded);
    }

    [Fact]
    public void Test_CreateCastleWithdrawGoldPacket_EncodesAmount()
    {
        int withdrawGold = 500000;
        var (header, _) = SabukSiegeProtocolManager.CreateCastleWithdrawGoldPacket(withdrawGold);

        Assert.Equal(ProtocolConsts.CM_CASTLEWITHDRAWGOLD, header.Ident);
        Assert.Equal(withdrawGold, header.Recog);
    }

    [Fact]
    public void Test_ParseWarStatus_ParsesPhaseAndRemainingSeconds()
    {
        var msgFighting = new DefaultMessage
        {
            Ident = ProtocolConsts.SM_WARSTATUS,
            Recog = 2, // Fighting
            Param = 480
        };
        var recordFighting = SabukSiegeProtocolManager.ParseWarStatus(msgFighting);
        Assert.Equal(SiegePhase.Fighting, recordFighting.Phase);
        Assert.Equal(480f, recordFighting.RemainingSeconds);

        var msgSettled = new DefaultMessage
        {
            Ident = ProtocolConsts.SM_WARSTATUS,
            Recog = 3, // Settled
            Param = 0
        };
        var recordSettled = SabukSiegeProtocolManager.ParseWarStatus(msgSettled);
        Assert.Equal(SiegePhase.Settled, recordSettled.Phase);
        Assert.Equal(0f, recordSettled.RemainingSeconds);
    }

    [Fact]
    public void Test_ParseWallStatus_ParsesDamageLevelAndHp()
    {
        var msg = new DefaultMessage
        {
            Ident = ProtocolConsts.SM_SABUK_WALLSTATUS,
            Recog = 2, // 严重破损
            Param = 45, // 45% HP
            Tag = 0    // 紧闭
        };

        var wall = SabukSiegeProtocolManager.ParseWallStatus(msg);

        Assert.Equal(2, wall.DamageLevel);
        Assert.Equal(45, wall.HpPercent);
        Assert.False(wall.IsOpen);
    }

    [Fact]
    public void Test_ParseCastleOwner_ParsesLordGuildAndMaster()
    {
        string raw = "昔日同盟/小虾米/2026-10-16/";
        var owner = SabukSiegeProtocolManager.ParseCastleOwner(raw);

        Assert.Equal("昔日同盟", owner.GuildName);
        Assert.Equal("小虾米", owner.MasterName);
        Assert.Equal("2026-10-16", owner.OccupyDate);
    }

    [Fact]
    public void Test_SabukSiegeManager_SyncServerWarStatus()
    {
        SabukSiegeManager.Instance.Reset();

        SabukSiegeManager.Instance.SyncServerWarStatus(SiegePhase.Fighting, 300f);

        Assert.Equal(SiegePhase.Fighting, SabukSiegeManager.Instance.CurrentPhase);
        Assert.Equal(300f, SabukSiegeManager.Instance.RemainingBattleTime);
    }

    [Fact]
    public void Test_SabukSiegeManager_SyncServerWallStatus()
    {
        SabukSiegeManager.Instance.Reset();

        SabukSiegeManager.Instance.SyncServerWallStatus(1, 80, true);

        Assert.Equal(1, SabukSiegeManager.Instance.GateDamageLevel);
        Assert.True(SabukSiegeManager.Instance.IsGateOpen);
        Assert.Equal(40000, SabukSiegeManager.Instance.GateCurrentHp); // 50000 * 80% = 40000
    }

    [Fact]
    public void Test_SabukSiegeManager_SyncServerCastleGold()
    {
        SabukSiegeManager.Instance.Reset();

        SabukSiegeManager.Instance.SyncServerCastleGold(8888888);

        Assert.Equal(8888888, SabukSiegeManager.Instance.CastleGold);
    }
}
