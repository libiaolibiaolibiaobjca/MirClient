namespace MirClient.Protocol.Resources;

/// <summary>
/// 盛大热血传奇音频与音效核心管理器 (纯 C# 领域状态机，解耦底层硬件，跨平台无头安全)
/// </summary>
public class SoundManager
{
    private static SoundManager? _instance;
    public static SoundManager Instance => _instance ??= new SoundManager();

    private int _volume = 80; // 默认 80% 主音量
    private bool _isMuted = false;

    public int Volume
    {
        get => _volume;
        set => _volume = Math.Clamp(value, 0, 100);
    }

    public void SetVolume(int volume) => Volume = volume;

    public float VolumeNormalized => _isMuted ? 0f : (_volume / 100f);

    public bool IsMuted
    {
        get => _isMuted;
        set => _isMuted = value;
    }

    public long TotalPlayCount { get; private set; } = 0;
    public SoundEffectType? LastPlayedSound { get; private set; }

    /// <summary>
    /// 底层音频驱动委托：当触发音效播放时派发给硬件驱动层
    /// </summary>
    public Action<SoundEffectType, float>? OnPlaySound { get; set; }

    public void ToggleMute()
    {
        _isMuted = !_isMuted;
    }

    /// <summary>
    /// 触发指定音效播放
    /// </summary>
    /// <param name="type">音效类型</param>
    /// <param name="volumeScale">局部音量倍率 (0.0~1.0)</param>
    public void Play(SoundEffectType type, float volumeScale = 1.0f)
    {
        TotalPlayCount++;
        LastPlayedSound = type;

        if (_isMuted || _volume <= 0)
        {
            return;
        }

        float finalVolume = Math.Clamp(VolumeNormalized * volumeScale, 0f, 1f);
        try
        {
            OnPlaySound?.Invoke(type, finalVolume);
        }
        catch
        {
            // 优雅抑制任何由于硬件声卡丢失产生的底层异常
        }
    }

    public void ResetStats()
    {
        TotalPlayCount = 0;
        LastPlayedSound = null;
    }
}
