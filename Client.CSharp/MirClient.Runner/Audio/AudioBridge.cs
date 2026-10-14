using System.Collections.Concurrent;
using Raylib_cs;
using MirClient.Protocol.Resources;

namespace MirClient.Runner.Audio;

/// <summary>
/// 硬件音频桥接驱动器：将 SoundManager 领域事件无缝映射至 Raylib 原生音频流水线
/// 采用无头安全设计：在 Linux CI/Docker 等无声卡环境下自动优雅静默，在真机环境下全量输出
/// </summary>
public static class AudioBridge
{
    private static bool _isInitialized = false;
    private static readonly ConcurrentDictionary<SoundEffectType, Sound> _soundCache = new();

    public static bool IsReady => _isInitialized && Raylib.IsAudioDeviceReady();

    public static void Initialize()
    {
        if (_isInitialized) return;

        try
        {
            if (!Raylib.IsAudioDeviceReady())
            {
                Raylib.InitAudioDevice();
            }

            if (Raylib.IsAudioDeviceReady())
            {
                _isInitialized = true;
                PreloadSyntheticSounds();
                Console.WriteLine("[音频驱动] 原生音频硬件加速设备初始化成功！");
            }
            else
            {
                Console.WriteLine("[音频驱动] 未检测到物理声卡硬件，自动进入无头静音保护模式。");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[音频驱动] 启动异常，已优雅降级为安全模式: {ex.Message}");
            _isInitialized = false;
        }

        // 挂接 SoundManager 全局回调
        SoundManager.Instance.OnPlaySound = PlayHardwareSound;
    }

    private static void PlayHardwareSound(SoundEffectType type, float volume)
    {
        if (!IsReady || volume <= 0f) return;

        try
        {
            if (_soundCache.TryGetValue(type, out Sound sound))
            {
                Raylib.SetSoundVolume(sound, volume);
                Raylib.PlaySound(sound);
            }
        }
        catch
        {
            // 忽略偶发的音频缓冲区溢出
        }
    }

    /// <summary>
    /// 预制传奇原版经典音色微型合成波，确保即便没有外部 WAV 资产也能有原汁原味的声音反馈
    /// </summary>
    private static void PreloadSyntheticSounds()
    {
        if (!IsReady) return;

        try
        {
            // 金币拾取: 经典 1250Hz -> 1800Hz 亮脆双音频
            RegisterSyntheticWave(SoundEffectType.PickupGold, 1400f, 0.08f, isChirp: true);
            // 挥刀破风: 220Hz 迅速衰减的风声
            RegisterSyntheticWave(SoundEffectType.AttackSwing, 220f, 0.12f, isNoise: true);
            // 雷电术: 160Hz 低沉轰鸣加噪点爆裂
            RegisterSyntheticWave(SoundEffectType.Thunder, 160f, 0.35f, isNoise: true);
            // 大火球: 320Hz 呼啸
            RegisterSyntheticWave(SoundEffectType.Fireball, 320f, 0.22f, isNoise: true);
            // 烈火剑法: 180Hz 烈焰灼烧
            RegisterSyntheticWave(SoundEffectType.FireSword, 180f, 0.28f, isNoise: true);
            // 治愈术: 660Hz -> 880Hz 圣光纯音
            RegisterSyntheticWave(SoundEffectType.Heal, 750f, 0.3f, isChirp: true);
            // 怪物受击: 140Hz 闷吼
            RegisterSyntheticWave(SoundEffectType.MonsterStruck, 140f, 0.14f);
            // 怪物阵亡: 90Hz 惨叫
            RegisterSyntheticWave(SoundEffectType.MonsterDie, 95f, 0.4f);
            // 玩家受击
            RegisterSyntheticWave(SoundEffectType.PlayerStruck, 180f, 0.12f);
            // 拾取装备物品: 440Hz 清脆入包咔哒声
            RegisterSyntheticWave(SoundEffectType.PickupItem, 520f, 0.06f);
            // 喝药水: 350Hz 咕嘟吞咽
            RegisterSyntheticWave(SoundEffectType.UsePotion, 380f, 0.15f);
            // 升级号角: 880Hz 激昂和弦
            RegisterSyntheticWave(SoundEffectType.LevelUp, 880f, 0.6f, isChirp: true);
            // 按钮点击
            RegisterSyntheticWave(SoundEffectType.ButtonClick, 600f, 0.04f);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[音频驱动] 预制音色生成降级: {ex.Message}");
        }
    }

    private static void RegisterSyntheticWave(SoundEffectType type, float freq, float durationSec, bool isChirp = false, bool isNoise = false)
    {
        int sampleRate = 22050;
        int frameCount = (int)(sampleRate * durationSec);
        if (frameCount <= 0) return;

        short[] samples = new short[frameCount];
        for (int i = 0; i < frameCount; i++)
        {
            float t = (float)i / sampleRate;
            float progress = (float)i / frameCount;
            float envelope = MathF.Sin(progress * MathF.PI); // 平滑包络，杜绝爆音咔哒声

            float currentFreq = isChirp ? (freq + (freq * 0.5f * progress)) : freq;
            float sample = MathF.Sin(2.0f * MathF.PI * currentFreq * t);

            if (isNoise)
            {
                float noise = (Random.Shared.NextSingle() * 2f - 1f) * 0.4f;
                sample = (sample * 0.6f) + noise;
            }

            samples[i] = (short)(sample * envelope * 24000);
        }

        unsafe
        {
            fixed (short* ptr = samples)
            {
                Wave wave = new()
                {
                    FrameCount = (uint)frameCount,
                    SampleRate = (uint)sampleRate,
                    SampleSize = 16,
                    Channels = 1,
                    Data = (void*)ptr
                };

                Sound sound = Raylib.LoadSoundFromWave(wave);
                _soundCache[type] = sound;
            }
        }
    }

    public static void Dispose()
    {
        if (!_isInitialized) return;

        try
        {
            foreach (var kvp in _soundCache)
            {
                Raylib.UnloadSound(kvp.Value);
            }
            _soundCache.Clear();
            _isInitialized = false;
        }
        catch {}
    }
}
