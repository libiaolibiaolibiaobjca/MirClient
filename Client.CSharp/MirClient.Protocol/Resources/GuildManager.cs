namespace MirClient.Protocol.Resources;

/// <summary>
/// 行会职务等级枚举
/// </summary>
public enum GuildRank
{
    Leader = 0,     // 会长
    ViceLeader = 1, // 副会长
    Elder = 2,      // 堂主
    Elite = 3,      // 精英
    Member = 4      // 帮众
}

/// <summary>
/// 行会成员模型
/// </summary>
public class GuildMember
{
    public string Name { get; set; } = "";
    public CharacterJob Job { get; set; } = CharacterJob.Warrior;
    public int Level { get; set; } = 1;
    public GuildRank Rank { get; set; } = GuildRank.Member;

    public string RankTitle => Rank switch
    {
        GuildRank.Leader     => "会长",
        GuildRank.ViceLeader => "副会长",
        GuildRank.Elder      => "堂主",
        GuildRank.Elite      => "精英",
        _                    => "帮众"
    };

    public string JobName => Job switch
    {
        CharacterJob.Warrior => "战士",
        CharacterJob.Wizard  => "法师",
        CharacterJob.Taoist  => "道士",
        _                    => "无"
    };

    public GuildMember() { }

    public GuildMember(string name, CharacterJob job, int level, GuildRank rank = GuildRank.Member)
    {
        Name = name;
        Job = job;
        Level = level;
        Rank = rank;
    }
}

/// <summary>
/// 盛大传奇十周年：行会与称号系统管理器 (纯 C# 领域状态机)
/// </summary>
public class GuildManager
{
    private static GuildManager? _instance;
    public static GuildManager Instance => _instance ??= new GuildManager();

    public const int MAX_MEMBERS = 100; // 经典行会人数上限

    public string GuildName { get; private set; } = "";
    public string Notice { get; set; } = "扬我国威，共铸玛法霸业！沙巴克荣耀属于我们！";
    public int Fund { get; set; } = 100000; // 行会资金

    private readonly List<GuildMember> _members = new();
    public IReadOnlyList<GuildMember> Members => _members;

    public bool HasGuild => !string.IsNullOrEmpty(GuildName);
    public GuildMember? Leader => _members.FirstOrDefault(m => m.Rank == GuildRank.Leader);

    /// <summary>
    /// 获取指定角色的行会头顶称号字符串 (如 "【傲世天下】会长")
    /// </summary>
    public string GetTitleForPlayer(string playerName)
    {
        if (!HasGuild) return "";
        var member = _members.FirstOrDefault(m => m.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));
        if (member == null) return "";
        return $"【{GuildName}】{member.RankTitle}";
    }

    /// <summary>
    /// 创建新行会并自任会长
    /// </summary>
    public bool CreateGuild(string guildName, string founderName, CharacterJob job, int level)
    {
        if (string.IsNullOrWhiteSpace(guildName) || string.IsNullOrWhiteSpace(founderName)) return false;

        GuildName = guildName.Trim();
        _members.Clear();
        _members.Add(new GuildMember(founderName.Trim(), job, level, GuildRank.Leader));
        return true;
    }

    /// <summary>
    /// 添加新成员
    /// </summary>
    public bool AddMember(string name, CharacterJob job, int level, GuildRank rank = GuildRank.Member)
    {
        if (!HasGuild) return false;
        if (_members.Count >= MAX_MEMBERS) return false;
        if (_members.Any(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) return false;

        _members.Add(new GuildMember(name.Trim(), job, level, rank));
        return true;
    }

    /// <summary>
    /// 逐出或移除成员
    /// </summary>
    public bool RemoveMember(string name)
    {
        if (!HasGuild) return false;
        int index = _members.FindIndex(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return false;

        bool wasLeader = _members[index].Rank == GuildRank.Leader;
        _members.RemoveAt(index);

        if (wasLeader)
        {
            // 会长离开，自动解散行会
            LeaveGuild();
        }

        return true;
    }

    /// <summary>
    /// 任命成员职务
    /// </summary>
    public bool SetMemberRank(string name, GuildRank newRank)
    {
        if (!HasGuild) return false;
        var member = _members.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (member == null) return false;

        if (newRank == GuildRank.Leader)
        {
            // 若任命新会长，原会长降为副会长
            foreach (var m in _members)
            {
                if (m.Rank == GuildRank.Leader) m.Rank = GuildRank.ViceLeader;
            }
        }

        member.Rank = newRank;
        return true;
    }

    /// <summary>
    /// 退出或解散行会
    /// </summary>
    public void LeaveGuild()
    {
        GuildName = "";
        _members.Clear();
    }

    /// <summary>
    /// 判断玩家是否为行会成员
    /// </summary>
    public bool IsMember(string name)
    {
        return _members.Any(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 查询成员职务
    /// </summary>
    public GuildRank GetMemberRank(string name)
    {
        var m = _members.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return m?.Rank ?? GuildRank.Member;
    }

    /// <summary>
    /// 从服务端同步行会名册 (SM_SENDGUILDMEMBERLIST = 756)
    /// </summary>
    public void SyncServerRanks(IReadOnlyList<GuildRankEntry> ranks)
    {
        if (ranks == null || ranks.Count == 0) return;

        var oldDict = _members.ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);
        _members.Clear();

        foreach (var r in ranks)
        {
            GuildRank rankEnum = r.RankNo switch
            {
                0 => GuildRank.Leader,
                1 => GuildRank.ViceLeader,
                2 => GuildRank.Elder,
                3 => GuildRank.Elite,
                _ => GuildRank.Member
            };

            foreach (var memberName in r.Members)
            {
                if (oldDict.TryGetValue(memberName, out var oldMember))
                {
                    oldMember.Rank = rankEnum;
                    _members.Add(oldMember);
                }
                else
                {
                    _members.Add(new GuildMember(memberName, CharacterJob.Warrior, 35, rankEnum));
                }
            }
        }
    }

    /// <summary>
    /// 从服务端同步行会基础信息与公告
    /// </summary>
    public void SyncGuildInfo(string guildName, string notice)
    {
        if (!string.IsNullOrWhiteSpace(guildName))
        {
            GuildName = guildName.Trim();
        }
        if (!string.IsNullOrWhiteSpace(notice))
        {
            Notice = notice.Trim();
        }
    }

    /// <summary>
    /// 重置状态（测试使用）
    /// </summary>
    public void Reset()
    {
        GuildName = "";
        Notice = "扬我国威，共铸玛法霸业！沙巴克荣耀属于我们！";
        Fund = 100000;
        _members.Clear();
    }
}
