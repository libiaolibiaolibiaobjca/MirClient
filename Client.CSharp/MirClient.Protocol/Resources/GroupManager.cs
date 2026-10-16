namespace MirClient.Protocol.Resources;

/// <summary>
/// 盛大热血传奇职业类型枚举
/// </summary>
public enum CharacterJob
{
    Warrior = 0, // 战士
    Wizard = 1,  // 法师
    Taoist = 2   // 道士
}

/// <summary>
/// 组队队伍成员模型
/// </summary>
public class GroupMember
{
    public string Name { get; set; } = "";
    public CharacterJob Job { get; set; } = CharacterJob.Warrior;
    public int Level { get; set; } = 1;
    public float CurrentHp { get; set; } = 100f;
    public float MaxHp { get; set; } = 100f;
    public string MapName { get; set; } = "比奇城";
    public bool IsLeader { get; set; } = false;

    public float HpPercent => MaxHp > 0 ? Math.Clamp(CurrentHp / MaxHp, 0f, 1f) : 0f;

    public string JobName => Job switch
    {
        CharacterJob.Warrior => "战士",
        CharacterJob.Wizard  => "法师",
        CharacterJob.Taoist  => "道士",
        _ => "无"
    };

    public GroupMember() { }

    public GroupMember(string name, CharacterJob job, int level, float hp, float maxHp, string mapName, bool isLeader = false)
    {
        Name = name;
        Job = job;
        Level = level;
        CurrentHp = hp;
        MaxHp = maxHp;
        MapName = mapName;
        IsLeader = isLeader;
    }
}

/// <summary>
/// 盛大传奇十周年：组队系统管理器 (纯 C# 领域状态机，解耦表现层)
/// </summary>
public class GroupManager
{
    private static GroupManager? _instance;
    public static GroupManager Instance => _instance ??= new GroupManager();

    public const int MAX_MEMBERS = 11; // 盛大原版队伍上限 11 人

    private readonly List<GroupMember> _members = new();
    public IReadOnlyList<GroupMember> Members => _members;

    // 是否允许接收组队邀请
    public bool AllowGroup { get; set; } = true;

    // 当前是否已处于组队状态
    public bool HasGroup => _members.Count > 0;

    // 获取当前队伍队长
    public GroupMember? Leader => _members.FirstOrDefault(m => m.IsLeader);

    /// <summary>
    /// 创建队伍并自任队长
    /// </summary>
    public bool CreateGroup(string leaderName, CharacterJob job, int level, float hp, float maxHp, string mapName)
    {
        _members.Clear();
        _members.Add(new GroupMember(leaderName, job, level, hp, maxHp, mapName, isLeader: true));
        return true;
    }

    /// <summary>
    /// 添加新队员 (若尚未建队，则先建队)
    /// </summary>
    public bool AddMember(string name, CharacterJob job, int level, float hp, float maxHp, string mapName)
    {
        if (_members.Count >= MAX_MEMBERS) return false;
        if (_members.Any(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) return false;

        bool isLeader = _members.Count == 0;
        _members.Add(new GroupMember(name, job, level, hp, maxHp, mapName, isLeader));
        return true;
    }

    /// <summary>
    /// 移除或踢出指定队员 (若移除的是队长且队伍仍有人，自动顺延第1个队员为新队长)
    /// </summary>
    public bool RemoveMember(string name)
    {
        int index = _members.FindIndex(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return false;

        bool wasLeader = _members[index].IsLeader;
        _members.RemoveAt(index);

        if (_members.Count > 0 && wasLeader)
        {
            _members[0].IsLeader = true;
        }

        return true;
    }

    /// <summary>
    /// 移交队长权限给指定队员
    /// </summary>
    public bool TransferLeader(string targetName)
    {
        var target = _members.FirstOrDefault(m => m.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase));
        if (target == null || target.IsLeader) return false;

        foreach (var m in _members)
        {
            m.IsLeader = false;
        }
        target.IsLeader = true;
        return true;
    }

    /// <summary>
    /// 退出队伍 / 解散队伍
    /// </summary>
    public void LeaveGroup()
    {
        _members.Clear();
    }

    /// <summary>
    /// 从 OpenMir2 服务端同步队伍成员名单 (SM_GROUPMEMBERS = 667)
    /// 服务端首个成员为队长
    /// </summary>
    public void SyncServerMembers(IReadOnlyList<string> serverMemberNames, string localPlayerName = "")
    {
        if (serverMemberNames == null || serverMemberNames.Count == 0)
        {
            _members.Clear();
            return;
        }

        var oldDict = _members.ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);
        _members.Clear();

        for (int i = 0; i < serverMemberNames.Count; i++)
        {
            string name = serverMemberNames[i];
            bool isLeader = (i == 0);

            if (oldDict.TryGetValue(name, out var existing))
            {
                existing.IsLeader = isLeader;
                _members.Add(existing);
            }
            else
            {
                _members.Add(new GroupMember(name, CharacterJob.Warrior, 1, 100f, 100f, "比奇城", isLeader));
            }
        }
    }

    /// <summary>
    /// 更新指定队员的实时生命值与所在地图
    /// </summary>
    public void UpdateMemberStatus(string name, float currentHp, float maxHp, string mapName)
    {
        var member = _members.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (member != null)
        {
            member.CurrentHp = Math.Max(0, currentHp);
            member.MaxHp = Math.Max(1, maxHp);
            if (!string.IsNullOrEmpty(mapName))
            {
                member.MapName = mapName;
            }
        }
    }

    /// <summary>
    /// 计算组队打怪经验加成与均分
    /// 规则：单人无加成；组队时总经验池享有 120% (1.2x) 组队加成，由全体队员平分
    /// </summary>
    /// <param name="baseExp">击杀怪物的基础经验</param>
    /// <param name="expPerMember">每名队员实际分得的经验</param>
    /// <returns>队伍总经验池</returns>
    public long CalculateSharedExp(long baseExp, out long expPerMember)
    {
        if (_members.Count <= 1)
        {
            expPerMember = baseExp;
            return baseExp;
        }

        // 120% 组队经验加成 (KISS 原则经典算法)
        long totalPool = (long)Math.Round(baseExp * 1.2);
        expPerMember = Math.Max(1, totalPool / _members.Count);
        return totalPool;
    }

    /// <summary>
    /// 重置状态（测试使用）
    /// </summary>
    public void Reset()
    {
        _members.Clear();
        AllowGroup = true;
    }
}
