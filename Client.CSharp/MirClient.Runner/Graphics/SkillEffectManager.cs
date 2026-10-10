using System.Numerics;
using Raylib_cs;

namespace MirClient.Runner.Graphics;

public interface ISkillEffect
{
    bool IsFinished { get; }
    void Update(float dt);
    void Draw(Vector2 screenCenter, Vector2 playerTilePos, int tileW, int tileH);
}

/// <summary>
/// 盛大传奇技能法术特效管理器 (支持 Additive 加法混合与全屏粒子渲染)
/// </summary>
public class SkillEffectManager
{
    private readonly List<ISkillEffect> _effects = new();

    public int ActiveEffectCount => _effects.Count;

    public void AddEffect(ISkillEffect effect)
    {
        lock (_effects)
        {
            _effects.Add(effect);
        }
    }

    public void Clear()
    {
        lock (_effects)
        {
            _effects.Clear();
        }
    }

    public void SpawnThunder(Vector2 targetTile) => AddEffect(new ThunderEffect(targetTile));
    public void SpawnFireSword(Vector2 playerTile, int direction) => AddEffect(new FireSwordEffect(playerTile, direction));
    public void SpawnHealing(Vector2 targetTile) => AddEffect(new HealingEffect(targetTile));
    public void SpawnFireball(Vector2 startTile, Vector2 targetTile, Action? onHit = null) => AddEffect(new FireballEffect(startTile, targetTile, onHit));

    public void Update(float dt)
    {
        lock (_effects)
        {
            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                var effect = _effects[i];
                effect.Update(dt);
                if (effect.IsFinished)
                {
                    _effects.RemoveAt(i);
                }
            }
        }
    }

    public void DrawAll(Vector2 screenCenter, Vector2 playerTilePos, int tileW, int tileH)
    {
        lock (_effects)
        {
            if (_effects.Count == 0) return;

            // 启用经典加法发光混合，黑底素材自然通透，光芒叠加到世界地图之上
            Raylib.BeginBlendMode(BlendMode.Additive);
            foreach (var effect in _effects)
            {
                effect.Draw(screenCenter, playerTilePos, tileW, tileH);
            }
            Raylib.EndBlendMode();
        }
    }
}

/// <summary>
/// 雷电术特效：从天顶贯穿屏幕的蓝白雷霆与落地电浆爆裂
/// </summary>
public class ThunderEffect : ISkillEffect
{
    private readonly Vector2 _targetTile;
    private float _time = 0f;
    private const float DURATION = 0.45f;

    public bool IsFinished => _time >= DURATION;

    public ThunderEffect(Vector2 targetTile)
    {
        _targetTile = targetTile;
    }

    public void Update(float dt) => _time += dt;

    public void Draw(Vector2 screenCenter, Vector2 playerTilePos, int tileW, int tileH)
    {
        Vector2 targetPixel = screenCenter + (_targetTile - playerTilePos) * new Vector2(tileW, tileH);
        float progress = _time / DURATION;
        float alpha = MathF.Sin(progress * MathF.PI); // 峰值亮白淡出

        int groundX = (int)targetPixel.X;
        int groundY = (int)targetPixel.Y;

        // 1. 垂直主闪电光柱 (粗蓝白雷霆)
        int a = (int)(255 * alpha);
        Color coreWhite = new Color(255, 255, 255, a);
        Color glowBlue = new Color(100, 180, 255, (int)(180 * alpha));

        Raylib.DrawLineEx(new Vector2(groundX, groundY - 480), new Vector2(groundX, groundY), 6f, glowBlue);
        Raylib.DrawLineEx(new Vector2(groundX, groundY - 480), new Vector2(groundX, groundY), 2f, coreWhite);

        // 闪电微小随机折线抖动
        float offset = (Random.Shared.NextSingle() - 0.5f) * 16f;
        Raylib.DrawLineEx(new Vector2(groundX + offset, groundY - 240), new Vector2(groundX, groundY), 3f, glowBlue);

        // 2. 落地电弧爆裂光球与扩散冲击环
        float blastRadius = 16f + 24f * progress;
        Raylib.DrawCircle(groundX, groundY, blastRadius, glowBlue);
        Raylib.DrawCircle(groundX, groundY, blastRadius * 0.5f, coreWhite);
        Raylib.DrawCircleLines(groundX, groundY, blastRadius * 1.3f, coreWhite);
    }
}

/// <summary>
/// 烈火剑法特效：挥刀刀刃爆发出的烈焰斩击弧光与火焰粒子
/// </summary>
public class FireSwordEffect : ISkillEffect
{
    private readonly Vector2 _playerTile;
    private readonly int _direction;
    private float _time = 0f;
    private const float DURATION = 0.35f;

    public bool IsFinished => _time >= DURATION;

    public FireSwordEffect(Vector2 playerTile, int direction)
    {
        _playerTile = playerTile;
        _direction = direction;
    }

    public void Update(float dt) => _time += dt;

    public void Draw(Vector2 screenCenter, Vector2 playerTilePos, int tileW, int tileH)
    {
        Vector2 playerPixel = screenCenter + (_playerTile - playerTilePos) * new Vector2(tileW, tileH);
        float progress = _time / DURATION;
        float alpha = 1.0f - progress;

        // 根据朝向计算火焰喷射弧心
        float angle = _direction * 45f;
        float rad = angle * (MathF.PI / 180f);
        Vector2 dirVec = new(MathF.Cos(rad), MathF.Sin(rad));

        Vector2 arcCenter = playerPixel + dirVec * 28f;
        int a = (int)(255 * alpha);
        Color flameRed = new Color(255, 60, 20, a);
        Color flameGold = new Color(255, 200, 30, (int)(230 * alpha));

        // 绘制三层发光烈焰月牙光圈
        float radius = 22f + 18f * progress;
        Raylib.DrawCircle((int)arcCenter.X, (int)arcCenter.Y, radius, flameRed);
        Raylib.DrawCircle((int)arcCenter.X, (int)arcCenter.Y, radius * 0.6f, flameGold);
        Raylib.DrawCircleLines((int)arcCenter.X, (int)arcCenter.Y, radius * 1.2f, flameGold);
    }
}

/// <summary>
/// 治愈术特效：施法者脚底旋转绿色光环与升腾的生命十字粒子
/// </summary>
public class HealingEffect : ISkillEffect
{
    private readonly Vector2 _targetTile;
    private float _time = 0f;
    private const float DURATION = 0.8f;

    public bool IsFinished => _time >= DURATION;

    public HealingEffect(Vector2 targetTile)
    {
        _targetTile = targetTile;
    }

    public void Update(float dt) => _time += dt;

    public void Draw(Vector2 screenCenter, Vector2 playerTilePos, int tileW, int tileH)
    {
        Vector2 targetPixel = screenCenter + (_targetTile - playerTilePos) * new Vector2(tileW, tileH);
        float progress = _time / DURATION;
        float alpha = MathF.Sin(progress * MathF.PI);

        int cx = (int)targetPixel.X;
        int cy = (int)targetPixel.Y;

        int a = (int)(220 * alpha);
        Color holyGreen = new Color(80, 255, 120, a);
        Color brightWhite = new Color(240, 255, 240, (int)(180 * alpha));

        // 1. 脚底同心扩散神圣光环
        float ringRadius = 14f + 16f * MathF.Sin(progress * MathF.PI);
        Raylib.DrawCircleLines(cx, cy + 4, ringRadius, holyGreen);
        Raylib.DrawCircleLines(cx, cy + 4, ringRadius * 0.7f, brightWhite);

        // 2. 升腾的生命治愈粒子
        for (int i = 0; i < 4; i++)
        {
            float pOffset = (progress + i * 0.25f) % 1.0f;
            int px = cx + (int)((i - 1.5f) * 12f);
            int py = cy - (int)(pOffset * 48f);

            // 绘制绿色生命十字
            Raylib.DrawLine(px - 4, py, px + 4, py, holyGreen);
            Raylib.DrawLine(px, py - 4, px, py + 4, holyGreen);
        }
    }
}

/// <summary>
/// 火球术特效：从施法者手中向目标瓦片高速飞行的炽热火球与落点爆燃
/// </summary>
public class FireballEffect : ISkillEffect
{
    private readonly Vector2 _startTile;
    private readonly Vector2 _targetTile;
    private float _time = 0f;
    private const float FLIGHT_TIME = 0.35f;
    private const float EXPLODE_TIME = 0.25f;

    public bool IsFinished => _time >= FLIGHT_TIME + EXPLODE_TIME;

    private readonly Action? _onHit;
    private bool _hitTriggered = false;

    public FireballEffect(Vector2 startTile, Vector2 targetTile, Action? onHit = null)
    {
        _startTile = startTile;
        _targetTile = targetTile;
        _onHit = onHit;
    }

    public void Update(float dt)
    {
        _time += dt;
        if (!_hitTriggered && _time >= FLIGHT_TIME)
        {
            _hitTriggered = true;
            _onHit?.Invoke();
        }
    }

    public void Draw(Vector2 screenCenter, Vector2 playerTilePos, int tileW, int tileH)
    {
        Vector2 startPx = screenCenter + (_startTile - playerTilePos) * new Vector2(tileW, tileH);
        Vector2 targetPx = screenCenter + (_targetTile - playerTilePos) * new Vector2(tileW, tileH);

        if (_time < FLIGHT_TIME)
        {
            // 飞行阶段：线性插值弹道
            float t = _time / FLIGHT_TIME;
            Vector2 cur = Vector2.Lerp(startPx, targetPx, t);

            // 旋转火球核心与外层火苗
            Color core = new Color(255, 240, 100, 255);
            Color outer = new Color(255, 80, 20, 200);

            Raylib.DrawCircle((int)cur.X, (int)cur.Y, 10f, outer);
            Raylib.DrawCircle((int)cur.X, (int)cur.Y, 5f, core);

            // 尾迹微粒
            Vector2 tail = Vector2.Lerp(startPx, targetPx, MathF.Max(0f, t - 0.15f));
            Raylib.DrawLineEx(tail, cur, 4f, outer);
        }
        else
        {
            // 爆裂阶段：在目标处展开爆炸光圈
            float expProgress = (_time - FLIGHT_TIME) / EXPLODE_TIME;
            float alpha = 1.0f - expProgress;
            float expRadius = 12f + 24f * expProgress;

            int a = (int)(255 * alpha);
            Color expRed = new Color(255, 60, 20, a);
            Color expGold = new Color(255, 210, 40, (int)(240 * alpha));

            Raylib.DrawCircle((int)targetPx.X, (int)targetPx.Y, expRadius, expRed);
            Raylib.DrawCircle((int)targetPx.X, (int)targetPx.Y, expRadius * 0.6f, expGold);
            Raylib.DrawCircleLines((int)targetPx.X, (int)targetPx.Y, expRadius * 1.2f, expGold);
        }
    }
}
