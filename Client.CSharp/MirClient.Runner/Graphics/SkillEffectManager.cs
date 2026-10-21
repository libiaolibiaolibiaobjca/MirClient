using System;
using System.Collections.Generic;
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

    // 经典技能特效生成入口
    public void SpawnThunder(Vector2 targetTile) => AddEffect(new ThunderEffect(targetTile));
    public void SpawnFireSword(Vector2 playerTile, int direction) => AddEffect(new FireSwordEffect(playerTile, direction));
    public void SpawnHealing(Vector2 targetTile) => AddEffect(new HealingEffect(targetTile));
    public void SpawnFireball(Vector2 startTile, Vector2 targetTile, Action? onHit = null) => AddEffect(new FireballEffect(startTile, targetTile, onHit));
    public void SpawnLevelUp(Vector2 targetTile) => AddEffect(new LevelUpEffect(targetTile));

    // 全量补齐的三职业经典法术特效
    public void SpawnAssassinate(Vector2 playerTile, int direction) => AddEffect(new AssassinateEffect(playerTile, direction));
    public void SpawnHalfMoon(Vector2 playerTile, int direction) => AddEffect(new HalfMoonEffect(playerTile, direction));
    public void SpawnFireWall(Vector2 targetTile, float duration = 15f) => AddEffect(new FireWallEffect(targetTile, duration));
    public void SpawnMagicShield(Vector2 targetTile, float duration = 15f) => AddEffect(new MagicShieldEffect(targetTile, duration));
    public void SpawnIceRoar(Vector2 targetTile) => AddEffect(new IceRoarEffect(targetTile));
    public void SpawnPoisonMist(Vector2 targetTile, bool isRedPoison) => AddEffect(new PoisonMistEffect(targetTile, isRedPoison));
    public void SpawnSoulCharm(Vector2 startTile, Vector2 targetTile, Action? onHit = null) => AddEffect(new SoulCharmEffect(startTile, targetTile, onHit));

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
        float alpha = MathF.Sin(progress * MathF.PI);

        Color coreColor = new(240, 245, 255, (int)(255 * alpha));
        Color glowColor = new(80, 160, 255, (int)(180 * alpha));

        float startY = targetPixel.Y - 450;
        float endY = targetPixel.Y;

        int segments = 12;
        float segH = (endY - startY) / segments;
        Vector2 prev = new(targetPixel.X, startY);

        for (int i = 1; i <= segments; i++)
        {
            float curY = startY + i * segH;
            float offsetX = (i == segments) ? 0 : MathF.Sin(i * 3.5f + _time * 25f) * 16f;
            Vector2 cur = new(targetPixel.X + offsetX, curY);

            Raylib.DrawLineEx(prev, cur, 8f, glowColor);
            Raylib.DrawLineEx(prev, cur, 3f, coreColor);
            prev = cur;
        }

        float burstRadius = 25f * (1f - progress);
        Raylib.DrawCircleV(targetPixel, burstRadius, glowColor);
        Raylib.DrawCircleV(targetPixel, burstRadius * 0.5f, coreColor);
    }
}

/// <summary>
/// 烈火剑法特效：烈焰附魔与身前炽热剑气爆发
/// </summary>
public class FireSwordEffect : ISkillEffect
{
    private readonly Vector2 _playerTile;
    private readonly int _direction;
    private float _time = 0f;
    private const float DURATION = 0.5f;

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
        float alpha = 1f - progress;

        Vector2 dirVec = _direction switch
        {
            0 => new Vector2(0, -1),
            1 => new Vector2(1, -1),
            2 => new Vector2(1, 0),
            3 => new Vector2(1, 1),
            4 => new Vector2(0, 1),
            5 => new Vector2(-1, 1),
            6 => new Vector2(-1, 0),
            7 => new Vector2(-1, -1),
            _ => new Vector2(0, 1)
        };
        dirVec = Vector2.Normalize(dirVec);

        Vector2 slashPos = playerPixel + dirVec * (24f + progress * 20f);
        Color flameColor = new(255, (byte)(120 * (1f - progress)), 20, (int)(255 * alpha));
        Color innerColor = new(255, 240, 150, (int)(255 * alpha));

        for (int i = 0; i < 7; i++)
        {
            float angleOffset = (i - 3) * 0.18f;
            Vector2 p = slashPos + new Vector2(MathF.Cos(angleOffset) * 20f, MathF.Sin(angleOffset) * 20f);
            Raylib.DrawCircleV(p, 12f * (1f - progress * 0.5f), flameColor);
            Raylib.DrawCircleV(p, 6f * (1f - progress * 0.5f), innerColor);
        }
    }
}

/// <summary>
/// 刺杀剑术特效：隔位 2 格纯白/淡青破防穿透剑气
/// </summary>
public class AssassinateEffect : ISkillEffect
{
    private readonly Vector2 _playerTile;
    private readonly int _direction;
    private float _time = 0f;
    private const float DURATION = 0.35f;

    public bool IsFinished => _time >= DURATION;

    public AssassinateEffect(Vector2 playerTile, int direction)
    {
        _playerTile = playerTile;
        _direction = direction;
    }

    public void Update(float dt) => _time += dt;

    public void Draw(Vector2 screenCenter, Vector2 playerTilePos, int tileW, int tileH)
    {
        Vector2 playerPixel = screenCenter + (_playerTile - playerTilePos) * new Vector2(tileW, tileH);
        float progress = _time / DURATION;
        float alpha = MathF.Sin(progress * MathF.PI);

        Vector2 dirVec = _direction switch
        {
            0 => new Vector2(0, -1),
            1 => new Vector2(1, -1),
            2 => new Vector2(1, 0),
            3 => new Vector2(1, 1),
            4 => new Vector2(0, 1),
            5 => new Vector2(-1, 1),
            6 => new Vector2(-1, 0),
            7 => new Vector2(-1, -1),
            _ => new Vector2(0, 1)
        };
        dirVec = Vector2.Normalize(dirVec);

        Vector2 startPx = playerPixel + dirVec * 16f;
        Vector2 endPx = playerPixel + dirVec * 88f; // 穿透两格

        Color swordGlow = new(180, 230, 255, (int)(220 * alpha));
        Color swordCore = new(255, 255, 255, (int)(255 * alpha));

        Raylib.DrawLineEx(startPx, endPx, 7f, swordGlow);
        Raylib.DrawLineEx(startPx, endPx, 2.5f, swordCore);

        // 剑气前端破空光尖
        Raylib.DrawCircleV(endPx, 8f * alpha, swordCore);
    }
}

/// <summary>
/// 半月弯刀特效：战士身前 5 格金色半月圆弧斩击
/// </summary>
public class HalfMoonEffect : ISkillEffect
{
    private readonly Vector2 _playerTile;
    private readonly int _direction;
    private float _time = 0f;
    private const float DURATION = 0.4f;

    public bool IsFinished => _time >= DURATION;

    public HalfMoonEffect(Vector2 playerTile, int direction)
    {
        _playerTile = playerTile;
        _direction = direction;
    }

    public void Update(float dt) => _time += dt;

    public void Draw(Vector2 screenCenter, Vector2 playerTilePos, int tileW, int tileH)
    {
        Vector2 playerPixel = screenCenter + (_playerTile - playerTilePos) * new Vector2(tileW, tileH);
        float progress = _time / DURATION;
        float alpha = MathF.Sin(progress * MathF.PI);

        float baseAngle = _direction switch
        {
            0 => -90f,
            1 => -45f,
            2 => 0f,
            3 => 45f,
            4 => 90f,
            5 => 135f,
            6 => 180f,
            7 => 225f,
            _ => 90f
        };

        Color moonGold = new(255, 215, 0, (int)(230 * alpha));
        Color moonCore = new(255, 255, 220, (int)(255 * alpha));

        float radius = 38f + progress * 16f;
        float startAngle = baseAngle - 60f;
        float endAngle = baseAngle + 60f;

        Raylib.DrawCircleSectorLines(playerPixel, radius, startAngle, endAngle, 24, moonGold);
        Raylib.DrawCircleSectorLines(playerPixel, radius - 4f, startAngle + 5f, endAngle - 5f, 20, moonCore);
    }
}

/// <summary>
/// 火墙特效：在地面指定格持续燃烧的熊熊烈火
/// </summary>
public class FireWallEffect : ISkillEffect
{
    private readonly Vector2 _targetTile;
    private float _time = 0f;
    private readonly float _duration;

    public bool IsFinished => _time >= _duration;

    public FireWallEffect(Vector2 targetTile, float duration = 15f)
    {
        _targetTile = targetTile;
        _duration = duration;
    }

    public void Update(float dt) => _time += dt;

    public void Draw(Vector2 screenCenter, Vector2 playerTilePos, int tileW, int tileH)
    {
        Vector2 targetPixel = screenCenter + (_targetTile - playerTilePos) * new Vector2(tileW, tileH);
        float remain = _duration - _time;
        float alpha = Math.Clamp(remain / 0.5f, 0f, 1f); // 结束前半秒平滑熄灭

        // 3 簇燃烧火焰微粒
        for (int i = 0; i < 5; i++)
        {
            float seed = i * 1.57f;
            float flicker = MathF.Sin(_time * 12f + seed) * 4f;
            float px = targetPixel.X + (i - 2) * 8f + flicker;
            float py = targetPixel.Y - 6f - MathF.Abs(MathF.Sin(_time * 8f + seed)) * 18f;

            Color c1 = new(255, (byte)(60 + i * 30), 10, (int)(220 * alpha));
            Color c2 = new(255, 230, 80, (int)(180 * alpha));

            Raylib.DrawCircleV(new Vector2(px, py), 9f, c1);
            Raylib.DrawCircleV(new Vector2(px, py + 4f), 5f, c2);
        }
    }
}

/// <summary>
/// 魔法盾特效：金色蛋形椭圆光盾环绕周身吸收伤害
/// </summary>
public class MagicShieldEffect : ISkillEffect
{
    private readonly Vector2 _targetTile;
    private float _time = 0f;
    private readonly float _duration;

    public bool IsFinished => _time >= _duration;

    public MagicShieldEffect(Vector2 targetTile, float duration = 15f)
    {
        _targetTile = targetTile;
        _duration = duration;
    }

    public void Update(float dt) => _time += dt;

    public void Draw(Vector2 screenCenter, Vector2 playerTilePos, int tileW, int tileH)
    {
        Vector2 targetPixel = screenCenter + (_targetTile - playerTilePos) * new Vector2(tileW, tileH);
        float pulse = 0.8f + 0.2f * MathF.Sin(_time * 5f);
        float remain = _duration - _time;
        float alpha = Math.Clamp(remain / 0.5f, 0f, 1f) * pulse;

        Color shieldGlow = new(255, 200, 50, (int)(140 * alpha));
        Color shieldLine = new(255, 240, 160, (int)(220 * alpha));

        // 金色椭圆光盾环绕 (宽 26, 高 38)
        Raylib.DrawEllipseLines((int)targetPixel.X, (int)targetPixel.Y - 14, 26, 38, shieldLine);
        Raylib.DrawEllipse((int)targetPixel.X, (int)targetPixel.Y - 14, 24, 35, shieldGlow);
    }
}

/// <summary>
/// 冰咆哮特效：3x3 范围极速旋转的暴风雪与冰棱爆炸
/// </summary>
public class IceRoarEffect : ISkillEffect
{
    private readonly Vector2 _targetTile;
    private float _time = 0f;
    private const float DURATION = 0.65f;

    public bool IsFinished => _time >= DURATION;

    public IceRoarEffect(Vector2 targetTile)
    {
        _targetTile = targetTile;
    }

    public void Update(float dt) => _time += dt;

    public void Draw(Vector2 screenCenter, Vector2 playerTilePos, int tileW, int tileH)
    {
        Vector2 targetPixel = screenCenter + (_targetTile - playerTilePos) * new Vector2(tileW, tileH);
        float progress = _time / DURATION;
        float alpha = MathF.Sin(progress * MathF.PI);

        Color iceBlue = new(130, 210, 255, (int)(220 * alpha));
        Color iceWhite = new(240, 250, 255, (int)(255 * alpha));

        float stormRadius = 45f * (0.4f + progress * 0.6f);
        float rot = _time * 18f;

        // 旋转冰风暴粒子
        for (int i = 0; i < 8; i++)
        {
            float angle = rot + i * (MathF.PI / 4f);
            float r = stormRadius * (0.6f + 0.4f * MathF.Sin(i + _time * 10f));
            Vector2 p = targetPixel + new Vector2(MathF.Cos(angle) * r, MathF.Sin(angle) * (r * 0.65f));

            Raylib.DrawCircleV(p, 6f, iceBlue);
            Raylib.DrawCircleV(p, 3f, iceWhite);
        }

        // 冰霜冲击光环
        Raylib.DrawCircleLines((int)targetPixel.X, (int)targetPixel.Y, stormRadius, iceBlue);
    }
}

/// <summary>
/// 施毒术特效：红毒/绿毒爆散毒雾与持续光晕
/// </summary>
public class PoisonMistEffect : ISkillEffect
{
    private readonly Vector2 _targetTile;
    private readonly bool _isRed;
    private float _time = 0f;
    private const float DURATION = 0.8f;

    public bool IsFinished => _time >= DURATION;

    public PoisonMistEffect(Vector2 targetTile, bool isRedPoison)
    {
        _targetTile = targetTile;
        _isRed = isRedPoison;
    }

    public void Update(float dt) => _time += dt;

    public void Draw(Vector2 screenCenter, Vector2 playerTilePos, int tileW, int tileH)
    {
        Vector2 targetPixel = screenCenter + (_targetTile - playerTilePos) * new Vector2(tileW, tileH);
        float progress = _time / DURATION;
        float alpha = (1f - progress);

        Color mistColor = _isRed
            ? new Color(230, 40, 50, (int)(180 * alpha))
            : new Color(40, 220, 60, (int)(180 * alpha));

        for (int i = 0; i < 6; i++)
        {
            float seed = i * 1.05f;
            float r = 10f + progress * 24f;
            float px = targetPixel.X + MathF.Cos(seed + _time * 3f) * r;
            float py = targetPixel.Y - 10f + MathF.Sin(seed + _time * 3f) * (r * 0.7f);

            Raylib.DrawCircleV(new Vector2(px, py), 8f * (1f - progress * 0.5f), mistColor);
        }
    }
}

/// <summary>
/// 灵魂火符特效：道士八卦灵符飞行弹道与命中爆破
/// </summary>
public class SoulCharmEffect : ISkillEffect
{
    private readonly Vector2 _startTile;
    private readonly Vector2 _targetTile;
    private readonly Action? _onHit;
    private float _time = 0f;
    private const float DURATION = 0.35f;

    public bool IsFinished => _time >= DURATION;

    public SoulCharmEffect(Vector2 startTile, Vector2 targetTile, Action? onHit = null)
    {
        _startTile = startTile;
        _targetTile = targetTile;
        _onHit = onHit;
    }

    public void Update(float dt)
    {
        bool wasFinished = IsFinished;
        _time += dt;
        if (!wasFinished && IsFinished)
        {
            _onHit?.Invoke();
        }
    }

    public void Draw(Vector2 screenCenter, Vector2 playerTilePos, int tileW, int tileH)
    {
        Vector2 startPx = screenCenter + (_startTile - playerTilePos) * new Vector2(tileW, tileH);
        Vector2 targetPx = screenCenter + (_targetTile - playerTilePos) * new Vector2(tileW, tileH);

        float t = Math.Clamp(_time / DURATION, 0f, 1f);
        Vector2 curPx = Vector2.Lerp(startPx, targetPx, t);

        Color charmYellow = new(255, 230, 100, 255);
        Color charmFire = new(255, 120, 30, 200);

        // 飞行八卦符咒 (旋转小长方形)
        Rectangle rect = new(curPx.X, curPx.Y, 12, 18);
        Vector2 origin = new(6, 9);
        Raylib.DrawRectanglePro(rect, origin, _time * 720f, charmYellow);
        Raylib.DrawCircleV(curPx, 9f, charmFire);
    }
}

/// <summary>
/// 治愈术特效：圣洁光环与上升治疗粒子
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

        Color holyGlow = new(100, 255, 150, (int)(180 * alpha));
        Color holyRing = new(200, 255, 200, (int)(240 * alpha));

        float ringRadius = 24f * MathF.Sin(progress * MathF.PI * 0.5f);
        Raylib.DrawCircleLines((int)targetPixel.X, (int)targetPixel.Y, ringRadius, holyRing);

        for (int i = 0; i < 6; i++)
        {
            float seed = i * 1.05f;
            float particleY = targetPixel.Y - (progress * 60f + seed * 8f);
            float particleX = targetPixel.X + MathF.Sin(_time * 8f + seed) * 16f;
            Raylib.DrawCircle((int)particleX, (int)particleY, 3.5f, holyGlow);
        }
    }
}

/// <summary>
/// 火球术特效：飞行弹道与命中火花爆破 (双阶段)
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
        Vector2 startPixel = screenCenter + (_startTile - playerTilePos) * new Vector2(tileW, tileH);
        Vector2 targetPixel = screenCenter + (_targetTile - playerTilePos) * new Vector2(tileW, tileH);

        if (_time < FLIGHT_TIME)
        {
            float t = _time / FLIGHT_TIME;
            Vector2 curPixel = Vector2.Lerp(startPixel, targetPixel, t);

            Color outerFlame = new(255, 80, 20, 220);
            Color innerFlame = new(255, 240, 100, 255);

            Raylib.DrawCircleV(curPixel, 14f, outerFlame);
            Raylib.DrawCircleV(curPixel, 7f, innerFlame);
        }
        else
        {
            float explodeProgress = (_time - FLIGHT_TIME) / EXPLODE_TIME;
            float alpha = 1f - explodeProgress;
            Color boomColor = new(255, (byte)(150 * alpha), 30, (int)(255 * alpha));
            Raylib.DrawCircleV(targetPixel, 18f + explodeProgress * 12f, boomColor);
        }
    }
}

/// <summary>
/// 升级特效：金光冲天与灵力光环
/// </summary>
public class LevelUpEffect : ISkillEffect
{
    private readonly Vector2 _tilePos;
    private float _time = 0f;
    private const float DURATION = 1.2f;

    public bool IsFinished => _time >= DURATION;

    public LevelUpEffect(Vector2 tilePos)
    {
        _tilePos = tilePos;
    }

    public void Update(float dt) => _time += dt;

    public void Draw(Vector2 screenCenter, Vector2 playerTilePos, int tileW, int tileH)
    {
        Vector2 targetPx = screenCenter + (_tilePos - playerTilePos) * new Vector2(tileW, tileH);
        float progress = _time / DURATION;
        float alpha = MathF.Sin(progress * MathF.PI);

        int a = (int)(255 * alpha);
        Color pillarColor = new(255, 230, 80, (int)(a * 0.75f));
        Color coreColor = new(255, 255, 200, a);

        int pillarW = 28 + (int)(12 * MathF.Sin(_time * 12f));
        int pillarX = (int)targetPx.X - pillarW / 2;
        int pillarTop = (int)targetPx.Y - 500;
        int pillarH = 500;

        Raylib.DrawRectangle(pillarX, pillarTop, pillarW, pillarH, pillarColor);
        Raylib.DrawRectangle((int)targetPx.X - pillarW / 4, pillarTop, pillarW / 2, pillarH, coreColor);

        for (int i = 0; i < 3; i++)
        {
            float ringPhase = (_time * 1.5f + i * 0.33f) % 1.0f;
            float ringRadius = 10f + ringPhase * 36f;
            int ringAlpha = (int)(a * (1.0f - ringPhase));
            Raylib.DrawCircleLines((int)targetPx.X, (int)targetPx.Y, ringRadius, new Color(255, 215, 0, ringAlpha));
        }

        float textY = targetPx.Y - 60f - (progress * 25f);
        FontManager.DrawTextWithShadow("LEVEL UP!", (int)targetPx.X - 38, (int)textY, 14, Color.Gold, Color.Black);
    }
}
