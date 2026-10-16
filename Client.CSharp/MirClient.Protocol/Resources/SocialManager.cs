namespace MirClient.Protocol.Resources;

/// <summary>
/// 好友信息模型
/// </summary>
public class FriendInfo
{
    public string Name { get; set; } = "";
    public CharacterJob Job { get; set; } = CharacterJob.Warrior;
    public int Level { get; set; } = 1;
    public string MapName { get; set; } = "比奇城";
    public bool IsOnline { get; set; } = true;

    public string JobName => Job switch
    {
        CharacterJob.Warrior => "战士",
        CharacterJob.Wizard  => "法师",
        CharacterJob.Taoist  => "道士",
        _                    => "无"
    };

    public FriendInfo() { }

    public FriendInfo(string name, CharacterJob job, int level, string mapName = "比奇城", bool isOnline = true)
    {
        Name = name;
        Job = job;
        Level = level;
        MapName = mapName;
        IsOnline = isOnline;
    }
}

/// <summary>
/// 徒弟信息模型
/// </summary>
public class ApprenticeInfo
{
    public string Name { get; set; } = "";
    public CharacterJob Job { get; set; } = CharacterJob.Warrior;
    public int Level { get; set; } = 1;
    public bool IsGraduated { get; set; } = false;

    public string JobName => Job switch
    {
        CharacterJob.Warrior => "战士",
        CharacterJob.Wizard  => "法师",
        CharacterJob.Taoist  => "道士",
        _                    => "无"
    };

    public ApprenticeInfo() { }

    public ApprenticeInfo(string name, CharacterJob job, int level, bool isGraduated = false)
    {
        Name = name;
        Job = job;
        Level = level;
        IsGraduated = isGraduated;
    }
}

/// <summary>
/// 盛大传奇十周年：师徒武馆与好友亲密度社交管理器 (纯 C# 领域状态机)
/// </summary>
public class SocialManager
{
    private static SocialManager? _instance;
    public static SocialManager Instance => _instance ??= new SocialManager();

    public const int MaxFriendsCount = 50;
    public const int MaxApprenticesCount = 3;
    public const int ApprenticeMaxJoinLevel = 28;
    public const int MasterMinLevel = 35;
    public const int GraduationMinLevel = 35;

    public int Reputation { get; private set; } = 0;
    public string? MasterName { get; private set; } = null;
    public bool IsGraduated { get; private set; } = false;

    private readonly List<FriendInfo> _friends = new();
    private readonly List<ApprenticeInfo> _apprentices = new();

    public IReadOnlyList<FriendInfo> Friends => _friends.AsReadOnly();
    public IReadOnlyList<ApprenticeInfo> Apprentices => _apprentices.AsReadOnly();

    public event Action<string, bool>? OnFriendOnlineStateChanged;
    public event Action<string, int>? OnApprenticeLeveledUp;
    public event Action? OnFriendListUpdated;
    public event Action? OnMasterListUpdated;
    public event Action<string>? OnSocialNotice;

    public void Reset()
    {
        Reputation = 0;
        MasterName = null;
        IsGraduated = false;
        _friends.Clear();
        _apprentices.Clear();
    }

    public void AddReputation(int amount)
    {
        Reputation = Math.Max(0, Reputation + amount);
    }

    public bool ConsumeReputation(int amount)
    {
        if (amount <= 0) return true;
        if (Reputation < amount) return false;
        Reputation -= amount;
        return true;
    }

    #region 好友管理

    public (bool Success, string Message) AddFriend(string name, CharacterJob job, int level, string mapName = "比奇城", bool isOnline = true)
    {
        if (string.IsNullOrWhiteSpace(name))
            return (false, "好友名字不能为空。");

        if (_friends.Count >= MaxFriendsCount)
            return (false, $"好友数量已达上限 ({MaxFriendsCount}人)。");

        if (_friends.Any(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return (false, $"【{name}】已在您的好友列表中。");

        var friend = new FriendInfo(name, job, level, mapName, isOnline);
        _friends.Add(friend);
        return (true, $"已成功添加【{name}】为好友。");
    }

    public (bool Success, string Message) RemoveFriend(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return (false, "好友名字不能为空。");

        var friend = _friends.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (friend == null)
            return (false, $"好友列表中未找到【{name}】。");

        _friends.Remove(friend);
        return (true, $"已将【{name}】从好友列表中移除。");
    }

    public FriendInfo? GetFriend(string name)
    {
        return _friends.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public (bool Success, string Message) SetFriendOnline(string name, bool isOnline, string? mapName = null)
    {
        var friend = GetFriend(name);
        if (friend == null)
            return (false, $"未找到好友【{name}】。");

        friend.IsOnline = isOnline;
        if (!string.IsNullOrEmpty(mapName))
        {
            friend.MapName = mapName;
        }

        OnFriendOnlineStateChanged?.Invoke(name, isOnline);
        var statusStr = isOnline ? "上线了" : "下线了";
        return (true, $"您的好友【{name}】{statusStr}。");
    }

    #endregion

    #region 师徒武馆

    public (bool Success, string Message) BecomeApprentice(string masterName, int myLevel)
    {
        if (string.IsNullOrWhiteSpace(masterName))
            return (false, "师父名字不能为空。");

        if (!string.IsNullOrEmpty(MasterName))
            return (false, $"您已有师父【{MasterName}】，不可重复拜师。");

        if (myLevel > ApprenticeMaxJoinLevel)
            return (false, $"您已达到 {myLevel} 级，超过拜师等级限制 ({ApprenticeMaxJoinLevel} 级)。");

        MasterName = masterName;
        return (true, $"拜师成功！您已拜入【{masterName}】门下，行走江湖请尊师重道。");
    }

    public (bool Success, string Message) AcceptApprentice(string apprenticeName, CharacterJob job, int apprenticeLevel, int myLevel)
    {
        if (string.IsNullOrWhiteSpace(apprenticeName))
            return (false, "徒弟名字不能为空。");

        if (myLevel < MasterMinLevel && !IsGraduated)
            return (false, $"您尚未达到 {MasterMinLevel} 级且未出师，暂无招收门徒资格。");

        var activeApprentices = _apprentices.Count(a => !a.IsGraduated);
        if (activeApprentices >= MaxApprenticesCount)
            return (false, $"您门下当前已有 {activeApprentices} 名未出师弟子，招收已达上限 ({MaxApprenticesCount} 人)。");

        if (apprenticeLevel > ApprenticeMaxJoinLevel)
            return (false, $"该侠士等级已达 {apprenticeLevel} 级，不可收为弟子。");

        if (_apprentices.Any(a => a.Name.Equals(apprenticeName, StringComparison.OrdinalIgnoreCase)))
            return (false, $"【{apprenticeName}】已在您的门下。");

        var apprentice = new ApprenticeInfo(apprenticeName, job, apprenticeLevel, false);
        _apprentices.Add(apprentice);
        return (true, $"收徒成功！【{apprenticeName}】正式拜入您的门下。");
    }

    public (bool Success, string Message, int RepGain) OnApprenticeLevelUp(string apprenticeName, int newLevel)
    {
        var apprentice = _apprentices.FirstOrDefault(a => a.Name.Equals(apprenticeName, StringComparison.OrdinalIgnoreCase));
        if (apprentice == null)
            return (false, $"【{apprenticeName}】非您门下弟子。", 0);

        if (newLevel <= apprentice.Level)
            return (false, "等级未发生提升。", 0);

        int levelDiff = newLevel - apprentice.Level;
        apprentice.Level = newLevel;

        int repGain = levelDiff * 2;
        Reputation += repGain;

        OnApprenticeLeveledUp?.Invoke(apprenticeName, newLevel);
        return (true, $"弟子【{apprenticeName}】勤学苦练升至 {newLevel} 级！师父声望增加 {repGain} 点。", repGain);
    }

    public (bool Success, string Message, int MasterRepReward, int ApprenticeRepReward) GraduateApprentice(string apprenticeName)
    {
        var apprentice = _apprentices.FirstOrDefault(a => a.Name.Equals(apprenticeName, StringComparison.OrdinalIgnoreCase));
        if (apprentice == null)
            return (false, $"未在门下找到弟子【{apprenticeName}】。", 0, 0);

        if (apprentice.IsGraduated)
            return (false, $"弟子【{apprenticeName}】早已功德圆满出师。", 0, 0);

        if (apprentice.Level < GraduationMinLevel)
            return (false, $"弟子【{apprenticeName}】当前 {apprentice.Level} 级，尚未达到出师标准 ({GraduationMinLevel} 级)。", 0, 0);

        apprentice.IsGraduated = true;
        const int masterReward = 30;
        const int apprenticeReward = 15;

        Reputation += masterReward;
        return (true, $"恭贺！弟子【{apprenticeName}】武艺卓绝，正式出师！师父荣获 {masterReward} 点声望与武馆贺礼！", masterReward, apprenticeReward);
    }

    public (bool Success, string Message, int RepReward) SelfGraduate(int myLevel)
    {
        if (string.IsNullOrEmpty(MasterName))
            return (false, "您暂无师门传承，无法举行出师礼。", 0);

        if (IsGraduated)
            return (false, "您早已功德圆满出师，成为一代豪侠。", 0);

        if (myLevel < GraduationMinLevel)
            return (false, $"您的武艺等级 ({myLevel}级) 尚未达到出师标准 ({GraduationMinLevel}级)。", 0);

        IsGraduated = true;
        const int repReward = 15;
        Reputation += repReward;
        return (true, $"比奇武馆钟鼓齐鸣！您已正式出师，名扬玛法！获得 {repReward} 点江湖声望！", repReward);
    }

    public (bool Success, string Message) ExpelApprentice(string apprenticeName)
    {
        var apprentice = _apprentices.FirstOrDefault(a => a.Name.Equals(apprenticeName, StringComparison.OrdinalIgnoreCase));
        if (apprentice == null)
            return (false, $"未在门下找到弟子【{apprenticeName}】。");

        _apprentices.Remove(apprentice);
        return (true, $"您已将【{apprenticeName}】逐出师门，自此恩断义绝。");
    }

    public (bool Success, string Message) LeaveMaster()
    {
        if (string.IsNullOrEmpty(MasterName))
            return (false, "您当前并未拜入任何师门。");

        var oldMaster = MasterName;
        MasterName = null;
        return (true, $"您已脱离【{oldMaster}】师门，从此独自闯荡玛法大陆。");
    }

    #endregion

    #region 服务端网络同步与广播

    /// <summary>
    /// 全量同步服务端下发的好友列表 (SM_FRIENDLIST)
    /// </summary>
    public void SyncServerFriendList(IEnumerable<FriendInfo> friends)
    {
        _friends.Clear();
        if (friends != null)
        {
            foreach (var f in friends)
            {
                if (_friends.Count >= MaxFriendsCount) break;
                _friends.Add(f);
            }
        }
        OnFriendListUpdated?.Invoke();
    }

    /// <summary>
    /// 同步服务端好友实时上下线状态 (SM_FRIEND_ONOFF)
    /// </summary>
    public void SyncServerFriendOnOff(string name, bool isOnline, string? mapName)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        var friend = GetFriend(name);
        if (friend != null)
        {
            friend.IsOnline = isOnline;
            if (!string.IsNullOrEmpty(mapName))
            {
                friend.MapName = mapName;
            }
            OnFriendOnlineStateChanged?.Invoke(name, isOnline);
        }
    }

    /// <summary>
    /// 全量同步服务端下发的师门与门徒名册 (SM_MASTER_LIST)
    /// </summary>
    public void SyncServerMasterList(string? masterName, bool isGraduated, int reputation, IEnumerable<ApprenticeInfo> apprentices)
    {
        MasterName = string.IsNullOrWhiteSpace(masterName) ? null : masterName;
        IsGraduated = isGraduated;
        Reputation = Math.Max(0, reputation);

        _apprentices.Clear();
        if (apprentices != null)
        {
            foreach (var a in apprentices)
            {
                _apprentices.Add(a);
            }
        }
        OnMasterListUpdated?.Invoke();
    }

    /// <summary>
    /// 同步服务端门下弟子升级广播 (SM_APPRENTICE_LEVELUP)
    /// </summary>
    public void SyncServerApprenticeLevelUp(string apprenticeName, int newLevel, int repGain)
    {
        if (string.IsNullOrWhiteSpace(apprenticeName)) return;

        var apprentice = _apprentices.FirstOrDefault(a => a.Name.Equals(apprenticeName, StringComparison.OrdinalIgnoreCase));
        if (apprentice != null)
        {
            apprentice.Level = Math.Max(apprentice.Level, newLevel);
        }

        if (repGain > 0)
        {
            Reputation += repGain;
        }

        OnApprenticeLeveledUp?.Invoke(apprenticeName, newLevel);
        OnMasterListUpdated?.Invoke();
    }

    #endregion
}
