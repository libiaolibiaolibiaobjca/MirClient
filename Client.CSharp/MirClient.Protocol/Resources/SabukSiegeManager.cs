using System.Numerics;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 沙巴克攻城战阶段状态枚举
/// </summary>
public enum SiegePhase
{
    Peace = 0,    // 和平休战期
    Prepare = 1,  // 集结号角备战期
    Fighting = 2, // 战火燃起攻城交战期
    Settled = 3   // 战事终局加冕结算期
}

/// <summary>
/// 行会攻城战绩与荣誉积分条目
/// </summary>
public class GuildSiegeScore
{
    public string GuildName { get; set; } = "";
    public int KillCount { get; set; } = 0;
    public int GateDamage { get; set; } = 0;
    public int PalaceHoldSeconds { get; set; } = 0;

    public int TotalScore => (GateDamage / 100) + (KillCount * 50) + (PalaceHoldSeconds * 10);

    public GuildSiegeScore() { }

    public GuildSiegeScore(string guildName)
    {
        GuildName = guildName;
    }
}

/// <summary>
/// 盛大传奇十周年：沙巴克攻城战与皇宫争夺系统管理器 (纯 C# 领域状态机)
/// </summary>
public class SabukSiegeManager
{
    private static SabukSiegeManager? _instance;
    public static SabukSiegeManager Instance => _instance ??= new SabukSiegeManager();

    public const int DEFAULT_GATE_MAX_HP = 50000;
    public const float DEFAULT_BATTLE_DURATION = 600.0f; // 默认交锋战 10 分钟

    public SiegePhase CurrentPhase { get; private set; } = SiegePhase.Peace;
    public string CurrentLordGuild { get; private set; } = "傲视天下"; // 初始沙城霸主
    public string PalaceOccupierGuild { get; private set; } = "傲视天下"; // 皇宫大殿据守行会

    public int GateMaxHp { get; private set; } = DEFAULT_GATE_MAX_HP;
    public int GateCurrentHp { get; private set; } = DEFAULT_GATE_MAX_HP;
    public bool IsGateDestroyed => GateCurrentHp <= 0;

    public float RemainingBattleTime { get; private set; } = 0f;
    public float TotalBattleTime { get; private set; } = DEFAULT_BATTLE_DURATION;

    // 参战行会积分记录
    private readonly Dictionary<string, GuildSiegeScore> _guildScores = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, GuildSiegeScore> GuildScores => _guildScores;

    // 累计争夺时钟计时器
    private float _palaceTickAccumulator = 0f;

    public SabukSiegeManager() { }

    /// <summary>
    /// 重置状态 (用于单元测试与新周目复位)
    /// </summary>
    public void Reset()
    {
        CurrentPhase = SiegePhase.Peace;
        CurrentLordGuild = "傲视天下";
        PalaceOccupierGuild = "傲视天下";
        GateMaxHp = DEFAULT_GATE_MAX_HP;
        GateCurrentHp = DEFAULT_GATE_MAX_HP;
        RemainingBattleTime = 0f;
        TotalBattleTime = DEFAULT_BATTLE_DURATION;
        _palaceTickAccumulator = 0f;
        _guildScores.Clear();
    }

    /// <summary>
    /// 发起沙巴克攻城战
    /// </summary>
    public bool StartSiege(float durationSeconds, out string message)
    {
        if (CurrentPhase == SiegePhase.Fighting)
        {
            message = "沙巴克攻城战正在激烈交火中，无需重复开启！";
            return false;
        }

        durationSeconds = durationSeconds > 0 ? durationSeconds : DEFAULT_BATTLE_DURATION;
        CurrentPhase = SiegePhase.Fighting;
        TotalBattleTime = durationSeconds;
        RemainingBattleTime = durationSeconds;

        GateMaxHp = DEFAULT_GATE_MAX_HP;
        GateCurrentHp = DEFAULT_GATE_MAX_HP;
        _palaceTickAccumulator = 0f;
        _guildScores.Clear();

        // 将当前守城行会预先计入参战名单
        if (!string.IsNullOrWhiteSpace(CurrentLordGuild))
        {
            _guildScores[CurrentLordGuild] = new GuildSiegeScore(CurrentLordGuild);
        }

        message = $"【全服通告】沙巴克攻城战号角吹响！战火燃起，战事时长: {Math.Ceiling(durationSeconds / 60)} 分钟！各路行会速速前往攻夺皇宫！";
        return true;
    }

    /// <summary>
    /// 结束沙巴克攻城战并进行新一届城主加冕结算
    /// </summary>
    public bool StopSiege(out string settlementReport)
    {
        if (CurrentPhase != SiegePhase.Fighting)
        {
            settlementReport = "当前未处于攻城战交火期！";
            return false;
        }

        CurrentPhase = SiegePhase.Settled;

        // 胜负判定：皇宫最终占据者拥有优先权；若无人占领则按积分最高者加冕
        string winnerGuild = PalaceOccupierGuild;
        if (string.IsNullOrWhiteSpace(winnerGuild))
        {
            var top = GetLeaderboard().FirstOrDefault();
            winnerGuild = top?.GuildName ?? CurrentLordGuild;
        }

        bool changed = !string.Equals(CurrentLordGuild, winnerGuild, StringComparison.OrdinalIgnoreCase);
        CurrentLordGuild = winnerGuild;
        CurrentPhase = SiegePhase.Peace;

        settlementReport = changed
            ? $"【沙城易主】轰动玛法！行会【{winnerGuild}】力克群雄，成功夺下沙巴克，加冕新一任沙城霸主！"
            : $"【霸主连任】傲视群雄！行会【{winnerGuild}】固若金汤，成功卫冕沙巴克，续写不败神话！";

        return true;
    }

    /// <summary>
    /// 每帧更新攻城战时钟与皇宫占领积累
    /// </summary>
    public void Update(float dt, Action<string>? onAutoSettled = null)
    {
        if (CurrentPhase != SiegePhase.Fighting) return;

        RemainingBattleTime -= dt;

        // 每秒累加一次皇宫占领时长 (支持跨帧大步长消费)
        _palaceTickAccumulator += dt;
        while (_palaceTickAccumulator >= 1.0f)
        {
            _palaceTickAccumulator -= 1.0f;
            if (!string.IsNullOrWhiteSpace(PalaceOccupierGuild))
            {
                var score = GetOrCreateGuildScore(PalaceOccupierGuild);
                score.PalaceHoldSeconds++;
            }
        }

        // 倒计时归零，自动终局结算
        if (RemainingBattleTime <= 0f)
        {
            RemainingBattleTime = 0f;
            StopSiege(out string report);
            onAutoSettled?.Invoke(report);
        }
    }

    /// <summary>
    /// 攻击沙巴克城门
    /// </summary>
    public bool DamageGate(string guildName, int damage, out string eventMsg)
    {
        if (CurrentPhase != SiegePhase.Fighting)
        {
            eventMsg = "休战期间沙巴克城门受神圣结界守护，不可攻击！";
            return false;
        }

        if (IsGateDestroyed)
        {
            eventMsg = "沙巴克城门已然轰然倒塌，各路大军可直取皇宫！";
            return false;
        }

        damage = Math.Max(1, damage);
        GateCurrentHp = Math.Max(0, GateCurrentHp - damage);

        var score = GetOrCreateGuildScore(guildName);
        score.GateDamage += damage;

        if (IsGateDestroyed)
        {
            eventMsg = $"【烽火警报】沙巴克城门已被行会【{guildName}】猛烈击溃！皇宫结界解除，争夺大幕拉开！";
        }
        else
        {
            eventMsg = $"【城门攻防】行会【{guildName}】对城门造成 {damage} 点破坏！城门剩余坚固度: {GateCurrentHp}/{GateMaxHp}";
        }

        return true;
    }

    /// <summary>
    /// 行会攻入皇宫大殿并成功夺取中央旗帜
    /// </summary>
    public bool OccupyPalace(string guildName, out string eventMsg)
    {
        if (CurrentPhase != SiegePhase.Fighting)
        {
            eventMsg = "非攻城战期间不可抢占皇宫大殿！";
            return false;
        }

        if (string.IsNullOrWhiteSpace(guildName))
        {
            eventMsg = "无行会人员无法占领皇宫！";
            return false;
        }

        if (string.Equals(PalaceOccupierGuild, guildName, StringComparison.OrdinalIgnoreCase))
        {
            eventMsg = $"皇宫大殿目前正由贵行会【{guildName}】誓死把守！";
            return false;
        }

        string oldOwner = PalaceOccupierGuild;
        PalaceOccupierGuild = guildName;
        GetOrCreateGuildScore(guildName);

        eventMsg = $"【皇宫捷报】行会【{guildName}】成功肃清敌军，夺得皇宫大殿旗帜控制权！(原据守: 【{oldOwner}】)";
        return true;
    }

    /// <summary>
    /// 记录攻城击杀战绩
    /// </summary>
    public void RecordKill(string killerGuild)
    {
        if (CurrentPhase != SiegePhase.Fighting || string.IsNullOrWhiteSpace(killerGuild)) return;
        var score = GetOrCreateGuildScore(killerGuild);
        score.KillCount++;
    }

    /// <summary>
    /// 获取攻城排行榜 (按综合积分降序)
    /// </summary>
    public List<GuildSiegeScore> GetLeaderboard()
    {
        return _guildScores.Values
            .OrderByDescending(s => s.TotalScore)
            .ThenByDescending(s => s.PalaceHoldSeconds)
            .ToList();
    }

    /// <summary>
    /// 判断指定行会是否为当届沙巴克霸主行会
    /// </summary>
    public bool IsSabukLordGuild(string guildName)
    {
        if (string.IsNullOrWhiteSpace(guildName)) return false;
        return string.Equals(CurrentLordGuild, guildName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 为沙巴克行会成员生成金色专属荣耀头顶称号
    /// </summary>
    public string FormatSabukTitle(string guildName, string rankTitle)
    {
        if (!IsSabukLordGuild(guildName)) return "";
        return rankTitle == "会长" ? "【沙巴克·城主】" : "【沙巴克】";
    }

    private GuildSiegeScore GetOrCreateGuildScore(string guildName)
    {
        if (string.IsNullOrWhiteSpace(guildName)) guildName = "散人义军";
        if (!_guildScores.TryGetValue(guildName, out var score))
        {
            score = new GuildSiegeScore(guildName);
            _guildScores[guildName] = score;
        }
        return score;
    }
}
