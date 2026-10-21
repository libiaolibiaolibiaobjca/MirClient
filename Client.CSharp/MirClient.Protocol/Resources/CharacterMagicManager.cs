using System;
using System.Collections.Generic;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 角色技能法术与快捷键绑定状态管理器 (单例)
/// 严格 1:1 对齐 OpenMir2 角色技能体系
/// </summary>
public class CharacterMagicManager
{
    private static readonly Lazy<CharacterMagicManager> _instance = new(() => new CharacterMagicManager());
    public static CharacterMagicManager Instance => _instance.Value;

    // 角色当前掌握的所有法术列表
    private readonly List<ClientMagicData> _magics = new();
    private readonly object _lock = new();

    // 快捷键映射: 槽位 0~7 对应 F1~F8
    // 保存对应槽位绑定的 MagicId (0 表示未绑定)
    private readonly ushort[] _keyBindings = new ushort[8];

    // 技能冷却记录: MagicId -> 下次可施展时间 (秒)
    private readonly Dictionary<ushort, double> _cooldowns = new();

    public event Action? OnMagicListChanged;
    public event Action<int, ushort>? OnKeyBindingChanged;

    public CharacterMagicManager()
    {
        InitDefaultPreset();
    }

    /// <summary>
    /// 初始化默认经典技能配置 (确保单机模式或进服初始时技能面板充实)
    /// </summary>
    public void InitDefaultPreset()
    {
        lock (_lock)
        {
            _magics.Clear();
            Array.Clear(_keyBindings, 0, _keyBindings.Length);

            // 预设战士核心技能
            AddPresetSkill(3, "基本剑术", 0, 0, 0, 0, 3, 1000);
            AddPresetSkill(7, "攻杀剑术", 0, 0, 0, 15, 3, 2000);
            AddPresetSkill(12, "刺杀剑术", 0, 5, 20, 35, 3, 3000);
            AddPresetSkill(25, "半月弯刀", 0, 8, 30, 45, 2, 4000);
            AddPresetSkill(26, "烈火剑法", 0, 18, 50, 75, 3, 5000);

            // 预设法师核心技能
            AddPresetSkill(1, "火球术", 1, 4, 15, 25, 3, 1000);
            AddPresetSkill(11, "雷电术", 1, 14, 45, 75, 3, 1200);
            AddPresetSkill(22, "火墙", 1, 25, 20, 35, 2, 3000);
            AddPresetSkill(31, "魔法盾", 1, 20, 0, 0, 3, 8000);
            AddPresetSkill(33, "冰咆哮", 1, 35, 60, 90, 2, 2500);

            // 预设道士核心技能
            AddPresetSkill(2, "治愈术", 2, 8, 30, 60, 3, 1500);
            AddPresetSkill(6, "施毒术", 2, 10, 10, 20, 2, 2000);
            AddPresetSkill(13, "灵魂火符", 2, 12, 35, 55, 3, 1200);
            AddPresetSkill(30, "召唤神兽", 2, 30, 0, 0, 2, 6000);

            // 默认绑定 F1~F4 基础快捷键
            BindKeyInternal(0, 11); // F1: 雷电术
            BindKeyInternal(1, 26); // F2: 烈火剑法
            BindKeyInternal(2, 2);  // F3: 治愈术
            BindKeyInternal(3, 1);  // F4: 火球术
        }
    }

    private void AddPresetSkill(ushort id, string name, byte job, ushort mp, ushort power, ushort maxPower, byte level, int delayMs)
    {
        var magic = new ClientMagicData
        {
            Level = level,
            CurTrain = 150,
            Def = new MagicInfoData
            {
                MagicId = id,
                MagicName = name,
                Job = job,
                Spell = mp,
                Power = power,
                MaxPower = maxPower,
                DelayTime = delayMs,
                MaxTrain = new int[] { 100, 200, 500, 1000 }
            }
        };
        _magics.Add(magic);
    }

    /// <summary>
    /// 全量同步服务端下发的技能列表 (SM_SENDMYMAGIC)
    /// </summary>
    public void SetServerMagics(List<ClientMagicData> list)
    {
        if (list == null || list.Count == 0) return;

        lock (_lock)
        {
            _magics.Clear();
            _magics.AddRange(list);

            // 读取各技能中记录的快捷键
            Array.Clear(_keyBindings, 0, _keyBindings.Length);
            foreach (var m in list)
            {
                if (m.Key >= '0' && m.Key <= '7')
                {
                    int slot = m.Key - '0';
                    _keyBindings[slot] = m.Def.MagicId;
                }
            }
        }
        OnMagicListChanged?.Invoke();
    }

    /// <summary>
    /// 学习新技能或更新现有技能 (SM_ADDMAGIC)
    /// </summary>
    public void AddOrUpdateMagic(ClientMagicData item)
    {
        if (item == null) return;
        lock (_lock)
        {
            int idx = _magics.FindIndex(m => m.Def.MagicId == item.Def.MagicId);
            if (idx >= 0)
            {
                _magics[idx] = item;
            }
            else
            {
                _magics.Add(item);
            }

            if (item.Key >= '0' && item.Key <= '7')
            {
                int slot = item.Key - '0';
                _keyBindings[slot] = item.Def.MagicId;
            }
        }
        OnMagicListChanged?.Invoke();
    }

    /// <summary>
    /// 移除技能 (SM_DELMAGIC)
    /// </summary>
    public void RemoveMagic(ushort magicId)
    {
        lock (_lock)
        {
            _magics.RemoveAll(m => m.Def.MagicId == magicId);
            for (int i = 0; i < _keyBindings.Length; i++)
            {
                if (_keyBindings[i] == magicId)
                {
                    _keyBindings[i] = 0;
                    OnKeyBindingChanged?.Invoke(i, 0);
                }
            }
        }
        OnMagicListChanged?.Invoke();
    }

    /// <summary>
    /// 获取当前角色拥有的全部技能只读快照
    /// </summary>
    public IReadOnlyList<ClientMagicData> GetAllMagics()
    {
        lock (_lock)
        {
            return _magics.ToArray();
        }
    }

    /// <summary>
    /// 获取指定快捷键槽位绑定的技能 (slot: 0~7 对应 F1~F8)
    /// </summary>
    public ClientMagicData? GetMagicBySlot(int slot)
    {
        if (slot < 0 || slot >= _keyBindings.Length) return null;
        lock (_lock)
        {
            ushort id = _keyBindings[slot];
            if (id == 0) return null;
            return _magics.Find(m => m.Def.MagicId == id);
        }
    }

    /// <summary>
    /// 获取某技能目前绑定的快捷键位 (返回 0~7 对应 F1~F8，无绑定则返回 -1)
    /// </summary>
    public int GetBoundSlot(ushort magicId)
    {
        lock (_lock)
        {
            for (int i = 0; i < _keyBindings.Length; i++)
            {
                if (_keyBindings[i] == magicId) return i;
            }
            return -1;
        }
    }

    /// <summary>
    /// 将指定法术绑定到快捷槽位 (slot: 0~7 对应 F1~F8)
    /// </summary>
    public void BindKey(int slot, ushort magicId)
    {
        if (slot < 0 || slot >= _keyBindings.Length) return;
        lock (_lock)
        {
            // 若该技能已被绑定在其它槽位，先解绑其它槽位
            for (int i = 0; i < _keyBindings.Length; i++)
            {
                if (_keyBindings[i] == magicId && i != slot)
                {
                    _keyBindings[i] = 0;
                    OnKeyBindingChanged?.Invoke(i, 0);
                }
            }

            _keyBindings[slot] = magicId;
            var magic = _magics.Find(m => m.Def.MagicId == magicId);
            if (magic != null)
            {
                magic.Key = (char)('0' + slot);
            }
        }
        OnKeyBindingChanged?.Invoke(slot, magicId);
    }

    /// <summary>
    /// 解除指定槽位的按键绑定
    /// </summary>
    public void UnbindSlot(int slot)
    {
        if (slot < 0 || slot >= _keyBindings.Length) return;
        lock (_lock)
        {
            ushort oldId = _keyBindings[slot];
            _keyBindings[slot] = 0;
            var magic = _magics.Find(m => m.Def.MagicId == oldId);
            if (magic != null)
            {
                magic.Key = (char)0;
            }
        }
        OnKeyBindingChanged?.Invoke(slot, 0);
    }

    private void BindKeyInternal(int slot, ushort magicId)
    {
        _keyBindings[slot] = magicId;
        var magic = _magics.Find(m => m.Def.MagicId == magicId);
        if (magic != null)
        {
            magic.Key = (char)('0' + slot);
        }
    }

    /// <summary>
    /// 校验技能是否处于冷却中
    /// </summary>
    public bool CanCast(ushort magicId, double currentTime, out float remainSec)
    {
        lock (_lock)
        {
            if (_cooldowns.TryGetValue(magicId, out double readyTime))
            {
                if (currentTime < readyTime)
                {
                    remainSec = (float)(readyTime - currentTime);
                    return false;
                }
            }
            remainSec = 0f;
            return true;
        }
    }

    /// <summary>
    /// 触发技能冷却
    /// </summary>
    public void TriggerCooldown(ushort magicId, double currentTime)
    {
        lock (_lock)
        {
            var magic = _magics.Find(m => m.Def.MagicId == magicId);
            int delayMs = (magic != null && magic.Def.DelayTime > 0) ? magic.Def.DelayTime : 1000;
            _cooldowns[magicId] = currentTime + (delayMs / 1000.0);
        }
    }
}
