using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class SoundTests
{
    [Fact]
    public void TestVolume_ClampAndNormalize()
    {
        SoundManager mgr = new();
        Assert.Equal(80, mgr.Volume);
        Assert.Equal(0.8f, mgr.VolumeNormalized, precision: 2);

        mgr.Volume = 150;
        Assert.Equal(100, mgr.Volume);
        Assert.Equal(1.0f, mgr.VolumeNormalized);

        mgr.Volume = -30;
        Assert.Equal(0, mgr.Volume);
        Assert.Equal(0.0f, mgr.VolumeNormalized);
    }

    [Fact]
    public void TestMute_ToggleAndSilence()
    {
        SoundManager mgr = new();
        Assert.False(mgr.IsMuted);

        mgr.ToggleMute();
        Assert.True(mgr.IsMuted);
        Assert.Equal(0f, mgr.VolumeNormalized);

        // 静音状态下尝试播放，计数增加但底层不分发
        bool dispatched = false;
        mgr.OnPlaySound = (_, _) => dispatched = true;
        mgr.Play(SoundEffectType.Thunder);

        Assert.False(dispatched);
        Assert.Equal(1, mgr.TotalPlayCount);
        Assert.Equal(SoundEffectType.Thunder, mgr.LastPlayedSound);

        // 解除静音
        mgr.ToggleMute();
        Assert.False(mgr.IsMuted);
        Assert.Equal(0.8f, mgr.VolumeNormalized, precision: 2);
    }

    [Fact]
    public void TestPlay_DispatchAndTrackStats()
    {
        SoundManager mgr = new();
        mgr.Volume = 50;

        SoundEffectType? playedType = null;
        float playedVol = 0f;
        mgr.OnPlaySound = (type, vol) =>
        {
            playedType = type;
            playedVol = vol;
        };

        mgr.Play(SoundEffectType.PickupGold, 0.8f);

        Assert.Equal(SoundEffectType.PickupGold, playedType);
        Assert.Equal(0.4f, playedVol, precision: 2); // 0.5 * 0.8 = 0.4
        Assert.Equal(1, mgr.TotalPlayCount);
        Assert.Equal(SoundEffectType.PickupGold, mgr.LastPlayedSound);
    }

    [Fact]
    public void TestMultipleSounds_LastPlayed()
    {
        SoundManager mgr = new();
        mgr.Play(SoundEffectType.AttackSwing);
        mgr.Play(SoundEffectType.MonsterStruck);
        mgr.Play(SoundEffectType.LevelUp);

        Assert.Equal(3, mgr.TotalPlayCount);
        Assert.Equal(SoundEffectType.LevelUp, mgr.LastPlayedSound);

        mgr.ResetStats();
        Assert.Equal(0, mgr.TotalPlayCount);
        Assert.Null(mgr.LastPlayedSound);
    }

    [Fact]
    public void TestExceptionSafety_DriverSuppression()
    {
        SoundManager mgr = new();
        // 模拟底层驱动抛出硬件音频异常
        mgr.OnPlaySound = (_, _) => throw new InvalidOperationException("Audio hardware device disconnected");

        // 验证上层依然绝对稳健，不向外抛出未处理异常
        var ex = Record.Exception(() => mgr.Play(SoundEffectType.FireSword));
        Assert.Null(ex);
        Assert.Equal(1, mgr.TotalPlayCount);
        Assert.Equal(SoundEffectType.FireSword, mgr.LastPlayedSound);
    }
}
