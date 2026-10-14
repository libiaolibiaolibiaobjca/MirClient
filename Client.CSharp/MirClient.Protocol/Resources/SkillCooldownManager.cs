namespace MirClient.Protocol.Resources;

/// <summary>
/// 盛大热血传奇技能公共冷却 (GCD) 与独立技能冷却管理器
/// </summary>
public class SkillCooldownManager
{
    private readonly Dictionary<SkillType, float> _cooldownConfig = new()
    {
        { SkillType.ThunderBolt, 1.2f }, // 雷电术 1.2s 施法间隔
        { SkillType.FireSword, 10.0f },  // 烈火剑法 10.0s 大招
        { SkillType.Healing, 3.0f },     // 治愈术 3.0s
        { SkillType.Fireball, 1.5f }     // 火球术 1.5s
    };

    private readonly Dictionary<SkillType, float> _remainCooldowns = new();
    private readonly Dictionary<SkillType, float> _totalCooldowns = new();

    private float _globalCooldown = 0f;
    public const float GCD_DURATION = 0.4f; // 全局公共法术后摇 0.4s

    public float GlobalCooldown => _globalCooldown;

    public void SetCooldownConfig(SkillType skill, float seconds)
    {
        _cooldownConfig[skill] = MathF.Max(0.1f, seconds);
    }

    public float GetConfigCooldown(SkillType skill)
    {
        return _cooldownConfig.TryGetValue(skill, out float cd) ? cd : 1.0f;
    }

    /// <summary>
    /// 推进冷却时间倒计时
    /// </summary>
    public void Update(float dt)
    {
        if (dt <= 0f) return;

        if (_globalCooldown > 0f)
        {
            _globalCooldown = MathF.Max(0f, _globalCooldown - dt);
        }

        foreach (var skill in _remainCooldowns.Keys.ToList())
        {
            float rem = _remainCooldowns[skill] - dt;
            if (rem <= 0f)
            {
                _remainCooldowns[skill] = 0f;
            }
            else
            {
                _remainCooldowns[skill] = rem;
            }
        }
    }

    /// <summary>
    /// 检查指定技能当前是否可以施放
    /// </summary>
    /// <param name="skill">技能类型</param>
    /// <param name="remainTime">剩余冷却时间</param>
    /// <returns>若就绪返回 true，在冷却中返回 false</returns>
    public bool CanCast(SkillType skill, out float remainTime)
    {
        // 优先检查技能独立冷却
        if (_remainCooldowns.TryGetValue(skill, out float skillRem) && skillRem > 0f)
        {
            remainTime = skillRem;
            return false;
        }

        // 检查全局公共后摇 (GCD)
        if (_globalCooldown > 0f)
        {
            remainTime = _globalCooldown;
            return false;
        }

        remainTime = 0f;
        return true;
    }

    /// <summary>
    /// 触发技能独立冷却并施加公共后摇 GCD
    /// </summary>
    public void TriggerCooldown(SkillType skill)
    {
        float total = GetConfigCooldown(skill);
        _totalCooldowns[skill] = total;
        _remainCooldowns[skill] = total;
        _globalCooldown = GCD_DURATION;
    }

    /// <summary>
    /// 获取当前冷却进度比例 (1.0f: 刚触发冷却满格, 0.0f: 冷却完毕已就绪)
    /// </summary>
    public float GetCooldownProgress(SkillType skill)
    {
        if (!_remainCooldowns.TryGetValue(skill, out float rem) || rem <= 0f)
        {
            return 0f;
        }

        float total = _totalCooldowns.TryGetValue(skill, out float t) && t > 0f ? t : GetConfigCooldown(skill);
        return Math.Clamp(rem / total, 0f, 1f);
    }

    /// <summary>
    /// 获取技能当前精确剩余冷却秒数
    /// </summary>
    public float GetRemainTime(SkillType skill)
    {
        return _remainCooldowns.TryGetValue(skill, out float rem) ? MathF.Max(0f, rem) : 0f;
    }

    /// <summary>
    /// 重置指定技能的冷却状态
    /// </summary>
    public void ResetCooldown(SkillType skill)
    {
        _remainCooldowns[skill] = 0f;
    }

    /// <summary>
    /// 重置所有技能冷却与 GCD (与 @full 或 @cd 指令联动)
    /// </summary>
    public void ResetAll()
    {
        _globalCooldown = 0f;
        foreach (var key in _remainCooldowns.Keys.ToList())
        {
            _remainCooldowns[key] = 0f;
        }
    }
}
